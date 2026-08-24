using System;
using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.AI;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Empire;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Diagnostic-only harness (GPT's call, 2026-08-24): before any further gate-percentage tuning,
    /// distinguish "too few legal opportunities" from "opportunities exist but can't be
    /// afforded/used" per tier. Runs the same AI-casting(on,off) scenario shape as
    /// MirroredAiSimulationMatrixTests but calls AISpellCaster.DiagnoseCandidates every tick
    /// (classifying every spellbook entry, not just the first legal match) instead of asserting
    /// against the locked bands. No pass/fail assertions here by design - this is measurement, not
    /// a gate. Real production code only (BattleController, AISpellCaster, AIEnemySpellbookResolver
    /// via enemyTier), same as the locked matrix.
    /// </summary>
    public class AiSpellCastCandidateDiagnosticTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            LaneBattleResolver.ResetRulesToDefault();
            foreach (GameObject go in _spawned)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
            _spawned.Clear();
        }

        private CardDatabase LoadDatabase()
        {
            var go = new GameObject("DiagCardDatabase");
            _spawned.Add(go);
            CardDatabase db = go.AddComponent<CardDatabase>();
            db.Initialize();
            return db;
        }

        private BattleController CreateController()
        {
            var go = new GameObject("DiagBattleController");
            _spawned.Add(go);
            return go.AddComponent<BattleController>();
        }

        private static void DeployWholeSquad(BattleController controller, PlayerBattleState side, AIArchetype archetype)
        {
            bool placed = true;
            while (placed)
            {
                placed = false;
                foreach (Card card in side.Hand.OrderByDescending(c => c.Attack + c.Health).ToList())
                {
                    if (card.ResourceCost > side.Resource) continue;
                    foreach (Lane lane in new[] { Lane.Front, Lane.Middle, Lane.Back })
                    {
                        if (!side.Lanes[lane].HasRoomFor(card)) continue;
                        if (controller.TryPlayCard(side, card, lane)) { placed = true; break; }
                    }
                    if (placed) break;
                }
            }
        }

        private enum TierGroup { Novice, Apprentice, VeteranPlus }

        private static (int avatarLevel, int castleLevel, AIDifficultyTier tier) GroupConfig(TierGroup g) => g switch
        {
            TierGroup.Novice => (10, 10, AIDifficultyTier.Novice),
            TierGroup.Apprentice => (25, 15, AIDifficultyTier.Apprentice),
            TierGroup.VeteranPlus => (50, 30, AIDifficultyTier.Veteran),
            _ => throw new ArgumentOutOfRangeException(nameof(g)),
        };

        private sealed class Diagnostics
        {
            public int Trials;
            public long TotalTicks;
            public long TicksObserved;
            public long OrdinaryAvailableTicks;
            public long OrdinaryRejectedEnergySum;
            public long OrdinaryRejectedCooldownSum;
            public long OrdinaryRejectedTargetSum;
            public long AvatarStrikeAvailableTicks;
            public long AvatarStrikeRejectedEnergyTicks;
            public long AvatarStrikeRejectedCooldownTicks;
            public long AvatarStrikeRejectedTargetTicks;
            public long EnergySumOnOrdinaryAvailableTicks;
            public long EnergySumAllTicks;
            public int OrdinaryRollAttempts;
            public int OrdinaryRollSuccesses;
            public int AvatarStrikeRollAttempts;
            public int AvatarStrikeRollSuccesses;

            public void Log(string label)
            {
                double avgEnergyAll = TicksObserved == 0 ? 0 : (double)EnergySumAllTicks / TicksObserved;
                double avgEnergyOnAvailable = OrdinaryAvailableTicks == 0 ? 0 : (double)EnergySumOnOrdinaryAvailableTicks / OrdinaryAvailableTicks;
                Debug.Log($"[CandidateDiag] {label}: trials={Trials} ticksObserved={TicksObserved} avgTicksPerMatch={(Trials == 0 ? 0 : (double)TotalTicks / Trials):F2}");
                Debug.Log($"[CandidateDiag] {label}: ORDINARY availableTicks={OrdinaryAvailableTicks} ({(TicksObserved == 0 ? 0 : (double)OrdinaryAvailableTicks / TicksObserved):P1} of ticks) " +
                          $"rejectedEnergy(sum)={OrdinaryRejectedEnergySum} rejectedCooldown(sum)={OrdinaryRejectedCooldownSum} rejectedTarget(sum)={OrdinaryRejectedTargetSum}");
                Debug.Log($"[CandidateDiag] {label}: ORDINARY rollAttempts={OrdinaryRollAttempts} rollSuccesses={OrdinaryRollSuccesses} " +
                          $"(observed roll-success rate={(OrdinaryRollAttempts == 0 ? 0 : (double)OrdinaryRollSuccesses / OrdinaryRollAttempts):P1})");
                Debug.Log($"[CandidateDiag] {label}: AVATARSTRIKE availableTicks={AvatarStrikeAvailableTicks} rejectedEnergyTicks={AvatarStrikeRejectedEnergyTicks} " +
                          $"rejectedCooldownTicks={AvatarStrikeRejectedCooldownTicks} rejectedTargetTicks={AvatarStrikeRejectedTargetTicks} " +
                          $"rollAttempts={AvatarStrikeRollAttempts} rollSuccesses={AvatarStrikeRollSuccesses}");
                Debug.Log($"[CandidateDiag] {label}: avgEnergy(allTicks)={avgEnergyAll:F1} avgEnergy(onOrdinaryAvailableTicks)={avgEnergyOnAvailable:F1}");
            }
        }

        private Diagnostics RunDiagnostic(List<Card> pool, TierGroup group, int trials)
        {
            (int avatarLevel, int castleLevel, AIDifficultyTier tier) = GroupConfig(group);
            var empire = new PlayerEmpireData();
            empire.SetLevelsForTesting(avatarLevel, castleLevel, barracksLevel: 25);
            empire.InitializeTCGModifiers();
            var economy = new BattleController.MatchEconomy(empire.ResourceCap, empire.Turn1Resource, empire.StartingAvatarHealth);
            List<string> equippedIds = AIEnemySpellbookResolver.ResolveSpellbook(tier).Select(s => s.Id).ToList();

            var diag = new Diagnostics();

            for (int i = 0; i < trials; i++)
            {
                if (i % 200 == 0) Debug.Log($"[CandidateDiag] {group}: trial {i}/{trials}");

                BattleController controller = CreateController();
                List<Card> playerDeck = pool.OrderBy(_ => UnityEngine.Random.value).Take(empire.DeckSlotCount).ToList();
                List<Card> enemyDeck = pool.OrderBy(_ => UnityEngine.Random.value).Take(empire.DeckSlotCount).ToList();

                controller.StartMatch(playerDeck, enemyDeck, economy, economy,
                    avatarLevel, unlockedStageIds: null, equippedSpellIds: equippedIds, enemyTier: tier);
                controller.EnableMirroredEnemySpellsForPvE();

                controller.DealFormationHand(controller.PlayerState);
                controller.DealFormationHand(controller.EnemyState);
                DeployWholeSquad(controller, controller.PlayerState, AIArchetype.Balanced);
                SimpleAIOpponent.TakeTurn(controller, AIArchetype.Balanced);

                if (!controller.ConfirmFormation())
                {
                    UnityEngine.Object.DestroyImmediate(controller.gameObject);
                    continue;
                }

                while (controller.Phase == BattlePhase.Combat)
                {
                    int predictedTick = controller.TickCount + 1;
                    int predictedEnergy = Math.Min(controller.MaxEnergy,
                        controller.EnemyEnergy + controller.EnergyPerTick + BattleController.BackLaneEnergy(controller.EnemyState));

                    AISpellCaster.DiagnoseCandidates(
                        controller.EnemySpellbook, predictedEnergy, predictedTick,
                        controller.EnemyState, controller.PlayerState,
                        out int ordinaryAvailable, out int ordinaryRejEnergy, out int ordinaryRejCooldown, out int ordinaryRejTarget,
                        out bool avatarStrikeAvailable, out bool avatarStrikeRejEnergy, out bool avatarStrikeRejCooldown, out bool avatarStrikeRejTarget);

                    diag.TicksObserved++;
                    diag.EnergySumAllTicks += predictedEnergy;
                    diag.OrdinaryRejectedEnergySum += ordinaryRejEnergy;
                    diag.OrdinaryRejectedCooldownSum += ordinaryRejCooldown;
                    diag.OrdinaryRejectedTargetSum += ordinaryRejTarget;
                    if (ordinaryAvailable > 0)
                    {
                        diag.OrdinaryAvailableTicks++;
                        diag.EnergySumOnOrdinaryAvailableTicks += predictedEnergy;
                        diag.OrdinaryRollAttempts++;
                    }
                    if (avatarStrikeAvailable) { diag.AvatarStrikeAvailableTicks++; diag.AvatarStrikeRollAttempts++; }
                    if (avatarStrikeRejEnergy) diag.AvatarStrikeRejectedEnergyTicks++;
                    if (avatarStrikeRejCooldown) diag.AvatarStrikeRejectedCooldownTicks++;
                    if (avatarStrikeRejTarget) diag.AvatarStrikeRejectedTargetTicks++;

                    int castLogBefore = controller.SpellCastLog.Count;
                    controller.AdvanceCombatTick();
                    int castLogAfter = controller.SpellCastLog.Count;

                    if (castLogAfter > castLogBefore)
                    {
                        SpellCastRecord record = controller.SpellCastLog[castLogAfter - 1];
                        if (!record.CastByPlayer)
                        {
                            bool wasAvatarStrike = controller.EnemySpellbook.Any(s => s.Effect == SpellEffect.AvatarStrike && s.Name == record.SpellName);
                            if (wasAvatarStrike) diag.AvatarStrikeRollSuccesses++;
                            else diag.OrdinaryRollSuccesses++;
                        }
                    }
                }

                diag.Trials++;
                diag.TotalTicks += controller.TickCount;
                UnityEngine.Object.DestroyImmediate(controller.gameObject);
            }

            diag.Log(group.ToString());
            return diag;
        }

        [Test]
        public void CandidateDiagnostic_Novice() => RunDiagnostic(LoadDatabase().AllCards.ToList(), TierGroup.Novice, 2000);

        [Test]
        public void CandidateDiagnostic_Apprentice() => RunDiagnostic(LoadDatabase().AllCards.ToList(), TierGroup.Apprentice, 2000);

        [Test]
        public void CandidateDiagnostic_VeteranPlus() => RunDiagnostic(LoadDatabase().AllCards.ToList(), TierGroup.VeteranPlus, 2000);
    }
}

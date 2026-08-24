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
    /// Diagnostic-only harness (GPT's follow-up call, 2026-08-24): the Apprentice-style candidate-
    /// scarcity diagnostic alone isn't enough for Novice (its -11.9pp win-rate delta persists across
    /// every tested gate value, which points at spell IMPACT/composition, not cast frequency). Adds
    /// per-spell candidate/reject breakdown, per-spell cast/damage/win-attribution, and paired
    /// baseline(off)/on(on) runs using IDENTICAL per-trial seeds (both UnityEngine.Random for
    /// deck/hand and BattleController's own MatchRngSeed for AI-cast rolls), so any measured
    /// difference between baseline and on is attributable to spellcasting itself, not trial-to-trial
    /// variance. No band assertions - measurement only. Novice and VeteranPlus only (Apprentice
    /// already resolved).
    /// </summary>
    public class AiSpellCastImpactDiagnosticTests
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
            var go = new GameObject("ImpactDiagCardDatabase");
            _spawned.Add(go);
            CardDatabase db = go.AddComponent<CardDatabase>();
            db.Initialize();
            db = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            return db;
        }

        private BattleController CreateController()
        {
            var go = new GameObject("ImpactDiagBattleController");
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

        private enum TierGroup { Novice, VeteranPlus }

        private static (int avatarLevel, int castleLevel, AIDifficultyTier tier) GroupConfig(TierGroup g) => g switch
        {
            TierGroup.Novice => (10, 10, AIDifficultyTier.Novice),
            TierGroup.VeteranPlus => (50, 30, AIDifficultyTier.Veteran),
            _ => throw new ArgumentOutOfRangeException(nameof(g)),
        };

        private static int LivingCount(PlayerBattleState side) =>
            side.Lanes[Lane.Front].Cards.Count(c => c.IsAlive) +
            side.Lanes[Lane.Middle].Cards.Count(c => c.IsAlive) +
            side.Lanes[Lane.Back].Cards.Count(c => c.IsAlive);

        private sealed class PerSpellStats
        {
            public int Available;
            public int RejectedEnergy;
            public int RejectedCooldown;
            public int RejectedTarget;
            public int SuccessfulCasts;
            public long TotalAvatarDamage;
            public int UnitsKilledOnCastTick;
            public int WinsWhenCast;
        }

        private sealed class ImpactResult
        {
            public string Label;
            public int Trials;
            public int AiWins;
            public int PlayerWins;
            public long TotalTicks;
            public readonly Dictionary<string, PerSpellStats> BySpell = new Dictionary<string, PerSpellStats>();
            public int ZeroCastTrials;
            public int TotalCasts;

            public PerSpellStats Stats(string name)
            {
                if (!BySpell.TryGetValue(name, out var s)) { s = new PerSpellStats(); BySpell[name] = s; }
                return s;
            }

            public void Log()
            {
                Debug.Log($"[ImpactDiag] {Label}: trials={Trials} aiWinRate={(Trials == 0 ? 0 : (double)AiWins / Trials):P1} " +
                          $"playerWinRate={(Trials == 0 ? 0 : (double)PlayerWins / Trials):P1} avgTicks={(Trials == 0 ? 0 : (double)TotalTicks / Trials):F2} " +
                          $"spellsPerMatch={(Trials == 0 ? 0 : (double)TotalCasts / Trials):F2} zeroCastRate={(Trials == 0 ? 0 : (double)ZeroCastTrials / Trials):P1}");
                foreach (var kv in BySpell.OrderByDescending(k => k.Value.SuccessfulCasts))
                {
                    var s = kv.Value;
                    Debug.Log($"[ImpactDiag] {Label} SPELL={kv.Key}: available={s.Available} rejEnergy={s.RejectedEnergy} rejCooldown={s.RejectedCooldown} rejTarget={s.RejectedTarget} " +
                              $"casts={s.SuccessfulCasts} totalAvatarDamage={s.TotalAvatarDamage} unitsKilledOnCastTick={s.UnitsKilledOnCastTick} winsWhenCast={s.WinsWhenCast}");
                }
            }
        }

        private ImpactResult RunPaired(List<Card> pool, TierGroup group, bool aiSpellsOn, int trials, int baseSeed)
        {
            (int avatarLevel, int castleLevel, AIDifficultyTier tier) = GroupConfig(group);
            var empire = new PlayerEmpireData();
            empire.SetLevelsForTesting(avatarLevel, castleLevel, barracksLevel: 25);
            empire.InitializeTCGModifiers();
            var economy = new BattleController.MatchEconomy(empire.ResourceCap, empire.Turn1Resource, empire.StartingAvatarHealth);
            List<string> equippedIds = AIEnemySpellbookResolver.ResolveSpellbook(tier).Select(s => s.Id).ToList();

            var result = new ImpactResult { Label = $"{group}/{(aiSpellsOn ? "On" : "Baseline")}" };

            for (int i = 0; i < trials; i++)
            {
                if (i % 200 == 0) Debug.Log($"[ImpactDiag] {result.Label}: trial {i}/{trials}");

                int seed = baseSeed + i;
                UnityEngine.Random.InitState(seed);

                BattleController controller = CreateController();
                List<Card> playerDeck = pool.OrderBy(_ => UnityEngine.Random.value).Take(empire.DeckSlotCount).ToList();
                List<Card> enemyDeck = pool.OrderBy(_ => UnityEngine.Random.value).Take(empire.DeckSlotCount).ToList();

                controller.StartMatch(playerDeck, enemyDeck, economy, economy,
                    avatarLevel, unlockedStageIds: null, equippedSpellIds: equippedIds, enemyTier: tier, rngSeed: seed);
                if (aiSpellsOn) controller.EnableMirroredEnemySpellsForPvE();

                controller.DealFormationHand(controller.PlayerState);
                controller.DealFormationHand(controller.EnemyState);
                DeployWholeSquad(controller, controller.PlayerState, AIArchetype.Balanced);
                SimpleAIOpponent.TakeTurn(controller, AIArchetype.Balanced);

                if (!controller.ConfirmFormation())
                {
                    UnityEngine.Object.DestroyImmediate(controller.gameObject);
                    continue;
                }

                var castsThisTrial = new List<(string name, int avatarDamage, int unitsKilled)>();

                while (controller.Phase == BattlePhase.Combat)
                {
                    int predictedTick = controller.TickCount + 1;
                    int predictedEnergy = Math.Min(controller.MaxEnergy,
                        controller.EnemyEnergy + controller.EnergyPerTick + BattleController.BackLaneEnergy(controller.EnemyState));

                    foreach (AvatarSpell spell in controller.EnemySpellbook)
                    {
                        AISpellCaster.DiagnoseCandidates(
                            new List<AvatarSpell> { spell }, predictedEnergy, predictedTick,
                            controller.EnemyState, controller.PlayerState,
                            out int ordinaryAvailable, out int ordinaryRejEnergy, out int ordinaryRejCooldown, out int ordinaryRejTarget,
                            out bool avatarStrikeAvailable, out bool avatarStrikeRejEnergy, out bool avatarStrikeRejCooldown, out bool avatarStrikeRejTarget,
                            tier);

                        var stats = result.Stats(spell.Name);
                        if (spell.Effect == SpellEffect.AvatarStrike)
                        {
                            if (avatarStrikeAvailable) stats.Available++;
                            if (avatarStrikeRejEnergy) stats.RejectedEnergy++;
                            if (avatarStrikeRejCooldown) stats.RejectedCooldown++;
                            if (avatarStrikeRejTarget) stats.RejectedTarget++;
                        }
                        else
                        {
                            stats.Available += ordinaryAvailable;
                            stats.RejectedEnergy += ordinaryRejEnergy;
                            stats.RejectedCooldown += ordinaryRejCooldown;
                            stats.RejectedTarget += ordinaryRejTarget;
                        }
                    }

                    int castLogBefore = controller.SpellCastLog.Count;
                    int livingBefore = LivingCount(controller.PlayerState);
                    controller.AdvanceCombatTick();
                    int livingAfter = LivingCount(controller.PlayerState);
                    int castLogAfter = controller.SpellCastLog.Count;

                    if (castLogAfter > castLogBefore)
                    {
                        SpellCastRecord record = controller.SpellCastLog[castLogAfter - 1];
                        if (!record.CastByPlayer)
                        {
                            int killed = Math.Max(0, livingBefore - livingAfter);
                            castsThisTrial.Add((record.SpellName, record.AvatarDamageDealt, killed));
                        }
                    }
                }

                result.Trials++;
                result.TotalTicks += controller.TickCount;
                bool aiWon = controller.PlayerState.IsDefeated && !controller.EnemyState.IsDefeated;
                bool playerWon = controller.EnemyState.IsDefeated && !controller.PlayerState.IsDefeated;
                if (aiWon) result.AiWins++;
                if (playerWon) result.PlayerWins++;

                if (castsThisTrial.Count == 0) result.ZeroCastTrials++;
                result.TotalCasts += castsThisTrial.Count;

                foreach (var (name, avatarDamage, killed) in castsThisTrial)
                {
                    var stats = result.Stats(name);
                    stats.SuccessfulCasts++;
                    stats.TotalAvatarDamage += avatarDamage;
                    stats.UnitsKilledOnCastTick += killed;
                }
                foreach (string name in castsThisTrial.Select(c => c.name).Distinct())
                {
                    if (aiWon) result.Stats(name).WinsWhenCast++;
                }

                UnityEngine.Object.DestroyImmediate(controller.gameObject);
            }

            result.Log();
            return result;
        }

        private void RunTier(TierGroup group)
        {
            List<Card> pool = LoadDatabase().AllCards.ToList();
            int baseSeed = 900001;
            RunPaired(pool, group, aiSpellsOn: false, trials: 1500, baseSeed: baseSeed);
            RunPaired(pool, group, aiSpellsOn: true, trials: 1500, baseSeed: baseSeed);
        }

        [Test]
        public void ImpactDiagnostic_Novice() => RunTier(TierGroup.Novice);

        [Test]
        public void ImpactDiagnostic_VeteranPlus() => RunTier(TierGroup.VeteranPlus);
    }
}

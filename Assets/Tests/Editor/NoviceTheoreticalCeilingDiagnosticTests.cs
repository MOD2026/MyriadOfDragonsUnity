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
    /// Diagnostic-only harness (AI-balance diagnose-before-tune, LOCKED 2026-08-25): Novice's
    /// measured 21.5% cast rate / 0.15 spells-per-match sit below the 25%/0.5 floor with a
    /// healthy win-rate delta - the register's decision rule is to first measure the THEORETICAL
    /// ceiling at 100% ordinary-spell roll (BattleController.SetForceAiSpellCastGateAlwaysPassForTests)
    /// before concluding candidate scarcity is the cause. No band assertions, no gate/spell value
    /// changes - measurement only, same real production code path (AISpellCaster.
    /// TryCastDuringCombatTick via AdvanceCombatTick) as a real match, just with the frequency
    /// roll forced to always pass.
    /// </summary>
    public class NoviceTheoreticalCeilingDiagnosticTests
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
            var go = new GameObject("CeilingDiagCardDatabase");
            _spawned.Add(go);
            CardDatabase db = go.AddComponent<CardDatabase>();
            db.Initialize();
            db = CardDatabase.Instance;
            return db;
        }

        private BattleController CreateController()
        {
            var go = new GameObject("CeilingDiagBattleController");
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

        [Test]
        public void CeilingDiagnostic_Novice_At100PercentOrdinaryRoll()
        {
            const int trials = 2000;
            const int avatarLevel = 10;
            const int castleLevel = 10;
            const AIDifficultyTier tier = AIDifficultyTier.Novice;

            var empire = new PlayerEmpireData();
            empire.SetLevelsForTesting(avatarLevel, castleLevel, barracksLevel: 25);
            empire.InitializeTCGModifiers();
            var economy = new BattleController.MatchEconomy(empire.ResourceCap, empire.Turn1Resource, empire.StartingAvatarHealth);
            List<string> equippedIds = AIEnemySpellbookResolver.ResolveSpellbook(tier).Select(s => s.Id).ToList();
            List<Card> pool = LoadDatabase().AllCards.ToList();

            long totalTicks = 0;
            long ticksObserved = 0;
            int totalOrdinaryCasts = 0;
            int totalAvatarStrikeCasts = 0;
            int trialsWithAtLeastOneOrdinaryCast = 0;
            int completedTrials = 0;

            for (int i = 0; i < trials; i++)
            {
                if (i % 200 == 0) Debug.Log($"[CeilingDiag] Novice@100%roll: trial {i}/{trials}");

                BattleController controller = CreateController();
                List<Card> playerDeck = pool.OrderBy(_ => UnityEngine.Random.value).Take(empire.DeckSlotCount).ToList();
                List<Card> enemyDeck = pool.OrderBy(_ => UnityEngine.Random.value).Take(empire.DeckSlotCount).ToList();

                controller.StartMatch(playerDeck, enemyDeck, economy, economy,
                    avatarLevel, unlockedStageIds: null, equippedSpellIds: equippedIds, enemyTier: tier);
                controller.EnableMirroredEnemySpellsForPvE();
                controller.SetForceAiSpellCastGateAlwaysPassForTests(true);

                controller.DealFormationHand(controller.PlayerState);
                controller.DealFormationHand(controller.EnemyState);
                DeployWholeSquad(controller, controller.PlayerState, AIArchetype.Balanced);
                SimpleAIOpponent.TakeTurn(controller, AIArchetype.Balanced);

                if (!controller.ConfirmFormation())
                {
                    UnityEngine.Object.DestroyImmediate(controller.gameObject);
                    continue;
                }

                int ordinaryCastsThisTrial = 0;

                while (controller.Phase == BattlePhase.Combat)
                {
                    ticksObserved++;
                    int castLogBefore = controller.SpellCastLog.Count;
                    controller.AdvanceCombatTick();
                    int castLogAfter = controller.SpellCastLog.Count;

                    if (castLogAfter > castLogBefore)
                    {
                        SpellCastRecord record = controller.SpellCastLog[castLogAfter - 1];
                        if (!record.CastByPlayer)
                        {
                            bool wasAvatarStrike = controller.EnemySpellbook.Any(s => s.Effect == SpellEffect.AvatarStrike && s.Name == record.SpellName);
                            if (wasAvatarStrike) totalAvatarStrikeCasts++;
                            else { totalOrdinaryCasts++; ordinaryCastsThisTrial++; }
                        }
                    }
                }

                totalTicks += controller.TickCount;
                completedTrials++;
                if (ordinaryCastsThisTrial > 0) trialsWithAtLeastOneOrdinaryCast++;

                UnityEngine.Object.DestroyImmediate(controller.gameObject);
            }

            double ceilingCastRate = ticksObserved == 0 ? 0 : (double)totalOrdinaryCasts / ticksObserved;
            double ceilingSpellsPerMatch = completedTrials == 0 ? 0 : (double)totalOrdinaryCasts / completedTrials;
            double avgTicksPerMatch = completedTrials == 0 ? 0 : (double)totalTicks / completedTrials;

            Debug.Log($"[CeilingDiag] Novice@100%roll: completedTrials={completedTrials} ticksObserved={ticksObserved} avgTicksPerMatch={avgTicksPerMatch:F2}");
            Debug.Log($"[CeilingDiag] Novice@100%roll: totalOrdinaryCasts={totalOrdinaryCasts} totalAvatarStrikeCasts={totalAvatarStrikeCasts} " +
                      $"trialsWithAtLeastOneOrdinaryCast={trialsWithAtLeastOneOrdinaryCast} ({(completedTrials == 0 ? 0 : (double)trialsWithAtLeastOneOrdinaryCast / completedTrials):P1})");
            Debug.Log($"[CeilingDiag] Novice@100%roll: THEORETICAL CEILING castRate(perTick)={ceilingCastRate:P2} spellsPerMatch={ceilingSpellsPerMatch:F3}");
            Debug.Log($"[CeilingDiag] Novice@100%roll: DECISION RULE - floor is 25% cast rate / 0.5 spells-per-match. " +
                      $"Ceiling clears floor: castRate={(ceilingCastRate >= 0.25)} spellsPerMatch={(ceilingSpellsPerMatch >= 0.5)}");
        }
    }
}

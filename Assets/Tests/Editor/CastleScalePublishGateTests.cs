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
    /// Publish gate for Castle Gold: shared-scale matrix L1/5/10/15/20/25/30.
    /// Run focused only (CastleScalePublishGateTests) — not the full balance suite.
    /// </summary>
    public class CastleScalePublishGateTests
    {
        // Bumped 80->300 (2026-08-24): real, verified flakiness at n=80 - two back-to-back runs
        // with identical code showed L30 KO swinging 50%->58%, and the original failing sample
        // (36.25%) sits ~2.5-4 standard errors below that observed rate at n=80 (SE~5.6pp at
        // p~0.5). Unseeded by design, matching BalanceSimulationTests' own MatchesPerRun=400
        // precedent for its main sweeps - a bigger sample shrinks the noise itself rather than
        // pinning which single random outcome occurs, which is the right fix for a threshold this
        // close to the true rate (SE~2.9pp at n=300, p~0.5 - the original 36% outlier would now
        // be ~5 SE out, not 3).
        private const int MatchesPerCell = 300;
        private static readonly int[] CastleRows = { 1, 5, 10, 15, 20, 25, 30 };

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            LaneBattleResolver.ResetRulesToDefault();
            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();
        }

        [Test]
        public void CastleSharedScale_Matrix_StaysDecisiveAndSpellViable()
        {
            var dbGo = new GameObject("CastleSimDb");
            _spawned.Add(dbGo);
            CardDatabase db = dbGo.AddComponent<CardDatabase>();
            db.Initialize();
            db = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            List<Card> pool = db.AllCards.ToList();
            Assert.Greater(pool.Count, 20);

            float previousKo = 1f;
            foreach (int castle in CastleRows)
            {
                int avatar = castle;
                var empire = new PlayerEmpireData();
                empire.SetLevelsForTesting(avatar, castle, barracksLevel: 25);
                empire.InitializeTCGModifiers();
                var economy = new BattleController.MatchEconomy(
                    empire.ResourceCap, empire.Turn1Resource, empire.StartingAvatarHealth);

                int knockouts = 0;
                int matches = 0;
                int totalTicks = 0;

                for (int i = 0; i < MatchesPerCell; i++)
                {
                    var go = new GameObject("CastleSimBattle");
                    BattleController controller = go.AddComponent<BattleController>();

                    List<Card> playerDeck = pool.OrderBy(_ => Random.value).Take(empire.DeckSlotCount).ToList();
                    List<Card> enemyDeck = pool.OrderBy(_ => Random.value).Take(empire.DeckSlotCount).ToList();

                    controller.StartMatch(playerDeck, enemyDeck, economy, economy);
                    controller.DealFormationHand(controller.PlayerState);
                    controller.DealFormationHand(controller.EnemyState);

                    DeployWholeSquad(controller, controller.PlayerState);
                    SimpleAIOpponent.TakeTurn(controller, AIArchetype.Balanced);

                    if (!controller.ConfirmFormation())
                    {
                        Object.DestroyImmediate(go);
                        continue;
                    }

                    while (controller.Phase == BattlePhase.Combat)
                        controller.AdvanceCombatTick();

                    matches++;
                    totalTicks += controller.TickCount;
                    if (controller.PlayerState.IsDefeated || controller.EnemyState.IsDefeated)
                        knockouts++;

                    Object.DestroyImmediate(go);
                }

                Assert.Greater(matches, MatchesPerCell / 2, $"Castle L{castle}: too few samples.");
                float ko = (float)knockouts / matches;
                float avgTicks = (float)totalTicks / matches;

                Assert.Greater(ko, castle >= 25 ? 0.40f : 0.45f,
                    $"Castle L{castle}: KO {ko:P0} below decisiveness floor for this band.");
                Assert.Greater(avgTicks, 3.5f, $"Castle L{castle}: {avgTicks:F1} ticks too short.");
                Assert.Less(avgTicks, BattleController.MaxCombatTicks - 1f,
                    $"Castle L{castle}: {avgTicks:F1} ticks near clock.");

                if (castle > 1)
                {
                    Assert.Greater(ko, previousKo - 0.25f,
                        $"Castle L{castle} KO collapsed vs prior row.");
                }
                previousKo = ko;
                Debug.Log($"[CastlePublishGate] L{castle} KO={ko:P0} avgTicks={avgTicks:F1} n={matches}");
            }
        }

        private static void DeployWholeSquad(BattleController controller, PlayerBattleState side)
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
                        if (controller.TryPlayCard(side, card, lane))
                        {
                            placed = true;
                            break;
                        }
                    }
                    if (placed) break;
                }
            }
        }
    }
}

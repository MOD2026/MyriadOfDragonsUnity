using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// COMBAT LEDGER, 2026-08-15 - proves BattleController.CombatLedger (data foundation for a
    /// future hardcore combat-log UI, not a UI feature itself) records exactly one entry per
    /// actually-resolved combat tick, in order, with numbers matching the same
    /// TurnResolutionResult/AvatarHealth the rest of the battle logic already relies on; that it
    /// is cleared on a fresh match; and that a no-op tick outside Combat adds nothing.
    /// </summary>
    public class CombatLedgerTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();
        }

        private CardDatabase LoadDatabase()
        {
            var go = new GameObject("TestCardDatabase");
            _spawned.Add(go);
            CardDatabase db = go.AddComponent<CardDatabase>();
            db.Initialize();
            return db;
        }

        private BattleController CreateController()
        {
            var go = new GameObject("TestBattleController");
            _spawned.Add(go);
            return go.AddComponent<BattleController>();
        }

        /// <summary>High avatar health on both sides (2000) so several ticks can resolve without
        /// the match ending mid-test - same pattern as BattleLogicTests.StartFormationMatch.</summary>
        private BattleController StartFormationMatch(CardDatabase db)
        {
            BattleController controller = CreateController();
            List<Card> deck = db.AllCards.Take(15).ToList();
            controller.StartMatch(deck.ToList(), deck.ToList(),
                new BattleController.MatchEconomy(60, 60, 2000),
                new BattleController.MatchEconomy(60, 60, 2000));
            controller.DealFormationHand(controller.PlayerState);
            controller.DealFormationHand(controller.EnemyState);
            return controller;
        }

        private static void PlayOneCardEachSide(BattleController controller)
        {
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, controller.PlayerState.Hand.First(), Lane.Front));
            Assert.IsTrue(controller.TryPlayCard(controller.EnemyState, controller.EnemyState.Hand.First(), Lane.Front));
        }

        [Test]
        public void OneResolvedTick_CreatesOneRecordMatchingTheResolutionAndPostTickHealth()
        {
            CardDatabase db = LoadDatabase();
            BattleController controller = StartFormationMatch(db);
            PlayOneCardEachSide(controller);
            Assert.IsTrue(controller.ConfirmFormation());

            TurnResolutionResult result = controller.AdvanceCombatTick();

            Assert.AreEqual(1, controller.CombatLedger.Count, "One resolved tick must create exactly one ledger record.");
            CombatTickRecord record = controller.CombatLedger[0];

            Assert.AreEqual(controller.TickCount, record.TickNumber);
            Assert.AreEqual(result.DamageDealtToSideA, record.DamageToPlayerAvatar);
            Assert.AreEqual(result.DamageDealtToSideB, record.DamageToEnemyAvatar);
            Assert.AreEqual(controller.PlayerState.AvatarHealth, record.PlayerAvatarHealthAfter,
                "The record's player HP must match the actual post-tick AvatarHealth.");
            Assert.AreEqual(controller.EnemyState.AvatarHealth, record.EnemyAvatarHealthAfter,
                "The record's enemy HP must match the actual post-tick AvatarHealth.");

            CollectionAssert.AreEqual(
                result.LaneResults.Select(r => (r.Lane, r.OverflowToA, r.OverflowToB)),
                record.LaneResults.Select(r => (r.Lane, r.OverflowToA, r.OverflowToB)),
                "The record must carry the same Front/Middle/Back overflow values LaneBattleResolver actually computed for this tick.");
        }

        [Test]
        public void MultipleTicks_PreserveChronologicalOrder()
        {
            CardDatabase db = LoadDatabase();
            BattleController controller = StartFormationMatch(db);
            PlayOneCardEachSide(controller);
            Assert.IsTrue(controller.ConfirmFormation());

            for (int i = 0; i < 3 && controller.Phase == BattlePhase.Combat; i++)
            {
                controller.AdvanceCombatTick();
            }

            Assert.GreaterOrEqual(controller.CombatLedger.Count, 2, "Setup: expected at least two ticks to have resolved.");
            for (int i = 0; i < controller.CombatLedger.Count; i++)
            {
                Assert.AreEqual(i + 1, controller.CombatLedger[i].TickNumber,
                    "Ledger records must stay in the exact chronological order ticks actually resolved in.");
            }
        }

        [Test]
        public void StartingANewMatch_ClearsPriorLedgerRecords()
        {
            CardDatabase db = LoadDatabase();
            BattleController controller = StartFormationMatch(db);
            PlayOneCardEachSide(controller);
            Assert.IsTrue(controller.ConfirmFormation());
            controller.AdvanceCombatTick();
            Assert.AreEqual(1, controller.CombatLedger.Count, "Setup: expected one record from the first match.");

            List<Card> deck = db.AllCards.Take(15).ToList();
            controller.StartMatch(deck.ToList(), deck.ToList(),
                new BattleController.MatchEconomy(60, 60, 2000),
                new BattleController.MatchEconomy(60, 60, 2000));

            Assert.AreEqual(0, controller.CombatLedger.Count, "Starting a fresh match must clear every prior ledger record.");
        }

        [Test]
        public void AdvanceCombatTickDuringFormation_AddsNoLedgerRecord()
        {
            CardDatabase db = LoadDatabase();
            BattleController controller = StartFormationMatch(db);
            Assert.AreEqual(BattlePhase.Formation, controller.Phase, "Setup: match should still be in Formation.");

            controller.AdvanceCombatTick();

            Assert.AreEqual(0, controller.CombatLedger.Count, "A tick called outside Combat must not add a ledger record.");
        }
    }
}

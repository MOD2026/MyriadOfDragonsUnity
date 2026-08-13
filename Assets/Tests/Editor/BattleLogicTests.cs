using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.AI;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Empire;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Automated tests for the battle logic, run headlessly via Unity's Test Runner - not just
    /// eyeballing the code. These exist specifically because two rounds of "I traced it by hand
    /// and it looks right" turned out wrong (the ColorTint bug, and the resource curve being
    /// unplayable in practice) - things a real run against the actual card_data.json would have
    /// caught before asking for a manual playtest.
    /// </summary>
    public class BattleLogicTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            // CardDatabase is a static singleton (Instance) - if an assertion above throws
            // before manual cleanup ran, Instance is left dangling and every later test's
            // LoadDatabase() silently gets an empty, half-initialized database instead of a
            // real one, producing unrelated cascading failures. TearDown always runs, so this
            // is the only cleanup that can be trusted.
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
            // Awake() never fires here - see the comment on CardDatabase.Initialize().
            db.Initialize();
            return db;
        }

        private BattleController CreateController()
        {
            var go = new GameObject("TestBattleController");
            _spawned.Add(go);
            return go.AddComponent<BattleController>();
        }

        private static BattleController.MatchEconomy TestEconomy(int resourceCap, int turn1Resource)
        {
            return new BattleController.MatchEconomy(resourceCap, turn1Resource, startingAvatarHealth: 30);
        }

        [Test]
        public void CardDatabase_LoadsAllCardsFromJson()
        {
            CardDatabase db = LoadDatabase();
            Assert.AreEqual(85, db.AllCards.Count, "Expected all 85 cards from card_data.json to load.");
        }

        [Test]
        public void CardDatabase_EveryCardHasPositiveStats()
        {
            CardDatabase db = LoadDatabase();
            Assert.Greater(db.AllCards.Count, 0, "Sanity check: database must not be empty for this test to mean anything.");
            foreach (Card card in db.AllCards)
            {
                Assert.Greater(card.Attack, 0, $"{card.Id} has non-positive Attack.");
                Assert.Greater(card.Health, 0, $"{card.Id} has non-positive Health.");
                Assert.Greater(card.ResourceCost, 0, $"{card.Id} has non-positive ResourceCost.");
            }
        }

        [Test]
        public void PlayerBattleState_Turn1_HasAtLeastOneAffordableCardInStartingHand_MostOfTheTime()
        {
            // Regression test for the exact bug reported during manual testing: Turn 1 resource
            // needs to actually be enough to play something from a real starting hand, not just
            // in theory. Runs many simulated hands since dealing is randomized - a single lucky
            // draw proves nothing, and a single unlucky one doesn't either.
            CardDatabase db = LoadDatabase();
            List<Card> deck = db.AllCards.ToList();
            Assert.Greater(deck.Count, 0, "Sanity check: deck must not be empty for this test to mean anything.");

            // resourceCap=8, turn1Resource=5 matches a genuine level-1 player under
            // PlayerEmpireData's formula (BaseResourceCap=8, no Avatar/Castle bonuses yet,
            // Turn1Resource = round(8*0.6)) - the worst-case (lowest-resource) real scenario.
            int trials = 200;
            int deadHands = 0;
            for (int i = 0; i < trials; i++)
            {
                var state = new PlayerBattleState(deck, resourceCap: 8, turn1Resource: 5, startingAvatarHealth: 20);
                state.GainResourceForTurn(); // Turn 1
                bool anyAffordable = state.Hand.Any(c => c.ResourceCost <= state.Resource);
                if (!anyAffordable) deadHands++;
            }

            double deadHandRate = (double)deadHands / trials;
            Assert.Less(deadHandRate, 0.15,
                $"Turn 1 produced a completely unplayable hand in {deadHandRate:P0} of {trials} simulated games - " +
                "too high. (This is exactly the bug reported on Turn 1 with 1-2 starting resource.)");
        }

        [Test]
        public void BattleController_TryPlayCard_DeductsResourceAndFillsLane()
        {
            CardDatabase db = LoadDatabase();
            List<Card> deck = db.AllCards.ToList();
            Card testCard = deck.First(c => c.ResourceCost <= 4);

            BattleController controller = CreateController();
            controller.StartMatch(deck, deck, TestEconomy(20, 10), TestEconomy(20, 10));

            if (!controller.PlayerState.Hand.Contains(testCard))
            {
                controller.PlayerState.Hand.Add(testCard);
            }
            controller.PlayerState.Resource = 10;

            int resourceBefore = controller.PlayerState.Resource;
            bool played = controller.TryPlayCard(controller.PlayerState, testCard, Lane.Front);

            Assert.IsTrue(played, "Expected an affordable card with an open lane to be playable.");
            Assert.AreEqual(resourceBefore - testCard.ResourceCost, controller.PlayerState.Resource);
            Assert.AreEqual(1, controller.PlayerState.Lanes[Lane.Front].Cards.Count);
            Assert.IsFalse(controller.PlayerState.Hand.Contains(testCard), "Played card should leave the hand.");
        }

        [Test]
        public void BattleController_TryPlayCard_RejectsWhenLaneIsFull()
        {
            CardDatabase db = LoadDatabase();
            List<Card> deck = db.AllCards.ToList();

            BattleController controller = CreateController();
            controller.StartMatch(deck, deck, TestEconomy(20, 10), TestEconomy(20, 10));
            controller.PlayerState.Resource = 100;

            // Fill the Front lane by *slots*, not by card count - a rarity 5+ card takes two of
            // the three (Card.SlotWeight), so three one-slot cards fill it but two big ones can
            // too. Using only one-slot cards keeps this test about the capacity rule itself
            // rather than about which cards the deck happened to contain.
            List<Card> singleSlot = deck.Where(c => c.SlotWeight == 1).ToList();
            Assert.GreaterOrEqual(singleSlot.Count, LaneState.MaxSlots + 1,
                "Setup: need enough one-slot cards to fill a lane and try one more.");

            for (int i = 0; i < LaneState.MaxSlots; i++)
            {
                Card card = singleSlot[i];
                if (!controller.PlayerState.Hand.Contains(card)) controller.PlayerState.Hand.Add(card);
                Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, card, Lane.Front));
            }

            Assert.AreEqual(0, controller.PlayerState.Lanes[Lane.Front].FreeSlots,
                "Setup: three one-slot cards should leave the lane with no free slots.");

            Card overflowCard = singleSlot[LaneState.MaxSlots];
            if (!controller.PlayerState.Hand.Contains(overflowCard)) controller.PlayerState.Hand.Add(overflowCard);
            int resourceBefore = controller.PlayerState.Resource;
            int handCountBefore = controller.PlayerState.Hand.Count;
            bool played = controller.TryPlayCard(controller.PlayerState, overflowCard, Lane.Front);

            Assert.IsFalse(played, "A full lane (3/3 slots) should reject another card.");
            Assert.AreEqual(resourceBefore, controller.PlayerState.Resource, "A rejected play must not spend Resource.");
            Assert.AreEqual(handCountBefore, controller.PlayerState.Hand.Count, "A rejected play must not remove the card from hand.");
        }

        [Test]
        public void BattleController_TryPlayCard_RejectsWhenCardNotInHand()
        {
            // TryPlayCard's first guard clause (Hand.Contains(card)) had no dedicated coverage -
            // the two existing tests both start from a card already in hand.
            CardDatabase db = LoadDatabase();
            List<Card> deck = db.AllCards.ToList();
            Card cardNotInHand = deck.First();

            BattleController controller = CreateController();
            controller.StartMatch(deck, deck, TestEconomy(20, 10), TestEconomy(20, 10));
            controller.PlayerState.Hand.Remove(cardNotInHand); // ensure absence regardless of the random deal
            controller.PlayerState.Resource = 100;

            int resourceBefore = controller.PlayerState.Resource;
            int laneCardsBefore = controller.PlayerState.Lanes[Lane.Front].Cards.Count;

            bool played = controller.TryPlayCard(controller.PlayerState, cardNotInHand, Lane.Front);

            Assert.IsFalse(played, "A card not in hand must be rejected.");
            Assert.AreEqual(resourceBefore, controller.PlayerState.Resource, "A rejected play must not spend Resource.");
            Assert.AreEqual(laneCardsBefore, controller.PlayerState.Lanes[Lane.Front].Cards.Count,
                "A rejected play must not place a unit on the board.");
        }

        [Test]
        public void BattleController_TryPlayCard_RejectsWhenUnaffordable_WithoutSpendingResourceOrLosingTheCard()
        {
            // TryPlayCard's second guard clause (ResourceCost <= Resource) also had no dedicated
            // rejection coverage.
            CardDatabase db = LoadDatabase();
            List<Card> deck = db.AllCards.ToList();
            Card expensiveCard = deck.OrderByDescending(c => c.ResourceCost).First();

            BattleController controller = CreateController();
            controller.StartMatch(deck, deck, TestEconomy(20, 10), TestEconomy(20, 10));
            if (!controller.PlayerState.Hand.Contains(expensiveCard))
            {
                controller.PlayerState.Hand.Add(expensiveCard);
            }
            controller.PlayerState.Resource = expensiveCard.ResourceCost - 1;

            int resourceBefore = controller.PlayerState.Resource;
            int handCountBefore = controller.PlayerState.Hand.Count;
            int laneCardsBefore = controller.PlayerState.Lanes[Lane.Front].Cards.Count;

            bool played = controller.TryPlayCard(controller.PlayerState, expensiveCard, Lane.Front);

            Assert.IsFalse(played, "An unaffordable card must be rejected.");
            Assert.AreEqual(resourceBefore, controller.PlayerState.Resource, "A rejected play must not spend Resource.");
            Assert.AreEqual(handCountBefore, controller.PlayerState.Hand.Count, "A rejected play must not remove the card from hand.");
            Assert.AreEqual(laneCardsBefore, controller.PlayerState.Lanes[Lane.Front].Cards.Count,
                "A rejected play must not place a unit on the board.");
        }

        [Test]
        public void BattleController_StartMatch_DrawsTwoCardsPerTurn()
        {
            // Regression test: BattleController.cs was silently restored from a stale Drive
            // backup that predated the hardcore-CCG "draw 2, not 1" rework (Part II §2.2) -
            // nothing caught it because no test asserted on draw count specifically. This does.
            CardDatabase db = LoadDatabase();
            List<Card> deck = db.AllCards.ToList();

            BattleController controller = CreateController();
            controller.StartMatch(deck, deck, TestEconomy(20, 10), TestEconomy(20, 10));

            // StartMatch deals StartingHandSize (4) via the constructor, then StartMatch's
            // internal BeginTurn() draws DrawsPerTurn more on top of that for Turn 1.
            Assert.AreEqual(PlayerBattleState.StartingHandSize + 2, controller.PlayerState.Hand.Count,
                "Expected 2 cards drawn for Turn 1 (StartingHandSize + DrawsPerTurn), not 1.");
        }

        [Test]
        public void BattleController_PerfectCardInBackLane_TriggersDrawHook()
        {
            // Regression test for the other half of the same stale-file incident: Perfect's
            // "adopts Strategist's draw hook, but only in the Back lane" behavior (the whole
            // point of the hardcore-CCG rework replacing its old flat cost tax) was silently
            // missing from ApplyOnPlayClassHook after the restore. Nothing caught it because no
            // test exercised Perfect-class cards specifically.
            CardDatabase db = LoadDatabase();
            List<Card> deck = db.AllCards.ToList();
            Card perfectCard = deck.FirstOrDefault(c => c.Class == CardClass.Perfect);
            Assert.NotNull(perfectCard, "Test assumption failed: expected at least one Perfect-class card in the pool.");

            BattleController controller = CreateController();
            controller.StartMatch(deck, deck, TestEconomy(20, 20), TestEconomy(20, 20));
            if (!controller.PlayerState.Hand.Contains(perfectCard))
            {
                controller.PlayerState.Hand.Add(perfectCard);
            }

            int handCountBefore = controller.PlayerState.Hand.Count;
            bool played = controller.TryPlayCard(controller.PlayerState, perfectCard, Lane.Back);

            Assert.IsTrue(played, "Expected a fully-affordable Perfect card to be playable into an open Back lane.");
            Assert.AreEqual(handCountBefore, controller.PlayerState.Hand.Count,
                "Playing a Perfect card into the Back lane should remove it from hand but also draw a " +
                "replacement (Strategist's hook) - net hand size should be unchanged, not down by one.");
        }

        [Test]
        public void BattleController_StrategistCardTriggersDrawHookRegardlessOfLane()
        {
            // The other half of ApplyOnPlayClassHook's rule ("Strategist's on-play draw always
            // fires, regardless of lane") was never exercised on its own - only Perfect's
            // Back-lane-specific case was tested above. Deployed into Front, not Back, so this
            // cannot be mistaken for exercising Perfect's rule instead.
            CardDatabase db = LoadDatabase();
            List<Card> deck = db.AllCards.ToList();
            Card strategistCard = deck.FirstOrDefault(c => c.Class == CardClass.Strategist);
            Assert.NotNull(strategistCard, "Test assumption failed: expected at least one Strategist-class card in the pool.");

            BattleController controller = CreateController();
            controller.StartMatch(deck, deck, TestEconomy(20, 20), TestEconomy(20, 20));
            if (!controller.PlayerState.Hand.Contains(strategistCard))
            {
                controller.PlayerState.Hand.Add(strategistCard);
            }

            int handCountBefore = controller.PlayerState.Hand.Count;
            bool played = controller.TryPlayCard(controller.PlayerState, strategistCard, Lane.Front);

            Assert.IsTrue(played, "Expected a fully-affordable Strategist card to be playable into Front.");
            Assert.AreEqual(handCountBefore, controller.PlayerState.Hand.Count,
                "Playing a Strategist card into Front should remove it from hand but also draw a " +
                "replacement - net hand size should be unchanged, not down by one, regardless of lane.");
        }

        [Test]
        public void BattleController_PerfectCardOutsideBackLane_DoesNotTriggerDrawHook()
        {
            // The negative case of Perfect's rule: outside Back, Perfect must behave like any
            // other card and NOT draw - only ever tested for the positive (Back-lane) case before.
            CardDatabase db = LoadDatabase();
            List<Card> deck = db.AllCards.ToList();
            Card perfectCard = deck.FirstOrDefault(c => c.Class == CardClass.Perfect);
            Assert.NotNull(perfectCard, "Test assumption failed: expected at least one Perfect-class card in the pool.");

            BattleController controller = CreateController();
            controller.StartMatch(deck, deck, TestEconomy(20, 20), TestEconomy(20, 20));
            if (!controller.PlayerState.Hand.Contains(perfectCard))
            {
                controller.PlayerState.Hand.Add(perfectCard);
            }

            int handCountBefore = controller.PlayerState.Hand.Count;
            bool played = controller.TryPlayCard(controller.PlayerState, perfectCard, Lane.Front);

            Assert.IsTrue(played, "Expected a fully-affordable Perfect card to be playable into Front.");
            Assert.AreEqual(handCountBefore - 1, controller.PlayerState.Hand.Count,
                "Playing a Perfect card outside the Back lane should only remove it from hand - Perfect's " +
                "draw hook is Back-lane-specific, so no replacement should be drawn here.");
        }

        [Test]
        public void LaneBattleResolver_ClashIsSymmetricAndOrderIndependent()
        {
            // The whole point of computing both sides' attack totals before either takes
            // damage is that resolution order can't matter. Verify that directly rather than
            // just asserting it in a comment.
            CardDatabase db = LoadDatabase();
            Card attackerCard = db.GetCard("warrior");
            Card defenderCard = db.GetCard("cyclops");
            Assert.NotNull(attackerCard, "Expected 'warrior' to exist in card_data.json.");
            Assert.NotNull(defenderCard, "Expected 'cyclops' to exist in card_data.json.");

            var laneA1 = new LaneState(Lane.Front);
            laneA1.Cards.Add(new BattleCardInstance(attackerCard, true, 0, 0));
            var laneB1 = new LaneState(Lane.Front);
            laneB1.Cards.Add(new BattleCardInstance(defenderCard, false, 0, 0));
            LaneClashResult result1 = LaneBattleResolver.ResolveLaneClash(laneA1, laneB1);

            var laneB2 = new LaneState(Lane.Front);
            laneB2.Cards.Add(new BattleCardInstance(defenderCard, false, 0, 0));
            var laneA2 = new LaneState(Lane.Front);
            laneA2.Cards.Add(new BattleCardInstance(attackerCard, true, 0, 0));
            LaneClashResult result2 = LaneBattleResolver.ResolveLaneClash(laneA2, laneB2);

            Assert.AreEqual(result1.OverflowToA, result2.OverflowToA);
            Assert.AreEqual(result1.OverflowToB, result2.OverflowToB);
            Assert.AreEqual(result1.SideACleared, result2.SideACleared);
            Assert.AreEqual(result1.SideBCleared, result2.SideBCleared);
        }

        [Test]
        public void LaneBattleResolver_OverflowOnlyCarriesWhenLaneFullyClears()
        {
            // Direct check of the "clear the lane, then overflow" rule in Part II §2.4:
            // a lane that survives with any card alive must block all overflow, even if the
            // incoming damage numerically exceeded the lane's total Health.
            //
            // Built from synthetic CardData rather than pulled off the real card pool:
            // rarity 1-7 stat ranges scale Attack and Health together (e.g. rarity 7 tops out
            // around 12 for both), so "find a real card tankier than the strongest real
            // attacker" isn't guaranteed to have an answer - that was a bad assumption in an
            // earlier version of this test, not a game-logic bug.
            var weakAttackerData = new CardData { id = "test_weak_attacker", name = "Test Weak Attacker", art_file = "x.png", element = "Andras", type = "knight", rarity = 1 };
            var tankyDefenderData = new CardData { id = "test_tanky_defender", name = "Test Tanky Defender", art_file = "x.png", element = "Andras", type = "knight", rarity = 7 };
            Card weakAttacker = Card.FromData(weakAttackerData);
            Card tankyDefender = Card.FromData(tankyDefenderData);
            Assert.Less(weakAttacker.Attack, tankyDefender.Health,
                "Test setup invariant broken: rarity 1 Attack (1-2) should always be less than rarity 7 Health (7-12).");

            var attackerLane = new LaneState(Lane.Front);
            attackerLane.Cards.Add(new BattleCardInstance(weakAttacker, true, 0, 0));
            var defenderLane = new LaneState(Lane.Front);
            defenderLane.Cards.Add(new BattleCardInstance(tankyDefender, false, 0, 0));

            LaneClashResult result = LaneBattleResolver.ResolveLaneClash(attackerLane, defenderLane);

            Assert.IsFalse(result.SideBCleared, "Defending lane should have a survivor left.");
            Assert.AreEqual(0, result.OverflowToB, "No overflow should carry through a lane that wasn't fully cleared.");
        }

        [Test]
        public void LaneBattleResolver_ResolveTurn_MultipliesOverflowBeforeHittingAvatarHealth()
        {
            // Regression/behavior test for the 2026-08-05 HP rescale: card combat (Attack vs
            // Health) must stay exactly as before - only the final "overflow hits the Avatar"
            // step should be scaled up. Asserts against LaneBattleResolver.AvatarDamageMultiplier
            // rather than a hardcoded number: this test used to duplicate "25" as a literal and
            // failed the moment the multiplier was retuned for pacing, which makes a deliberate
            // balance change look like a regression.
            int multiplier = LaneBattleResolver.AvatarDamageMultiplier;
            var weakDefenderData = new CardData { id = "test_weak_defender2", name = "Test Weak Defender 2", art_file = "x.png", element = "Andras", type = "knight", rarity = 1 };
            var strongAttackerData = new CardData { id = "test_strong_attacker2", name = "Test Strong Attacker 2", art_file = "x.png", element = "Andras", type = "knight", rarity = 7 };
            Card weakDefender = Card.FromData(weakDefenderData);
            Card strongAttacker = Card.FromData(strongAttackerData);

            var defenderSide = new PlayerBattleState(new List<Card> { weakDefender }, resourceCap: 20, turn1Resource: 20, startingAvatarHealth: 1000);
            var attackerSide = new PlayerBattleState(new List<Card> { strongAttacker }, resourceCap: 20, turn1Resource: 20, startingAvatarHealth: 1000);
            defenderSide.Lanes[Lane.Front].Cards.Add(new BattleCardInstance(weakDefender, true, 0, 0));
            attackerSide.Lanes[Lane.Front].Cards.Add(new BattleCardInstance(strongAttacker, false, 0, 0));

            int expectedRawOverflow = strongAttacker.Attack - weakDefender.Health;
            Assert.Greater(expectedRawOverflow, 0, "Test setup invariant broken: expected the weak defender's lane to be overwhelmed.");

            TurnResolutionResult result = LaneBattleResolver.ResolveTurn(defenderSide, attackerSide);

            Assert.AreEqual(expectedRawOverflow * multiplier, result.DamageDealtToSideA,
                "Expected the reported Avatar damage to already be the multiplied amount, not the raw lane overflow.");
            Assert.AreEqual(1000 - (expectedRawOverflow * multiplier), defenderSide.AvatarHealth,
                "Expected AvatarHealth to drop by (raw overflow * AvatarDamageMultiplier), not by the raw overflow alone.");
            Assert.Greater(multiplier, 1,
                "The multiplier exists to reconcile small card stats with a large Avatar HP pool - " +
                "at 1 it would do nothing and matches would take dozens of extra ticks.");
        }

        [Test]
        public void LaneBattleResolver_ClearedLaneIsPrunedAndDoesNotLeakOverflowAgain()
        {
            // Regression test for the dead-card bug fixed 2026-08-05: a lane, once fully
            // cleared, must become genuinely empty - not a pile of 0-HP corpses that (a) still
            // occupy slots, permanently blocking new plays there, and (b) keep re-reporting
            // "cleared" on every later resolution, leaking the same overflow damage again and
            // again even with zero new activity. This was the actual root cause behind "we do
            // zero damage to the enemy" (their lanes likely never fully cleared even once) and
            // Avatar HP crashing fast (one lane clearing early kept re-leaking full damage
            // every turn after).
            var weakDefenderData = new CardData { id = "test_weak_defender", name = "Test Weak Defender", art_file = "x.png", element = "Andras", type = "knight", rarity = 1 };
            var strongAttackerData = new CardData { id = "test_strong_attacker", name = "Test Strong Attacker", art_file = "x.png", element = "Andras", type = "knight", rarity = 7 };
            Card weakDefender = Card.FromData(weakDefenderData);
            Card strongAttacker = Card.FromData(strongAttackerData);
            Assert.Greater(strongAttacker.Attack, weakDefender.Health,
                "Test setup invariant broken: rarity 7 Attack (7-12) should exceed rarity 1 Health (1-2).");

            var defendingLane = new LaneState(Lane.Front);
            defendingLane.Cards.Add(new BattleCardInstance(weakDefender, true, 0, 0));
            var attackingLane = new LaneState(Lane.Front);
            attackingLane.Cards.Add(new BattleCardInstance(strongAttacker, false, 0, 0));

            LaneClashResult firstClash = LaneBattleResolver.ResolveLaneClash(defendingLane, attackingLane);
            Assert.IsTrue(firstClash.SideACleared, "Test setup invariant broken: expected the weak defender's lane to be cleared.");
            Assert.Greater(firstClash.OverflowToA, 0, "Expected real overflow damage on the turn the lane actually cleared.");

            Assert.AreEqual(0, defendingLane.Cards.Count,
                "Cleared lane should be genuinely empty (dead cards pruned), not left full of 0-HP husks.");
            Assert.IsTrue(defendingLane.HasOpenSlot, "A pruned/empty lane must free up its slots for new cards.");

            // Semantics deliberately changed 2026-08-05 (see ApplyDamageToLane): this used to
            // assert that a now-empty lane produces NO overflow on later turns. That reading is
            // what produced "I deal zero damage to the Avatar after 2 rounds" - it also meant an
            // undefended lane swallowed every attack forever, so leaving a lane empty was a
            // perfect defence. A live attacker facing an empty lane SHOULD reach the Avatar.
            LaneClashResult secondClash = LaneBattleResolver.ResolveLaneClash(defendingLane, attackingLane);
            Assert.Greater(secondClash.OverflowToA, 0,
                "A surviving attacker facing an empty lane should keep reaching the Avatar - an " +
                "undefended lane is not supposed to be a permanent shield.");

            // The part of the original bug that must NOT come back: overflow leaking from a lane
            // with no living attacker opposite it. Before the dead-card pruning fix, corpses left
            // in the lane kept re-reporting "cleared" every turn and leaked damage with zero
            // activity on either side. With both lanes genuinely empty, nothing should happen.
            attackingLane.Cards.Clear();
            LaneClashResult idleClash = LaneBattleResolver.ResolveLaneClash(defendingLane, attackingLane);
            Assert.AreEqual(0, idleClash.OverflowToA,
                "With no living attacker, an empty lane must not leak any overflow at all.");
            Assert.AreEqual(0, idleClash.OverflowToB,
                "With no living attacker, an empty lane must not leak any overflow at all.");
        }

        [Test]
        public void LaneBattleResolver_TauntAbsorbsDamageBeforeNonTauntUnitsInTheSameLane()
        {
            // Taunt priority (ApplyDamageToLane: "taunts.Concat(others)") is the rule the whole
            // formation layer's Taunt/lane-holding trade rests on (see
            // docs/Mechanics_Gap_Analysis.md §1.1, "Partial overflow... directly removes the
            // point of Taunt and of holding a lane"), yet nothing exercised it directly before
            // this test.
            var attackerData = new CardData { id = "taunt_test_attacker", name = "Taunt Test Attacker", art_file = "x.png", element = "Andras", type = "warrior", rarity = 1 };
            var tauntData = new CardData { id = "taunt_test_knight", name = "Taunt Test Knight", art_file = "x.png", element = "Andras", type = "knight", rarity = 7 };
            var nonTauntData = new CardData { id = "taunt_test_nontaunt", name = "Taunt Test Non-Taunt", art_file = "x.png", element = "Andras", type = "warrior", rarity = 4 };
            Card attackerCard = Card.FromData(attackerData);
            Card tauntCard = Card.FromData(tauntData);
            Card nonTauntCard = Card.FromData(nonTauntData);
            Assert.Less(attackerCard.Attack, tauntCard.Health,
                "Test setup invariant broken: rarity 1 Attack (1-2) should always be less than rarity 7 Health (7-12), so the Taunt unit survives absorbing the full hit.");

            var attackingLane = new LaneState(Lane.Front);
            attackingLane.Cards.Add(new BattleCardInstance(attackerCard, true, 0, 0));

            var defendingLane = new LaneState(Lane.Front);
            // Non-Taunt placed FIRST in the list, Taunt SECOND - Taunt priority must come from
            // HasTaunt, not from placement order, or this deliberately-reversed setup would
            // fail to catch a regression to plain placement-order damage.
            var nonTaunt = new BattleCardInstance(nonTauntCard, false, 0, 0);
            var taunt = new BattleCardInstance(tauntCard, false, 0, 0);
            defendingLane.Cards.Add(nonTaunt);
            defendingLane.Cards.Add(taunt);

            int tauntHealthBefore = taunt.CurrentHealth;
            int nonTauntHealthBefore = nonTaunt.CurrentHealth;

            LaneClashResult result = LaneBattleResolver.ResolveLaneClash(defendingLane, attackingLane);

            Assert.AreEqual(tauntHealthBefore - attackerCard.Attack, taunt.CurrentHealth,
                "The Taunt unit should absorb the attacking lane's full Attack.");
            Assert.IsTrue(taunt.IsAlive, "Test setup invariant broken: the Taunt unit should survive absorbing this hit.");
            Assert.AreEqual(nonTauntHealthBefore, nonTaunt.CurrentHealth,
                "A non-Taunt unit must take zero damage while a living Taunt unit shares its lane.");
            Assert.IsFalse(result.SideACleared, "The lane should not be reported as cleared while both units are alive.");
        }

        [Test]
        public void LaneBattleResolver_DamageSpillsToNonTauntUnitsOnceTheTauntUnitDies()
        {
            // The other half of the same rule: "then the rest in placement order" - once the
            // Taunt unit's Health is exhausted, the remainder must not vanish or leak past a
            // lane that still has a living (non-Taunt) defender.
            var attackerData = new CardData { id = "taunt_spill_attacker", name = "Taunt Spill Attacker", art_file = "x.png", element = "Andras", type = "warrior", rarity = 4 };
            var tauntData = new CardData { id = "taunt_spill_knight", name = "Taunt Spill Knight", art_file = "x.png", element = "Andras", type = "knight", rarity = 1 };
            var nonTauntData = new CardData { id = "taunt_spill_nontaunt", name = "Taunt Spill Non-Taunt", art_file = "x.png", element = "Andras", type = "warrior", rarity = 7 };
            Card attackerCard = Card.FromData(attackerData);
            Card tauntCard = Card.FromData(tauntData);
            Card nonTauntCard = Card.FromData(nonTauntData);

            var attackingLane = new LaneState(Lane.Front);
            attackingLane.Cards.Add(new BattleCardInstance(attackerCard, true, 0, 0));

            var defendingLane = new LaneState(Lane.Front);
            var taunt = new BattleCardInstance(tauntCard, false, 0, 0);
            var nonTaunt = new BattleCardInstance(nonTauntCard, false, 0, 0);
            // Forced to a known small Health so the spillover amount is exactly computable
            // regardless of the hash-derived variance within rarity 1's 1-2 Health range.
            taunt.ApplyDamage(taunt.CurrentHealth - 1);
            Assert.AreEqual(1, taunt.CurrentHealth, "Test setup: Taunt unit should be forced to exactly 1 Health.");
            defendingLane.Cards.Add(taunt);
            defendingLane.Cards.Add(nonTaunt);

            int expectedRemainder = attackerCard.Attack - 1;
            int nonTauntHealthBefore = nonTaunt.CurrentHealth;
            Assert.Greater(expectedRemainder, 0,
                "Test setup invariant broken: rarity 4 Attack (4-6) minus the Taunt unit's forced 1 Health should always leave a remainder.");
            Assert.Greater(nonTauntHealthBefore, expectedRemainder,
                "Test setup invariant broken: rarity 7 Health (7-12) should always exceed the maximum possible spillover (5) from a rarity 4 attacker.");

            LaneClashResult result = LaneBattleResolver.ResolveLaneClash(defendingLane, attackingLane);

            Assert.IsFalse(taunt.IsAlive, "The Taunt unit should die once its Health is exhausted.");
            Assert.AreEqual(0, taunt.CurrentHealth);
            Assert.AreEqual(nonTauntHealthBefore - expectedRemainder, nonTaunt.CurrentHealth,
                "The remainder left over once the Taunt unit dies must spill onto the next living defender, not vanish.");
            Assert.IsTrue(nonTaunt.IsAlive, "Test setup invariant broken: the non-Taunt unit should survive the spillover.");
            Assert.IsFalse(result.SideACleared,
                "The lane should not be reported as cleared while the non-Taunt unit survives the spillover.");
            Assert.AreEqual(0, result.OverflowToA,
                "No overflow should reach the Avatar while a living defender (the non-Taunt unit) remains in the lane.");
        }

        [Test]
        public void GameBootstrap_Initialize_BuildsAPlayableMatchWithARenderedHand()
        {
            // This is the actual regression test for today's incident: GameBootstrap.cs was
            // silently reconstructed after a data-loss event, and nothing caught that the
            // rebuilt version had a real bug (an empty hand row) until a human looked at a
            // screenshot. BattleLogicTests above never touch GameBootstrap at all - they only
            // cover battle logic - so this is the only automated check that GameBootstrap's
            // Initialize() actually wires up a playable match instead of silently doing nothing.
            var go = new GameObject("TestGameBootstrap");
            _spawned.Add(go);
            GameBootstrap bootstrap = go.AddComponent<GameBootstrap>();

            // GameBootstrap.Initialize() spawns its own top-level GameObjects (Canvas,
            // EventSystem, CardDatabase, BattleController) that it doesn't keep references to -
            // find and register them for cleanup so this test doesn't leak a CardDatabase
            // singleton into whichever test happens to run next.
            bootstrap.Initialize();
            foreach (string name in new[] { "Canvas", "EventSystem", "CardDatabase", "BattleController" })
            {
                GameObject spawned = GameObject.Find(name);
                if (spawned != null) _spawned.Add(spawned);
            }

            Assert.NotNull(bootstrap.Battle, "Expected Initialize() to create and wire up a BattleController.");
            Assert.Greater(bootstrap.HandCardCount, 0,
                "Expected the starting hand to render at least one card button - an empty hand row here " +
                "is exactly the bug a manual playtest caught after this file was reconstructed.");

            // Also exercise a second RefreshAll() cycle (via End Turn) - this is what actually
            // hits RefreshHand/RefreshLaneSlots destroying and rebuilding their previous
            // children, not the first call from Initialize() where those containers start empty.
            Assert.DoesNotThrow(() => bootstrap.EndTurnForTests(),
                "End Turn should not throw when it destroys and rebuilds the hand/lane displays.");
            Assert.Greater(bootstrap.HandCardCount, 0, "Expected the hand to still render cards after End Turn.");
        }

        [Test]
        public void LaneBattleResolver_AttackingAnUndefendedLane_DamagesTheAvatar()
        {
            CardDatabase db = LoadDatabase();
            BattleController controller = CreateController();
            List<Card> deck = db.AllCards.Take(12).ToList();
            controller.StartMatch(deck.ToList(), deck.ToList(), TestEconomy(40, 40), TestEconomy(40, 40));

            // Player commits to Front; the enemy defends nothing at all.
            Card attacker = controller.PlayerState.Hand.First(c => c.ResourceCost <= 40);
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, attacker, Lane.Front),
                "Setup failure: expected to be able to play a card into the Front lane.");

            int enemyHealthBefore = controller.EnemyState.AvatarHealth;
            TurnResolutionResult result = LaneBattleResolver.ResolveTurn(controller.PlayerState, controller.EnemyState);

            // Regression guard for "I deal zero damage to the Avatar after 2 rounds": overflow
            // used to be gated on IsFullyCleared, which is false for a lane that was never
            // occupied - so attacking into thin air did precisely nothing.
            Assert.Greater(result.DamageDealtToSideB, 0,
                "An attack into a completely undefended lane must reach the enemy Avatar.");
            Assert.Less(controller.EnemyState.AvatarHealth, enemyHealthBefore,
                "The enemy Avatar's health should actually drop when its lane is undefended.");
        }

        [Test]
        public void LaneBattleResolver_ADefendedLaneStillBlocksOverflow()
        {
            CardDatabase db = LoadDatabase();
            BattleController controller = CreateController();
            List<Card> deck = db.AllCards.Take(12).ToList();
            controller.StartMatch(deck.ToList(), deck.ToList(), TestEconomy(40, 40), TestEconomy(40, 40));

            // Give the defender a high-Health card so it cannot possibly be cleared in one hit,
            // otherwise this test would pass for the wrong reason.
            Card defender = controller.EnemyState.Hand.OrderByDescending(c => c.Health).First();
            Assert.IsTrue(controller.TryPlayCard(controller.EnemyState, defender, Lane.Front),
                "Setup failure: expected to be able to play a defender into the Front lane.");

            Card attacker = controller.PlayerState.Hand.OrderBy(c => c.Attack).First();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, attacker, Lane.Front),
                "Setup failure: expected to be able to play an attacker into the Front lane.");

            // Only meaningful while the defender genuinely survives the hit - if the attacker
            // could one-shot it the lane would legitimately clear and overflow would be correct.
            // Must guard against the EFFECTIVE attack, not the raw card value: ResolveLaneClash
            // applies elemental advantage (+25%, rounded) to the lane's total attack before damage
            // is dealt (see LaneBattleResolver.ApplyElementalAdvantage), and with only one card in
            // this lane the lane total IS the card's Attack. A test run that happened to draw a
            // favourable element matchup could boost the attacker past the defender's Health while
            // this guard was still comparing against the un-boosted number - which is exactly how
            // this test flaked once already (real overflow, wrong reason: an under-guarded setup,
            // not a resolver bug).
            bool attackerHasAdvantage = LaneBattleResolver.Counters(attacker.Element, defender.Element);
            float boost = attackerHasAdvantage ? (1f + LaneBattleResolver.ElementalAdvantageBonus) : 1f;
            int effectiveAttack = (int)System.Math.Round(attacker.Attack * boost, System.MidpointRounding.AwayFromZero);

            if (defender.Health <= effectiveAttack + 1) Assert.Ignore("Deck produced no defender that survives one hit.");

            int enemyHealthBefore = controller.EnemyState.AvatarHealth;
            LaneBattleResolver.ResolveTurn(controller.PlayerState, controller.EnemyState);

            Assert.AreEqual(enemyHealthBefore, controller.EnemyState.AvatarHealth,
                "A lane with a surviving defender must still absorb everything - the undefended-lane " +
                "fix must not turn into 'all damage always reaches the Avatar'.");
        }

        [Test]
        public void GameBootstrap_RecommendedLineup_PicksStrongerCardsThanADefaultDeck()
        {
            var bootstrap = new GameObject("TestBootstrap_Recommend").AddComponent<GameBootstrap>();
            _spawned.Add(bootstrap.gameObject);
            bootstrap.Initialize();

            foreach (string name in new[] { "Canvas", "EventSystem", "CardDatabase", "BattleController" })
            {
                GameObject spawned = GameObject.Find(name);
                if (spawned != null) _spawned.Add(spawned);
            }

            double defaultPower = bootstrap.Battle.PlayerState.DrawPile
                .Concat(bootstrap.Battle.PlayerState.Hand)
                .Average(c => c.Attack + c.Health);

            bootstrap.UseRecommendedLineupForTests();

            double recommendedPower = bootstrap.Battle.PlayerState.DrawPile
                .Concat(bootstrap.Battle.PlayerState.Hand)
                .Average(c => c.Attack + c.Health);

            // "Recommended" used to shuffle randomly inside each cost band, so it produced a deck
            // with a tidy curve but no better cards than Reset - reported directly as "the
            // recommended line up should pick the best card to play".
            Assert.Greater(recommendedPower, defaultPower,
                "The recommended lineup should average stronger cards than the default split.");
        }

        [Test]
        public void RecommendedLineup_ActuallyDeploysTheSquadOntoTheBoard()
        {
            var bootstrap = new GameObject("TestBootstrap_AutoDeploy").AddComponent<GameBootstrap>();
            _spawned.Add(bootstrap.gameObject);
            bootstrap.Initialize();

            foreach (string name in new[] { "Canvas", "EventSystem", "CardDatabase", "BattleController" })
            {
                GameObject spawned = GameObject.Find(name);
                if (spawned != null) _spawned.Add(spawned);
            }

            int deployedBefore = bootstrap.Battle.PlayerState.Lanes.Values.Sum(l => l.Cards.Count);
            Assert.AreEqual(0, deployedBefore, "A fresh match should start with an empty board.");

            bootstrap.UseRecommendedLineupForTests();

            int deployedAfter = bootstrap.Battle.PlayerState.Lanes.Values.Sum(l => l.Cards.Count);

            // "The recommendation is not working whereby it should select the card for the
            // players" - it used to rebuild the deck only, which is invisible: the board still
            // came up empty and every card still had to be placed by hand.
            Assert.Greater(deployedAfter, 0,
                "Recommended should place the squad on the board, not just reshuffle the deck.");
        }

        [Test]
        public void RecommendedLineup_PlacesKnightsInMiddleAndStrategistsInBack()
        {
            var bootstrap = new GameObject("TestBootstrap_LanePrefs").AddComponent<GameBootstrap>();
            _spawned.Add(bootstrap.gameObject);
            bootstrap.Initialize();

            foreach (string name in new[] { "Canvas", "EventSystem", "CardDatabase", "BattleController" })
            {
                GameObject spawned = GameObject.Find(name);
                if (spawned != null) _spawned.Add(spawned);
            }

            bootstrap.UseRecommendedLineupForTests();
            PlayerBattleState player = bootstrap.Battle.PlayerState;

            // Only assert on lanes that filled up from preference rather than from fallback -
            // once a preferred lane is full the remaining cards legitimately spill elsewhere.
            var middle = player.Lanes[Lane.Middle].Cards.Select(c => c.Definition.Class).ToList();
            var back = player.Lanes[Lane.Back].Cards.Select(c => c.Definition.Class).ToList();

            if (middle.Count > 0 && middle.Count < LaneState.MaxSlots)
            {
                Assert.IsTrue(middle.All(c => c == CardClass.Knight),
                    "Middle (+1 Health) should be reserved for Knights while it still has room - " +
                    "Taunt is what the extra Health is for.");
            }
            if (back.Count > 0 && back.Count < LaneState.MaxSlots)
            {
                Assert.IsTrue(back.All(c => c is CardClass.Strategist or CardClass.Perfect),
                    "Back should hold the draw-hook classes while it still has room - Perfect's " +
                    "draw only fires in the Back lane at all.");
            }
        }

        [Test]
        public void FormationSynergy_MatchingTagsBuffTheWholeSquad()
        {
            CardDatabase db = LoadDatabase();

            // Three Knights all carry AegisGuard, which is the 3-of-a-kind threshold.
            List<Card> knights = db.AllCards.Where(c => c.Class == CardClass.Knight).Take(3).ToList();
            if (knights.Count < 3) Assert.Ignore("Card pool has fewer than 3 Knights to test with.");

            SynergyBonus trio = FormationSynergy.Calculate(knights);
            Assert.Greater(trio.AttackBonus, 0, "Three matching tags should grant an Attack bonus.");
            Assert.Greater(trio.HealthBonus, 0, "Three matching tags should grant a Health bonus.");

            SynergyBonus pair = FormationSynergy.Calculate(knights.Take(2));
            Assert.Greater(pair.AttackBonus, 0, "Two matching tags should still grant something.");
            Assert.Less(pair.AttackBonus, trio.AttackBonus,
                "A pair should be worth strictly less than a trio, or there's no reason to field the third.");

            SynergyBonus single = FormationSynergy.Calculate(knights.Take(1));
            Assert.IsFalse(single.HasAny, "A lone card has nothing to synergise with.");
        }

        [Test]
        public void FormationSynergy_BonusesFromDifferentTagsStackTogether()
        {
            // FormationSynergy.Calculate's own doc comment: "Bonuses from different tags stack,
            // so a squad running two separate pairs is rewarded for both" - never directly
            // exercised with two distinct tags active at once, only ever a single tag at
            // varying counts.
            CardDatabase db = LoadDatabase();
            List<Card> knights = db.AllCards.Where(c => c.Class == CardClass.Knight).Take(2).ToList();
            List<Card> strategists = db.AllCards.Where(c => c.Class == CardClass.Strategist).Take(2).ToList();
            if (knights.Count < 2 || strategists.Count < 2)
            {
                Assert.Ignore("Card pool has fewer than 2 Knights or 2 Strategists to test with.");
            }

            SynergyBonus knightPairAlone = FormationSynergy.Calculate(knights);
            SynergyBonus strategistPairAlone = FormationSynergy.Calculate(strategists);
            SynergyBonus combined = FormationSynergy.Calculate(knights.Concat(strategists));

            Assert.AreEqual(knightPairAlone.AttackBonus + strategistPairAlone.AttackBonus, combined.AttackBonus,
                "Two different tags' pair bonuses should add together, not override or cap each other.");
            Assert.That(combined.ActiveTags, Has.Member(SkillTag.AegisGuard),
                "Both active tags should be reported, not just whichever was counted last.");
            Assert.That(combined.ActiveTags, Has.Member(SkillTag.TacticalCommand),
                "Both active tags should be reported, not just whichever was counted last.");
        }

        [Test]
        public void FormationSynergy_AppliedToDeployedUnitsWhenFormationLocks()
        {
            CardDatabase db = LoadDatabase();
            BattleController controller = CreateController();
            // One-slot Knights only (rarity <= 4). A rarity 5+ card takes two of a lane's three
            // slots, so three big Knights cannot share a lane at all - without this filter the
            // test silently skipped itself and asserted nothing.
            List<Card> knights = db.AllCards
                .Where(c => c.Class == CardClass.Knight && c.SlotWeight == 1)
                .Take(3)
                .ToList();
            if (knights.Count < 3) Assert.Ignore("Card pool has fewer than 3 one-slot Knights to test with.");

            controller.StartMatch(knights.ToList(), knights.ToList(),
                new BattleController.MatchEconomy(60, 60, 2000),
                new BattleController.MatchEconomy(60, 60, 2000));
            controller.DealFormationHand(controller.PlayerState);

            var deployed = new List<Card>();
            foreach (Card card in controller.PlayerState.Hand.ToList())
            {
                if (controller.TryPlayCard(controller.PlayerState, card, Lane.Back)) deployed.Add(card);
            }
            if (deployed.Count < 3) Assert.Ignore("Could not deploy 3 Knights into one lane.");

            // Back lane grants no stat bonus, so any increase here is the synergy and nothing else.
            List<int> attackBefore = controller.PlayerState.Lanes[Lane.Back].Cards.Select(c => c.Attack).ToList();
            Assert.IsTrue(controller.ConfirmFormation());
            List<int> attackAfter = controller.PlayerState.Lanes[Lane.Back].Cards.Select(c => c.Attack).ToList();

            for (int i = 0; i < attackBefore.Count; i++)
            {
                Assert.Greater(attackAfter[i], attackBefore[i],
                    "Locking a synergised formation should buff every deployed unit, not just some.");
            }
            Assert.IsTrue(controller.PlayerSynergy.HasAny,
                "The applied bonus should be exposed so the HUD can show what the composition earned.");
        }

        [Test]
        public void IntroContent_LoadsRealDataForTheIntroAndTutorial()
        {
            // RESTORED 2026-08-07 against GameBootstrap's own JSON loaders (see that file's
            // MaybeShowTutorial comment) after the metagame side rebuilt MyriadOfDragons.Story
            // into a static, in-code per-stage dialogue lookup with no equivalent for
            // TutorialStep.highlight. This is now the only automated coverage of the tutorial
            // content actually loading correctly.
            //
            // Story is optional content - a missing or malformed file must never stop the game
            // booting into a playable battle, so both loaders are required not to throw either way.
            GameBootstrap.IntroChapterData prologue = null;
            GameBootstrap.IntroTutorialStepData[] steps = null;
            Assert.DoesNotThrow(() => prologue = GameBootstrap.LoadIntroChapter("prologue"));
            Assert.DoesNotThrow(() => steps = GameBootstrap.LoadTutorialSteps());

            Assert.IsNotNull(prologue, "The intro sequence is driven by a chapter with id 'prologue'.");
            Assert.IsNotEmpty(prologue.beats, "The prologue needs beats to show.");
            Assert.IsNotEmpty(steps, "The tutorial needs steps to show.");

            // Guards against the placeholder text being shipped by accident - it said
            // PLACEHOLDER in capitals precisely so this check could catch it.
            foreach (GameBootstrap.IntroBeatData beat in prologue.beats)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(beat.text), "Every beat needs text.");
                Assert.IsFalse(beat.text.Contains("PLACEHOLDER"),
                    "Placeholder story text should not survive into a real chapter.");
            }
            foreach (GameBootstrap.IntroTutorialStepData step in steps)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(step.instruction), "Every step needs an instruction.");
                Assert.IsFalse(step.instruction.Contains("PLACEHOLDER"),
                    "Placeholder tutorial text should not survive into the real tutorial.");
            }
        }

        [Test]
        public void LanePicker_RecallReturnsTheCardToHandAndRefundsItsResource()
        {
            BattleController controller = StartFormationMatch();

            Card card = controller.PlayerState.Hand.First();
            int resourceBefore = controller.PlayerState.Resource;
            int handBefore = controller.PlayerState.Hand.Count;

            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, card, Lane.Front));
            Assert.AreEqual(resourceBefore - card.ResourceCost, controller.PlayerState.Resource,
                "Deploying should spend the card's Resource cost.");

            Assert.IsTrue(controller.TryRecallCard(Lane.Front, 0),
                "A card deployed during Formation should be recallable - a picker you can't undo " +
                "is a one-way commit, not a picker.");

            Assert.AreEqual(resourceBefore, controller.PlayerState.Resource,
                "Recalling should refund exactly what deploying cost.");
            Assert.AreEqual(0, controller.PlayerState.Lanes[Lane.Front].Cards.Count,
                "The lane slot should be free again.");
            Assert.IsTrue(controller.PlayerState.Hand.Contains(card),
                "The recalled card should be back in hand and re-deployable.");
            Assert.GreaterOrEqual(controller.PlayerState.Hand.Count, handBefore - 1,
                "Recall should not lose the card entirely.");
        }

        [Test]
        public void LanePicker_RecallIsRejectedOnceCombatHasStarted()
        {
            BattleController controller = StartFormationMatch();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, controller.PlayerState.Hand.First(), Lane.Front));
            Assert.IsTrue(controller.ConfirmFormation());

            Assert.IsFalse(controller.TryRecallCard(Lane.Front, 0),
                "The squad is locked once combat begins - that lock is what makes the " +
                "reinforcement windows meaningful.");
            Assert.AreEqual(1, controller.PlayerState.Lanes[Lane.Front].Cards.Count,
                "A rejected recall must leave the board untouched.");
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void LanePicker_RecallWithAnInvalidSlotIndex_DoesNotChangeState(int invalidSlotIndex)
        {
            // TryRecallCard's second guard clause (slotIndex < 0 || slotIndex >= Cards.Count) was
            // never exercised - only the Phase guard (LanePicker_RecallIsRejectedOnceCombatHasStarted)
            // had atomicity coverage. One card is deployed into Front, occupying only slot 0, so
            // -1 and 1 exercise the two directions of the same bounds check.
            BattleController controller = StartFormationMatch();
            Card card = controller.PlayerState.Hand.First();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, card, Lane.Front));

            int resourceBefore = controller.PlayerState.Resource;
            int handCountBefore = controller.PlayerState.Hand.Count;
            int laneCardsBefore = controller.PlayerState.Lanes[Lane.Front].Cards.Count;

            Assert.IsFalse(controller.TryRecallCard(Lane.Front, invalidSlotIndex),
                "An out-of-range slot index must be rejected, even during Formation.");

            Assert.AreEqual(resourceBefore, controller.PlayerState.Resource, "A rejected recall must not refund Resource.");
            Assert.AreEqual(handCountBefore, controller.PlayerState.Hand.Count, "A rejected recall must not return a card to hand.");
            Assert.AreEqual(laneCardsBefore, controller.PlayerState.Lanes[Lane.Front].Cards.Count,
                "A rejected recall must not remove the deployed card from the lane.");
        }

        // ---------- Formation phase + automated combat + Avatar spells ----------

        private BattleController StartFormationMatch(int enemyHealth = 2000)
        {
            CardDatabase db = LoadDatabase();
            BattleController controller = CreateController();
            List<Card> deck = db.AllCards.Take(15).ToList();
            controller.StartMatch(deck.ToList(), deck.ToList(),
                new BattleController.MatchEconomy(60, 60, 2000),
                new BattleController.MatchEconomy(60, 60, enemyHealth));
            controller.DealFormationHand(controller.PlayerState);
            controller.DealFormationHand(controller.EnemyState);
            return controller;
        }

        [Test]
        public void Formation_DealsAWholeSquadUpFront_InsteadOfTwoCardsPerTurn()
        {
            BattleController controller = StartFormationMatch();

            Assert.AreEqual(BattlePhase.Formation, controller.Phase,
                "A match should open in the Formation phase.");
            Assert.GreaterOrEqual(controller.PlayerState.Hand.Count, 3 * LaneState.MaxSlots,
                "The formation hand should cover the whole board at once - dealing two cards a " +
                "turn is exactly the fatigue this model removes.");
        }

        [Test]
        public void Formation_CannotStartCombatWithAnEmptyBoard()
        {
            BattleController controller = StartFormationMatch();

            Assert.IsFalse(controller.ConfirmFormation(),
                "Confirming an empty formation should be rejected, not start a fight with no squad.");
            Assert.AreEqual(BattlePhase.Formation, controller.Phase,
                "A rejected confirmation must leave the phase untouched.");
        }

        [Test]
        public void Formation_ConfirmingTwice_IsRejectedAndDoesNotDoubleApplySynergy()
        {
            // ConfirmFormation's own Phase guard is the only thing preventing ApplySynergy's
            // direct BuffAttack/BuffMaxHealth mutation from firing a second time - nothing
            // exercised a second call before this. A pair of Knights (a real, nonzero synergy
            // bonus) makes a silent double-application visible rather than invisible.
            CardDatabase db = LoadDatabase();
            List<Card> knights = db.AllCards
                .Where(c => c.Class == CardClass.Knight && c.SlotWeight == 1)
                .Take(2)
                .ToList();
            if (knights.Count < 2) Assert.Ignore("Card pool has fewer than 2 one-slot Knights to test with.");

            BattleController controller = CreateController();
            var economy = new BattleController.MatchEconomy(60, 60, 2000);
            controller.StartMatch(knights.ToList(), knights.ToList(), economy, economy);
            controller.DealFormationHand(controller.PlayerState);
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, knights[0], Lane.Back));
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, knights[1], Lane.Back));

            Assert.IsTrue(controller.ConfirmFormation(), "Setup: the first confirmation should lock the formation.");
            SynergyBonus synergyAfterFirstLock = controller.PlayerSynergy;
            Assert.IsTrue(synergyAfterFirstLock.HasAny, "Setup: two matching-tag Knights should earn a pair bonus at lock-in.");
            List<BattleCardInstance> units = controller.PlayerState.Lanes[Lane.Back].Cards.ToList();
            List<int> attackAfterFirstLock = units.Select(c => c.Attack).ToList();
            List<int> healthAfterFirstLock = units.Select(c => c.MaxHealth).ToList();

            Assert.IsFalse(controller.ConfirmFormation(), "A second confirmation must be rejected - the squad is already locked.");
            Assert.AreEqual(BattlePhase.Combat, controller.Phase, "A rejected second confirmation must leave the phase as Combat.");
            Assert.AreEqual(synergyAfterFirstLock.AttackBonus, controller.PlayerSynergy.AttackBonus,
                "A rejected second confirmation must not change the recorded synergy.");
            Assert.AreEqual(synergyAfterFirstLock.HealthBonus, controller.PlayerSynergy.HealthBonus,
                "A rejected second confirmation must not change the recorded synergy.");
            for (int i = 0; i < units.Count; i++)
            {
                Assert.AreEqual(attackAfterFirstLock[i], units[i].Attack,
                    "A rejected second confirmation must not double-apply the synergy bonus.");
                Assert.AreEqual(healthAfterFirstLock[i], units[i].MaxHealth,
                    "A rejected second confirmation must not double-apply the synergy bonus.");
            }
        }

        [Test]
        public void Combat_AdvanceCombatTickDuringFormation_IsANoOp()
        {
            // AdvanceCombatTick's own Phase guard was never exercised - every existing test calls
            // it only after ConfirmFormation.
            BattleController controller = StartFormationMatch();
            Assert.AreEqual(BattlePhase.Formation, controller.Phase, "Setup: match should still be in Formation.");

            TurnResolutionResult result = controller.AdvanceCombatTick();

            Assert.AreEqual(0, controller.TickCount, "A tick advanced before combat starts must not count.");
            Assert.AreEqual(0, controller.Energy, "A tick advanced before combat starts must not accrue Energy.");
            Assert.AreEqual(BattlePhase.Formation, controller.Phase, "A no-op tick must not change the phase.");
            Assert.IsNull(result.LaneResults, "A no-op tick must return the default (unresolved) result, not a real clash.");
        }

        [Test]
        public void Combat_TicksAccrueEnergyAndDoNotRefillTheHand()
        {
            BattleController controller = StartFormationMatch();
            Card card = controller.PlayerState.Hand.First();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, card, Lane.Front));
            Assert.IsTrue(controller.ConfirmFormation());

            int handAfterLock = controller.PlayerState.Hand.Count;
            Assert.AreEqual(0, controller.Energy, "Energy should start empty when combat begins.");

            controller.AdvanceCombatTick();

            // The obvious per-frame implementation (`energy += rate * deltaTime` rounded to int)
            // rounds a 5/sec rate to 0 every frame at 60fps and never accrues anything at all.
            // Per-tick accrual is the fix, and this is the guard for it.
            Assert.Greater(controller.Energy, 0, "A combat tick must actually accrue Energy.");
            Assert.AreEqual(1, controller.TickCount, "AdvanceCombatTick should count the tick.");
            Assert.AreEqual(handAfterLock, controller.PlayerState.Hand.Count,
                "Combat must not keep drawing cards - the squad is locked once the fight starts.");
        }

        [Test]
        public void Spells_RespectEnergyCostAndCooldown()
        {
            BattleController controller = StartFormationMatch();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, controller.PlayerState.Hand.First(), Lane.Front));
            Assert.IsTrue(controller.ConfirmFormation());

            // Nothing is castable on an empty Energy pool.
            Assert.IsFalse(controller.TryCastSpell(0, Lane.Front, out _),
                "A spell must not be castable with no Energy.");

            // Bank enough Energy for the cheapest spell.
            int cheapestIndex = controller.Spellbook
                .Select((s, i) => (s, i))
                .OrderBy(pair => pair.s.EnergyCost)
                .First().i;
            while (controller.Energy < controller.Spellbook[cheapestIndex].EnergyCost
                   && controller.Phase == BattlePhase.Combat)
            {
                controller.AdvanceCombatTick();
            }
            if (controller.Phase != BattlePhase.Combat) Assert.Ignore("Match resolved before enough Energy accrued.");

            int energyBefore = controller.Energy;
            AvatarSpell spell = controller.Spellbook[cheapestIndex];
            Assert.IsTrue(controller.TryCastSpell(cheapestIndex, Lane.Front, out _),
                "A spell that is off cooldown and affordable should cast.");
            Assert.AreEqual(energyBefore - spell.EnergyCost, controller.Energy,
                "Casting should deduct exactly the spell's Energy cost.");
            Assert.IsFalse(spell.IsOffCooldown, "Casting should put the spell on cooldown.");
            Assert.IsFalse(controller.TryCastSpell(cheapestIndex, Lane.Front, out _),
                "A spell on cooldown must not cast again immediately.");
        }

        [Test]
        public void Spells_CannotBeCastDuringFormation()
        {
            // TryCastSpell's own doc comment: "Returns false with nothing changed if the cast
            // isn't legal." Every existing spell test casts from inside Combat, exercising the
            // cooldown/energy guards - the Phase guard itself was never directly exercised.
            BattleController controller = StartFormationMatch();
            Assert.AreEqual(BattlePhase.Formation, controller.Phase, "Setup: match should still be in Formation.");

            int enemyHealthBefore = controller.EnemyState.AvatarHealth;
            bool cast = controller.TryCastSpell(0, Lane.Front, out int dealt);

            Assert.IsFalse(cast, "A spell must not be castable before combat begins.");
            Assert.AreEqual(0, dealt, "A rejected cast must report zero damage dealt.");
            Assert.AreEqual(enemyHealthBefore, controller.EnemyState.AvatarHealth,
                "A rejected cast must not change the enemy Avatar's Health.");
            Assert.IsTrue(controller.Spellbook.All(s => s.IsOffCooldown), "A rejected cast must not put any spell on cooldown.");
        }

        [TestCase(-1)]
        [TestCase(int.MaxValue)]
        public void Spells_InvalidSpellIndex_IsRejectedWithoutChangingState(int invalidIndex)
        {
            // The other unexercised TryCastSpell guard: an out-of-range spellIndex. int.MaxValue
            // rather than Spellbook.Count, so this stays correct if the spellbook is ever resized.
            BattleController controller = StartFormationMatch();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, controller.PlayerState.Hand.First(), Lane.Front));
            Assert.IsTrue(controller.ConfirmFormation());
            controller.AdvanceCombatTick();
            Assert.Greater(controller.Energy, 0, "Setup: a combat tick should have accrued some Energy.");

            int energyBefore = controller.Energy;
            int enemyHealthBefore = controller.EnemyState.AvatarHealth;

            bool cast = controller.TryCastSpell(invalidIndex, Lane.Front, out int dealt);

            Assert.IsFalse(cast, "An out-of-range spell index must be rejected.");
            Assert.AreEqual(0, dealt, "A rejected cast must report zero damage dealt.");
            Assert.AreEqual(energyBefore, controller.Energy, "A rejected cast must not spend Energy.");
            Assert.AreEqual(enemyHealthBefore, controller.EnemyState.AvatarHealth,
                "A rejected cast must not change the enemy Avatar's Health.");
            Assert.IsTrue(controller.Spellbook.All(s => s.IsOffCooldown), "A rejected cast must not put any spell on cooldown.");
        }

        [Test]
        public void Spells_AvatarStrikeDamagesTheEnemyAvatarDirectly()
        {
            BattleController controller = StartFormationMatch();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, controller.PlayerState.Hand.First(), Lane.Front));
            Assert.IsTrue(controller.ConfirmFormation());

            int strikeIndex = controller.Spellbook.FindIndex(s => s.Effect == SpellEffect.AvatarStrike);
            Assert.GreaterOrEqual(strikeIndex, 0, "The default spellbook should contain an AvatarStrike spell.");

            // Hand it the Energy directly rather than ticking there - ticking would also deal
            // lane damage, which would make it impossible to attribute the HP drop to the spell.
            while (controller.Energy < controller.Spellbook[strikeIndex].EnergyCost
                   && controller.Phase == BattlePhase.Combat)
            {
                controller.AdvanceCombatTick();
            }
            if (controller.Phase != BattlePhase.Combat) Assert.Ignore("Match resolved before enough Energy accrued.");

            int healthBefore = controller.EnemyState.AvatarHealth;
            Assert.IsTrue(controller.TryCastSpell(strikeIndex, Lane.Front, out int dealt));

            Assert.Greater(dealt, 0, "AvatarStrike should report the damage it dealt.");
            Assert.AreEqual(healthBefore - dealt, controller.EnemyState.AvatarHealth,
                "The enemy Avatar's health should drop by exactly the reported amount.");
        }

        [Test]
        public void Spells_AvatarStrikeClampsToRemainingAvatarHealthAndNeverGoesNegative()
        {
            // AvatarSpell.Cast's own clamp (Math.Min(Magnitude, opponent.AvatarHealth)) had no
            // dedicated test - the existing AvatarStrike test only checked "some damage was
            // dealt", never the boundary where Magnitude exceeds what's left to take.
            BattleController controller = StartFormationMatch();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, controller.PlayerState.Hand.First(), Lane.Front));
            Assert.IsTrue(controller.ConfirmFormation());

            int strikeIndex = controller.Spellbook.FindIndex(s => s.Effect == SpellEffect.AvatarStrike);
            Assert.GreaterOrEqual(strikeIndex, 0, "The default spellbook should contain an AvatarStrike spell.");
            AvatarSpell strike = controller.Spellbook[strikeIndex];

            while (controller.Energy < strike.EnergyCost && controller.Phase == BattlePhase.Combat)
            {
                controller.AdvanceCombatTick();
            }
            if (controller.Phase != BattlePhase.Combat) Assert.Ignore("Match resolved before enough Energy accrued.");

            // Set remaining Health below the spell's own Magnitude directly, after Energy has
            // already safely accrued against a high-Health enemy - isolates the clamp itself from
            // whether ordinary combat happens to finish the enemy off first.
            Assert.Greater(strike.Magnitude, 0, "Sanity check: the spell must have a positive Magnitude for the clamp to mean anything.");
            controller.EnemyState.AvatarHealth = strike.Magnitude - 1;
            int healthBefore = controller.EnemyState.AvatarHealth;

            Assert.IsTrue(controller.TryCastSpell(strikeIndex, Lane.Front, out int dealt));

            Assert.AreEqual(healthBefore, dealt,
                "A strike exceeding remaining Health must be clamped to exactly the remaining Health, not the full Magnitude.");
            Assert.AreEqual(0, controller.EnemyState.AvatarHealth,
                "The enemy Avatar's Health must floor at exactly zero, never go negative.");
        }

        [Test]
        public void Spells_LaneAttackBuffPermanentlyRaisesAttackOfFriendlyUnitsInTheLane()
        {
            // SpellEffect.LaneAttackBuff (War Cry) had zero coverage anywhere in this file.
            BattleController controller = StartFormationMatch();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, controller.PlayerState.Hand.First(), Lane.Front));
            Assert.IsTrue(controller.ConfirmFormation());

            int buffIndex = controller.Spellbook.FindIndex(s => s.Effect == SpellEffect.LaneAttackBuff);
            Assert.GreaterOrEqual(buffIndex, 0, "The default spellbook should contain a LaneAttackBuff spell.");

            while (controller.Energy < controller.Spellbook[buffIndex].EnergyCost && controller.Phase == BattlePhase.Combat)
            {
                controller.AdvanceCombatTick();
            }
            if (controller.Phase != BattlePhase.Combat) Assert.Ignore("Match resolved before enough Energy accrued.");

            // The enemy's Front lane is empty, so it deals no damage back to the player's Front -
            // the deployed unit is guaranteed to still be alive here.
            BattleCardInstance frontUnit = controller.PlayerState.Lanes[Lane.Front].Cards[0];
            int attackBefore = frontUnit.Attack;

            Assert.IsTrue(controller.TryCastSpell(buffIndex, Lane.Front, out int dealt),
                "A LaneAttackBuff spell that is off cooldown and affordable should cast.");

            Assert.AreEqual(0, dealt, "LaneAttackBuff does not deal Avatar damage - it should report zero.");
            Assert.Greater(frontUnit.Attack, attackBefore,
                "The living unit in the target lane should have its Attack permanently raised.");
        }

        [Test]
        public void Spells_LaneHealRestoresDamagedUnitsButNeverRevivesTheDead()
        {
            CardDatabase db = LoadDatabase();
            Card definition = db.AllCards.OrderByDescending(c => c.Health).First();

            var alive = new BattleCardInstance(definition, true, 0, 0);
            var dead = new BattleCardInstance(definition, true, 0, 0);
            alive.ApplyDamage(2);
            dead.ApplyDamage(dead.MaxHealth);

            int woundedHealth = alive.CurrentHealth;
            alive.Heal(1);
            Assert.AreEqual(woundedHealth + 1, alive.CurrentHealth, "Healing should restore Health.");

            alive.Heal(999);
            Assert.AreEqual(alive.MaxHealth, alive.CurrentHealth,
                "Healing must cap at the unit's starting Health, not inflate it.");

            dead.Heal(999);
            Assert.IsFalse(dead.IsAlive,
                "Healing must not resurrect a dead unit - that would quietly undo the lane-clearing " +
                "rules that overflow damage depends on.");
        }

        [Test]
        public void Combat_CannotRunForeverWhenBothBoardsAreWipedOut()
        {
            BattleController controller = StartFormationMatch();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, controller.PlayerState.Hand.First(), Lane.Front));
            Assert.IsTrue(controller.ConfirmFormation());

            // Step well past the cap. The exact bug: once both squads wipe each other out every
            // lane is empty, total Attack is 0 on both sides, no further damage is possible, and
            // the match simply never ends - observed in play at Clash 7 with both boards empty
            // and both Avatars stuck above half Health.
            for (int i = 0; i < BattleController.MaxCombatTicks + 5; i++)
            {
                controller.AdvanceCombatTick();
            }

            Assert.AreEqual(BattlePhase.Resolved, controller.Phase,
                "A match must always terminate - the tick cap is what guarantees it.");
            Assert.LessOrEqual(controller.TickCount, BattleController.MaxCombatTicks,
                "Combat should stop advancing once the cap is reached.");
        }

        [Test]
        public void Combat_TickCapDecidesOnHealthFraction_NotRawHealth()
        {
            // Enemy has a far larger pool, so raw remaining Health would favour them even while
            // they're proportionally worse off. Fraction is the only fair comparison when the
            // two sides' maximums differ by design (they do - see SoloAIScalingSystem).
            CardDatabase db = LoadDatabase();
            BattleController controller = CreateController();
            List<Card> deck = db.AllCards.Take(15).ToList();
            controller.StartMatch(deck.ToList(), deck.ToList(),
                new BattleController.MatchEconomy(60, 60, 100),
                new BattleController.MatchEconomy(60, 60, 10000));
            controller.DealFormationHand(controller.PlayerState);
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, controller.PlayerState.Hand.First(), Lane.Back));
            Assert.IsTrue(controller.ConfirmFormation());

            bool? playerWon = null;
            controller.OnMatchEnded += won => playerWon = won;

            for (int i = 0; i < BattleController.MaxCombatTicks + 2 && controller.Phase == BattlePhase.Combat; i++)
            {
                controller.AdvanceCombatTick();
            }

            Assert.AreEqual(BattlePhase.Resolved, controller.Phase);
            Assert.IsNotNull(playerWon, "Reaching the cap must still raise OnMatchEnded.");
            Assert.IsNotEmpty(controller.OutcomeReason,
                "A cap-decided match should explain itself rather than claiming an Avatar fell.");
        }

        [Test]
        public void Combat_TickCapDrawWhenBothSidesEndAtEqualHealthFraction()
        {
            // ResolveOnTickCap's Mathf.Approximately(playerFraction, enemyFraction) branch - a
            // draw reported as a loss for progression but named honestly (see the production
            // comment on ResolveOnTickCap) - was never directly exercised: every existing
            // tick-cap test uses deliberately UNEQUAL pools to prove the fraction-not-raw-health
            // rule, and Draw_IsNeverRewardedMoreThanALoss only tests the downstream Empire
            // consequence of an already-false `won` flag, not that BattleController itself
            // detects a draw and produces one.
            //
            // A perfectly symmetric mirror match (the exact same Card instance deployed into the
            // same lane on both sides, identical starting Avatar Health) guarantees AvatarHealth
            // stays IDENTICAL on both sides every tick, by the same clash symmetry
            // LaneBattleResolver_ClashIsSymmetricAndOrderIndependent already proves - whichever
            // damage source fires (lane overflow, siege, or both), it applies the same formula to
            // the same inputs on both sides. This reaches the draw branch deterministically
            // without depending on any specific card's stats.
            CardDatabase db = LoadDatabase();
            Card mirrorCard = db.AllCards.First();
            var deck = new List<Card> { mirrorCard };

            BattleController controller = CreateController();
            var economy = new BattleController.MatchEconomy(60, 60, 6000);
            controller.StartMatch(deck, deck, economy, economy);
            controller.DealFormationHand(controller.PlayerState);
            controller.DealFormationHand(controller.EnemyState);

            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, mirrorCard, Lane.Front));
            Assert.IsTrue(controller.TryPlayCard(controller.EnemyState, mirrorCard, Lane.Front));
            Assert.IsTrue(controller.ConfirmFormation());

            bool? playerWon = null;
            controller.OnMatchEnded += won => playerWon = won;
            MatchResult? result = null;
            controller.OnMatchCompleted += r => result = r;

            for (int i = 0; i < BattleController.MaxCombatTicks && controller.Phase == BattlePhase.Combat; i++)
            {
                controller.AdvanceCombatTick();
            }

            Assert.AreEqual(BattlePhase.Resolved, controller.Phase, "Setup: the tick cap should resolve the match.");
            Assert.AreEqual(controller.PlayerState.AvatarHealth, controller.EnemyState.AvatarHealth,
                "Setup invariant: a perfectly symmetric mirror match must leave both Avatars at identical Health.");
            Assert.Less(controller.PlayerState.AvatarHealth, controller.PlayerState.MaxAvatarHealth,
                "Setup: the mirror match should have taken at least some damage, or this test isn't exercising a contested draw.");

            Assert.IsNotNull(playerWon, "Reaching the cap on a draw must still raise OnMatchEnded.");
            Assert.IsFalse(playerWon!.Value,
                "A draw must be reported as a loss for progression - the player must never be credited a victory nobody won.");
            Assert.IsTrue(result.HasValue, "OnMatchCompleted must still fire on a draw.");
            Assert.IsFalse(result!.Value.IsVictory,
                "OnMatchCompleted must agree with OnMatchEnded: a draw is not a victory.");
            Assert.IsTrue(controller.OutcomeReason.Contains("DRAW"),
                "A draw must explain itself distinctly from an ordinary victory/defeat, since nobody's Avatar actually fell.");
        }

        [Test]
        public void OnMatchCompleted_CarriesTheSameOutcomeAsOnMatchEnded_OnATickCapDecision()
        {
            // Same lopsided-pools setup as Combat_TickCapDecidesOnHealthFraction_NotRawHealth -
            // deliberately reused rather than a new scenario, so this test is verifying
            // OnMatchCompleted's data against a known-correct OnMatchEnded result, not just
            // asserting OnMatchCompleted in isolation.
            CardDatabase db = LoadDatabase();
            BattleController controller = CreateController();
            List<Card> deck = db.AllCards.Take(15).ToList();
            controller.StartMatch(deck.ToList(), deck.ToList(),
                new BattleController.MatchEconomy(60, 60, 100),
                new BattleController.MatchEconomy(60, 60, 10000));
            controller.DealFormationHand(controller.PlayerState);
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, controller.PlayerState.Hand.First(), Lane.Back));
            Assert.IsTrue(controller.ConfirmFormation());

            bool? playerWon = null;
            controller.OnMatchEnded += won => playerWon = won;
            MatchResult? result = null;
            int firedCount = 0;
            controller.OnMatchCompleted += r => { result = r; firedCount++; };

            for (int i = 0; i < BattleController.MaxCombatTicks + 2 && controller.Phase == BattlePhase.Combat; i++)
            {
                controller.AdvanceCombatTick();
            }

            Assert.AreEqual(1, firedCount, "OnMatchCompleted must fire exactly once per match.");
            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(playerWon!.Value, result.Value.IsVictory,
                "OnMatchCompleted and OnMatchEnded must agree - they describe the same event.");
            Assert.AreEqual(controller.TickCount, result.Value.TicksTaken);
            Assert.AreEqual(controller.OutcomeReason, result.Value.OutcomeReason);
            Assert.AreEqual(100, result.Value.PlayerMaxHealth);
            Assert.AreEqual(10000, result.Value.EnemyMaxHealth);
            Assert.AreEqual(controller.PlayerState.AvatarHealth, result.Value.PlayerHealthRemaining);
            Assert.AreEqual(controller.EnemyState.AvatarHealth, result.Value.EnemyHealthRemaining);
        }

        [Test]
        public void OnMatchCompleted_OutcomeReasonIsEmpty_ForAStraightKnockout()
        {
            // Deliberately the opposite pool shape to the tick-cap test above - a 1 HP enemy
            // Avatar all but guarantees a knockout well before the cap, which is the path where
            // OutcomeReason is intentionally left blank (see MatchResult.OutcomeReason's own doc
            // comment - nobody needs an explanation for "the Avatar hit 0").
            //
            // The enemy is deliberately left with an empty board rather than run through
            // SimpleAIOpponent: PlayerBattleState shuffles with an unseeded System.Random, so an
            // AI-built formation is not guaranteed to leave any lane clearable within the tick
            // cap - only a lane with zero living defenders is guaranteed to let ANY positive
            // Attack overflow straight to the Avatar (see LaneBattleResolver.ApplyDamageToLane's
            // "no living defenders" rule, already proven by
            // LaneBattleResolver_AttackingAnUndefendedLane_DamagesTheAvatar). Every real card has
            // positive Attack (CardDatabase_EveryCardHasPositiveStats) and
            // AvatarDamageMultiplier is always >= 1, so a permanently undefended 1 HP enemy is a
            // genuine, deterministic tick-1 knockout under ordinary combat rules regardless of
            // which card the player's shuffle happens to draw - no invulnerability, no fake
            // victory, no changed damage rules.
            CardDatabase db = LoadDatabase();
            BattleController controller = CreateController();
            List<Card> deck = db.AllCards.Take(15).ToList();
            controller.StartMatch(deck.ToList(), deck.ToList(),
                new BattleController.MatchEconomy(60, 60, 10000),
                new BattleController.MatchEconomy(60, 60, 1));
            controller.DealFormationHand(controller.PlayerState);
            foreach (Card card in controller.PlayerState.Hand.ToList())
            {
                foreach (Lane lane in new[] { Lane.Front, Lane.Middle, Lane.Back })
                {
                    if (controller.PlayerState.Lanes[lane].HasRoomFor(card)
                        && card.ResourceCost <= controller.PlayerState.Resource
                        && controller.TryPlayCard(controller.PlayerState, card, lane))
                    {
                        break;
                    }
                }
            }
            Assert.IsTrue(controller.ConfirmFormation());

            MatchResult? result = null;
            controller.OnMatchCompleted += r => result = r;

            for (int i = 0; i < BattleController.MaxCombatTicks && controller.Phase == BattlePhase.Combat; i++)
            {
                controller.AdvanceCombatTick();
            }

            Assert.AreEqual(BattlePhase.Resolved, controller.Phase);
            Assert.IsTrue(result.HasValue, "The match must resolve within the tick cap given a 1 HP enemy Avatar.");
            Assert.Less(controller.TickCount, BattleController.MaxCombatTicks,
                "Sanity check: this should be a knockout, not a cap decision - otherwise this test " +
                "is not actually exercising the path it claims to.");
            Assert.IsTrue(result.Value.IsVictory);
            Assert.IsEmpty(result.Value.OutcomeReason,
                "A knockout must leave OutcomeReason blank, matching BattleController.OutcomeReason's " +
                "own existing behaviour - only a tick-cap decision explains itself.");
        }

        [Test]
        public void Reinforcements_OnlyDeployableInsideTheWindow()
        {
            BattleController controller = StartFormationMatch();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, controller.PlayerState.Hand.First(), Lane.Front));
            Assert.IsTrue(controller.ConfirmFormation());

            Card reserve = controller.PlayerState.Hand.FirstOrDefault();
            if (reserve == null) Assert.Ignore("No cards left in hand to reinforce with.");

            Assert.IsFalse(controller.IsReinforcementWindowOpen,
                "The window should be shut immediately after formation locks.");
            Assert.IsFalse(controller.TryDeployReinforcement(reserve, Lane.Back),
                "Reinforcing outside the window must be rejected - always-open reinforcement is " +
                "what lets a player chump-block forever, which the tick cap exists to prevent.");

            // Advance to the first reinforcement tick.
            while (controller.Phase == BattlePhase.Combat && !controller.IsReinforcementWindowOpen)
            {
                controller.AdvanceCombatTick();
            }
            if (controller.Phase != BattlePhase.Combat) Assert.Ignore("Match ended before the window opened.");

            Card stillHeld = controller.PlayerState.Hand.FirstOrDefault(c => c.ResourceCost <= controller.PlayerState.Resource);
            if (stillHeld == null) Assert.Ignore("No affordable card left to reinforce with.");

            int deployedBefore = controller.PlayerState.Lanes.Values.Sum(l => l.Cards.Count);
            Assert.IsTrue(controller.TryDeployReinforcement(stillHeld, Lane.Back),
                "Reinforcing inside the window should succeed.");
            Assert.Greater(controller.PlayerState.Lanes.Values.Sum(l => l.Cards.Count), deployedBefore,
                "A successful reinforcement should actually put a unit on the board.");
        }

        // ---------- Reinforcement + formation-synergy interaction ----------
        //
        // TryDeployReinforcement recalculates FormationSynergy.Calculate on every reinforcement
        // ("a reinforcement can complete a pair or trio") but buffs only the newly deployed unit -
        // the squad already standing had its bonus applied once at ConfirmFormation, and
        // re-buffing it every window would stack the same bonus repeatedly (see
        // BattleController.TryDeployReinforcement's own comment). Neither
        // Reinforcements_OnlyDeployableInsideTheWindow (window gating only) nor
        // FormationSynergy_AppliedToDeployedUnitsWhenFormationLocks (lock-in only) exercised this
        // interaction before the tests below.

        /// <summary>
        /// Locks a formation with exactly two one-slot Knights in Back (a pair - AegisGuard x2,
        /// which already earns a nonzero bonus) so a reinforcement completing the trio is a real,
        /// measurable change. Back grants no lane stat bonus, so any Attack/Health change on
        /// these units is synergy and nothing else. Also gives the enemy one deployed card so
        /// tests that check the enemy is untouched have real state to prove that against.
        /// </summary>
        private BattleController BuildKnightPairLockedForReinforcement(out Card thirdKnight, out Card otherReinforcement)
        {
            CardDatabase db = LoadDatabase();
            List<Card> knights = db.AllCards
                .Where(c => c.Class == CardClass.Knight && c.SlotWeight == 1)
                .Take(3)
                .ToList();
            if (knights.Count < 3) Assert.Ignore("Card pool has fewer than 3 one-slot Knights to test with.");

            Card other = db.AllCards.FirstOrDefault(c => c.Class != CardClass.Knight && c.SlotWeight == 1);
            if (other == null) Assert.Ignore("Card pool has no non-Knight one-slot card to reinforce with.");

            var playerDeck = new List<Card> { knights[0], knights[1], knights[2], other };
            List<Card> enemyDeck = db.AllCards.Take(15).ToList();

            BattleController controller = CreateController();
            var economy = new BattleController.MatchEconomy(60, 60, 2000);
            controller.StartMatch(playerDeck, enemyDeck, economy, economy);
            controller.DealFormationHand(controller.PlayerState);
            controller.DealFormationHand(controller.EnemyState);

            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, knights[0], Lane.Back));
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, knights[1], Lane.Back));

            Card enemyCard = controller.EnemyState.Hand.FirstOrDefault(c => c.ResourceCost <= controller.EnemyState.Resource);
            if (enemyCard != null)
            {
                controller.TryPlayCard(controller.EnemyState, enemyCard, Lane.Front);
            }

            Assert.IsTrue(controller.ConfirmFormation());
            Assert.IsTrue(controller.PlayerSynergy.HasAny,
                "Setup failure: two matching-tag Knights should already have earned a pair bonus at lock-in.");

            thirdKnight = knights[2];
            otherReinforcement = other;
            return controller;
        }

        private static void AdvanceToTick(BattleController controller, int tick)
        {
            while (controller.Phase == BattlePhase.Combat && controller.TickCount < tick)
            {
                controller.AdvanceCombatTick();
            }
        }

        [Test]
        public void Reinforcement_CompletingASynergySet_RecalculatesSynergyAndBuffsOnlyTheNewUnit()
        {
            BattleController controller = BuildKnightPairLockedForReinforcement(out Card thirdKnight, out _);

            SynergyBonus pairBonus = controller.PlayerSynergy;
            List<BattleCardInstance> standingUnits = controller.PlayerState.Lanes[Lane.Back].Cards.ToList();
            Assert.AreEqual(2, standingUnits.Count, "Setup: exactly two Knights should be standing before reinforcement.");
            List<int> standingAttackBefore = standingUnits.Select(c => c.Attack).ToList();
            List<int> standingHealthBefore = standingUnits.Select(c => c.MaxHealth).ToList();

            AdvanceToTick(controller, BattleController.ReinforcementTicks[0]);
            if (controller.Phase != BattlePhase.Combat) Assert.Ignore("Match ended before the first reinforcement window opened.");
            Assert.IsTrue(controller.IsReinforcementWindowOpen, "Setup: the first reinforcement window should be open at this tick.");

            Assert.IsTrue(controller.TryDeployReinforcement(thirdKnight, Lane.Back),
                "A valid reinforcement completing a synergy trio should be accepted.");

            // Point 1: deploying inside the window recalculates synergy to a strictly stronger
            // bonus than the pair it replaces - not asserted as a fixed magnitude, since the pair
            // and trio bonuses are tunable balance numbers, only that completing the trio must
            // produce a real, measurable increase.
            SynergyBonus trioBonus = controller.PlayerSynergy;
            Assert.Greater(trioBonus.AttackBonus, pairBonus.AttackBonus,
                "Completing a trio should recalculate synergy to a strictly stronger Attack bonus than the pair it replaces.");
            Assert.Greater(trioBonus.HealthBonus, pairBonus.HealthBonus,
                "A trio grants a Health bonus a pair does not - recalculation must pick that up.");

            // Point 3: units already standing before the reinforcement must not be buffed again.
            for (int i = 0; i < standingUnits.Count; i++)
            {
                Assert.AreEqual(standingAttackBefore[i], standingUnits[i].Attack,
                    "A unit already standing before the reinforcement must not be buffed again by the recalculation.");
                Assert.AreEqual(standingHealthBefore[i], standingUnits[i].MaxHealth,
                    "A unit already standing before the reinforcement must not be buffed again by the recalculation.");
            }

            // Point 2: the newly deployed unit receives exactly the current synergy bonus on top
            // of its own base stats (Back grants no lane bonus) - derived from the actual
            // production formula (base + current synergy), not a hardcoded expected number.
            BattleCardInstance deployed = controller.PlayerState.Lanes[Lane.Back].Cards[^1];
            Assert.AreEqual(thirdKnight.Attack + trioBonus.AttackBonus, deployed.Attack,
                "The newly deployed unit's Attack should be its base Attack plus exactly the current synergy bonus.");
            Assert.AreEqual(thirdKnight.Health + trioBonus.HealthBonus, deployed.MaxHealth,
                "The newly deployed unit's Health should be its base Health plus exactly the current synergy bonus.");
        }

        [Test]
        public void Reinforcement_OutsideTheWindow_DoesNotChangeSynergyOrUnitState()
        {
            BattleController controller = BuildKnightPairLockedForReinforcement(out Card thirdKnight, out _);

            Assert.IsFalse(controller.IsReinforcementWindowOpen,
                "Setup: the window should be shut immediately after formation locks.");

            SynergyBonus synergyBefore = controller.PlayerSynergy;
            List<BattleCardInstance> standingUnits = controller.PlayerState.Lanes[Lane.Back].Cards.ToList();
            List<int> attackBefore = standingUnits.Select(c => c.Attack).ToList();
            List<int> healthBefore = standingUnits.Select(c => c.MaxHealth).ToList();
            int handCountBefore = controller.PlayerState.Hand.Count;
            int laneCardsBefore = controller.PlayerState.Lanes.Values.Sum(l => l.Cards.Count);

            Assert.IsFalse(controller.TryDeployReinforcement(thirdKnight, Lane.Back),
                "A reinforcement attempted outside the window must be rejected.");

            Assert.AreEqual(synergyBefore.AttackBonus, controller.PlayerSynergy.AttackBonus,
                "A rejected reinforcement must not change the recorded synergy bonus.");
            Assert.AreEqual(synergyBefore.HealthBonus, controller.PlayerSynergy.HealthBonus,
                "A rejected reinforcement must not change the recorded synergy bonus.");
            for (int i = 0; i < standingUnits.Count; i++)
            {
                Assert.AreEqual(attackBefore[i], standingUnits[i].Attack, "A rejected reinforcement must not change unit stats.");
                Assert.AreEqual(healthBefore[i], standingUnits[i].MaxHealth, "A rejected reinforcement must not change unit stats.");
            }
            Assert.AreEqual(handCountBefore, controller.PlayerState.Hand.Count,
                "A rejected reinforcement must not consume the card from hand.");
            Assert.AreEqual(laneCardsBefore, controller.PlayerState.Lanes.Values.Sum(l => l.Cards.Count),
                "A rejected reinforcement must not place a unit on the board.");
        }

        [Test]
        public void Reinforcement_OnPlayerSide_DoesNotAlterEnemySynergyOrState()
        {
            BattleController controller = BuildKnightPairLockedForReinforcement(out Card thirdKnight, out _);
            AdvanceToTick(controller, BattleController.ReinforcementTicks[0]);
            if (controller.Phase != BattlePhase.Combat) Assert.Ignore("Match ended before the first reinforcement window opened.");
            Assert.IsTrue(controller.IsReinforcementWindowOpen, "Setup: the first reinforcement window should be open at this tick.");

            SynergyBonus enemySynergyBefore = controller.EnemySynergy;
            List<BattleCardInstance> enemyUnits = controller.EnemyState.Lanes.Values.SelectMany(l => l.Cards).ToList();
            List<int> enemyAttackBefore = enemyUnits.Select(c => c.Attack).ToList();
            List<int> enemyHealthBefore = enemyUnits.Select(c => c.MaxHealth).ToList();
            int enemyHandBefore = controller.EnemyState.Hand.Count;
            int enemyResourceBefore = controller.EnemyState.Resource;
            int enemyAvatarHealthBefore = controller.EnemyState.AvatarHealth;
            int enemyLaneCardsBefore = controller.EnemyState.Lanes.Values.Sum(l => l.Cards.Count);

            Assert.IsTrue(controller.TryDeployReinforcement(thirdKnight, Lane.Back),
                "Setup: the player-side reinforcement should succeed inside the window.");

            Assert.AreEqual(enemySynergyBefore.AttackBonus, controller.EnemySynergy.AttackBonus,
                "A player-side reinforcement must not touch the enemy's synergy.");
            Assert.AreEqual(enemySynergyBefore.HealthBonus, controller.EnemySynergy.HealthBonus,
                "A player-side reinforcement must not touch the enemy's synergy.");
            for (int i = 0; i < enemyUnits.Count; i++)
            {
                Assert.AreEqual(enemyAttackBefore[i], enemyUnits[i].Attack, "A player-side reinforcement must not change any enemy unit's stats.");
                Assert.AreEqual(enemyHealthBefore[i], enemyUnits[i].MaxHealth, "A player-side reinforcement must not change any enemy unit's stats.");
            }
            Assert.AreEqual(enemyHandBefore, controller.EnemyState.Hand.Count,
                "A player-side reinforcement must not change the enemy's hand.");
            Assert.AreEqual(enemyResourceBefore, controller.EnemyState.Resource,
                "A player-side reinforcement must not spend the enemy's Resource.");
            Assert.AreEqual(enemyAvatarHealthBefore, controller.EnemyState.AvatarHealth,
                "A player-side reinforcement must not change the enemy's Avatar Health.");
            Assert.AreEqual(enemyLaneCardsBefore, controller.EnemyState.Lanes.Values.Sum(l => l.Cards.Count),
                "A player-side reinforcement must not place any unit on the enemy's board.");
        }

        [Test]
        public void Reinforcement_ASecondReinforcement_DoesNotStackAnAlreadyAppliedSynergyBonus()
        {
            BattleController controller = BuildKnightPairLockedForReinforcement(out Card thirdKnight, out Card otherReinforcement);

            AdvanceToTick(controller, BattleController.ReinforcementTicks[0]);
            if (controller.Phase != BattlePhase.Combat) Assert.Ignore("Match ended before the first reinforcement window opened.");
            Assert.IsTrue(controller.TryDeployReinforcement(thirdKnight, Lane.Back),
                "Setup: completing the trio on the first reinforcement should succeed.");

            SynergyBonus trioBonus = controller.PlayerSynergy;
            List<BattleCardInstance> knightUnits = controller.PlayerState.Lanes[Lane.Back].Cards.ToList();
            Assert.AreEqual(3, knightUnits.Count, "Setup: three Knights should now be standing after the first reinforcement.");
            List<int> attackAfterFirstReinforcement = knightUnits.Select(c => c.Attack).ToList();
            List<int> healthAfterFirstReinforcement = knightUnits.Select(c => c.MaxHealth).ToList();

            AdvanceToTick(controller, BattleController.ReinforcementTicks[1]);
            if (controller.Phase != BattlePhase.Combat) Assert.Ignore("Match ended before the second reinforcement window opened.");

            // The second reinforcement is deliberately not a Knight, so it does not change the
            // AegisGuard trio's tag count - synergy must recalculate to the SAME trio-level
            // bonus, not a stronger one, proving repeated reinforcement cannot stack it further.
            Assert.IsTrue(controller.TryDeployReinforcement(otherReinforcement, Lane.Front),
                "Setup: a second, unrelated reinforcement should still be accepted inside its own window.");

            Assert.AreEqual(trioBonus.AttackBonus, controller.PlayerSynergy.AttackBonus,
                "A reinforcement that does not change any tag's count must not change the synergy bonus.");
            Assert.AreEqual(trioBonus.HealthBonus, controller.PlayerSynergy.HealthBonus,
                "A reinforcement that does not change any tag's count must not change the synergy bonus.");

            for (int i = 0; i < knightUnits.Count; i++)
            {
                Assert.AreEqual(attackAfterFirstReinforcement[i], knightUnits[i].Attack,
                    "A later reinforcement must not stack a synergy bonus onto units that already received it.");
                Assert.AreEqual(healthAfterFirstReinforcement[i], knightUnits[i].MaxHealth,
                    "A later reinforcement must not stack a synergy bonus onto units that already received it.");
            }

            // The second reinforcement should still receive its own (lane + synergy) treatment
            // once - proving the rule still fires on later reinforcements rather than being
            // silently disabled, without hardcoding the private lane-bonus magnitude.
            BattleCardInstance secondDeployed = controller.PlayerState.Lanes[Lane.Front].Cards[^1];
            Assert.Greater(secondDeployed.Attack, otherReinforcement.Attack,
                "The second reinforcement should still receive a real Attack bonus on top of its base stats.");
        }

        // ---------- Reinforcement resource/card-state atomicity ----------
        //
        // TryDeployReinforcement's four guard clauses (window, hand membership, affordability,
        // lane room) all run before any mutation - Reinforcement_OutsideTheWindow_... already
        // proves this for the window-closed rejection specifically. These two tests cover the
        // other two rejection reasons a reinforcement can fail for *inside* an open window:
        // insufficient Resource and a full target lane. Neither was previously exercised.

        [Test]
        public void Reinforcement_InsufficientResourceInsideTheWindow_DoesNotChangeStateOrConsumeResource()
        {
            BattleController controller = BuildKnightPairLockedForReinforcement(out Card thirdKnight, out _);
            AdvanceToTick(controller, BattleController.ReinforcementTicks[0]);
            if (controller.Phase != BattlePhase.Combat) Assert.Ignore("Match ended before the first reinforcement window opened.");
            Assert.IsTrue(controller.IsReinforcementWindowOpen, "Setup: the first reinforcement window should be open at this tick.");

            // Drain Resource below the card's cost - the window is open and the card is in hand,
            // so affordability is the only thing this rejection can be attributed to.
            Assert.Greater(thirdKnight.ResourceCost, 0, "Setup invariant: every real card has a positive cost.");
            controller.PlayerState.Resource = thirdKnight.ResourceCost - 1;

            SynergyBonus synergyBefore = controller.PlayerSynergy;
            List<BattleCardInstance> standingUnits = controller.PlayerState.Lanes[Lane.Back].Cards.ToList();
            List<int> attackBefore = standingUnits.Select(c => c.Attack).ToList();
            List<int> healthBefore = standingUnits.Select(c => c.MaxHealth).ToList();
            int resourceBefore = controller.PlayerState.Resource;
            int handCountBefore = controller.PlayerState.Hand.Count;
            int laneCardsBefore = controller.PlayerState.Lanes.Values.Sum(l => l.Cards.Count);

            Assert.IsFalse(controller.TryDeployReinforcement(thirdKnight, Lane.Back),
                "A reinforcement the player cannot afford must be rejected even inside an open window.");

            Assert.AreEqual(resourceBefore, controller.PlayerState.Resource, "A rejected reinforcement must not spend Resource.");
            Assert.AreEqual(handCountBefore, controller.PlayerState.Hand.Count, "A rejected reinforcement must not consume the card from hand.");
            Assert.AreEqual(laneCardsBefore, controller.PlayerState.Lanes.Values.Sum(l => l.Cards.Count),
                "A rejected reinforcement must not place a unit on the board.");
            Assert.AreEqual(synergyBefore.AttackBonus, controller.PlayerSynergy.AttackBonus,
                "A rejected reinforcement must not change the recorded synergy bonus.");
            Assert.AreEqual(synergyBefore.HealthBonus, controller.PlayerSynergy.HealthBonus,
                "A rejected reinforcement must not change the recorded synergy bonus.");
            for (int i = 0; i < standingUnits.Count; i++)
            {
                Assert.AreEqual(attackBefore[i], standingUnits[i].Attack, "A rejected reinforcement must not change unit stats.");
                Assert.AreEqual(healthBefore[i], standingUnits[i].MaxHealth, "A rejected reinforcement must not change unit stats.");
            }
        }

        private BattleController BuildFullFrontLaneLockedForReinforcement(out Card overflowCard)
        {
            CardDatabase db = LoadDatabase();
            List<Card> singleSlot = db.AllCards.Where(c => c.SlotWeight == 1).Take(4).ToList();
            if (singleSlot.Count < 4) Assert.Ignore("Card pool has fewer than 4 one-slot cards to test with.");

            BattleController controller = CreateController();
            var economy = new BattleController.MatchEconomy(60, 60, 2000);
            controller.StartMatch(singleSlot, singleSlot, economy, economy);
            controller.DealFormationHand(controller.PlayerState);

            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, singleSlot[i], Lane.Front));
            }
            Assert.AreEqual(0, controller.PlayerState.Lanes[Lane.Front].FreeSlots,
                "Setup: three one-slot cards should fill the Front lane.");

            Assert.IsTrue(controller.ConfirmFormation());

            overflowCard = singleSlot[3];
            return controller;
        }

        [Test]
        public void Reinforcement_FullLaneInsideTheWindow_DoesNotChangeStateOrConsumeCard()
        {
            BattleController controller = BuildFullFrontLaneLockedForReinforcement(out Card overflowCard);
            AdvanceToTick(controller, BattleController.ReinforcementTicks[0]);
            if (controller.Phase != BattlePhase.Combat) Assert.Ignore("Match ended before the first reinforcement window opened.");
            Assert.IsTrue(controller.IsReinforcementWindowOpen, "Setup: the first reinforcement window should be open at this tick.");
            Assert.AreEqual(0, controller.PlayerState.Lanes[Lane.Front].FreeSlots,
                "Setup: the Front lane must still be full going into the reinforcement attempt.");
            Assert.LessOrEqual(overflowCard.ResourceCost, controller.PlayerState.Resource,
                "Setup invariant: the overflow card must be affordable, so the rejection is attributable to the full lane specifically.");

            SynergyBonus synergyBefore = controller.PlayerSynergy;
            List<BattleCardInstance> standingUnits = controller.PlayerState.Lanes[Lane.Front].Cards.ToList();
            List<int> attackBefore = standingUnits.Select(c => c.Attack).ToList();
            List<int> healthBefore = standingUnits.Select(c => c.MaxHealth).ToList();
            int resourceBefore = controller.PlayerState.Resource;
            int handCountBefore = controller.PlayerState.Hand.Count;
            int laneCardsBefore = controller.PlayerState.Lanes.Values.Sum(l => l.Cards.Count);

            Assert.IsFalse(controller.TryDeployReinforcement(overflowCard, Lane.Front),
                "A reinforcement into an already-full lane must be rejected even inside an open window.");

            Assert.AreEqual(resourceBefore, controller.PlayerState.Resource, "A rejected reinforcement must not spend Resource.");
            Assert.AreEqual(handCountBefore, controller.PlayerState.Hand.Count, "A rejected reinforcement must not consume the card from hand.");
            Assert.AreEqual(laneCardsBefore, controller.PlayerState.Lanes.Values.Sum(l => l.Cards.Count),
                "A rejected reinforcement must not place a unit on the board.");
            Assert.AreEqual(synergyBefore.AttackBonus, controller.PlayerSynergy.AttackBonus,
                "A rejected reinforcement must not change the recorded synergy bonus.");
            Assert.AreEqual(synergyBefore.HealthBonus, controller.PlayerSynergy.HealthBonus,
                "A rejected reinforcement must not change the recorded synergy bonus.");
            for (int i = 0; i < standingUnits.Count; i++)
            {
                Assert.AreEqual(attackBefore[i], standingUnits[i].Attack, "A rejected reinforcement must not change unit stats.");
                Assert.AreEqual(healthBefore[i], standingUnits[i].MaxHealth, "A rejected reinforcement must not change unit stats.");
            }
        }

        [Test]
        public void Spells_LaneDamageReachesTheAvatarWhenTheLaneIsUndefended()
        {
            BattleController controller = StartFormationMatch();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, controller.PlayerState.Hand.First(), Lane.Front));
            Assert.IsTrue(controller.ConfirmFormation());

            int damageIndex = controller.Spellbook.FindIndex(s => s.Effect == SpellEffect.LaneDamage);
            Assert.GreaterOrEqual(damageIndex, 0);

            while (controller.Energy < controller.Spellbook[damageIndex].EnergyCost
                   && controller.Phase == BattlePhase.Combat)
            {
                controller.AdvanceCombatTick();
            }
            if (controller.Phase != BattlePhase.Combat) Assert.Ignore("Match resolved before enough Energy accrued.");

            // Empty the target lane so the cast has nothing to hit.
            controller.EnemyState.Lanes[Lane.Back].Cards.Clear();

            int healthBefore = controller.EnemyState.AvatarHealth;
            Assert.IsTrue(controller.TryCastSpell(damageIndex, Lane.Back, out int dealt));

            // Previously this silently did nothing, so by the late game - when both boards have
            // wiped out - every damage spell was a dead button.
            Assert.Greater(dealt, 0, "Damage cast into an undefended lane should reach the Avatar.");
            Assert.Less(controller.EnemyState.AvatarHealth, healthBefore,
                "The enemy Avatar's Health should actually drop.");
        }

        [Test]
        public void Spells_LaneDamageAppliedToADefendedLaneDamagesUnitsNotTheAvatar()
        {
            // The other half of AvatarSpell.Cast's LaneDamage branch: the existing test only
            // exercises the undefended (no living units) path. This is the defended path, where
            // damage must apply to the living units and never bypass to the Avatar.
            CardDatabase db = LoadDatabase();
            Card tankyDefender = db.AllCards.OrderByDescending(c => c.Health).First();

            BattleController controller = CreateController();
            var economy = new BattleController.MatchEconomy(60, 60, 2000);
            controller.StartMatch(new List<Card> { tankyDefender }, new List<Card> { tankyDefender }, economy, economy);
            controller.DealFormationHand(controller.PlayerState);
            controller.DealFormationHand(controller.EnemyState);
            // Player deploys to Front only to satisfy ConfirmFormation; the enemy's Back-lane
            // defender - the actual spell target - never clashes with anything (nothing is
            // deployed in either side's Back but the enemy's own card), so it is guaranteed alive
            // and undamaged when the spell lands.
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, tankyDefender, Lane.Front));
            Assert.IsTrue(controller.TryPlayCard(controller.EnemyState, tankyDefender, Lane.Back));
            Assert.IsTrue(controller.ConfirmFormation());

            int damageIndex = controller.Spellbook.FindIndex(s => s.Effect == SpellEffect.LaneDamage);
            Assert.GreaterOrEqual(damageIndex, 0, "The default spellbook should contain a LaneDamage spell.");

            while (controller.Energy < controller.Spellbook[damageIndex].EnergyCost && controller.Phase == BattlePhase.Combat)
            {
                controller.AdvanceCombatTick();
            }
            if (controller.Phase != BattlePhase.Combat) Assert.Ignore("Match resolved before enough Energy accrued.");

            BattleCardInstance target = controller.EnemyState.Lanes[Lane.Back].Cards[0];
            int targetHealthBefore = target.CurrentHealth;
            int enemyAvatarHealthBefore = controller.EnemyState.AvatarHealth;

            Assert.IsTrue(controller.TryCastSpell(damageIndex, Lane.Back, out int dealt));

            Assert.AreEqual(0, dealt, "Damage into a defended lane must not reach the Avatar - the spell should report zero.");
            Assert.Less(target.CurrentHealth, targetHealthBefore, "The living defender in the target lane should take damage.");
            Assert.AreEqual(enemyAvatarHealthBefore, controller.EnemyState.AvatarHealth,
                "A defended-lane cast must not change the enemy Avatar's Health.");
        }

        // ---------- V4 design changes: overtime, Back-lane energy, elements, slot weighting ----------

        [Test]
        public void Overtime_DamageMultiplierEscalatesInTheBackHalfOfAMatch()
        {
            int early = LaneBattleResolver.AvatarDamageMultiplierForTick(1);
            int mid = LaneBattleResolver.AvatarDamageMultiplierForTick(LaneBattleResolver.OvertimeStartTick);
            int late = LaneBattleResolver.AvatarDamageMultiplierForTick(LaneBattleResolver.LateOvertimeStartTick);

            Assert.AreEqual(LaneBattleResolver.AvatarDamageMultiplier, early,
                "Ticks before overtime should use the base multiplier.");
            Assert.Greater(mid, early, "Overtime should raise the multiplier.");
            Assert.Greater(late, mid, "Late overtime should raise it further.");

            // The point of escalation is that the cap becomes a deadline rather than a result -
            // if the last ticks hit no harder than the first, turtling to the tie-breaker stays
            // the optimal line.
            Assert.AreEqual(late, LaneBattleResolver.AvatarDamageMultiplierForTick(BattleController.MaxCombatTicks),
                "The final tick should be at the highest escalation band.");
        }

        [Test]
        public void Overtime_SameBoardDealsMoreAvatarDamageLaterInTheMatch()
        {
            static (PlayerBattleState attacker, PlayerBattleState defender) BuildUndefendedClash()
            {
                var attackerData = new CardData { id = "ot_attacker", name = "OT Attacker", art_file = "x.png", element = "Andras", type = "warrior", rarity = 7 };
                Card attackerCard = Card.FromData(attackerData);
                var attacker = new PlayerBattleState(new List<Card> { attackerCard }, 20, 20, 5000);
                var defender = new PlayerBattleState(new List<Card> { attackerCard }, 20, 20, 5000);
                attacker.Lanes[Lane.Front].Cards.Add(new BattleCardInstance(attackerCard, true, 0, 0));
                return (attacker, defender);
            }

            var (earlyAttacker, earlyDefender) = BuildUndefendedClash();
            TurnResolutionResult earlyResult = LaneBattleResolver.ResolveTurn(earlyDefender, earlyAttacker, tickNumber: 1);

            var (lateAttacker, lateDefender) = BuildUndefendedClash();
            TurnResolutionResult lateResult = LaneBattleResolver.ResolveTurn(lateDefender, lateAttacker,
                tickNumber: LaneBattleResolver.LateOvertimeStartTick);

            Assert.Greater(earlyResult.DamageDealtToSideA, 0, "Setup: the undefended side should be taking damage.");
            Assert.Greater(lateResult.DamageDealtToSideA, earlyResult.DamageDealtToSideA,
                "An identical board must hit harder in late overtime than on tick 1.");
        }

        [Test]
        public void BackLane_GeneratesExtraEnergyPerLivingCard()
        {
            BattleController controller = StartFormationMatch();

            // Same card count either way, so the only variable is which lane it sits in.
            Card card = controller.PlayerState.Hand.First();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, card, Lane.Back));
            Assert.IsTrue(controller.ConfirmFormation());

            int expectedBonus = BattleController.BackLaneEnergyPerCard;
            Assert.AreEqual(expectedBonus, BattleController.BackLaneEnergy(controller.PlayerState),
                "One living Back-lane card should contribute exactly one card's worth of Energy.");

            controller.AdvanceCombatTick();
            Assert.AreEqual(controller.EnergyPerTick + expectedBonus, controller.Energy,
                "A combat tick should grant base regen plus the Back lane's contribution - the Back " +
                "lane granted nothing at all before this, which is why nobody had a reason to use it.");
        }

        [Test]
        public void Elements_LaneAdvantageAppliesAtLaneScaleNotPerCard()
        {
            // Ktini beats Andras. Both lanes get identical stats so the only difference is element.
            var ktiniData = new CardData { id = "el_ktini", name = "Ktini Unit", art_file = "x.png", element = "Ktini", type = "warrior", rarity = 5 };
            var andrasData = new CardData { id = "el_andras", name = "Andras Unit", art_file = "x.png", element = "Andras", type = "warrior", rarity = 5 };
            Card ktini = Card.FromData(ktiniData);
            Card andras = Card.FromData(andrasData);

            Assert.IsTrue(LaneBattleResolver.Counters(CardElement.Ktini, CardElement.Andras),
                "Setup: the cycle should have Ktini countering Andras.");
            Assert.IsFalse(LaneBattleResolver.Counters(CardElement.Andras, CardElement.Ktini),
                "The cycle must not run both ways, or the advantage cancels out.");

            // Measured as *overflow*, not as damage dealt to the defender. Damage to a unit is
            // capped by that unit's remaining Health, so a bonus that kills it a bit harder is
            // invisible there - the extra only shows up in what spills past the lane. Both
            // defenders are normalised to exactly 1 Health so the two runs differ solely by
            // element, not by the hash-derived Health variance within a rarity.
            static int OverflowAgainst(Card attackerCard, Card defenderCard)
            {
                var attacking = new LaneState(Lane.Front);
                var defending = new LaneState(Lane.Front);
                attacking.Cards.Add(new BattleCardInstance(attackerCard, true, 0, 0));

                var defender = new BattleCardInstance(defenderCard, false, 0, 0);
                defender.ApplyDamage(defender.CurrentHealth - 1); // exactly 1 Health left
                defending.Cards.Add(defender);

                return LaneBattleResolver.ResolveLaneClash(defending, attacking).OverflowToA;
            }

            var attackingLane = new LaneState(Lane.Front);
            var defendingLane = new LaneState(Lane.Front);
            attackingLane.Cards.Add(new BattleCardInstance(ktini, true, 0, 0));
            defendingLane.Cards.Add(new BattleCardInstance(andras, false, 0, 0));
            Assert.AreEqual(CardElement.Ktini, LaneBattleResolver.DominantElement(attackingLane));
            Assert.AreEqual(CardElement.Andras, LaneBattleResolver.DominantElement(defendingLane));

            // Control: Ktini attacking Ktini has no advantage (the cycle is not reflexive).
            var neutralData = new CardData { id = "el_ktini_def", name = "Ktini Wall", art_file = "x.png", element = "Ktini", type = "warrior", rarity = 5 };
            int neutralOverflow = OverflowAgainst(ktini, Card.FromData(neutralData));
            int counterOverflow = OverflowAgainst(ktini, andras);

            Assert.Greater(counterOverflow, neutralOverflow,
                "A lane holding the elemental advantage should push more damage through than the " +
                "same lane without it. The bonus is applied once to the lane total precisely so it " +
                "survives rounding - 25% of a single 3-Attack card rounds to +1, which is the flat " +
                "bonus it was meant to replace.");
        }

        [Test]
        public void Elements_MixedLaneHasNoDominantElementAndNoAdvantage()
        {
            var ktiniData = new CardData { id = "mix_ktini", name = "K", art_file = "x.png", element = "Ktini", type = "warrior", rarity = 4 };
            var andrasData = new CardData { id = "mix_andras", name = "A", art_file = "x.png", element = "Andras", type = "warrior", rarity = 4 };

            var mixedLane = new LaneState(Lane.Front);
            mixedLane.Cards.Add(new BattleCardInstance(Card.FromData(ktiniData), true, 0, 0));
            mixedLane.Cards.Add(new BattleCardInstance(Card.FromData(andrasData), true, 0, 0));

            Assert.IsNull(LaneBattleResolver.DominantElement(mixedLane),
                "An evenly split lane has no element identity, so it neither gains nor suffers an " +
                "advantage - otherwise a mixed lane would win ties by accident.");
        }

        [Test]
        public void SlotWeighting_HighRarityTakesTwoSlots()
        {
            var lowData = new CardData { id = "sw_low", name = "Low", art_file = "x.png", element = "Andras", type = "warrior", rarity = 4 };
            var highData = new CardData { id = "sw_high", name = "High", art_file = "x.png", element = "Andras", type = "warrior", rarity = 7 };
            Card low = Card.FromData(lowData);
            Card high = Card.FromData(highData);

            Assert.AreEqual(1, low.SlotWeight, "Rarity 1-4 should occupy a single slot.");
            Assert.AreEqual(2, high.SlotWeight, "Rarity 5-7 should occupy two slots.");

            var lane = new LaneState(Lane.Front);
            Assert.AreEqual(LaneState.MaxSlots, lane.FreeSlots);
            Assert.IsTrue(lane.HasRoomFor(high));

            lane.Cards.Add(new BattleCardInstance(high, true, 0, 0));
            Assert.AreEqual(2, lane.SlotsUsed, "A rarity 7 card should consume two of the three slots.");
            Assert.AreEqual(1, lane.FreeSlots);

            // The whole point: with one slot left, a small card still fits and a big one does not.
            Assert.IsTrue(lane.HasRoomFor(low), "A one-slot card should still fit in the last slot.");
            Assert.IsFalse(lane.HasRoomFor(high),
                "A two-slot card must not fit in a single free slot - that is what stops high " +
                "rarity being a strict upgrade on a slot-limited board.");
            Assert.IsTrue(lane.HasOpenSlot, "The lane still has room for something, just not for everything.");
        }

        [Test]
        public void SlotWeighting_TryPlayCardRejectsAnOversizedCard()
        {
            BattleController controller = StartFormationMatch();

            Card big = controller.PlayerState.Hand.FirstOrDefault(c => c.SlotWeight == 2);
            Card small = controller.PlayerState.Hand.FirstOrDefault(c => c.SlotWeight == 1);
            if (big == null || small == null) Assert.Ignore("Hand did not contain both a one-slot and a two-slot card.");

            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, big, Lane.Front),
                "Setup: a two-slot card should fit an empty lane.");
            Assert.AreEqual(1, controller.PlayerState.Lanes[Lane.Front].FreeSlots);

            Card anotherBig = controller.PlayerState.Hand.FirstOrDefault(c => c.SlotWeight == 2);
            if (anotherBig != null)
            {
                Assert.IsFalse(controller.TryPlayCard(controller.PlayerState, anotherBig, Lane.Front),
                    "A second two-slot card must be rejected when only one slot remains.");
            }

            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, small, Lane.Front),
                "A one-slot card should still fit the remaining slot.");
            Assert.AreEqual(0, controller.PlayerState.Lanes[Lane.Front].FreeSlots);
        }

        [Test]
        public void Draw_IsNeverRewardedMoreThanALoss()
        {
            // A tie-breaker draw must not be worth farming. Two accounts running maximum-defence
            // decks would otherwise reach a mutual 100%-HP draw every time and collect a reward
            // for it at zero risk - the exploit in the V3 proposal's "partial rating to both".
            var afterDraw = new PlayerEmpireData();
            afterDraw.SetLevelsForTesting(avatarLevel: 10, castleLevel: 10, barracksLevel: 10);
            afterDraw.InitializeTCGModifiers();
            int levelBefore = afterDraw.AvatarLevel;
            afterDraw.ApplyMatchResult(won: false); // a draw is reported as a loss
            int drawGain = afterDraw.AvatarLevel - levelBefore;

            var afterWin = new PlayerEmpireData();
            afterWin.SetLevelsForTesting(avatarLevel: 10, castleLevel: 10, barracksLevel: 10);
            afterWin.InitializeTCGModifiers();
            afterWin.ApplyMatchResult(won: true);
            int winGain = afterWin.AvatarLevel - 10;

            Assert.Less(drawGain, winGain,
                "A draw must always be worth strictly less than a win, or stalling to the cap " +
                "becomes a viable way to farm progression.");
        }

        // ---------- Solo AI scaling (SoloAIScalingSystem / SimpleAIOpponent) ----------

        private static PlayerEmpireData EmpireAtAvatarLevel(int avatarLevel)
        {
            var empire = new PlayerEmpireData();
            empire.SetLevelsForTesting(avatarLevel, castleLevel: 15, barracksLevel: 25);
            empire.InitializeTCGModifiers();
            return empire;
        }

        [Test]
        public void SoloAIScaling_OpponentGrowsWithPlayerLevel_InsteadOfStayingPinned()
        {
            var scaling = new SoloAIScalingSystem();

            AIBattleProfile early = scaling.GenerateAIOpponent(EmpireAtAvatarLevel(5));
            AIBattleProfile later = scaling.GenerateAIOpponent(EmpireAtAvatarLevel(30));

            // This is the whole point of the system: the opponent used to be pinned at a fixed
            // level-1 baseline while the player gained +3 Avatar levels per win, so the game got
            // steadily *easier* the longer it was played. If these two are ever equal again, that
            // regression is back.
            Assert.Greater(later.MaxAvatarHealth, early.MaxAvatarHealth,
                "A higher-level player should face an opponent with a bigger HP pool, not the same one.");
            Assert.GreaterOrEqual(later.StartingResourceCap, early.StartingResourceCap,
                "A higher-level player's opponent should not have less resource than a beginner's.");
        }

        [Test]
        public void SoloAIScaling_TierBoundariesMatchTheDocumentedBands()
        {
            var scaling = new SoloAIScalingSystem();

            // Boundary values specifically (not mid-band ones) - an off-by-one in the <= chain
            // would still pass if every assertion sat safely in the middle of its band.
            Assert.AreEqual(AIDifficultyTier.Novice, scaling.DetermineTier(1));
            Assert.AreEqual(AIDifficultyTier.Novice, scaling.DetermineTier(10));
            Assert.AreEqual(AIDifficultyTier.Apprentice, scaling.DetermineTier(11));
            Assert.AreEqual(AIDifficultyTier.Apprentice, scaling.DetermineTier(25));
            Assert.AreEqual(AIDifficultyTier.Veteran, scaling.DetermineTier(26));
            Assert.AreEqual(AIDifficultyTier.Veteran, scaling.DetermineTier(50));
            Assert.AreEqual(AIDifficultyTier.Master, scaling.DetermineTier(51));
            Assert.AreEqual(AIDifficultyTier.Master, scaling.DetermineTier(80));
            Assert.AreEqual(AIDifficultyTier.Titan, scaling.DetermineTier(81));
            Assert.AreEqual(AIDifficultyTier.Titan, scaling.DetermineTier(500));
        }

        [Test]
        public void SoloAIScaling_NoviceIsHandicappedAndTitanOutscalesThePlayer()
        {
            var scaling = new SoloAIScalingSystem();

            PlayerEmpireData novicePlayer = EmpireAtAvatarLevel(5);
            PlayerEmpireData titanPlayer = EmpireAtAvatarLevel(100);

            AIBattleProfile novice = scaling.GenerateAIOpponent(novicePlayer);
            AIBattleProfile titan = scaling.GenerateAIOpponent(titanPlayer);

            Assert.Less(novice.MaxAvatarHealth, novicePlayer.StartingAvatarHealth,
                "A Novice-tier opponent is meant to be handicapped relative to the player it faces.");
            Assert.Greater(titan.MaxAvatarHealth, titanPlayer.StartingAvatarHealth,
                "A Titan-tier opponent is meant to out-bulk the player it faces.");
        }

        // ---------- Command Centre-approved first tutorial battle content ----------
        //
        // Legality and forgiving-handicap regression coverage for the approved starter roster
        // (warrior/Front, novice_knight/Middle, goblin_caster/Back) and enemy deck
        // (butcher, cursed_soldier, giant_worms, tribal_warrior) at genuine Avatar level 1. This
        // validates legality and the approved handicap only - it does not simulate or assert
        // victory probability, which is explicitly out of scope for this slice.

        /// <summary>A genuine new player: every progression track at 1, matching Command Centre's
        /// approved baseline rather than any of the existing mid-test profiles this file already
        /// uses elsewhere (which deliberately keep Castle/Barracks above 1).</summary>
        private static PlayerEmpireData NewPlayerEmpireAtLevelOne()
        {
            var empire = new PlayerEmpireData();
            empire.SetLevelsForTesting(avatarLevel: 1, castleLevel: 1, barracksLevel: 1);
            empire.InitializeTCGModifiers();
            return empire;
        }

        [Test]
        public void TutorialRoster_AllSevenApprovedCardIdsResolveFromCardDatabase()
        {
            CardDatabase db = LoadDatabase();
            string[] approvedIds =
            {
                "warrior", "novice_knight", "goblin_caster",
                "butcher", "cursed_soldier", "giant_worms", "tribal_warrior",
            };

            foreach (string id in approvedIds)
            {
                Assert.IsNotNull(db.GetCard(id), $"Approved tutorial card id '{id}' must resolve from the real CardDatabase.");
            }
        }

        [Test]
        public void TutorialRoster_StarterCardsRetainTheApprovedClasses()
        {
            CardDatabase db = LoadDatabase();

            Assert.AreEqual(CardClass.Warrior, db.GetCard("warrior").Class,
                "warrior must remain Warrior class for the approved Front placement.");
            Assert.AreEqual(CardClass.Knight, db.GetCard("novice_knight").Class,
                "novice_knight must remain Knight class for Taunt and the approved Middle placement.");
            Assert.AreEqual(CardClass.Strategist, db.GetCard("goblin_caster").Class,
                "goblin_caster must remain Strategist class for the approved Back placement.");
        }

        [Test]
        public void TutorialRoster_StarterFormationDeploysLegallyWithExpectedLaneAndClassEffects()
        {
            CardDatabase db = LoadDatabase();
            Card warrior = db.GetCard("warrior");
            Card noviceKnight = db.GetCard("novice_knight");
            Card goblinCaster = db.GetCard("goblin_caster");
            Assert.NotNull(warrior, "Setup: warrior must resolve.");
            Assert.NotNull(noviceKnight, "Setup: novice_knight must resolve.");
            Assert.NotNull(goblinCaster, "Setup: goblin_caster must resolve.");

            PlayerEmpireData playerEmpire = NewPlayerEmpireAtLevelOne();
            BattleController controller = CreateController();
            var deck = new List<Card> { warrior, noviceKnight, goblinCaster };
            var economy = new BattleController.MatchEconomy(
                playerEmpire.ResourceCap, playerEmpire.Turn1Resource, playerEmpire.StartingAvatarHealth);
            controller.StartMatch(deck, deck, economy, economy);
            controller.DealFormationHand(controller.PlayerState);

            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, warrior, Lane.Front),
                "The approved starter Formation must be legally deployable: warrior to Front.");
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, noviceKnight, Lane.Middle),
                "The approved starter Formation must be legally deployable: novice_knight to Middle.");
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, goblinCaster, Lane.Back),
                "The approved starter Formation must be legally deployable: goblin_caster to Back.");
            Assert.IsTrue(controller.ConfirmFormation(), "The approved starter Formation must lock legally.");

            // The three approved cards carry three different synergy tags (Warrior/Andras,
            // Knight, Strategist), none reaching FormationSynergy's pair/trio threshold - so
            // every stat increase asserted below comes from the lane bonus alone, not synergy,
            // keeping each assertion attributable to exactly the mechanic it claims to test.
            BattleCardInstance frontUnit = controller.PlayerState.Lanes[Lane.Front].Cards[0];
            BattleCardInstance middleUnit = controller.PlayerState.Lanes[Lane.Middle].Cards[0];

            Assert.Greater(frontUnit.Attack, warrior.Attack,
                "Front placement must increase the deployed unit's live Attack over its base Attack.");
            Assert.Greater(middleUnit.MaxHealth, noviceKnight.Health,
                "Middle placement must increase the deployed unit's live MaxHealth over its base Health.");
            Assert.IsTrue(middleUnit.HasTaunt, "novice_knight must retain Taunt in the approved Middle placement.");
            Assert.Greater(BattleController.BackLaneEnergy(controller.PlayerState), 0,
                "The locked Back placement must produce positive Back-lane Energy.");
        }

        [Test]
        public void TutorialEnemyDeck_AllFourCardsAlwaysReachTheOpeningHandRegardlessOfShuffleOrder()
        {
            CardDatabase db = LoadDatabase();
            var enemyDeck = new List<Card>
            {
                db.GetCard("butcher"), db.GetCard("cursed_soldier"),
                db.GetCard("giant_worms"), db.GetCard("tribal_warrior"),
            };
            Assert.That(enemyDeck, Has.All.Not.Null, "All four approved enemy card ids must resolve.");

            // PlayerBattleState shuffles with an unseeded System.Random (see its own Shuffle
            // method) - many independent constructions exercise many different concrete orderings
            // rather than trusting one lucky run, without ever asserting a probability: every
            // single trial must pass, because a deck no larger than StartingHandSize is drawn to
            // completion regardless of the order it was shuffled into.
            for (int trial = 0; trial < 25; trial++)
            {
                var side = new PlayerBattleState(enemyDeck, resourceCap: 20, turn1Resource: 20, startingAvatarHealth: 170);

                Assert.AreEqual(enemyDeck.Count, side.Hand.Count,
                    "A deck no larger than StartingHandSize must be drawn into the opening hand in full, regardless of shuffle order.");
                foreach (Card card in enemyDeck)
                {
                    Assert.IsTrue(side.Hand.Contains(card),
                        $"'{card.Id}' must be present in the opening hand regardless of shuffle order.");
                }
                Assert.AreEqual(0, side.DrawPile.Count, "The deck should be fully drawn, leaving nothing left to draw later.");
            }
        }

        [Test]
        public void TutorialEncounter_NoviceScalingAtLevelOneIsMoreForgivingThanThePlayer()
        {
            PlayerEmpireData playerEmpire = NewPlayerEmpireAtLevelOne();
            var scaling = new SoloAIScalingSystem();

            Assert.AreEqual(AIDifficultyTier.Novice, scaling.DetermineTier(playerEmpire.AvatarLevel),
                "A genuine level-1 new player must face the Novice difficulty tier.");

            AIBattleProfile enemy = scaling.GenerateAIOpponent(playerEmpire, AIArchetype.Balanced);

            // Relationships against the player's own live-computed values, not copied literals -
            // the approved handicap ratios can be retuned without breaking this assertion's intent.
            Assert.Less(enemy.MaxAvatarHealth, playerEmpire.StartingAvatarHealth,
                "The approved Novice-tier enemy must have less maximum Avatar Health than the level-1 player.");
            Assert.LessOrEqual(enemy.StartingResourceCap, playerEmpire.ResourceCap,
                "The approved Novice-tier enemy must not have more starting Resource than the level-1 player.");
        }

        [Test]
        public void SimpleAI_AggressiveFillsFrontLane_DefensivePrefersMiddle()
        {
            // Same deck and economy for both, so the only variable is the archetype - otherwise
            // a difference here could just be deck luck rather than the placement logic.
            List<Card> deck = LoadDatabase().AllCards.Take(12).ToList();

            BattleController aggressive = CreateController();
            aggressive.StartMatch(deck.ToList(), deck.ToList(), TestEconomy(40, 40), TestEconomy(40, 40));
            SimpleAIOpponent.TakeTurn(aggressive, AIArchetype.Aggressive);

            BattleController defensive = CreateController();
            defensive.StartMatch(deck.ToList(), deck.ToList(), TestEconomy(40, 40), TestEconomy(40, 40));
            SimpleAIOpponent.TakeTurn(defensive, AIArchetype.Defensive);

            // Measured in free *slots*, not card count: a rarity 5+ card occupies two of a lane's
            // three (Card.SlotWeight), so a full lane can hold two cards rather than three.
            Assert.AreEqual(0, aggressive.EnemyState.Lanes[Lane.Front].FreeSlots,
                "An Aggressive opponent should fill the Front (+1 Attack) lane before any other.");
            Assert.AreEqual(0, defensive.EnemyState.Lanes[Lane.Middle].FreeSlots,
                "A Defensive opponent should fill the Middle (+1 Health) lane before any other.");

            // Front is Defensive's *last* choice, not a lane it refuses to use: with enough
            // resource it fills Middle and Back and then spills into Front, and the Strategist /
            // Perfect on-play draw hooks keep refilling its hand while it does so.
            //
            // Asserted as an ordering, not as "Front holds fewer than 3". PlayerBattleState
            // shuffles with a time-seeded System.Random, so how many cards the AI can actually
            // deploy varies between runs - on a lucky draw it fills all nine slots and Front
            // legitimately reaches 3. A count-based assertion passes or fails on the shuffle
            // rather than on the behaviour, which is what made this flaky.
            // Compared in slots used, for the same reason as above.
            int front = defensive.EnemyState.Lanes[Lane.Front].SlotsUsed;
            int middle = defensive.EnemyState.Lanes[Lane.Middle].SlotsUsed;
            int back = defensive.EnemyState.Lanes[Lane.Back].SlotsUsed;

            Assert.LessOrEqual(front, middle,
                "Defensive should never hold more in Front than in its preferred Middle lane.");
            Assert.LessOrEqual(front, back,
                "Defensive should never hold more in Front than in Back - Front is its last resort.");
        }

        [Test]
        public void SimpleAI_SpendsResourceOnItsStrongestAffordableCardsFirst()
        {
            CardDatabase db = LoadDatabase();
            List<Card> deck = db.AllCards.Take(12).ToList();

            BattleController controller = CreateController();
            // A cap low enough that the AI genuinely cannot play everything, which is the only
            // situation where "which card did it pick" is observable at all.
            controller.StartMatch(deck.ToList(), deck.ToList(), TestEconomy(40, 40), TestEconomy(8, 8));

            List<Card> handBefore = controller.EnemyState.Hand.ToList();
            SimpleAIOpponent.TakeTurn(controller, AIArchetype.Balanced);

            List<Card> played = controller.EnemyState.Lanes.Values
                .SelectMany(l => l.Cards)
                .Select(c => c.Definition)
                .ToList();

            Assert.IsNotEmpty(played, "The AI should have played at least one card with 8 resource available.");

            // The most expensive card it could afford at the start of the turn must be among what
            // it actually played - the old greedy version walked the hand in arbitrary order and
            // would happily burn its resource on cheap cards first.
            int mostExpensiveAffordable = handBefore.Where(c => c.ResourceCost <= 8)
                .Select(c => c.ResourceCost)
                .DefaultIfEmpty(0)
                .Max();

            if (mostExpensiveAffordable > 0)
            {
                Assert.AreEqual(mostExpensiveAffordable, played.Max(c => c.ResourceCost),
                    "The AI should have spent on the strongest card it could afford, not the first one in hand.");
            }
        }
    }
}

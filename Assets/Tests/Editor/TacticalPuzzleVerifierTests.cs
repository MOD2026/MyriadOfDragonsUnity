using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// The deterministic single-state verifier that the locked design gates Tactical Puzzle on:
    /// "If implementation does not support this deterministic verifier, the mode should not be
    /// built yet."
    ///
    /// The load-bearing property is DETERMINISM - a seeded daily puzzle is only verifiable if the
    /// same state plus the same actions always produce the same result. That is asserted directly
    /// rather than assumed.
    ///
    /// The other thing worth testing is that it REUSES existing rules: a puzzle that disagreed with
    /// real combat or real deployment legality would be worse than no puzzle, so the tests check
    /// that illegal deploys are rejected by the same SlotWeight capacity rule the battle uses.
    /// </summary>
    public class TacticalPuzzleVerifierTests
    {
        /// <summary>Cards are built the sanctioned way - Card.FromData, same as BattleLogicTests.
        /// Attack/Health/ResourceCost are DERIVED from rarity and are not settable, so these tests
        /// control strength via rarity and assert RELATIONSHIPS rather than pinning stat
        /// magnitudes - which is the standing project rule anyway.</summary>
        private static Card MakeCard(string id, int rarity) =>
            Card.FromData(new CardData
            {
                id = id, name = id, art_file = "x.png", element = "Andras", type = "knight", rarity = rarity,
            });

        private const int Weak = 1;
        private const int Strong = 7;

        /// <summary>Resource defaults deliberately high. ResourceCost is DERIVED from rarity, so
        /// a hand-picked pool silently turns unrelated tests into affordability tests - that is
        /// exactly what happened on the first run here, where every rarity-7 deploy was correctly
        /// rejected and five objective tests failed as a consequence. Only the affordability test
        /// constrains the pool.</summary>
        private static PlayerBattleState Side(IEnumerable<Card> hand, int resource)
        {
            var s = new PlayerBattleState(new List<Card>(), 10, resource, 30);
            s.Hand.Clear();
            foreach (Card c in hand) s.Hand.Add(c);
            s.Resource = resource;
            return s;
        }

        private static void Place(PlayerBattleState side, Lane lane, Card card, bool playerOwned)
        {
            side.Lanes[lane].Cards.Add(new BattleCardInstance(card, playerOwned, 0, 0));
        }

        private static TacticalPuzzleAction Deploy(int handIndex, Lane lane) =>
            new TacticalPuzzleAction { Kind = TacticalPuzzleActionKind.Deploy, HandIndex = handIndex, Lane = lane };

        // ---------- determinism: the property the whole mode depends on ----------

        [Test]
        public void TheSameStateAndActions_AlwaysProduceTheSameResult()
        {
            TacticalPuzzleResult first = null;
            for (int run = 0; run < 5; run++)
            {
                PlayerBattleState player = Side(new[] { MakeCard("a", Weak) }, 99);
                PlayerBattleState enemy = Side(new Card[0], 0);
                Place(enemy, Lane.Front, MakeCard("e", Weak), false);

                var objective = new TacticalPuzzleObjective
                {
                    Kind = TacticalPuzzleObjectiveKind.ProtectLane, ProtectedLane = Lane.Front,
                };
                TacticalPuzzleResult r = TacticalPuzzleVerifier.Verify(
                    player, enemy, objective, new[] { Deploy(0, Lane.Front) });

                if (first == null) { first = r; continue; }
                Assert.AreEqual(first.Status, r.Status, "Run " + run + " diverged - the verifier is not deterministic.");
                Assert.AreEqual(first.ResourceRemaining, r.ResourceRemaining);
                Assert.AreEqual(first.FriendlyUnitsAlive, r.FriendlyUnitsAlive);
                Assert.AreEqual(first.LanesHeld, r.LanesHeld);
            }
        }

        // ---------- reuse of existing legality ----------

        [Test]
        public void ADeployBeyondLaneCapacity_IsRejected_ByTheSameSlotWeightRuleTheBattleUses()
        {
            PlayerBattleState player = Side(new[] { MakeCard("d", Weak) }, 99);
            for (int i = 0; i < LaneState.MaxSlots; i++)
                Place(player, Lane.Front, MakeCard("filler" + i, Weak), true);
            Assert.AreEqual(0, player.Lanes[Lane.Front].FreeSlots, "Setup: lane should be full.");

            TacticalPuzzleResult r = TacticalPuzzleVerifier.Verify(
                player, Side(new Card[0], 0),
                new TacticalPuzzleObjective { Kind = TacticalPuzzleObjectiveKind.ProtectLane, ProtectedLane = Lane.Front },
                new[] { Deploy(0, Lane.Front) });

            Assert.AreEqual(TacticalPuzzleStatus.IllegalAction, r.Status);
            Assert.AreEqual(0, r.FailedActionIndex, "The rejecting action must be identified.");
        }

        [Test]
        public void ADeployBeyondResource_IsRejectedAsInsufficientResource_NotAsIllegal()
        {
            // Resource 0 guarantees the check fires regardless of the derived cost curve -
            // pinning a rarity-to-cost number here would be exactly the brittle assumption the
            // project forbids.
            PlayerBattleState player = Side(new[] { MakeCard("pricey", Strong) }, 0);

            TacticalPuzzleResult r = TacticalPuzzleVerifier.Verify(
                player, Side(new Card[0], 0),
                new TacticalPuzzleObjective { Kind = TacticalPuzzleObjectiveKind.ProtectLane, ProtectedLane = Lane.Front },
                new[] { Deploy(0, Lane.Front) });

            Assert.AreEqual(TacticalPuzzleStatus.InsufficientResource, r.Status,
                "Affordability and legality are different failures and must not be conflated.");
        }

        [Test]
        public void AnOutOfRangeHandIndex_IsRejected()
        {
            PlayerBattleState player = Side(new[] { MakeCard("a", Weak) }, 99);

            TacticalPuzzleResult r = TacticalPuzzleVerifier.Verify(
                player, Side(new Card[0], 0),
                new TacticalPuzzleObjective { Kind = TacticalPuzzleObjectiveKind.ProtectLane },
                new[] { Deploy(7, Lane.Front) });

            Assert.AreEqual(TacticalPuzzleStatus.IllegalAction, r.Status);
        }

        [Test]
        public void ADeploy_SpendsResourceAndConsumesTheHandCard()
        {
            PlayerBattleState player = Side(new[] { MakeCard("a", Weak) }, 99);

            TacticalPuzzleVerifier.Verify(
                player, Side(new Card[0], 0),
                new TacticalPuzzleObjective { Kind = TacticalPuzzleObjectiveKind.ProtectLane, ProtectedLane = Lane.Front },
                new[] { Deploy(0, Lane.Front) });

            Assert.Less(player.Resource, 99, "Resource must be spent.");
            CollectionAssert.IsEmpty(player.Hand, "There is no draw, so a played card simply leaves the fixed hand.");
        }

        // ---------- objectives ----------

        [Test]
        public void ProtectLane_IsMet_WhenTheLaneStillHoldsALivingUnit()
        {
            PlayerBattleState player = Side(new[] { MakeCard("tank", Strong) }, 99);
            PlayerBattleState enemy = Side(new Card[0], 0);
            Place(enemy, Lane.Front, MakeCard("weak", Weak), false);

            TacticalPuzzleResult r = TacticalPuzzleVerifier.Verify(
                player, enemy,
                new TacticalPuzzleObjective { Kind = TacticalPuzzleObjectiveKind.ProtectLane, ProtectedLane = Lane.Front },
                new[] { Deploy(0, Lane.Front) });

            Assert.IsTrue(r.Solved);
            Assert.GreaterOrEqual(r.LanesHeld, 1);
        }

        [Test]
        public void ProtectLane_IsNotMet_WhenNothingWasDeployedThere()
        {
            PlayerBattleState player = Side(new[] { MakeCard("tank", Strong) }, 99);
            PlayerBattleState enemy = Side(new Card[0], 0);

            TacticalPuzzleResult r = TacticalPuzzleVerifier.Verify(
                player, enemy,
                new TacticalPuzzleObjective { Kind = TacticalPuzzleObjectiveKind.ProtectLane, ProtectedLane = Lane.Front },
                new TacticalPuzzleAction[0]);

            Assert.AreEqual(TacticalPuzzleStatus.ObjectiveNotMet, r.Status);
        }

        [Test]
        public void DefeatMarkedTarget_IsMet_OnlyWhenThatSpecificUnitDies()
        {
            PlayerBattleState player = Side(new[] { MakeCard("hitter", Strong) }, 99);
            PlayerBattleState enemy = Side(new Card[0], 0);
            Place(enemy, Lane.Front, MakeCard("mark", Weak), false);
            BattleCardInstance marked = enemy.Lanes[Lane.Front].Cards[0];

            TacticalPuzzleResult r = TacticalPuzzleVerifier.Verify(
                player, enemy,
                new TacticalPuzzleObjective { Kind = TacticalPuzzleObjectiveKind.DefeatMarkedTarget, MarkedTarget = marked },
                new[] { Deploy(0, Lane.Front) });

            Assert.IsTrue(r.Solved);
            Assert.IsFalse(marked.IsAlive);
        }

        [Test]
        public void DefeatMarkedTarget_IsNotMet_WhenTheTargetSurvives()
        {
            PlayerBattleState player = Side(new[] { MakeCard("weak", Weak) }, 99);
            PlayerBattleState enemy = Side(new Card[0], 0);
            Place(enemy, Lane.Front, MakeCard("tanky", Strong), false);
            BattleCardInstance marked = enemy.Lanes[Lane.Front].Cards[0];

            TacticalPuzzleResult r = TacticalPuzzleVerifier.Verify(
                player, enemy,
                new TacticalPuzzleObjective { Kind = TacticalPuzzleObjectiveKind.DefeatMarkedTarget, MarkedTarget = marked },
                new[] { Deploy(0, Lane.Front) });

            Assert.AreEqual(TacticalPuzzleStatus.ObjectiveNotMet, r.Status);
            Assert.IsTrue(marked.IsAlive);
        }

        [Test]
        public void SurviveClashes_FailsWhenTheBoardIsWipedBeforeTheCountIsReached()
        {
            PlayerBattleState player = Side(new[] { MakeCard("paper", Weak) }, 99);
            PlayerBattleState enemy = Side(new Card[0], 0);
            Place(enemy, Lane.Front, MakeCard("crusher", Strong), false);

            TacticalPuzzleResult r = TacticalPuzzleVerifier.Verify(
                player, enemy,
                new TacticalPuzzleObjective { Kind = TacticalPuzzleObjectiveKind.SurviveClashes, ClashCount = 3 },
                new[] { Deploy(0, Lane.Front) });

            Assert.AreEqual(TacticalPuzzleStatus.ObjectiveNotMet, r.Status);
        }

        [Test]
        public void SurviveClashes_WithNoClashCount_IsAMalformedObjective_NotAFreeWin()
        {
            PlayerBattleState player = Side(new[] { MakeCard("a", Strong) }, 99);

            TacticalPuzzleResult r = TacticalPuzzleVerifier.Verify(
                player, Side(new Card[0], 0),
                new TacticalPuzzleObjective { Kind = TacticalPuzzleObjectiveKind.SurviveClashes, ClashCount = 0 },
                new[] { Deploy(0, Lane.Front) });

            Assert.AreEqual(TacticalPuzzleStatus.ObjectiveNotMet, r.Status,
                "An unset clash count must never resolve as solved.");
        }

        [Test]
        public void MinimalResourceSolve_FailsWhenTheBudgetIsExceeded()
        {
            PlayerBattleState player = Side(new[] { MakeCard("a", Strong) }, 99);
            PlayerBattleState enemy = Side(new Card[0], 0);

            TacticalPuzzleResult r = TacticalPuzzleVerifier.Verify(
                player, enemy,
                new TacticalPuzzleObjective { Kind = TacticalPuzzleObjectiveKind.MinimalResourceSolve, ResourceBudget = 2 },
                new[] { Deploy(0, Lane.Front) });

            Assert.AreEqual(TacticalPuzzleStatus.ObjectiveNotMet, r.Status);
        }

        [Test]
        public void MinimalResourceSolve_IsNotSatisfiedByDoingNothingAndHoldingNoBoard()
        {
            PlayerBattleState player = Side(new[] { MakeCard("a", Strong) }, 99);

            TacticalPuzzleResult r = TacticalPuzzleVerifier.Verify(
                player, Side(new Card[0], 0),
                new TacticalPuzzleObjective { Kind = TacticalPuzzleObjectiveKind.MinimalResourceSolve, ResourceBudget = 3 },
                new TacticalPuzzleAction[0]);

            Assert.AreEqual(TacticalPuzzleStatus.ObjectiveNotMet, r.Status,
                "Spending nothing and holding nothing is not a solve.");
        }

        // ---------- scoring inputs + malformed puzzles ----------

        [Test]
        public void TheResult_ReportsTheDecisionBasedScoreInputs()
        {
            PlayerBattleState player = Side(new[] { MakeCard("a", Strong), MakeCard("b", Strong) }, 99);
            PlayerBattleState enemy = Side(new Card[0], 0);

            TacticalPuzzleResult r = TacticalPuzzleVerifier.Verify(
                player, enemy,
                new TacticalPuzzleObjective { Kind = TacticalPuzzleObjectiveKind.ProtectLane, ProtectedLane = Lane.Front },
                new[] { Deploy(0, Lane.Front), Deploy(0, Lane.Middle) });

            Assert.AreEqual(2, r.ActionsUsed);
            Assert.Less(r.ResourceRemaining, 99, "Two deploys must have cost Resource.");
            Assert.AreEqual(2, r.FriendlyUnitsAlive);
            Assert.AreEqual(2, r.LanesHeld);
        }

        [Test]
        public void AMalformedPuzzle_IsRejectedRatherThanGuessed()
        {
            TacticalPuzzleResult r = TacticalPuzzleVerifier.Verify(
                null, null, null, new TacticalPuzzleAction[0]);

            Assert.AreEqual(TacticalPuzzleStatus.MalformedPuzzle, r.Status);
        }

        [Test]
        public void NoActions_IsValidInput_NotACrash()
        {
            PlayerBattleState player = Side(new Card[0], 99);

            Assert.DoesNotThrow(() => TacticalPuzzleVerifier.Verify(
                player, Side(new Card[0], 0),
                new TacticalPuzzleObjective { Kind = TacticalPuzzleObjectiveKind.ProtectLane }, null));
        }
    }
}

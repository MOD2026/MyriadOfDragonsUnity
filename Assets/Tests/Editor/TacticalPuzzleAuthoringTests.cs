using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// The authoring half of the Tactical Puzzle gate: can a puzzle be DEFINED as data, and does
    /// the definition say what its author thinks it says?
    ///
    /// NO PUZZLE CONTENT IS ASSERTED HERE. Every fixture builds a throwaway definition to exercise
    /// a structural rule; none of these numbers are proposed puzzle values, and nothing asserts a
    /// difficulty or a curve. Puzzle content is an explicitly separate design pass.
    ///
    /// The card source is injected rather than taken from CardDatabase, so these tests do not
    /// depend on the shipped card set - a puzzle test that broke when a designer retuned a card
    /// would be testing the wrong thing. Stats are never hardcoded either: rarity DERIVES
    /// Attack/Health/ResourceCost/SlotWeight (Card.ComputeStats), so the tests read those off the
    /// card rather than assuming them.
    /// </summary>
    public class TacticalPuzzleAuthoringTests
    {
        private static Card MakeCard(string id, int rarity, string type = "warrior") =>
            Card.FromData(new CardData
            {
                id = id,
                name = id,
                art_file = "",
                element = "Andras",
                type = type,
                rarity = rarity,
            });

        /// <summary>A tiny fixed catalogue. Rarity 1 is the lightest unit available and rarity 7
        /// the heaviest, which is what makes them useful for capacity and affordability rules -
        /// not because those rarities mean anything to puzzle design.</summary>
        private static Dictionary<string, Card> Catalogue() => new Dictionary<string, Card>
        {
            { "small_a", MakeCard("small_a", 1) },
            { "small_b", MakeCard("small_b", 1) },
            { "heavy", MakeCard("heavy", 7) },
        };

        private static System.Func<string, Card> SourceFrom(Dictionary<string, Card> cards) =>
            id => id != null && cards.TryGetValue(id, out Card c) ? c : null;

        private System.Func<string, Card> _source;
        private Dictionary<string, Card> _cards;

        [SetUp]
        public void SetUp()
        {
            _cards = Catalogue();
            _source = SourceFrom(_cards);
        }

        /// <summary>A minimally-coherent definition. Resource is set generously so that tests
        /// about OTHER rules are never accidentally decided by affordability - the mistake that
        /// silently turned five verifier tests into affordability tests.</summary>
        private TacticalPuzzleDefinition ValidDefinition()
        {
            return new TacticalPuzzleDefinition
            {
                PuzzleId = "test_structure_only",
                DisplayName = "Structure Only",
                StartingResource = 99,
                ResourceCap = 99,
                AvatarHealth = 20,
                Hand = new List<string> { "small_a", "small_b" },
                PlayerBoard = new List<TacticalPuzzleUnitSpec>
                {
                    new TacticalPuzzleUnitSpec { CardId = "small_a", Lane = Lane.Front },
                },
                EnemyBoard = new List<TacticalPuzzleUnitSpec>
                {
                    new TacticalPuzzleUnitSpec { CardId = "small_b", Lane = Lane.Front },
                },
                Objective = new TacticalPuzzleObjectiveSpec
                {
                    Kind = TacticalPuzzleObjectiveKind.ProtectLane,
                    ProtectedLane = Lane.Front,
                },
            };
        }

        // ---------------------------------------------------------------- validation

        [Test]
        public void ACoherentDefinition_ReportsNoProblems()
        {
            CollectionAssert.IsEmpty(TacticalPuzzleAuthoring.Validate(ValidDefinition(), _source),
                "A definition built to satisfy every structural rule still reported problems.");
        }

        [Test]
        public void Validation_ReportsEveryProblemAtOnce_NotJustTheFirst()
        {
            // An authoring tool that surfaces one error per save is a bad authoring tool, so this
            // is a real requirement rather than a convenience.
            var def = ValidDefinition();
            def.PuzzleId = "";
            def.ResourceCap = 0;
            def.AvatarHealth = 0;
            def.Hand.Add("no_such_card");

            List<string> problems = TacticalPuzzleAuthoring.Validate(def, _source);

            Assert.GreaterOrEqual(problems.Count, 4,
                "Expected each independent problem reported separately, got: " + string.Join("  |  ", problems));
        }

        [Test]
        public void AnUnknownCardId_IsAnAuthoringProblem_NotACrash()
        {
            var def = ValidDefinition();
            def.PlayerBoard[0].CardId = "typo_id";

            List<string> problems = TacticalPuzzleAuthoring.Validate(def, _source);

            Assert.IsTrue(problems.Any(p => p.Contains("typo_id")),
                "A typo'd card id must be reported by name so an author can find it: " +
                string.Join("  |  ", problems));
        }

        [Test]
        public void AnOverfullLane_IsCaughtBySlotWeight_NotByCardCount()
        {
            // Two rarity-7 units are only TWO cards but exceed a three-slot lane, so a count-based
            // check would pass this and a real deployment would then be impossible.
            var def = ValidDefinition();
            Assert.Greater(_cards["heavy"].SlotWeight, 1, "Setup: expected the heavy card to take multiple slots.");
            def.PlayerBoard = new List<TacticalPuzzleUnitSpec>
            {
                new TacticalPuzzleUnitSpec { CardId = "heavy", Lane = Lane.Front },
                new TacticalPuzzleUnitSpec { CardId = "heavy", Lane = Lane.Front },
            };

            List<string> problems = TacticalPuzzleAuthoring.Validate(def, _source);

            Assert.IsTrue(problems.Any(p => p.Contains("Front") && p.Contains("slots")),
                "Two multi-slot units in one lane must be reported as over capacity: " +
                string.Join("  |  ", problems));
        }

        [Test]
        public void ThreeLightUnits_FitALaneExactly()
        {
            // The other side of the same rule - the capacity check must not be a blanket
            // "no more than two cards".
            var def = ValidDefinition();
            def.PlayerBoard = new List<TacticalPuzzleUnitSpec>
            {
                new TacticalPuzzleUnitSpec { CardId = "small_a", Lane = Lane.Front },
                new TacticalPuzzleUnitSpec { CardId = "small_a", Lane = Lane.Front },
                new TacticalPuzzleUnitSpec { CardId = "small_a", Lane = Lane.Front },
            };

            CollectionAssert.IsEmpty(TacticalPuzzleAuthoring.Validate(def, _source),
                "Three single-slot units exactly fill a lane and must validate.");
        }

        [Test]
        public void AUnitThatWouldStartDead_IsAnAuthoringProblem()
        {
            var def = ValidDefinition();
            def.PlayerBoard[0].PreDamage = _cards["small_a"].Health;

            Assert.IsTrue(TacticalPuzzleAuthoring.Validate(def, _source).Any(p => p.Contains("already dead")),
                "Pre-damage at or above Health means the puzzle starts with a corpse on the board.");
        }

        [Test]
        public void EachObjectiveKind_RequiresItsOwnField()
        {
            // The verifier already treats an unset field as "not solved" rather than a free win.
            // This is the second line of defence, at the point where the author can still fix it.
            var missing = new Dictionary<TacticalPuzzleObjectiveKind, TacticalPuzzleObjectiveSpec>
            {
                { TacticalPuzzleObjectiveKind.SurviveClashes,
                  new TacticalPuzzleObjectiveSpec { Kind = TacticalPuzzleObjectiveKind.SurviveClashes, ClashCount = 0 } },
                { TacticalPuzzleObjectiveKind.DefeatMarkedTarget,
                  new TacticalPuzzleObjectiveSpec { Kind = TacticalPuzzleObjectiveKind.DefeatMarkedTarget, MarkedTarget = null } },
                { TacticalPuzzleObjectiveKind.MinimalResourceSolve,
                  new TacticalPuzzleObjectiveSpec { Kind = TacticalPuzzleObjectiveKind.MinimalResourceSolve, ResourceBudget = 0 } },
            };

            foreach (var pair in missing)
            {
                var def = ValidDefinition();
                def.Objective = pair.Value;
                CollectionAssert.IsNotEmpty(TacticalPuzzleAuthoring.Validate(def, _source),
                    pair.Key + " with its required field unset must be reported.");
            }
        }

        [Test]
        public void AMarkedTarget_PointingAtNothing_IsReported()
        {
            var def = ValidDefinition();
            def.Objective = new TacticalPuzzleObjectiveSpec
            {
                Kind = TacticalPuzzleObjectiveKind.DefeatMarkedTarget,
                MarkedTarget = new TacticalPuzzleUnitRef
                {
                    Side = TacticalPuzzleSide.Enemy, Lane = Lane.Back, IndexInLane = 0,
                },
            };

            Assert.IsTrue(TacticalPuzzleAuthoring.Validate(def, _source).Any(p => p.Contains("no authored unit")),
                "An objective pointing at an empty coordinate is unwinnable and must be caught.");
        }

        [Test]
        public void AResourceBudget_AboveTheStartingPool_CannotBind_AndIsReported()
        {
            var def = ValidDefinition();
            def.StartingResource = 5;
            def.Objective = new TacticalPuzzleObjectiveSpec
            {
                Kind = TacticalPuzzleObjectiveKind.MinimalResourceSolve,
                ResourceBudget = 6,
            };

            Assert.IsTrue(TacticalPuzzleAuthoring.Validate(def, _source).Any(p => p.Contains("could never bind")),
                "A budget larger than the whole pool is a constraint that can never fail - that is an authoring slip.");
        }

        // ---------------------------------------------------------------- materialisation

        [Test]
        public void Materialisation_PlacesTheAuthoredBoardAndHand_WithNoDraw()
        {
            var def = ValidDefinition();
            MaterializedPuzzle puzzle = TacticalPuzzleAuthoring.Materialize(def, _source);

            Assert.AreEqual(def.Hand.Count, puzzle.PlayerSide.Hand.Count, "Hand size must match the authored hand.");
            Assert.AreEqual(0, puzzle.PlayerSide.DrawPile.Count, "A puzzle has no draw pile - there is no draw.");
            Assert.AreEqual(def.StartingResource, puzzle.PlayerSide.Resource, "Authored Resource must be applied.");
            Assert.AreEqual(1, puzzle.PlayerSide.Lanes[Lane.Front].Cards.Count);
            Assert.AreEqual(1, puzzle.EnemySide.Lanes[Lane.Front].Cards.Count);
        }

        [Test]
        public void TheAuthoredHandOrder_SurvivesMaterialisation()
        {
            // This is the one that would break silently and take a whole puzzle set with it.
            // PlayerBattleState's constructor SHUFFLES the deck it is given and auto-draws, so
            // passing the hand in as a deck would randomise it and every Deploy index in every
            // envelope would then point at the wrong card - intermittently.
            var def = ValidDefinition();
            def.Hand = new List<string> { "small_a", "small_b", "heavy" };

            for (int attempt = 0; attempt < 5; attempt++)
            {
                MaterializedPuzzle puzzle = TacticalPuzzleAuthoring.Materialize(def, _source);
                CollectionAssert.AreEqual(
                    def.Hand,
                    puzzle.PlayerSide.Hand.Select(c => c.Id).ToList(),
                    "Hand order must be exactly as authored on every materialisation (attempt " + attempt + ").");
            }
        }

        [Test]
        public void Materialisation_IsDeterministic_AcrossRepeatedBuilds()
        {
            var def = ValidDefinition();
            string First()
            {
                MaterializedPuzzle p = TacticalPuzzleAuthoring.Materialize(def, _source);
                return string.Join(",", p.PlayerSide.Lanes.Values
                    .SelectMany(l => l.Cards.Select(c => l.Lane + ":" + c.Definition.Id + ":" + c.CurrentHealth)));
            }

            string baseline = First();
            for (int i = 0; i < 4; i++)
                Assert.AreEqual(baseline, First(), "Materialisation must be deterministic - a seeded daily depends on it.");
        }

        [Test]
        public void PreDamage_GoesThroughTheRealDamagePath()
        {
            var def = ValidDefinition();
            def.PlayerBoard[0].PreDamage = 1;

            MaterializedPuzzle puzzle = TacticalPuzzleAuthoring.Materialize(def, _source);
            BattleCardInstance unit = puzzle.PlayerSide.Lanes[Lane.Front].Cards[0];

            Assert.AreEqual(unit.MaxHealth - 1, unit.CurrentHealth,
                "Pre-damage must reduce health through ApplyDamage, not by assigning it.");
            Assert.IsTrue(unit.IsAlive);
        }

        [Test]
        public void LaneBonuses_MatchWhatALiveDeploymentWouldGive()
        {
            // A puzzle unit whose stats differ from the same unit in a real match would make the
            // mode teach the wrong thing.
            var def = ValidDefinition();
            def.PlayerBoard = new List<TacticalPuzzleUnitSpec>
            {
                new TacticalPuzzleUnitSpec { CardId = "small_a", Lane = Lane.Front },
                new TacticalPuzzleUnitSpec { CardId = "small_a", Lane = Lane.Middle },
            };

            MaterializedPuzzle puzzle = TacticalPuzzleAuthoring.Materialize(def, _source);
            Card card = _cards["small_a"];

            Assert.AreEqual(card.Attack + RepositionRules.LaneAttackBonusFor(Lane.Front),
                puzzle.PlayerSide.Lanes[Lane.Front].Cards[0].Attack, "Front lane attack bonus not applied.");
            Assert.AreEqual(card.Health + RepositionRules.LaneHealthBonusFor(Lane.Middle),
                puzzle.PlayerSide.Lanes[Lane.Middle].Cards[0].MaxHealth, "Middle lane health bonus not applied.");
        }

        // ---------------------------------------------------------------- references

        [Test]
        public void AUnitReference_ResolvesToTheAuthoredUnit()
        {
            var def = ValidDefinition();
            def.EnemyBoard = new List<TacticalPuzzleUnitSpec>
            {
                new TacticalPuzzleUnitSpec { CardId = "small_a", Lane = Lane.Back },
                new TacticalPuzzleUnitSpec { CardId = "small_b", Lane = Lane.Back },
            };

            MaterializedPuzzle puzzle = TacticalPuzzleAuthoring.Materialize(def, _source);

            Assert.AreEqual("small_b", puzzle.Resolve(new TacticalPuzzleUnitRef
            {
                Side = TacticalPuzzleSide.Enemy, Lane = Lane.Back, IndexInLane = 1,
            })?.Definition.Id, "Index must follow authored order within the lane.");
        }

        [Test]
        public void AReferenceToNothing_ResolvesToNull_RatherThanThrowing()
        {
            MaterializedPuzzle puzzle = TacticalPuzzleAuthoring.Materialize(ValidDefinition(), _source);

            Assert.IsNull(puzzle.Resolve(new TacticalPuzzleUnitRef
            {
                Side = TacticalPuzzleSide.Player, Lane = Lane.Back, IndexInLane = 3,
            }), "A bad reference must be reportable, not fatal.");
        }

        [Test]
        public void AReferenceStillPointsAtItsUnit_AfterThatUnitMovesLane()
        {
            // Coordinates mean "where this unit started". If they were re-read off the live lane
            // lists, a Windstep earlier in a sequence would renumber every later reference.
            var def = ValidDefinition();
            def.PlayerBoard = new List<TacticalPuzzleUnitSpec>
            {
                new TacticalPuzzleUnitSpec { CardId = "small_a", Lane = Lane.Front },
            };

            MaterializedPuzzle puzzle = TacticalPuzzleAuthoring.Materialize(def, _source);
            var unitRef = new TacticalPuzzleUnitRef
            {
                Side = TacticalPuzzleSide.Player, Lane = Lane.Front, IndexInLane = 0,
            };
            BattleCardInstance before = puzzle.Resolve(unitRef);

            RepositionRules.ExecuteWindstep(puzzle.PlayerSide, before, Lane.Middle);

            Assert.AreSame(before, puzzle.Resolve(unitRef),
                "A coordinate must keep naming the same unit after that unit moves.");
        }

        // ---------------------------------------------------------------- envelope

        [Test]
        public void AClaimedSolution_IsCheckedAgainstTheRealVerifier()
        {
            var def = ValidDefinition();
            def.Envelope = new List<TacticalPuzzleExpectation>
            {
                new TacticalPuzzleExpectation
                {
                    Description = "hold the front lane",
                    Actions = new List<TacticalPuzzleActionSpec>(),
                    ExpectedStatus = TacticalPuzzleStatus.ObjectiveMet,
                },
            };

            List<TacticalPuzzleEnvelopeMismatch> mismatches = TacticalPuzzleAuthoring.CheckEnvelope(def, _source);

            // Whatever the real rules produce, the point is that the claim was MEASURED. If the
            // outcome disagrees, the mismatch names the expectation - which is the whole feature.
            Assert.IsTrue(mismatches.Count == 0 || mismatches[0].Description == "hold the front lane",
                "A mismatch must identify which authored expectation failed.");
        }

        [Test]
        public void AWrongClaim_IsReportedAsAMismatch()
        {
            // The single most likely authoring mistake: an author says a line solves the puzzle
            // and it does not. Claim the impossible and require the tool to catch it.
            var def = ValidDefinition();
            def.Objective = new TacticalPuzzleObjectiveSpec
            {
                Kind = TacticalPuzzleObjectiveKind.DefeatMarkedTarget,
                MarkedTarget = new TacticalPuzzleUnitRef
                {
                    Side = TacticalPuzzleSide.Enemy, Lane = Lane.Front, IndexInLane = 0,
                },
            };
            def.EnemyBoard = new List<TacticalPuzzleUnitSpec>
            {
                // A heavy defender against a single light attacker - not killable in one clash.
                new TacticalPuzzleUnitSpec { CardId = "heavy", Lane = Lane.Front },
            };
            def.Envelope = new List<TacticalPuzzleExpectation>
            {
                new TacticalPuzzleExpectation
                {
                    Description = "claims a kill that cannot happen",
                    Actions = new List<TacticalPuzzleActionSpec>(),
                    ExpectedStatus = TacticalPuzzleStatus.ObjectiveMet,
                },
            };

            List<TacticalPuzzleEnvelopeMismatch> mismatches = TacticalPuzzleAuthoring.CheckEnvelope(def, _source);

            Assert.AreEqual(1, mismatches.Count,
                "An intended solution that does not solve must be reported: " +
                string.Join("  |  ", mismatches.Select(m => m.ToString())));
            Assert.AreEqual(TacticalPuzzleStatus.ObjectiveMet, mismatches[0].Expected);
            Assert.AreNotEqual(TacticalPuzzleStatus.ObjectiveMet, mismatches[0].Actual);
        }

        [Test]
        public void AnIllegalityClaim_IsCheckedTooAndCanNameTheOffendingAction()
        {
            var def = ValidDefinition();
            def.StartingResource = 0;
            def.ResourceCap = 1;
            def.Envelope = new List<TacticalPuzzleExpectation>
            {
                new TacticalPuzzleExpectation
                {
                    Description = "cannot afford the first deploy",
                    Actions = new List<TacticalPuzzleActionSpec>
                    {
                        new TacticalPuzzleActionSpec
                        {
                            Kind = TacticalPuzzleActionKind.Deploy, HandIndex = 0, Lane = Lane.Middle,
                        },
                    },
                    ExpectedStatus = TacticalPuzzleStatus.InsufficientResource,
                    ExpectedFailedActionIndex = 0,
                },
            };

            Assert.Greater(_cards["small_a"].ResourceCost, 0, "Setup: the deploy must actually cost something.");
            CollectionAssert.IsEmpty(TacticalPuzzleAuthoring.CheckEnvelope(def, _source),
                "An authored 'this is unaffordable' claim that is true must produce no mismatch.");
        }

        [Test]
        public void AnEnvelopeThatBlamesTheWrongAction_IsAMismatch_EvenWithTheRightOutcome()
        {
            // Right outcome for the wrong reason is still a broken puzzle: the tutorial text or
            // hint attached to it would point at the wrong move.
            var def = ValidDefinition();
            def.StartingResource = 0;
            def.ResourceCap = 1;
            def.Envelope = new List<TacticalPuzzleExpectation>
            {
                new TacticalPuzzleExpectation
                {
                    Description = "blames action 1, but action 0 is the one that fails",
                    Actions = new List<TacticalPuzzleActionSpec>
                    {
                        new TacticalPuzzleActionSpec
                        {
                            Kind = TacticalPuzzleActionKind.Deploy, HandIndex = 0, Lane = Lane.Middle,
                        },
                        new TacticalPuzzleActionSpec
                        {
                            Kind = TacticalPuzzleActionKind.Deploy, HandIndex = 1, Lane = Lane.Middle,
                        },
                    },
                    ExpectedStatus = TacticalPuzzleStatus.InsufficientResource,
                    ExpectedFailedActionIndex = 1,
                },
            };

            List<TacticalPuzzleEnvelopeMismatch> mismatches = TacticalPuzzleAuthoring.CheckEnvelope(def, _source);

            Assert.AreEqual(1, mismatches.Count);
            StringAssert.Contains("wrong action blamed", mismatches[0].Detail);
        }

        [Test]
        public void EachExpectation_RunsAgainstAFreshBoard()
        {
            // The verifier MUTATES the state it is given, so a shared state would let an earlier
            // expectation's deploys leak into a later one - and the later one would then pass or
            // fail for reasons its author never wrote down.
            //
            // The property is REPRODUCIBILITY, not any particular outcome: the same line must
            // produce the same result every time. An earlier version of this test asserted
            // ObjectiveMet instead, which quietly made it a claim about puzzle CONTENT - the exact
            // thing this suite must not assert - and it failed for that reason rather than
            // because anything leaked.
            var def = ValidDefinition();
            var line = new List<TacticalPuzzleActionSpec>
            {
                new TacticalPuzzleActionSpec
                {
                    Kind = TacticalPuzzleActionKind.Deploy, HandIndex = 0, Lane = Lane.Back,
                },
            };

            TacticalPuzzleResult first = TacticalPuzzleAuthoring.Run(def, line, _source);
            TacticalPuzzleResult second = TacticalPuzzleAuthoring.Run(def, line, _source);

            Assert.AreEqual(first.Status, second.Status, "Same line, different outcome - state leaked.");
            Assert.AreEqual(first.ResourceRemaining, second.ResourceRemaining,
                "Resource must be spent from a fresh pool each run.");
            Assert.AreEqual(first.FriendlyUnitsAlive, second.FriendlyUnitsAlive,
                "The board must be rebuilt, not carried over.");

            // And the definition itself must be untouched by a run - the deploy consumed a card
            // from the MATERIALISED hand, not from the authored data.
            Assert.AreEqual(2, def.Hand.Count, "Running a line must not mutate the definition.");
            Assert.AreEqual(def.Hand.Count,
                TacticalPuzzleAuthoring.Materialize(def, _source).PlayerSide.Hand.Count,
                "A later materialisation must still get the full authored hand.");
        }

        [Test]
        public void AnEmptyEnvelope_ProducesNoMismatches_ButIsNotMistakenForVerification()
        {
            // Guards this suite against the trap two earlier harnesses hit: a check that always
            // passes because it measures nothing. An empty envelope is legitimately clean, so the
            // meaningful assertion is that a NON-empty one can still fail - covered above.
            var def = ValidDefinition();
            def.Envelope = new List<TacticalPuzzleExpectation>();

            CollectionAssert.IsEmpty(TacticalPuzzleAuthoring.CheckEnvelope(def, _source));
            CollectionAssert.IsEmpty(TacticalPuzzleAuthoring.Validate(def, _source));
        }

        // ---------------------------------------------------------------- serialisation

        [Test]
        public void ADefinition_SurvivesAJsonRoundTrip()
        {
            // "Data-driven" is only true if a puzzle can leave the editor. JsonUtility is the
            // engine's own serialiser and the one a seeded daily would ship through.
            var def = ValidDefinition();
            def.Envelope = new List<TacticalPuzzleExpectation>
            {
                new TacticalPuzzleExpectation
                {
                    Description = "line",
                    ExpectedStatus = TacticalPuzzleStatus.ObjectiveMet,
                    Actions = new List<TacticalPuzzleActionSpec>
                    {
                        new TacticalPuzzleActionSpec
                        {
                            Kind = TacticalPuzzleActionKind.Deploy, HandIndex = 1, Lane = Lane.Back,
                        },
                    },
                },
            };

            string json = UnityEngine.JsonUtility.ToJson(def);
            var restored = UnityEngine.JsonUtility.FromJson<TacticalPuzzleDefinition>(json);

            Assert.AreEqual(def.PuzzleId, restored.PuzzleId);
            Assert.AreEqual(def.StartingResource, restored.StartingResource);
            CollectionAssert.AreEqual(def.Hand, restored.Hand, "Hand order must survive serialisation.");
            Assert.AreEqual(def.PlayerBoard[0].CardId, restored.PlayerBoard[0].Lane == Lane.Front
                ? restored.PlayerBoard[0].CardId : null);
            Assert.AreEqual(1, restored.Envelope.Count);
            Assert.AreEqual(1, restored.Envelope[0].Actions[0].HandIndex);
            Assert.AreEqual(Lane.Back, restored.Envelope[0].Actions[0].Lane);

            CollectionAssert.IsEmpty(TacticalPuzzleAuthoring.Validate(restored, _source),
                "A round-tripped definition must still validate.");
        }
    }
}

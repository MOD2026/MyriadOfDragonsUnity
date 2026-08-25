using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// END-TO-END PROOF OF THE PUZZLE PIPELINE. Not shippable content.
    ///
    /// THE PUZZLE DEFINED HERE IS A THROWAWAY EXAMPLE FIXTURE. It is deliberately trivial and
    /// nothing about it is a proposal: not the objective, not the Resource, not the board. Real
    /// puzzle content is a separate design pass. If anyone later mistakes this for a template,
    /// the thing to copy is the SHAPE - define, validate, materialize, check a right line and a
    /// wrong line - never the values.
    ///
    /// WHY THIS EXISTS SEPARATELY FROM TacticalPuzzleAuthoringTests: those tests inject a fake
    /// card source, which is correct for unit-testing rules but means the REAL resolution path was
    /// never exercised. This fixture runs the pipeline through the actual CardDatabase via
    /// TacticalPuzzleAuthoring.DefaultCardSource, so "it works" covers the wiring an authored
    /// puzzle would actually use, not just the logic around it. That was the genuine gap.
    ///
    /// NO CARD ID IS HARDCODED. The example picks its cards out of the live database by structural
    /// property (single-slot, cheapest available), so a designer retuning or renaming cards cannot
    /// break this fixture - and so nothing here reads as a claim about which cards belong in a
    /// puzzle.
    /// </summary>
    public class TacticalPuzzleExamplePipelineTests
    {
        private GameObject _databaseHost;
        private Card _lightCard;

        [SetUp]
        public void SetUp()
        {
            // Claim the singleton for this fixture: the whole EditMode suite shares one process
            // and a leftover instance from another fixture would otherwise serve stale cards.
            CardDatabase.ResetForTests();
            _databaseHost = new GameObject("ExamplePuzzleCardDatabase");
            CardDatabase db = _databaseHost.AddComponent<CardDatabase>();
            db.Initialize();

            Assert.IsNotNull(CardDatabase.Instance, "Setup: the example needs a live CardDatabase singleton.");
            Assert.Greater(CardDatabase.Instance.AllCards.Count, 0,
                "Setup: the card database loaded no cards, so this fixture would prove nothing.");

            // Structural pick, not a content pick: the lightest, cheapest single-slot unit
            // available. Ordering by cost then id keeps the choice deterministic across runs.
            _lightCard = CardDatabase.Instance.AllCards
                .Where(c => c.SlotWeight == 1)
                .OrderBy(c => c.ResourceCost)
                .ThenBy(c => c.Id, System.StringComparer.Ordinal)
                .FirstOrDefault();

            Assert.IsNotNull(_lightCard, "Setup: expected at least one single-slot card in the database.");
        }

        [TearDown]
        public void TearDown()
        {
            if (_databaseHost != null) Object.DestroyImmediate(_databaseHost);
            CardDatabase.ResetForTests();
        }

        /// <summary>
        /// The example puzzle. Objective: hold the Back lane. The player starts with a full Front
        /// lane and an empty Back lane; the intended line deploys one unit into Back.
        ///
        /// Chosen because it exercises the pipeline WITHOUT depending on combat maths - the enemy
        /// has nothing in Back, so whether the objective is met turns on whether the deploy landed,
        /// not on who wins a clash. A pipeline proof that could fail because of damage tuning would
        /// be a bad pipeline proof.
        ///
        /// Resource is set far above any card's cost so that affordability never silently decides
        /// an outcome this example is not about.
        /// </summary>
        private TacticalPuzzleDefinition ExamplePuzzle()
        {
            string id = _lightCard.Id;
            return new TacticalPuzzleDefinition
            {
                PuzzleId = "example_pipeline_proof",
                DisplayName = "EXAMPLE - pipeline proof, not content",
                StartingResource = 99,
                ResourceCap = 99,
                AvatarHealth = 20,
                Hand = new List<string> { id },
                PlayerBoard = new List<TacticalPuzzleUnitSpec>
                {
                    new TacticalPuzzleUnitSpec { CardId = id, Lane = Lane.Front },
                    new TacticalPuzzleUnitSpec { CardId = id, Lane = Lane.Front },
                    new TacticalPuzzleUnitSpec { CardId = id, Lane = Lane.Front },
                },
                EnemyBoard = new List<TacticalPuzzleUnitSpec>
                {
                    new TacticalPuzzleUnitSpec { CardId = id, Lane = Lane.Front },
                },
                Objective = new TacticalPuzzleObjectiveSpec
                {
                    Kind = TacticalPuzzleObjectiveKind.ProtectLane,
                    ProtectedLane = Lane.Back,
                },
            };
        }

        private static TacticalPuzzleActionSpec DeployInto(Lane lane) => new TacticalPuzzleActionSpec
        {
            Kind = TacticalPuzzleActionKind.Deploy,
            HandIndex = 0,
            Lane = lane,
        };

        [Test]
        public void Step1_TheExampleDefinition_ValidatesThroughTheRealCardDatabase()
        {
            List<string> problems = TacticalPuzzleAuthoring.Validate(ExamplePuzzle());

            CollectionAssert.IsEmpty(problems,
                "The example puzzle must validate against the live database with no injected " +
                "source: " + string.Join("  |  ", problems));
        }

        [Test]
        public void Step2_ItMaterializesIntoTheAuthoredState()
        {
            MaterializedPuzzle puzzle = TacticalPuzzleAuthoring.Materialize(ExamplePuzzle());

            Assert.AreEqual(3, puzzle.PlayerSide.Lanes[Lane.Front].Cards.Count, "Front lane not built as authored.");
            Assert.AreEqual(0, puzzle.PlayerSide.Lanes[Lane.Back].Cards.Count, "Back lane should start empty.");
            Assert.AreEqual(1, puzzle.EnemySide.Lanes[Lane.Front].Cards.Count, "Enemy board not materialised.");
            Assert.AreEqual(1, puzzle.PlayerSide.Hand.Count, "Hand not built as authored.");
            Assert.AreEqual(0, puzzle.PlayerSide.DrawPile.Count, "A puzzle has no draw pile.");
        }

        [Test]
        public void Step3_TheKnownCorrectSolution_Solves()
        {
            TacticalPuzzleResult result = TacticalPuzzleAuthoring.Run(
                ExamplePuzzle(), new List<TacticalPuzzleActionSpec> { DeployInto(Lane.Back) });

            Assert.AreEqual(TacticalPuzzleStatus.ObjectiveMet, result.Status,
                "Deploying into the protected lane must solve this example. " + result.Message);
            Assert.IsTrue(result.Solved);
            Assert.AreEqual(1, result.ActionsUsed);
        }

        [Test]
        public void Step4_TheKnownWrongSolution_DoesNotSolve()
        {
            // Deploying into the wrong lane leaves Back empty. Same puzzle, same single action,
            // opposite outcome - which is what makes this a real check rather than a smoke test.
            TacticalPuzzleResult result = TacticalPuzzleAuthoring.Run(
                ExamplePuzzle(), new List<TacticalPuzzleActionSpec> { DeployInto(Lane.Middle) });

            Assert.AreEqual(TacticalPuzzleStatus.ObjectiveNotMet, result.Status,
                "Deploying away from the protected lane must NOT solve this example.");
            Assert.IsFalse(result.Solved);
        }

        [Test]
        public void Step5_AnImpossibleAction_IsRejectedAsIllegal_NotAsAMiss()
        {
            // The Front lane is authored full. A deploy there is not a wrong ANSWER, it is an
            // illegal MOVE, and the pipeline has to keep those distinct - a puzzle UI needs to
            // refuse the tap rather than accept it and mark the attempt failed.
            TacticalPuzzleResult result = TacticalPuzzleAuthoring.Run(
                ExamplePuzzle(), new List<TacticalPuzzleActionSpec> { DeployInto(Lane.Front) });

            Assert.AreEqual(TacticalPuzzleStatus.IllegalAction, result.Status,
                "A deploy into a full lane must be rejected as illegal. " + result.Message);
            Assert.AreEqual(0, result.FailedActionIndex, "The rejected action must be named.");
        }

        [Test]
        public void Step6_TheWholeEnvelope_ChecksOutAgainstTheRealVerifier()
        {
            // The end-to-end assertion: an author writes down what they believe, and the tooling
            // confirms all of it in one pass. This is the call that would run in CI over real
            // content once BS's design pass lands.
            TacticalPuzzleDefinition def = ExamplePuzzle();
            def.Envelope = new List<TacticalPuzzleExpectation>
            {
                new TacticalPuzzleExpectation
                {
                    Description = "intended solution - deploy into the protected lane",
                    Actions = new List<TacticalPuzzleActionSpec> { DeployInto(Lane.Back) },
                    ExpectedStatus = TacticalPuzzleStatus.ObjectiveMet,
                },
                new TacticalPuzzleExpectation
                {
                    Description = "wrong lane - leaves the objective lane empty",
                    Actions = new List<TacticalPuzzleActionSpec> { DeployInto(Lane.Middle) },
                    ExpectedStatus = TacticalPuzzleStatus.ObjectiveNotMet,
                },
                new TacticalPuzzleExpectation
                {
                    Description = "full lane - illegal move, not a wrong answer",
                    Actions = new List<TacticalPuzzleActionSpec> { DeployInto(Lane.Front) },
                    ExpectedStatus = TacticalPuzzleStatus.IllegalAction,
                    ExpectedFailedActionIndex = 0,
                },
                new TacticalPuzzleExpectation
                {
                    Description = "doing nothing does not solve it",
                    Actions = new List<TacticalPuzzleActionSpec>(),
                    ExpectedStatus = TacticalPuzzleStatus.ObjectiveNotMet,
                },
            };

            CollectionAssert.IsEmpty(TacticalPuzzleAuthoring.Validate(def),
                "The example envelope must itself be well-formed.");

            List<TacticalPuzzleEnvelopeMismatch> mismatches = TacticalPuzzleAuthoring.CheckEnvelope(def);

            CollectionAssert.IsEmpty(mismatches,
                "Every authored claim must hold against the real verifier: " +
                string.Join("  |  ", mismatches.Select(m => m.ToString())));
        }

        [Test]
        public void Step7_TheEnvelopeCheck_ActuallyDetectsAWrongClaim()
        {
            // Guards Step 6 against the trap two of my own harnesses hit today: a check that
            // passes because it measures nothing. Corrupt one claim and require it to be caught,
            // by name.
            TacticalPuzzleDefinition def = ExamplePuzzle();
            def.Envelope = new List<TacticalPuzzleExpectation>
            {
                new TacticalPuzzleExpectation
                {
                    Description = "deliberately false claim",
                    Actions = new List<TacticalPuzzleActionSpec> { DeployInto(Lane.Middle) },
                    ExpectedStatus = TacticalPuzzleStatus.ObjectiveMet,
                },
            };

            List<TacticalPuzzleEnvelopeMismatch> mismatches = TacticalPuzzleAuthoring.CheckEnvelope(def);

            Assert.AreEqual(1, mismatches.Count, "A false claim must be reported.");
            Assert.AreEqual("deliberately false claim", mismatches[0].Description,
                "The mismatch must name the expectation an author can go and fix.");
            Assert.AreEqual(TacticalPuzzleStatus.ObjectiveNotMet, mismatches[0].Actual);
        }

        [Test]
        public void Step8_TheWholePipeline_IsReproducible()
        {
            // A seeded daily puzzle is only viable if the same definition gives the same answer
            // every time, including through the real database rather than a fixed test source.
            TacticalPuzzleDefinition def = ExamplePuzzle();
            var line = new List<TacticalPuzzleActionSpec> { DeployInto(Lane.Back) };

            TacticalPuzzleResult baseline = TacticalPuzzleAuthoring.Run(def, line);
            for (int i = 0; i < 4; i++)
            {
                TacticalPuzzleResult again = TacticalPuzzleAuthoring.Run(def, line);
                Assert.AreEqual(baseline.Status, again.Status, "Run " + i + " disagreed on status.");
                Assert.AreEqual(baseline.ResourceRemaining, again.ResourceRemaining, "Run " + i + " disagreed on Resource.");
                Assert.AreEqual(baseline.FriendlyUnitsAlive, again.FriendlyUnitsAlive, "Run " + i + " disagreed on survivors.");
                Assert.AreEqual(baseline.LanesHeld, again.LanesHeld, "Run " + i + " disagreed on lanes held.");
            }
        }
    }
}

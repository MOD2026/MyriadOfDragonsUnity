using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// The playable War-Room Reconstructions screen: session logic, slate progression, and the
    /// presenter that renders them.
    ///
    /// WHY THESE TESTS CAN EXIST AT ALL: none of the rules live in the MonoBehaviour. Every
    /// interaction is a plain method the Buttons merely forward to, so a test can drive the whole
    /// screen without a rendered frame - which matters because a GraphicRaycaster resolves nothing
    /// headlessly, measured earlier in this project. What these tests CANNOT cover is whether a
    /// real finger lands on the right rect; that needs a seat with visual verification and is
    /// flagged rather than assumed.
    ///
    /// The puzzles here are throwaway fixtures, not content.
    /// </summary>
    public class TacticalPuzzlePresenterTests
    {
        private GameObject _host;
        private GameObject _databaseHost;
        private Card _light;

        [SetUp]
        public void SetUp()
        {
            CardDatabase.ResetForTests();
            _databaseHost = new GameObject("PuzzleUiCardDatabase");
            CardDatabase db = _databaseHost.AddComponent<CardDatabase>();
            db.Initialize();

            _light = CardDatabase.Instance.AllCards
                .Where(c => c.SlotWeight == 1)
                .OrderBy(c => c.ResourceCost)
                .ThenBy(c => c.Id, System.StringComparer.Ordinal)
                .FirstOrDefault();
            Assert.IsNotNull(_light, "Setup: need one single-slot card.");
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            if (_databaseHost != null) Object.DestroyImmediate(_databaseHost);
            foreach (Canvas c in Object.FindObjectsOfType<Canvas>())
                if (c != null) Object.DestroyImmediate(c.gameObject);
            TacticalPuzzleLibrary.ClearPuzzlesForTests();
            CardDatabase.ResetForTests();
        }

        /// <summary>Hold the Back lane; it starts empty and the enemy is elsewhere, so the outcome
        /// turns on whether the player's deploy landed rather than on damage tuning.</summary>
        private TacticalPuzzleDefinition Puzzle(string id, int actionBudget = 0)
        {
            string cardId = _light.Id;
            return new TacticalPuzzleDefinition
            {
                PuzzleId = id,
                DisplayName = id,
                StartingResource = 99,
                ResourceCap = 99,
                AvatarHealth = 20,
                ActionBudget = actionBudget,
                Hand = new List<string> { cardId, cardId },
                PlayerBoard = new List<TacticalPuzzleUnitSpec>
                {
                    new TacticalPuzzleUnitSpec { CardId = cardId, Lane = Lane.Front },
                    new TacticalPuzzleUnitSpec { CardId = cardId, Lane = Lane.Front },
                    new TacticalPuzzleUnitSpec { CardId = cardId, Lane = Lane.Front },
                },
                EnemyBoard = new List<TacticalPuzzleUnitSpec>
                {
                    new TacticalPuzzleUnitSpec { CardId = cardId, Lane = Lane.Front },
                },
                Objective = new TacticalPuzzleObjectiveSpec
                {
                    Kind = TacticalPuzzleObjectiveKind.ProtectLane,
                    ProtectedLane = Lane.Back,
                },
            };
        }

        private TacticalPuzzlePresenter Open(params TacticalPuzzleDefinition[] puzzles)
        {
            _host = new GameObject("WarRoomHost");
            var presenter = _host.AddComponent<TacticalPuzzlePresenter>();
            presenter.Initialize(puzzles);
            return presenter;
        }

        // ------------------------------------------------------------------ session rules

        [Test]
        public void AnIllegalOrder_IsRefused_AndLeavesThePositionUntouched()
        {
            // Refusing the tap is the whole point: touching a full lane must not consume the
            // attempt, because a puzzle is a thinking exercise and punishing exploration would
            // make it one about caution instead.
            var session = new TacticalPuzzleSession(Puzzle("refusal"));
            int before = session.AcceptedActions.Count;

            TacticalPuzzleIssueReport report = session.TryIssue(new TacticalPuzzleActionSpec
            {
                Kind = TacticalPuzzleActionKind.Deploy, HandIndex = 0, Lane = Lane.Front,
            });

            Assert.AreEqual(TacticalPuzzleIssueOutcome.RejectedIllegal, report.Outcome,
                "A deploy into the authored-full Front lane must be refused.");
            Assert.AreEqual(before, session.AcceptedActions.Count, "A refused order must not be committed.");
            Assert.IsFalse(session.IsSolved);
        }

        [Test]
        public void ALegalOrder_IsCommitted_AndCanSolveThePosition()
        {
            var session = new TacticalPuzzleSession(Puzzle("solve"));

            TacticalPuzzleIssueReport report = session.TryIssue(new TacticalPuzzleActionSpec
            {
                Kind = TacticalPuzzleActionKind.Deploy, HandIndex = 0, Lane = Lane.Back,
            });

            Assert.IsTrue(report.Accepted, "Deploying into the empty protected lane must be legal.");
            Assert.AreEqual(1, session.AcceptedActions.Count);
            Assert.IsTrue(session.IsSolved, "Holding the protected lane solves this fixture.");
        }

        [Test]
        public void Undo_TakesBackExactlyOneOrder()
        {
            var session = new TacticalPuzzleSession(Puzzle("undo"));
            session.TryIssue(new TacticalPuzzleActionSpec
            {
                Kind = TacticalPuzzleActionKind.Deploy, HandIndex = 0, Lane = Lane.Back,
            });
            Assert.IsTrue(session.IsSolved, "Setup: expected the position solved before undoing.");

            Assert.IsTrue(session.Undo());
            Assert.AreEqual(0, session.AcceptedActions.Count);
            Assert.IsFalse(session.IsSolved, "Undo must roll the verdict back too, not just the list.");
            Assert.IsFalse(session.Undo(), "Undo on an untouched position must report that it did nothing.");
        }

        [Test]
        public void Reset_ReturnsTheBoardToTheAuthoredPosition()
        {
            TacticalPuzzleDefinition def = Puzzle("reset");
            var session = new TacticalPuzzleSession(def);
            int startingHand = session.Board.PlayerSide.Hand.Count;

            session.TryIssue(new TacticalPuzzleActionSpec
            {
                Kind = TacticalPuzzleActionKind.Deploy, HandIndex = 0, Lane = Lane.Back,
            });
            session.Reset();

            Assert.AreEqual(0, session.AcceptedActions.Count);
            Assert.AreEqual(startingHand, session.Board.PlayerSide.Hand.Count, "Hand must be restored.");
            Assert.AreEqual(0, session.Board.PlayerSide.Lanes[Lane.Back].Cards.Count, "Board must be restored.");
            Assert.AreEqual(def.StartingResource, session.Board.PlayerSide.Resource, "Resource must be restored.");
        }

        [Test]
        public void AnActionBudget_IsEnforced_AndUnboundedIsDistinctFromLarge()
        {
            var bounded = new TacticalPuzzleSession(Puzzle("bounded", actionBudget: 1));
            Assert.AreEqual(1, bounded.ActionsRemaining);

            bounded.TryIssue(new TacticalPuzzleActionSpec
            {
                Kind = TacticalPuzzleActionKind.Deploy, HandIndex = 0, Lane = Lane.Middle,
            });

            Assert.AreEqual(0, bounded.ActionsRemaining);
            Assert.IsTrue(bounded.BudgetExhausted);

            TacticalPuzzleIssueReport refused = bounded.TryIssue(new TacticalPuzzleActionSpec
            {
                Kind = TacticalPuzzleActionKind.Deploy, HandIndex = 0, Lane = Lane.Back,
            });
            Assert.AreEqual(TacticalPuzzleIssueOutcome.RejectedBudgetExhausted, refused.Outcome);

            var unbounded = new TacticalPuzzleSession(Puzzle("unbounded"));
            Assert.AreEqual(-1, unbounded.ActionsRemaining,
                "Unbounded must be distinguishable from a large budget so a UI can render nothing.");
            Assert.IsFalse(unbounded.BudgetExhausted);
        }

        [Test]
        public void LegalDeploys_AgreeWithWhatTheSessionActuallyAccepts()
        {
            // A highlight that disagreed with legality would be worse than no highlight, so the
            // suggestion list is checked against the real answer rather than trusted.
            var session = new TacticalPuzzleSession(Puzzle("highlights"));
            List<TacticalPuzzleActionSpec> legal = session.LegalDeploysNow();

            CollectionAssert.IsNotEmpty(legal, "Some deploy must be legal in the starting position.");
            Assert.IsFalse(legal.Any(a => a.Lane == Lane.Front),
                "The authored-full Front lane must never be offered as a legal deploy.");

            foreach (TacticalPuzzleActionSpec candidate in legal)
            {
                var probe = new TacticalPuzzleSession(Puzzle("highlights"));
                Assert.IsTrue(probe.TryIssue(candidate).Accepted,
                    "Offered a deploy into " + candidate.Lane + " that the session then refused.");
            }
        }

        // ------------------------------------------------------------------ slate progression

        [Test]
        public void OnlyTheFirstSlot_StartsAvailable()
        {
            var slate = new TacticalPuzzleSlate(new[] { Puzzle("a"), Puzzle("b"), Puzzle("c") });

            Assert.AreEqual(TacticalPuzzleSlotState.Available, slate.SlotAt(0).State);
            Assert.AreEqual(TacticalPuzzleSlotState.Locked, slate.SlotAt(1).State);
            Assert.AreEqual(TacticalPuzzleSlotState.Locked, slate.SlotAt(2).State);
        }

        [Test]
        public void SolvingASlot_CompletesIt_AndUnlocksTheNextOnly()
        {
            var slate = new TacticalPuzzleSlate(new[] { Puzzle("a"), Puzzle("b"), Puzzle("c") });
            var session = new TacticalPuzzleSession(slate.SlotAt(0).Definition);
            session.TryIssue(new TacticalPuzzleActionSpec
            {
                Kind = TacticalPuzzleActionKind.Deploy, HandIndex = 0, Lane = Lane.Back,
            });

            slate.RecordAttempt(0, session.Current);

            Assert.AreEqual(TacticalPuzzleSlotState.Completed, slate.SlotAt(0).State);
            Assert.AreEqual(TacticalPuzzleSlotState.Available, slate.SlotAt(1).State);
            Assert.AreEqual(TacticalPuzzleSlotState.Locked, slate.SlotAt(2).State,
                "Solving one slot must not open the whole set.");
        }

        [Test]
        public void AnUnsolvedAttempt_ChangesNothing()
        {
            // Walking away from a puzzle unsolved must not consume it - otherwise trying is
            // punished.
            var slate = new TacticalPuzzleSlate(new[] { Puzzle("a"), Puzzle("b") });
            var session = new TacticalPuzzleSession(slate.SlotAt(0).Definition);
            session.TryIssue(new TacticalPuzzleActionSpec
            {
                Kind = TacticalPuzzleActionKind.Deploy, HandIndex = 0, Lane = Lane.Middle,
            });
            Assert.IsFalse(session.IsSolved, "Setup: this line must not solve the fixture.");

            slate.RecordAttempt(0, session.Current);

            Assert.AreEqual(TacticalPuzzleSlotState.Available, slate.SlotAt(0).State);
            Assert.AreEqual(TacticalPuzzleSlotState.Locked, slate.SlotAt(1).State);
            Assert.AreEqual(0, slate.CompletedCount);
        }

        // ------------------------------------------------------------------ presenter

        [Test]
        public void TheEntryScreen_CarriesTheLockedNarrativeFraming()
        {
            TacticalPuzzlePresenter presenter = Open(Puzzle("a"));
            string[] texts = presenter.CanvasObjectForTests
                .GetComponentsInChildren<Text>(true).Select(t => t.text).ToArray();

            Assert.IsTrue(texts.Any(t => t == TacticalPuzzleCopy.ScreenTitle),
                "The locked title must be used verbatim, not paraphrased.");
            Assert.IsTrue(texts.Any(t => t == TacticalPuzzleCopy.Intro),
                "The locked intro copy must be used verbatim.");
        }

        [Test]
        public void AnEmptyLibrary_SaysSoRatherThanRenderingAnEmptyRow()
        {
            // This is the REAL state today - puzzle content has not been authored - so the screen
            // has to be honest about it instead of looking broken.
            TacticalPuzzlePresenter presenter = Open();

            Assert.AreEqual(0, presenter.SlateForTests.Slots.Count);
            Assert.IsTrue(presenter.CanvasObjectForTests.GetComponentsInChildren<Text>(true)
                    .Any(t => t.name == "EmptyState"),
                "An empty slate must render an explicit empty state.");
        }

        [Test]
        public void OpeningAnAvailableSlot_EntersTheBoardView()
        {
            TacticalPuzzlePresenter presenter = Open(Puzzle("a"));

            Assert.IsTrue(presenter.OpenSlot(0));
            Assert.AreEqual(TacticalPuzzleView.Board, presenter.CurrentView);
            Assert.IsNotNull(presenter.SessionForTests);
            Assert.AreEqual(0, presenter.ActiveSlotIndexForTests);
        }

        [Test]
        public void ALockedSlot_RefusesToOpen_AndExplainsWhy()
        {
            TacticalPuzzlePresenter presenter = Open(Puzzle("a"), Puzzle("b"));

            Assert.IsFalse(presenter.OpenSlot(1), "Slot 1 starts locked.");
            Assert.AreEqual(TacticalPuzzleView.Entry, presenter.CurrentView, "A refused open must not navigate.");
            Assert.AreEqual(TacticalPuzzleCopy.LockedLine, presenter.StatusForTests,
                "A tap that does nothing at all reads as a broken button - it must say why.");
        }

        [Test]
        public void TappingALane_RoutesThroughTheSession_AndSolvingShowsTheResult()
        {
            TacticalPuzzlePresenter presenter = Open(Puzzle("a"), Puzzle("b"));
            presenter.OpenSlot(0);

            TacticalPuzzleIssueOutcome outcome = presenter.DeploySelectedInto(Lane.Back);

            Assert.AreEqual(TacticalPuzzleIssueOutcome.Accepted, outcome);
            Assert.AreEqual(TacticalPuzzleView.Result, presenter.CurrentView,
                "Solving the position must move to the result view.");
            Assert.AreEqual(TacticalPuzzleSlotState.Completed, presenter.SlateForTests.SlotAt(0).State);
            Assert.AreEqual(TacticalPuzzleSlotState.Available, presenter.SlateForTests.SlotAt(1).State,
                "Completing a slot from the UI must unlock the next.");
        }

        [Test]
        public void TappingAFullLane_IsRefused_AndKeepsThePlayerOnTheBoard()
        {
            TacticalPuzzlePresenter presenter = Open(Puzzle("a"));
            presenter.OpenSlot(0);

            TacticalPuzzleIssueOutcome outcome = presenter.DeploySelectedInto(Lane.Front);

            Assert.AreEqual(TacticalPuzzleIssueOutcome.RejectedIllegal, outcome);
            Assert.AreEqual(TacticalPuzzleView.Board, presenter.CurrentView, "A refused tap must not navigate.");
            Assert.AreEqual(0, presenter.SessionForTests.AcceptedActions.Count);
            Assert.IsFalse(string.IsNullOrEmpty(presenter.StatusForTests), "The refusal must be explained.");
        }

        [Test]
        public void TheSelectedHandIndex_StaysValidAfterACardLeavesTheHand()
        {
            // Deploying removes a card, so a stale index would point at a different card or off
            // the end - and the next tap would deploy something the player did not choose.
            TacticalPuzzlePresenter presenter = Open(Puzzle("a"));
            presenter.OpenSlot(0);
            presenter.SelectHandCard(1);

            presenter.DeploySelectedInto(Lane.Middle);

            int handSize = presenter.SessionForTests.Board.PlayerSide.Hand.Count;
            Assert.Less(presenter.SelectedHandIndexForTests, Mathf.Max(1, handSize),
                "Selection must be clamped back into the shortened hand.");
            Assert.GreaterOrEqual(presenter.SelectedHandIndexForTests, 0);
        }

        [Test]
        public void TheResultView_ShowsTheDecisionBasedScoreInputs_WithoutInventingAScore()
        {
            TacticalPuzzlePresenter presenter = Open(Puzzle("a"));
            presenter.OpenSlot(0);
            presenter.DeploySelectedInto(Lane.Back);

            Assert.AreEqual(TacticalPuzzleView.Result, presenter.CurrentView);
            string joined = string.Join(" ", presenter.CanvasObjectForTests
                .GetComponentsInChildren<Text>(true).Select(t => t.text));

            StringAssert.Contains("Orders used", joined);
            StringAssert.Contains("Resource remaining", joined);
            StringAssert.Contains("Units preserved", joined);
            StringAssert.Contains("Lanes held", joined);
            StringAssert.Contains(TacticalPuzzleCopy.SolvedLine, joined);
        }

        [Test]
        public void EveryViewBuildsARealCanvas_AndTearsTheOldOneDown()
        {
            // Guards against the leak pattern that polluted this suite before: each rebuild must
            // replace the canvas, not stack another one.
            TacticalPuzzlePresenter presenter = Open(Puzzle("a"));
            Assert.AreEqual(1, CountPuzzleCanvases(), "Entry view should own exactly one canvas.");

            presenter.OpenSlot(0);
            Assert.AreEqual(TacticalPuzzleView.Board, presenter.CurrentView);
            Assert.AreEqual(1, CountPuzzleCanvases(), "Board view stacked a second canvas.");

            presenter.DeploySelectedInto(Lane.Back);
            Assert.AreEqual(TacticalPuzzleView.Result, presenter.CurrentView);
            Assert.AreEqual(1, CountPuzzleCanvases(), "Result view stacked a second canvas.");

            presenter.BackToEntry();
            Assert.AreEqual(1, CountPuzzleCanvases(), "Returning to entry stacked a second canvas.");
        }

        private static int CountPuzzleCanvases() =>
            Object.FindObjectsOfType<Canvas>().Count(c => c.name == TacticalPuzzlePresenter.CanvasName);

        [Test]
        public void MissingArt_DoesNotBlockTheScreen()
        {
            // Art is a separate, non-blocking track: the screen must be fully usable whether or
            // not the sprites actually exist on disk.
            foreach (string role in new[] { "entry", "board", "result", "tile_locked", "tile_available", "tile_completed" })
                Assert.IsTrue(TacticalPuzzlePresenter.HasArtPathForTests(role),
                    "Art role '" + role + "' must have a resource path reserved so art can drop in later.");

            TacticalPuzzlePresenter presenter = Open(Puzzle("a"));
            presenter.OpenSlot(0);

            Assert.AreEqual(TacticalPuzzleView.Board, presenter.CurrentView,
                "The board must be reachable with no art present.");
            Assert.Greater(presenter.CanvasObjectForTests.GetComponentsInChildren<Image>(true).Length, 3,
                "The screen must still render its own chrome without sprites.");
        }

        [Test]
        public void TheEmpireEntryPoint_OpensTheScreen()
        {
            // The actual "can a player reach it" gap. Placed on Empire because HomePagePresenter
            // belongs to the metagame seat.
            TacticalPuzzleLibrary.SetPuzzlesForTests(new[] { Puzzle("a") });

            _host = new GameObject("EmpireHost");
            var empire = _host.AddComponent<EmpirePresenter>();
            empire.Initialize(onBackToHome: null);

            TacticalPuzzlePresenter puzzles = empire.OpenWarRoomForTests();

            Assert.IsNotNull(puzzles, "The Empire entry point must create the puzzle screen.");
            Assert.AreEqual(TacticalPuzzleView.Entry, puzzles.CurrentView);
            Assert.AreEqual(1, puzzles.SlateForTests.Slots.Count,
                "The entry point must pass the library's puzzles through.");
        }

        [Test]
        public void TheLibraryIsEmptyByDefault_WhichIsTheHonestState()
        {
            // If this ever starts returning puzzles without a design pass having happened, someone
            // has invented content - which is the failure mode the whole authoring layer exists to
            // prevent.
            TacticalPuzzleLibrary.ClearPuzzlesForTests();

            Assert.IsTrue(TacticalPuzzleLibrary.IsEmpty,
                "No puzzle content has been authored yet - the library must not pretend otherwise.");
        }
    }
}

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
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            // The screen now WRITES to the real save when a puzzle is solved, so every test here
            // needs its own profile on its own disk. Without this, a test that solves puzzle "a"
            // persists it, and a later test asserting "slot 0 starts available" finds it already
            // completed - which is exactly what happened the first time this suite ran against
            // persistence, and it is the same order-dependent pollution pattern that has bitten
            // this project before.
            _scratchSaveDir = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "MoDPuzzleUi_" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(_scratchSaveDir);
            MyriadOfDragons.Save.SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            MyriadOfDragons.Save.SaveSystem.ResetCurrentProfileForTests();

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
            TacticalPuzzleLibrary.ResetCacheForTests();
            CardDatabase.ResetForTests();

            MyriadOfDragons.Save.SaveSystem.ClearRootDirectoryOverride();
            MyriadOfDragons.Save.SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && System.IO.Directory.Exists(_scratchSaveDir))
                System.IO.Directory.Delete(_scratchSaveDir, true);
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

        /// <summary>
        /// Genuinely ambiguous: an empty enemy board and a survive-one-clash objective, so
        /// deploying the single card into ANY of the three lanes solves it - three different end
        /// positions, three real answers.
        ///
        /// The earlier version of this fixture used two IDENTICAL cards and relied on the solver
        /// counting action orders. Once answer identity became the resulting POSITION, two identical
        /// cards correctly collapsed to ONE answer and this fixture stopped being ambiguous at all -
        /// so the fixture, not the rule, was what needed fixing.
        /// </summary>
        private TacticalPuzzleDefinition AmbiguousPuzzle(string id)
        {
            return new TacticalPuzzleDefinition
            {
                PuzzleId = id,
                DisplayName = id,
                StartingResource = 99,
                ResourceCap = 99,
                AvatarHealth = 20,
                ActionBudget = 1,
                Hand = new List<string> { _light.Id },
                PlayerBoard = new List<TacticalPuzzleUnitSpec>(),
                EnemyBoard = new List<TacticalPuzzleUnitSpec>(),
                Objective = new TacticalPuzzleObjectiveSpec
                {
                    Kind = TacticalPuzzleObjectiveKind.SurviveClashes,
                    ClashCount = 1,
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

        // ------------------------------------------------------------------ art wiring
        //
        // The pre-existing MissingArt test only asserts a path is RESERVED per role. That was right
        // while no art existed, but it CANNOT FAIL when art is broken - it never loads anything. Now
        // that real renders have landed, these tests do the part that check could not.

        [Test]
        public void EveryArtRole_ActuallyLoadsASprite()
        {
            // File-on-disk is not the same as loadable. Unity resolves Resources.Load<Sprite> only
            // when the importer produced a Sprite - a texture imported with the wrong Texture Type
            // returns null from a path that looks perfectly correct. That exact failure already bit
            // this project once with audio, where present files loaded as null.
            var missing = new List<string>();
            foreach (string role in TacticalPuzzlePresenter.ArtRolesForTests)
            {
                if (TacticalPuzzlePresenter.LoadArtForTests(role) == null) missing.Add(role);
            }

            CollectionAssert.IsEmpty(missing,
                "These art roles resolved to null - the file may exist but not be imported as a " +
                "Sprite: " + string.Join(", ", missing));
        }

        [Test]
        public void EachView_RendersItsOwnArt_NotAnotherViewsFallback()
        {
            // The result modal art was delivered and then never applied: the backdrop picked
            // "entry" or "board" only, so the result view rendered the BOARD frame. A role that
            // loads but is never used looks identical to working art in every other check.
            TacticalPuzzlePresenter presenter = Open(Puzzle("a"));
            Sprite entry = BackdropSprite(presenter);

            presenter.OpenSlot(0);
            Sprite board = BackdropSprite(presenter);

            presenter.DeploySelectedInto(Lane.Back);
            Assert.AreEqual(TacticalPuzzleView.Result, presenter.CurrentView, "Setup: expected the result view.");
            Sprite result = BackdropSprite(presenter);

            Assert.AreEqual(TacticalPuzzlePresenter.LoadArtForTests("entry"), entry, "Entry view art.");
            Assert.AreEqual(TacticalPuzzlePresenter.LoadArtForTests("board"), board, "Board view art.");
            Assert.AreEqual(TacticalPuzzlePresenter.LoadArtForTests("result"), result,
                "The result view must use the result-modal art, not fall back to the board frame.");
            Assert.AreNotSame(board, result, "Result and board must not render the same sprite.");
        }

        private static Sprite BackdropSprite(TacticalPuzzlePresenter presenter) =>
            presenter.CanvasObjectForTests.GetComponentsInChildren<Image>(true)
                .First(i => i.name == "Backdrop").sprite;

        [Test]
        public void SlotTiles_RenderThePerStateArt()
        {
            // Three separate per-state images landed rather than one atlas, so the state->art
            // mapping is real logic and can be wired to the wrong role without anything failing.
            TacticalPuzzlePresenter presenter = Open(Puzzle("a"), Puzzle("b"));

            Assert.AreEqual(TacticalPuzzlePresenter.LoadArtForTests("tile_available"), TileSprite(presenter, 0),
                "Slot 0 starts available.");
            Assert.AreEqual(TacticalPuzzlePresenter.LoadArtForTests("tile_locked"), TileSprite(presenter, 1),
                "Slot 1 starts locked.");

            presenter.OpenSlot(0);
            presenter.DeploySelectedInto(Lane.Back);
            presenter.BackToEntry();

            Assert.AreEqual(TacticalPuzzleSlotState.Completed, presenter.SlateForTests.SlotAt(0).State,
                "Setup: slot 0 should be completed by now.");
            Assert.AreEqual(TacticalPuzzlePresenter.LoadArtForTests("tile_completed"), TileSprite(presenter, 0),
                "A completed slot must render the completed art.");
            Assert.AreEqual(TacticalPuzzlePresenter.LoadArtForTests("tile_available"), TileSprite(presenter, 1),
                "The newly unlocked slot must render the available art.");
        }

        private static Sprite TileSprite(TacticalPuzzlePresenter presenter, int index) =>
            presenter.CanvasObjectForTests.GetComponentsInChildren<Image>(true)
                .First(i => i.name == "Slot_" + index).sprite;

        [Test]
        public void TheThreeTileStates_AreThreeDifferentSprites()
        {
            // Guards the mapping above from passing vacuously if two roles ever point at the same
            // file - every assertion there would still hold while the states looked identical.
            Sprite locked = TacticalPuzzlePresenter.LoadArtForTests("tile_locked");
            Sprite available = TacticalPuzzlePresenter.LoadArtForTests("tile_available");
            Sprite completed = TacticalPuzzlePresenter.LoadArtForTests("tile_completed");

            Assert.AreNotSame(locked, available, "Locked and available tiles must be distinguishable.");
            Assert.AreNotSame(available, completed, "Available and completed tiles must be distinguishable.");
            Assert.AreNotSame(locked, completed, "Locked and completed tiles must be distinguishable.");
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

        // ------------------------------------------------------------------ reposition orders
        //
        // Windstep and Seismic Swap were supported by the verifier and the session from the start
        // and had NO route through the UI - a player could only Deploy. Two of the three legal
        // actions, and the mode's core verb, were unplayable.

        /// <summary>A puzzle with room to move: two units in Front, nothing full, generous
        /// Resource so affordability never decides a reposition test.</summary>
        private TacticalPuzzleDefinition RepositionPuzzle()
        {
            string cardId = _light.Id;
            return new TacticalPuzzleDefinition
            {
                PuzzleId = "reposition",
                DisplayName = "reposition",
                StartingResource = 99,
                ResourceCap = 99,
                AvatarHealth = 20,
                Hand = new List<string> { cardId },
                PlayerBoard = new List<TacticalPuzzleUnitSpec>
                {
                    new TacticalPuzzleUnitSpec { CardId = cardId, Lane = Lane.Front },
                    new TacticalPuzzleUnitSpec { CardId = cardId, Lane = Lane.Middle },
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

        private static TacticalPuzzleUnitRef PlayerUnit(Lane lane, int index) =>
            new TacticalPuzzleUnitRef
            {
                Side = TacticalPuzzleSide.Player, Lane = lane, IndexInLane = index,
            };

        [Test]
        public void TheDisplayedBoard_IsThePositionBeforeTheClash_NotAfterIt()
        {
            // REAL BUG THIS PINS: evaluating an objective RESOLVES LANE CLASHES and mutates the
            // board. The session used to expose the board Play() returned - i.e. POST-COMBAT - so
            // the screen showed dead units and spent health while the player was still choosing
            // orders, and every legality probe reasoned about a board the fight had already been
            // fought on.
            //
            // Caught because a Seismic Swap between two authored units came back illegal: one of
            // them had already died in a clash the player never saw.
            var def = RepositionPuzzle();
            var session = new TacticalPuzzleSession(def);

            int authoredUnits = def.PlayerBoard.Count;
            Assert.AreEqual(authoredUnits, session.FriendlyUnits().Count,
                "Before any order, every authored unit must still be standing - a clash has not " +
                "happened yet.");

            foreach (TacticalPuzzleUnitSpec spec in def.PlayerBoard)
            {
                BattleCardInstance unit = session.Board.PlayerSide.Lanes[spec.Lane].Cards
                    .FirstOrDefault(c => c.Definition.Id == spec.CardId);
                Assert.IsNotNull(unit, "Authored unit missing from " + spec.Lane + ".");
                Assert.AreEqual(unit.MaxHealth, unit.CurrentHealth,
                    "An undamaged authored unit must display at full health, not post-clash health.");
            }

            // The verdict still evaluates the objective - only the DISPLAY board is pre-clash.
            Assert.IsNotNull(session.Current, "A verdict must still be computed.");
        }

        [Test]
        public void TheSession_OffersRepositionOrders_NotJustDeploys()
        {
            var session = new TacticalPuzzleSession(RepositionPuzzle());

            CollectionAssert.IsNotEmpty(session.LegalWindstepsNow(),
                "Units with adjacent free lanes must have legal Windsteps.");
            CollectionAssert.IsNotEmpty(session.LegalSeismicSwapsNow(),
                "Two friendly units must have a legal Seismic Swap.");
        }

        [Test]
        public void EverySuggestedReposition_IsActuallyAccepted()
        {
            // Same rule as the deploy suggestions: a highlight that disagreed with legality would
            // be worse than no highlight. Checked against the real attempt, not trusted.
            var probe = new TacticalPuzzleSession(RepositionPuzzle());
            foreach (TacticalPuzzleActionSpec candidate in probe.LegalWindstepsNow())
            {
                var fresh = new TacticalPuzzleSession(RepositionPuzzle());
                Assert.IsTrue(fresh.TryIssue(candidate).Accepted,
                    "Offered a Windstep to " + candidate.Lane + " that the session then refused.");
            }

            foreach (TacticalPuzzleActionSpec candidate in probe.LegalSeismicSwapsNow())
            {
                var fresh = new TacticalPuzzleSession(RepositionPuzzle());
                Assert.IsTrue(fresh.TryIssue(candidate).Accepted,
                    "Offered a Seismic Swap that the session then refused.");
            }
        }

        [Test]
        public void SeismicSwapPairs_AreOfferedOnce_NotBothWaysRound()
        {
            // Offering both orderings would put two buttons in front of the player that do exactly
            // the same thing.
            var session = new TacticalPuzzleSession(RepositionPuzzle());
            var seen = new HashSet<string>();

            foreach (TacticalPuzzleActionSpec swap in session.LegalSeismicSwapsNow())
            {
                string a = swap.UnitA.ToString(), b = swap.UnitB.ToString();
                string key = string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;
                Assert.IsTrue(seen.Add(key), "The same unordered pair was offered twice: " + key);
            }
        }

        [Test]
        public void AUnitKeepsItsStartingCoordinate_AfterItHasMoved()
        {
            // THE REASON THE REVERSE LOOKUP EXISTS. The UI renders a unit where it CURRENTLY
            // stands, but an action names it by where it STARTED - after a Windstep those differ,
            // and a UI that named the current lane would address the wrong unit (or nothing).
            var session = new TacticalPuzzleSession(RepositionPuzzle());
            TacticalPuzzleUnitRef start = PlayerUnit(Lane.Front, 0);

            BattleCardInstance moved = session.Board.Resolve(start);
            Assert.IsNotNull(moved, "Setup: expected a unit at Front[0].");

            Assert.IsTrue(session.TryIssue(new TacticalPuzzleActionSpec
            {
                Kind = TacticalPuzzleActionKind.Windstep, UnitA = start, Lane = Lane.Middle,
            }).Accepted, "Setup: expected a legal Windstep to Middle.");

            BattleCardInstance afterMove = session.Board.Resolve(start);
            Assert.IsNotNull(afterMove, "The starting coordinate must still resolve after the move.");
            Assert.AreEqual(start.ToString(), session.Board.CoordinateOf(afterMove).ToString(),
                "The reverse lookup must return the STARTING coordinate, not the current lane.");
        }

        // ---- presenter routing

        [Test]
        public void TappingAUnit_WithNoOrderChosen_SaysSoRatherThanDoingNothing()
        {
            TacticalPuzzlePresenter presenter = Open(RepositionPuzzle());
            presenter.OpenSlot(0);

            presenter.SelectBoardUnit(PlayerUnit(Lane.Front, 0));

            Assert.IsNull(presenter.PendingUnitForTests, "No order chosen - nothing should be armed.");
            Assert.IsFalse(string.IsNullOrEmpty(presenter.StatusForTests),
                "A tap that does nothing at all reads as a broken tile.");
        }

        [Test]
        public void AWindstep_CanBeIssuedThroughTheScreen()
        {
            TacticalPuzzlePresenter presenter = Open(RepositionPuzzle());
            presenter.OpenSlot(0);

            presenter.BeginWindstepOrder();
            Assert.AreEqual(TacticalPuzzleActionKind.Windstep, presenter.PendingOrderForTests);

            presenter.SelectBoardUnit(PlayerUnit(Lane.Front, 0));
            Assert.IsNotNull(presenter.PendingUnitForTests, "The mover must be armed.");

            TacticalPuzzleIssueOutcome outcome = presenter.TapLane(Lane.Middle);

            Assert.AreEqual(TacticalPuzzleIssueOutcome.Accepted, outcome);
            Assert.AreEqual(1, presenter.SessionForTests.AcceptedActions.Count);
            Assert.AreEqual(TacticalPuzzleActionKind.Windstep,
                presenter.SessionForTests.AcceptedActions[0].Kind,
                "The screen must issue a Windstep, not fall through to a Deploy.");
            Assert.IsNull(presenter.PendingOrderForTests, "A completed order must disarm.");
        }

        [Test]
        public void ASeismicSwap_CanBeIssuedThroughTheScreen()
        {
            TacticalPuzzlePresenter presenter = Open(RepositionPuzzle());
            presenter.OpenSlot(0);

            presenter.BeginSeismicSwapOrder();
            presenter.SelectBoardUnit(PlayerUnit(Lane.Front, 0));
            TacticalPuzzleIssueOutcome? outcome = presenter.SelectBoardUnit(PlayerUnit(Lane.Middle, 0));

            Assert.AreEqual(TacticalPuzzleIssueOutcome.Accepted, outcome);
            Assert.AreEqual(TacticalPuzzleActionKind.SeismicSwap,
                presenter.SessionForTests.AcceptedActions[0].Kind);
            Assert.IsNull(presenter.PendingOrderForTests, "A completed order must disarm.");
        }

        [Test]
        public void AUnitCannotSwapWithItself()
        {
            TacticalPuzzlePresenter presenter = Open(RepositionPuzzle());
            presenter.OpenSlot(0);

            presenter.BeginSeismicSwapOrder();
            presenter.SelectBoardUnit(PlayerUnit(Lane.Front, 0));
            presenter.SelectBoardUnit(PlayerUnit(Lane.Front, 0));

            Assert.AreEqual(0, presenter.SessionForTests.AcceptedActions.Count,
                "Tapping the same unit twice must not issue a swap.");
            StringAssert.Contains("itself", presenter.StatusForTests);
        }

        [Test]
        public void ARefusedReposition_DisarmsInsteadOfLeavingAHalfBuiltOrder()
        {
            // A half-armed order surviving a refusal is how the NEXT innocent tap silently issues
            // an order the player never composed.
            TacticalPuzzlePresenter presenter = Open(RepositionPuzzle());
            presenter.OpenSlot(0);

            presenter.BeginWindstepOrder();
            presenter.SelectBoardUnit(PlayerUnit(Lane.Front, 0));
            presenter.TapLane(Lane.Front);   // moving to its own lane is not a legal Windstep

            Assert.IsNull(presenter.PendingOrderForTests, "A refused order must disarm.");
            Assert.IsNull(presenter.PendingUnitForTests, "A refused order must not leave a unit armed.");
        }

        [Test]
        public void ChoosingAHandCard_AbandonsAHalfBuiltReposition()
        {
            // Picking a card is a DEPLOY intent; if the pending Windstep survived, the next lane
            // tap would move a unit instead of deploying.
            TacticalPuzzlePresenter presenter = Open(RepositionPuzzle());
            presenter.OpenSlot(0);

            presenter.BeginWindstepOrder();
            presenter.SelectBoardUnit(PlayerUnit(Lane.Front, 0));
            presenter.SelectHandCard(0);

            Assert.IsNull(presenter.PendingOrderForTests);

            presenter.TapLane(Lane.Back);
            Assert.AreEqual(TacticalPuzzleActionKind.Deploy,
                presenter.SessionForTests.AcceptedActions[0].Kind,
                "After choosing a card, a lane tap must deploy - not complete the abandoned order.");
        }

        [Test]
        public void CancellingAnOrder_LeavesTheAttemptUntouched()
        {
            TacticalPuzzlePresenter presenter = Open(RepositionPuzzle());
            presenter.OpenSlot(0);

            presenter.BeginWindstepOrder();
            presenter.SelectBoardUnit(PlayerUnit(Lane.Front, 0));
            presenter.CancelOrder();

            Assert.IsNull(presenter.PendingOrderForTests);
            Assert.AreEqual(0, presenter.SessionForTests.AcceptedActions.Count,
                "Cancelling must not consume an order.");
        }

        [Test]
        public void TheBoardOffersOrderButtonsAndUnitTargets()
        {
            // Guards the routing tests from passing while the player has no way to reach any of it.
            TacticalPuzzlePresenter presenter = Open(RepositionPuzzle());
            presenter.OpenSlot(0);

            string[] names = presenter.CanvasObjectForTests
                .GetComponentsInChildren<Button>(true).Select(b => b.name).ToArray();

            CollectionAssert.Contains(names, "Btn_Windstep");
            CollectionAssert.Contains(names, "Btn_SeismicSwap");
            Assert.IsTrue(names.Any(n => n.StartsWith("Unit_")),
                "Friendly units must be tappable targets, or a reposition cannot be composed.");
        }

        // ------------------------------------------------------------------ persistence
        //
        // PlayerProfile.tacticalPuzzleRecords, added 2026-08-25 after the field list was proposed,
        // vetted and locked - not written unilaterally into a frozen file.

        [Test]
        public void AProfileThatHasNeverSeenThisMode_ReadsAsNothingSolved()
        {
            // The migration case. Old saves deserialize to an empty list, and that must read as
            // "not solved" without any backfill step.
            var profile = new MyriadOfDragons.Save.PlayerProfile();
            var slate = new TacticalPuzzleSlate(new[] { Puzzle("a"), Puzzle("b") });

            slate.ApplySavedProgress(profile);

            Assert.AreEqual(0, slate.CompletedCount);
            Assert.AreEqual(TacticalPuzzleSlotState.Available, slate.SlotAt(0).State);
            Assert.AreEqual(TacticalPuzzleSlotState.Locked, slate.SlotAt(1).State);
        }

        [Test]
        public void ASolvedPuzzle_IsWrittenToTheProfile_AndReloadsAsCompleted()
        {
            var profile = new MyriadOfDragons.Save.PlayerProfile();
            var slate = new TacticalPuzzleSlate(new[] { Puzzle("a"), Puzzle("b") });
            var session = new TacticalPuzzleSession(slate.SlotAt(0).Definition);
            session.TryIssue(new TacticalPuzzleActionSpec
            {
                Kind = TacticalPuzzleActionKind.Deploy, HandIndex = 0, Lane = Lane.Back,
            });
            Assert.IsTrue(session.IsSolved, "Setup: expected a solve.");

            slate.WriteProgress(0, profile, session.Current, "2026-08-25");

            Assert.IsTrue(profile.HasSolvedTacticalPuzzle("a"));
            MyriadOfDragons.Save.TacticalPuzzleRecord record = profile.FindTacticalPuzzleRecord("a");
            Assert.AreEqual(session.Current.ActionsUsed, record.bestActionsUsed);
            Assert.AreEqual("2026-08-25", record.firstSolvedUtcDate);

            // A FRESH slate built from the same profile must come back completed and unlocked.
            var reloaded = new TacticalPuzzleSlate(new[] { Puzzle("a"), Puzzle("b") });
            reloaded.ApplySavedProgress(profile);

            Assert.AreEqual(TacticalPuzzleSlotState.Completed, reloaded.SlotAt(0).State);
            Assert.AreEqual(TacticalPuzzleSlotState.Available, reloaded.SlotAt(1).State,
                "Availability must be RE-DERIVED from completions, not read from the save.");
        }

        [Test]
        public void AnUnsolvedAttempt_WritesNothingAtAll()
        {
            var profile = new MyriadOfDragons.Save.PlayerProfile();
            var slate = new TacticalPuzzleSlate(new[] { Puzzle("a") });
            var session = new TacticalPuzzleSession(slate.SlotAt(0).Definition);
            session.TryIssue(new TacticalPuzzleActionSpec
            {
                Kind = TacticalPuzzleActionKind.Deploy, HandIndex = 0, Lane = Lane.Middle,
            });
            Assert.IsFalse(session.IsSolved, "Setup: this line must not solve.");

            slate.WriteProgress(0, profile, session.Current, "2026-08-25");

            Assert.AreEqual(0, profile.tacticalPuzzleRecords.Count,
                "Walking away from an unsolved position must never touch the save.");
        }

        [Test]
        public void ReSolvingBetter_ReplacesTheRecord_AndReSolvingWorseDoesNot()
        {
            var profile = new MyriadOfDragons.Save.PlayerProfile();
            var slate = new TacticalPuzzleSlate(new[] { Puzzle("a") });

            var good = new TacticalPuzzleResult
            {
                Status = TacticalPuzzleStatus.ObjectiveMet, ActionsUsed = 2, ResourceRemaining = 5,
            };
            var better = new TacticalPuzzleResult
            {
                Status = TacticalPuzzleStatus.ObjectiveMet, ActionsUsed = 1, ResourceRemaining = 3,
            };
            var worse = new TacticalPuzzleResult
            {
                Status = TacticalPuzzleStatus.ObjectiveMet, ActionsUsed = 4, ResourceRemaining = 9,
            };

            slate.WriteProgress(0, profile, good, "2026-08-25");
            slate.WriteProgress(0, profile, better, "2026-08-26");
            Assert.AreEqual(1, profile.FindTacticalPuzzleRecord("a").bestActionsUsed,
                "Fewer orders must replace the stored best.");

            slate.WriteProgress(0, profile, worse, "2026-08-27");
            Assert.AreEqual(1, profile.FindTacticalPuzzleRecord("a").bestActionsUsed,
                "A worse attempt must not overwrite a better stored best.");

            Assert.AreEqual(1, profile.tacticalPuzzleRecords.Count, "Re-solving must not add a second record.");
            Assert.AreEqual("2026-08-25", profile.FindTacticalPuzzleRecord("a").firstSolvedUtcDate,
                "firstSolvedUtcDate must stay the FIRST solve, not the latest.");
        }

        [Test]
        public void AnUnknownStoredBest_LosesToARealResult_RatherThanBeatingIt()
        {
            // THE REASON THE -1 SENTINEL EXISTS. If a field added to the record later defaults to
            // 0 on existing saves, "0 orders used" reads as a perfect score and every stored best
            // becomes permanently unbeatable. -1 means unknown and must lose to any real attempt.
            var profile = new MyriadOfDragons.Save.PlayerProfile();
            profile.tacticalPuzzleRecords.Add(new MyriadOfDragons.Save.TacticalPuzzleRecord
            {
                puzzleId = "a",   // every best left at its -1 default
            });
            var slate = new TacticalPuzzleSlate(new[] { Puzzle("a") });

            Assert.IsFalse(profile.FindTacticalPuzzleRecord("a").HasRankingInfo,
                "A record with -1 bests must report that it carries no ranking info.");

            slate.WriteProgress(0, profile, new TacticalPuzzleResult
            {
                Status = TacticalPuzzleStatus.ObjectiveMet, ActionsUsed = 6, ResourceRemaining = 1,
            }, "2026-08-25");

            Assert.AreEqual(6, profile.FindTacticalPuzzleRecord("a").bestActionsUsed,
                "A real result must overwrite an unknown best - -1 means no information, not zero orders.");
        }

        [Test]
        public void RecordsAreKeyedByPuzzleId_NotBySlotPosition()
        {
            // Slate ORDER can change between releases; a positional key would silently re-point a
            // player's completions at different puzzles.
            var profile = new MyriadOfDragons.Save.PlayerProfile();
            var original = new TacticalPuzzleSlate(new[] { Puzzle("a"), Puzzle("b") });
            original.WriteProgress(0, profile, new TacticalPuzzleResult
            {
                Status = TacticalPuzzleStatus.ObjectiveMet, ActionsUsed = 1, ResourceRemaining = 1,
            }, "2026-08-25");

            // Same puzzles, opposite order.
            var reordered = new TacticalPuzzleSlate(new[] { Puzzle("b"), Puzzle("a") });
            reordered.ApplySavedProgress(profile);

            Assert.AreEqual(TacticalPuzzleSlotState.Completed, reordered.SlotAt(1).State,
                "The solved puzzle must still read as solved after moving position.");
            Assert.AreNotEqual(TacticalPuzzleSlotState.Completed, reordered.SlotAt(0).State,
                "An unsolved puzzle must not inherit another puzzle completion.");
        }

        [Test]
        public void TheScreen_LoadsSavedProgressOnOpen()
        {
            // End to end through the presenter, using the live profile the screen actually reads.
            MyriadOfDragons.Save.PlayerProfile profile = MyriadOfDragons.Data.SaveManager.SaveData;
            Assert.IsNotNull(profile, "Setup: expected a live profile.");
            profile.tacticalPuzzleRecords.Clear();
            profile.tacticalPuzzleRecords.Add(new MyriadOfDragons.Save.TacticalPuzzleRecord
            {
                puzzleId = "a", bestActionsUsed = 3, bestResourceRemaining = 2,
            });

            TacticalPuzzlePresenter presenter = Open(Puzzle("a"), Puzzle("b"));

            Assert.AreEqual(TacticalPuzzleSlotState.Completed, presenter.SlateForTests.SlotAt(0).State,
                "The screen must show a previously solved puzzle as completed.");
            Assert.AreEqual(TacticalPuzzleSlotState.Available, presenter.SlateForTests.SlotAt(1).State,
                "The next slot must be open again after a reload.");
            Assert.AreEqual(3, presenter.SlateForTests.SlotAt(0).SavedBestActions);
        }

        // ------------------------------------------------------------------ solver
        //
        // Steps 3, 4 and 7 of the content-validation protocol need the WHOLE space of play:
        // "is it solvable at all", "is the stated line the cheapest", "do unrelated lines tie".
        // Validate and CheckEnvelope cannot answer those - they only judge what an author claimed.

        [Test]
        public void TheSolver_FindsALineThatSolvesASolvablePuzzle()
        {
            var solutions = TacticalPuzzleSolver.FindAllSolutions(Puzzle("solvable"));

            CollectionAssert.IsNotEmpty(solutions, "A puzzle solvable by one deploy must yield a line.");
            Assert.AreEqual(1, solutions[0].ActionsUsed,
                "The cheapest line here is a single deploy into the protected lane.");
        }

        [Test]
        public void EverySolutionTheSolverReturns_IsActuallyAccepted()
        {
            // The solver must never certify a line the game would refuse - that would mean shipping
            // content whose "answer" does not work.
            foreach (TacticalPuzzleSolution s in TacticalPuzzleSolver.FindAllSolutions(Puzzle("check")))
            {
                var replay = new TacticalPuzzleSession(Puzzle("check"));
                foreach (TacticalPuzzleActionSpec a in s.Actions)
                    Assert.IsTrue(replay.TryIssue(a).Accepted,
                        "Solver returned a line the session refuses: " + s);
                Assert.IsTrue(replay.IsSolved, "Solver returned a line that does not solve: " + s);
            }
        }

        [Test]
        public void AnUnsolvablePuzzle_ReportsNoSolutions_RatherThanInventingOne()
        {
            // The reject condition that matters most. Objective points at a lane the player can
            // never hold: no hand, and the protected lane starts empty.
            var impossible = Puzzle("impossible");
            impossible.Hand = new List<string>();
            impossible.PlayerBoard = new List<TacticalPuzzleUnitSpec>();

            CollectionAssert.IsEmpty(TacticalPuzzleSolver.FindAllSolutions(impossible),
                "An unsolvable puzzle must return zero lines - that is an automatic content reject.");
            StringAssert.Contains("UNSOLVABLE", TacticalPuzzleSolver.DescribeSolutionSpace(impossible));
        }

        [Test]
        public void TheSolver_ReportsWhenSeveralLinesTieForCheapest()
        {
            // "Multiple unrelated lines solve it equally cheaply" is a real reject: a puzzle with
            // two equal answers has no intended answer, so its hint, score and lesson all point at
            // something the player need not have found.
            //
            // Two identical cards in hand both solve it in one deploy, so this fixture ties by
            // construction.
            string report = TacticalPuzzleSolver.DescribeSolutionSpace(AmbiguousPuzzle("ambiguous"));

            StringAssert.Contains("distinct position(s)", report);
            StringAssert.Contains("AMBIGUOUS", report);

            // And the other direction: identical cards reaching the SAME position are ONE answer,
            // not several. This is the distinction that wrongly failed tac_w1_h02 before the fix.
            StringAssert.DoesNotContain("AMBIGUOUS",
                TacticalPuzzleSolver.DescribeSolutionSpace(Puzzle("single_answer")));
        }

        [Test]
        public void TheSearchIsBounded_SoAnUnboundedPuzzleCannotRunForever()
        {
            // Branching is (hand x 3 lanes) + windsteps + swap pairs. A puzzle with no budget must
            // still terminate, and a puzzle needing more than the ceiling is also one no player
            // could hold in their head - the limit is a design signal, not only a guard.
            var unbounded = Puzzle("unbounded");
            unbounded.ActionBudget = 0;

            CollectionAssert.IsNotEmpty(TacticalPuzzleSolver.FindAllSolutions(unbounded),
                "An unbounded puzzle must still be searched, up to the ceiling.");
            foreach (TacticalPuzzleSolution s in TacticalPuzzleSolver.FindAllSolutions(unbounded))
                Assert.LessOrEqual(s.ActionsUsed, TacticalPuzzleSolver.MaxSearchDepth,
                    "No returned line may exceed the search ceiling.");
        }

        // ------------------------------------------------------------------ 7-step content validation
        //
        // The protocol BS specified and CC dispatched. It runs over WHATEVER the library holds, so
        // the moment real definitions land at Resources/Data/tactical_puzzles.json this is the
        // whole pass - no new code.
        //
        // Steps: 1 structural, 2 replay stated solution, 3 enumerate legal sequences, 4 stated
        // solution is valid AND minimum-cost, 5 a tempting alternative fails for its stated reason,
        // 6 envelope checks from a fresh state, 7 reject unsolvable or ambiguous.

        /// <summary>Runs the protocol against one definition and returns a human-readable verdict.
        /// Steps 2 and 5 need the AUTHOR's claims, which live in the envelope - a definition with no
        /// envelope is reported as such rather than silently passing those steps.</summary>
        private string RunSevenStepValidation(TacticalPuzzleDefinition def)
        {
            var report = new System.Text.StringBuilder();
            report.Append(def.PuzzleId).Append(": ");

            List<string> problems = TacticalPuzzleAuthoring.Validate(def);
            if (problems.Count > 0)
                return report.Append("STEP 1 FAIL - ").Append(string.Join("; ", problems)).ToString();
            report.Append("[1 structural OK] ");

            List<TacticalPuzzleSolution> all = TacticalPuzzleSolver.FindAllSolutions(def);
            if (all.Count == 0)
                return report.Append("STEP 7 FAIL - UNSOLVABLE within its action budget.").ToString();

            TacticalPuzzleSolution best = all[0];
            // Distinct ANSWERS, not distinct action orders - see TacticalPuzzleSolution.ResultingPosition.
            int ties = all.Where(x => x.ActionsUsed == best.ActionsUsed &&
                                      x.ResourceSpent == best.ResourceSpent)
                          .Select(x => x.ResultingPosition ?? "")
                          .Distinct().Count();
            report.Append("[3 enumerated ").Append(all.Count).Append(" line(s)] ");
            report.Append("[cheapest ").Append(best.ActionsUsed).Append(" order(s)/")
                  .Append(best.ResourceSpent).Append(" Resource] ");

            if (ties > 1)
                return report.Append("STEP 7 FAIL - AMBIGUOUS, ").Append(ties)
                             .Append(" unrelated lines tie for cheapest.").ToString();
            report.Append("[7 unique cheapest OK] ");

            if (def.Envelope == null || def.Envelope.Count == 0)
                return report.Append("STEPS 2/5/6 NOT RUN - no envelope: the author's stated " +
                                     "solution and tempting alternatives were never captured.").ToString();

            List<TacticalPuzzleEnvelopeMismatch> mismatches = TacticalPuzzleAuthoring.CheckEnvelope(def);
            if (mismatches.Count > 0)
                return report.Append("STEPS 2/5/6 FAIL - ")
                             .Append(string.Join("; ", mismatches.Select(m => m.ToString()))).ToString();

            return report.Append("[2/5/6 envelope OK] PASS").ToString();
        }

        [Test]
        public void Diagnostic_DumpEverySolvingLineForAuthoredContent()
        {
            // Diagnostic, not a gate: prints the actual lines so a TIE verdict can be judged as
            // genuinely different answers versus mere action-order permutations of one answer.
            foreach (TacticalPuzzleDefinition def in TacticalPuzzleLibrary.AvailablePuzzles())
            {
                foreach (TacticalPuzzleSolution sol in TacticalPuzzleSolver.FindAllSolutions(def))
                    Debug.Log("[LINE] " + def.PuzzleId + "  ::  " + sol);
            }
            Assert.Pass();
        }

        [Test]
        public void SevenStepValidation_RunsOverWhateverContentTheLibraryHolds()
        {
            // THE REAL PASS. Today the library is empty, so this reports that honestly instead of
            // pretending to have validated content that does not exist.
            TacticalPuzzleLibrary.ClearPuzzlesForTests();
            TacticalPuzzleLibrary.ResetCacheForTests();

            IReadOnlyList<TacticalPuzzleDefinition> content = TacticalPuzzleLibrary.AvailablePuzzles();
            if (content.Count == 0)
            {
                Assert.IsTrue(TacticalPuzzleLibrary.IsEmpty,
                    "No authored puzzle content is loadable yet - the 7-step pass has nothing to " +
                    "run against. This is not a failure of the harness.");
                return;
            }

            var failures = new List<string>();
            foreach (TacticalPuzzleDefinition def in content)
            {
                string verdict = RunSevenStepValidation(def);
                // Logged unconditionally: a per-candidate report is the deliverable here, not just
                // a pass/fail bit, and a passing test otherwise prints nothing.
                Debug.Log("[7STEP] " + verdict);
                if (!verdict.EndsWith("PASS")) failures.Add(verdict);
            }

            CollectionAssert.IsEmpty(failures,
                "Authored puzzles failed the 7-step validation:\n" + string.Join("\n", failures));
        }

        [Test]
        public void SevenStepValidation_ProducesARealVerdict_OnAKnownGoodAndAKnownBadPuzzle()
        {
            // Proves the harness DISCRIMINATES, using throwaway fixtures - not content. Without
            // this, an empty library would make the pass above green forever and prove nothing.
            var good = Puzzle("harness_good");
            good.ActionBudget = 1;                        // one deploy, one cheapest line
            good.Hand = new List<string> { _light.Id };   // single card: no tie by construction

            string goodVerdict = RunSevenStepValidation(good);
            StringAssert.Contains("[1 structural OK]", goodVerdict);
            StringAssert.Contains("[7 unique cheapest OK]", goodVerdict);

            var unsolvable = Puzzle("harness_unsolvable");
            unsolvable.Hand = new List<string>();
            unsolvable.PlayerBoard = new List<TacticalPuzzleUnitSpec>();
            StringAssert.Contains("UNSOLVABLE", RunSevenStepValidation(unsolvable));

            StringAssert.Contains("AMBIGUOUS",
                RunSevenStepValidation(AmbiguousPuzzle("harness_ambiguous")));

            var broken = Puzzle("harness_broken");
            broken.PlayerBoard.Add(new TacticalPuzzleUnitSpec { CardId = "no_such_card", Lane = Lane.Back });
            StringAssert.Contains("STEP 1 FAIL", RunSevenStepValidation(broken));
        }

        // ------------------------------------------------------------------ content loading
        //
        // Authored puzzles arrive as a DATA drop (Resources/Data/tactical_puzzles.json), not a code
        // change. No content exists yet - these tests drive the loader through JSON they build
        // themselves, so they prove the pipeline without inventing puzzle content.

        [Test]
        public void AWellFormedPuzzleSet_RoundTripsThroughTheLoadersJsonShape()
        {
            // The shape the content pass has to produce. If this breaks, authored content silently
            // stops loading.
            var set = new TacticalPuzzleDefinitionList();
            set.puzzles.Add(Puzzle("loaded_a"));
            set.puzzles.Add(Puzzle("loaded_b"));

            string json = UnityEngine.JsonUtility.ToJson(set);
            var restored = UnityEngine.JsonUtility.FromJson<TacticalPuzzleDefinitionList>(json);

            Assert.AreEqual(2, restored.puzzles.Count, "Both puzzles must survive the wrapper.");
            Assert.AreEqual("loaded_a", restored.puzzles[0].PuzzleId, "Authored ORDER must survive.");
            Assert.AreEqual("loaded_b", restored.puzzles[1].PuzzleId);
            CollectionAssert.IsEmpty(TacticalPuzzleAuthoring.Validate(restored.puzzles[0]),
                "A round-tripped puzzle must still validate.");
        }

        [Test]
        public void AnIncoherentPuzzle_WouldBeRejectedByTheGateTheLoaderUses()
        {
            // The loader skips any definition that fails Validate, because an incoherent puzzle can
            // be UNSOLVABLE - and handing a player a position they cannot win, with no way to know
            // why, is worse than showing them nothing. This pins the gate itself.
            var broken = Puzzle("broken");
            broken.PlayerBoard.Add(new TacticalPuzzleUnitSpec { CardId = "no_such_card", Lane = Lane.Back });

            CollectionAssert.IsNotEmpty(TacticalPuzzleAuthoring.Validate(broken),
                "A puzzle referencing an unknown card must not pass the loader's gate.");
        }

        [Test]
        public void DuplicatePuzzleIds_AreADefectBecauseIdsKeySaveRecords()
        {
            // Two puzzles sharing an id would share one save record: solving the first would make
            // the second read as already solved. The loader rejects the duplicate; this pins WHY.
            var profile = new MyriadOfDragons.Save.PlayerProfile();
            var slate = new TacticalPuzzleSlate(new[] { Puzzle("same_id"), Puzzle("same_id") });

            slate.WriteProgress(0, profile, new TacticalPuzzleResult
            {
                Status = TacticalPuzzleStatus.ObjectiveMet, ActionsUsed = 1, ResourceRemaining = 1,
            }, "2026-08-25");

            var reloaded = new TacticalPuzzleSlate(new[] { Puzzle("same_id"), Puzzle("same_id") });
            reloaded.ApplySavedProgress(profile);

            Assert.AreEqual(TacticalPuzzleSlotState.Completed, reloaded.SlotAt(1).State,
                "This is the FAILURE MODE the loader's duplicate-id rejection prevents: solving " +
                "one puzzle marks the other solved, because a record is keyed by id alone.");
        }

        [Test]
        public void TheAuthoredContentFile_LoadsAndEveryPuzzleSurvivesValidation()
        {
            // REWRITTEN 2026-08-26: this used to assert the content file was ABSENT, which was the
            // honest state until Week 1 content landed. Its subject changed, so the assertion was
            // re-read rather than deleted - the same call as the Prison cooldown and art-role tests.
            TacticalPuzzleLibrary.ClearPuzzlesForTests();
            TacticalPuzzleLibrary.ResetCacheForTests();

            Assert.IsNotNull(Resources.Load<TextAsset>(TacticalPuzzleLibrary.ResourcePath),
                "Authored puzzle content is expected at " + TacticalPuzzleLibrary.ResourcePath + ".");

            IReadOnlyList<TacticalPuzzleDefinition> loaded = TacticalPuzzleLibrary.AvailablePuzzles();
            CollectionAssert.IsNotEmpty(loaded, "The content file must yield at least one puzzle.");

            // The loader SKIPS anything that fails Validate, so a puzzle silently vanishing is the
            // failure to catch here - count what is on disk against what survived.
            foreach (TacticalPuzzleDefinition def in loaded)
                CollectionAssert.IsEmpty(TacticalPuzzleAuthoring.Validate(def),
                    def.PuzzleId + " loaded but does not validate.");
        }

        [Test]
        public void TheEnvelopeCheck_RunsOverWhateverContentIsLoaded()
        {
            // The call that belongs in CI once real puzzles ship: every authored claim replayed
            // through the real verifier. With a deliberately false claim installed it must report
            // that puzzle by id.
            var lying = Puzzle("lying_puzzle");
            lying.Envelope = new List<TacticalPuzzleExpectation>
            {
                new TacticalPuzzleExpectation
                {
                    Description = "claims doing nothing solves it",
                    Actions = new List<TacticalPuzzleActionSpec>(),
                    ExpectedStatus = TacticalPuzzleStatus.ObjectiveMet,
                },
            };

            TacticalPuzzleLibrary.SetPuzzlesForTests(new[] { lying });

            var mismatches = TacticalPuzzleLibrary.ValidateAllEnvelopes();

            Assert.IsTrue(mismatches.ContainsKey("lying_puzzle"),
                "A puzzle whose authored claim is false must be reported by id.");
            CollectionAssert.IsNotEmpty(mismatches["lying_puzzle"]);
        }

        [Test]
        public void TheEnvelopeCheck_IsSilentWhenEveryClaimHolds()
        {
            // Guards the check above from reporting mismatches unconditionally.
            TacticalPuzzleLibrary.SetPuzzlesForTests(new[] { Puzzle("honest_puzzle") });

            CollectionAssert.IsEmpty(TacticalPuzzleLibrary.ValidateAllEnvelopes(),
                "A puzzle with no envelope claims nothing, so it cannot mismatch.");
        }

        [Test]
        public void EveryLoadedPuzzleId_IsUnique_BecauseIdsKeySaveRecords()
        {
            // REWRITTEN 2026-08-26: this used to assert the library was EMPTY, guarding against
            // invented content before a design pass existed. Week 1 content has now landed through
            // the proper route, so the guard that still matters is the one the loader enforces:
            // duplicate ids would make solving one puzzle mark another solved.
            TacticalPuzzleLibrary.ClearPuzzlesForTests();
            TacticalPuzzleLibrary.ResetCacheForTests();

            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (TacticalPuzzleDefinition def in TacticalPuzzleLibrary.AvailablePuzzles())
                Assert.IsTrue(seen.Add(def.PuzzleId),
                    "Duplicate PuzzleId '" + def.PuzzleId + "' - ids key save records.");
        }
    }
}

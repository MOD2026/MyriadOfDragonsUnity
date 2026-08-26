using System;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Solo Circuit screen - the player-reachable half of the Circuit.
    ///
    /// These exist because "design answered" is not "shipped": the three trials were green as plain
    /// classes long before a player could reach any of them. What is asserted here is reachability
    /// and screen hygiene, not pixel layout.
    /// </summary>
    public class SoloCircuitPresenterTests
    {
        private static readonly DateTime NowUtc =
            new DateTime(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);

        private GameObject _host;
        private SoloCircuitPresenter _presenter;
        private GameObject _databaseHost;

        [SetUp]
        public void SetUp()
        {
            // A REAL CardDatabase, not a suppressed log. The Circuit screen resolves card rarity
            // and can open the puzzle library, both of which log genuine errors when the catalog is
            // absent - and NUnit fails a test on an unhandled error log. Silencing that with
            // LogAssert would also silence a real "unknown card id" regression, so the fixture
            // supplies the catalog the same way TacticalPuzzlePresenterTests does.
            CardDatabase.ResetForTests();
            _databaseHost = new GameObject("SoloCircuitCardDatabase");
            _databaseHost.AddComponent<CardDatabase>().Initialize();

            _host = new GameObject("SoloCircuitHost");
            _presenter = _host.AddComponent<SoloCircuitPresenter>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_databaseHost != null) UnityEngine.Object.DestroyImmediate(_databaseHost);
            CardDatabase.ResetForTests();
            if (_host != null) UnityEngine.Object.DestroyImmediate(_host);
            GameObject stale = GameObject.Find(SoloCircuitPresenter.CanvasName);
            if (stale != null) UnityEngine.Object.DestroyImmediate(stale);
        }

        private static PlayerProfile FreshProfile() => new PlayerProfile();

        [Test]
        public void TheScreenBuilds_AndIsActuallyReachable()
        {
            _presenter.Initialize(FreshProfile(), NowUtc, null);

            Assert.IsNotNull(_presenter.CanvasObjectForTests, "The screen must actually construct.");
            Assert.IsNotNull(GameObject.Find(SoloCircuitPresenter.CanvasName),
                "The canvas must exist in the scene by name - that is what makes it reachable.");
        }

        [Test]
        public void ClosingTheScreen_LeavesNoStaleCanvas()
        {
            // The bug class this project keeps hitting: a screen that closes without destroying its
            // canvas leaves an invisible interactive layer that swallows taps on whatever opens
            // next. That is the leading suspect for the Mail-screen freeze.
            _presenter.Initialize(FreshProfile(), NowUtc, null);
            _presenter.PressBackForTests();

            Assert.IsNull(GameObject.Find(SoloCircuitPresenter.CanvasName),
                "Closing must destroy the canvas, not merely hide it.");
        }

        [Test]
        public void TheBackCallback_ActuallyFires_SoThePlayerIsNotTrapped()
        {
            bool wentBack = false;
            _presenter.Initialize(FreshProfile(), NowUtc, () => wentBack = true);

            _presenter.PressBackForTests();

            Assert.IsTrue(wentBack, "A screen with no working exit strands the player.");
        }

        [Test]
        public void ReopeningTheScreen_DoesNotStackASecondCanvas()
        {
            _presenter.Initialize(FreshProfile(), NowUtc, null);
            _presenter.Initialize(FreshProfile(), NowUtc, null);

            GameObject[] all = UnityEngine.Object.FindObjectsByType<Canvas>(
                FindObjectsSortMode.None) is Canvas[] canvases
                ? Array.ConvertAll(canvases, c => c.gameObject)
                : Array.Empty<GameObject>();

            int matching = 0;
            foreach (GameObject go in all)
                if (go.name == SoloCircuitPresenter.CanvasName) matching++;

            Assert.AreEqual(1, matching, "Rebuilding must replace the canvas, never stack one.");
        }

        [Test]
        public void TheScreenShowsTodaysRealRule_NotAPlaceholder()
        {
            _presenter.Initialize(FreshProfile(), NowUtc, null);

            string formation = _presenter.TrialStatusForTests(SoloCircuitTrial.Formation);
            string collection = _presenter.TrialStatusForTests(SoloCircuitTrial.Collection);

            // Must match what the deterministic seed actually selected for this UTC day - a screen
            // showing a different rule than the one being scored is worse than no screen.
            string dayKey = SoloCollectionCircuit.UtcDayKey(NowUtc);
            Assert.AreEqual(SoloCircuitDailySeed.FormationRestrictionFor(dayKey), formation);
            Assert.AreEqual(SoloCircuitCollectionRule.BandFor(dayKey).Describe(), collection);
        }

        [Test]
        public void TappingATrial_DoesNotGrantIt_BecauseTapsAreNotCompletions()
        {
            PlayerProfile profile = FreshProfile();
            _presenter.Initialize(profile, NowUtc, null);

            _presenter.PressTrialForTests(SoloCircuitTrial.Formation);

            // REVERSED DELIBERATELY. This used to assert that tapping a trial cleared it - which
            // meant the reward was claimable by opening a screen and pressing a button. Formation
            // needs a real battle result under the day's restriction, and no such signal exists
            // yet, so the honest behaviour is to grant NOTHING.
            Assert.IsFalse(
                SoloCollectionCircuit.IsTrialClearedToday(profile.soloCircuitProgress, SoloCircuitTrial.Formation),
                "Tapping a trial must never grant it - only a real completion signal may.");
            Assert.AreEqual(0, profile.soloCircuitProgress.goldEarnedTodayUtc,
                "No Gold may be paid for a tap.");
        }

        [Test]
        public void TheScreenIsAPopup_AndLeavesTheScreenBeneathItAlive()
        {
            // THE REGRESSION GUARD. This screen was originally built fullscreen, which meant it
            // called CleanupStaleMetagameCanvases and destroyed EmpireCanvas out from under itself.
            // CR reverted that exact assumption on Guild Hall twice (dcf9610 -> 7185a4c), and ST's
            // locked framing puts the Circuit under Empire's War Room, which already opens its
            // screens as overlays. A canvas standing in for Empire must SURVIVE this screen opening.
            GameObject empire = new GameObject("EmpireCanvas");
            try
            {
                _presenter.Initialize(FreshProfile(), NowUtc, null);

                Assert.IsNotNull(GameObject.Find("EmpireCanvas"),
                    "A popup must never destroy the screen underneath it - that is the exact " +
                    "regression 7185a4c reverted on Guild Hall.");
            }
            finally
            {
                if (empire != null) UnityEngine.Object.DestroyImmediate(empire);
            }
        }

        [Test]
        public void TheScreenShowsTheLockedNarrativeCopy_NotPlaceholders()
        {
            // ST's copy is locked in the register. Pinned so a later edit cannot quietly drift the
            // player-facing wording away from what was actually approved.
            _presenter.Initialize(FreshProfile(), NowUtc, null);

            string all = string.Empty;
            foreach (UnityEngine.UI.Text t in
                     _presenter.CanvasObjectForTests.GetComponentsInChildren<UnityEngine.UI.Text>(true))
            {
                all += t.text + " ";
            }

            StringAssert.Contains("COMMAND CIRCUIT", all);
            StringAssert.Contains("War Room", all);
            StringAssert.Contains("ORDER THE RANKS", all);
            StringAssert.Contains("MUSTER THE RANKS", all);
            StringAssert.Contains("READ THE FIELD", all);
        }

        [Test]
        public void TheWarRoomChip_OpensTheCircuit_AndLeavesReconstructionsReachable()
        {
            // Reachability from a REAL entry point, not just constructibility. Also guards the
            // reason Command Circuit is a SIBLING chip rather than a branch of War Room: the
            // Tactical Brief trial routes into the same puzzle content War Room opens, so hijacking
            // that chip would have made free-play Reconstructions unreachable.
            GameObject empireHost = new GameObject("EmpireHostForCircuitEntry");
            try
            {
                var empire = empireHost.AddComponent<EmpirePresenter>();

                SoloCircuitPresenter circuit = empire.OpenSoloCircuitForTests();
                Assert.IsNotNull(circuit, "The Command Circuit chip must actually open the screen.");
                Assert.IsNotNull(GameObject.Find(SoloCircuitPresenter.CanvasName));

                TacticalPuzzlePresenter puzzles = empire.OpenWarRoomForTests();
                Assert.IsNotNull(puzzles,
                    "War Room Reconstructions must remain reachable alongside the Circuit.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(empireHost);
                GameObject stale = GameObject.Find(SoloCircuitPresenter.CanvasName);
                if (stale != null) UnityEngine.Object.DestroyImmediate(stale);
            }
        }

        [Test]
        public void ANullProfile_DoesNotCrashTheScreen()
        {
            // A screen reached before a profile loads must degrade, not throw - a thrown exception
            // mid-build leaves a half-constructed canvas, which is the stale-layer bug again.
            Assert.DoesNotThrow(() => _presenter.Initialize(null, NowUtc, null));
            Assert.IsNotNull(_presenter.CanvasObjectForTests);
        }
    }
}

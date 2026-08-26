using System;
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

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("SoloCircuitHost");
            _presenter = _host.AddComponent<SoloCircuitPresenter>();
        }

        [TearDown]
        public void TearDown()
        {
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
        public void ClearingATrial_PersistsAndTheScreenReflectsIt()
        {
            PlayerProfile profile = FreshProfile();
            _presenter.Initialize(profile, NowUtc, null);

            _presenter.PressTrialForTests(SoloCircuitTrial.Formation);

            Assert.IsTrue(
                SoloCollectionCircuit.IsTrialClearedToday(profile.soloCircuitProgress, SoloCircuitTrial.Formation),
                "The clear must land on the profile, not only in the view.");
            StringAssert.Contains("Cleared",
                _presenter.TrialStatusForTests(SoloCircuitTrial.Formation),
                "The screen must show the new state after acting.");
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

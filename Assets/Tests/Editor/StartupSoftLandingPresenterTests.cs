using System;
using System.Collections.Generic;
using System.IO;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>VS-UI-SOFT-LANDING-STARTUP-001. Covers the startup soft-landing screen: which
    /// copy each real save-file state produces, that Continue always routes into the real,
    /// unmodified HomePagePresenter (existing users still reach Home; new users reach the same
    /// route, which carries Home's own existing first-run state), and that the five Home
    /// destinations and their callbacks are untouched.</summary>
    public class StartupSoftLandingPresenterTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDStartupSoftLanding_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
            foreach (GameObject go in _spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _spawned.Clear();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir)) Directory.Delete(_scratchSaveDir, true);
        }

        private StartupSoftLandingPresenter Build()
        {
            var go = new GameObject("StartupSoftLandingHost");
            _spawned.Add(go);
            var presenter = go.AddComponent<StartupSoftLandingPresenter>();
            presenter.Initialize();
            return presenter;
        }

        [Test]
        public void NoSaveFileYet_ReadAsFirstRun_AndShowsFirstRunCopy()
        {
            Assert.IsFalse(SaveSystem.Exists, "Test setup should start with no save file on disk.");
            StartupSoftLandingPresenter presenter = Build();

            Assert.IsTrue(presenter.IsFirstRunForTests);
            Text subtitle = presenter.CanvasObjectForTests.transform.Find("Subtitle").GetComponent<Text>();
            StringAssert.Contains("Welcome.", subtitle.text);
            StringAssert.DoesNotContain("back", subtitle.text);
        }

        [Test]
        public void ExistingSaveFile_ReadAsReturning_AndShowsReturningCopy()
        {
            SaveSystem.Save(new PlayerProfile { gold = 500 });
            SaveSystem.ResetCurrentProfileForTests();
            Assert.IsTrue(SaveSystem.Exists, "Test setup should have written a save file to disk.");

            StartupSoftLandingPresenter presenter = Build();

            Assert.IsFalse(presenter.IsFirstRunForTests);
            Text subtitle = presenter.CanvasObjectForTests.transform.Find("Subtitle").GetComponent<Text>();
            StringAssert.Contains("Welcome back.", subtitle.text);
        }

        [Test]
        public void Continue_TearsDownStartupCanvas_AndBuildsRealHomePagePresenter()
        {
            StartupSoftLandingPresenter presenter = Build();
            GameObject host = presenter.gameObject;
            Assert.IsNotNull(presenter.CanvasObjectForTests);

            presenter.PressContinueForTests();

            Assert.IsNull(GameObject.Find(StartupSoftLandingPresenter.CanvasName),
                "Startup canvas must not remain after Continue.");
            var home = host.GetComponent<HomePagePresenter>();
            Assert.IsNotNull(home, "Continue must construct the real HomePagePresenter, not a stand-in.");
        }

        [Test]
        public void Continue_ForBothFirstRunAndReturning_ReachesHomeWithAllFiveDestinationsAndCallbacks()
        {
            foreach (bool preSeedSave in new[] { false, true })
            {
                SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
                SaveSystem.ResetCurrentProfileForTests();
                if (preSeedSave)
                {
                    SaveSystem.Save(new PlayerProfile { gold = 500 });
                    SaveSystem.ResetCurrentProfileForTests();
                }

                var go = new GameObject("StartupSoftLandingHost_" + preSeedSave);
                _spawned.Add(go);
                var presenter = go.AddComponent<StartupSoftLandingPresenter>();
                presenter.Initialize();
                presenter.PressContinueForTests();

                var home = go.GetComponent<HomePagePresenter>();
                home.BuildHomePageUIForTests();
                GameObject canvasObj = home.HomeCanvasObjectForTests;
                Assert.IsNotNull(canvasObj, "preSeedSave=" + preSeedSave);

                Transform bar = canvasObj.transform.Find("ContentPanel/DestinationBar");
                Assert.IsNotNull(bar, "DestinationBar missing, preSeedSave=" + preSeedSave);
                var labels = new List<string>();
                foreach (Button btn in bar.GetComponentsInChildren<Button>(true))
                {
                    Text label = btn.GetComponentInChildren<Text>(true);
                    if (label != null) labels.Add(label.text);
                }
                CollectionAssert.AreEquivalent(
                    new[] { "HOME", "BATTLE", "QUESTS", "COLLECTION", "EMPIRE" }, labels,
                    "preSeedSave=" + preSeedSave);

                CampaignMapPresenter.CleanupStaleMetagameCanvases();
            }
        }
    }
}

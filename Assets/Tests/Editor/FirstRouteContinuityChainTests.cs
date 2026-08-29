using System.Collections.Generic;
using System.IO;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// CR-FIRST-ROUTE-CHAIN-021. Closes the one continuity gap CR-BETA-SMOKE-COVERAGE-017's audit
    /// found: StartupSoftLandingPresenterTests.cs proves Continue reaches a real HomePagePresenter,
    /// and HomePageTutorialRewardGuardTests.cs proves a WELCOME-card click reaches real tutorial
    /// entry - but no test ever chained the two. Each builds Home through a DIFFERENT path (Continue
    /// vs. a direct SpawnHomePagePresenter helper), so neither proves the Home a real player
    /// actually reaches (through Continue) genuinely carries the WELCOME card and a working Start
    /// Tutorial button. Uses only existing public seams from both files - no new seam, no
    /// production change.
    /// </summary>
    public class FirstRouteContinuityChainTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDFirstRouteChain_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
            foreach (GameObject go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, true);
        }

        /// <summary>Same pattern as GameBootstrapStateCoverageTests/HomePageTutorialRewardGuardTests
        /// - a real GameBootstrap must exist as GameBootstrap.Instance before Home is reached,
        /// because HomePagePresenter.OnStartTutorialClicked reads GameBootstrap.Instance directly
        /// (a null-conditional call - silently does nothing if no instance exists).</summary>
        private GameBootstrap SpawnAndInitializeBootstrap(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            GameBootstrap bootstrap = go.AddComponent<GameBootstrap>();
            bootstrap.Initialize();
            foreach (string spawnedName in new[] { "Canvas", "EventSystem", "CardDatabase", "BattleController" })
            {
                foreach (GameObject candidate in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    if (candidate.name == spawnedName && !_spawned.Contains(candidate))
                        _spawned.Add(candidate);
                }
            }
            return bootstrap;
        }

        [Test]
        public void StartupContinue_ReachesARealHome_ThatCarriesTheWorkingWelcomeCard()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Chain_Bootstrap");
            Assert.IsFalse(bootstrap.IsTutorialMatch, "Setup: expected a freshly-initialized match to not be tutorial-flagged yet.");

            var startupGo = new GameObject("Chain_StartupHost");
            _spawned.Add(startupGo);
            var startupPresenter = startupGo.AddComponent<StartupSoftLandingPresenter>();
            startupPresenter.Initialize();
            Assert.IsNotNull(startupPresenter.CanvasObjectForTests, "Setup: expected the real startup canvas to exist.");

            startupPresenter.PressContinueForTests();

            var home = startupGo.GetComponent<HomePagePresenter>();
            Assert.IsNotNull(home, "STATE UNREACHED: Continue did not construct the real HomePagePresenter.");
            home.BuildHomePageUIForTests();

            GameObject homeCanvas = home.HomeCanvasObjectForTests;
            Assert.IsNotNull(homeCanvas, "STATE UNREACHED: the Continue-reached Home built no canvas.");

            Transform welcomeCard = homeCanvas.transform.Find("ContentPanel/HomeFeedCanvas/HomeFeed/Viewport/Content/FeedCard_WELCOME");
            Assert.IsNotNull(welcomeCard,
                "STATE UNREACHED: the real Continue-reached Home's feed has no WELCOME card, even though a " +
                "directly-built Home does (HomePageTutorialRewardGuardTests) - the two construction paths have diverged.");

            Button startTutorialButton = welcomeCard.Find("PrimaryAction")?.GetComponent<Button>();
            Assert.IsNotNull(startTutorialButton, "The WELCOME card on the Continue-reached Home must contain a real Start Tutorial primary action.");

            startTutorialButton.onClick.Invoke();

            Assert.IsTrue(bootstrap.IsTutorialMatch,
                "A real click on the WELCOME card's Start Tutorial button, on the Home a real player actually " +
                "reaches via Continue, must reach the real tutorial entry path (StartApprovedTutorialBattle).");
        }
    }
}

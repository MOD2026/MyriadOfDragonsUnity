using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// BLOCK W - Gate chapter reader wired into the real Campaign launch gate
    /// (HomePagePresenter.TryLaunchCampaignStage). Gate is necessary but not sufficient
    /// (EMPIRE_SCHEMA_LOCK): stage unlock is checked first and still blocks on its own; Gate is a
    /// separate, additional requirement. Uses real stage ids ("1-1"/"2-1") so
    /// CampaignMapPresenter.TryParseStageChapter parses a genuine chapter number, not a synthetic
    /// test id. Does not touch GateLevelForChapter.
    /// </summary>
    public class GateCampaignLaunchTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsGateLaunch_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();

            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
            {
                Directory.Delete(_scratchSaveDir, recursive: true);
            }
        }

        private static List<string> SaveValidDeckProfile(CardDatabase database, System.Action<PlayerProfile> configure = null)
        {
            var sizingProfile = new PlayerProfile();
            sizingProfile.ApplyDataToEmpire();
            int deckSize = sizingProfile.Empire.DeckSlotCount;

            List<string> deckIds = database.AllCards.Select(c => c.Id)
                .Where(id => id != "warrior" && id != "novice_knight" && id != "goblin_caster")
                .Take(deckSize)
                .ToList();
            Assert.AreEqual(deckSize, deckIds.Count, "Setup: expected enough real cards to fill a full-size deck.");

            var profile = new PlayerProfile
            {
                cardCollection = new List<string>(deckIds),
                activeDeckCardIds = new List<string>(deckIds),
                stamina = 100,
            };
            configure?.Invoke(profile);
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: the production save path must persist the profile.");
            SaveSystem.ResetCurrentProfileForTests();
            return deckIds;
        }

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
            bootstrap.SetBattleCanvasVisible(false);
            return bootstrap;
        }

        private HomePagePresenter SpawnHomePagePresenter(GameBootstrap bootstrap)
        {
            var go = new GameObject("HomePagePresenterUnderTest_GateLaunch");
            _spawned.Add(go);
            var presenter = go.AddComponent<HomePagePresenter>();
            presenter.BindBattleControllerForTests(bootstrap.Battle);
            presenter.BindGameBootstrapForTests(bootstrap);
            return presenter;
        }

        private static CampaignMapPresenter OpenCampaignAndStageDetails(HomePagePresenter home, CampaignStageData stage)
        {
            home.OpenStoryCampaignForTests();
            CampaignMapPresenter campaign = home.GetComponent<CampaignMapPresenter>();
            Assert.IsNotNull(campaign, "Setup: expected OpenStoryCampaignForTests to build a real CampaignMapPresenter.");
            campaign.OpenStageDetailsForTests(stage);
            return campaign;
        }

        [Test]
        public void GateLevel1_Chapter1Launches_Chapter2BlockedEvenIfStageUnlocked()
        {
            var databaseGo = new GameObject("GateLaunch_CardDatabase_L1");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.

            SaveValidDeckProfile(database, p =>
            {
                p.gateLevel = 1;
                p.unlockedStageIds = new List<string> { "1-1", "2-1" };
            });

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("GateLaunch_Bootstrap_L1_Ch1");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage1_1 = CampaignMapPresenter.GetStageForTests("1-1");

            CampaignMapPresenter campaign = OpenCampaignAndStageDetails(home, stage1_1);
            bool clicked = campaign.ClickLaunchButtonForTests();

            Assert.IsTrue(clicked, "Setup: expected the Launch button to exist and be clickable.");
            Assert.IsTrue(bootstrap.BattleCanvasVisibleForTests, "Gate L1 must still allow Chapter 1 to launch.");
        }

        [Test]
        public void GateLevel1_Chapter2Stage_BlockedByGate_EvenThoughUnlocked()
        {
            var databaseGo = new GameObject("GateLaunch_CardDatabase_L1_Ch2");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.

            SaveValidDeckProfile(database, p =>
            {
                p.gateLevel = 1;
                p.unlockedStageIds = new List<string> { "1-1", "2-1" };
            });

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("GateLaunch_Bootstrap_L1_Ch2");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage2_1 = CampaignMapPresenter.GetStageForTests("2-1");

            CampaignMapPresenter campaign = OpenCampaignAndStageDetails(home, stage2_1);
            campaign.ClickLaunchButtonForTests();

            Assert.IsFalse(bootstrap.BattleCanvasVisibleForTests,
                "Gate L1 must block Stage 2-1 even though it is already unlocked (necessary, not sufficient).");
            Assert.AreEqual(HomePagePresenter.GateBlockedMessage, campaign.StatusTextForTests,
                "A Gate-blocked launch must show the distinct Gate message, not the locked-stage message.");
        }

        [Test]
        public void GateLevel3_Chapter2Stage_LaunchesWhenUnlocked()
        {
            var databaseGo = new GameObject("GateLaunch_CardDatabase_L3");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.

            SaveValidDeckProfile(database, p =>
            {
                p.gateLevel = 3;
                p.unlockedStageIds = new List<string> { "1-1", "2-1" };
            });

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("GateLaunch_Bootstrap_L3");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage2_1 = CampaignMapPresenter.GetStageForTests("2-1");

            CampaignMapPresenter campaign = OpenCampaignAndStageDetails(home, stage2_1);
            bool clicked = campaign.ClickLaunchButtonForTests();

            Assert.IsTrue(clicked, "Setup: expected the Launch button to exist and be clickable.");
            Assert.IsTrue(bootstrap.BattleCanvasVisibleForTests, "Gate L3 must allow an unlocked Chapter 2 stage to launch.");
        }

        [Test]
        public void GateLevel3_Chapter2Stage_StillBlockedIfStageNotUnlocked()
        {
            var databaseGo = new GameObject("GateLaunch_CardDatabase_L3_Locked");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.

            // Gate would allow Chapter 2, but 2-1 was never added to unlockedStageIds - the
            // stage-unlock check must still win (necessary != sufficient works both directions).
            SaveValidDeckProfile(database, p =>
            {
                p.gateLevel = 3;
                p.unlockedStageIds = new List<string> { "1-1" };
            });

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("GateLaunch_Bootstrap_L3_Locked");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage2_1 = CampaignMapPresenter.GetStageForTests("2-1");

            CampaignMapPresenter campaign = OpenCampaignAndStageDetails(home, stage2_1);
            campaign.ClickLaunchButtonForTests();

            Assert.IsFalse(bootstrap.BattleCanvasVisibleForTests, "An un-unlocked stage must never launch, regardless of Gate.");
            Assert.AreEqual(HomePagePresenter.LockedBlockedMessage, campaign.StatusTextForTests,
                "Stage-unlock must be checked and reported before Gate - a locked stage is not a Gate problem.");
        }

        [Test]
        public void DefaultFreshProfile_GateLevelOne_NeverBlocksChapter1()
        {
            var databaseGo = new GameObject("GateLaunch_CardDatabase_Fresh");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.

            // A completely default, hand-untouched PlayerProfile - gateLevel is whatever the
            // schema itself defaults to (1), not explicitly set here, to prove Chapter 1 is safe
            // for every fresh profile without any test-side Gate configuration.
            List<string> deckIds = SaveValidDeckProfile(database);
            PlayerProfile defaultCheck = new PlayerProfile();
            Assert.AreEqual(1, defaultCheck.gateLevel, "Setup: expected a fresh PlayerProfile's own default Gate level to be 1.");
            _ = deckIds;

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("GateLaunch_Bootstrap_Fresh");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage1_1 = CampaignMapPresenter.GetStageForTests("1-1");

            CampaignMapPresenter campaign = OpenCampaignAndStageDetails(home, stage1_1);
            bool clicked = campaign.ClickLaunchButtonForTests();

            Assert.IsTrue(clicked, "Setup: expected the Launch button to exist and be clickable.");
            Assert.IsTrue(bootstrap.BattleCanvasVisibleForTests, "Chapter 1 must never be blocked for a default fresh profile's Gate level.");
        }
    }
}

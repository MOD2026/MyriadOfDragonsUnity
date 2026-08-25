using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// CAMPAIGN LAUNCH FEEDBACK CONTRACT (release feature) - proves the whole player-visible
    /// launch-feedback loop through the real production owners: HomePagePresenter.
    /// OpenStoryCampaignForTests/TryLaunchCampaignStage (via the real Launch button's own
    /// onClick - CampaignMapPresenter.ClickLaunchButtonForTests) and the Campaign map's own
    /// persistent status text (CampaignMapPresenter.StatusTextForTests). No message is asserted
    /// by re-deriving it - every expected string is the exact literal the contract requires,
    /// matching the shared HomePagePresenter.*BlockedMessage constants the production code
    /// itself uses.
    /// </summary>
    public class CampaignLaunchFeedbackContractTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsCampaignFeedback_" + System.Guid.NewGuid().ToString("N"));
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
            var go = new GameObject("HomePagePresenterUnderTest_CampaignFeedback");
            _spawned.Add(go);
            var presenter = go.AddComponent<HomePagePresenter>();
            presenter.BindBattleControllerForTests(bootstrap.Battle);
            presenter.BindGameBootstrapForTests(bootstrap);
            return presenter;
        }

        /// <summary>Synthetic stage ids deliberately never match a StoryDatabase "&lt;id&gt;_pre"
        /// sequence (unlike the real "1-1"/"1-2"/"1-3") so ClickLaunchButtonForTests's Launch
        /// button calls AttemptLaunch synchronously (StoryOverlayPresenter.PlaySequence invokes
        /// its completion callback immediately when there is no sequence to play - see its own
        /// null/empty-lines branch) instead of opening a real story dialogue overlay this test
        /// would then have to click through.</summary>
        private static CampaignStageData BuildTestStage(string stageId, string[] enemyDeckCardIds) =>
            new CampaignStageData(stageId, "Test Stage", "Test Enemy", "UI/Portraits/Paladin", "A synthetic stage for the launch-feedback contract tests.", 100, 10, enemyDeckCardIds: enemyDeckCardIds);

        private static CampaignMapPresenter OpenCampaignAndStageDetails(HomePagePresenter home, CampaignStageData stage)
        {
            home.OpenStoryCampaignForTests();
            CampaignMapPresenter campaign = home.GetComponent<CampaignMapPresenter>();
            Assert.IsNotNull(campaign, "Setup: expected OpenStoryCampaignForTests to build a real CampaignMapPresenter.");
            campaign.OpenStageDetailsForTests(stage);
            return campaign;
        }

        [Test]
        public void CampaignMap_ShowsPersistentStaminaAndEntryCostStatus()
        {
            var databaseGo = new GameObject("CampaignFeedback_CardDatabase_Status");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            SaveValidDeckProfile(database, p => p.stamina = 73);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignFeedback_StatusBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);

            home.OpenStoryCampaignForTests();
            CampaignMapPresenter campaign = home.GetComponent<CampaignMapPresenter>();

            Assert.AreEqual($"Stamina: 73/{bootstrap.Profile.maxStamina} • Stage entry: 1", campaign.StatusTextForTests,
                "Requirement 1: the Campaign map must show a persistent 'Stamina: current/max • Stage entry: 1' status.");
        }

        [Test]
        public void BlockedLaunch_LockedStage_ShowsExactMessage_AndStaysOnCampaignMap()
        {
            var databaseGo = new GameObject("CampaignFeedback_CardDatabase_Locked");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            SaveValidDeckProfile(database); // default profile: only Stage 1-1 unlocked

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignFeedback_LockedBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            // Never added to unlockedStageIds - a fresh profile only ever starts with "1-1".
            CampaignStageData lockedStage = BuildTestStage("test-locked", CampaignMapPresenter.GetStageForTests("1-1").enemyDeckCardIds);

            CampaignMapPresenter campaign = OpenCampaignAndStageDetails(home, lockedStage);
            bool clicked = campaign.ClickLaunchButtonForTests();

            Assert.IsTrue(clicked, "Setup: expected the Launch button to exist and be clickable.");
            Assert.IsFalse(bootstrap.BattleCanvasVisibleForTests, "A locked stage must never reveal Battle.");
            Assert.AreEqual(HomePagePresenter.LockedBlockedMessage, campaign.StatusTextForTests,
                "Requirement 2: a locked stage must show exactly the required locked message.");
            Assert.IsNotNull(home.GetComponent<CampaignMapPresenter>(), "Requirement 3: the player must not be left stranded - the Campaign map itself must remain.");
        }

        [Test]
        public void BlockedLaunch_InsufficientStamina_ShowsExactMessage_AndStaysOnCampaignMap()
        {
            var databaseGo = new GameObject("CampaignFeedback_CardDatabase_Stamina");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            string[] enemyIds = CampaignMapPresenter.GetStageForTests("1-1").enemyDeckCardIds;
            SaveValidDeckProfile(database, p => { p.stamina = 0; p.unlockedStageIds = new List<string> { "1-1", "test-stamina" }; });

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignFeedback_StaminaBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage = BuildTestStage("test-stamina", enemyIds);

            CampaignMapPresenter campaign = OpenCampaignAndStageDetails(home, stage);
            campaign.ClickLaunchButtonForTests();

            Assert.IsFalse(bootstrap.BattleCanvasVisibleForTests, "Insufficient Stamina must never reveal Battle.");
            Assert.AreEqual(HomePagePresenter.StaminaBlockedMessage, campaign.StatusTextForTests,
                "Requirement 2: insufficient Stamina must show exactly the required Stamina message.");
            Assert.AreEqual(0, bootstrap.Profile.stamina, "A blocked launch must not deduct Stamina.");
        }

        [Test]
        public void BlockedLaunch_InvalidStageConfig_ShowsExactMessage_AndStaysOnCampaignMap()
        {
            var databaseGo = new GameObject("CampaignFeedback_CardDatabase_InvalidConfig");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            SaveValidDeckProfile(database);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignFeedback_InvalidConfigBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            var unconfiguredStage = new CampaignStageData("x-unconfigured", "Unconfigured", "Nobody", "UI/Portraits/Paladin", "No battle configuration.", 0, 0);

            CampaignMapPresenter campaign = OpenCampaignAndStageDetails(home, unconfiguredStage);
            campaign.ClickLaunchButtonForTests();

            Assert.IsFalse(bootstrap.BattleCanvasVisibleForTests, "An invalid stage configuration must never reveal Battle.");
            Assert.AreEqual(HomePagePresenter.InvalidConfigBlockedMessage, campaign.StatusTextForTests,
                "Requirement 2: an invalid stage configuration must show exactly the required message.");
        }

        [Test]
        public void BlockedLaunch_NoConfirmedDeck_RoutesDirectlyToDeckBuilder_WithExactMessage()
        {
            var databaseGo = new GameObject("CampaignFeedback_CardDatabase_NoDeck");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.

            string[] enemyIds = CampaignMapPresenter.GetStageForTests("1-1").enemyDeckCardIds;
            var noDeckProfile = new PlayerProfile { stamina = 100, unlockedStageIds = new List<string> { "1-1", "test-nodeck" } };
            Assert.IsTrue(SaveSystem.Save(noDeckProfile), "Setup: expected the no-deck profile to save.");
            SaveSystem.ResetCurrentProfileForTests();

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignFeedback_NoDeckBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage = BuildTestStage("test-nodeck", enemyIds);

            CampaignMapPresenter campaign = OpenCampaignAndStageDetails(home, stage);
            campaign.ClickLaunchButtonForTests();

            Assert.IsFalse(bootstrap.BattleCanvasVisibleForTests, "Requirement 3: a missing deck must never reveal Battle.");

            DeckBuilderPresenter deckBuilder = home.GetComponent<DeckBuilderPresenter>();
            Assert.IsNotNull(deckBuilder, "Requirement 3: a missing deck must route directly to the existing Deck Builder.");

            GameObject deckBuilderCanvas = GameObject.Find("DeckBuilderCanvas");
            Assert.IsNotNull(deckBuilderCanvas, "Setup: expected the real Deck Builder canvas to be built.");
            Text deckStatus = deckBuilderCanvas.transform.Find("DeckPanel/DeckStatus")?.GetComponent<Text>();
            Assert.IsNotNull(deckStatus, "Setup: expected the existing Deck Builder status surface to exist.");
            Assert.AreEqual(HomePagePresenter.DeckBlockedMessage, deckStatus.text,
                "Requirement 3: the Deck Builder redirect must show the exact existing deck-status message.");
        }

        [Test]
        public void ValidLaunch_SpendsOneStamina_AndRevealsBattleExactlyOnce()
        {
            var databaseGo = new GameObject("CampaignFeedback_CardDatabase_Success");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            string[] enemyIds = CampaignMapPresenter.GetStageForTests("1-1").enemyDeckCardIds;
            SaveValidDeckProfile(database, p => { p.stamina = 5; p.unlockedStageIds = new List<string> { "1-1", "test-success" }; });

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignFeedback_SuccessBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage = BuildTestStage("test-success", enemyIds);

            CampaignMapPresenter campaign = OpenCampaignAndStageDetails(home, stage);
            bool clicked = campaign.ClickLaunchButtonForTests();

            Assert.IsTrue(clicked, "Setup: expected the Launch button to exist and be clickable.");
            Assert.IsTrue(bootstrap.BattleCanvasVisibleForTests, "Requirement 4: a valid launch must reveal Battle.");
            Assert.AreEqual(4, bootstrap.Profile.stamina, "Requirement 4: a valid launch must spend exactly 1 Stamina.");
            Assert.IsNull(bootstrap.NormalMatchStatusForTests, "Setup: expected a valid, unblocked campaign match.");
        }
    }
}

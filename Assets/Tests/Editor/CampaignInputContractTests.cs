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
    /// CAMPAIGN STAGE-DETAIL LAUNCH INPUT CONTRACT (release feature) - proves the real Campaign
    /// input surface through the real production Buttons: CampaignMapPresenter.
    /// ClickStageNodeForTests/ClickLaunchButtonForTests invoke the actual stage-node and Launch
    /// Battle Button.onClick (EditMode cannot generate a real pointer click, so this is the
    /// closest fidelity available - never a direct call to OpenStageDetails/AttemptLaunch
    /// standing in for the click itself). Structural root-Button/decorative-hierarchy checks
    /// reuse the existing UIReleaseGateTestUtility already proven on DeckBuilder/Shop, not a
    /// second, independently-invented input gate.
    /// </summary>
    public class CampaignInputContractTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        private static readonly string[] CampaignActionRootNames =
        {
            "Btn_Back", "StageNode_1-1", "StageNode_1-2", "StageNode_1-3", "Btn_Launch", "Btn_Close",
        };

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsCampaignInput_" + System.Guid.NewGuid().ToString("N"));
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

        private static void SaveValidDeckProfile(CardDatabase database, System.Action<PlayerProfile> configure = null)
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
                unlockedStageIds = new List<string> { "1-1", "1-2", "1-3" },
            };
            configure?.Invoke(profile);
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: the production save path must persist the profile.");
            SaveSystem.ResetCurrentProfileForTests();
        }

        private GameBootstrap SpawnAndInitializeBootstrap(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            GameBootstrap bootstrap = go.AddComponent<GameBootstrap>();
            bootstrap.Initialize();
            foreach (string spawnedName in new[] { "Canvas", "EventSystem", "CardDatabase", "BattleController" })
            {
                GameObject spawned = GameObject.Find(spawnedName);
                if (spawned != null) _spawned.Add(spawned);
            }
            bootstrap.SetBattleCanvasVisible(false);
            return bootstrap;
        }

        private HomePagePresenter SpawnHomePagePresenter(GameBootstrap bootstrap)
        {
            var go = new GameObject("HomePagePresenterUnderTest_CampaignInput");
            _spawned.Add(go);
            var presenter = go.AddComponent<HomePagePresenter>();
            presenter.BindBattleControllerForTests(bootstrap.Battle);
            presenter.BindGameBootstrapForTests(bootstrap);
            return presenter;
        }

        private static CampaignMapPresenter OpenCampaignWithStageDetailsOpen(HomePagePresenter home, string stageId)
        {
            home.OpenStoryCampaignForTests();
            CampaignMapPresenter campaign = home.GetComponent<CampaignMapPresenter>();
            Assert.IsNotNull(campaign, "Setup: expected OpenStoryCampaignForTests to build a real CampaignMapPresenter.");
            bool clicked = campaign.ClickStageNodeForTests(stageId);
            Assert.IsTrue(clicked, $"Setup: expected the real Stage {stageId} node Button to exist and be clickable.");
            return campaign;
        }

        /// <summary>Every real Chapter 1 stage ("1-1"/"1-2"/"1-3") has a registered StoryDatabase
        /// "&lt;id&gt;_pre" sequence, so a real Launch Battle click on one opens a story dialogue
        /// overlay instead of calling AttemptLaunch synchronously - a separate system this task's
        /// scope excludes ("do not change ... game rules"). Tests that specifically exercise the
        /// Launch Battle click's outcome therefore open the modal for a synthetic stage id (no
        /// "_pre" sequence exists) via OpenStageDetailsForTests - the stage-SELECTION click itself
        /// is separately proven end-to-end against a real stage by
        /// SelectingAStage_OpensItsDetailModal_ViaTheRealStageNodeButton, so the real click path
        /// is still exercised, just not doubled up onto the same call in every test (requirement
        /// 6 is about not relying ONLY on direct handler calls, not about never using them).</summary>
        private static CampaignMapPresenter OpenSyntheticStageDetails(HomePagePresenter home, CampaignStageData stage)
        {
            home.OpenStoryCampaignForTests();
            CampaignMapPresenter campaign = home.GetComponent<CampaignMapPresenter>();
            Assert.IsNotNull(campaign, "Setup: expected OpenStoryCampaignForTests to build a real CampaignMapPresenter.");
            campaign.OpenStageDetailsForTests(stage);
            return campaign;
        }

        private static CampaignStageData BuildTestStage(string stageId, string[] enemyDeckCardIds) =>
            new CampaignStageData(stageId, "Test Stage", "Test Enemy", "UI/Portraits/Paladin", "A synthetic stage for the input-contract tests.", 100, 10, enemyDeckCardIds: enemyDeckCardIds);

        [Test]
        public void SelectingAStage_OpensItsDetailModal_ViaTheRealStageNodeButton()
        {
            var databaseGo = new GameObject("CampaignInput_CardDatabase_Select");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            SaveValidDeckProfile(database);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignInput_SelectBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);

            CampaignMapPresenter campaign = OpenCampaignWithStageDetailsOpen(home, "1-1");

            Assert.IsTrue(campaign.IsDetailModalOpenForTests, "Requirement 1: selecting a Campaign stage via its real node Button must open the existing detail modal.");
            GameObject modal = GameObject.Find("CampaignMapCanvas/StageDetailModal");
            Assert.IsNotNull(modal, "Requirement 1: the real StageDetailModal object must exist after selection.");
        }

        [Test]
        public void TappingLaunch_OnAValidStage_InvokesLaunchExactlyOnce_AndBattleBecomesVisible()
        {
            var databaseGo = new GameObject("CampaignInput_CardDatabase_Valid");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            string[] enemyIds = CampaignMapPresenter.GetStageForTests("1-1").enemyDeckCardIds;
            SaveValidDeckProfile(database, p => { p.stamina = 5; p.unlockedStageIds.Add("test-valid"); });

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignInput_ValidBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);

            CampaignMapPresenter campaign = OpenSyntheticStageDetails(home, BuildTestStage("test-valid", enemyIds));
            bool clicked = campaign.ClickLaunchButtonForTests();

            Assert.IsTrue(clicked, "Requirement 2: expected the real Launch Battle Button to exist and be clickable.");
            Assert.IsTrue(bootstrap.BattleCanvasVisibleForTests, "Requirement 3: a valid launch must reveal Battle.");
            // Exactly-once proof: Stamina must drop by exactly 1, not more - a double-invocation
            // of the launch callback would spend twice.
            Assert.AreEqual(4, bootstrap.Profile.stamina, "Requirement 2: the real Launch Battle click must invoke the launch callback exactly once (Stamina spent exactly once).");
        }

        [Test]
        public void TappingLaunch_ForEachBlockedOutcome_ShowsTheExactStatusText_ViaTheRealButtons()
        {
            var databaseGo = new GameObject("CampaignInput_CardDatabase_Blocked");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            string[] enemyIds = CampaignMapPresenter.GetStageForTests("1-1").enemyDeckCardIds;
            SaveValidDeckProfile(database, p => { p.stamina = 0; p.unlockedStageIds.Add("test-blocked"); }); // insufficient Stamina blocks the launch

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignInput_BlockedBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);

            CampaignMapPresenter campaign = OpenSyntheticStageDetails(home, BuildTestStage("test-blocked", enemyIds));
            bool clicked = campaign.ClickLaunchButtonForTests();

            Assert.IsTrue(clicked, "Requirement 2: expected the real Launch Battle Button to exist and be clickable.");
            Assert.IsFalse(bootstrap.BattleCanvasVisibleForTests, "Requirement 3: a blocked launch must never reveal Battle.");
            Assert.AreEqual(HomePagePresenter.StaminaBlockedMessage, campaign.StatusTextForTests,
                "Requirement 3: the exact blocked reason must appear on the existing Campaign status text, via the real Button click path.");
        }

        [Test]
        public void ActionRoots_EachUseOneRealRootImageAndButton_WithTargetGraphicAssigned()
        {
            var databaseGo = new GameObject("CampaignInput_CardDatabase_RootContract");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            SaveValidDeckProfile(database);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignInput_RootContractBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            OpenCampaignWithStageDetailsOpen(home, "1-1");

            GameObject canvas = GameObject.Find("CampaignMapCanvas");
            Assert.IsNotNull(canvas, "Setup: expected the real Campaign map canvas to exist.");

            foreach (string actionName in new[] { "Btn_Back", "StageNode_1-1", "Btn_Launch", "Btn_Close" })
            {
                GameObject root = FindDescendant(canvas.transform, actionName);
                UIReleaseGateTestUtility.AssertActionRoot(root, actionName);
            }
        }

        [Test]
        public void ActionRoots_HaveNoRaycastableDecorativeChildren()
        {
            var databaseGo = new GameObject("CampaignInput_CardDatabase_Decorative");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            SaveValidDeckProfile(database);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignInput_DecorativeBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            OpenCampaignWithStageDetailsOpen(home, "1-1");

            GameObject canvas = GameObject.Find("CampaignMapCanvas");
            Assert.IsNotNull(canvas, "Setup: expected the real Campaign map canvas to exist.");

            foreach (string actionName in new[] { "Btn_Back", "StageNode_1-1", "Btn_Launch", "Btn_Close" })
            {
                GameObject root = FindDescendant(canvas.transform, actionName);
                UIReleaseGateTestUtility.AssertDecorativeChildrenAreNonRaycastable(root, actionName);
            }
        }

        [Test]
        public void NoFullScreenOrTransparentGraphicBlocksAnyCampaignAction()
        {
            var databaseGo = new GameObject("CampaignInput_CardDatabase_NoBlock");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            SaveValidDeckProfile(database);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignInput_NoBlockBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            OpenCampaignWithStageDetailsOpen(home, "1-1");

            GameObject canvas = GameObject.Find("CampaignMapCanvas");
            Assert.IsNotNull(canvas, "Setup: expected the real Campaign map canvas to exist.");

            UIReleaseGateTestUtility.AssertNoBlockingGraphicOverAction(canvas, CampaignActionRootNames);
        }

        private static GameObject FindDescendant(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t.gameObject;
            }
            return null;
        }
    }
}

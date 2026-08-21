using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Data;
using MyriadOfDragons.Economy;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// CAMPAIGN STAMINA-ENTRY CONTRACT (release feature) - proves the whole gate through the
    /// real production owner, HomePagePresenter.LaunchCampaignStageForTests (the exact private
    /// gate OpenStoryCampaign's onLaunchBattle callback calls - EditMode cannot click through
    /// CampaignMapPresenter's UI to reach it otherwise), and GameBootstrap.
    /// TrySpendCampaignStaminaForAttempt (the sole spend choke point, itself delegating to
    /// CurrencyManager.SpendStamina, the sole stamina authority). Nothing here reimplements the
    /// gate order, the spend, or the retry wiring.
    /// </summary>
    public class CampaignStaminaEntryContractTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsCampaignStamina_" + System.Guid.NewGuid().ToString("N"));
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

        private static List<string> SaveValidDeckForNormalMatch(CardDatabase database, System.Action<PlayerProfile> configure = null)
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
            };
            configure?.Invoke(profile);
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: the production save path must persist the confirmed deck.");
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
                GameObject spawned = GameObject.Find(spawnedName);
                if (spawned != null) _spawned.Add(spawned);
            }
            bootstrap.SetBattleCanvasVisible(false);
            return bootstrap;
        }

        private HomePagePresenter SpawnHomePagePresenter(GameBootstrap bootstrap)
        {
            var go = new GameObject("HomePagePresenterUnderTest_CampaignStamina");
            _spawned.Add(go);
            var presenter = go.AddComponent<HomePagePresenter>();
            presenter.BindBattleControllerForTests(bootstrap.Battle);
            presenter.BindGameBootstrapForTests(bootstrap);
            return presenter;
        }

        private static void PlayOneCardAndWin(BattleController controller)
        {
            Card anyCard = controller.PlayerState.Hand.First(c => c.ResourceCost <= controller.PlayerState.Resource);
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, anyCard, Lane.Front),
                "Setup: expected to be able to play at least one card into Front.");
            Assert.IsTrue(controller.ConfirmFormation(), "Setup: expected the Formation to lock legally.");

            int ticksRun = 0;
            while (controller.Phase == BattlePhase.Combat)
            {
                controller.AdvanceCombatTick();
                ticksRun++;
                Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks, "Setup: expected a knockout well inside the tick cap.");
            }
        }

        private static void DeployWeaklyAndLose(BattleController controller)
        {
            Card weakestPlayerCard = controller.PlayerState.Hand.OrderBy(c => c.Attack).First();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, weakestPlayerCard, Lane.Back),
                "Setup: expected the player's one deployed card to legally occupy Back.");

            foreach (Card enemyCard in controller.EnemyState.Hand.ToList())
            {
                Lane lane = controller.EnemyState.Lanes[Lane.Front].Cards.Count < LaneState.MaxSlots ? Lane.Front : Lane.Middle;
                controller.TryPlayCard(controller.EnemyState, enemyCard, lane);
            }
            Assert.IsTrue(controller.ConfirmFormation(), "Setup: expected the Formation to lock legally.");

            int ticksRun = 0;
            while (controller.Phase == BattlePhase.Combat)
            {
                controller.AdvanceCombatTick();
                ticksRun++;
                Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks, "Setup: expected a defeat well inside the tick cap.");
            }
        }

        [Test]
        public void SuccessfulLaunch_SpendsExactlyOneStamina()
        {
            var databaseGo = new GameObject("CampaignStamina_CardDatabase_Success");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            SaveValidDeckForNormalMatch(database, p => p.stamina = 100);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignStamina_SuccessBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-1");

            home.LaunchCampaignStageForTests(stage);

            Assert.IsTrue(bootstrap.BattleCanvasVisibleForTests, "Requirement 1: a successful launch must reveal Battle.");
            Assert.IsNull(bootstrap.NormalMatchStatusForTests, "Setup: expected a valid, unblocked campaign match.");
            Assert.AreEqual(99, bootstrap.Profile.stamina, "Requirement 1: launching a valid Campaign stage must cost exactly 1 Stamina.");
        }

        [Test]
        public void InsufficientStamina_BlocksLaunch_NoBattleNoContextNoMutation()
        {
            var databaseGo = new GameObject("CampaignStamina_CardDatabase_Insufficient");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            SaveValidDeckForNormalMatch(database, p => p.stamina = 0);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignStamina_InsufficientBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-1");

            int goldBefore = bootstrap.Profile.gold;
            var unlockedBefore = new List<string>(bootstrap.Profile.unlockedStageIds);

            home.LaunchCampaignStageForTests(stage);

            Assert.IsFalse(bootstrap.BattleCanvasVisibleForTests, "Requirement 3: insufficient Stamina must never reveal Battle.");
            Assert.IsNull(home.ActiveStageForTests, "Requirement 3: insufficient Stamina must never create campaign context.");
            Assert.AreEqual(0, bootstrap.Profile.stamina, "Requirement 3: insufficient Stamina must not be further deducted (never goes negative).");
            Assert.AreEqual(goldBefore, bootstrap.Profile.gold, "Requirement 3: a blocked launch must not mutate any other wallet field.");
            CollectionAssert.AreEqual(unlockedBefore, bootstrap.Profile.unlockedStageIds, "Requirement 3: a blocked launch must not mutate unlocks.");
        }

        [Test]
        public void InvalidLaunch_MissingConfigOrLockedStageOrNoDeck_NeverSpendsStamina()
        {
            var databaseGo = new GameObject("CampaignStamina_CardDatabase_InvalidLaunch");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();

            // (a) Invalid/missing stage battle configuration.
            SaveValidDeckForNormalMatch(database, p => p.stamina = 50);
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignStamina_InvalidConfigBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            var unconfiguredStage = new CampaignStageData("x-unconfigured", "Unconfigured", "Nobody", "UI/Portraits/Paladin", "No battle configuration.", 0, 0);

            home.LaunchCampaignStageForTests(unconfiguredStage);
            Assert.IsFalse(bootstrap.BattleCanvasVisibleForTests, "Requirement 2/3: an invalid stage configuration must block before Battle is revealed.");
            Assert.AreEqual(50, bootstrap.Profile.stamina, "Requirement 2: an invalid stage configuration must never spend Stamina.");

            // (b) A real, validly-configured, but LOCKED stage (1-3, never won into on a fresh profile).
            CampaignStageData lockedStage = CampaignMapPresenter.GetStageForTests("1-3");
            home.LaunchCampaignStageForTests(lockedStage);
            Assert.IsFalse(bootstrap.BattleCanvasVisibleForTests, "Requirement 2/3: a locked stage must block before Battle is revealed.");
            Assert.AreEqual(50, bootstrap.Profile.stamina, "Requirement 2: a locked stage must never spend Stamina.");

            // (c) A valid, unlocked stage, but no confirmed player deck.
            var noDeckProfile = new PlayerProfile { stamina = 50 };
            Assert.IsTrue(SaveSystem.Save(noDeckProfile), "Setup: expected the no-deck profile to save.");
            SaveSystem.ResetCurrentProfileForTests();
            GameBootstrap bootstrap2 = SpawnAndInitializeBootstrap("CampaignStamina_NoDeckBootstrap");
            HomePagePresenter home2 = SpawnHomePagePresenter(bootstrap2);
            CampaignStageData validStage = CampaignMapPresenter.GetStageForTests("1-1");

            home2.LaunchCampaignStageForTests(validStage);
            Assert.IsFalse(bootstrap2.BattleCanvasVisibleForTests, "Requirement 2/3: no confirmed deck must block before Battle is revealed.");
            Assert.AreEqual(50, bootstrap2.Profile.stamina, "Requirement 2: no confirmed deck must never spend Stamina.");
        }

        [Test]
        public void RetryAfterDefeat_IsANewAttempt_AndCostsStaminaAgain()
        {
            var databaseGo = new GameObject("CampaignStamina_CardDatabase_Retry");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            SaveValidDeckForNormalMatch(database, p => p.stamina = 100);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignStamina_RetryBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-1");

            home.LaunchCampaignStageForTests(stage);
            Assert.AreEqual(99, bootstrap.Profile.stamina, "Setup: expected the launch to cost 1 Stamina.");

            DeployWeaklyAndLose(bootstrap.Battle);
            Assert.AreEqual(99, bootstrap.Profile.stamina, "Requirement 5: a defeat's own resolution must not charge Stamina a second time.");

            bootstrap.RetryForTests();

            Assert.AreEqual(98, bootstrap.Profile.stamina, "Requirement 4: a retry after defeat is a new attempt and must cost 1 Stamina again.");
            Assert.IsNull(bootstrap.NormalMatchStatusForTests, "Setup: expected the retry to still be a valid, unblocked campaign match.");
        }

        [Test]
        public void RetryWithInsufficientStamina_BlocksTheRetry_WithoutStartingANewMatch()
        {
            var databaseGo = new GameObject("CampaignStamina_CardDatabase_RetryBlocked");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            SaveValidDeckForNormalMatch(database, p => p.stamina = 1); // exactly enough for the launch, none left for a retry

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignStamina_RetryBlockedBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-1");

            home.LaunchCampaignStageForTests(stage);
            Assert.AreEqual(0, bootstrap.Profile.stamina, "Setup: expected the launch to consume the player's last Stamina.");

            DeployWeaklyAndLose(bootstrap.Battle);
            BattlePhase phaseBeforeRetry = bootstrap.Battle.Phase;

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*retry blocked.*"));
            bootstrap.RetryForTests();

            Assert.AreEqual(0, bootstrap.Profile.stamina, "An insufficient-Stamina retry must not go negative or spend anything.");
            Assert.AreEqual(phaseBeforeRetry, bootstrap.Battle.Phase, "An insufficient-Stamina retry must not start a new match - the resolved result must remain as it was.");
        }

        [Test]
        public void NormalToBattle_AndTutorial_CostNoStamina()
        {
            var databaseGo = new GameObject("CampaignStamina_CardDatabase_Isolation");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            SaveValidDeckForNormalMatch(database, p => p.stamina = 100);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignStamina_IsolationBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);

            home.OnToBattleClickedForTests();
            Assert.IsTrue(bootstrap.BattleCanvasVisibleForTests, "Setup: expected a valid saved deck to enter normal Battle.");
            Assert.AreEqual(100, bootstrap.Profile.stamina, "Requirement 6: the ordinary Home 'To Battle' path must cost no Stamina.");

            bootstrap.ReturnToCityForTests();
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SetBattleCanvasVisible(true);
            Assert.AreEqual(100, bootstrap.Profile.stamina, "Requirement 7: Tutorial must cost no Stamina.");
        }

        [Test]
        public void EnergyPotionPurchase_RemainsCompatible_AndComposesCorrectlyWithACampaignSpend()
        {
            var databaseGo = new GameObject("CampaignStamina_CardDatabase_EnergyPotion");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            SaveValidDeckForNormalMatch(database, p => { p.stamina = 10; p.gems = 100; });

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignStamina_EnergyPotionBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            PlayerProfile profile = bootstrap.Profile;

            var shopGo = new GameObject("ShopPresenterUnderTest_CampaignStamina");
            _spawned.Add(shopGo);
            var shop = shopGo.AddComponent<ShopPresenter>();
            shop.Initialize(profile, onBackToHome: null);
            GameObject shopCanvas = GameObject.Find("ShopCanvas");
            if (shopCanvas != null) _spawned.Add(shopCanvas);

            shop.PurchaseForTests("res_energy"); // 30 Gems -> +50 Stamina, clamped to maxStamina
            Assert.AreEqual(70, profile.gems, "Requirement 8: Energy Potion must still cost exactly 30 Gems.");
            Assert.AreEqual(60, profile.stamina, "Requirement 8: Energy Potion must still restore exactly +50 Stamina (10 + 50).");

            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-1");
            home.LaunchCampaignStageForTests(stage);

            Assert.AreEqual(59, profile.stamina, "Requirement 8/9: a Campaign launch after an Energy Potion purchase must still spend exactly 1 Stamina through CurrencyManager, composing correctly with the earlier grant.");
        }

        [Test]
        public void CampaignStaminaSpend_PersistsAcrossAReload()
        {
            var databaseGo = new GameObject("CampaignStamina_CardDatabase_Reload");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            SaveValidDeckForNormalMatch(database, p => p.stamina = 100);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignStamina_ReloadBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-1");

            home.LaunchCampaignStageForTests(stage);
            Assert.AreEqual(99, bootstrap.Profile.stamina, "Setup: expected the launch to cost 1 Stamina.");

            SaveSystem.ResetCurrentProfileForTests();
            PlayerProfile reloaded = SaveSystem.Load();

            Assert.AreEqual(99, reloaded.stamina, "Requirement: the Campaign Stamina spend must survive a full reload.");
        }
    }
}

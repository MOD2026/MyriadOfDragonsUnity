using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Data;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// CAMPAIGN MATCH-CONTEXT LIFECYCLE CONTRACT (release feature) - proves the full launch ->
    /// retry -> victory/defeat -> return-home lifecycle through the real production owners:
    /// GameBootstrap.SetPendingCampaignStageForNextMatch/StartNewMatch/OnPlayAgainOrRetryPressed/
    /// OnReturnToCityPressed and HomePagePresenter.HandleMatchCompleted/HandleReturnToCityRequested/
    /// OnToBattleClicked - the same existing match-lifecycle owners the prior Campaign-stage
    /// battle-configuration contract already used, extended in place, never a second system.
    /// </summary>
    public class CampaignMatchContextLifecycleTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsCampaignLifecycle_" + System.Guid.NewGuid().ToString("N"));
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

        private static List<string> SaveValidDeckForNormalMatch(CardDatabase database)
        {
            var sizingProfile = new PlayerProfile();
            sizingProfile.ApplyDataToEmpire();
            int deckSize = sizingProfile.Empire.DeckSlotCount;

            List<string> deckIds = database.AllCards.Select(c => c.Id)
                .Where(id => id != "warrior" && id != "novice_knight" && id != "goblin_caster")
                .Take(deckSize)
                .ToList();
            Assert.AreEqual(deckSize, deckIds.Count, "Setup: expected enough real cards to fill a full-size deck.");

            var profile = new PlayerProfile();
            profile.cardCollection = new List<string>(deckIds);
            profile.activeDeckCardIds = new List<string>(deckIds);
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
            var go = new GameObject("HomePagePresenterUnderTest_Lifecycle");
            _spawned.Add(go);
            var presenter = go.AddComponent<HomePagePresenter>();
            presenter.BindBattleControllerForTests(bootstrap.Battle);
            presenter.BindGameBootstrapForTests(bootstrap);
            return presenter;
        }

        private static HashSet<string> EnemyIds(GameBootstrap bootstrap) =>
            new HashSet<string>(bootstrap.Battle.EnemyState.Hand
                .Concat(bootstrap.Battle.EnemyState.DrawPile)
                .Select(c => c.Id));

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

        /// <summary>Mirror of PlayOneCardAndWin, roles reversed - same technique
        /// ChapterOneProgressionTests/HomePageTutorialRewardGuardTests already use to force a
        /// deterministic player defeat inside the tick cap.</summary>
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
        public void Defeat_ThenRetry_RefightesIdenticalConfiguredEnemyDeck()
        {
            var databaseGo = new GameObject("Lifecycle_CardDatabase_Retry");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            SaveValidDeckForNormalMatch(database);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Lifecycle_RetryBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);

            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-2");
            home.SetActiveStageForTests(stage);
            bootstrap.SetPendingCampaignStageForNextMatch(stage);
            bootstrap.SetBattleCanvasVisible(true);
            Assert.IsNull(bootstrap.NormalMatchStatusForTests, "Setup: expected a valid, unblocked campaign match.");
            HashSet<string> firstEnemyIds = EnemyIds(bootstrap);
            CollectionAssert.AreEquivalent(stage.enemyDeckCardIds, firstEnemyIds, "Setup: expected the first attempt to deal the stage's configured enemy deck.");

            DeployWeaklyAndLose(bootstrap.Battle);

            bootstrap.RetryForTests();

            Assert.IsNull(bootstrap.NormalMatchStatusForTests, "A retry after defeat must still be a valid, unblocked campaign match.");
            HashSet<string> retryEnemyIds = EnemyIds(bootstrap);
            CollectionAssert.AreEquivalent(stage.enemyDeckCardIds, retryEnemyIds,
                "Requirement 2: a retry after defeat must refight the identical configured enemy deck.");
        }

        [Test]
        public void Victory_ClearsCampaignContext_SoASubsequentPlayAgainNoLongerUsesTheStageDeck()
        {
            var databaseGo = new GameObject("Lifecycle_CardDatabase_VictoryClear");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            SaveValidDeckForNormalMatch(database);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Lifecycle_VictoryClearBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);

            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-1");
            home.SetActiveStageForTests(stage);
            bootstrap.SetPendingCampaignStageForNextMatch(stage);
            bootstrap.SetBattleCanvasVisible(true);
            Assert.IsNull(bootstrap.NormalMatchStatusForTests, "Setup: expected a valid, unblocked campaign match.");

            PlayOneCardAndWin(bootstrap.Battle);

            bootstrap.PlayAgainForTests();

            Assert.IsNull(bootstrap.NormalMatchStatusForTests, "A post-victory Play Again must still be a valid, unblocked match.");
            HashSet<string> replayEnemyIds = EnemyIds(bootstrap);
            CollectionAssert.AreNotEquivalent(stage.enemyDeckCardIds, replayEnemyIds,
                "Requirement 3: once a campaign victory resolves, the context must clear - a subsequent Play Again must no longer deal the stage's configured deck.");
        }

        [Test]
        public void Defeat_ThenReturnToCity_ClearsCampaignContext()
        {
            var databaseGo = new GameObject("Lifecycle_CardDatabase_DefeatReturn");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            SaveValidDeckForNormalMatch(database);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Lifecycle_DefeatReturnBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);

            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-3");
            home.SetActiveStageForTests(stage);
            bootstrap.SetPendingCampaignStageForNextMatch(stage);
            bootstrap.SetBattleCanvasVisible(true);
            Assert.IsNull(bootstrap.NormalMatchStatusForTests, "Setup: expected a valid, unblocked campaign match.");

            DeployWeaklyAndLose(bootstrap.Battle);

            bootstrap.ReturnToCityForTests();

            Assert.IsFalse(bootstrap.BattleCanvasVisibleForTests, "Setup: expected Return to City to hide the Battle canvas.");
            Assert.IsNull(home.ActiveStageForTests, "Return to City after a campaign defeat must clear the reward-attribution stage too.");

            // A later reveal with no fresh campaign launch (e.g. a plain "To Battle" refresh) must
            // no longer inherit the defeated stage's enemy deck - requirement 4/5 together.
            bootstrap.SetBattleCanvasVisible(true);
            Assert.IsNull(bootstrap.NormalMatchStatusForTests, "A normal match after returning to city must not be blocked.");
            HashSet<string> laterEnemyIds = EnemyIds(bootstrap);
            CollectionAssert.AreNotEquivalent(stage.enemyDeckCardIds, laterEnemyIds,
                "Requirement 4: returning to Home from a campaign defeat must clear the campaign context.");
        }

        [Test]
        public void NormalToBattle_AfterACampaignStage_NeverInheritsTheCampaignEnemyDeck()
        {
            var databaseGo = new GameObject("Lifecycle_CardDatabase_NormalIsolation");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            SaveValidDeckForNormalMatch(database);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Lifecycle_NormalIsolationBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);

            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-2");
            home.SetActiveStageForTests(stage);
            bootstrap.SetPendingCampaignStageForNextMatch(stage);
            bootstrap.SetBattleCanvasVisible(true);
            PlayOneCardAndWin(bootstrap.Battle);
            bootstrap.ReturnToCityForTests();

            // The real ordinary "To Battle" handler - requirement 5, exercised end-to-end through
            // the actual full lifecycle (launch -> win -> return -> normal entry), not a synthetic
            // leftover-state setup.
            home.OnToBattleClickedForTests();

            Assert.IsTrue(bootstrap.BattleCanvasVisibleForTests, "A valid saved deck must still enter normal Battle.");
            Assert.IsNull(bootstrap.NormalMatchStatusForTests, "A normal match must not be blocked.");
            HashSet<string> normalEnemyIds = EnemyIds(bootstrap);
            CollectionAssert.AreNotEquivalent(stage.enemyDeckCardIds, normalEnemyIds,
                "Requirement 5: an ordinary Home 'To Battle' match after a campaign stage must never inherit its enemy deck.");
        }

        [Test]
        public void Tutorial_NeverInheritsOrMutatesAPendingCampaignContext()
        {
            var databaseGo = new GameObject("Lifecycle_CardDatabase_TutorialIsolation");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            SaveValidDeckForNormalMatch(database);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Lifecycle_TutorialIsolationBootstrap");

            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-3");
            bootstrap.SetPendingCampaignStageForNextMatch(stage);

            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SetBattleCanvasVisible(true);

            HashSet<string> tutorialPlayerIds = new HashSet<string>(bootstrap.Battle.PlayerState.Hand
                .Concat(bootstrap.Battle.PlayerState.DrawPile)
                .Select(c => c.Id));
            CollectionAssert.AreEquivalent(new[] { "warrior", "novice_knight", "goblin_caster" }, tutorialPlayerIds,
                "Tutorial must keep its fixed scripted starter formation regardless of a pending campaign stage.");
            CollectionAssert.AreNotEquivalent(stage.enemyDeckCardIds, EnemyIds(bootstrap),
                "Tutorial must never inherit a pending campaign stage's configured enemy deck.");

            // Requirement 6, the "never mutate" half: after Tutorial runs, the pending campaign
            // stage must be completely untouched. SetBattleCanvasVisible's own hidden->visible
            // auto-refresh is gated off while IsTutorialMatch is still true (see its own comment),
            // so it cannot prove this on its own - PlayAgainForTests calls OnPlayAgainPressed
            // directly, which unconditionally runs StartNewMatch() regardless of IsTutorialMatch,
            // the same real path a later normal/campaign match will eventually go through.
            bootstrap.PlayAgainForTests();
            Assert.IsNull(bootstrap.NormalMatchStatusForTests, "Setup: expected the still-pending campaign stage to remain valid.");
            CollectionAssert.AreEquivalent(stage.enemyDeckCardIds, EnemyIds(bootstrap),
                "Tutorial must never mutate or clear a pending campaign stage's configuration.");
        }

        [Test]
        public void RewardIdempotency_StillHolds_AfterVictoryClearsTheEnemyDeckContext()
        {
            var databaseGo = new GameObject("Lifecycle_CardDatabase_RewardIdempotency");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            SaveValidDeckForNormalMatch(database);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Lifecycle_RewardIdempotencyBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);

            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-1");
            home.SetActiveStageForTests(stage);
            bootstrap.SetPendingCampaignStageForNextMatch(stage);
            bootstrap.SetBattleCanvasVisible(true);

            PlayerProfile profile = SaveManager.SaveData;
            int goldBefore = profile.gold;
            int gemsBefore = profile.gems;

            PlayOneCardAndWin(bootstrap.Battle);

            Assert.AreEqual(goldBefore + stage.goldReward, profile.gold, "Setup: expected the first clear to grant its configured reward.");
            Assert.AreEqual(gemsBefore + stage.gemReward, profile.gems, "Setup: expected the first clear to grant its configured reward.");
            int claimedCountAfterFirstClear = profile.claimedStageRewardIds.Count;

            // "Play Again" now falls through to the ordinary random-enemy path (the enemy-deck
            // context already cleared on victory) - currentActiveStage itself is untouched by
            // that clear, so the reward guard below must still recognize this as the same
            // already-cleared stage and grant nothing a second time, regardless of which enemy
            // deck the replay actually used.
            bootstrap.PlayAgainForTests();
            BattleController replayController = bootstrap.Battle;
            PlayOneCardAndWin(replayController);

            Assert.AreEqual(goldBefore + stage.goldReward, profile.gold, "A replay after the enemy-deck context cleared must still grant no duplicate gold.");
            Assert.AreEqual(gemsBefore + stage.gemReward, profile.gems, "A replay after the enemy-deck context cleared must still grant no duplicate gems.");
            Assert.AreEqual(claimedCountAfterFirstClear, profile.claimedStageRewardIds.Count,
                "A replay after the enemy-deck context cleared must not add a second claimed-reward entry.");
        }
    }
}

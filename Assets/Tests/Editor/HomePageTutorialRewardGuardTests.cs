using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.AI;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Data;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// TUTORIAL PLAYABLE SPINE, 2026-08-14 - proves HomePagePresenter.HandleMatchCompleted's
    /// reward guard AND GameBootstrap.HandleMatchEnded's progression guard through the REAL
    /// private paths: a real BattleController fires the real OnMatchEnded/OnMatchCompleted
    /// events, and each listener's real (still-private) handler runs, exactly as production
    /// does. BindBattleControllerForTests exists only to perform the same subscription Start()
    /// already does - Start() never fires in EditMode (no Play Mode lifecycle) - it does not
    /// touch or expose HandleMatchCompleted itself.
    ///
    /// TWO SEPARATE PROFILE INSTANCES, NOT ONE - this is not an EditMode-only quirk to work
    /// around, it is what production code actually does today, in both EditMode and Play Mode.
    /// GameBootstrap.Initialize() sets its own `_profile` from `Application.isPlaying ?
    /// SaveSystem.CurrentProfile : new PlayerProfile()` - in EditMode that is ALWAYS a fresh,
    /// separate `new PlayerProfile()`, never `SaveSystem.CurrentProfile`. HandleMatchEnded (the
    /// tutorial progression guard from this same slice) reads/writes THAT profile, exposed here
    /// via `bootstrap.Profile`. HomePagePresenter.HandleMatchCompleted (the older reward guard)
    /// reads/writes `SaveManager.SaveData`, a facade over `SaveSystem.CurrentProfile` - a
    /// DIFFERENT object entirely. Gold/gems/unlockedStageIds only ever flow through
    /// HandleMatchCompleted, so those assertions read `SaveManager.SaveData`.
    /// avatarLevel/totalMatches/totalWins/winStreak and the tutorial starter-card grant
    /// (GrantApprovedStarterCardsIfMissing, which writes directly into GameBootstrap's own
    /// `_profile.cardCollection`) only ever flow through GameBootstrap, so those assertions
    /// read `bootstrap.Profile`. Asserting the wrong one is a false pass/fail, not a stricter
    /// check - see the "totalMatches stayed 0" investigation this file's git history records.
    ///
    /// Kept separate from BattleLogicTests.cs because this is the one battle-adjacent test file
    /// that needs SaveSystem test isolation (HandleMatchCompleted reads/writes
    /// SaveManager.SaveData, a facade over SaveSystem.CurrentProfile) - Battle's own tests never
    /// touch the save system at all.
    /// </summary>
    public class HomePageTutorialRewardGuardTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            // Isolation for SaveManager.SaveData/Save(), which otherwise reads and writes the
            // real machine's save file - see SaveSystem.OverrideRootDirectoryForTests's own
            // comment. A fresh temp directory per test, never the real persistentDataPath.
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
            _spawned.Clear();

            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
            {
                Directory.Delete(_scratchSaveDir, recursive: true);
            }
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
            return bootstrap;
        }

        private HomePagePresenter SpawnHomePagePresenter(BattleController boundController)
        {
            var go = new GameObject("HomePagePresenterUnderTest");
            _spawned.Add(go);
            var presenter = go.AddComponent<HomePagePresenter>();
            presenter.BindBattleControllerForTests(boundController);
            return presenter;
        }

        /// <summary>Leaves every enemy lane empty on purpose - the same deterministic-win setup
        /// already relied on elsewhere in this suite (see LaneBattleResolver_...DamagesTheAvatar):
        /// an undefended lane lets the player's Front-lane Attack overflow to the enemy Avatar
        /// every tick, guaranteeing a fast, deterministic knockout well inside the tick cap.</summary>
        private static void RunToResolutionWithUndefendedEnemy(BattleController controller)
        {
            Assert.IsTrue(controller.ConfirmFormation(), "Setup: expected the Formation to lock legally.");
            int ticksRun = 0;
            while (controller.Phase == BattlePhase.Combat)
            {
                controller.AdvanceCombatTick();
                ticksRun++;
                Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks, "Setup: expected a knockout well inside the tick cap.");
            }
        }

        /// <summary>Same lock-and-tick loop as RunToResolutionWithUndefendedEnemy, under a
        /// neutral name - used by setups (e.g. TutorialDefeat) where the doc comment "enemy
        /// left undefended" would describe the wrong side.</summary>
        private static void LockFormationAndRunToResolution(BattleController controller)
        {
            Assert.IsTrue(controller.ConfirmFormation(), "Setup: expected the Formation to lock legally.");
            int ticksRun = 0;
            while (controller.Phase == BattlePhase.Combat)
            {
                controller.AdvanceCombatTick();
                ticksRun++;
                Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks, "Setup: expected a knockout well inside the tick cap.");
            }
        }

        [Test]
        public void TutorialVictory_ThroughTheRealRewardHandler_LeavesGoldGemsProgressionAndEntitlementsUnchanged()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("RewardGuard_TutorialBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            BattleController controller = bootstrap.Battle;

            HomePagePresenter presenter = SpawnHomePagePresenter(controller);

            // Two separate profiles - see this class's own doc comment. Reward fields
            // (gold/gems/unlockedStageIds) only ever flow through HomePagePresenter's
            // SaveManager.SaveData; progression fields (avatarLevel/totalMatches/totalWins/
            // winStreak) and the starter-card grant only ever flow through GameBootstrap's own
            // bootstrap.Profile. Both captured AFTER StartApprovedTutorialBattle() so the
            // approved starter-card grant (GrantApprovedStarterCardsIfMissing, unconditional and
            // unrelated to match outcome) is already reflected in the "before" snapshot - this
            // test proves the MATCH RESULT changes nothing further, not that the starter grant
            // itself never happens.
            PlayerProfile rewardProfile = SaveManager.SaveData;
            int goldBefore = rewardProfile.gold;
            int gemsBefore = rewardProfile.gems;
            int stageCountBefore = rewardProfile.unlockedStageIds.Count;

            PlayerProfile progressionProfile = bootstrap.Profile;
            int avatarLevelBefore = progressionProfile.avatarLevel;
            int totalMatchesBefore = progressionProfile.totalMatches;
            int totalWinsBefore = progressionProfile.totalWins;
            int winStreakBefore = progressionProfile.winStreak;
            var cardCollectionBefore = new List<string>(progressionProfile.cardCollection);

            bool? isVictory = null;
            controller.OnMatchCompleted += result => isVictory = result.IsVictory;

            foreach (Card card in controller.PlayerState.Hand.ToList())
            {
                Lane lane = card.Id switch { "warrior" => Lane.Front, "novice_knight" => Lane.Middle, _ => Lane.Back };
                controller.TryPlayCard(controller.PlayerState, card, lane);
            }
            // Enemy left undefended on purpose - see RunToResolutionWithUndefendedEnemy.
            RunToResolutionWithUndefendedEnemy(controller);

            Assert.IsTrue(isVictory, "Setup: expected the undefended enemy to produce a player victory.");
            Assert.AreEqual(goldBefore, rewardProfile.gold,
                "A tutorial victory must never change gold - the reward guard must have skipped the grant.");
            Assert.AreEqual(gemsBefore, rewardProfile.gems,
                "A tutorial victory must never change gems - the reward guard must have skipped the grant.");
            Assert.AreEqual(stageCountBefore, rewardProfile.unlockedStageIds.Count,
                "A tutorial victory must never unlock a stage.");
            Assert.AreEqual(avatarLevelBefore, progressionProfile.avatarLevel,
                "A tutorial victory must never change avatarLevel - GameBootstrap.HandleMatchEnded must have skipped RecordMatchResult.");
            Assert.AreEqual(totalMatchesBefore, progressionProfile.totalMatches,
                "A tutorial victory must never increment totalMatches.");
            Assert.AreEqual(totalWinsBefore, progressionProfile.totalWins,
                "A tutorial victory must never increment totalWins.");
            Assert.AreEqual(winStreakBefore, progressionProfile.winStreak,
                "A tutorial victory must never change winStreak.");
            CollectionAssert.AreEquivalent(cardCollectionBefore, progressionProfile.cardCollection,
                "A tutorial victory must not change cardCollection beyond the approved starter grant already captured above.");
        }

        [Test]
        public void TutorialDefeat_ThroughTheRealRewardHandler_LeavesGoldGemsProgressionAndEntitlementsUnchanged()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("RewardGuard_TutorialDefeatBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            BattleController controller = bootstrap.Battle;

            HomePagePresenter presenter = SpawnHomePagePresenter(controller);

            // Two separate profiles - see this class's own doc comment.
            PlayerProfile rewardProfile = SaveManager.SaveData;
            int goldBefore = rewardProfile.gold;
            int gemsBefore = rewardProfile.gems;
            int stageCountBefore = rewardProfile.unlockedStageIds.Count;

            PlayerProfile progressionProfile = bootstrap.Profile;
            int avatarLevelBefore = progressionProfile.avatarLevel;
            int totalMatchesBefore = progressionProfile.totalMatches;
            int totalWinsBefore = progressionProfile.totalWins;
            int winStreakBefore = progressionProfile.winStreak;
            var cardCollectionBefore = new List<string>(progressionProfile.cardCollection);

            bool? isVictory = null;
            controller.OnMatchCompleted += result => isVictory = result.IsVictory;

            // Roles reversed from RunToResolutionWithUndefendedEnemy: the player deploys only
            // its weakest card, into Back, leaving Front/Middle undefended; the enemy deploys
            // its whole (larger, by design harder) hand into Front/Middle. The enemy's combined
            // overflow into the player's undefended lanes each tick vastly exceeds the player's
            // single-card counter-overflow into the enemy's undefended Back, guaranteeing a
            // player defeat well inside the tick cap.
            Card weakestPlayerCard = controller.PlayerState.Hand.OrderBy(c => c.Attack).First();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, weakestPlayerCard, Lane.Back),
                "Setup: expected the player's one deployed card to legally occupy Back.");

            foreach (Card enemyCard in controller.EnemyState.Hand.ToList())
            {
                Lane lane = controller.EnemyState.Lanes[Lane.Front].Cards.Count < LaneState.MaxSlots ? Lane.Front : Lane.Middle;
                controller.TryPlayCard(controller.EnemyState, enemyCard, lane);
            }
            int enemyDeployedCount = controller.EnemyState.Lanes[Lane.Front].Cards.Count + controller.EnemyState.Lanes[Lane.Middle].Cards.Count;
            Assert.Greater(enemyDeployedCount, 0, "Setup: expected at least one enemy card to deploy into Front/Middle.");

            LockFormationAndRunToResolution(controller);

            Assert.IsFalse(isVictory, "Setup: expected the undefended player lanes to produce a player defeat.");
            Assert.AreEqual(goldBefore, rewardProfile.gold, "A tutorial defeat must never change gold.");
            Assert.AreEqual(gemsBefore, rewardProfile.gems, "A tutorial defeat must never change gems.");
            Assert.AreEqual(stageCountBefore, rewardProfile.unlockedStageIds.Count, "A tutorial defeat must never unlock a stage.");
            Assert.AreEqual(avatarLevelBefore, progressionProfile.avatarLevel,
                "A tutorial defeat must never change avatarLevel - GameBootstrap.HandleMatchEnded must have skipped RecordMatchResult.");
            Assert.AreEqual(totalMatchesBefore, progressionProfile.totalMatches,
                "A tutorial defeat must never increment totalMatches.");
            Assert.AreEqual(totalWinsBefore, progressionProfile.totalWins,
                "A tutorial defeat must never increment totalWins.");
            Assert.AreEqual(winStreakBefore, progressionProfile.winStreak,
                "A tutorial defeat must never change winStreak (RecordMatchResult resets it to 0 on a real loss - here it must not run at all).");
            CollectionAssert.AreEquivalent(cardCollectionBefore, progressionProfile.cardCollection,
                "A tutorial defeat must not change cardCollection beyond the approved starter grant already captured above.");
        }

        /// <summary>A normal (non-tutorial) match now requires a confirmed valid saved deck to
        /// deal any hand at all (release repair: TryBuildSavedPlayerDeck blocks instead of
        /// falling back to a generated deck) - this test's own subject is the reward grant on a
        /// normal victory, not the saved-deck contract itself, so this just satisfies that
        /// prerequisite the same way NormalBattleSavedDeckIntegrationTests/DeckPersistenceTests
        /// already do, rather than reimplementing or weakening it here.</summary>
        private static void SaveValidDeckForNormalMatch()
        {
            var databaseGo = new GameObject("RewardGuard_CardDatabase");
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.

            var sizingProfile = new PlayerProfile();
            sizingProfile.ApplyDataToEmpire();
            int deckSize = sizingProfile.Empire.DeckSlotCount;

            List<string> deckIds = database.AllCards.Select(c => c.Id)
                .Where(id => id != "warrior" && id != "novice_knight" && id != "goblin_caster")
                .Take(deckSize)
                .ToList();
            Assert.AreEqual(deckSize, deckIds.Count, "Setup: expected enough real cards to fill a full-size deck.");

            PlayerProfile profile = new PlayerProfile();
            profile.cardCollection = new List<string>(deckIds);
            profile.ApplyDataToEmpire();
            profile.activeDeckCardIds = new List<string>(deckIds);
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: the production save path must persist the confirmed deck.");
            SaveSystem.ResetCurrentProfileForTests();

            Object.DestroyImmediate(databaseGo);
        }

        [Test]
        public void NormalVictory_ThroughTheRealRewardHandler_GrantsNoGoldOrGems()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("RewardGuard_NormalBootstrap");
            Assert.IsFalse(bootstrap.IsTutorialMatch, "Setup: expected a freshly-initialized match to not be tutorial-flagged.");
            BattleController controller = bootstrap.Battle;

            HomePagePresenter presenter = SpawnHomePagePresenter(controller);

            // Two separate profiles - see this class's own doc comment. gold/gems only ever
            // flow through HomePagePresenter's SaveManager.SaveData; totalMatches/totalWins
            // only ever flow through GameBootstrap.HandleMatchEnded's own bootstrap.Profile.
            PlayerProfile rewardProfile = SaveManager.SaveData;
            int goldBefore = rewardProfile.gold;
            int gemsBefore = rewardProfile.gems;

            PlayerProfile progressionProfile = bootstrap.Profile;
            int totalMatchesBefore = progressionProfile.totalMatches;
            int totalWinsBefore = progressionProfile.totalWins;

            bool? isVictory = null;
            controller.OnMatchCompleted += result => isVictory = result.IsVictory;

            Card anyCard = controller.PlayerState.Hand.First(c => c.ResourceCost <= controller.PlayerState.Resource);
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, anyCard, Lane.Front),
                "Setup: expected to be able to play at least one card into Front.");
            // Enemy left undefended on purpose - see RunToResolutionWithUndefendedEnemy.
            RunToResolutionWithUndefendedEnemy(controller);

            Assert.IsTrue(isVictory, "Setup: expected the undefended enemy to produce a player victory.");
            Assert.AreEqual(goldBefore, rewardProfile.gold,
                "A normal non-campaign victory must not grant gold.");
            Assert.AreEqual(gemsBefore, rewardProfile.gems,
                "A normal non-campaign victory must not grant gems.");
            // Regression proof for the new `if (!IsTutorialMatch)` guard in
            // GameBootstrap.HandleMatchEnded: an ordinary match must still call
            // RecordMatchResult exactly as before - these must have actually changed, not just
            // "not been broken".
            Assert.AreEqual(totalMatchesBefore + 1, progressionProfile.totalMatches,
                "A normal match's existing totalMatches increment must be unaffected by the tutorial guard.");
            Assert.AreEqual(totalWinsBefore + 1, progressionProfile.totalWins,
                "A normal victory's existing totalWins increment must be unaffected by the tutorial guard.");
        }

        /// <summary>
        /// Home IA rebuild, 2026-08-27 (register: "LOCKED: Home IA rebuild") - the standalone
        /// TutorialStrip/HomeFeatureRoot region is gone; the tutorial invite is now the first
        /// card in Home's swipeable feed (register: "Tutorial banner: new player only, first few
        /// days, then converts to the Events feed card"), gated on profile.totalMatches == 0 (the
        /// closest real existing signal - no real "days since install" field exists). A fresh
        /// SetUp profile via SaveSystem.ResetCurrentProfileForTests() has totalMatches == 0, so
        /// the WELCOME card is expected to be present here.
        /// </summary>
        [Test]
        public void HomeTutorialStrip_ShowsApprovedCopyAndStartTutorialButtonEntersTheApprovedTutorialBattle()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("TutorialStrip_Bootstrap");
            Assert.IsFalse(bootstrap.IsTutorialMatch, "Setup: expected a freshly-initialized match to not be tutorial-flagged yet.");

            HomePagePresenter presenter = SpawnHomePagePresenter(bootstrap.Battle);
            presenter.BuildHomePageUIForTests();

            GameObject homeCanvas = presenter.HomeCanvasObjectForTests;
            Assert.IsNotNull(homeCanvas, "Setup: expected Home's canvas to exist.");

            Transform welcomeCard = homeCanvas.transform.Find("ContentPanel/HomeFeedCanvas/HomeFeed/Viewport/Content/FeedCard_WELCOME");
            Assert.IsNotNull(welcomeCard, "Home's feed must contain a WELCOME card for a new (totalMatches==0) player.");

            Text tutorialCopy = welcomeCard.Find("Body")?.GetComponent<Text>();
            Assert.IsNotNull(tutorialCopy, "The WELCOME card must contain its feature copy text.");
            Assert.AreEqual(HomePagePresenter.HomeFeatureTutorialInviteCopy, tutorialCopy.text,
                "The WELCOME card's body must invite Start Tutorial for a new player.");

            Button startTutorialButton = welcomeCard.Find("PrimaryAction")?.GetComponent<Button>();
            Assert.IsNotNull(startTutorialButton, "The WELCOME card must contain a real Start Tutorial primary action.");

            startTutorialButton.onClick.Invoke();

            Assert.IsTrue(bootstrap.IsTutorialMatch,
                "A real click on the Start Tutorial button must reach the real tutorial entry path (StartApprovedTutorialBattle), not a stub.");
        }
    }
}

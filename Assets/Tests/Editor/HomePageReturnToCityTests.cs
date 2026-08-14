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
    /// BATTLE TO HOME RETURN HANDOFF, 2026-08-15 - proves GameBootstrap.OnReturnToCityRequested
    /// through the REAL private path: GameBootstrap.ReturnToCityForTests() calls the real
    /// (still-private) OnReturnToCityPressed(), which hides the battle canvas and fires the real
    /// event; HomePagePresenter's real (still-private) HandleReturnToCityRequested handles it,
    /// exactly as production does. BindGameBootstrapForTests exists only to perform the same
    /// subscription Start() already does - Start() never fires in EditMode (no Play Mode
    /// lifecycle) - it does not touch or expose HandleReturnToCityRequested itself.
    ///
    /// Two separate profiles, same as HomePageTutorialRewardGuardTests.cs - GameBootstrap's own
    /// `bootstrap.Profile` (avatarLevel/totalMatches/totalWins/winStreak/cardCollection) versus
    /// `SaveManager.SaveData` (gold/gems/unlockedStageIds), see that file's own doc comment for
    /// why. This file exists to prove the NEW return-to-city handoff never touches either -
    /// not to re-derive the reward/progression guards themselves, which are already covered
    /// there.
    /// </summary>
    public class HomePageReturnToCityTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
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
                GameObject spawned = GameObject.Find(spawnedName);
                if (spawned != null) _spawned.Add(spawned);
            }
            return bootstrap;
        }

        /// <summary>Builds a real Home canvas (via BuildHomePageUIForTests, since Start() never
        /// fires in EditMode) and binds it to the given bootstrap's return event, exactly as
        /// Start() does in production.</summary>
        private HomePagePresenter SpawnAndBindHomePagePresenter(GameBootstrap bootstrap)
        {
            var go = new GameObject("HomePagePresenterUnderTest");
            _spawned.Add(go);
            var presenter = go.AddComponent<HomePagePresenter>();
            presenter.BuildHomePageUIForTests();
            presenter.BindGameBootstrapForTests(bootstrap);
            return presenter;
        }

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

        /// <summary>Player deploys only its weakest card into Back; enemy deploys its whole hand
        /// into Front/Middle - guarantees a player defeat well inside the tick cap. Same setup
        /// HomePageTutorialRewardGuardTests.TutorialDefeat_... uses.</summary>
        private static void RunToResolutionWithUndefendedPlayer(BattleController controller)
        {
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
        public void NormalBattleReturn_RestoresHomeCanvas()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("ReturnHandoff_NormalBootstrap");
            HomePagePresenter presenter = SpawnAndBindHomePagePresenter(bootstrap);

            GameObject homeCanvas = presenter.HomeCanvasObjectForTests;
            Assert.IsNotNull(homeCanvas, "Setup: expected Home's canvas to exist.");
            // Mirrors OnToBattleClicked's own behavior - Home hides itself before battle starts.
            homeCanvas.SetActive(false);

            bootstrap.ReturnToCityForTests();

            Assert.IsTrue(homeCanvas.activeSelf, "Return to City must restore Home's canvas.");
        }

        [Test]
        public void TutorialVictoryReturn_RestoresHomeCanvas_WithoutFurtherRewardOrProgressionChange()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("ReturnHandoff_TutorialVictoryBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            BattleController controller = bootstrap.Battle;
            HomePagePresenter presenter = SpawnAndBindHomePagePresenter(bootstrap);
            GameObject homeCanvas = presenter.HomeCanvasObjectForTests;

            // Subscribes the real reward guard too, exactly as Start() would.
            presenter.BindBattleControllerForTests(controller);

            foreach (Card card in controller.PlayerState.Hand.ToList())
            {
                Lane lane = card.Id switch { "warrior" => Lane.Front, "novice_knight" => Lane.Middle, _ => Lane.Back };
                controller.TryPlayCard(controller.PlayerState, card, lane);
            }
            RunToResolutionWithUndefendedEnemy(controller);

            homeCanvas.SetActive(false);

            // Captured AFTER the match has fully resolved (reward/progression guards already
            // ran) so this test proves ReturnToCityForTests itself changes nothing further.
            PlayerProfile rewardProfile = SaveManager.SaveData;
            int goldAfterMatch = rewardProfile.gold;
            int gemsAfterMatch = rewardProfile.gems;
            int stageCountAfterMatch = rewardProfile.unlockedStageIds.Count;

            PlayerProfile progressionProfile = bootstrap.Profile;
            int avatarLevelAfterMatch = progressionProfile.avatarLevel;
            int totalMatchesAfterMatch = progressionProfile.totalMatches;
            int totalWinsAfterMatch = progressionProfile.totalWins;
            int winStreakAfterMatch = progressionProfile.winStreak;

            bootstrap.ReturnToCityForTests();

            Assert.IsTrue(homeCanvas.activeSelf, "Return to City must restore Home's canvas after a tutorial victory.");
            Assert.AreEqual(goldAfterMatch, rewardProfile.gold, "Return to City must not change gold.");
            Assert.AreEqual(gemsAfterMatch, rewardProfile.gems, "Return to City must not change gems.");
            Assert.AreEqual(stageCountAfterMatch, rewardProfile.unlockedStageIds.Count, "Return to City must not unlock a stage.");
            Assert.AreEqual(avatarLevelAfterMatch, progressionProfile.avatarLevel, "Return to City must not change avatarLevel.");
            Assert.AreEqual(totalMatchesAfterMatch, progressionProfile.totalMatches, "Return to City must not change totalMatches.");
            Assert.AreEqual(totalWinsAfterMatch, progressionProfile.totalWins, "Return to City must not change totalWins.");
            Assert.AreEqual(winStreakAfterMatch, progressionProfile.winStreak, "Return to City must not change winStreak.");
        }

        [Test]
        public void TutorialDefeatReturn_RestoresHomeCanvas_WithoutFurtherRewardOrProgressionChange()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("ReturnHandoff_TutorialDefeatBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            BattleController controller = bootstrap.Battle;
            HomePagePresenter presenter = SpawnAndBindHomePagePresenter(bootstrap);
            GameObject homeCanvas = presenter.HomeCanvasObjectForTests;

            presenter.BindBattleControllerForTests(controller);

            RunToResolutionWithUndefendedPlayer(controller);

            homeCanvas.SetActive(false);

            PlayerProfile rewardProfile = SaveManager.SaveData;
            int goldAfterMatch = rewardProfile.gold;
            int gemsAfterMatch = rewardProfile.gems;
            int stageCountAfterMatch = rewardProfile.unlockedStageIds.Count;

            PlayerProfile progressionProfile = bootstrap.Profile;
            int avatarLevelAfterMatch = progressionProfile.avatarLevel;
            int totalMatchesAfterMatch = progressionProfile.totalMatches;
            int totalWinsAfterMatch = progressionProfile.totalWins;
            int winStreakAfterMatch = progressionProfile.winStreak;

            bootstrap.ReturnToCityForTests();

            Assert.IsTrue(homeCanvas.activeSelf, "Return to City must restore Home's canvas after a tutorial defeat.");
            Assert.AreEqual(goldAfterMatch, rewardProfile.gold, "Return to City must not change gold.");
            Assert.AreEqual(gemsAfterMatch, rewardProfile.gems, "Return to City must not change gems.");
            Assert.AreEqual(stageCountAfterMatch, rewardProfile.unlockedStageIds.Count, "Return to City must not unlock a stage.");
            Assert.AreEqual(avatarLevelAfterMatch, progressionProfile.avatarLevel, "Return to City must not change avatarLevel.");
            Assert.AreEqual(totalMatchesAfterMatch, progressionProfile.totalMatches, "Return to City must not change totalMatches.");
            Assert.AreEqual(totalWinsAfterMatch, progressionProfile.totalWins, "Return to City must not change totalWins.");
            Assert.AreEqual(winStreakAfterMatch, progressionProfile.winStreak, "Return to City must not change winStreak.");
        }

        [Test]
        public void ReturnToCity_WithNoHomeSubscriber_DoesNotThrow()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("ReturnHandoff_NoSubscriberBootstrap");
            // No HomePagePresenter spawned/bound at all - OnReturnToCityRequested has zero
            // subscribers, matching the null-conditional `?.Invoke()` safety every other
            // event/handoff in this codebase already relies on.
            Assert.DoesNotThrow(() => bootstrap.ReturnToCityForTests());
        }
    }
}

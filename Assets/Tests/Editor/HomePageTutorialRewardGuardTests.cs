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

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// TUTORIAL PLAYABLE SPINE, 2026-08-14 - proves HomePagePresenter.HandleMatchCompleted's
    /// reward guard through the REAL private path: a real BattleController fires the real
    /// OnMatchCompleted event, HomePagePresenter's real (still-private) HandleMatchCompleted
    /// handles it, exactly as production does. BindBattleControllerForTests exists only to
    /// perform the same subscription Start() already does - Start() never fires in EditMode
    /// (no Play Mode lifecycle) - it does not touch or expose HandleMatchCompleted itself.
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
                GameObject spawned = GameObject.Find(spawnedName);
                if (spawned != null) _spawned.Add(spawned);
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

        [Test]
        public void TutorialVictory_ThroughTheRealRewardHandler_LeavesGoldGemsAndStageUnlocksUnchanged()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("RewardGuard_TutorialBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            BattleController controller = bootstrap.Battle;

            HomePagePresenter presenter = SpawnHomePagePresenter(controller);

            PlayerProfile profile = SaveManager.SaveData;
            int goldBefore = profile.gold;
            int gemsBefore = profile.gems;
            int stageCountBefore = profile.unlockedStageIds.Count;

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
            Assert.AreEqual(goldBefore, profile.gold,
                "A tutorial victory must never change gold - the reward guard must have skipped the grant.");
            Assert.AreEqual(gemsBefore, profile.gems,
                "A tutorial victory must never change gems - the reward guard must have skipped the grant.");
            Assert.AreEqual(stageCountBefore, profile.unlockedStageIds.Count,
                "A tutorial victory must never unlock a stage.");
        }

        [Test]
        public void NormalVictory_ThroughTheRealRewardHandler_StillGrantsGoldAndGems()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("RewardGuard_NormalBootstrap");
            Assert.IsFalse(bootstrap.IsTutorialMatch, "Setup: expected a freshly-initialized match to not be tutorial-flagged.");
            BattleController controller = bootstrap.Battle;

            HomePagePresenter presenter = SpawnHomePagePresenter(controller);

            PlayerProfile profile = SaveManager.SaveData;
            int goldBefore = profile.gold;
            int gemsBefore = profile.gems;

            bool? isVictory = null;
            controller.OnMatchCompleted += result => isVictory = result.IsVictory;

            Card anyCard = controller.PlayerState.Hand.First(c => c.ResourceCost <= controller.PlayerState.Resource);
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, anyCard, Lane.Front),
                "Setup: expected to be able to play at least one card into Front.");
            // Enemy left undefended on purpose - see RunToResolutionWithUndefendedEnemy.
            RunToResolutionWithUndefendedEnemy(controller);

            Assert.IsTrue(isVictory, "Setup: expected the undefended enemy to produce a player victory.");
            // 250 gold / 25 gems are HandleMatchCompleted's own existing fallback constants
            // (currentActiveStage == null in this test) - asserted directly here because this
            // test's whole purpose is confirming that PRE-EXISTING behavior is unaffected by the
            // new tutorial guard, not validating a value this change introduces.
            Assert.AreEqual(goldBefore + 250, profile.gold, "A normal victory's existing gold reward must be unaffected by the tutorial guard.");
            Assert.AreEqual(gemsBefore + 25, profile.gems, "A normal victory's existing gems reward must be unaffected by the tutorial guard.");
        }
    }
}

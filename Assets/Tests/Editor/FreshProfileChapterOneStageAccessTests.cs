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
    /// FRESH-PROFILE CHAPTER 1 STAGE-ACCESS CONTRACT (release feature) - proves a brand-new
    /// profile starts with only Stage 1-1 unlocked (previously defaulted to {"1-1","1-2"} -
    /// PlayerProfile.unlockedStageIds), and that the existing sequential unlock chain (win 1-1 ->
    /// unlock 1-2 -> win 1-2 -> unlock 1-3, via the real HomePagePresenter.HandleMatchCompleted /
    /// CampaignMapPresenter.GetNextStageId - the sole order authority, no second stage-order
    /// system) is the only way further stages ever become reachable. Reuses exactly the same
    /// production owners and test techniques as ChapterOneProgressionTests.
    /// </summary>
    public class FreshProfileChapterOneStageAccessTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsFreshStageAccess_" + System.Guid.NewGuid().ToString("N"));
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

        private static void SaveValidDeckForNormalMatch()
        {
            var databaseGo = new GameObject("FreshStageAccess_CardDatabase");
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();

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
            var go = new GameObject("HomePagePresenterUnderTest_FreshStageAccess");
            _spawned.Add(go);
            var presenter = go.AddComponent<HomePagePresenter>();
            presenter.BindBattleControllerForTests(boundController);
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

        private static CampaignStageData FindStage(string stageId)
        {
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests(stageId);
            Assert.IsNotNull(stage, $"Setup: expected Stage {stageId} to exist in the production campaign list.");
            return stage;
        }

        [Test]
        public void BrandNewProfile_StartsWithOnlyStage1_1Unlocked()
        {
            var profile = new PlayerProfile();

            CollectionAssert.AreEquivalent(new[] { "1-1" }, profile.unlockedStageIds,
                "Requirement 1: a brand-new profile must start with only Stage 1-1 unlocked.");
        }

        [Test]
        public void Winning1_1_UnlocksOnly1_2()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("FreshStageAccess_Win11Bootstrap");
            BattleController controller = bootstrap.Battle;
            HomePagePresenter presenter = SpawnHomePagePresenter(controller);
            presenter.SetActiveStageForTests(FindStage("1-1"));

            PlayerProfile profile = SaveManager.SaveData;
            CollectionAssert.AreEquivalent(new[] { "1-1" }, profile.unlockedStageIds, "Setup: expected the fresh saved profile to start with only Stage 1-1 unlocked.");

            PlayOneCardAndWin(controller);

            CollectionAssert.AreEquivalent(new[] { "1-1", "1-2" }, profile.unlockedStageIds,
                "Requirement 2: winning Stage 1-1 must unlock exactly Stage 1-2, nothing more.");
        }

        [Test]
        public void Winning1_2_AfterWinning1_1_UnlocksOnly1_3()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("FreshStageAccess_Win12Bootstrap");
            BattleController controller = bootstrap.Battle;
            HomePagePresenter presenter = SpawnHomePagePresenter(controller);

            presenter.SetActiveStageForTests(FindStage("1-1"));
            PlayOneCardAndWin(controller);

            bootstrap.PlayAgainForTests();
            controller = bootstrap.Battle;
            presenter.SetActiveStageForTests(FindStage("1-2"));
            PlayOneCardAndWin(controller);

            PlayerProfile profile = SaveManager.SaveData;
            CollectionAssert.AreEquivalent(new[] { "1-1", "1-2", "1-3" }, profile.unlockedStageIds,
                "Requirement 3: winning Stage 1-2 (reached by first winning 1-1) must unlock exactly Stage 1-3.");
        }

        [Test]
        public void Winning1_3_NowUnlocksTheStartOfChapter2_2_1_NotNothing()
        {
            // Chapter 2 "Ashes of Boiotia" (2026-08-22): 1-3 is no longer the last stage in the
            // existing ordered campaign list - CampaignMapPresenter.chapterStages now continues
            // 1-3 -> 2-1 -> 2-2 -> 2-3. This replaces the old "1-3 is terminal" assertion this
            // test used to make (Winning1_3_DoesNotInventANonexistentNextStage) - the underlying
            // contract (a win must unlock exactly the real next stage in the list, never invent
            // one) is unchanged; only which stage is "next" after 1-3 changed.
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("FreshStageAccess_Win13Bootstrap");
            BattleController controller = bootstrap.Battle;
            HomePagePresenter presenter = SpawnHomePagePresenter(controller);

            presenter.SetActiveStageForTests(FindStage("1-1"));
            PlayOneCardAndWin(controller);
            bootstrap.PlayAgainForTests();
            controller = bootstrap.Battle;
            presenter.SetActiveStageForTests(FindStage("1-2"));
            PlayOneCardAndWin(controller);
            bootstrap.PlayAgainForTests();
            controller = bootstrap.Battle;
            presenter.SetActiveStageForTests(FindStage("1-3"));

            Assert.AreEqual("2-1", CampaignMapPresenter.GetNextStageId("1-3"),
                "Setup: expected Stage 2-1 to be the real next stage after 1-3 in the existing ordered campaign list.");
            PlayOneCardAndWin(controller);

            PlayerProfile profile = SaveManager.SaveData;
            CollectionAssert.AreEquivalent(new[] { "1-1", "1-2", "1-3", "2-1" }, profile.unlockedStageIds,
                "Requirement 4: winning Stage 1-3 must unlock exactly the real next stage (2-1), never nothing and never an invented id.");
        }

        [Test]
        public void Winning2_3_DoesNotInventANonexistentNextStage()
        {
            // 2-3 is the new terminal stage in the existing ordered campaign list - the same
            // "final stage invents nothing further" contract Chapter 1's own 1-3 used to be the
            // subject of, now proven against the real current end of the list instead.
            Assert.IsNull(CampaignMapPresenter.GetNextStageId("2-3"),
                "Setup: expected Stage 2-3 to be the last stage in the existing ordered campaign list.");
        }

        [Test]
        public void ExistingPlayer_WithFurtherStagesAlreadyUnlocked_KeepsThemAfterLoad()
        {
            // Simulates an existing, already-progressed player - their real saved JSON already
            // contains every stage id they earned, overwriting PlayerProfile's own field
            // initializer during deserialization regardless of what that default is.
            var existingProfile = new PlayerProfile
            {
                unlockedStageIds = new List<string> { "1-1", "1-2", "1-3" }
            };
            Assert.IsTrue(SaveSystem.Save(existingProfile), "Setup: expected the existing-player profile to save.");
            SaveSystem.ResetCurrentProfileForTests();

            PlayerProfile reloaded = SaveSystem.Load();

            CollectionAssert.AreEquivalent(new[] { "1-1", "1-2", "1-3" }, reloaded.unlockedStageIds,
                "Requirement 5: an existing player's already-unlocked stages must never be lost on load, regardless of the fresh-profile default.");
        }

        [Test]
        public void Defeat_AndTutorialVictory_NeverUnlockAnyStage()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("FreshStageAccess_IsolationBootstrap");
            BattleController controller = bootstrap.Battle;
            HomePagePresenter presenter = SpawnHomePagePresenter(controller);
            presenter.SetActiveStageForTests(FindStage("1-1"));

            PlayerProfile profile = SaveManager.SaveData;
            DeployWeaklyAndLose(controller);

            CollectionAssert.AreEquivalent(new[] { "1-1" }, profile.unlockedStageIds,
                "Requirement 6: a stage defeat must never unlock any stage beyond the fresh default.");

            bootstrap.StartApprovedTutorialBattle();
            controller = bootstrap.Battle;
            foreach (Card card in controller.PlayerState.Hand.ToList())
            {
                Lane lane = card.Id switch { "warrior" => Lane.Front, "novice_knight" => Lane.Middle, _ => Lane.Back };
                controller.TryPlayCard(controller.PlayerState, card, lane);
            }
            Assert.IsTrue(controller.ConfirmFormation(), "Setup: expected the tutorial Formation to lock legally.");
            int ticksRun = 0;
            while (controller.Phase == BattlePhase.Combat)
            {
                controller.AdvanceCombatTick();
                ticksRun++;
                Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks, "Setup: expected a tutorial knockout inside the tick cap.");
            }

            CollectionAssert.AreEquivalent(new[] { "1-1" }, profile.unlockedStageIds,
                "Requirement 6: a tutorial victory must never unlock any campaign stage.");
        }

        [Test]
        public void FreshDefault_AndFirstUnlock_BothSurviveAReload()
        {
            var freshProfile = new PlayerProfile();
            Assert.IsTrue(SaveSystem.Save(freshProfile), "Setup: expected the brand-new profile to save.");
            SaveSystem.ResetCurrentProfileForTests();

            PlayerProfile reloadedFresh = SaveSystem.Load();
            CollectionAssert.AreEquivalent(new[] { "1-1" }, reloadedFresh.unlockedStageIds,
                "Requirement 7: the fresh-profile default itself must survive a reload unchanged.");

            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("FreshStageAccess_ReloadBootstrap");
            BattleController controller = bootstrap.Battle;
            HomePagePresenter presenter = SpawnHomePagePresenter(controller);
            presenter.SetActiveStageForTests(FindStage("1-1"));
            PlayOneCardAndWin(controller);

            PlayerProfile reloadedAfterWin = SaveSystem.Load();
            CollectionAssert.AreEquivalent(new[] { "1-1", "1-2" }, reloadedAfterWin.unlockedStageIds,
                "Requirement 7: the first real unlock (1-1 -> 1-2) must also survive a reload.");
        }
    }
}

using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Data;
using MyriadOfDragons.Save;
using MyriadOfDragons.Story;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Chapter 1 post-victory story bridges: each stage has a "&lt;id&gt;_post" sequence, and
    /// HomePagePresenter.HandleMatchCompleted plays it on first clear only (never on defeat,
    /// replay, tutorial, or non-campaign victories).
    /// </summary>
    public class ChapterOnePostVictoryStoryTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsCh1PostStory_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            StoryOverlayPresenter.ResetTestHooks();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();
            StoryOverlayPresenter.ResetTestHooks();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
            {
                Directory.Delete(_scratchSaveDir, recursive: true);
            }
        }

        private static void SaveValidDeckForNormalMatch()
        {
            var databaseGo = new GameObject("PostStory_CardDatabase");
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
            var go = new GameObject("HomePagePresenter_PostStory");
            _spawned.Add(go);
            var presenter = go.AddComponent<HomePagePresenter>();
            presenter.BindBattleControllerForTests(boundController);
            return presenter;
        }

        private static void PlayOneCardAndWin(BattleController controller)
        {
            Card anyCard = controller.PlayerState.Hand.First(c => c.ResourceCost <= controller.PlayerState.Resource);
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, anyCard, Lane.Front));
            Assert.IsTrue(controller.ConfirmFormation());
            int ticksRun = 0;
            while (controller.Phase == BattlePhase.Combat)
            {
                controller.AdvanceCombatTick();
                ticksRun++;
                Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks);
            }
        }

        private static CampaignStageData FindStage(string stageId)
        {
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests(stageId);
            Assert.IsNotNull(stage, $"Setup: expected Stage {stageId}.");
            return stage;
        }

        [Test]
        public void StoryDatabase_DefinesPostVictorySequencesForAllChapter1Stages()
        {
            foreach (string stageId in new[] { "1-1", "1-2", "1-3" })
            {
                string key = HomePagePresenter.GetCampaignPostVictoryStoryKey(stageId);
                StorySequence seq = StoryDatabase.GetSequence(key);
                Assert.IsNotNull(seq, $"Expected StoryDatabase entry '{key}'.");
                Assert.Greater(seq.lines.Count, 0, $"'{key}' must have at least one dialogue line.");
            }
        }

        [Test]
        public void FirstClear_PlaysMatchingPostVictoryStory_WithoutBlockingRewards()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("PostStory_FirstClear");
            BattleController controller = bootstrap.Battle;
            HomePagePresenter presenter = SpawnHomePagePresenter(controller);
            presenter.SetActiveStageForTests(FindStage("1-2"));

            PlayerProfile profile = SaveManager.SaveData;
            int goldBefore = profile.gold;

            PlayOneCardAndWin(controller);

            Assert.AreEqual("1-2_post", StoryOverlayPresenter.LastPlayedSequenceIdForTests,
                "A first clear must request the matching post-victory story sequence.");
            Assert.AreEqual(goldBefore + FindStage("1-2").goldReward, profile.gold,
                "Post-victory story must not block or replace the gold grant.");
            CollectionAssert.Contains(profile.claimedStageRewardIds, "1-2");
        }

        [Test]
        public void ReplayingAClearedStage_DoesNotReplayPostVictoryStory()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("PostStory_Replay");
            BattleController controller = bootstrap.Battle;
            HomePagePresenter presenter = SpawnHomePagePresenter(controller);
            presenter.SetActiveStageForTests(FindStage("1-1"));

            PlayOneCardAndWin(controller);
            Assert.AreEqual("1-1_post", StoryOverlayPresenter.LastPlayedSequenceIdForTests);

            StoryOverlayPresenter.ResetTestHooks();
            bootstrap.PlayAgainForTests();
            controller = bootstrap.Battle;
            PlayOneCardAndWin(controller);

            Assert.IsNull(StoryOverlayPresenter.LastPlayedSequenceIdForTests,
                "A replay of an already-claimed stage must not re-fire the post-victory story.");
        }

        [Test]
        public void NonCampaignVictory_DoesNotPlayCampaignPostVictoryStory()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("PostStory_NormalVictory");
            BattleController controller = bootstrap.Battle;
            SpawnHomePagePresenter(controller);
            // No SetActiveStageForTests - ordinary normal Battle victory path.

            PlayOneCardAndWin(controller);

            Assert.IsNull(StoryOverlayPresenter.LastPlayedSequenceIdForTests,
                "A non-campaign victory must not play a Campaign post-victory story.");
        }
    }
}

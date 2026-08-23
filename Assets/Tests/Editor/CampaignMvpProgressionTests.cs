using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>MVP campaign window + progression self-check (1-1 → 1-2 → 1-3).</summary>
    public class CampaignMvpProgressionTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsMvpProg_" + System.Guid.NewGuid().ToString("N"));
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
                Directory.Delete(_scratchSaveDir, true);
        }

        [Test]
        public void FreshProfile_MvpWindow_ShowsOnlyTwoStages()
        {
            var profile = new PlayerProfile();
            var window = CampaignMapPresenter.GetMvpWindowStagesForTests(1, profile);

            Assert.LessOrEqual(window.Count, 3);
            Assert.GreaterOrEqual(window.Count, 2);
            Assert.AreEqual("1-1", window[0].stageId);
            Assert.AreEqual("1-2", window[1].stageId);
            Assert.IsTrue(window[0].isUnlocked);
            Assert.IsFalse(window[1].isUnlocked);
        }

        [Test]
        public void Chapter2_MvpWindow_UsesSameThreeNodePattern()
        {
            var profile = new PlayerProfile();
            profile.unlockedStageIds = new List<string> { "2-1" };
            var window = CampaignMapPresenter.GetMvpWindowStagesForTests(2, profile);

            Assert.LessOrEqual(window.Count, 3);
            Assert.GreaterOrEqual(window.Count, 2);
            Assert.AreEqual("2-1", window[0].stageId);
            Assert.IsTrue(window[0].isUnlocked);
            Assert.IsTrue(window.Any(s => s.stageId == "2-2"));
        }

        [Test]
        public void Win1_1_Unlocks1_2_AndMvpWindowAdvances()
        {
            SaveValidDeck();
            GameBootstrap bootstrap = SpawnBootstrap("MvpWinBootstrap");
            BattleController battle = bootstrap.Battle;
            HomePagePresenter home = SpawnHome(battle);
            home.SetActiveStageForTests(CampaignMapPresenter.GetStageForTests("1-1"));

            bool? victory = null;
            battle.OnMatchCompleted += r => victory = r.IsVictory;
            PlayOneCardAndWin(battle);

            Assert.IsTrue(victory, "Setup: expected a deterministic player victory.");
            PlayerProfile profile = SaveSystem.CurrentProfile;
            CollectionAssert.Contains(profile.unlockedStageIds, "1-2");

            var window = CampaignMapPresenter.GetMvpWindowStagesForTests(1, profile);
            Assert.IsTrue(window.Any(s => s.stageId == "1-2" && s.isUnlocked));
        }

        [Test]
        public void CampaignMap_RendersMvpNodeCount_NotFullChapter()
        {
            var go = new GameObject("MapMvpHarness");
            _spawned.Add(go);
            var map = go.AddComponent<CampaignMapPresenter>();
            map.Initialize(null, _ => CampaignLaunchOutcome.BlockedLocked);

            Transform content = map.StageNodesContentForTests;
            Assert.NotNull(content);
            Assert.LessOrEqual(content.childCount, 3);
            Assert.NotNull(content.Find("StageNode_1-1"));
            Assert.NotNull(content.Find("StageNode_1-2"));
            Assert.IsNull(content.Find("StageNode_1-12"));
            Assert.IsNull(content.Find("StageNode_10-1"), "Chapter 1 MVP view must not render Chapter 10 nodes.");

            Object.DestroyImmediate(GameObject.Find("CampaignMapCanvas"));
        }

        private GameBootstrap SpawnBootstrap(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            var bootstrap = go.AddComponent<GameBootstrap>();
            bootstrap.Initialize();
            foreach (string spawnedName in new[] { "Canvas", "EventSystem", "CardDatabase", "BattleController" })
            {
                GameObject spawned = GameObject.Find(spawnedName);
                if (spawned != null) _spawned.Add(spawned);
            }
            return bootstrap;
        }

        private HomePagePresenter SpawnHome(BattleController battle)
        {
            var go = new GameObject("HomeMvp");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            home.BindBattleControllerForTests(battle);
            return home;
        }

        private static void PlayOneCardAndWin(BattleController controller)
        {
            Card anyCard = controller.PlayerState.Hand.First(c => c.ResourceCost <= controller.PlayerState.Resource);
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, anyCard, Lane.Front));
            Assert.IsTrue(controller.ConfirmFormation());

            int ticks = 0;
            while (controller.Phase == BattlePhase.Combat)
            {
                controller.AdvanceCombatTick();
                ticks++;
                Assert.LessOrEqual(ticks, BattleController.MaxCombatTicks);
            }
        }

        private static void SaveValidDeck()
        {
            var databaseGo = new GameObject("Mvp_CardDatabase");
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();

            var sizingProfile = new PlayerProfile();
            sizingProfile.ApplyDataToEmpire();
            int deckSize = sizingProfile.Empire.DeckSlotCount;
            List<string> deckIds = database.AllCards.Select(c => c.Id)
                .Where(id => id != "warrior" && id != "novice_knight" && id != "goblin_caster")
                .Take(deckSize).ToList();

            var profile = new PlayerProfile();
            profile.cardCollection = new List<string>(deckIds);
            profile.activeDeckCardIds = new List<string>(deckIds);
            Assert.IsTrue(SaveSystem.Save(profile));
            SaveSystem.ResetCurrentProfileForTests();
            Object.DestroyImmediate(databaseGo);
        }
    }
}

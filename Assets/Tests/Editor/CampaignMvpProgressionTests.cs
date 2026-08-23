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
        public void FreshProfile_MvpWindow_ShowsFrontierAndOneLockedTeaser()
        {
            var profile = new PlayerProfile();
            var window = CampaignMapPresenter.GetMvpWindowStagesForTests(1, profile);

            Assert.AreEqual(2, window.Count);
            Assert.AreEqual("1-1", window[0].stageId);
            Assert.AreEqual("1-2", window[1].stageId);
            Assert.IsTrue(window[0].isUnlocked);
            Assert.IsFalse(window[1].isUnlocked);
        }

        [Test]
        public void Chapter2_MvpWindow_KeepsUnlockedPlusOneTeaser()
        {
            var profile = new PlayerProfile();
            profile.unlockedStageIds = new List<string> { "2-1" };
            var window = CampaignMapPresenter.GetMvpWindowStagesForTests(2, profile);

            Assert.AreEqual(2, window.Count);
            Assert.AreEqual("2-1", window[0].stageId);
            Assert.IsTrue(window[0].isUnlocked);
            Assert.IsTrue(window.Any(s => s.stageId == "2-2"));
            Assert.IsFalse(window.Any(s => s.stageId == "2-3"),
                "Only one locked teaser past the frontier — not the rest of the chapter.");
        }

        [Test]
        public void UnlockedThrough1_3_Keeps1_1ReachableForReplay()
        {
            var profile = new PlayerProfile
            {
                unlockedStageIds = new List<string> { "1-1", "1-2", "1-3" },
            };
            var window = CampaignMapPresenter.GetMvpWindowStagesForTests(1, profile);

            CollectionAssert.AreEqual(new[] { "1-1", "1-2", "1-3", "1-4" },
                window.Select(s => s.stageId).ToArray(),
                "All unlocked stages must stay in the window for replay; one locked teaser after.");
            Assert.IsTrue(window[0].isUnlocked);
            Assert.IsFalse(window.Last().isUnlocked);
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
            Assert.IsTrue(window.Any(s => s.stageId == "1-1" && s.isUnlocked),
                "Cleared 1-1 must remain in the window after unlock advances.");
            Assert.IsTrue(window.Any(s => s.stageId == "1-2" && s.isUnlocked));
        }

        [Test]
        public void CampaignMap_RendersUnlockedHistoryPlusTeaser_NotFullChapter()
        {
            var go = new GameObject("MapMvpHarness");
            _spawned.Add(go);
            var map = go.AddComponent<CampaignMapPresenter>();
            map.Initialize(null, _ => CampaignLaunchOutcome.BlockedLocked);

            Transform content = map.StageNodesContentForTests;
            Assert.NotNull(content);
            Assert.AreEqual(2, content.childCount, "Fresh profile: unlocked 1-1 + locked 1-2 teaser only.");
            Assert.NotNull(content.Find("StageNode_1-1"));
            Assert.NotNull(content.Find("StageNode_1-2"));
            Assert.IsNull(content.Find("StageNode_1-12"));
            Assert.IsNull(content.Find("StageNode_10-1"), "Chapter 1 view must not render Chapter 10 nodes.");

            Object.DestroyImmediate(GameObject.Find("CampaignMapCanvas"));
        }

        [Test]
        public void CampaignMap_WithUnlockedThrough1_3_Instantiates1_1Node()
        {
            var profile = new PlayerProfile
            {
                unlockedStageIds = new List<string> { "1-1", "1-2", "1-3" },
            };
            Assert.IsTrue(SaveSystem.Save(profile));
            SaveSystem.ResetCurrentProfileForTests();

            var go = new GameObject("MapReplayHarness");
            _spawned.Add(go);
            var map = go.AddComponent<CampaignMapPresenter>();
            map.Initialize(null, _ => CampaignLaunchOutcome.BlockedLocked);

            Transform content = map.StageNodesContentForTests;
            Assert.NotNull(content.Find("StageNode_1-1"),
                "1-1 must be instantiated for replay after the frontier moves past it.");
            Assert.NotNull(content.Find("StageNode_1-2"));
            Assert.NotNull(content.Find("StageNode_1-3"));
            Assert.NotNull(content.Find("StageNode_1-4"));
            Assert.IsNull(content.Find("StageNode_1-5"),
                "Must not dump further locked stages beyond the one teaser.");
            Assert.IsTrue(map.ClickStageNodeForTests("1-1"),
                "Cleared 1-1 must remain clickable for replay.");

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
            // Undefended-enemy unlock harness (same as FreshProfileChapterOneStageAccessTests):
            // ConfirmFormation without enemy deploy; disable mirrored AI spells so they cannot KO.
            controller.SetMirroredEnemySpellsEnabledForTests(false);

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

using System.Collections.Generic;
using System.IO;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Campaign launch used to destroy only the CampaignMapPresenter component, leaving
    /// CampaignMapCanvas alive to intercept battle/tutorial clicks. These tests lock the
    /// teardown contract.
    /// </summary>
    public class CampaignMapCanvasLifecycleTests
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
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
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

        private HomePagePresenter SpawnHome()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Lifecycle_Bootstrap");
            var go = new GameObject("HomePagePresenterUnderTest_Lifecycle");
            _spawned.Add(go);
            HomePagePresenter home = go.AddComponent<HomePagePresenter>();
            home.BindBattleControllerForTests(bootstrap.Battle);
            home.BindGameBootstrapForTests(bootstrap);
            return home;
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

        private static void SaveValidDeck()
        {
            var databaseGo = new GameObject("Lifecycle_CardDatabase");
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();

            var sizing = new PlayerProfile();
            sizing.ApplyDataToEmpire();
            int deckSize = sizing.Empire.DeckSlotCount;
            List<string> deckIds = new List<string>();
            foreach (Card card in database.AllCards)
            {
                if (deckIds.Count >= deckSize) break;
                if (card.Id == "dragon") continue;
                deckIds.Add(card.Id);
            }
            Assert.AreEqual(deckSize, deckIds.Count);

            var profile = new PlayerProfile
            {
                cardCollection = new List<string>(deckIds),
                activeDeckCardIds = new List<string>(deckIds),
                unlockedStageIds = new List<string> { "1-1", "1-2" },
            };
            Assert.IsTrue(SaveSystem.Save(profile));
            SaveSystem.ResetCurrentProfileForTests();
            Object.DestroyImmediate(databaseGo);
        }

        [Test]
        public void LaunchCampaignStage_RemovesCampaignMapCanvasBeforeBattleIsShown()
        {
            SaveValidDeck();
            HomePagePresenter home = SpawnHome();
            home.OpenStoryCampaignForTests();

            Assert.IsNotNull(GameObject.Find("CampaignMapCanvas"), "Setup: Story must create a campaign canvas.");

            CampaignStageData stage1_2 = CampaignMapPresenter.GetStageForTests("1-2");
            Assert.IsNotNull(stage1_2);

            CampaignMapPresenter map = home.GetComponent<CampaignMapPresenter>();
            Assert.IsNotNull(map);

            CampaignLaunchOutcome outcome = home.LaunchCampaignStageForTests(stage1_2, map);
            Assert.AreEqual(CampaignLaunchOutcome.Launched, outcome);
            Assert.IsNull(GameObject.Find("CampaignMapCanvas"),
                "Launch must tear down the campaign canvas immediately so it cannot block battle input.");
            Assert.IsTrue(GameBootstrap.Instance.BattleCanvasVisibleForTests,
                "Battle canvas must be visible after a successful launch.");
        }

        [Test]
        public void TeardownMapForBattle_RemovesCanvasEvenWhenPresenterSurvives()
        {
            SaveValidDeck();
            HomePagePresenter home = SpawnHome();
            home.OpenStoryCampaignForTests();

            CampaignMapPresenter map = home.GetComponent<CampaignMapPresenter>();
            Assert.IsNotNull(map);
            Assert.IsNotNull(GameObject.Find("CampaignMapCanvas"));

            map.TeardownMapForBattle();
            Assert.IsNull(GameObject.Find("CampaignMapCanvas"));
        }
    }
}

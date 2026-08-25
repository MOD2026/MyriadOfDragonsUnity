using System;
using System.IO;
using MyriadOfDragons.Data;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Metagame;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    public class MemoryExpeditionShellTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsMemoryExpeditionShell_" + System.Guid.NewGuid().ToString("N"));
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
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
            _spawned.Clear();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, true);
        }

        [Test]
        public void Presenter_StartsOrResumesRun_AndBuildsGrid()
        {
            var profile = new PlayerProfile { playerName = "ExpeditionTester", stamina = 50, maxStamina = 100 };
            Assert.IsTrue(SaveSystem.Save(profile));
            SaveSystem.ResetCurrentProfileForTests();

            var go = new GameObject("MemoryExpeditionHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<MemoryExpeditionPresenter>();
            DateTime utc = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);
            presenter.SetUtcNowForTests(utc);
            presenter.Initialize(onBack: null);

            GameObject canvas = presenter.CanvasObjectForTests;
            Assert.NotNull(canvas);
            Assert.IsTrue(MemoryExpeditionUiLibrary.HasMemoryExpeditionV1Pack);
            Assert.NotNull(canvas.transform.Find("TileGrid/Tile_0"));
            Assert.NotNull(canvas.transform.Find("ClaimBar/Btn_Claim"));
            Assert.IsNull(canvas.transform.Find("RouteRow"),
                "Route-choice OpenValues shell must be replaced by the live grid.");

            Assert.NotNull(presenter.StateForTests);
            Assert.AreEqual(1, presenter.StateForTests.CurrentRound);
            Assert.AreEqual(MemoryExpedition.Rounds[0].TileCount, presenter.TileCountForTests);

            PlayerProfile saved = SaveManager.SaveData;
            Assert.IsFalse(string.IsNullOrEmpty(saved.memoryExpeditionDayKey));
            Assert.AreEqual(presenter.StateForTests.Seed, saved.memoryExpeditionSeed);
        }

        [Test]
        public void Presenter_TapPersistsSelection_AndClaimGrantsOnce()
        {
            var profile = new PlayerProfile { playerName = "ExpeditionClaim", stamina = 40, maxStamina = 100, gold = 0 };
            Assert.IsTrue(SaveSystem.Save(profile));
            SaveSystem.ResetCurrentProfileForTests();

            var go = new GameObject("MemoryExpeditionClaimHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<MemoryExpeditionPresenter>();
            DateTime utc = new DateTime(2026, 8, 25, 15, 0, 0, DateTimeKind.Utc);
            presenter.SetUtcNowForTests(utc);
            presenter.Initialize(onBack: null);

            MemoryExpeditionTapResult first = presenter.TapTileForTests(0);
            Assert.AreEqual(MemoryExpeditionTapStatus.FirstTileSelected, first.Status);
            Assert.AreEqual(0, SaveManager.SaveData.memoryExpeditionFirstSelectedTile);

            // Force a claimable band without solving the whole round: claim uses HighestRoundCleared.
            // Simulate a cleared round-1 via service/state write through profile helpers.
            PlayerProfile live = SaveManager.SaveData;
            MemoryExpeditionState state = live.ToMemoryExpeditionState();
            state.HighestRoundCleared = 1;
            live.ApplyMemoryExpeditionState(state);
            SaveManager.Save();
            SaveSystem.ResetCurrentProfileForTests();

            // Re-open presenter on same day so claim sees HighestRoundCleared=1.
            presenter.TeardownUI();
            presenter.SetUtcNowForTests(utc);
            presenter.Initialize(onBack: null);

            int goldBefore = SaveManager.SaveData.gold;
            int xpBefore = SaveManager.SaveData.passSeasonXp;
            MemoryExpeditionClaimResult claim = presenter.ClaimForTests();
            Assert.AreEqual(MemoryExpeditionClaimStatus.Granted, claim.Status, claim.Message);
            Assert.Greater(SaveManager.SaveData.gold, goldBefore);
            Assert.Greater(SaveManager.SaveData.passSeasonXp, xpBefore);
            Assert.IsTrue(SaveManager.SaveData.memoryExpeditionRewardClaimed);

            MemoryExpeditionClaimResult second = presenter.ClaimForTests();
            Assert.AreEqual(MemoryExpeditionClaimStatus.AlreadyClaimed, second.Status);
        }

        [Test]
        public void Home_MemoryExpeditionButton_OpensShell_AndBackReturnsHome()
        {
            var go = new GameObject("HomeMemoryExpeditionReach");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            home.BuildHomePageUIForTests();
            GameObject homeCanvas = home.HomeCanvasObjectForTests;

            Button openBtn = homeCanvas.transform.Find("TopHud/Btn_MemoryExpedition")?.GetComponent<Button>();
            Assert.NotNull(openBtn);
            openBtn.onClick.Invoke();
            Assert.IsFalse(homeCanvas.activeSelf);
            Assert.NotNull(GameObject.Find(MemoryExpeditionPresenter.CanvasName));
            Assert.NotNull(GameObject.Find(MemoryExpeditionPresenter.CanvasName).transform.Find("TileGrid"));

            Button back = GameObject.Find(MemoryExpeditionPresenter.CanvasName).transform
                .Find("MemoryExpeditionHeader/Btn_Back")?.GetComponent<Button>();
            Assert.NotNull(back);
            back.onClick.Invoke();
            Assert.IsTrue(homeCanvas.activeSelf);
            Assert.IsNull(go.GetComponent<MemoryExpeditionPresenter>());
        }
    }
}

using System.IO;
using MyriadOfDragons.Save;
using MyriadOfDragons.Season;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    public class DailyLoginQuestsShellTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsDailyLogin_" + System.Guid.NewGuid().ToString("N"));
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
            if (Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, true);
        }

        [Test]
        public void OpenValues_KeepPausedStreakCopy_AndThreeQuestSlots()
        {
            Assert.AreEqual("PAUSED — STREAK NOT RESET", DailyLoginQuestsOpenValues.StreakPausedCopy);
            Assert.AreEqual(3, DailyLoginQuestsOpenValues.DailyQuestSlots);
            Assert.IsTrue(DailyLoginQuestsOpenValues.AreRewardsConfigured);
            Assert.AreEqual(DailyLoginQuestsService.LoginGoldBase, DailyLoginQuestsOpenValues.LoginRewardAmount);
            Assert.AreEqual(DailyLoginQuestsService.QuestGold, DailyLoginQuestsOpenValues.QuestRewardAmount);
        }

        [Test]
        public void Presenter_ShowsReadyStatus_AndClaimsApply()
        {
            var go = new GameObject("DailyLoginHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<DailyLoginQuestsPresenter>();
            presenter.Initialize(onBackToHome: null);

            Assert.IsTrue(DailyLoginQuestsUiLibrary.HasDailyLoginQuestsV1Pack);
            GameObject canvas = presenter.CanvasObjectForTests;
            Assert.AreEqual(DailyLoginQuestsUiLibrary.LandscapeShellName,
                canvas.transform.Find("Background")?.GetComponent<Image>()?.sprite?.name);
            StringAssert.Contains("READY", presenter.StatusTextForTests);
            Assert.NotNull(canvas.transform.Find("DailyLoginPanel/StreakNodes/LoginWell_5"));
            Assert.NotNull(canvas.transform.Find("DailyQuestsPanel/QuestRow_2/Btn_Claim"));
            StringAssert.Contains("login",
                canvas.transform.Find("DailyQuestsPanel/QuestRow_0/QuestCopy")?.GetComponent<Text>()?.text?.ToLowerInvariant());
            StringAssert.Contains("g",
                canvas.transform.Find("DailyLoginPanel/StreakNodes/LoginWell_0/RewardAmount")?.GetComponent<Text>()?.text);
            StringAssert.Contains("Gold",
                canvas.transform.Find("DailyLoginHeader/WalletLine")?.GetComponent<Text>()?.text);

            DailyLoginQuestClaimResult login = presenter.ClaimLoginForTests(0);
            Assert.AreEqual(DailyLoginQuestClaimStatus.Applied, login.Status);
            DailyLoginQuestClaimResult quest = presenter.ClaimQuestForTests(0);
            Assert.AreEqual(DailyLoginQuestClaimStatus.Applied, quest.Status);
        }

        [Test]
        public void Home_LoginButton_OpensDailyLogin_AndBackReturnsHome()
        {
            var go = new GameObject("HomeDailyLoginReach");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            home.BuildHomePageUIForTests();
            GameObject homeCanvas = home.HomeCanvasObjectForTests;

            Button loginBtn = homeCanvas.transform.Find("Btn_DailyLogin")?.GetComponent<Button>();
            Assert.NotNull(loginBtn);
            loginBtn.onClick.Invoke();
            Assert.IsFalse(homeCanvas.activeSelf);
            Assert.NotNull(GameObject.Find(DailyLoginQuestsPresenter.CanvasName));

            Button back = GameObject.Find(DailyLoginQuestsPresenter.CanvasName).transform
                .Find("DailyLoginHeader/Btn_Back")?.GetComponent<Button>();
            back.onClick.Invoke();
            Assert.IsTrue(homeCanvas.activeSelf);
            Assert.IsNull(go.GetComponent<DailyLoginQuestsPresenter>());
        }
    }
}

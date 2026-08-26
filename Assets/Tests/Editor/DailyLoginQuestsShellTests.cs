using System.IO;
using MyriadOfDragons.Metagame;
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
            // Real reachability path since the Home IA rebuild - Daily Login is a tab inside the
            // Quests/Events destination hub, not a direct Home button.
            var go = new GameObject("HomeDailyLoginReach");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            home.BuildHomePageUIForTests();
            GameObject homeCanvas = home.HomeCanvasObjectForTests;

            home.OpenQuestsEventsHubForTests();
            GameObject hub = home.TabHubObjectForTests;
            Assert.NotNull(hub, "Setup: expected the Quests/Events hub to open.");
            Button loginBtn = hub.transform.Find("Dest_DAILY LOGIN")?.GetComponent<Button>();
            Assert.NotNull(loginBtn, "Setup: expected a DAILY LOGIN tab inside the Quests/Events hub.");
            loginBtn.onClick.Invoke();
            Assert.IsFalse(homeCanvas.activeSelf);
            Assert.NotNull(GameObject.Find(DailyLoginQuestsPresenter.CanvasName));

            Button back = GameObject.Find(DailyLoginQuestsPresenter.CanvasName).transform
                .Find("DailyLoginHeader/Btn_Back")?.GetComponent<Button>();
            back.onClick.Invoke();
            Assert.IsTrue(homeCanvas.activeSelf);
            Assert.IsNull(go.GetComponent<DailyLoginQuestsPresenter>());
        }

        [Test]
        public void RealClaim_EmitsAModeRewardClaimedTelemetryEvent()
        {
            // Real retention-telemetry wiring check (register: "Retention telemetry architecture
            // - LOCKED" / dispatch "wire the actual emit calls into real gameplay call sites").
            // Daily Login/Quests rewards are already configured (AreRewardsConfigured == true, see
            // OpenValues_KeepPausedStreakCopy_AndThreeQuestSlots above), so this exercises the REAL
            // Applied path end to end, not a simulated one.
            var go = new GameObject("DailyLoginTelemetryHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<DailyLoginQuestsPresenter>();
            var fakeGateway = new FakeRetentionTelemetryGateway();
            var telemetryScratchDir = Path.Combine(Path.GetTempPath(), "MoDDailyLoginTelemetry_" + System.Guid.NewGuid().ToString("N"));
            var outbox = new RetentionTelemetryOutbox(fakeGateway, telemetryScratchDir);
            presenter.Initialize(onBackToHome: null, telemetryOutbox: outbox);

            DailyLoginQuestClaimResult login = presenter.ClaimLoginForTests(0);
            Assert.AreEqual(DailyLoginQuestClaimStatus.Applied, login.Status, "Setup: expected a real successful claim.");

            Assert.AreEqual(1, fakeGateway.SentEvents.Count, "A real successful claim must emit exactly one telemetry event.");
            Assert.AreEqual(RetentionTelemetryEvents.EventTypeModeRewardClaimed, fakeGateway.SentEvents[0].eventType);
            Assert.AreEqual("daily_login_quests", fakeGateway.SentEvents[0].mode);
            Assert.AreEqual(0, outbox.QueuedEventsForTests.Count, "A successfully-sent event must not remain queued.");

            if (Directory.Exists(telemetryScratchDir)) Directory.Delete(telemetryScratchDir, recursive: true);
        }

        [Test]
        public void RefusedClaim_NeverEmitsTelemetry()
        {
            // Real negative check - a claim that does NOT actually succeed (already claimed,
            // not complete, etc.) must never be reported to analytics as a real reward claim.
            var go = new GameObject("DailyLoginTelemetryRefuseHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<DailyLoginQuestsPresenter>();
            var fakeGateway = new FakeRetentionTelemetryGateway();
            var telemetryScratchDir = Path.Combine(Path.GetTempPath(), "MoDDailyLoginTelemetryRefuse_" + System.Guid.NewGuid().ToString("N"));
            var outbox = new RetentionTelemetryOutbox(fakeGateway, telemetryScratchDir);
            presenter.Initialize(onBackToHome: null, telemetryOutbox: outbox);

            presenter.ClaimLoginForTests(0);
            DailyLoginQuestClaimResult secondAttempt = presenter.ClaimLoginForTests(0);
            Assert.AreNotEqual(DailyLoginQuestClaimStatus.Applied, secondAttempt.Status, "Setup: second claim of the same slot must refuse.");

            Assert.AreEqual(1, fakeGateway.SentEvents.Count, "Only the first, real successful claim should have emitted telemetry.");

            if (Directory.Exists(telemetryScratchDir)) Directory.Delete(telemetryScratchDir, recursive: true);
        }
    }
}

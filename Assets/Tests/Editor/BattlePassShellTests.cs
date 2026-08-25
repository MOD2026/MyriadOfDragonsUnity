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
    public class BattlePassShellTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsBattlePass_" + System.Guid.NewGuid().ToString("N"));
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
        public void OpenValues_StayUnset_AndSeasonLengthIsLocked28Days()
        {
            Assert.AreEqual(28, BattlePassOpenValues.SeasonLengthDays);
            Assert.AreEqual("28-DAY SEASON", BattlePassOpenValues.SeasonLengthCopy);
            Assert.IsNull(BattlePassOpenValues.SeasonXpPerTier);
            Assert.IsNull(BattlePassOpenValues.PremiumUnlockPrice);
            Assert.IsFalse(BattlePassOpenValues.AreTierRewardsConfigured);
        }

        [Test]
        public void Presenter_BuildsDualTrackShell_AndClaimsRefuseWhileOpen()
        {
            var go = new GameObject("BattlePassHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<BattlePassPresenter>();
            presenter.Initialize(onBackToHome: null);

            Assert.IsTrue(BattlePassUiLibrary.HasBattlePassV1Pack);
            GameObject canvas = presenter.CanvasObjectForTests;
            Assert.NotNull(canvas);
            Assert.AreEqual(BattlePassUiLibrary.DualTrackShellName,
                canvas.transform.Find("Background")?.GetComponent<Image>()?.sprite?.name);
            Assert.AreEqual("28-DAY SEASON",
                canvas.transform.Find("BattlePassHeader/SeasonLength")?.GetComponent<Text>()?.text);
            Assert.NotNull(canvas.transform.Find("TrackTable/FreeTrackRow/TierWell_0/RewardAmount"));
            Assert.NotNull(canvas.transform.Find("TrackTable/PremiumTrackRow/TierWell_7"));
            // Real regression found in tonight's full-suite baseline: these asserted the OLD
            // "OPEN" placeholder text. Commit 92c8b54 ("Bind Battle Pass chrome to real Season XP
            // instead of OPEN placeholders" - owner-directed, "Owner saw unresolved template
            // tokens") deliberately replaced it with real bound data. Updated to assert the real,
            // current behavior instead of reverting the product change.
            Assert.AreEqual(MetagameShellProfileBinding.PassSeasonXpLine(),
                canvas.transform.Find("TrackTable/FreeTrackRow/TierWell_0/RewardAmount")?.GetComponent<Text>()?.text);
            Assert.AreEqual(MetagameShellProfileBinding.PassTierProgressLine(),
                canvas.transform.Find("SeasonXpRow/XpValues")?.GetComponent<Text>()?.text);

            BattlePassClaimResult claim = presenter.ClaimTierForTests(0, premiumTrack: false);
            Assert.AreEqual(BattlePassClaimStatus.OpenValuesNotLocked, claim.Status);
            BattlePassClaimResult unlock = presenter.UnlockPremiumForTests();
            Assert.AreEqual(BattlePassClaimStatus.OpenValuesNotLocked, unlock.Status);
        }

        [Test]
        public void Home_PassButton_OpensBattlePass_AndBackReturnsHome()
        {
            var go = new GameObject("HomeBattlePassReach");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            home.BuildHomePageUIForTests();
            GameObject homeCanvas = home.HomeCanvasObjectForTests;

            Button passBtn = homeCanvas.transform.Find("TopHud/Btn_BattlePass")?.GetComponent<Button>();
            Assert.NotNull(passBtn);
            passBtn.onClick.Invoke();

            Assert.IsFalse(homeCanvas.activeSelf);
            Assert.NotNull(go.GetComponent<BattlePassPresenter>());
            Assert.NotNull(GameObject.Find(BattlePassPresenter.CanvasName));

            Button back = GameObject.Find(BattlePassPresenter.CanvasName).transform
                .Find("BattlePassHeader/Btn_Back")?.GetComponent<Button>();
            Assert.NotNull(back);
            back.onClick.Invoke();
            Assert.IsTrue(homeCanvas.activeSelf);
            Assert.IsNull(go.GetComponent<BattlePassPresenter>());
        }

        [Test]
        public void RefusedClaim_OpenValuesNotLocked_NeverEmitsTelemetry()
        {
            // Real retention-telemetry wiring check (register: "Retention telemetry architecture
            // - LOCKED" / dispatch "wire the actual emit calls into real gameplay call sites").
            // Unlike Daily Login/Empire Expedition, BattlePassOpenValues.AreTierRewardsConfigured
            // is still false (see OpenValues_StayUnset_AndSeasonLengthIsLocked28Days above) with
            // no test-only override - a real "Applied" claim genuinely cannot happen yet, so this
            // is the one real behavior currently reachable: a refused claim must never be reported
            // to analytics as a real reward claim.
            var go = new GameObject("BattlePassTelemetryHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<BattlePassPresenter>();
            var fakeGateway = new FakeRetentionTelemetryGateway();
            var telemetryScratchDir = Path.Combine(Path.GetTempPath(), "MoDBattlePassTelemetry_" + System.Guid.NewGuid().ToString("N"));
            var outbox = new RetentionTelemetryOutbox(fakeGateway, telemetryScratchDir);
            presenter.Initialize(onBackToHome: null, telemetryOutbox: outbox);

            BattlePassClaimResult claim = presenter.ClaimTierForTests(0, premiumTrack: false);
            Assert.AreEqual(BattlePassClaimStatus.OpenValuesNotLocked, claim.Status, "Setup: real production claim must still refuse.");

            Assert.AreEqual(0, fakeGateway.SentEvents.Count, "An OpenValuesNotLocked refusal is not a real claim - must not emit telemetry.");

            if (Directory.Exists(telemetryScratchDir)) Directory.Delete(telemetryScratchDir, recursive: true);
        }
    }
}

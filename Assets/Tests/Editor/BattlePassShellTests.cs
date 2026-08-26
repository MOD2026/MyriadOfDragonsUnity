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
        public void OpenValues_AllNumbersLocked_ButPremiumUnlockHasNoBackingFieldYet()
        {
            // All 4 numbers LOCKED 2026-08-26 (BS): SeasonXpPerTier=1400, PremiumUnlockPrice=800,
            // ClaimGraceDays=7, plus the free/paid Gold tables. AreTierRewardsConfigured is true
            // now, but that only means the NUMBERS exist - premium unlock itself still can't
            // persist (no PlayerProfile field), so paid-track claims and UnlockPremiumForTests
            // still genuinely refuse (see PaidTrackClaim_AlwaysRefuses... below).
            Assert.AreEqual(28, BattlePassOpenValues.SeasonLengthDays);
            Assert.AreEqual("28-DAY SEASON", BattlePassOpenValues.SeasonLengthCopy);
            Assert.AreEqual(1400, BattlePassOpenValues.SeasonXpPerTier);
            Assert.AreEqual(800, BattlePassOpenValues.PremiumUnlockPrice);
            Assert.AreEqual(7, BattlePassOpenValues.ClaimGraceDays);
            Assert.IsTrue(BattlePassOpenValues.AreTierRewardsConfigured);
        }

        [Test]
        public void Presenter_BuildsDualTrackShell_FreeClaimsApply_PremiumStillRefuses()
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

            // Gold table LOCKED 2026-08-26 - a free-track claim now genuinely applies.
            BattlePassClaimResult claim = presenter.ClaimTierForTests(0, premiumTrack: false);
            Assert.AreEqual(BattlePassClaimStatus.Applied, claim.Status);
            Assert.AreEqual(BattlePassOpenValues.FreeTierGold[0], claim.GoldGranted);
            // Price is locked, but there's no PlayerProfile field to persist an unlock yet -
            // unlock and every paid-track claim still genuinely refuse.
            BattlePassClaimResult unlock = presenter.UnlockPremiumForTests();
            Assert.AreEqual(BattlePassClaimStatus.PremiumLocked, unlock.Status);
            BattlePassClaimResult paidClaim = presenter.ClaimTierForTests(0, premiumTrack: true);
            Assert.AreEqual(BattlePassClaimStatus.PremiumLocked, paidClaim.Status);
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
        public void RefusedClaim_PremiumLocked_NeverEmitsTelemetry()
        {
            // Real retention-telemetry wiring check (register: "Retention telemetry architecture
            // - LOCKED" / dispatch "wire the actual emit calls into real gameplay call sites").
            // Free-track claims apply for real now that the Gold table is locked, so the refusal
            // this test exercises moved to the paid track, which still genuinely refuses
            // (no PlayerProfile field to persist an unlock, even though the price is locked) -
            // the invariant under test (a refusal is never
            // reported to analytics as a real reward claim) is unchanged.
            var go = new GameObject("BattlePassTelemetryHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<BattlePassPresenter>();
            var fakeGateway = new FakeRetentionTelemetryGateway();
            var telemetryScratchDir = Path.Combine(Path.GetTempPath(), "MoDBattlePassTelemetry_" + System.Guid.NewGuid().ToString("N"));
            var outbox = new RetentionTelemetryOutbox(fakeGateway, telemetryScratchDir);
            presenter.Initialize(onBackToHome: null, telemetryOutbox: outbox);

            BattlePassClaimResult claim = presenter.ClaimTierForTests(0, premiumTrack: true);
            Assert.AreEqual(BattlePassClaimStatus.PremiumLocked, claim.Status, "Setup: real production paid-track claim must still refuse.");

            Assert.AreEqual(0, fakeGateway.SentEvents.Count, "A PremiumLocked refusal is not a real claim - must not emit telemetry.");

            if (Directory.Exists(telemetryScratchDir)) Directory.Delete(telemetryScratchDir, recursive: true);
        }

        [Test]
        public void RealFreeClaim_EmitsAModeRewardClaimedTelemetryEvent()
        {
            var go = new GameObject("BattlePassTelemetryRealClaimHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<BattlePassPresenter>();
            var fakeGateway = new FakeRetentionTelemetryGateway();
            var telemetryScratchDir = Path.Combine(Path.GetTempPath(), "MoDBattlePassTelemetryReal_" + System.Guid.NewGuid().ToString("N"));
            var outbox = new RetentionTelemetryOutbox(fakeGateway, telemetryScratchDir);
            presenter.Initialize(onBackToHome: null, telemetryOutbox: outbox);

            BattlePassClaimResult claim = presenter.ClaimTierForTests(0, premiumTrack: false);
            Assert.AreEqual(BattlePassClaimStatus.Applied, claim.Status, "Setup: expected a real successful free-track claim.");

            Assert.AreEqual(1, fakeGateway.SentEvents.Count, "A real successful claim must emit exactly one telemetry event.");
            Assert.AreEqual(RetentionTelemetryEvents.EventTypeModeRewardClaimed, fakeGateway.SentEvents[0].eventType);
            Assert.AreEqual("battle_pass", fakeGateway.SentEvents[0].mode);

            if (Directory.Exists(telemetryScratchDir)) Directory.Delete(telemetryScratchDir, recursive: true);
        }

        [Test]
        public void GoldTable_PaidTrackIsExactlyDoubleFreeTrack_TotalsMatchLockedFigures()
        {
            Assert.AreEqual(BattlePassOpenValues.ShellTierWellCount, BattlePassOpenValues.FreeTierGold.Length);
            Assert.AreEqual(BattlePassOpenValues.ShellTierWellCount, BattlePassOpenValues.PaidTierGold.Length);
            int freeTotal = 0, paidTotal = 0;
            for (int i = 0; i < BattlePassOpenValues.ShellTierWellCount; i++)
            {
                Assert.AreEqual(BattlePassOpenValues.FreeTierGold[i] * 2, BattlePassOpenValues.PaidTierGold[i],
                    $"Tier {i} paid amount must be exactly double free.");
                freeTotal += BattlePassOpenValues.FreeTierGold[i];
                paidTotal += BattlePassOpenValues.PaidTierGold[i];
            }
            Assert.AreEqual(15000, freeTotal);
            Assert.AreEqual(30000, paidTotal);
        }

        [Test]
        public void FreeTierClaims_MustBeAscending_SkippingOrReclaimingRefuses()
        {
            var profile = new PlayerProfile { gold = 0 };

            BattlePassClaimResult skipAhead = BattlePassOpenValues.TryClaimTier(profile, 1, premiumTrack: false);
            Assert.AreEqual(BattlePassClaimStatus.NotNextTierInOrder, skipAhead.Status,
                "Tier 1 is not claimable before tier 0.");
            Assert.AreEqual(0, profile.gold);

            BattlePassClaimResult first = BattlePassOpenValues.TryClaimTier(profile, 0, premiumTrack: false);
            Assert.AreEqual(BattlePassClaimStatus.Applied, first.Status);
            Assert.AreEqual(BattlePassOpenValues.FreeTierGold[0], profile.gold);
            Assert.AreEqual(1, profile.battlePassClaimedFreeTier);

            BattlePassClaimResult reclaim = BattlePassOpenValues.TryClaimTier(profile, 0, premiumTrack: false);
            Assert.AreEqual(BattlePassClaimStatus.NotNextTierInOrder, reclaim.Status,
                "Tier 0 was already claimed - reclaiming must refuse, not double-grant.");
            Assert.AreEqual(BattlePassOpenValues.FreeTierGold[0], profile.gold, "A refused reclaim must not grant Gold again.");
        }

        [Test]
        public void PaidTrackClaim_AlwaysRefuses_PremiumCanNeverBeUnlockedYet()
        {
            var profile = new PlayerProfile { gold = 0 };
            BattlePassClaimResult result = BattlePassOpenValues.TryClaimTier(profile, 0, premiumTrack: true);
            Assert.AreEqual(BattlePassClaimStatus.PremiumLocked, result.Status);
            Assert.AreEqual(0, profile.gold);
            Assert.AreEqual(0, profile.battlePassClaimedPaidTier);
        }
    }
}

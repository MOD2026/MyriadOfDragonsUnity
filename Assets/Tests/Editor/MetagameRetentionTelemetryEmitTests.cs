using System.IO;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Data;
using MyriadOfDragons.Metagame;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>Remaining Metagame-owned retention-telemetry emit sites (Campaign win/loss,
    /// Home feature_entry, Shop Stamina daily cap) — same FakeRetentionTelemetryGateway pattern
    /// as EmpireExpeditionShellTests / BattlePassShellTests.</summary>
    public class MetagameRetentionTelemetryEmitTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MoDMetagameTelemetry_" + System.Guid.NewGuid().ToString("N"));
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
                Directory.Delete(_scratchSaveDir, true);
        }

        [Test]
        public void CampaignVictoryFirstClear_ThroughHomeHandler_EmitsRunCompletedAndRewardClaimed()
        {
            var profile = new PlayerProfile();
            profile.unlockedStageIds.Add("1-1");
            Assert.IsTrue(SaveSystem.Save(profile));
            SaveSystem.ResetCurrentProfileForTests();

            var go = new GameObject("HomeCampaignTelemetryHarness");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            var fakeGateway = new FakeRetentionTelemetryGateway();
            var telemetryScratch = Path.Combine(Path.GetTempPath(), "MoDCampTel_" + System.Guid.NewGuid().ToString("N"));
            var outbox = new RetentionTelemetryOutbox(fakeGateway, telemetryScratch);
            home.BindTelemetryOutboxForTests(outbox);

            home.SetActiveStageForTests(new CampaignStageData(
                "1-1", "Outer Border Guard", "Orc Scout Patrol", "UI/Portraits/Paladin",
                "test", 10, 1, unlocked: true));
            home.NotifyMatchCompletedForTests(new MatchResult(
                isVictory: true, ticksTaken: 3, playerHealthRemaining: 50, playerMaxHealth: 100,
                enemyHealthRemaining: 0, enemyMaxHealth: 100, outcomeReason: null));

            Assert.AreEqual(2, fakeGateway.SentEvents.Count,
                "First-clear Campaign victory must emit mode_run_completed + mode_reward_claimed.");
            Assert.AreEqual(RetentionTelemetryEvents.EventTypeModeRunCompleted, fakeGateway.SentEvents[0].eventType);
            Assert.AreEqual("campaign", fakeGateway.SentEvents[0].mode);
            Assert.AreEqual("1-1", fakeGateway.SentEvents[0].runId);
            Assert.AreEqual("success", fakeGateway.SentEvents[0].outcome);
            Assert.AreEqual(RetentionTelemetryEvents.EventTypeModeRewardClaimed, fakeGateway.SentEvents[1].eventType);

            if (Directory.Exists(telemetryScratch)) Directory.Delete(telemetryScratch, true);
        }

        [Test]
        public void CampaignDefeat_ThroughHomeHandler_EmitsModeRunCompletedFailureOnly()
        {
            var go = new GameObject("HomeCampaignDefeatTelemetryHarness");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            var fakeGateway = new FakeRetentionTelemetryGateway();
            var telemetryScratch = Path.Combine(Path.GetTempPath(), "MoDCampDefeatTel_" + System.Guid.NewGuid().ToString("N"));
            var outbox = new RetentionTelemetryOutbox(fakeGateway, telemetryScratch);
            home.BindTelemetryOutboxForTests(outbox);

            home.SetActiveStageForTests(new CampaignStageData(
                "2-5", "Windward Bastion", "Enemy", "UI/Portraits/Paladin", "test", 10, 1));
            home.NotifyMatchCompletedForTests(new MatchResult(
                isVictory: false, ticksTaken: 3, playerHealthRemaining: 0, playerMaxHealth: 100,
                enemyHealthRemaining: 40, enemyMaxHealth: 100, outcomeReason: null));

            Assert.AreEqual(1, fakeGateway.SentEvents.Count);
            Assert.AreEqual(RetentionTelemetryEvents.EventTypeModeRunCompleted, fakeGateway.SentEvents[0].eventType);
            Assert.AreEqual("failure", fakeGateway.SentEvents[0].outcome);
            Assert.AreEqual("2-5", fakeGateway.SentEvents[0].runId);

            if (Directory.Exists(telemetryScratch)) Directory.Delete(telemetryScratch, true);
        }

        [Test]
        public void HomeFeatureEntry_EmitsFeatureEntry()
        {
            var go = new GameObject("HomeFeatureEntryHarness");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            var fakeGateway = new FakeRetentionTelemetryGateway();
            var telemetryScratch = Path.Combine(Path.GetTempPath(), "MoDHomeFeatTel_" + System.Guid.NewGuid().ToString("N"));
            var outbox = new RetentionTelemetryOutbox(fakeGateway, telemetryScratch);
            home.BindTelemetryOutboxForTests(outbox);

            home.EmitFeatureEntryForTests("shop");

            Assert.AreEqual(1, fakeGateway.SentEvents.Count);
            Assert.AreEqual(RetentionTelemetryEvents.EventTypeFeatureEntry, fakeGateway.SentEvents[0].eventType);
            Assert.AreEqual("shop", fakeGateway.SentEvents[0].mode);

            if (Directory.Exists(telemetryScratch)) Directory.Delete(telemetryScratch, true);
        }

        [Test]
        public void ShopStaminaDailyCap_EmitsDailyCapReached()
        {
            var profile = new PlayerProfile();
            profile.gems = 9999;
            profile.staminaShopWindowStartUtcTicks = ShopStaminaCatalog.NowUtcTicks();
            profile.staminaShopPurchasesInWindow = ShopStaminaCatalog.MaxPurchasesPerRollingDay;
            Assert.IsTrue(SaveSystem.Save(profile));
            SaveSystem.ResetCurrentProfileForTests();
            profile = SaveSystem.CurrentProfile ?? SaveManager.SaveData;

            var go = new GameObject("ShopStaminaCapTelemetryHarness");
            _spawned.Add(go);
            var shop = go.AddComponent<ShopPresenter>();
            var fakeGateway = new FakeRetentionTelemetryGateway();
            var telemetryScratch = Path.Combine(Path.GetTempPath(), "MoDShopCapTel_" + System.Guid.NewGuid().ToString("N"));
            var outbox = new RetentionTelemetryOutbox(fakeGateway, telemetryScratch);
            shop.Initialize(profile, onBackToHome: null, telemetryOutbox: outbox);

            int gemsBefore = profile.gems;
            int staminaBefore = profile.stamina;
            int purchasesBefore = profile.staminaShopPurchasesInWindow;
            Assert.AreEqual(ShopStaminaCatalog.MaxPurchasesPerRollingDay, purchasesBefore,
                "Setup: profile must already be at the 4/24h Stamina refill cap.");

            // PurchaseForTests returns whether the SKU exists — NOT whether the purchase succeeded.
            // Cap enforcement is asserted on wallet + window counters below (never on the bool).
            Assert.IsTrue(shop.PurchaseForTests(ShopStaminaCatalog.SkuIdForGemCost(30)),
                "Setup: stamina ladder SKU must exist on the Shop.");
            Assert.AreEqual(gemsBefore, profile.gems,
                "Capped ladder purchase must refuse without spending Gems.");
            Assert.AreEqual(staminaBefore, profile.stamina,
                "Capped ladder purchase must refuse without granting Stamina.");
            Assert.AreEqual(purchasesBefore, profile.staminaShopPurchasesInWindow,
                "Capped refuse must not increment the shared 4/24h purchase counter.");

            Assert.AreEqual(1, fakeGateway.SentEvents.Count);
            Assert.AreEqual(RetentionTelemetryEvents.EventTypeDailyCapReached, fakeGateway.SentEvents[0].eventType);
            Assert.AreEqual("shop_stamina", fakeGateway.SentEvents[0].mode);

            if (Directory.Exists(telemetryScratch)) Directory.Delete(telemetryScratch, true);
        }
    }
}

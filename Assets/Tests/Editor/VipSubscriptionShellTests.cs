using System;
using System.IO;
using MyriadOfDragons.Metagame;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    public class VipSubscriptionShellTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsVipShell_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            VipSubscriptionOpenValues.SetNowUtcTicksForTests(null);
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
            VipSubscriptionOpenValues.SetNowUtcTicksForTests(null);
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, true);
        }

        [Test]
        public void OpenValues_PricesAndDurations_MatchLockedSpec()
        {
            Assert.AreEqual(800, VipSubscriptionOpenValues.WeeklyGemPrice);
            Assert.AreEqual(1500, VipSubscriptionOpenValues.FortnightGemPrice);
            Assert.AreEqual(3000, VipSubscriptionOpenValues.MonthlyGemPrice);
            Assert.IsTrue(VipSubscriptionOpenValues.ArePricesConfigured);
            CollectionAssert.AreEqual(new[] { 7, 14, 30 }, VipSubscriptionOpenValues.PlanDurationDays);
            CollectionAssert.AreEqual(new[] { 1, 2, 4 }, VipSubscriptionOpenValues.PlanMaxClaims);
            StringAssert.Contains("Stamina", VipSubscriptionOpenValues.StatusNote);
            Assert.AreEqual("[runtime]", VipSubscriptionOpenValues.RuntimePlaceholder);
        }

        [Test]
        public void Presenter_BuildsArtShell_AndSubscribeAppliesWeekly()
        {
            PlayerProfile profile = SaveSystem.CurrentProfile;
            profile.gems = 2000;
            profile.stamina = 20;
            SaveSystem.Save(profile);

            var go = new GameObject("VipHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<VipSubscriptionPresenter>();
            presenter.Initialize(onBack: null);

            GameObject canvas = presenter.CanvasObjectForTests;
            Assert.NotNull(canvas);
            Assert.IsTrue(VipSubscriptionUiLibrary.HasVipSubscriptionV1Pack);
            Assert.AreEqual(VipSubscriptionUiLibrary.ScreenShellName,
                canvas.transform.Find("Background")?.GetComponent<Image>()?.sprite?.name);
            Assert.IsFalse(canvas.transform.Find("Background").GetComponent<Image>().raycastTarget);
            Assert.NotNull(canvas.transform.Find("BenefitGrid/BenefitWell_5"));
            Assert.NotNull(canvas.transform.Find("IdentityColumn/StateSocket_2"));
            Assert.IsNotNull(canvas.transform.Find("IdentityColumn/StateSocket_0/StateIcon")?.GetComponent<Image>()?.sprite,
                "State sockets must show atlas-sliced icons, not empty colored wells.");
            Assert.IsNotNull(canvas.transform.Find("BenefitGrid/BenefitWell_0/BenefitIcon")?.GetComponent<Image>()?.sprite,
                "Benefit wells must show atlas icons.");
            StringAssert.Contains("Weekly",
                canvas.transform.Find("BenefitGrid/BenefitWell_0/Label")?.GetComponent<Text>()?.text);

            VipSubscriptionActionResult subscribe = presenter.SubscribeForTests();
            Assert.AreEqual(VipSubscriptionActionStatus.Applied, subscribe.Status);
            Assert.AreEqual(VipSubscriptionOpenValues.WeeklyGemPrice,
                2000 - SaveSystem.CurrentProfile.gems);
            Assert.AreEqual("weekly", SaveSystem.CurrentProfile.vipPlanId);
            Assert.AreEqual(1, SaveSystem.CurrentProfile.vipClaimsConsumed);
            Assert.AreEqual(20 + ShopStaminaCatalog.StaminaGrantPerPotion, SaveSystem.CurrentProfile.stamina);
            Assert.AreEqual(1, SaveSystem.CurrentProfile.staminaShopPurchasesInWindow);
            StringAssert.Contains("Active",
                canvas.transform.Find("IdentityColumn/EntitlementState")?.GetComponent<Text>()?.text);

            VipSubscriptionActionResult second = presenter.SubscribePlanForTests(VipPlanKind.Monthly);
            Assert.AreEqual(VipSubscriptionActionStatus.AlreadySubscribed, second.Status);
        }

        [Test]
        public void Subscribe_ForfeitsClaimAtFullStamina_StillConsumesShopSlot()
        {
            long t0 = new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc).Ticks;
            VipSubscriptionOpenValues.SetNowUtcTicksForTests(t0);

            PlayerProfile profile = SaveSystem.CurrentProfile;
            profile.gems = 1000;
            profile.stamina = profile.maxStamina;
            profile.staminaShopPurchasesInWindow = 0;
            profile.staminaShopWindowStartUtcTicks = t0;
            SaveSystem.Save(profile);

            VipSubscriptionActionResult result = VipSubscriptionOpenValues.TrySubscribe(VipPlanKind.Weekly);
            Assert.AreEqual(VipSubscriptionActionStatus.Applied, result.Status);
            Assert.AreEqual(1, result.ClaimsForfeited);
            Assert.AreEqual(0, result.ClaimsApplied);
            Assert.AreEqual(profile.maxStamina, SaveSystem.CurrentProfile.stamina);
            Assert.AreEqual(1, SaveSystem.CurrentProfile.vipClaimsConsumed);
            Assert.AreEqual(1, SaveSystem.CurrentProfile.staminaShopPurchasesInWindow);
        }

        [Test]
        public void Fortnight_SecondClaimUnlocksAfterSevenDays_AndSharesShopCap()
        {
            long t0 = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
            VipSubscriptionOpenValues.SetNowUtcTicksForTests(t0);

            PlayerProfile profile = SaveSystem.CurrentProfile;
            profile.gems = 2000;
            profile.stamina = 10;
            profile.staminaShopPurchasesInWindow = 0;
            profile.staminaShopWindowStartUtcTicks = t0;
            SaveSystem.Save(profile);

            Assert.AreEqual(VipSubscriptionActionStatus.Applied,
                VipSubscriptionOpenValues.TrySubscribe(VipPlanKind.Fortnight).Status);
            Assert.AreEqual(1, SaveSystem.CurrentProfile.vipClaimsConsumed);
            Assert.AreEqual(1, SaveSystem.CurrentProfile.staminaShopPurchasesInWindow);

            // Second claim unlocks at +7d. Keep the Shop rolling window open and full so the
            // VIP claim must defer (7d would otherwise expire the 24h window and free the cap).
            long tPlus7 = t0 + TimeSpan.FromDays(7).Ticks;
            VipSubscriptionOpenValues.SetNowUtcTicksForTests(tPlus7);
            SaveSystem.CurrentProfile.staminaShopWindowStartUtcTicks = tPlus7;
            SaveSystem.CurrentProfile.staminaShopPurchasesInWindow = ShopStaminaCatalog.MaxPurchasesPerRollingDay;
            SaveSystem.Save(SaveSystem.CurrentProfile);

            VipSubscriptionActionResult deferred = VipSubscriptionOpenValues.TryRestore();
            Assert.AreEqual(VipSubscriptionActionStatus.Applied, deferred.Status);
            Assert.AreEqual(1, SaveSystem.CurrentProfile.vipClaimsConsumed,
                "Claim must defer when Shop 24h cap is full — no bypass.");

            // New rolling window + room under cap → second claim lands.
            VipSubscriptionOpenValues.SetNowUtcTicksForTests(tPlus7 + ShopStaminaCatalog.RollingWindowTicks);
            SaveSystem.CurrentProfile.stamina = 10;
            SaveSystem.Save(SaveSystem.CurrentProfile);
            VipSubscriptionActionResult second = VipSubscriptionOpenValues.TryRestore();
            Assert.AreEqual(2, SaveSystem.CurrentProfile.vipClaimsConsumed);
            Assert.AreEqual(1, second.ClaimsApplied);
        }

        [Test]
        public void Lapse_ExpiresUnusedClaims_NoCompensation()
        {
            long t0 = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
            VipSubscriptionOpenValues.SetNowUtcTicksForTests(t0);

            PlayerProfile profile = SaveSystem.CurrentProfile;
            profile.gems = 5000;
            profile.stamina = 10;
            // Cap shop slots immediately so monthly's later claims stay unused.
            profile.staminaShopPurchasesInWindow = ShopStaminaCatalog.MaxPurchasesPerRollingDay;
            profile.staminaShopWindowStartUtcTicks = t0;
            SaveSystem.Save(profile);

            Assert.AreEqual(VipSubscriptionActionStatus.Applied,
                VipSubscriptionOpenValues.TrySubscribe(VipPlanKind.Monthly).Status);
            Assert.AreEqual(0, SaveSystem.CurrentProfile.vipClaimsConsumed,
                "First claim deferred under full shop cap.");

            long afterExpiry = t0 + TimeSpan.FromDays(31).Ticks;
            VipSubscriptionOpenValues.SetNowUtcTicksForTests(afterExpiry);
            VipSubscriptionActionResult restore = VipSubscriptionOpenValues.TryRestore();
            Assert.AreEqual(VipSubscriptionActionStatus.NotSubscribed, restore.Status);
            StringAssert.Contains("lapsed", restore.Message.ToLowerInvariant());
            Assert.AreEqual(string.Empty, SaveSystem.CurrentProfile.vipPlanId);
            Assert.AreEqual(0, SaveSystem.CurrentProfile.vipClaimsConsumed);
        }

        [Test]
        public void Home_VipButton_OpensShell_AndBackReturnsHome()
        {
            var go = new GameObject("HomeVipReach");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            home.BuildHomePageUIForTests();
            GameObject homeCanvas = home.HomeCanvasObjectForTests;

            Button openBtn = homeCanvas.transform.Find("TopHud/Btn_Vip")?.GetComponent<Button>();
            Assert.NotNull(openBtn);
            openBtn.onClick.Invoke();
            Assert.IsFalse(homeCanvas.activeSelf);
            Assert.NotNull(GameObject.Find(VipSubscriptionPresenter.CanvasName));

            Button back = GameObject.Find(VipSubscriptionPresenter.CanvasName).transform
                .Find("VipHeader/Btn_Back")?.GetComponent<Button>();
            Assert.NotNull(back);
            back.onClick.Invoke();
            Assert.IsTrue(homeCanvas.activeSelf);
            Assert.IsNull(go.GetComponent<VipSubscriptionPresenter>());
        }
    }
}

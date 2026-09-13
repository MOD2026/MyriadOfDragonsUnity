using System.Diagnostics;
using System.IO;
using MyriadOfDragons.Metagame;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Shop V1 chrome pack — catalog shell, pack-open frame, and 4×2 stamina atlas slices are
    /// present and wired (unlocked vs locked selected independently per tier).
    /// </summary>
    public class ShopV1ChromeTests
    {
        private GameObject _shopGo;
        private GameObject _shopCanvas;
        private string _scratchSaveDir;
        private string _scratchTelemetryDir;

        [SetUp]
        public void SetUp()
        {
            WhHangProfileTrace.Mark("ShopV1ChromeTests.SetUp.enter");
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsShopV1Chrome_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            _scratchTelemetryDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsShopV1ChromeTelemetry_" + System.Guid.NewGuid().ToString("N"));
            WhHangProfileTrace.Mark($"ShopV1ChromeTests.SetUp.exit scratch={_scratchSaveDir}");
        }

        [TearDown]
        public void TearDown()
        {
            var sw = Stopwatch.StartNew();
            WhHangProfileTrace.Mark("ShopV1ChromeTests.TearDown.enter");
            if (_shopGo != null) Object.DestroyImmediate(_shopGo);
            WhHangProfileTrace.Mark("ShopV1ChromeTests.TearDown.after_Destroy_shopGo", sw.ElapsedMilliseconds);
            sw.Restart();
            if (_shopCanvas != null) Object.DestroyImmediate(_shopCanvas);
            WhHangProfileTrace.Mark("ShopV1ChromeTests.TearDown.after_Destroy_shopCanvas", sw.ElapsedMilliseconds);
            _shopGo = null;
            _shopCanvas = null;
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            sw.Restart();
            WhHangProfileTrace.Mark($"ShopV1ChromeTests.TearDown.before_Directory.Delete path={_scratchSaveDir}");
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, recursive: true);
            if (_scratchTelemetryDir != null && Directory.Exists(_scratchTelemetryDir))
                Directory.Delete(_scratchTelemetryDir, recursive: true);
            WhHangProfileTrace.Mark("ShopV1ChromeTests.TearDown.after_Directory.Delete", sw.ElapsedMilliseconds);
            WhHangProfileTrace.Mark("ShopV1ChromeTests.TearDown.exit");
        }

        /// <summary>PRODUCTIVE CODING TASK - Shop teardown hang fix. Every ShopPresenter.Initialize()
        /// call in this file used to omit the telemetryOutbox parameter, so ShopPresenter fell back
        /// to its own default: <c>new RetentionTelemetryOutbox(new UnityCloudCodeRetentionTelemetryGateway())</c>
        /// - a REAL Cloud Code gateway persisting to the REAL Application.persistentDataPath, not
        /// this test's isolated scratch dir. Every sibling presenter test that can reach a
        /// telemetry-emit path (BattlePassShellTests, DailyLoginQuestsShellTests,
        /// EmpireExpeditionShellTests, MetagameRetentionTelemetryEmitTests.ShopStaminaDailyCap_...)
        /// already injects a FakeRetentionTelemetryGateway + scratch dir instead - this file was
        /// the one outlier. UnityCloudCodeRetentionTelemetryGateway.ModuleDeployed=false currently
        /// short-circuits every real flush before any network/auth call (RELEASE BLOCKER FIX
        /// 2026-09-03), which is why this file's own two tests don't hang today - but that global
        /// gate is a single static flag one future flip (or the real Telemetry module shipping)
        /// away from turning any fire-and-forget FlushAsync() in this file back into a live,
        /// un-awaited network call against real device storage - exactly the shape of hang this
        /// task was dispatched to fix. Builds this file's own ShopPresenter instances through the
        /// same isolated, deterministic gateway every other presenter's tests already use, so this
        /// file can never again be the one exception relying on a global gate to stay fast.</summary>
        private ShopPresenter BuildShopWithFakeTelemetry(GameObject host, PlayerProfile profile,
            out FakeRetentionTelemetryGateway fakeGateway)
        {
            fakeGateway = new FakeRetentionTelemetryGateway();
            var outbox = new RetentionTelemetryOutbox(fakeGateway, _scratchTelemetryDir);
            var shop = host.AddComponent<ShopPresenter>();
            shop.Initialize(profile, onBackToHome: null, telemetryOutbox: outbox);
            return shop;
        }

        [Test]
        public void ShopV1Pack_IsPresentInResources()
        {
            Assert.IsTrue(ShopV1UiLibrary.HasShopV1Pack,
                "Shop V1 Resources/UI/ShopV1 pack must include catalog shell, pack-open frame, and stamina tier slices.");
        }

        [Test]
        public void BuildShop_UsesCatalogShellBackground_AndStaminaStateSprites()
        {
            var profile = new PlayerProfile { gems = 500, stamina = 20, maxStamina = 100 };
            CollectionSchemaMigration.Apply(profile);

            _shopGo = new GameObject("ShopV1ChromeHarness");
            var shop = BuildShopWithFakeTelemetry(_shopGo, profile, out _);
            _shopCanvas = GameObject.Find("ShopCanvas");
            Assert.IsNotNull(_shopCanvas);

            Image bg = _shopCanvas.transform.Find("Background")?.GetComponent<Image>();
            Assert.IsNotNull(bg);
            Assert.IsNotNull(bg.sprite, "Shop background must use the catalog shell sprite.");
            Assert.AreEqual(ShopV1UiLibrary.CatalogShellName, bg.sprite.name);

            // Fresh ladder: tier 1 unlocked art, tiers 2–4 locked art.
            Assert.AreEqual(
                ShopV1UiLibrary.LoadStaminaTierSprite(1, unlocked: true),
                _shopCanvas.transform.Find($"ShopGrid/ShopCard_{ShopStaminaCatalog.SkuIdForGemCost(30)}")
                    ?.GetComponent<Image>()?.sprite,
                "Tier 1 must start on unlocked stamina sprite.");
            Assert.AreEqual(
                ShopV1UiLibrary.LoadStaminaTierSprite(2, unlocked: false),
                _shopCanvas.transform.Find($"ShopGrid/ShopCard_{ShopStaminaCatalog.SkuIdForGemCost(60)}")
                    ?.GetComponent<Image>()?.sprite,
                "Tier 2 must start on locked stamina sprite.");

            var buySw = Stopwatch.StartNew();
            WhHangProfileTrace.Mark("ShopV1ChromeTests.before_PurchaseForTests_tier30");
            Assert.IsTrue(shop.PurchaseForTests(ShopStaminaCatalog.SkuIdForGemCost(30)));
            WhHangProfileTrace.Mark("ShopV1ChromeTests.after_PurchaseForTests_tier30", buySw.ElapsedMilliseconds);

            WhHangProfileTrace.Mark("ShopV1ChromeTests.before_post_purchase_asserts");
            Assert.AreEqual(
                ShopV1UiLibrary.LoadStaminaTierSprite(2, unlocked: true),
                _shopCanvas.transform.Find($"ShopGrid/ShopCard_{ShopStaminaCatalog.SkuIdForGemCost(60)}")
                    ?.GetComponent<Image>()?.sprite,
                "After buying tier 1, tier 2 must swap to unlocked sprite.");
            Assert.AreEqual(
                ShopV1UiLibrary.LoadStaminaTierSprite(1, unlocked: false),
                _shopCanvas.transform.Find($"ShopGrid/ShopCard_{ShopStaminaCatalog.SkuIdForGemCost(30)}")
                    ?.GetComponent<Image>()?.sprite,
                "After buying tier 1, tier 1 must swap to locked sprite (no longer the next step).");
            WhHangProfileTrace.Mark("ShopV1ChromeTests.after_post_purchase_asserts");
        }

        [Test]
        public void BuildShop_GemPackTiles_UseSharedFrameAndRuntimeWells()
        {
            var profile = new PlayerProfile { gems = 500, stamina = 20, maxStamina = 100 };
            CollectionSchemaMigration.Apply(profile);

            _shopGo = new GameObject("ShopV1GemPackHarness");
            var shop = BuildShopWithFakeTelemetry(_shopGo, profile, out _);
            _shopCanvas = GameObject.Find("ShopCanvas");
            Assert.IsNotNull(_shopCanvas);
            Assert.IsTrue(ShopV1UiLibrary.HasGemPackTile);

            Transform sigil = _shopCanvas.transform.Find($"ShopGrid/ShopCard_{CollectionPackCatalog.SingleSigilSkuId}");
            Assert.IsNotNull(sigil, "Single Sigil pack tile must exist in the catalog wells.");

            Image frame = sigil.Find("GemPackFrame")?.GetComponent<Image>();
            Assert.IsNotNull(frame);
            Assert.AreEqual(ShopV1UiLibrary.GemPackTileName, frame.sprite.name);
            Assert.IsFalse(frame.raycastTarget, "Shared frame is decorative; BUY owns the hit target.");
            Assert.IsTrue(frame.preserveAspect, "Gem-pack frame must not stretch.");

            Text title = sigil.Find("Title")?.GetComponent<Text>();
            Assert.IsNotNull(title);
            Assert.AreEqual("Single Sigil", title.text);

            Text price = sigil.Find("PriceLabel")?.GetComponent<Text>();
            Assert.IsNotNull(price);
            StringAssert.Contains("150", price.text);
            StringAssert.Contains("Gem", price.text);

            Transform pity = sigil.Find("PityLine");
            Assert.IsNotNull(pity);
            Assert.IsFalse(pity.gameObject.activeSelf, "Single Sigil has no high-draw pity; hide the pity well.");

            Button buy = sigil.Find("Btn_Buy")?.GetComponent<Button>();
            Assert.IsNotNull(buy);
            Assert.AreEqual("BUY", sigil.Find("Btn_Buy/PriceText")?.GetComponent<Text>()?.text);

            Transform scout = _shopCanvas.transform.Find($"ShopGrid/ShopCard_{CollectionPackCatalog.ScoutCacheSkuId}");
            Assert.IsNotNull(scout);
            Assert.IsTrue(scout.Find("PityLine").gameObject.activeSelf,
                "Scout Cache has high draws; pity well must stay live and runtime-owned.");
        }

        /// <summary>PRODUCTIVE CODING TASK - Shop teardown hang regression guard. Exhausts the
        /// real 4/24h Stamina ladder (buying all four GemCosts tiers) and then attempts a fifth
        /// purchase - the exact condition that drives ShopPresenter.AttemptPurchase into
        /// EmitStaminaDailyCapReached()'s Enqueue+fire-and-forget FlushAsync() path. Neither of
        /// this file's other two tests ever reach that path (each buys at most one tier), so it
        /// was previously untested here even though it is this file's only route to a telemetry
        /// flush. Runs through the injected FakeRetentionTelemetryGateway (no real network/auth,
        /// no real Application.persistentDataPath) and asserts the whole purchase-attempt-through-
        /// TearDown sequence completes in well under RetentionTelemetryOutbox.FlushDeadline (8s) -
        /// if this file ever regresses back to a real gateway (e.g. a future call site forgets
        /// telemetryOutbox again once UnityCloudCodeRetentionTelemetryGateway.ModuleDeployed flips
        /// true), this test starts timing out long before the wrapper's own hang-kill fires.</summary>
        [Test]
        public void ExhaustingTheStaminaDailyCap_EmitsTelemetryThroughTheFakeGateway_AndTearsDownFast()
        {
            var profile = new PlayerProfile { gems = 10000, stamina = 0, maxStamina = 100000 };
            CollectionSchemaMigration.Apply(profile);

            _shopGo = new GameObject("ShopV1DailyCapHarness");
            var shop = BuildShopWithFakeTelemetry(_shopGo, profile, out FakeRetentionTelemetryGateway fakeGateway);
            _shopCanvas = GameObject.Find("ShopCanvas");
            Assert.IsNotNull(_shopCanvas);

            var totalSw = Stopwatch.StartNew();
            foreach (int gemCost in ShopStaminaCatalog.GemCosts)
            {
                Assert.IsTrue(shop.PurchaseForTests(ShopStaminaCatalog.SkuIdForGemCost(gemCost)),
                    $"Setup: tier {gemCost} must exist and be purchasable to reach the daily cap.");
            }

            // The ladder is exhausted (4/4 for today) - this fifth attempt at the last tier's own
            // price must be refused AND must emit DailyStaminaRefillCapReached through the fake,
            // not a real gateway.
            int lastTierGemCost = ShopStaminaCatalog.GemCosts[ShopStaminaCatalog.GemCosts.Length - 1];
            shop.PurchaseForTests(ShopStaminaCatalog.SkuIdForGemCost(lastTierGemCost));

            Assert.AreEqual(1, fakeGateway.SentEvents.Count,
                "Exhausting the daily Stamina cap must emit exactly one daily_cap_reached event.");
            Assert.AreEqual(RetentionTelemetryEvents.EventTypeDailyCapReached, fakeGateway.SentEvents[0].eventType);
            Assert.AreEqual("shop_stamina", fakeGateway.SentEvents[0].mode);
            Assert.AreEqual("DailyStaminaRefillCapReached", fakeGateway.SentEvents[0].outcome);

            Assert.Less(totalSw.ElapsedMilliseconds, 2000,
                "The daily-cap purchase attempt (Enqueue + fire-and-forget FlushAsync through the " +
                "fake gateway) must resolve near-instantly - a multi-second stall here is the exact " +
                "regression this test guards against (a real network-backed gateway sneaking back " +
                "into this file).");
        }
    }
}

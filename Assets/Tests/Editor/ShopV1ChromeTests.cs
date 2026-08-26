using System.Diagnostics;
using System.IO;
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

        [SetUp]
        public void SetUp()
        {
            WhHangProfileTrace.Mark("ShopV1ChromeTests.SetUp.enter");
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsShopV1Chrome_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
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
            WhHangProfileTrace.Mark("ShopV1ChromeTests.TearDown.after_Directory.Delete", sw.ElapsedMilliseconds);
            WhHangProfileTrace.Mark("ShopV1ChromeTests.TearDown.exit");
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
            var shop = _shopGo.AddComponent<ShopPresenter>();
            shop.Initialize(profile, onBackToHome: null);
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
            var shop = _shopGo.AddComponent<ShopPresenter>();
            shop.Initialize(profile, onBackToHome: null);
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
    }
}

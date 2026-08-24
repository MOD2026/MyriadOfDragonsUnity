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
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsShopV1Chrome_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            if (_shopGo != null) Object.DestroyImmediate(_shopGo);
            if (_shopCanvas != null) Object.DestroyImmediate(_shopCanvas);
            _shopGo = null;
            _shopCanvas = null;
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, recursive: true);
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

            Assert.IsTrue(shop.PurchaseForTests(ShopStaminaCatalog.SkuIdForGemCost(30)));

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
        }
    }
}

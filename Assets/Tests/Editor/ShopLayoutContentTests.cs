using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Data;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Shop content-in-wells regression — empty bordered chrome from shell/tile art must be filled
    /// (icon/product art + copy + BUY label), and stamina Desc must never share CreateTextElement's
    /// center-anchor pile with BUY (the long-standing overlap bug).
    /// </summary>
    public class ShopLayoutContentTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MoDShopLayoutContent_" + System.Guid.NewGuid().ToString("N"));
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
            foreach (Canvas c in Object.FindObjectsOfType<Canvas>())
            {
                if (c != null && c.name == "ShopCanvas")
                    Object.DestroyImmediate(c.gameObject);
            }
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, true);
        }

        private ShopPresenter OpenShop()
        {
            var profile = new PlayerProfile { gems = 500, stamina = 20, maxStamina = 100 };
            CollectionSchemaMigration.Apply(profile);
            Assert.IsTrue(SaveSystem.Save(profile));
            SaveSystem.ResetCurrentProfileForTests();

            var go = new GameObject("ShopLayoutContentHarness");
            _spawned.Add(go);
            var shop = go.AddComponent<ShopPresenter>();
            shop.Initialize(SaveSystem.CurrentProfile ?? SaveManager.SaveData, onBackToHome: null);
            return shop;
        }

        private static Rect WorldRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            float xMin = c.Min(v => v.x), xMax = c.Max(v => v.x);
            float yMin = c.Min(v => v.y), yMax = c.Max(v => v.y);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        [Test]
        public void StaminaRows_AssignIconSprite_AndCopyDoesNotOverlapBuyLabel()
        {
            ShopPresenter shop = OpenShop();
            Transform canvas = GameObject.Find("ShopCanvas").transform;

            foreach (int gemCost in ShopStaminaCatalog.GemCosts)
            {
                string id = ShopStaminaCatalog.SkuIdForGemCost(gemCost);
                Transform card = canvas.Find($"ShopGrid/ShopCard_{id}");
                Assert.IsNotNull(card, $"Missing stamina card {id}");

                Image icon = card.Find("StaminaIcon")?.GetComponent<Image>();
                Assert.IsNotNull(icon, $"{id}: StaminaIcon missing");
                Assert.IsNotNull(icon.sprite, $"{id}: empty circle — icon sprite never assigned");

                Text copy = card.Find("Copy")?.GetComponent<Text>();
                Text buyLabel = card.Find("Btn_Buy/PriceText")?.GetComponent<Text>();
                Assert.IsNotNull(copy, $"{id}: Copy well missing");
                Assert.IsNotNull(buyLabel, $"{id}: BUY label missing");
                Assert.IsNull(card.Find("Desc"), $"{id}: legacy center-anchored Desc must be gone");
                Assert.IsNull(card.Find("Title"), $"{id}: legacy center-anchored Title must be gone");

                Rect copyRect = WorldRect(copy.rectTransform);
                Rect buyRect = WorldRect(buyLabel.rectTransform);
                Assert.IsFalse(copyRect.Overlaps(buyRect),
                    $"{id}: Copy {copyRect} overlaps BUY label {buyRect}");

                Image buyImg = card.Find("Btn_Buy")?.GetComponent<Image>();
                Assert.IsNotNull(buyImg);
                Assert.IsNull(buyImg.sprite,
                    $"{id}: BUY must be an invisible hit-target — ApplyNavTileButton secondary chrome paints a second empty box");
            }
        }

        [Test]
        public void GemPackTiles_FillProductArtWell_AndBuyIsHitTargetOnly()
        {
            OpenShop();
            Transform canvas = GameObject.Find("ShopCanvas").transform;

            int packCards = 0;
            foreach (Transform child in canvas.Find("ShopGrid"))
            {
                if (child.name.StartsWith("ShopCard_res_")) continue;
                if (!child.name.StartsWith("ShopCard_")) continue;
                packCards++;

                Image art = child.Find("ProductArt")?.GetComponent<Image>();
                Assert.IsNotNull(art, $"{child.name}: ProductArt missing");
                Assert.IsNotNull(art.sprite,
                    $"{child.name}: ProductArt well empty — product_art_* missing and fallbacks failed");

                Image buyImg = child.Find("Btn_Buy")?.GetComponent<Image>();
                Assert.IsNotNull(buyImg);
                Assert.IsNull(buyImg.sprite,
                    $"{child.name}: BUY must not receive ui_button_secondary_* (empty box under BUY)");
                Assert.IsNotNull(child.Find("Btn_Buy/PriceText")?.GetComponent<Text>(),
                    $"{child.name}: BUY label missing");
            }

            Assert.Greater(packCards, 0, "Expected at least one gem-pack tile on the live Shop grid.");
        }

        [Test]
        public void Header_BackIsHitTargetOnly_NotSecondaryButtonChrome()
        {
            OpenShop();
            Transform canvas = GameObject.Find("ShopCanvas").transform;
            Image backImg = canvas.Find("Btn_Back")?.GetComponent<Image>();
            Assert.IsNotNull(backImg);
            Assert.IsNull(backImg.sprite,
                "Header Back must not get ApplyNavTileButton secondary chrome (empty box beside Back).");
            Assert.IsNotNull(canvas.Find("Btn_Back/Text")?.GetComponent<Text>());
            Assert.IsNotNull(canvas.Find("Title")?.GetComponent<Text>());
        }
    }
}

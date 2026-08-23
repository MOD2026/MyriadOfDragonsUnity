using System.IO;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// MVP gate — Shop V2 Stamina Gem-tier ladder is reachable on the live grid and escalates
    /// in order without dead-ending (blocked tiers stay non-interactable; status explains why).
    /// </summary>
    public class ShopStaminaLadderUiTests
    {
        private GameObject _shopGo;
        private GameObject _shopCanvas;
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsShopStaminaLadder_" + System.Guid.NewGuid().ToString("N"));
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
        public void BuildShop_ShowsAllFourLiveStaminaLadderTiles()
        {
            ShopPresenter shop = SpawnShop(NewProfile(gems: 500, stamina: 20));

            foreach (int gemCost in ShopStaminaCatalog.GemCosts)
            {
                string skuId = ShopStaminaCatalog.SkuIdForGemCost(gemCost);
                Assert.IsTrue(shop.ShopGridContainsSkuForTests(skuId),
                    $"Live Shop grid must include Stamina ladder SKU '{skuId}' ({gemCost} Gems).");
            }
        }

        [Test]
        public void Ladder_OnlyFirstTierInteractable_UntilPurchased_ThenEscalates()
        {
            PlayerProfile profile = NewProfile(gems: 500, stamina: 20);
            ShopPresenter shop = SpawnShop(profile);

            Assert.IsTrue(shop.StaminaBuyButtonInteractableForTests(30),
                "First ladder step (30 Gems) must be purchasable on a fresh profile.");
            Assert.IsFalse(shop.StaminaBuyButtonInteractableForTests(60),
                "Second ladder step must stay locked until the first tier is bought.");

            Assert.IsTrue(shop.PurchaseForTests(ShopStaminaCatalog.SkuIdForGemCost(30)));
            Assert.AreEqual(70, profile.stamina, "First tier must grant +50 Stamina.");
            Assert.AreEqual(470, profile.gems, "First tier must spend exactly 30 Gems.");

            Assert.IsTrue(shop.StaminaBuyButtonInteractableForTests(60),
                "Second ladder step must unlock after the first purchase.");
            Assert.IsFalse(shop.StaminaBuyButtonInteractableForTests(120),
                "Third ladder step must remain locked until the second tier is bought.");
        }

        [Test]
        public void Ladder_OutOfOrderPurchase_IsBlocked_WithStatus_AndDoesNotSpendGems()
        {
            PlayerProfile profile = NewProfile(gems: 500, stamina: 20);
            ShopPresenter shop = SpawnShop(profile);

            Assert.IsTrue(shop.PurchaseForTests(ShopStaminaCatalog.SkuIdForGemCost(120)));

            Assert.AreEqual(500, profile.gems, "Skipping ahead on the ladder must not spend Gems.");
            Assert.AreEqual(20, profile.stamina, "Skipping ahead on the ladder must not grant Stamina.");
            Assert.IsFalse(string.IsNullOrEmpty(shop.ShopStatusTextForTests),
                "Blocked ladder purchase must surface a status message instead of dead-ending silently.");
            StringAssert.Contains("30", shop.ShopStatusTextForTests,
                "Status must tell the player which Gem tier is next on the ladder.");
        }

        private ShopPresenter SpawnShop(PlayerProfile profile)
        {
            _shopGo = new GameObject("ShopStaminaLadderHarness");
            var shop = _shopGo.AddComponent<ShopPresenter>();
            shop.Initialize(profile, onBackToHome: null);
            _shopCanvas = GameObject.Find("ShopCanvas");
            return shop;
        }

        private static PlayerProfile NewProfile(int gems, int stamina)
        {
            var profile = new PlayerProfile
            {
                gems = gems,
                stamina = stamina,
                maxStamina = 100,
            };
            CollectionSchemaMigration.Apply(profile);
            return profile;
        }
    }
}

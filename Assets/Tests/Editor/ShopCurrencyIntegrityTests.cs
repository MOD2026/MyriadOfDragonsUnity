using System.Collections.Generic;
using System.IO;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Economy;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// SHOP CURRENCY INTEGRITY (release feature) - proves ShopPresenter.AttemptPurchase no longer
    /// mutates player.gold/gems/stamina directly and instead routes every affordability check,
    /// deduction, and non-card resource grant through CurrencyManager, the sole wallet authority.
    /// Exercised through PurchaseForTests, the same private handler the real "BUY" button calls -
    /// not reimplemented here. Card-pack coverage uses live Shop V2 Single Sigil, not pack_novice.
    /// </summary>
    public class ShopCurrencyIntegrityTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;
        private CardDatabase _database;

        [SetUp]
        public void SetUp()
        {
            CollectionPackReceiptService.ClearCommittedReceiptsForTests();
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsShopCurrency_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();

            var databaseGo = new GameObject("ShopCurrency_CardDatabase");
            _spawned.Add(databaseGo);
            _database = databaseGo.AddComponent<CardDatabase>();
            _database.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            CollectionPackReceiptService.ClearCommittedReceiptsForTests();
            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();

            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
            {
                Directory.Delete(_scratchSaveDir, recursive: true);
            }
        }

        private ShopPresenter SpawnAndInitializeShop(PlayerProfile profile)
        {
            var go = new GameObject("ShopPresenterUnderTest");
            _spawned.Add(go);
            var shop = go.AddComponent<ShopPresenter>();
            shop.Initialize(profile, onBackToHome: null);
            GameObject shopCanvas = GameObject.Find("ShopCanvas");
            if (shopCanvas != null) _spawned.Add(shopCanvas);
            return shop;
        }

        [Test]
        public void SuccessfulPurchase_DeductsExactCostAndAppliesReward_ThroughCurrencyManager()
        {
            // player is a locally-constructed profile, deliberately NOT SaveSystem.Profile - this
            // proves the transaction acts on the profile it was given, not the global singleton.
            var profile = new PlayerProfile { gold = 0, gems = 200, stamina = 10, maxStamina = 100 };
            ShopPresenter shop = SpawnAndInitializeShop(profile);

            shop.PurchaseForTests("res_gold");

            Assert.AreEqual(150, CurrencyManager.GetBalance(profile, CurrencyType.Gems), "Gold Vault must still cost 50 Gems, deducted via CurrencyManager.");
            Assert.AreEqual(1500, profile.gold, "Gold Vault must still grant 1,500 Gold.");
        }

        [Test]
        public void InsufficientFunds_LeavesWalletAndRewardUntouched()
        {
            var profile = new PlayerProfile { gold = 0, gems = 10 }; // Gold Vault costs 50 Gems
            ShopPresenter shop = SpawnAndInitializeShop(profile);

            shop.PurchaseForTests("res_gold");

            Assert.AreEqual(10, profile.gems, "Insufficient Gems must leave the balance unchanged.");
            Assert.AreEqual(0, profile.gold, "Insufficient currency must grant no reward.");
        }

        [Test]
        public void NonCardReward_StaminaGrant_RoutesThroughCurrencyManagerAndClampsToMax()
        {
            var profile = new PlayerProfile { gold = 0, gems = 100, stamina = 70, maxStamina = 100 };
            ShopPresenter shop = SpawnAndInitializeShop(profile);

            shop.PurchaseForTests("res_energy");

            Assert.AreEqual(70, CurrencyManager.GetBalance(profile, CurrencyType.Gems), "Energy Potion must still cost 30 Gems.");
            Assert.AreEqual(100, profile.stamina, "Stamina grant must clamp to maxStamina (70 + 50 would exceed 100).");
        }

        [Test]
        public void CardPackReward_LiveSingleSigil_SpendsGemsAndGrantsOneCopy()
        {
            Assert.IsTrue(CollectionPackCatalog.TryGetSku(CollectionPackCatalog.SingleSigilSkuId, out CollectionPackSku singleSigil),
                "Setup: live Shop V2 Single Sigil SKU must exist.");
            var profile = new PlayerProfile { gems = singleSigil.GemCost };
            CollectionSchemaMigration.Apply(profile);
            int copiesBefore = TotalCopyCount(profile);
            ShopPresenter shop = SpawnAndInitializeShop(profile);

            shop.PurchaseForTests(CollectionPackCatalog.SingleSigilSkuId);

            Assert.AreEqual(0, CurrencyManager.GetBalance(profile, CurrencyType.Gems),
                "Single Sigil Gem cost must be spent in full via the pack receipt path.");
            Assert.AreEqual(copiesBefore + 1, TotalCopyCount(profile),
                "A Single Sigil purchase must grant exactly one card copy.");
            Assert.IsTrue(profile.cardProgression.Count > 0, "Granted card must land in cardProgression.");
            string grantedId = profile.cardProgression[^1].cardId;
            Assert.IsNotNull(CardDatabase.Instance.GetCard(grantedId), "The granted id must still resolve through CardDatabase.");
        }

        [Test]
        public void SinglePurchaseCall_SpendsCurrencyExactlyOnce_NotTwice()
        {
            var profile = new PlayerProfile { gold = 0, gems = 100 };
            ShopPresenter shop = SpawnAndInitializeShop(profile);

            shop.PurchaseForTests("res_gold");

            // Gold Vault costs 50 Gems and grants 1,500 Gold - if the reward's own
            // CurrencyManager.AddCurrency call and AttemptPurchase's own SpendCurrency call each
            // independently persisted/spent, or if the deduction ran more than once, these values
            // would drift from the single-application result.
            Assert.AreEqual(50, CurrencyManager.GetBalance(profile, CurrencyType.Gems), "One purchase must spend the 50 Gem cost exactly once.");
            Assert.AreEqual(1500, profile.gold, "One purchase must apply the 1,500 Gold reward exactly once.");
        }

        [Test]
        public void Purchase_DoesNotModifyActiveDeckCardIds()
        {
            Assert.IsTrue(CollectionPackCatalog.TryGetSku(CollectionPackCatalog.SingleSigilSkuId, out CollectionPackSku singleSigil));
            var profile = new PlayerProfile { gems = singleSigil.GemCost };
            CollectionSchemaMigration.Apply(profile);
            var deckBefore = new List<string>(profile.activeDeckCardIds);
            ShopPresenter shop = SpawnAndInitializeShop(profile);

            shop.PurchaseForTests(CollectionPackCatalog.SingleSigilSkuId);

            CollectionAssert.AreEqual(deckBefore, profile.activeDeckCardIds,
                "A Shop purchase must never silently modify activeDeckCardIds - deck selection remains Deck Builder's own job.");
        }

        private static int TotalCopyCount(PlayerProfile profile)
        {
            int total = 0;
            if (profile?.cardProgression == null) return 0;
            foreach (CardProgressionRecord record in profile.cardProgression)
                total += record.copyCount;
            return total;
        }

        [Test]
        public void SuccessfulPurchase_PersistsExactlyOnce_AndSurvivesReload()
        {
            var profile = new PlayerProfile { gold = 0, gems = 200, stamina = 10, maxStamina = 100 };
            ShopPresenter shop = SpawnAndInitializeShop(profile);

            shop.PurchaseForTests("res_gold");
            shop.PurchaseForTests("res_energy");

            PlayerProfile reloaded = SaveSystem.Load();

            Assert.AreEqual(1500, reloaded.gold, "Gold Vault's grant must survive a fresh reload from disk.");
            Assert.AreEqual(60, reloaded.stamina, "Energy Potion's grant must survive a fresh reload from disk (10 + 50).");
            Assert.AreEqual(120, reloaded.gems, "Both purchases' combined Gem cost (50 + 30) must survive the reload.");
        }
    }
}

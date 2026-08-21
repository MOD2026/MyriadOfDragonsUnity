using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// SHOP CARD PERSISTENCE (release feature) - proves the Shop -> owned-card loop through the
    /// real production handler, ShopPresenter.AttemptPurchase, via PurchaseForTests (the same
    /// private method the real "BUY" button calls - EditMode tests have no way to click a UI
    /// Button). Card-granting reward logic (TryGrantNextUnownedCard) is exercised exactly as
    /// production runs it, not reimplemented here.
    /// </summary>
    public class ShopCardPersistenceTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;
        private CardDatabase _database;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsShop_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();

            var databaseGo = new GameObject("Shop_CardDatabase");
            _spawned.Add(databaseGo);
            _database = databaseGo.AddComponent<CardDatabase>();
            _database.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
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

        /// <summary>Every real card id the Shop is allowed to grant - CardDatabase's own order,
        /// excluding the placeholder "dragon" id, exactly matching ShopPresenter's own
        /// TryGrantNextUnownedCard rule (not a separately-maintained expectation).</summary>
        private List<string> RealGrantableCardIds() =>
            _database.AllCards.Select(c => c.Id).Where(id => id != "dragon").ToList();

        [Test]
        public void ValidPurchase_SpendsGoldOnlyWhenAffordable_AndGrantsExactlyOneRealCardId()
        {
            var profile = new PlayerProfile { gold = 500, gems = 0 };
            int cardsBefore = profile.cardCollection.Count;
            ShopPresenter shop = SpawnAndInitializeShop(profile);

            bool found = shop.PurchaseForTests("pack_novice");

            Assert.IsTrue(found, "Setup: expected the Novice Card Pack item to exist.");
            Assert.AreEqual(0, profile.gold, "The displayed 500 Gold cost must be spent in full when affordable.");
            Assert.AreEqual(cardsBefore + 1, profile.cardCollection.Count, "A card-pack purchase must grant exactly one card.");

            string grantedId = profile.cardCollection[^1];
            Assert.IsNotNull(CardDatabase.Instance.GetCard(grantedId),
                $"The granted id '{grantedId}' must resolve through the existing CardDatabase.");
        }

        [Test]
        public void ShopNeverGrantsThePlaceholderDragonId()
        {
            var profile = new PlayerProfile { gold = 500, gems = 100 };
            // Own everything except the very last few real cards and "dragon" itself, so the
            // next grant is forced to land near the end of the sequence - if "dragon" were ever
            // eligible, this is the setup most likely to expose it.
            List<string> realIds = RealGrantableCardIds();
            profile.cardCollection = new List<string>(realIds.Take(realIds.Count - 2));

            ShopPresenter shop = SpawnAndInitializeShop(profile);
            shop.PurchaseForTests("pack_novice");
            shop.PurchaseForTests("pack_dragon");

            CollectionAssert.DoesNotContain(profile.cardCollection, "dragon",
                "The Shop must never grant the placeholder id 'dragon', under any purchase or sequence position.");
        }

        [Test]
        public void GrantedCard_PersistsAcrossASaveReload()
        {
            var profile = new PlayerProfile { gold = 500, gems = 0 };
            ShopPresenter shop = SpawnAndInitializeShop(profile);

            shop.PurchaseForTests("pack_novice");
            string grantedId = profile.cardCollection[^1];

            PlayerProfile reloaded = SaveSystem.Load();

            CollectionAssert.Contains(reloaded.cardCollection, grantedId,
                "The granted card must survive a fresh reload from disk, not just the in-memory profile.");
            Assert.AreEqual(0, reloaded.gold, "The spent Gold must also survive the reload.");
        }

        [Test]
        public void RepeatedPurchases_GrantDifferentCards_NoDuplicates()
        {
            var profile = new PlayerProfile { gold = 5000, gems = 0 };
            ShopPresenter shop = SpawnAndInitializeShop(profile);

            for (int i = 0; i < 5; i++)
            {
                shop.PurchaseForTests("pack_novice");
            }

            Assert.AreEqual(5, profile.cardCollection.Count, "Setup: expected five successful grants.");
            Assert.AreEqual(profile.cardCollection.Count, profile.cardCollection.Distinct().Count(),
                "Repeated purchases must never grant a duplicate card id.");
        }

        [Test]
        public void InsufficientCurrency_ChangesNothing()
        {
            var profile = new PlayerProfile { gold = 100, gems = 0 }; // Novice pack costs 500 Gold
            int cardsBefore = profile.cardCollection.Count;
            var collectionBefore = new List<string>(profile.cardCollection);
            ShopPresenter shop = SpawnAndInitializeShop(profile);

            shop.PurchaseForTests("pack_novice");

            Assert.AreEqual(100, profile.gold, "Insufficient Gold must leave the balance unchanged.");
            Assert.AreEqual(cardsBefore, profile.cardCollection.Count, "Insufficient currency must grant no card.");
            CollectionAssert.AreEqual(collectionBefore, profile.cardCollection, "Insufficient currency must leave the collection byte-for-byte unchanged.");
        }

        [Test]
        public void ExhaustedRewardSequence_DoesNotSpendCurrency_AndShowsExistingStatusSurface()
        {
            var profile = new PlayerProfile { gold = 500, gems = 100 };
            // Own every real, grantable card already - the sequence has nothing left to give.
            profile.cardCollection = RealGrantableCardIds();
            int cardsBefore = profile.cardCollection.Count;

            ShopPresenter shop = SpawnAndInitializeShop(profile);
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(".*could not be fulfilled.*"));

            shop.PurchaseForTests("pack_novice");

            Assert.AreEqual(500, profile.gold, "An exhausted reward sequence must not spend Gold.");
            Assert.AreEqual(cardsBefore, profile.cardCollection.Count, "An exhausted reward sequence must grant no card and add no duplicate.");
        }

        [Test]
        public void Purchase_DoesNotModifyActiveDeckCardIds()
        {
            var profile = new PlayerProfile { gold = 500, gems = 0 };
            var deckBefore = new List<string>(profile.activeDeckCardIds);
            ShopPresenter shop = SpawnAndInitializeShop(profile);

            shop.PurchaseForTests("pack_novice");

            CollectionAssert.AreEqual(deckBefore, profile.activeDeckCardIds,
                "A Shop purchase must never silently modify activeDeckCardIds - deck selection remains Deck Builder's own job.");
        }

        [Test]
        public void NonCardPurchases_KeepTheirExistingEffectAndPersistence()
        {
            var profile = new PlayerProfile { gold = 0, gems = 200, stamina = 10, maxStamina = 100 };
            ShopPresenter shop = SpawnAndInitializeShop(profile);

            shop.PurchaseForTests("res_gold");
            Assert.AreEqual(150, profile.gems, "Gold Vault must still cost 50 Gems.");
            Assert.AreEqual(1500, profile.gold, "Gold Vault must still grant 1,500 Gold.");

            shop.PurchaseForTests("res_energy");
            Assert.AreEqual(120, profile.gems, "Energy Potion must still cost 30 Gems.");
            Assert.AreEqual(60, profile.stamina, "Energy Potion must still restore +50 Stamina.");

            PlayerProfile reloaded = SaveSystem.Load();
            Assert.AreEqual(1500, reloaded.gold, "Gold Vault's grant must persist across a reload, same as before.");
            Assert.AreEqual(60, reloaded.stamina, "Energy Potion's grant must persist across a reload, same as before.");
        }
    }
}

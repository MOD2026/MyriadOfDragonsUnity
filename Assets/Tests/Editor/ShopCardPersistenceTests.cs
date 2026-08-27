using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// SHOP CARD PERSISTENCE (release feature) - proves the Shop -> owned-card loop through the
    /// real production handler, ShopPresenter.AttemptPurchase, via PurchaseForTests (the same
    /// private method the real "BUY" button calls - EditMode tests have no way to click a UI
    /// Button). Live Shop V2 Single Sigil gem pack receipt is the card-grant path under test —
    /// not the retired pack_novice stub.
    /// </summary>
    public class ShopCardPersistenceTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;
        private CardDatabase _database;

        [SetUp]
        public void SetUp()
        {
            CollectionPackReceiptService.ClearCommittedReceiptsForTests();
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsShop_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();

            var databaseGo = new GameObject("Shop_CardDatabase");
            _spawned.Add(databaseGo);
            _database = databaseGo.AddComponent<CardDatabase>();
            _database.Initialize();
            _database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
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

        private static PlayerProfile NewMigratedProfile(int gems)
        {
            var profile = new PlayerProfile { gems = gems };
            CollectionSchemaMigration.Apply(profile);
            return profile;
        }

        private List<string> RealGrantableCardIds() =>
            _database.AllCards.Select(c => c.Id).Where(id => id != "dragon").ToList();

        private static int TotalCopyCount(PlayerProfile profile)
        {
            int total = 0;
            if (profile?.cardProgression == null) return 0;
            foreach (CardProgressionRecord record in profile.cardProgression)
                total += record.copyCount;
            return total;
        }

        private static Dictionary<string, int> SnapshotProgression(PlayerProfile profile)
        {
            var snapshot = new Dictionary<string, int>();
            if (profile?.cardProgression == null) return snapshot;
            foreach (CardProgressionRecord record in profile.cardProgression)
                snapshot[record.cardId] = record.copyCount;
            return snapshot;
        }

        private static string FindNewlyGrantedCardId(Dictionary<string, int> before, PlayerProfile after)
        {
            foreach (CardProgressionRecord record in after.cardProgression)
            {
                before.TryGetValue(record.cardId, out int countBefore);
                if (record.copyCount > countBefore)
                    return record.cardId;
            }

            return null;
        }

        [Test]
        public void ValidPurchase_SpendsGemsOnlyWhenAffordable_AndGrantsExactlyOneRealCardId()
        {
            Assert.IsTrue(CollectionPackCatalog.TryGetSku(CollectionPackCatalog.SingleSigilSkuId, out CollectionPackSku singleSigil),
                "Setup: live Shop V2 Single Sigil SKU must exist.");
            PlayerProfile profile = NewMigratedProfile(gems: singleSigil.GemCost);
            int copiesBefore = TotalCopyCount(profile);
            ShopPresenter shop = SpawnAndInitializeShop(profile);

            bool found = shop.PurchaseForTests(CollectionPackCatalog.SingleSigilSkuId);

            Assert.IsTrue(found, "Setup: expected the live Single Sigil item to exist.");
            Assert.AreEqual(0, profile.gems, "The locked Single Sigil Gem cost must be spent in full when affordable.");
            Assert.AreEqual(copiesBefore + 1, TotalCopyCount(profile), "Single Sigil must grant exactly one card copy.");

            string grantedId = profile.cardProgression[^1].cardId;
            Assert.IsNotNull(CardDatabase.Instance.GetCard(grantedId),
                $"The granted id '{grantedId}' must resolve through the existing CardDatabase.");
            Assert.AreNotEqual("dragon", grantedId);
        }

        [Test]
        public void ShopNeverGrantsThePlaceholderDragonId()
        {
            Assert.IsTrue(CollectionPackCatalog.TryGetSku(CollectionPackCatalog.SingleSigilSkuId, out CollectionPackSku singleSigil));
            Assert.IsTrue(CollectionPackCatalog.TryGetSku(CollectionPackCatalog.ScoutCacheSkuId, out CollectionPackSku scoutCache));
            PlayerProfile profile = NewMigratedProfile(gems: singleSigil.GemCost + scoutCache.GemCost);

            // Own everything except the last few real cards so draws land near the end of the pool —
            // if "dragon" were ever eligible, this is the setup most likely to expose it.
            List<string> realIds = RealGrantableCardIds();
            foreach (string id in realIds.Take(realIds.Count - 2))
                CollectionProgression.TryGrantFirstCopy(profile, id, cid => CardDatabase.Instance.GetCard(cid) != null);

            ShopPresenter shop = SpawnAndInitializeShop(profile);
            shop.PurchaseForTests(CollectionPackCatalog.SingleSigilSkuId);
            shop.PurchaseForTests(CollectionPackCatalog.ScoutCacheSkuId);

            foreach (CardProgressionRecord record in profile.cardProgression)
            {
                Assert.AreNotEqual("dragon", record.cardId,
                    "The Shop must never grant the placeholder id 'dragon', under any live pack purchase.");
            }
        }

        [Test]
        public void GrantedCard_PersistsAcrossASaveReload()
        {
            Assert.IsTrue(CollectionPackCatalog.TryGetSku(CollectionPackCatalog.SingleSigilSkuId, out CollectionPackSku singleSigil));
            PlayerProfile profile = NewMigratedProfile(gems: singleSigil.GemCost);
            ShopPresenter shop = SpawnAndInitializeShop(profile);
            var before = SnapshotProgression(profile);

            shop.PurchaseForTests(CollectionPackCatalog.SingleSigilSkuId);
            string grantedId = FindNewlyGrantedCardId(before, profile);
            Assert.IsFalse(string.IsNullOrEmpty(grantedId), "Setup: Single Sigil must grant a card.");

            PlayerProfile reloaded = SaveSystem.Load();

            Assert.IsTrue(CollectionProgression.OwnsAnyCopy(reloaded, grantedId),
                "The granted card must survive a fresh reload from disk, not just the in-memory profile.");
            Assert.AreEqual(0, reloaded.gems, "The spent Gems must also survive the reload.");
        }

        [Test]
        public void RepeatedPurchases_EachGrantACardCopy_NeverGrantDragon()
        {
            Assert.IsTrue(CollectionPackCatalog.TryGetSku(CollectionPackCatalog.SingleSigilSkuId, out CollectionPackSku singleSigil));
            const int purchaseCount = 5;
            PlayerProfile profile = NewMigratedProfile(gems: singleSigil.GemCost * purchaseCount);
            ShopPresenter shop = SpawnAndInitializeShop(profile);
            int copiesBefore = TotalCopyCount(profile);

            for (int i = 0; i < purchaseCount; i++)
                shop.PurchaseForTests(CollectionPackCatalog.SingleSigilSkuId);

            Assert.AreEqual(copiesBefore + purchaseCount, TotalCopyCount(profile),
                "Each Single Sigil purchase must grant exactly one card copy (duplicates allowed as extra copies).");
            Assert.AreEqual(0, profile.gems, "Five Single Sigil purchases must spend the full Gem budget.");
            foreach (CardProgressionRecord record in profile.cardProgression)
                Assert.AreNotEqual("dragon", record.cardId);
        }

        [Test]
        public void InsufficientCurrency_ChangesNothing()
        {
            Assert.IsTrue(CollectionPackCatalog.TryGetSku(CollectionPackCatalog.SingleSigilSkuId, out CollectionPackSku singleSigil));
            PlayerProfile profile = NewMigratedProfile(gems: singleSigil.GemCost - 1);
            int copiesBefore = TotalCopyCount(profile);
            var progressionBefore = SnapshotProgression(profile);
            ShopPresenter shop = SpawnAndInitializeShop(profile);

            shop.PurchaseForTests(CollectionPackCatalog.SingleSigilSkuId);

            Assert.AreEqual(singleSigil.GemCost - 1, profile.gems, "Insufficient Gems must leave the balance unchanged.");
            Assert.AreEqual(copiesBefore, TotalCopyCount(profile), "Insufficient currency must grant no card.");
            CollectionAssert.AreEqual(progressionBefore, SnapshotProgression(profile),
                "Insufficient currency must leave progression byte-for-byte unchanged.");
        }

        [Test]
        public void OwningAllCards_StillGrantsDuplicateCopy_AndSpendsGems()
        {
            // Live Shop V2 packs are not "exhausted" by full ownership — they grant extra copies.
            Assert.IsTrue(CollectionPackCatalog.TryGetSku(CollectionPackCatalog.SingleSigilSkuId, out CollectionPackSku singleSigil));
            PlayerProfile profile = NewMigratedProfile(gems: singleSigil.GemCost);
            foreach (string id in RealGrantableCardIds())
                CollectionProgression.TryGrantFirstCopy(profile, id, cid => CardDatabase.Instance.GetCard(cid) != null);

            int uniqueBefore = profile.cardProgression.Count;
            int copiesBefore = TotalCopyCount(profile);
            ShopPresenter shop = SpawnAndInitializeShop(profile);

            shop.PurchaseForTests(CollectionPackCatalog.SingleSigilSkuId);

            Assert.AreEqual(0, profile.gems, "A full collection must still spend the Single Sigil Gem cost.");
            Assert.AreEqual(copiesBefore + 1, TotalCopyCount(profile),
                "Owning every card must still grant one additional copy, not withhold the purchase.");
            Assert.AreEqual(uniqueBefore, profile.cardProgression.Count,
                "The extra copy must land on an already-owned card id, not invent a new unique row.");
        }

        [Test]
        public void Purchase_DoesNotModifyActiveDeckCardIds()
        {
            Assert.IsTrue(CollectionPackCatalog.TryGetSku(CollectionPackCatalog.SingleSigilSkuId, out CollectionPackSku singleSigil));
            PlayerProfile profile = NewMigratedProfile(gems: singleSigil.GemCost);
            var deckBefore = new List<string>(profile.activeDeckCardIds);
            ShopPresenter shop = SpawnAndInitializeShop(profile);

            shop.PurchaseForTests(CollectionPackCatalog.SingleSigilSkuId);

            CollectionAssert.AreEqual(deckBefore, profile.activeDeckCardIds,
                "A Shop purchase must never silently modify activeDeckCardIds - deck selection remains Deck Builder's own job.");
        }

        [Test]
        public void NonCardPurchases_KeepTheirExistingEffectAndPersistence()
        {
            var profile = new PlayerProfile { gold = 0, gems = 200, stamina = 10, maxStamina = 100 };
            CollectionSchemaMigration.Apply(profile);
            ShopPresenter shop = SpawnAndInitializeShop(profile);

            // Gold Vault remains a PurchaseForTests-only V1 stub (withheld from live grid).
            shop.PurchaseForTests(ShopV1StubCatalog.GoldVaultId);
            Assert.AreEqual(150, profile.gems, "Gold Vault must still cost 50 Gems.");
            Assert.AreEqual(1500, profile.gold, "Gold Vault must still grant 1,500 Gold.");

            // First Stamina ladder tier is live Shop V2 (res_energy / 30 Gems).
            shop.PurchaseForTests(ShopStaminaCatalog.SkuIdForGemCost(30));
            Assert.AreEqual(120, profile.gems, "First Stamina ladder tier must still cost 30 Gems.");
            Assert.AreEqual(60, profile.stamina, "Stamina potion must still restore +50 Stamina.");

            PlayerProfile reloaded = SaveSystem.Load();
            Assert.AreEqual(1500, reloaded.gold, "Gold Vault's grant must persist across a reload, same as before.");
            Assert.AreEqual(60, reloaded.stamina, "Stamina potion's grant must persist across a reload, same as before.");
        }

        [Test]
        public void SamePurchaseReceiptId_DeduplicatesAcrossShopAttempts_DifferentReceiptIdsBothGrant()
        {
            Assert.IsTrue(CollectionPackCatalog.TryGetSku(CollectionPackCatalog.SingleSigilSkuId, out CollectionPackSku singleSigil));
            const int initialGems = 5000;
            PlayerProfile profile = NewMigratedProfile(gems: initialGems);
            ShopPresenter shop = SpawnAndInitializeShop(profile);

            int copiesBefore = TotalCopyCount(profile);
            const string stableReceiptA = "shop-purchase-attempt-stable-A";
            const string stableReceiptB = "shop-purchase-attempt-stable-B";

            // 1. First attempt with receipt A -> must spend Gems and grant 1 card copy.
            Assert.IsTrue(shop.PurchaseForTests(CollectionPackCatalog.SingleSigilSkuId, stableReceiptA));
            Assert.AreEqual(initialGems - singleSigil.GemCost, profile.gems, "First purchase with receipt A must spend gems.");
            Assert.AreEqual(copiesBefore + 1, TotalCopyCount(profile), "First purchase with receipt A must grant 1 card copy.");

            int gemsAfterA = profile.gems;
            int copiesAfterA = TotalCopyCount(profile);

            // 2. Second attempt (retry/double-tap) with the SAME receipt A -> must NOT spend Gems or grant duplicate cards.
            Assert.IsTrue(shop.PurchaseForTests(CollectionPackCatalog.SingleSigilSkuId, stableReceiptA));
            Assert.AreEqual(gemsAfterA, profile.gems, "Retry with same receipt A must NOT spend gems again.");
            Assert.AreEqual(copiesAfterA, TotalCopyCount(profile), "Retry with same receipt A must NOT grant duplicate card copies.");

            PlayerProfile reloadedAfterRetry = SaveSystem.Load();
            Assert.AreEqual(gemsAfterA, reloadedAfterRetry.gems, "Persisted gems must reflect exactly one purchase.");
            Assert.AreEqual(copiesAfterA, TotalCopyCount(reloadedAfterRetry), "Persisted card copies must reflect exactly one purchase.");

            // 3. Third attempt with DIFFERENT receipt B -> must spend Gems again and grant second card copy.
            Assert.IsTrue(shop.PurchaseForTests(CollectionPackCatalog.SingleSigilSkuId, stableReceiptB));
            Assert.AreEqual(gemsAfterA - singleSigil.GemCost, profile.gems, "New purchase with distinct receipt B must spend gems.");
            Assert.AreEqual(copiesAfterA + 1, TotalCopyCount(profile), "New purchase with distinct receipt B must grant a second card copy.");

            PlayerProfile reloadedAfterB = SaveSystem.Load();
            Assert.AreEqual(gemsAfterA - singleSigil.GemCost, reloadedAfterB.gems, "Persisted gems must reflect two total purchases.");
            Assert.AreEqual(copiesAfterA + 1, TotalCopyCount(reloadedAfterB), "Persisted card copies must reflect two total purchases.");
        }

        [Test]
        public void TryOpenGemPack_DirectInvocation_RespectsReceiptIdIdempotency()
        {
            Assert.IsTrue(CollectionPackCatalog.TryGetSku(CollectionPackCatalog.SingleSigilSkuId, out CollectionPackSku singleSigil));
            const int initialGems = 5000;
            PlayerProfile profile = NewMigratedProfile(gems: initialGems);

            const string receiptId1 = "direct-pack-attempt-1";
            const string receiptId2 = "direct-pack-attempt-2";

            int copiesBefore = TotalCopyCount(profile);

            // First call with receiptId1
            Assert.IsTrue(ShopPresenter.TryOpenGemPack(profile, CollectionPackCatalog.SingleSigilSkuId, receiptId1, out PackReceiptResult first));
            Assert.IsTrue(first.Success);
            Assert.AreEqual(receiptId1, first.ReceiptId);
            Assert.AreEqual(initialGems - singleSigil.GemCost, profile.gems);
            Assert.AreEqual(copiesBefore + 1, TotalCopyCount(profile));

            int gemsAfterFirst = profile.gems;
            int copiesAfterFirst = TotalCopyCount(profile);

            // Re-invoke with same receiptId1 -> idempotent return
            Assert.IsTrue(ShopPresenter.TryOpenGemPack(profile, CollectionPackCatalog.SingleSigilSkuId, receiptId1, out PackReceiptResult second));
            Assert.IsTrue(second.Success);
            Assert.AreEqual(first.ReceiptId, second.ReceiptId);
            Assert.AreEqual(gemsAfterFirst, profile.gems, "Re-invoking with same receiptId must not spend gems again.");
            Assert.AreEqual(copiesAfterFirst, TotalCopyCount(profile), "Re-invoking with same receiptId must not grant more card copies.");

            // Invoke with distinct receiptId2 -> grants second pack
            Assert.IsTrue(ShopPresenter.TryOpenGemPack(profile, CollectionPackCatalog.SingleSigilSkuId, receiptId2, out PackReceiptResult third));
            Assert.IsTrue(third.Success);
            Assert.AreEqual(receiptId2, third.ReceiptId);
            Assert.AreEqual(gemsAfterFirst - singleSigil.GemCost, profile.gems, "Distinct receiptId must spend gems.");
            Assert.AreEqual(copiesAfterFirst + 1, TotalCopyCount(profile), "Distinct receiptId must grant an additional card copy.");
        }
    }
}

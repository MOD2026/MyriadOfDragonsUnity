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
    /// <summary>Collection V1: Shop gem pack → cardProgression → DeckBuilder owned pool.</summary>
    public class ShopToDeckIntegrationTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            CollectionPackReceiptService.ClearCommittedReceiptsForTests();
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsShopToDeck_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();

            var databaseGo = new GameObject("CardDatabase_ShopToDeck");
            _spawned.Add(databaseGo);
            databaseGo.AddComponent<CardDatabase>().Initialize();
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

            foreach (string canvasName in new[] { "ShopCanvas", "DeckBuilderCanvas" })
            {
                GameObject canvas = GameObject.Find(canvasName);
                if (canvas != null) Object.DestroyImmediate(canvas);
            }

            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, recursive: true);
        }

        [Test]
        public void MigratedProfile_GemPackPurchase_AppearsInDeckBuilderOwnedPool()
        {
            PlayerProfile profile = NewMigratedProfile(gems: 500);
            Assert.IsTrue(profile.UsesCollectionV1);
            Assert.AreEqual(0, profile.cardProgression.Count);

            var shopGo = new GameObject("ShopHarness");
            _spawned.Add(shopGo);
            ShopPresenter shop = shopGo.AddComponent<ShopPresenter>();
            shop.Initialize(profile, onBackToHome: null);
            TrackCanvas("ShopCanvas");

            Assert.IsTrue(shop.PurchaseForTests(CollectionPackCatalog.SingleSigilSkuId));

            Assert.AreEqual(1, profile.cardProgression.Count);
            string grantedId = profile.cardProgression[0].cardId;
            Assert.GreaterOrEqual(profile.cardProgression[0].copyCount, 1);

            PlayerProfile reloaded = SaveSystem.Load();
            Assert.IsNotNull(CollectionProgression.FindRecordForTests(reloaded, grantedId));

            reloaded.ApplyDataToEmpire();
            int deckSize = reloaded.Empire != null && reloaded.Empire.DeckSlotCount > 0
                ? reloaded.Empire.DeckSlotCount
                : 10;
            List<string> fillerIds = CardDatabase.Instance.AllCards
                .Select(c => c.Id)
                .Where(id => id != "dragon" && id != grantedId)
                .Take(deckSize - 1)
                .ToList();
            Assert.AreEqual(deckSize - 1, fillerIds.Count);
            foreach (string fillerId in fillerIds)
                CollectionProgression.TryGrantFirstCopy(reloaded, fillerId, id => CardDatabase.Instance.GetCard(id) != null);
            SaveSystem.Save(reloaded);
            SaveSystem.ResetCurrentProfileForTests();

            var deckGo = new GameObject("DeckBuilderHarness");
            _spawned.Add(deckGo);
            DeckBuilderPresenter deck = deckGo.AddComponent<DeckBuilderPresenter>();
            deck.Initialize(onBackToHome: null);
            TrackCanvas("DeckBuilderCanvas");

            List<string> deckIds = fillerIds.Append(grantedId).ToList();
            deck.SetAndConfirmDeckForTests(deckIds);

            CollectionAssert.Contains(SaveSystem.Load().activeDeckCardIds, grantedId,
                "DeckBuilder must accept the shop-granted card from cardProgression as owned.");
        }

        private void TrackCanvas(string canvasName)
        {
            GameObject canvas = GameObject.Find(canvasName);
            if (canvas != null) _spawned.Add(canvas);
        }

        private static PlayerProfile NewMigratedProfile(int gems)
        {
            var profile = new PlayerProfile { gems = gems };
            CollectionSchemaMigration.Apply(profile);
            return profile;
        }
    }
}

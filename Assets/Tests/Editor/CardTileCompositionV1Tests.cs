using System.IO;
using System.Linq;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Data;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Card tile composition V1 — 400×600 frame + asset-driven portrait catalog; hierarchy and live
    /// text wired in Deck Builder and Collection, with three approved legacy-art exceptions.
    /// </summary>
    public class CardTileCompositionV1Tests
    {
        private string _scratchSaveDir;
        private GameObject _databaseGo;
        private GameObject _deckGo;
        private GameObject _collectionGo;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsCardTileV1_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();

            _databaseGo = new GameObject("CardDatabase_CardTileV1");
            _databaseGo.AddComponent<CardDatabase>().Initialize();

            var profile = new PlayerProfile();
            CollectionSchemaMigration.Apply(profile);
            foreach (string cardId in CardTileCompositionV1.CompositionCardIds)
            {
                profile.cardProgression.Add(new CardProgressionRecord
                {
                    cardId = cardId,
                    copyCount = 1,
                    cardLevel = 1,
                    evolutionStep = 0,
                    trainingXp = 0,
                });
            }

            Assert.IsTrue(SaveSystem.Save(profile));
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            if (_deckGo != null) Object.DestroyImmediate(_deckGo);
            if (_collectionGo != null) Object.DestroyImmediate(_collectionGo);
            if (_databaseGo != null) Object.DestroyImmediate(_databaseGo);
            Object.DestroyImmediate(GameObject.Find("DeckBuilderCanvas"));
            Object.DestroyImmediate(GameObject.Find("CollectionCanvas"));
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (Directory.Exists(_scratchSaveDir))
            {
                try { Directory.Delete(_scratchSaveDir, true); }
                catch (IOException) { }
            }
        }

        [Test]
        public void CardTileV1Pack_IsPresentInResources()
        {
            Assert.IsTrue(CardTileCompositionV1.HasPack,
                "Resources/UI/CardTiles/V1 must include the frame and authored portrait catalog.");
            Assert.AreEqual("card_tile_frame_v1", CardTileCompositionV1.LoadFrame().name);
            Assert.AreEqual(82, CardTileCompositionV1.CompositionCardIds.Count,
                "85 catalog cards minus three approved legacy exceptions must use V1 composition.");
            foreach (string cardId in CardTileCompositionV1.CompositionCardIds)
                Assert.NotNull(CardTileCompositionV1.LoadPortraitTile(cardId), cardId);
        }

        [Test]
        public void DeckBuilder_PocCard_UsesV1LayerOrderAndLiveStats()
        {
            _deckGo = new GameObject("DeckBuilderCardTileV1Harness");
            var presenter = _deckGo.AddComponent<DeckBuilderPresenter>();
            presenter.Initialize(null);

            Transform warriorTile = GameObject.Find("DeckBuilderCanvas")
                ?.transform.Find("CollectionPanel/CollectionScroll/Viewport/Content/Card_warrior");
            Assert.NotNull(warriorTile, "Setup: owned warrior tile must exist.");

            AssertV1Hierarchy(warriorTile, "warrior");

            Card card = CardDatabase.Instance.GetCard("warrior");
            Assert.NotNull(card);
            Assert.AreEqual(card.DisplayName, warriorTile.Find("Name").GetComponent<Text>().text);
            Assert.AreEqual(CardTileCompositionV1.FormatClassSchoolLine(card),
                warriorTile.Find("ClassSchool").GetComponent<Text>().text);
            Assert.AreEqual($"{card.ResourceCost}", warriorTile.Find("Cost").GetComponent<Text>().text);
            Assert.AreEqual($"ATK {card.Attack}", warriorTile.Find("AtkStat").GetComponent<Text>().text);
            Assert.AreEqual($"HP {card.Health}", warriorTile.Find("HpStat").GetComponent<Text>().text);
            Assert.NotNull(warriorTile.Find("CardFrame").GetComponent<Image>().sprite);
            Assert.NotNull(warriorTile.Find("CardPortrait").GetComponent<Image>().sprite);
        }

        [Test]
        public void Collection_PocCard_UsesV1LayerOrderAndLiveStats()
        {
            _collectionGo = new GameObject("CollectionCardTileV1Harness");
            var presenter = _collectionGo.AddComponent<CollectionPresenter>();
            presenter.Initialize(null, null);

            Transform cyclopsTile = GameObject.Find("CollectionCanvas")
                ?.transform.Find("GridPanel/CollectionScroll/Viewport/Content/OwnedCard_cyclops");
            Assert.NotNull(cyclopsTile, "Setup: owned cyclops tile must exist.");

            AssertV1Hierarchy(cyclopsTile, "cyclops");

            Card card = CardDatabase.Instance.GetCard("cyclops");
            Assert.NotNull(card);
            Assert.AreEqual(card.DisplayName, cyclopsTile.Find("Name").GetComponent<Text>().text);
            Assert.AreEqual($"ATK {card.Attack}", cyclopsTile.Find("AtkStat").GetComponent<Text>().text);
        }

        [Test]
        public void BlockedIdentityCards_StillUseLegacyTileHierarchy()
        {
            var profile = SaveManager.SaveData ?? new PlayerProfile();
            CollectionSchemaMigration.Apply(profile);
            profile.cardProgression.Add(new CardProgressionRecord
            {
                cardId = "dragon_tamer",
                copyCount = 1,
                cardLevel = 1,
            });
            Assert.IsTrue(SaveSystem.Save(profile));
            SaveSystem.ResetCurrentProfileForTests();

            _collectionGo = new GameObject("CollectionLegacyTileHarness");
            var presenter = _collectionGo.AddComponent<CollectionPresenter>();
            presenter.Initialize(null, null);

            Transform legacyTile = GameObject.Find("CollectionCanvas")
                ?.transform.Find("GridPanel/CollectionScroll/Viewport/Content/OwnedCard_dragon_tamer");
            Assert.NotNull(legacyTile);
            Assert.NotNull(legacyTile.Find("OpaqueCardBase"), "Blocked identities keep legacy frame-below-art tile.");
            Assert.IsNull(legacyTile.Find("CardFrame"));
        }

        [Test]
        public void FullCatalog_SplitsExactlyEightyTwoCompositionAndThreeLegacyCards()
        {
            string[] expectedLegacy = { "dragon_tamer", "ancient_dragon", "forest_fairy" };
            Assert.AreEqual(85, CardDatabase.Instance.AllCards.Count);

            string[] actualLegacy = CardDatabase.Instance.AllCards
                .Where(card => !CardTileCompositionV1.ShouldUseComposition(card.Id))
                .Select(card => card.Id)
                .OrderBy(id => id)
                .ToArray();

            CollectionAssert.AreEquivalent(expectedLegacy, actualLegacy);
            Assert.AreEqual(82, CardDatabase.Instance.AllCards.Count(card =>
                CardTileCompositionV1.ShouldUseComposition(card.Id)));
        }

        private static void AssertV1Hierarchy(Transform tile, string cardId)
        {
            string[] expected = CardTileCompositionV1.ExpectedLayerNames;
            for (int i = 0; i < expected.Length; i++)
            {
                Transform child = tile.GetChild(i);
                Assert.AreEqual(expected[i], child.name,
                    $"Card {cardId}: child index {i} must be {expected[i]} (portrait → frame → text layers).");
            }

            Assert.AreEqual(
                $"card_tile_art_{cardId}_v1",
                tile.Find("CardPortrait").GetComponent<Image>().sprite.name);
        }
    }
}

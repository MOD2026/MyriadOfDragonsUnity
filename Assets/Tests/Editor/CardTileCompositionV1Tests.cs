using System.IO;
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
    /// Card tile composition V1 — 400×600 frame + four POC portrait tiles; hierarchy and live text
    /// wired in Deck Builder and Collection for warrior/archer_dragon/cyclops/dragonqueen only.
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
            foreach (string cardId in CardTileCompositionV1.PocCardIds)
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
                "Resources/UI/CardTiles/V1 must include frame + four POC portrait tiles.");
            Assert.AreEqual("card_tile_frame_v1", CardTileCompositionV1.LoadFrame().name);
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
        public void NonPocCard_StillUsesLegacyTileHierarchy()
        {
            var profile = SaveManager.SaveData ?? new PlayerProfile();
            CollectionSchemaMigration.Apply(profile);
            profile.cardProgression.Add(new CardProgressionRecord
            {
                cardId = "pandora",
                copyCount = 1,
                cardLevel = 1,
            });
            Assert.IsTrue(SaveSystem.Save(profile));
            SaveSystem.ResetCurrentProfileForTests();

            _collectionGo = new GameObject("CollectionLegacyTileHarness");
            var presenter = _collectionGo.AddComponent<CollectionPresenter>();
            presenter.Initialize(null, null);

            Transform pandoraTile = GameObject.Find("CollectionCanvas")
                ?.transform.Find("GridPanel/CollectionScroll/Viewport/Content/OwnedCard_pandora");
            Assert.NotNull(pandoraTile);
            Assert.NotNull(pandoraTile.Find("OpaqueCardBase"), "Non-POC cards keep legacy frame-below-art tile.");
            Assert.IsNull(pandoraTile.Find("CardFrame"));
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

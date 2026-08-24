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
    /// <summary>Block Y — HomeV3 chrome on Collection + Deck Builder (header frame + nav tiles).</summary>
    public class CollectionDeckHomeV3ChromeTests
    {
        private string _scratchSaveDir;
        private GameObject _databaseGo;
        private GameObject _collectionGo;
        private GameObject _deckGo;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsHomeV3Chrome_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();

            _databaseGo = new GameObject("CardDatabase_HomeV3Chrome");
            _databaseGo.AddComponent<CardDatabase>().Initialize();

            var profile = new PlayerProfile();
            CollectionSchemaMigration.Apply(profile);
            Assert.IsTrue(SaveSystem.Save(profile));
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            if (_collectionGo != null) Object.DestroyImmediate(_collectionGo);
            if (_deckGo != null) Object.DestroyImmediate(_deckGo);
            if (_databaseGo != null) Object.DestroyImmediate(_databaseGo);
            Object.DestroyImmediate(GameObject.Find("CollectionCanvas"));
            Object.DestroyImmediate(GameObject.Find("DeckBuilderCanvas"));
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (Directory.Exists(_scratchSaveDir))
            {
                try { Directory.Delete(_scratchSaveDir, true); }
                catch (IOException) { }
            }
        }

        [Test]
        public void Collection_Initialize_AppliesHomeV3HeaderWhenPackPresent()
        {
            _collectionGo = new GameObject("CollectionHomeV3Harness");
            var presenter = _collectionGo.AddComponent<CollectionPresenter>();
            Assert.DoesNotThrow(() => presenter.Initialize(null, null));

            GameObject canvas = GameObject.Find("CollectionCanvas");
            Assert.NotNull(canvas);
            Transform header = canvas.transform.Find("Header");
            Assert.NotNull(header);
            Image headerImage = header.GetComponent<Image>();
            Assert.NotNull(headerImage);
            // Foundation: identity/header frame needs slice metadata — neutral fill only.
            Assert.IsNull(headerImage.sprite);

            Assert.NotNull(canvas.transform.Find("BackButton"));
            Assert.NotNull(canvas.transform.Find("OpenDeckBuilderButton"));
            Assert.NotNull(canvas.transform.Find("ControlsRow/ClassFilter"));
            Assert.NotNull(canvas.transform.Find("ControlsRow/CollectionSort"));
            Assert.IsTrue(HomeV3UiLibrary.HasHomeV3Pack, "Approved HomeV3 pill pack must be present.");
        }

        [Test]
        public void DeckBuilder_Initialize_AppliesHomeV3HeaderWhenPackPresent()
        {
            _deckGo = new GameObject("DeckBuilderHomeV3Harness");
            var presenter = _deckGo.AddComponent<DeckBuilderPresenter>();
            Assert.DoesNotThrow(() => presenter.Initialize(null));

            GameObject canvas = GameObject.Find("DeckBuilderCanvas");
            Assert.NotNull(canvas);
            Transform header = canvas.transform.Find("HeaderBar");
            Assert.NotNull(header);
            Image headerImage = header.GetComponent<Image>();
            Assert.NotNull(headerImage);
            Assert.IsNull(headerImage.sprite, "Deck header uses neutral fill — no unsliced Home frame.");

            Transform rail = canvas.transform.Find("ActionRail");
            Assert.NotNull(rail);
            Assert.NotNull(rail.Find("Btn_Back_Rail"));
            Assert.NotNull(rail.Find("Btn_Recommended"));
            Assert.NotNull(rail.Find("Btn_Confirm"));
            Assert.IsTrue(HomeV3UiLibrary.HasHomeV3Pack);
        }
    }
}

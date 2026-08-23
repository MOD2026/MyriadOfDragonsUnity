using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Data;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>DeckBuilderPresenter owned-card source: cardProgression (V1) vs legacy cardCollection.</summary>
    public class DeckBuilderCollectionOwnershipTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsDeckBuilderCollection_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
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

        private DeckBuilderPresenter SpawnPresenterWithDatabase(out List<string> realCardIds)
        {
            var databaseGo = new GameObject("DeckBuilderCollection_CardDatabase");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            realCardIds = database.AllCards.Select(c => c.Id).Take(25).ToList();
            Assert.GreaterOrEqual(realCardIds.Count, 10, "Setup: expected at least 10 real cards.");

            var presenterGo = new GameObject("DeckBuilderPresenterUnderTest");
            _spawned.Add(presenterGo);
            DeckBuilderPresenter presenter = presenterGo.AddComponent<DeckBuilderPresenter>();
            presenter.Initialize(onBackToHome: null);

            GameObject deckBuilderCanvas = GameObject.Find("DeckBuilderCanvas");
            if (deckBuilderCanvas != null) _spawned.Add(deckBuilderCanvas);

            return presenter;
        }

        [Test]
        public void DeckBuilder_ReadsOwnedCards_FromCardProgression_WhenCollectionV1()
        {
            DeckBuilderPresenter presenter = SpawnPresenterWithDatabase(out List<string> realCardIds);

            PlayerProfile profile = SaveManager.SaveData;
            profile.collectionSchemaVersion = CollectionSchemaRules.CurrentCollectionSchemaVersion;
            profile.cardProgression.Clear();
            profile.cardCollection.Clear();
            foreach (string cardId in realCardIds)
            {
                profile.cardProgression.Add(new CardProgressionRecord { cardId = cardId, copyCount = 1 });
            }

            profile.ApplyDataToEmpire();
            int deckSizeLimit = profile.Empire.DeckSlotCount;
            List<string> chosenDeck = realCardIds.Take(deckSizeLimit).ToList();

            presenter.TeardownUI();
            presenter.Initialize(onBackToHome: null);

            presenter.SetAndConfirmDeckForTests(chosenDeck);

            PlayerProfile reloaded = SaveSystem.Load();
            CollectionAssert.AreEquivalent(chosenDeck, reloaded.activeDeckCardIds,
                "Collection V1: owned cards from cardProgression must be deck-buildable and persist on confirm.");
        }

        [Test]
        public void DeckBuilder_FallsBackToLegacyCardCollection_WhenNotCollectionV1()
        {
            DeckBuilderPresenter presenter = SpawnPresenterWithDatabase(out List<string> realCardIds);

            PlayerProfile profile = SaveManager.SaveData;
            profile.collectionSchemaVersion = 0;
            profile.cardProgression.Clear();
            profile.cardCollection = new List<string>(realCardIds);

            profile.ApplyDataToEmpire();
            int deckSizeLimit = profile.Empire.DeckSlotCount;
            List<string> chosenDeck = realCardIds.Take(deckSizeLimit).ToList();

            presenter.TeardownUI();
            presenter.Initialize(onBackToHome: null);

            presenter.SetAndConfirmDeckForTests(chosenDeck);

            PlayerProfile reloaded = SaveSystem.Load();
            CollectionAssert.AreEquivalent(chosenDeck, reloaded.activeDeckCardIds,
                "Legacy profile: cardCollection must remain the owned-card source before V1 migration.");
        }

        [Test]
        public void DeckBuilder_IgnoresLegacyCardCollection_WhenCollectionV1()
        {
            DeckBuilderPresenter presenter = SpawnPresenterWithDatabase(out List<string> realCardIds);

            PlayerProfile profile = SaveManager.SaveData;
            profile.collectionSchemaVersion = CollectionSchemaRules.CurrentCollectionSchemaVersion;
            profile.cardProgression.Clear();
            profile.cardCollection.Clear();

            List<string> progressionIds = realCardIds.Take(12).ToList();
            foreach (string cardId in progressionIds)
            {
                profile.cardProgression.Add(new CardProgressionRecord { cardId = cardId, copyCount = 1 });
            }

            List<string> legacyOnlyIds = realCardIds.Skip(12).Take(8).ToList();
            profile.cardCollection = new List<string>(legacyOnlyIds);

            profile.ApplyDataToEmpire();
            int deckSizeLimit = profile.Empire.DeckSlotCount;
            List<string> chosenDeck = progressionIds.Take(deckSizeLimit).ToList();

            presenter.TeardownUI();
            presenter.Initialize(onBackToHome: null);

            presenter.SetAndConfirmDeckForTests(progressionIds.Concat(legacyOnlyIds));

            PlayerProfile reloaded = SaveSystem.Load();
            CollectionAssert.AreEquivalent(chosenDeck, reloaded.activeDeckCardIds,
                "Collection V1: legacy-only ids must not be treated as owned when absent from cardProgression.");
        }
    }
}

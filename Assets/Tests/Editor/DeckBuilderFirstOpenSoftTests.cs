using System.Collections.Generic;
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
    /// Block N — Deck Builder first-open Soft: incomplete / unconfirmed deck shows Campaign /
    /// To Battle guidance without auto-confirming.
    /// </summary>
    public class DeckBuilderFirstOpenSoftTests
    {
        private static readonly string[] ApprovedStarterCollectionCardIds =
        {
            "warrior", "novice_knight", "goblin_caster",
            "cleric", "archer_elf", "fox", "bunny", "forest", "tribal_warrior", "undead_soldier",
        };

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsDeckSoft_" + System.Guid.NewGuid().ToString("N"));
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

            var canvas = GameObject.Find("DeckBuilderCanvas");
            if (canvas != null) Object.DestroyImmediate(canvas);

            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, recursive: true);
        }

        [Test]
        public void FirstOpen_WithOwnedCardsAndEmptyConfirmedDeck_ShowsCampaignToBattleGuidance()
        {
            var databaseGo = new GameObject("DeckSoft_CardDatabase");
            _spawned.Add(databaseGo);
            databaseGo.AddComponent<CardDatabase>().Initialize();

            var profile = new PlayerProfile
            {
                cardCollection = new List<string>(ApprovedStarterCollectionCardIds),
                activeDeckCardIds = new List<string>(), // no confirmed deck
            };
            Assert.IsTrue(SaveSystem.Save(profile));
            SaveSystem.ResetCurrentProfileForTests();

            var presenterGo = new GameObject("DeckBuilderPresenter_Soft");
            _spawned.Add(presenterGo);
            DeckBuilderPresenter presenter = presenterGo.AddComponent<DeckBuilderPresenter>();
            presenter.Initialize(onBackToHome: null);

            GameObject canvas = GameObject.Find("DeckBuilderCanvas");
            Assert.NotNull(canvas);
            _spawned.Add(canvas);

            Text status = presenter.DeckStatusTextForTests;
            Assert.NotNull(status);
            StringAssert.Contains(DeckBuilderPresenter.FormatConfirmedDeckRequiredGuidance(MyriadOfDragons.Empire.PlayerEmpireData.DeckSlotsForBarracksLevel(1)), status.text,
                "First-open Soft must tell the player a confirmed deck is required before Campaign / To Battle.");
            StringAssert.DoesNotContain("Deck confirmed", status.text);

            Assert.IsFalse(SaveManager.SaveData.activeDeckCardIds != null
                           && SaveManager.SaveData.activeDeckCardIds.Count == MyriadOfDragons.Empire.PlayerEmpireData.DeckSlotsForBarracksLevel(1),
                "First-open Soft must not auto-confirm a deck.");
        }

        [Test]
        public void AfterConfirm_StatusNoLongerImpliesMustConfirm()
        {
            var databaseGo = new GameObject("DeckSoft_CardDatabase_Confirm");
            _spawned.Add(databaseGo);
            databaseGo.AddComponent<CardDatabase>().Initialize();

            var profile = new PlayerProfile
            {
                cardCollection = new List<string>(ApprovedStarterCollectionCardIds),
                activeDeckCardIds = new List<string>(),
            };
            Assert.IsTrue(SaveSystem.Save(profile));
            SaveSystem.ResetCurrentProfileForTests();

            var presenterGo = new GameObject("DeckBuilderPresenter_SoftConfirm");
            _spawned.Add(presenterGo);
            DeckBuilderPresenter presenter = presenterGo.AddComponent<DeckBuilderPresenter>();
            presenter.Initialize(onBackToHome: null);

            GameObject canvas = GameObject.Find("DeckBuilderCanvas");
            Assert.NotNull(canvas);
            _spawned.Add(canvas);

            presenter.SetAndConfirmDeckForTests(ApprovedStarterCollectionCardIds);

            Text status = presenter.DeckStatusTextForTests;
            Assert.NotNull(status);
            StringAssert.Contains("Deck confirmed", status.text);
            StringAssert.DoesNotContain(DeckBuilderPresenter.FormatConfirmedDeckRequiredGuidance(MyriadOfDragons.Empire.PlayerEmpireData.DeckSlotsForBarracksLevel(1)), status.text,
                "After confirm, Soft must not still say the player must confirm a deck.");
            CollectionAssert.AreEqual(ApprovedStarterCollectionCardIds.Take(MyriadOfDragons.Empire.PlayerEmpireData.DeckSlotsForBarracksLevel(1)).ToList(), SaveManager.SaveData.activeDeckCardIds);
        }
    }
}

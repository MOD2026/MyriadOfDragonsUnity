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
    public class DeckBuilderReleaseGateTests
    {
        private GameObject _spawned;
        private string _scratchSaveDir;

        [SetUp]
        public void Setup()
        {
            // Real bug found 2026-08-26 (CR, diagnosed while verifying an unrelated change):
            // this fixture never isolated save state or granted any owned cards, so
            // DeckBuilderPresenter.LoadOwnedCollectionCards() correctly read an empty
            // profile.cardCollection - zero card roots and a correctly-non-interactable
            // Recommended Deck button were the presenter working exactly as designed against
            // the (empty) state this fixture actually provided, not a presenter bug. Mirrors
            // the already-working DeckBuilderCollectionOwnershipTests.SpawnPresenterWithDatabase
            // pattern: real SaveSystem isolation, a real CardDatabase, real owned card ids.
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsDeckBuilderReleaseGate_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();

            var databaseGo = new GameObject("DeckBuilderReleaseGate_CardDatabase");
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // Initialize() may destroy this local instance if a duplicate was already live - always resolve to the survivor.
            List<string> realCardIds = database.AllCards.Select(c => c.Id).Take(25).ToList();
            Assert.GreaterOrEqual(realCardIds.Count, 10, "Setup: expected at least 10 real cards.");

            PlayerProfile profile = SaveManager.SaveData;
            profile.cardCollection = new List<string>(realCardIds);

            // A confirmable deck needs a FULL active deck too, not just ownership - CanConfirmDeck()
            // requires activeDeck.Count == deckSizeLimit. Mirrors DeckBuilderCollectionOwnershipTests'
            // own resolution of the real slot count via profile.Empire.DeckSlotCount.
            profile.ApplyDataToEmpire();
            int deckSizeLimit = profile.Empire.DeckSlotCount;
            Assert.LessOrEqual(deckSizeLimit, realCardIds.Count,
                "Setup: need at least as many real cards as deck slots.");
            profile.activeDeckCardIds = new List<string>(realCardIds.Take(deckSizeLimit));

            _spawned = new GameObject("DeckBuilderReleaseGateHarness");
        }

        [TearDown]
        public void TearDown()
        {
            if (_spawned != null)
            {
                Object.DestroyImmediate(_spawned);
            }

            GameObject database = GameObject.Find("DeckBuilderReleaseGate_CardDatabase");
            if (database != null)
            {
                Object.DestroyImmediate(database);
            }

            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
            {
                Directory.Delete(_scratchSaveDir, recursive: true);
            }
        }

        [Test]
        public void DeckBuilder_ScreenRootGeometry_IsInsideCanvas_AndOrderedCorrectly()
        {
            var presenter = _spawned.AddComponent<DeckBuilderPresenter>();
            presenter.Initialize(() => { });

            GameObject canvas = _spawned.transform.Find("DeckBuilderCanvas")?.gameObject;
            Assert.NotNull(canvas, "Deck gate: expected a DeckBuilder canvas to be created.");

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            UIReleaseGateTestUtility.RequireInsideCanvas("Deck canvas", canvasRect);

            RectTransform topBar = canvas.transform.Find("HeaderBar")?.GetComponent<RectTransform>();
            RectTransform collectionPanel = canvas.transform.Find("CollectionPanel")?.GetComponent<RectTransform>();
            RectTransform deckPanel = canvas.transform.Find("DeckPanel")?.GetComponent<RectTransform>();
            RectTransform actionRail = canvas.transform.Find("ActionRail")?.GetComponent<RectTransform>();

            Assert.NotNull(topBar, "Deck gate: missing HeaderBar.");
            Assert.NotNull(collectionPanel, "Deck gate: missing CollectionPanel.");
            Assert.NotNull(deckPanel, "Deck gate: missing DeckPanel.");
            Assert.NotNull(actionRail, "Deck gate: missing ActionRail.");

            UIReleaseGateTestUtility.RequireInsideCanvas("HeaderBar", topBar);
            UIReleaseGateTestUtility.RequireInsideCanvas("CollectionPanel", collectionPanel);
            UIReleaseGateTestUtility.RequireInsideCanvas("DeckPanel", deckPanel);
            UIReleaseGateTestUtility.RequireInsideCanvas("ActionRail", actionRail);

            UIReleaseGateTestUtility.RequireNonOverlapping("Deck sections", new[]
            {
                topBar,
                collectionPanel,
                deckPanel,
                actionRail,
            });

            UIReleaseGateTestUtility.RequireVerticalOrder("Deck layout order", topBar, collectionPanel, actionRail);
        }

        [Test]
        public void DeckBuilder_NestedCoordinatesAndInput_AreSafeForClicks()
        {
            var presenter = _spawned.AddComponent<DeckBuilderPresenter>();
            presenter.Initialize(() => { });

            GameObject canvas = _spawned.transform.Find("DeckBuilderCanvas")?.gameObject;
            Assert.NotNull(canvas, "Deck gate: expected a DeckBuilder canvas to be created.");

            UIReleaseGateTestUtility.RequireNestedPlacementSafety(canvas, "CollectionPanel", "DeckPanel", "ActionRail");

            foreach (string requiredName in new[] { "Btn_Back_Rail", "Btn_Recommended", "Btn_Confirm" })
            {
                GameObject actionRoot = canvas.transform.Find($"ActionRail/{requiredName}")?.gameObject;
                UIReleaseGateTestUtility.AssertActionRoot(actionRoot, requiredName);
                UIReleaseGateTestUtility.AssertDecorativeChildrenAreNonRaycastable(actionRoot, requiredName);
            }

            UIReleaseGateTestUtility.AssertNoBlockingGraphicOverAction(canvas, new[] { "Btn_Back_Rail", "Btn_Recommended", "Btn_Confirm" });
        }

        [Test]
        public void DeckBuilder_Cards_HavePositiveArt_AndNoOpaqueFrameAboveArt()
        {
            var presenter = _spawned.AddComponent<DeckBuilderPresenter>();
            presenter.Initialize(() => { });

            GameObject canvas = _spawned.transform.Find("DeckBuilderCanvas")?.gameObject;
            Assert.NotNull(canvas, "Deck gate: expected a DeckBuilder canvas to be created.");

            var cardRoots = canvas.GetComponentsInChildren<RectTransform>(true)
                .Where(rt => rt.name.StartsWith("Card_") || rt.name.StartsWith("DeckRow_"))
                .ToList();

            Assert.Greater(cardRoots.Count, 0, "Deck gate: no deck or collection card roots were created. The card presentation gate needs at least one presentable card object.");

            foreach (RectTransform cardRootTransform in cardRoots)
            {
                GameObject cardRoot = cardRootTransform.gameObject;
                UIReleaseGateTestUtility.AssertCardArtPresentation(cardRoot);
            }
        }
    }
}

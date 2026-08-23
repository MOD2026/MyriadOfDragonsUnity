using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    public class DeckBuilderReleaseGateTests
    {
        private GameObject _spawned;

        [SetUp]
        public void Setup()
        {
            _spawned = new GameObject("DeckBuilderReleaseGateHarness");
        }

        [TearDown]
        public void TearDown()
        {
            if (_spawned != null)
            {
                Object.DestroyImmediate(_spawned);
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

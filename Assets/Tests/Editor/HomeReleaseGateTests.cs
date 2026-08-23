using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    public class HomeReleaseGateTests
    {
        private GameObject _spawned;

        [SetUp]
        public void Setup()
        {
            _spawned = new GameObject("HomeReleaseGateHarness");
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
        public void Home_ScreenRootGeometry_IsInsideCanvas_AndOrderedCorrectly()
        {
            var presenter = _spawned.AddComponent<HomePagePresenter>();
            presenter.BuildHomePageUIForTests();

            GameObject canvas = presenter.HomeCanvasObjectForTests;
            Assert.NotNull(canvas, "Home gate: expected a Home canvas to be created.");

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            UIReleaseGateTestUtility.RequireInsideCanvas("Home canvas", canvasRect);

            RectTransform identityRoot = canvas.transform.Find("IdentityRoot")?.GetComponent<RectTransform>();
            RectTransform resourceRow = canvas.transform.Find("ResourceRow")?.GetComponent<RectTransform>();
            RectTransform featureRoot = canvas.transform.Find("HomeFeatureRoot")?.GetComponent<RectTransform>();
            RectTransform navStage = canvas.transform.Find("NavigationStage")?.GetComponent<RectTransform>();

            Assert.NotNull(identityRoot, "Home gate: missing IdentityRoot.");
            Assert.NotNull(resourceRow, "Home gate: missing ResourceRow.");
            Assert.NotNull(featureRoot, "Home gate: missing HomeFeatureRoot.");
            Assert.NotNull(navStage, "Home gate: missing NavigationStage.");
            Assert.IsNull(canvas.transform.Find("EmpireConstructionRoot"),
                "Empire construction belongs on Empire screen, not Home.");

            UIReleaseGateTestUtility.RequireInsideCanvas("IdentityRoot", identityRoot);
            UIReleaseGateTestUtility.RequireInsideCanvas("ResourceRow", resourceRow);
            UIReleaseGateTestUtility.RequireInsideCanvas("HomeFeatureRoot", featureRoot);
            UIReleaseGateTestUtility.RequireInsideCanvas("NavigationStage", navStage);

            UIReleaseGateTestUtility.RequireVerticalOrder(
                "Home layout order (header → feature → nav)",
                resourceRow,
                featureRoot,
                navStage);
        }

        [Test]
        public void Home_NestedCoordinatesAndInput_AreSafeForClicks()
        {
            var presenter = _spawned.AddComponent<HomePagePresenter>();
            presenter.BuildHomePageUIForTests();

            GameObject canvas = presenter.HomeCanvasObjectForTests;
            Assert.NotNull(canvas, "Home gate: expected a Home canvas to be created.");

            UIReleaseGateTestUtility.RequireNestedPlacementSafety(canvas, "NavigationStage");

            foreach (string requiredName in new[] { "Btn_Campaign", "Btn_Empire", "Btn_Cards", "Btn_Shop", "Btn_To Battle" })
            {
                GameObject actionRoot = canvas.transform.Find($"NavigationStage/{requiredName}")?.gameObject;
                UIReleaseGateTestUtility.AssertActionRoot(actionRoot, requiredName);
                UIReleaseGateTestUtility.AssertDecorativeChildrenAreNonRaycastable(actionRoot, requiredName);
            }

            UIReleaseGateTestUtility.AssertNoBlockingGraphicOverAction(canvas, new[] { "Btn_Campaign", "Btn_Empire", "Btn_Cards", "Btn_Shop", "Btn_To Battle" });
        }
    }
}

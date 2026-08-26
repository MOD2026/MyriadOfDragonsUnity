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

            // Home IA rebuild: TutorialStrip/HomeFeatureRoot and ContentPanel/NavigationStage are
            // gone, replaced by the scrollable HomeFeed and the persistent DestinationBar, both
            // parented under ContentPanel (register: "LOCKED: Home IA rebuild").
            RectTransform identityRoot = canvas.transform.Find("TopHud/IdentityRoot")?.GetComponent<RectTransform>();
            RectTransform resourceRow = canvas.transform.Find("TopHud/ResourceRow")?.GetComponent<RectTransform>();
            RectTransform homeFeed = canvas.transform.Find("ContentPanel/HomeFeed")?.GetComponent<RectTransform>();
            RectTransform destinationBar = canvas.transform.Find("ContentPanel/DestinationBar")?.GetComponent<RectTransform>();

            Assert.NotNull(identityRoot, "Home gate: missing IdentityRoot.");
            Assert.NotNull(resourceRow, "Home gate: missing ResourceRow.");
            Assert.NotNull(homeFeed, "Home gate: missing HomeFeed.");
            Assert.NotNull(destinationBar, "Home gate: missing DestinationBar.");
            Assert.IsNull(canvas.transform.Find("EmpireConstructionRoot"),
                "Empire construction belongs on Empire screen, not Home.");
            Assert.IsNotNull(canvas.transform.Find("TopHud"), "Home must expose TopHud semantic region.");
            Assert.IsNotNull(canvas.transform.Find("TutorialStrip"), "Home must expose TutorialStrip semantic region.");
            Assert.IsNotNull(canvas.transform.Find("ActionRail"), "Home must expose ActionRail semantic region.");
            Assert.IsNotNull(canvas.transform.Find("ContentPanel"), "Home must expose ContentPanel semantic region.");
            Assert.IsNotNull(canvas.transform.Find("Footer"), "Home must expose Footer semantic region.");

            UIReleaseGateTestUtility.RequireInsideCanvas("IdentityRoot", identityRoot);
            UIReleaseGateTestUtility.RequireInsideCanvas("ResourceRow", resourceRow);
            UIReleaseGateTestUtility.RequireInsideCanvas("HomeFeed", homeFeed);
            UIReleaseGateTestUtility.RequireInsideCanvas("DestinationBar", destinationBar);

            UIReleaseGateTestUtility.RequireVerticalOrder(
                "Home layout order (header → feed → destination bar)",
                resourceRow,
                homeFeed,
                destinationBar);
        }

        [Test]
        public void Home_NestedCoordinatesAndInput_AreSafeForClicks()
        {
            var presenter = _spawned.AddComponent<HomePagePresenter>();
            presenter.BuildHomePageUIForTests();

            GameObject canvas = presenter.HomeCanvasObjectForTests;
            Assert.NotNull(canvas, "Home gate: expected a Home canvas to be created.");

            UIReleaseGateTestUtility.RequireNestedPlacementSafety(canvas, "ContentPanel/DestinationBar");

            foreach (string requiredName in new[] { "Dest_HOME", "Dest_BATTLE", "Dest_QUESTS", "Dest_COLLECTION", "Dest_EMPIRE" })
            {
                GameObject actionRoot = canvas.transform.Find($"ContentPanel/DestinationBar/{requiredName}")?.gameObject;
                UIReleaseGateTestUtility.AssertActionRoot(actionRoot, requiredName);
                UIReleaseGateTestUtility.AssertDecorativeChildrenAreNonRaycastable(actionRoot, requiredName);
            }

            UIReleaseGateTestUtility.AssertNoBlockingGraphicOverAction(canvas, new[] { "Dest_HOME", "Dest_BATTLE", "Dest_QUESTS", "Dest_COLLECTION", "Dest_EMPIRE" });
        }
    }
}

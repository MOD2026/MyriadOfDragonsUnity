using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Home layout regression suite (LOCKED 2026-08-25): build at canonical 16:9, reject
    /// unintended actionable-root overlap, verify Button targetGraphic/raycast contracts,
    /// keep decorative graphics from blocking input, and assert known dead-space / Y bands.
    /// </summary>
    public class HomeLayoutRegressionTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsHomeLayout_" + System.Guid.NewGuid().ToString("N"));
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
                Directory.Delete(_scratchSaveDir, recursive: true);
        }

        private HomePagePresenter BuildHome()
        {
            var go = new GameObject("HomeLayoutRegressionHarness");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            home.BuildHomePageUIForTests();
            GameObject canvas = home.HomeCanvasObjectForTests;
            Assert.IsNotNull(canvas);
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1920f, 1080f);
            Canvas.ForceUpdateCanvases();
            return home;
        }

        [Test]
        public void ApplyNeutralActionButton_PreservesAlreadyAssignedSprite()
        {
            var go = new GameObject("NeutralActionPreserveSprite", typeof(RectTransform), typeof(Image), typeof(Button));
            _spawned.Add(go);
            Image img = go.GetComponent<Image>();
            Button btn = go.GetComponent<Button>();
            Sprite gear = Resources.Load<Sprite>("UI/Icons/icon_settings_gear");
            Assert.IsNotNull(gear, "Setup: settings gear sprite must exist under Resources/UI/Icons.");
            img.sprite = gear;

            HomeV3UiLibrary.ApplyNeutralActionButton(btn, img, Color.white);

            Assert.AreSame(gear, img.sprite,
                "ApplyNeutralActionButton must not null a sprite that was already assigned.");
            Assert.AreSame(img, btn.targetGraphic);
        }

        [Test]
        public void Home_Canonical16x9_ActionableRootsDoNotOverlap_AndInputContractsHold()
        {
            HomePagePresenter home = BuildHome();
            Transform canvas = home.HomeCanvasObjectForTests.transform;

            foreach (string region in HomeSemanticRegions.All)
                Assert.IsNotNull(canvas.Find(region), $"Missing semantic region '{region}'.");

            RectTransform socialBazaar = (RectTransform)canvas.Find("TopHud/Btn_Bazaar");
            RectTransform tutorial = (RectTransform)canvas.Find("TutorialStrip/HomeFeatureRoot");
            Assert.IsNotNull(socialBazaar);
            Assert.IsNotNull(tutorial);
            Assert.IsNotNull(tutorial.Find("AlertIcon"),
                "HomeFeatureRoot must reserve an AlertIcon well for the tutorial banner composition.");
            Assert.IsNotNull(tutorial.Find("FeatureCopy"));

            // Pixel contract: social chips Y 100-168, tutorial Y 176-242 (top-left space).
            Assert.IsFalse(UIReleaseGateTestUtility.GetScreenRect(socialBazaar)
                    .Overlaps(UIReleaseGateTestUtility.GetScreenRect(tutorial)),
                "Social-chip row must not overlap the tutorial banner.");

            var actionable = new List<RectTransform>
            {
                (RectTransform)canvas.Find("TopHud/IdentityRoot"),
                (RectTransform)canvas.Find("TopHud/Btn_Settings"),
                (RectTransform)canvas.Find("TopHud/Btn_SpellLoadout"),
                (RectTransform)canvas.Find("TopHud/Btn_BattlePass"),
                (RectTransform)canvas.Find("TopHud/Btn_DailyLogin"),
                (RectTransform)canvas.Find("TopHud/Btn_Bazaar"),
                (RectTransform)canvas.Find("TopHud/Btn_Chat"),
                (RectTransform)canvas.Find("TopHud/Btn_Mail"),
                (RectTransform)canvas.Find("TopHud/Btn_Friends"),
                (RectTransform)canvas.Find("TopHud/Btn_MemoryExpedition"),
                (RectTransform)canvas.Find("TopHud/Btn_Vip"),
                (RectTransform)canvas.Find("TopHud/WeeklyPermitStrip/ClaimWeeklyPermitsButton"),
                (RectTransform)canvas.Find("TutorialStrip/HomeFeatureRoot/StartTutorialButtonRoot"),
                (RectTransform)canvas.Find("ContentPanel/NavigationStage/Btn_Campaign"),
                (RectTransform)canvas.Find("ContentPanel/NavigationStage/Btn_Empire"),
                (RectTransform)canvas.Find("ContentPanel/NavigationStage/Btn_Cards"),
                (RectTransform)canvas.Find("ContentPanel/NavigationStage/Btn_Shop"),
                (RectTransform)canvas.Find("ContentPanel/NavigationStage/Btn_Avatar"),
                (RectTransform)canvas.Find("ContentPanel/NavigationStage/Btn_To Battle"),
            };

            foreach (RectTransform rt in actionable)
                Assert.IsNotNull(rt, "Expected an actionable Home control to exist.");

            // Abutting edges (Identity bottom Y=100 / social top Y=100) are allowed; reject true interior overlap.
            for (int i = 0; i < actionable.Count; i++)
            {
                for (int j = i + 1; j < actionable.Count; j++)
                {
                    Rect a = UIReleaseGateTestUtility.GetScreenRect(actionable[i]);
                    Rect b = UIReleaseGateTestUtility.GetScreenRect(actionable[j]);
                    float overlapW = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
                    float overlapH = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
                    float area = Mathf.Max(0f, overlapW) * Mathf.Max(0f, overlapH);
                    Assert.Less(area, 1f,
                        $"Home actionable roots: interior overlap between '{actionable[i].name}' and '{actionable[j].name}' " +
                        $"(area={area:F1}) -> {UIReleaseGateTestUtility.BoundsText(actionable[i])} vs {UIReleaseGateTestUtility.BoundsText(actionable[j])}.");
                }
            }

            string[] actionRoots =
            {
                "TopHud/IdentityRoot",
                "TopHud/Btn_Settings",
                "TopHud/Btn_Bazaar",
                "TutorialStrip/HomeFeatureRoot/StartTutorialButtonRoot",
                "ContentPanel/NavigationStage/Btn_Campaign",
                "ContentPanel/NavigationStage/Btn_To Battle",
            };
            foreach (string path in actionRoots)
            {
                GameObject root = canvas.Find(path)?.gameObject;
                UIReleaseGateTestUtility.AssertActionRoot(root, path);
            }

            // Decorative: backdrop + tutorial strip background must not steal clicks.
            Image backdrop = canvas.Find("Background")?.GetComponent<Image>();
            Assert.IsNotNull(backdrop);
            Assert.IsFalse(backdrop.raycastTarget, "Home backdrop must be non-raycastable.");

            Image tutorialBg = canvas.Find("TutorialStrip/HomeFeatureRoot")?.GetComponent<Image>();
            Assert.IsNotNull(tutorialBg);
            Assert.IsFalse(tutorialBg.raycastTarget, "Tutorial banner fill must be decorative (non-raycastable).");

            // Known dead-space band between social chips (bottom 168) and tutorial (top 176):
            // a 1920×1080 point at mid-gap should not sit inside either actionable rect.
            Rect gapProbe = new Rect(400f, 1080f - 172f, 2f, 2f); // y from bottom for Unity Rect
            foreach (RectTransform rt in actionable)
            {
                Rect r = UIReleaseGateTestUtility.GetScreenRect(rt);
                Assert.IsFalse(r.Overlaps(gapProbe),
                    $"Dead-space gap Y~172 must stay clear of actionable '{rt.name}' ({UIReleaseGateTestUtility.BoundsText(rt)}).");
            }

            Image settingsImg = canvas.Find("TopHud/Btn_Settings")?.GetComponent<Image>();
            Assert.IsNotNull(settingsImg);
            Assert.IsNotNull(settingsImg.sprite,
                "Settings gear Image must keep its sprite after ApplyNeutralActionButton.");
            Assert.AreEqual("icon_settings_gear", settingsImg.sprite.name);
        }
    }
}

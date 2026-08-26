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

            // Home IA rebuild (register: "LOCKED: Home IA rebuild - five-destination shell +
            // rotating feed, replaces the static 22-target grid"): the old TopHud social-chip row
            // and TutorialStrip/HomeFeatureRoot banner are gone. The tutorial invite is now the
            // WELCOME feed card (new-player only, via BuildFeed's isNewPlayer branch) and social
            // (Chat/Mail/Friends) collapsed into one Btn_SocialDrawer entry point.
            const string feedContent = "ContentPanel/HomeFeed/Viewport/Content";

            var actionable = new List<RectTransform>
            {
                (RectTransform)canvas.Find("TopHud/IdentityRoot"),
                (RectTransform)canvas.Find("TopHud/Btn_Settings"),
                (RectTransform)canvas.Find("TopHud/Btn_SocialDrawer"),
                (RectTransform)canvas.Find($"{feedContent}/FeedCard_WELCOME/PrimaryAction"),
                (RectTransform)canvas.Find($"{feedContent}/FeedCard_CAMPAIGN/PrimaryAction"),
                (RectTransform)canvas.Find($"{feedContent}/FeedCard_QUESTS & EVENTS/PrimaryAction"),
                (RectTransform)canvas.Find($"{feedContent}/FeedCard_EMPIRE/PrimaryAction"),
                (RectTransform)canvas.Find($"{feedContent}/FeedCard_WEEKLY PERMIT/ClaimWeeklyPermitsButton"),
                (RectTransform)canvas.Find($"{feedContent}/FeedCard_WEEKLY PERMIT/Btn_PermitWeekKey"),
                (RectTransform)canvas.Find("ContentPanel/DestinationBar/Dest_HOME"),
                (RectTransform)canvas.Find("ContentPanel/DestinationBar/Dest_BATTLE"),
                (RectTransform)canvas.Find("ContentPanel/DestinationBar/Dest_QUESTS"),
                (RectTransform)canvas.Find("ContentPanel/DestinationBar/Dest_COLLECTION"),
                (RectTransform)canvas.Find("ContentPanel/DestinationBar/Dest_EMPIRE"),
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
                "TopHud/Btn_SocialDrawer",
                $"{feedContent}/FeedCard_WELCOME/PrimaryAction",
                $"{feedContent}/FeedCard_CAMPAIGN/PrimaryAction",
                "ContentPanel/DestinationBar/Dest_BATTLE",
            };
            foreach (string path in actionRoots)
            {
                GameObject root = canvas.Find(path)?.gameObject;
                UIReleaseGateTestUtility.AssertActionRoot(root, path);
            }

            // Decorative: backdrop and every feed card's own background fill must not steal clicks.
            Image backdrop = canvas.Find("Background")?.GetComponent<Image>();
            Assert.IsNotNull(backdrop);
            Assert.IsFalse(backdrop.raycastTarget, "Home backdrop must be non-raycastable.");

            Image welcomeCardBg = canvas.Find($"{feedContent}/FeedCard_WELCOME")?.GetComponent<Image>();
            Assert.IsNotNull(welcomeCardBg);
            Assert.IsFalse(welcomeCardBg.raycastTarget, "Feed card fill must be decorative (non-raycastable).");

            Image settingsImg = canvas.Find("TopHud/Btn_Settings")?.GetComponent<Image>();
            Assert.IsNotNull(settingsImg);
            Assert.IsNotNull(settingsImg.sprite,
                "Settings gear Image must keep its sprite after ApplyNeutralActionButton.");
            Assert.AreEqual("icon_settings_gear", settingsImg.sprite.name);
        }
    }
}

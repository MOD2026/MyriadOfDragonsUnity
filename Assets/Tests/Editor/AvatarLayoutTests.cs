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
    /// Avatar screen layout/geometry coverage - one of 16 screens with zero prior layout
    /// regression coverage (WH separately covering the 5 highest-traffic screens: Home, Shop,
    /// Collection, DeckBuilder, CampaignMap). Same method as EmpireBuildingDetailLayoutTests/
    /// TacticalPuzzleLayoutTests: measure the BUILT hierarchy's real world rects via
    /// RectTransform.GetWorldCorners, not re-derived from the same anchor constants the presenter
    /// itself uses, and only flag art that draws AFTER a button it geometrically overlaps (Unity's
    /// real depth-first paint/raycast order via GetComponentsInChildren) - a full-screen backdrop
    /// UNDER everything is not a bug.
    /// </summary>
    public class AvatarLayoutTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDAvatarLayout_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
            foreach (GameObject go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir)) Directory.Delete(_scratchSaveDir, true);
        }

        private AvatarPresenter Open()
        {
            var go = new GameObject("AvatarLayoutHost");
            _spawned.Add(go);
            var presenter = go.AddComponent<AvatarPresenter>();
            presenter.Initialize(onBackToHome: null);
            // Measure at the canonical landscape size, same as HomeLayoutRegressionTests.BuildHome().
            // Without this the canvas takes the EditMode Screen rect (640x360 here), where the
            // header's fixed 100px and the body panel's normalized 0.87 top no longer describe the
            // same layout the game ships - a fixed-pixel header control gets measured against a
            // panel that has shrunk to a third of its real clearance.
            RectTransform canvasRect = presenter.CanvasObjectForTests.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1920f, 1080f);
            Canvas.ForceUpdateCanvases();
            foreach (RectTransform rt in presenter.CanvasObjectForTests.GetComponentsInChildren<RectTransform>(true)
                         .OrderByDescending(r => r.GetComponentsInParent<Transform>(true).Length))
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            return presenter;
        }

        private static Rect WorldRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            float xMin = c.Min(v => v.x), xMax = c.Max(v => v.x);
            float yMin = c.Min(v => v.y), yMax = c.Max(v => v.y);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        [Test]
        public void Avatar_ActuallyBuildsItsCanvas()
        {
            AvatarPresenter presenter = Open();
            Assert.IsNotNull(presenter.CanvasObjectForTests, "Avatar built no canvas.");
            Assert.Greater(presenter.CanvasObjectForTests.GetComponentsInChildren<RectTransform>(true).Length, 3,
                "Avatar built a suspiciously empty canvas.");
        }

        [Test]
        public void Avatar_NeverDrawsArtOnTopOfAnInteractiveControl()
        {
            AvatarPresenter presenter = Open();
            Transform canvas = presenter.CanvasObjectForTests.transform;

            Transform[] drawOrder = canvas.GetComponentsInChildren<Transform>(true);
            var indexOf = new Dictionary<Transform, int>();
            for (int i = 0; i < drawOrder.Length; i++) indexOf[drawOrder[i]] = i;

            var collisions = new List<string>();
            foreach (Button button in canvas.GetComponentsInChildren<Button>(true))
            {
                if (!button.gameObject.activeInHierarchy) continue;
                Rect btn = WorldRect(button.GetComponent<RectTransform>());
                int buttonIndex = indexOf[button.transform];

                foreach (Image img in canvas.GetComponentsInChildren<Image>(true))
                {
                    if (img.sprite == null || !img.gameObject.activeInHierarchy) continue;
                    if (img.GetComponent<Button>() != null) continue;      // the button's own art
                    if (img.transform.IsChildOf(button.transform)) continue;
                    if (indexOf[img.transform] <= buttonIndex) continue;   // drawn before the button = safely behind it

                    Rect art = WorldRect(img.rectTransform);
                    if (art.width <= 0f || art.height <= 0f) continue;
                    if (art.Overlaps(btn))
                        collisions.Add($"'{img.name}' {art} overlaps '{button.name}' {btn} and draws AFTER it");
                }
            }

            CollectionAssert.IsEmpty(collisions,
                "Art draws on top of an interactive control, so a tap would land on art instead of the button: " +
                string.Join("  |  ", collisions));
        }

        /// <summary>
        /// VS-UI-AVATAR-BACK-TARGET-006. Back was 160x40 at y=-5 inside a 100px header - smaller
        /// than the 160x60 SettingsPresenter.CreateHeaderButton already rejected as an unreliable
        /// landscape tap target, and too short for the nav-tile frame sprite, whose top ornament
        /// rendered flush against the screen edge. Asserted as relationships (contained in its
        /// header, clear of both header edges) plus the one magnitude that IS the spec.
        /// </summary>
        [Test]
        public void Avatar_BackButton_MeetsTapTargetAndSitsFullyInsideItsHeader()
        {
            AvatarPresenter presenter = Open();
            Transform canvas = presenter.CanvasObjectForTests.transform;

            Transform header = canvas.Find("AvatarHeader");
            Assert.IsNotNull(header, "Avatar built no AvatarHeader.");
            Transform backTf = header.Find("Btn_Back");
            Assert.IsNotNull(backTf, "Avatar header built no Btn_Back.");
            Assert.IsNotNull(backTf.GetComponent<Button>(), "Btn_Back is not an actual Button.");

            Rect back = WorldRect((RectTransform)backTf);
            Rect head = WorldRect((RectTransform)header);

            // Size in canvas reference units (rect), not world corners - the CanvasScaler scales
            // world space by the device/reference ratio, so a world rect measures ~89x32 for the
            // very same 200x72 plate. Containment below is a pure relationship, so world rects are
            // the right space for it.
            Rect backLocal = ((RectTransform)backTf).rect;
            Assert.GreaterOrEqual(backLocal.width, 200f, "Back tap target is narrower than the 200x72 standard.");
            Assert.GreaterOrEqual(backLocal.height, 72f, "Back tap target is shorter than the 200x72 standard.");

            Assert.GreaterOrEqual(back.yMin, head.yMin,
                $"Back {back} hangs below its header {head}.");
            Assert.LessOrEqual(back.yMax, head.yMax,
                $"Back {back} overruns the top of its header {head} - the frame ornament would clip at the screen edge.");
            Assert.GreaterOrEqual(back.xMin, head.xMin, $"Back {back} starts left of its header {head}.");
            Assert.LessOrEqual(back.xMax, head.xMax, $"Back {back} runs past the right of its header {head}.");
        }
    }
}

using System;
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
    /// Solo Circuit layout/paint-order — the only UI Presenter that still lacked dedicated
    /// paint-order coverage after the prior sweep (SoloCircuitPresenterTests cover
    /// reachability/hygiene only). Same method as SettingsLayoutTests: measure built world rects
    /// via GetWorldCorners; flag sprite art that draws AFTER a button it geometrically overlaps.
    /// </summary>
    public class SoloCircuitLayoutTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MoDSoloCircuitLayout_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
            foreach (GameObject go in _spawned)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
            _spawned.Clear();
            foreach (Canvas c in UnityEngine.Object.FindObjectsOfType<Canvas>())
            {
                if (c != null) UnityEngine.Object.DestroyImmediate(c.gameObject);
            }
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, true);
        }

        private SoloCircuitPresenter Open()
        {
            PlayerProfile profile = SaveSystem.CurrentProfile;
            var go = new GameObject("SoloCircuitLayoutHost");
            _spawned.Add(go);
            var presenter = go.AddComponent<SoloCircuitPresenter>();
            presenter.Initialize(profile, new DateTime(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc), onBack: null);
            if (presenter.CanvasObjectForTests != null)
                _spawned.Add(presenter.CanvasObjectForTests);

            foreach (RectTransform rt in presenter.CanvasObjectForTests.GetComponentsInChildren<RectTransform>(true)
                         .OrderByDescending(r => r.GetComponentsInParent<Transform>(true).Length))
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            Canvas.ForceUpdateCanvases();
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
        public void SoloCircuit_ActuallyBuildsItsCanvas_WithTrialPlayButtons()
        {
            SoloCircuitPresenter presenter = Open();
            Assert.IsNotNull(presenter.CanvasObjectForTests, "Solo Circuit built no canvas.");
            Assert.AreEqual(SoloCircuitPresenter.CanvasName, presenter.CanvasObjectForTests.name);

            Transform canvas = presenter.CanvasObjectForTests.transform;
            Assert.IsNotNull(canvas.Find("SoloCircuitHeader/Btn_Back"), "Back missing.");
            Assert.IsNotNull(canvas.Find("TrialList/Trial_Formation/Btn_Play"), "Formation PLAY missing.");
            Assert.IsNotNull(canvas.Find("TrialList/Trial_Collection/Btn_Play"), "Collection PLAY missing.");
            Assert.IsNotNull(canvas.Find("TrialList/Trial_TacticalBrief/Btn_Play"), "TacticalBrief PLAY missing.");
        }

        [Test]
        public void SoloCircuit_NeverDrawsArtOnTopOfAnInteractiveControl()
        {
            SoloCircuitPresenter presenter = Open();
            Transform canvas = presenter.CanvasObjectForTests.transform;

            Transform[] drawOrder = canvas.GetComponentsInChildren<Transform>(true);
            var indexOf = new Dictionary<Transform, int>();
            for (int i = 0; i < drawOrder.Length; i++) indexOf[drawOrder[i]] = i;

            var collisions = new List<string>();
            foreach (Button button in canvas.GetComponentsInChildren<Button>(true))
            {
                if (!button.gameObject.activeInHierarchy) continue;
                RectTransform buttonRt = button.GetComponent<RectTransform>();
                if (buttonRt == null) continue;
                Rect btn = WorldRect(buttonRt);
                if (btn.width <= 0f || btn.height <= 0f) continue;
                if (!indexOf.TryGetValue(button.transform, out int buttonIndex)) continue;

                foreach (Image img in canvas.GetComponentsInChildren<Image>(true))
                {
                    if (img.sprite == null || !img.gameObject.activeInHierarchy) continue;
                    if (img.GetComponent<Button>() != null) continue;
                    if (img.transform.IsChildOf(button.transform)) continue;
                    if (!indexOf.TryGetValue(img.transform, out int artIndex)) continue;
                    if (artIndex <= buttonIndex) continue;

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
        /// VS-REVAMP-002: the approved SoloCircuitV1 backdrop must actually be BOUND, not silently
        /// falling back to UIFrozenTokens.ColorBackground. CreateFullscreenBackground swallows a
        /// failed Resources.Load into a flat opaque colour, so a broken path/import looks fine on a
        /// screenshot and is invisible to every other test on this screen. Mirrors
        /// TacticalPuzzlePresenterTests' backdrop assertion and the Empire equivalent.
        /// </summary>
        [Test]
        public void SoloCircuit_Backdrop_BindsTheApprovedSoloCircuitV1Asset()
        {
            Sprite expected = Resources.Load<Sprite>(SoloCircuitPresenter.BackdropResourcePath);
            Assert.IsNotNull(expected,
                "Approved asset failed to Resources.Load '" + SoloCircuitPresenter.BackdropResourcePath
                + "' - import/path broken, so the screen can only ever show the flat fallback.");

            SoloCircuitPresenter presenter = Open();
            Transform backdropTransform = presenter.CanvasObjectForTests.transform.Find("Background");
            Assert.IsNotNull(backdropTransform,
                "Setup: expected the Solo Circuit fullscreen backdrop GameObject to exist.");

            Image backdrop = backdropTransform.GetComponent<Image>();
            Assert.IsNotNull(backdrop, "The Solo Circuit backdrop must carry an Image component.");

            Assert.AreSame(expected, backdrop.sprite,
                "Solo Circuit must render the approved SoloCircuitV1 backdrop, not a flat-colour fallback "
                + "or a silent substitute.");

            // The popup contract from the presenter header: EmpireCanvas is genuinely alive
            // underneath, so this backdrop stays raycast-blocking on purpose.
            Assert.IsTrue(backdrop.raycastTarget,
                "The Solo Circuit backdrop must keep blocking taps - EmpireCanvas is live underneath this popup.");
        }
    }
}

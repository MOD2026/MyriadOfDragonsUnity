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
    /// Empire (main overview screen, NOT the building-detail popup - that already has coverage in
    /// EmpireBuildingDetailLayoutTests) layout/geometry coverage after design-token rollout
    /// (batch 1, 5f204a5). Same method as EmpireBuildingDetailLayoutTests/
    /// TacticalPuzzleLayoutTests: measure the BUILT hierarchy's real world rects, only flag art
    /// that draws AFTER a button it geometrically overlaps (real depth-first paint/raycast order).
    /// The construction-panel / Btn_Back overlap that surfaced once the panel gained a bordered
    /// sprite is exactly why this must keep running post-token.
    /// </summary>
    public class EmpireLayoutTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDEmpireLayout_" + System.Guid.NewGuid().ToString("N"));
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

        private EmpirePresenter Open()
        {
            var go = new GameObject("EmpireLayoutHost");
            _spawned.Add(go);
            var presenter = go.AddComponent<EmpirePresenter>();
            presenter.Initialize(onBackToHome: null);
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
        public void Empire_ActuallyBuildsItsCanvas()
        {
            EmpirePresenter presenter = Open();
            Assert.IsNotNull(presenter.CanvasObjectForTests, "Empire built no canvas.");
            Assert.Greater(presenter.CanvasObjectForTests.GetComponentsInChildren<RectTransform>(true).Length, 3,
                "Empire built a suspiciously empty canvas.");
        }

        [Test]
        public void Empire_NeverDrawsArtOnTopOfAnInteractiveControl()
        {
            EmpirePresenter presenter = Open();
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
                    if (img.GetComponent<Button>() != null) continue;
                    // Non-raycastable art cannot receive a tap at all, so it cannot be the thing a
                    // tap "lands on" - uGUI skips it during the raycast entirely. Without this the
                    // check reports a failure it has already ruled out by construction: every
                    // AddLocalGradientScrim graphic is created with raycastTarget = false
                    // (UISharedFoundation.AddLocalGradientScrim), and the Empire backdrop now clears
                    // it too (VS-REVAMPV2-009). Overlap alone was never the hazard - overlap plus
                    // input interception is.
                    if (!img.raycastTarget) continue;
                    if (img.transform.IsChildOf(button.transform)) continue;
                    if (indexOf[img.transform] <= buttonIndex) continue;

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

        [Test]
        public void Empire_Background_BindsTheApprovedTextFreeCityBackdrop()
        {
            EmpirePresenter presenter = Open();
            Transform canvasTransform = presenter.CanvasObjectForTests.transform;

            // Verify the Background child exists
            Transform backgroundTransform = canvasTransform.Find("Background");
            Assert.IsNotNull(backgroundTransform, "Setup: expected the Empire backdrop GameObject to exist.");
            GameObject background = backgroundTransform.gameObject;
            Image backgroundImage = background.GetComponent<Image>();
            Assert.IsNotNull(backgroundImage,
                "Background must have an Image component.");

            // VS-REVAMPV2-009: the backdrop is decoration spanning the whole 1920x1080 canvas and
            // must not be raycastable. CreateFullscreenBackground leaves raycastTarget at Unity's
            // default true, so EmpirePresenter clears it explicitly; without that the entire screen
            // is one tap-absorbing graphic. Empire is a fullscreen screen, unlike the popups
            // (SoloCircuit, TacticalPuzzle, GuildHallEntry) whose backdrops block on purpose to
            // protect a live canvas underneath - there is nothing under Empire to protect.
            Assert.IsFalse(backgroundImage.raycastTarget,
                "The full-screen Empire backdrop must never intercept a tap meant for a real control "
                + "drawn above it.");

            // Empire must bind the approved text-free city backdrop (never the flattened empire_v2 mockup with baked title/rail/pill chrome) — a null sprite means silent fallback to flat color
            Assert.IsNotNull(backgroundImage.sprite,
                "Empire Background Image.sprite is null — flat colour fallback, not the approved backdrop artwork.");

            // Load the approved sprite and verify it matches what Empire loaded
            const string approvedAssetPath = "UI/Backdrops/Zihan_City_NO NAMES";
            Sprite expected = Resources.Load<Sprite>(approvedAssetPath);
            Assert.IsNotNull(expected,
                $"Empire backdrop failed to Resources.Load '{approvedAssetPath}' — import/path broken.");
            Assert.AreSame(expected, backgroundImage.sprite,
                $"Empire must render the approved text-free backdrop '{approvedAssetPath}', not a silent substitute.");
        }
    }
}

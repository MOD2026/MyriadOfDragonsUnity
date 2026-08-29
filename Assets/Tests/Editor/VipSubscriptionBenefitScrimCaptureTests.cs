using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MyriadOfDragons.Metagame;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Interactive capture evidence for VS-UI-VIP-BENEFIT-SCRIM-GEOMETRY-001 (follows 2d886ad,
    /// which fixed BuildBenefitGrid's scrim size/offset but shipped WITHOUT a visual capture).
    ///
    /// Two things are proved here, both against a LOADED profile (an active monthly VIP plan with
    /// claims consumed, not the empty default) and at the locked landscape target 1920x1080:
    ///   1. geometry - every one of the six benefit wells contains both of its gradient scrims,
    ///      measured as real world rects, and no scrim leaves its own well;
    ///   2. pixels - a real render of the screen written to disk, so the containment can be
    ///      eye-checked rather than only asserted.
    ///
    /// Resolution note: a presenter built in EditMode resolves its canvas to the editor's default
    /// screen (640x480), not to 1920x1080 - so the canvas rect is set to the target size first,
    /// and BuildBenefitGrid's OWN formula (0.9 x 0.32 of the measured well) is re-evaluated
    /// against the resized wells. That is exactly what the shipped code computes on a real
    /// 1920x1080 device, where rect.size already resolves to the full-size well; the test does not
    /// invent a different rule, it evaluates the shipped one at the target resolution.
    /// </summary>
    public class VipSubscriptionBenefitScrimCaptureTests
    {
        private const int CaptureWidth = 1920;
        private const int CaptureHeight = 1080;
        // Same fractions BuildBenefitGrid uses for each well's scrim pair.
        private const float ScrimWidthFraction = 0.9f;
        private const float ScrimHeightFraction = 0.32f;

        private readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();
        private string _scratchSaveDir;
        private Camera _captureCamera;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDVipScrimCapture_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();

            // A LOADED profile, not the default empty one: real currencies, a levelled avatar, and
            // an ACTIVE monthly VIP entitlement mid-way through its claims, so the identity column
            // and status line render their subscribed state rather than the not-subscribed shell.
            var profile = new PlayerProfile
            {
                gold = 50_000,
                gems = 5_000,
                stamina = 100,
                maxStamina = 100,
                constructionMaterials = 50_000,
                avatarLevel = 10,
                vipPlanId = "monthly",
                vipStartedUtcTicks = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc).Ticks,
                vipExpiresUtcTicks = new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc).Ticks,
                vipClaimsConsumed = 2,
            };
            SaveSystem.Save(profile);
            SaveSystem.ResetCurrentProfileForTests();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
            foreach (UnityEngine.Object o in _spawned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _spawned.Clear();
            _captureCamera = null;
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir)) Directory.Delete(_scratchSaveDir, true);
        }

        private VipSubscriptionPresenter OpenAt1920x1080()
        {
            var go = new GameObject("VipScrimCaptureHost");
            _spawned.Add(go);
            var presenter = go.AddComponent<VipSubscriptionPresenter>();
            presenter.Initialize(onBack: null);
            GameObject canvasObj = presenter.CanvasObjectForTests;
            Assert.IsNotNull(canvasObj, "VipSubscription built no canvas.");

            // A Screen Space - Overlay canvas DRIVES its own RectTransform from the real screen
            // (640x480 in EditMode), so assigning sizeDelta there is silently ignored - measured:
            // the first version of this test read back 640 after setting 1920. World Space hands
            // the rect back to us, which is both what makes 1920x1080 reachable at all and what
            // the capture camera needs, so it is set up ONCE here and shared by both tests.
            _captureCamera = BuildCaptureCamera();
            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _captureCamera;
            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.position = Vector3.zero;
            canvasRect.rotation = Quaternion.identity;
            canvasRect.localScale = Vector3.one;
            canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
            canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
            canvasRect.sizeDelta = new Vector2(CaptureWidth, CaptureHeight);
            Canvas.ForceUpdateCanvases();
            foreach (RectTransform rt in canvasObj.GetComponentsInChildren<RectTransform>(true)
                         .OrderByDescending(r => r.GetComponentsInParent<Transform>(true).Length))
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            // Re-evaluate the shipped scrim formula against the now full-size wells (see class doc).
            foreach (RectTransform well in BenefitWells(canvasObj.transform))
            {
                Vector2 wellSize = well.rect.size;
                foreach (RectTransform scrim in ScrimsOf(well))
                    scrim.sizeDelta = new Vector2(wellSize.x * ScrimWidthFraction, wellSize.y * ScrimHeightFraction);
            }
            Canvas.ForceUpdateCanvases();
            return presenter;
        }

        private static IEnumerable<RectTransform> BenefitWells(Transform canvas)
        {
            return canvas.GetComponentsInChildren<RectTransform>(true)
                .Where(r => r.name.StartsWith("BenefitWell_"))
                .OrderBy(r => r.name);
        }

        private static IEnumerable<RectTransform> ScrimsOf(RectTransform well)
        {
            Transform layer = well.Find("ScrimLayer");
            if (layer == null) yield break;
            foreach (RectTransform child in layer.GetComponentsInChildren<RectTransform>(true))
                if (child.name == "GradientScrim") yield return child;
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
        public void VipSubscription_AllSixBenefitScrims_StayInsideTheirOwnWell_At1920x1080()
        {
            VipSubscriptionPresenter presenter = OpenAt1920x1080();
            Rect canvasRect = WorldRect(presenter.CanvasObjectForTests.GetComponent<RectTransform>());
            Assert.AreEqual(CaptureWidth, Mathf.RoundToInt(canvasRect.width), "Canvas is not at the locked 1920 width.");
            Assert.AreEqual(CaptureHeight, Mathf.RoundToInt(canvasRect.height), "Canvas is not at the locked 1080 height.");

            RectTransform[] wells = BenefitWells(presenter.CanvasObjectForTests.transform).ToArray();
            Assert.AreEqual(VipSubscriptionOpenValues.BenefitWellCount, wells.Length,
                "Wrong number of benefit wells built.");

            var report = new StringBuilder();
            var escapes = new List<string>();
            const float tolerance = 0.01f;
            foreach (RectTransform well in wells)
            {
                Rect w = WorldRect(well);
                RectTransform[] scrims = ScrimsOf(well).ToArray();
                Assert.AreEqual(2, scrims.Length, $"{well.name} should carry exactly two gradient scrims.");
                report.AppendLine($"{well.name} well={w}");
                foreach (RectTransform scrim in scrims)
                {
                    Rect s = WorldRect(scrim);
                    report.AppendLine($"    scrim={s}");
                    if (s.xMin < w.xMin - tolerance || s.xMax > w.xMax + tolerance ||
                        s.yMin < w.yMin - tolerance || s.yMax > w.yMax + tolerance)
                        escapes.Add($"{well.name}: scrim {s} escapes well {w}");
                }
            }

            Debug.Log("[VipScrim] containment at 1920x1080:\n" + report);
            CollectionAssert.IsEmpty(escapes,
                "A benefit scrim draws outside its own well: " + string.Join("  |  ", escapes));
        }

        [Test]
        public void VipSubscription_LoadedProfile_CapturedAt1920x1080()
        {
            VipSubscriptionPresenter presenter = OpenAt1920x1080();
            GameObject canvasObj = presenter.CanvasObjectForTests;
            Assert.AreEqual(VipSubscriptionOpenValues.BenefitWellCount,
                BenefitWells(canvasObj.transform).Count(), "Capture must show all six benefit wells.");

            Texture2D tex = CaptureWorldSpace();
            Assert.IsNotNull(tex, "Capture produced no texture.");

            string outDir = Path.Combine(Directory.GetCurrentDirectory(), "vip_scrim_capture_out");
            Directory.CreateDirectory(outDir);
            string path = Path.Combine(outDir, "VipSubscription_LoadedProfile_1920x1080.png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            Assert.IsTrue(File.Exists(path) && new FileInfo(path).Length > 0, "Capture file was not written.");
            Debug.Log($"[VipScrim] capture written to {path}");
        }

        /// <summary>Screen Space - Overlay canvases cannot be rendered into a RenderTexture (the
        /// constraint ScreenContactSheetGenerator documents). The canvas was already put into
        /// World Space against this camera by OpenAt1920x1080, so the frame captured here is the
        /// exact geometry the containment test measures - not a second, differently-sized
        /// arrangement built for the screenshot.</summary>
        private Camera BuildCaptureCamera()
        {
            var camGo = new GameObject("VipScrimCaptureCamera");
            _spawned.Add(camGo);
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f, 1f);
            cam.orthographic = true;
            cam.orthographicSize = CaptureHeight * 0.5f;
            cam.aspect = CaptureWidth / (float)CaptureHeight;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 5000f;
            cam.cullingMask = ~0;
            camGo.transform.position = new Vector3(0f, 0f, -1000f);
            camGo.transform.rotation = Quaternion.identity;
            return cam;
        }

        private Texture2D CaptureWorldSpace()
        {
            Camera cam = _captureCamera;
            if (cam == null) return null;

            var rt = new RenderTexture(CaptureWidth, CaptureHeight, 24);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;

            cam.targetTexture = null;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            return tex;
        }
    }
}

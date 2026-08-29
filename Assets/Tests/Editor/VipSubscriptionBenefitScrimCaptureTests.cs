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
        private readonly List<string> _capturedLoadWarnings = new List<string>();
        private string _scratchSaveDir;
        private Camera _captureCamera;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDVipScrimCapture_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();

            // Shell/atlas load failures are reported by VipSubscriptionUiLibrary as Debug.LogWarning,
            // which no assertion could otherwise see. Recorded from SetUp so every test in this
            // fixture - including the two that predate this instrumentation - is covered.
            _capturedLoadWarnings.Clear();
            Application.logMessageReceived += OnLogMessage;

            // Default seed stays exactly what it was: a LOADED profile, not the empty default -
            // real currencies, a levelled avatar, and an ACTIVE monthly VIP entitlement mid-way
            // through its claims, so the identity column and status line render their subscribed
            // state rather than the not-subscribed shell.
            SeedActiveMonthlyProfile();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
        }

        /// <summary>The fixture's original seed, unchanged - extracted so the unsubscribed capture
        /// can re-seed inside its own test without altering what every other test receives.</summary>
        private static void SeedActiveMonthlyProfile()
        {
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
        }

        /// <summary>Same wallet/level as the active seed so the two captures differ ONLY in
        /// entitlement - no VIP plan id and no start/expiry ticks, which is what
        /// VipSubscriptionOpenValues.IsSubscriptionActive reads to decide "Not subscribed".</summary>
        private static void SeedUnsubscribedProfile()
        {
            var profile = new PlayerProfile
            {
                gold = 50_000,
                gems = 5_000,
                stamina = 100,
                maxStamina = 100,
                constructionMaterials = 50_000,
                avatarLevel = 10,
            };
            SaveSystem.Save(profile);
            SaveSystem.ResetCurrentProfileForTests();
        }

        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Warning && type != LogType.Error && type != LogType.Exception) return;
            if (string.IsNullOrEmpty(condition)) return;
            if (condition.Contains("[VipSubscription]")
                || condition.Contains(VipSubscriptionUiLibrary.ScreenShellName)
                || condition.Contains(VipSubscriptionUiLibrary.StateAtlasName))
                _capturedLoadWarnings.Add($"{type}: {condition}");
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLogMessage;
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

            // Was a second hardcoded copy of the same literal; routed through OutDir so this
            // capture honours the same redirection as every other one in the fixture.
            string path = Path.Combine(OutDir, "VipSubscription_LoadedProfile_1920x1080.png");
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

        // ------------------------------------------------------------------------------------
        // VS-VIP-HARNESS-INSTRUMENT-006 - evidence instrumentation.
        //
        // The committed harness proved benefit-scrim containment and wrote one PNG. Everything
        // below exists so ONE Unity lane can produce the whole locked VIP manifest instead of two.
        // No production dependency is added: every value is read back from shipped
        // VipSubscriptionUiLibrary / VipSubscriptionPresenter surfaces that already exist.
        // ------------------------------------------------------------------------------------

        private const string OutDirName = "vip_scrim_capture_out";

        /// <summary>Env var a batch lane sets to redirect this fixture's captures. Exists because
        /// the runner's -BatchOutDir governs only its own logs, so before this seam every run
        /// overwrote the previous run's evidence in place and a seat had to back the directory up
        /// by hand to keep it (VS-VIP-RUNTIME-EVIDENCE-007, three runs, three manual backups).</summary>
        private const string OutDirEnvVar = "VIP_CAPTURE_OUT_DIR";

        private static string _outDirOverrideForTests;

        /// <summary>Test-only seam, same shape as SaveSystem.OverrideRootDirectoryForTests and
        /// CardTileCompositionV1.SetPackPresenceOverrideForTests. Null restores the default.
        /// Production VIP behaviour and atlas semantics are untouched by this - nothing outside
        /// this fixture reads it.</summary>
        public static void OverrideOutputDirectoryForTests(string directory)
        {
            _outDirOverrideForTests = string.IsNullOrWhiteSpace(directory) ? null : directory;
        }

        public static void ClearOutputDirectoryOverrideForTests()
        {
            _outDirOverrideForTests = null;
        }

        /// <summary>Resolution order: explicit test override, then the env var a batch lane sets,
        /// then the historical default. The default is preserved exactly, so a run that sets
        /// neither writes precisely where it always did and every existing capture name and fit
        /// field is unchanged.</summary>
        private static string OutDir
        {
            get
            {
                string selected = _outDirOverrideForTests;
                if (string.IsNullOrWhiteSpace(selected))
                    selected = Environment.GetEnvironmentVariable(OutDirEnvVar);
                if (string.IsNullOrWhiteSpace(selected))
                    selected = OutDirName;

                string dir = Path.IsPathRooted(selected)
                    ? selected
                    : Path.Combine(Directory.GetCurrentDirectory(), selected);
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        /// <summary>Writes name.png plus name_fit.txt beside it. The fit file always leads with the
        /// same three fields every other capture set in this project uses (screen/rendered/match)
        /// so one reader parses every lane's output, then any extra key=value lines the caller
        /// supplies. Callers pass the locked manifest filenames; nothing here renames or removes an
        /// existing capture.</summary>
        private static void WriteCaptureWithFit(string fileNameNoExtension, Texture2D tex,
            int expectedWidth, int expectedHeight, params string[] extraFitLines)
        {
            Assert.IsNotNull(tex, fileNameNoExtension + ": capture produced no texture.");

            string pngPath = Path.Combine(OutDir, fileNameNoExtension + ".png");
            File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            Assert.IsTrue(File.Exists(pngPath) && new FileInfo(pngPath).Length > 0,
                fileNameNoExtension + ": capture file was not written.");

            bool match = tex.width == expectedWidth && tex.height == expectedHeight;
            var fit = new StringBuilder();
            fit.AppendLine($"screen={expectedWidth}x{expectedHeight}");
            fit.AppendLine($"rendered={tex.width}x{tex.height}");
            fit.AppendLine($"match={(match ? 1 : 0)}");
            fit.AppendLine($"hasPack={VipSubscriptionUiLibrary.HasVipSubscriptionV1Pack}");
            for (int i = 0; i < extraFitLines.Length; i++) fit.AppendLine(extraFitLines[i]);

            File.WriteAllText(Path.Combine(OutDir, fileNameNoExtension + "_fit.txt"), fit.ToString());
            Debug.Log($"[VipScrim] wrote {pngPath} ({tex.width}x{tex.height}, match={(match ? 1 : 0)})");
        }

        /// <summary>The capture camera is orthographic and centred on the world origin, and
        /// OpenAt1920x1080 centres the canvas rect there too, so world (0,0) is the frame centre:
        /// pixel = world + half-extent. Used to crop a rendered frame to one control's world rect.</summary>
        private static Texture2D CropRenderedRegion(Texture2D frame, Rect worldRect, string label)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt(worldRect.xMin + CaptureWidth * 0.5f), 0, frame.width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(worldRect.yMin + CaptureHeight * 0.5f), 0, frame.height - 1);
            int w = Mathf.Clamp(Mathf.RoundToInt(worldRect.width), 1, frame.width - x);
            int h = Mathf.Clamp(Mathf.RoundToInt(worldRect.height), 1, frame.height - y);
            return CopyPixels(frame, x, y, w, h, label);
        }

        private static Texture2D CopyPixels(Texture2D src, int x, int y, int w, int h, string label)
        {
            Color[] pixels;
            try
            {
                pixels = src.GetPixels(x, y, w, h);
            }
            catch (Exception e)
            {
                Assert.Fail($"{label}: GetPixels({x},{y},{w},{h}) failed with {e.GetType().Name}. " +
                            "The atlas meta must keep Read/Write enabled (isReadable: 1).");
                return null;
            }

            var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            outTex.SetPixels(pixels);
            outTex.Apply();
            return outTex;
        }

        /// <summary>Lays textures left to right on one strip, top-aligned, 4px gutter, so a
        /// reviewer can compare cell widths and edges side by side in a single image.</summary>
        private static Texture2D BuildHorizontalContactSheet(IList<Texture2D> parts, string label)
        {
            Assert.IsTrue(parts != null && parts.Count > 0, label + ": nothing to lay out.");
            const int gutter = 4;
            int totalW = 0, maxH = 0;
            for (int i = 0; i < parts.Count; i++)
            {
                Assert.IsNotNull(parts[i], $"{label}: part {i} is null.");
                totalW += parts[i].width + (i > 0 ? gutter : 0);
                maxH = Mathf.Max(maxH, parts[i].height);
            }

            var sheet = new Texture2D(totalW, maxH, TextureFormat.RGBA32, false);
            var clear = new Color[totalW * maxH];
            for (int i = 0; i < clear.Length; i++) clear[i] = new Color(0.05f, 0.05f, 0.07f, 1f);
            sheet.SetPixels(clear);

            int cursor = 0;
            for (int i = 0; i < parts.Count; i++)
            {
                sheet.SetPixels(cursor, maxH - parts[i].height, parts[i].width, parts[i].height,
                    parts[i].GetPixels());
                cursor += parts[i].width + gutter;
            }

            sheet.Apply();
            return sheet;
        }

        private void AssertNoShellOrAtlasLoadWarnings(string context)
        {
            CollectionAssert.IsEmpty(_capturedLoadWarnings,
                context + ": shell/atlas load warnings were raised: " +
                string.Join("  |  ", _capturedLoadWarnings));
        }

        private static IEnumerable<RectTransform> PlanSockets(Transform canvas)
        {
            return canvas.GetComponentsInChildren<RectTransform>(true)
                .Where(r => r.name.StartsWith("StateSocket_"))
                .OrderBy(r => r.name);
        }

        private static string EntitlementLine(VipSubscriptionPresenter presenter)
        {
            return presenter.CanvasObjectForTests.transform
                .GetComponentsInChildren<Text>(true)
                .First(t => t.name == "EntitlementState").text;
        }

        [Test]
        public void VipSubscription_Unsubscribed_CapturedAt1920x1080()
        {
            // Re-seeded INSIDE the test: SetUp's active-monthly seed is what every other test in
            // this fixture expects, and the scratch save root is torn down per test, so this
            // cannot leak into them.
            SeedUnsubscribedProfile();

            VipSubscriptionPresenter presenter = OpenAt1920x1080();
            Assert.AreEqual("Not subscribed", EntitlementLine(presenter),
                "Unsubscribed capture must render the not-subscribed entitlement line.");

            Texture2D frame = CaptureWorldSpace();
            WriteCaptureWithFit("VIP_Unsubscribed_1920x1080", frame, CaptureWidth, CaptureHeight,
                "state=unsubscribed",
                $"loadWarnings={_capturedLoadWarnings.Count}");
            UnityEngine.Object.DestroyImmediate(frame);

            AssertNoShellOrAtlasLoadWarnings("Unsubscribed capture");
        }

        [Test]
        public void VipSubscription_ActiveEntitlement_CapturedAt1920x1080()
        {
            VipSubscriptionPresenter presenter = OpenAt1920x1080();
            string entitlement = EntitlementLine(presenter);
            StringAssert.StartsWith("Active", entitlement,
                "Active capture must render the subscribed entitlement line.");

            Texture2D frame = CaptureWorldSpace();
            WriteCaptureWithFit("VIP_ActiveEntitlement_1920x1080", frame, CaptureWidth, CaptureHeight,
                "state=active_monthly_claims_2_of_4",
                "entitlement=" + entitlement,
                $"loadWarnings={_capturedLoadWarnings.Count}");
            UnityEngine.Object.DestroyImmediate(frame);

            AssertNoShellOrAtlasLoadWarnings("Active entitlement capture");
        }

        [Test]
        public void VipSubscription_AtlasPack_LoadsWithEveryCellNonNull_AndCapturesCells0To7()
        {
            Sprite shell = VipSubscriptionUiLibrary.Load(VipSubscriptionUiLibrary.ScreenShellName);
            Sprite atlas = VipSubscriptionUiLibrary.Load(VipSubscriptionUiLibrary.StateAtlasName);
            Assert.IsNotNull(shell, "Shell sprite failed to load.");
            Assert.IsNotNull(atlas, "State atlas sprite failed to load.");
            Assert.IsTrue(VipSubscriptionUiLibrary.HasVipSubscriptionV1Pack,
                "HasVipSubscriptionV1Pack must be true or no VIP capture means anything.");
            Assert.IsNotNull(atlas.texture, "Atlas sprite has no backing texture.");

            // The IMPORTED texture, not the 2079x756 source PNG: maxTextureSize is 2048 and
            // maxTextureSizeSet is 0 (never configured), so whether the runtime width is 2079 or
            // 2048 is what decides fractional-boundary vs resampling. Measured, never assumed.
            int importedWidth = atlas.texture.width;
            int importedHeight = atlas.texture.height;
            float cellW = atlas.textureRect.width / VipSubscriptionUiLibrary.StateAtlasCellCount;
            bool fractional = Mathf.Abs(cellW - Mathf.Round(cellW)) > 0.0005f;
            string boundary = fractional ? "FRACTIONAL" : "EXACT";

            Debug.Log($"[VipScrim] atlas imported={importedWidth}x{importedHeight} " +
                      $"textureRect={atlas.textureRect} " +
                      $"cellCount={VipSubscriptionUiLibrary.StateAtlasCellCount} " +
                      $"cellW={cellW:F3} boundary={boundary}");

            var cellTextures = new List<Texture2D>();
            var perCell = new StringBuilder();
            for (int i = 0; i < VipSubscriptionUiLibrary.StateAtlasCellCount; i++)
            {
                Sprite cell = VipSubscriptionUiLibrary.LoadStateAtlasCell(i);
                Assert.IsNotNull(cell, $"LoadStateAtlasCell({i}) returned null.");
                Rect r = cell.textureRect;
                perCell.AppendLine($"cell{i}=x:{r.x:F3},y:{r.y:F3},w:{r.width:F3},h:{r.height:F3}");

                // Clamped to the texture: when cellW is FRACTIONAL the last cell's
                // floor(x) + round(width) overruns the atlas by a pixel, and an unclamped
                // GetPixels there would abort the whole evidence lane on cell 7 alone.
                int cx = Mathf.Clamp(Mathf.FloorToInt(r.x), 0, cell.texture.width - 1);
                int cy = Mathf.Clamp(Mathf.FloorToInt(r.y), 0, cell.texture.height - 1);
                int cw = Mathf.Clamp(Mathf.RoundToInt(r.width), 1, cell.texture.width - cx);
                int ch = Mathf.Clamp(Mathf.RoundToInt(r.height), 1, cell.texture.height - cy);
                Texture2D cellTex = CopyPixels(cell.texture, cx, cy, cw, ch, $"atlas cell {i}");
                cellTextures.Add(cellTex);

                WriteCaptureWithFit($"VIP_AtlasCell_{i}", cellTex, cellTex.width, cellTex.height,
                    $"cellIndex={i}",
                    $"cellRect=x:{r.x:F3},y:{r.y:F3},w:{r.width:F3},h:{r.height:F3}",
                    $"cellW={cellW:F3}",
                    $"atlasImported={importedWidth}x{importedHeight}",
                    "boundary=" + boundary,
                    // Static audit VS-UI-VIP-ATLAS-MAPPING-003: sockets take 0-2 and wells take
                    // 0-5, so 0-2 are shared and 6-7 are consumed by nothing. Recorded per cell so
                    // the claim sits beside the pixels instead of in a separate document.
                    "consumers=" + AtlasCellConsumers(i));
            }

            Texture2D sheet = BuildHorizontalContactSheet(cellTextures, "atlas cells 0-7");
            WriteCaptureWithFit("VIP_AtlasCells_0-7", sheet, sheet.width, sheet.height,
                $"atlasImported={importedWidth}x{importedHeight}",
                $"textureRect=x:{atlas.textureRect.x:F3},y:{atlas.textureRect.y:F3}," +
                $"w:{atlas.textureRect.width:F3},h:{atlas.textureRect.height:F3}",
                $"cellCount={VipSubscriptionUiLibrary.StateAtlasCellCount}",
                $"cellW={cellW:F3}",
                "boundary=" + boundary,
                $"loadWarnings={_capturedLoadWarnings.Count}",
                perCell.ToString().TrimEnd());

            foreach (Texture2D t in cellTextures) UnityEngine.Object.DestroyImmediate(t);
            UnityEngine.Object.DestroyImmediate(sheet);

            AssertNoShellOrAtlasLoadWarnings("Atlas cell capture");
        }

        /// <summary>Mechanical consumer map from the static audit - what the shipped presenter
        /// passes as a cell index, NOT what the art is meant to depict. The semantic question
        /// (are 0-2 plan crests or status glyphs?) is an art-owner answer no capture can settle.</summary>
        private static string AtlasCellConsumers(int cellIndex)
        {
            if (cellIndex <= 2) return $"StateSocket_{cellIndex}+BenefitWell_{cellIndex}";
            if (cellIndex <= 5) return $"BenefitWell_{cellIndex}";
            return "none";
        }

        [Test]
        public void VipSubscription_AllSixWells_AndThreeSockets_RenderTheirIcons_WithContactSheets()
        {
            VipSubscriptionPresenter presenter = OpenAt1920x1080();
            Transform canvas = presenter.CanvasObjectForTests.transform;

            RectTransform[] wells = BenefitWells(canvas).ToArray();
            RectTransform[] sockets = PlanSockets(canvas).ToArray();
            Assert.AreEqual(VipSubscriptionOpenValues.BenefitWellCount, wells.Length,
                "Wrong number of benefit wells built.");
            Assert.AreEqual(VipSubscriptionOpenValues.StateSocketCount, sockets.Length,
                "Wrong number of plan sockets built.");

            // ApplyAtlasIcon silently builds NOTHING when its sprite is null, so a missing icon is
            // invisible to any count-based check - assert the child and its sprite directly.
            foreach (RectTransform well in wells)
            {
                Transform icon = well.Find("BenefitIcon");
                Assert.IsNotNull(icon, well.name + " rendered no BenefitIcon.");
                Image img = icon.GetComponent<Image>();
                Assert.IsNotNull(img.sprite, well.name + ": BenefitIcon has no sprite.");
                Assert.IsTrue(img.preserveAspect, well.name + ": BenefitIcon must preserve aspect.");
            }

            foreach (RectTransform socket in sockets)
            {
                Transform icon = socket.Find("StateIcon");
                Assert.IsNotNull(icon, socket.name + " rendered no StateIcon.");
                Image img = icon.GetComponent<Image>();
                Assert.IsNotNull(img.sprite, socket.name + ": StateIcon has no sprite.");
                Assert.IsTrue(img.preserveAspect, socket.name + ": StateIcon must preserve aspect.");
            }

            Texture2D frame = CaptureWorldSpace();
            Assert.IsNotNull(frame, "Contact-sheet source frame did not render.");

            var wellCrops = new List<Texture2D>();
            var wellMeta = new StringBuilder();
            foreach (RectTransform well in wells)
            {
                Rect w = WorldRect(well);
                wellMeta.AppendLine($"{well.name}=x:{w.x:F2},y:{w.y:F2},w:{w.width:F2},h:{w.height:F2}");
                wellCrops.Add(CropRenderedRegion(frame, w, well.name));
            }

            Texture2D wellSheet = BuildHorizontalContactSheet(wellCrops, "benefit wells 0-5");
            WriteCaptureWithFit("VIP_BenefitWells_0-5", wellSheet, wellSheet.width, wellSheet.height,
                $"wellCount={wells.Length}",
                $"socketCount={sockets.Length}",
                $"loadWarnings={_capturedLoadWarnings.Count}",
                wellMeta.ToString().TrimEnd());

            var socketCrops = new List<Texture2D>();
            var socketMeta = new StringBuilder();
            foreach (RectTransform socket in sockets)
            {
                Rect sr = WorldRect(socket);
                socketMeta.AppendLine($"{socket.name}=x:{sr.x:F2},y:{sr.y:F2},w:{sr.width:F2},h:{sr.height:F2}");
                socketCrops.Add(CropRenderedRegion(frame, sr, socket.name));
            }

            Texture2D socketSheet = BuildHorizontalContactSheet(socketCrops, "plan sockets 0-2");
            WriteCaptureWithFit("VIP_PlanSockets_0-2", socketSheet, socketSheet.width, socketSheet.height,
                $"socketCount={sockets.Length}",
                $"loadWarnings={_capturedLoadWarnings.Count}",
                socketMeta.ToString().TrimEnd());

            foreach (Texture2D t in wellCrops) UnityEngine.Object.DestroyImmediate(t);
            foreach (Texture2D t in socketCrops) UnityEngine.Object.DestroyImmediate(t);
            UnityEngine.Object.DestroyImmediate(wellSheet);
            UnityEngine.Object.DestroyImmediate(socketSheet);
            UnityEngine.Object.DestroyImmediate(frame);

            AssertNoShellOrAtlasLoadWarnings("Well/socket render evidence");
        }
    }
}

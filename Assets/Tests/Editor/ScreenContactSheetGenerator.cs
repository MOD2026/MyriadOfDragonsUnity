using System;
using System.Collections.Generic;
using System.IO;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Batch-captures every presenter screen to a single PNG contact sheet for owner review, so
    /// screens don't have to be pasted one at a time. CC-authorized, independent of the art
    /// blocker (register: "Real 9-slice art request... Contact-sheet harness: go ahead and start
    /// it now").
    ///
    /// Overlay canvases (RenderMode.ScreenSpaceOverlay, what every screen in this project uses)
    /// draw directly to the framebuffer and are NOT capturable via Camera.Render() into a
    /// RenderTexture - that's a real Unity constraint, not an oversight. This works around it by
    /// temporarily switching each canvas to ScreenSpaceCamera with a dedicated offscreen camera +
    /// RenderTexture for the capture, then destroying the whole harness object (canvas mode is
    /// never restored/reused - the presenter itself is torn down right after).
    ///
    /// Runs in EditMode (this IS an EditMode test) - no Play Mode needed, since capture only
    /// requires Camera.Render(), which works in the editor without entering play mode.
    /// </summary>
    public class ScreenContactSheetGenerator
    {
        private readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();
        private string _scratchSaveDir;
        // THE LOCKED TARGET RESOLUTION - landscape 1920x1080 (CLAUDE.md, 2026-08-27).
        // Individual screen PNGs are captured at full size because that is the geometry the
        // owner reviews and the validator measures; both must describe the same frame.
        private const int CaptureWidth = 1920;
        private const int CaptureHeight = 1080;

        // The tiled sheet halves each tile. 24 screens at full size would be a 9600x5400
        // texture - ~155MB - which is a real risk of failing the run on memory rather than on
        // anything about the UI. The sheet is for spotting which screen to open; the full-size
        // PNG beside it is for actually looking.
        private const int SheetTileDivisor = 2;
        private const int TileCols = 5;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsContactSheet_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            var profile = new PlayerProfile
            {
                gold = 50_000, gems = 5_000, stamina = 100, maxStamina = 100,
                constructionMaterials = 50_000, avatarLevel = 10,
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
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (Directory.Exists(_scratchSaveDir)) Directory.Delete(_scratchSaveDir, true);
        }

        [Test]
        public void GenerateContactSheet()
        {
            var shots = new List<(string name, Texture2D tex)>();

            // Screens come from UiScreenRegistry, not a list local to this file. The UI
            // validation run walks the SAME list, so a screen can never be eye-reviewed here yet
            // silently missed by the gate - two lists would drift and the gate would still report
            // a clean pass over the smaller set.
            foreach (UiScreenEntry screen in UiScreenRegistry.Screens)
            {
                Capture(shots, screen.Name, () => screen.Build(NewHost));
            }

            Assert.Greater(shots.Count, 0, "No screens captured at all - harness itself is broken.");

            string outDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsContactSheetOutput");
            Directory.CreateDirectory(outDir);
            foreach (var (name, tex) in shots)
            {
                if (tex == null) continue;
                File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
            }

            Texture2D sheet = BuildTiledSheet(shots);
            string sheetPath = Path.Combine(outDir, "_ContactSheet.png");
            File.WriteAllBytes(sheetPath, sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
            foreach (var (_, tex) in shots) if (tex != null) UnityEngine.Object.DestroyImmediate(tex);

            Debug.Log($"[ContactSheet] Captured {shots.Count} screens to {outDir} (sheet: {sheetPath}).");
        }

        private GameObject NewHost(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        /// <summary>Builds, captures, and tears down one screen - failures are recorded as a
        /// missing tile rather than aborting the whole run, so one broken screen's Initialize
        /// doesn't hide every other screen's real result.</summary>
        private void Capture(List<(string, Texture2D)> shots, string label, Func<GameObject> build)
        {
            GameObject canvasObj = null;
            try
            {
                canvasObj = build();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ContactSheet] '{label}' failed to build: {e.Message}");
            }

            if (canvasObj == null)
            {
                Debug.LogWarning($"[ContactSheet] '{label}' produced no canvas - skipped.");
                shots.Add((label, null));
                return;
            }

            Texture2D tex = CaptureOverlayCanvas(canvasObj);
            shots.Add((label, tex));

            // Tear down everything this capture created so the next screen starts clean -
            // CleanupStaleMetagameCanvases handles the named-canvas cases, DestroyImmediate on the
            // whole host covers the rest (components + any child canvases like popups).
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
        }

        /// <summary>Screen Space - Overlay canvases (every screen in this project) draw directly
        /// to the framebuffer and cannot be captured by Camera.Render() into a RenderTexture in
        /// their normal mode. Temporarily reassigns the canvas to Screen Space - Camera with a
        /// throwaway offscreen camera so a real render can be captured, exactly for this one
        /// frame - the canvas is destroyed right after by the caller's cleanup, so nothing needs
        /// restoring.</summary>
        private Texture2D CaptureOverlayCanvas(GameObject canvasObj)
        {
            Canvas canvas = canvasObj.GetComponent<Canvas>();
            if (canvas == null) return null;

            var camGo = new GameObject("ContactSheetCamera");
            _spawned.Add(camGo);
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f, 1f);
            cam.orthographic = true;
            cam.cullingMask = ~0;

            var rt = new RenderTexture(CaptureWidth, CaptureHeight, 24);
            cam.targetTexture = rt;

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;

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

        private Texture2D BuildTiledSheet(List<(string name, Texture2D tex)> shots)
        {
            int tileW = CaptureWidth / SheetTileDivisor;
            int tileH = CaptureHeight / SheetTileDivisor;
            int rows = Mathf.CeilToInt(shots.Count / (float)TileCols);
            int sheetW = TileCols * tileW;
            int sheetH = rows * tileH;
            var sheet = new Texture2D(sheetW, sheetH, TextureFormat.RGB24, false);

            var blank = new Color[sheetW * sheetH];
            for (int i = 0; i < blank.Length; i++) blank[i] = new Color(0.02f, 0.02f, 0.03f);
            sheet.SetPixels(blank);

            for (int i = 0; i < shots.Count; i++)
            {
                Texture2D tile = shots[i].tex;
                if (tile == null) continue;

                Color[] full = tile.GetPixels();
                var small = new Color[tileW * tileH];
                for (int y = 0; y < tileH; y++)
                {
                    int srcRow = y * SheetTileDivisor * CaptureWidth;
                    int dstRow = y * tileW;
                    for (int x = 0; x < tileW; x++)
                    {
                        small[dstRow + x] = full[srcRow + x * SheetTileDivisor];
                    }
                }

                int col = i % TileCols;
                int row = i / TileCols;
                int px = col * tileW;
                int py = sheetH - (row + 1) * tileH; // top-left origin visually
                sheet.SetPixels(px, py, tileW, tileH, small);
            }

            sheet.Apply();
            return sheet;
        }
    }
}

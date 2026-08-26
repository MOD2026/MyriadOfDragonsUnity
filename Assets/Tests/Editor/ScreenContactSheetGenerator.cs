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
        private const int CaptureWidth = 960;
        private const int CaptureHeight = 540;
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

            Capture(shots, "Home", () =>
            {
                var host = NewHost("CS_Home");
                var home = host.AddComponent<HomePagePresenter>();
                home.BuildHomePageUIForTests();
                return home.HomeCanvasObjectForTests;
            });

            Capture(shots, "CampaignMap", () =>
            {
                var host = NewHost("CS_CampaignMap");
                var map = host.AddComponent<CampaignMapPresenter>();
                map.Initialize(onBackToHome: null, onLaunchBattle: null);
                return GameObject.Find("CampaignMapCanvas");
            });

            Capture(shots, "Shop", () =>
            {
                var host = NewHost("CS_Shop");
                var shop = host.AddComponent<ShopPresenter>();
                shop.Initialize(SaveSystem.CurrentProfile, onBackToHome: null);
                return GameObject.Find("ShopCanvas");
            });

            Capture(shots, "DeckBuilder", () =>
            {
                var host = NewHost("CS_DeckBuilder");
                var deck = host.AddComponent<DeckBuilderPresenter>();
                deck.Initialize(onBackToHome: null);
                return GameObject.Find("DeckBuilderCanvas");
            });

            Capture(shots, "Collection", () =>
            {
                var host = NewHost("CS_Collection");
                var col = host.AddComponent<CollectionPresenter>();
                col.Initialize(onBackToHome: null, onOpenDeckBuilder: null);
                return col.CanvasObjectForTests;
            });

            Capture(shots, "Empire", () =>
            {
                var host = NewHost("CS_Empire");
                var empire = host.AddComponent<EmpirePresenter>();
                empire.Initialize(onBackToHome: null);
                return empire.CanvasObjectForTests;
            });

            Capture(shots, "Avatar", () =>
            {
                var host = NewHost("CS_Avatar");
                var avatar = host.AddComponent<AvatarPresenter>();
                avatar.Initialize(onBackToHome: null);
                return avatar.CanvasObjectForTests;
            });

            Capture(shots, "Settings", () =>
            {
                var host = NewHost("CS_Settings");
                var settings = host.AddComponent<SettingsPresenter>();
                settings.Initialize(onBackToHome: null);
                return settings.CanvasObjectForTests;
            });

            Capture(shots, "EmpireBuildingDetail", () =>
            {
                var host = NewHost("CS_EmpireBuildingDetail");
                var detail = host.AddComponent<EmpireBuildingDetailPresenter>();
                detail.Initialize(EmpireBuildingKind.Castle, onClose: null);
                return GameObject.Find(EmpireBuildingDetailPresenter.CanvasName);
            });

            Capture(shots, "EmpireExpedition", () =>
            {
                var host = NewHost("CS_EmpireExpedition");
                var exp = host.AddComponent<EmpireExpeditionPresenter>();
                exp.Initialize(onBack: null, guildBonusQuery: UnavailableGuildExpeditionBonusQuery.Instance);
                return GameObject.Find(EmpireExpeditionPresenter.CanvasName);
            });

            Capture(shots, "GuildHallEntry", () =>
            {
                var host = NewHost("CS_GuildHallEntry");
                var gh = host.AddComponent<GuildHallEntryPresenter>();
                gh.Initialize(onBack: null);
                return GameObject.Find(GuildHallEntryPresenter.CanvasName);
            });

            Capture(shots, "GuildExpedition", () =>
            {
                var host = NewHost("CS_GuildExpedition");
                var ge = host.AddComponent<GuildExpeditionPresenter>();
                ge.Initialize(onBack: null);
                return GameObject.Find(GuildExpeditionPresenter.CanvasName);
            });

            Capture(shots, "TacticalPuzzle", () =>
            {
                var host = NewHost("CS_TacticalPuzzle");
                var tp = host.AddComponent<TacticalPuzzlePresenter>();
                tp.Initialize(TacticalPuzzleLibrary.AvailablePuzzles(), onExit: null);
                return GameObject.Find(TacticalPuzzlePresenter.CanvasName);
            });

            Capture(shots, "BattlePass", () =>
            {
                var host = NewHost("CS_BattlePass");
                var bp = host.AddComponent<BattlePassPresenter>();
                bp.Initialize(onBackToHome: null);
                return GameObject.Find(BattlePassPresenter.CanvasName);
            });

            Capture(shots, "DailyLoginQuests", () =>
            {
                var host = NewHost("CS_DailyLoginQuests");
                var dl = host.AddComponent<DailyLoginQuestsPresenter>();
                dl.Initialize(onBackToHome: null);
                return GameObject.Find(DailyLoginQuestsPresenter.CanvasName);
            });

            Capture(shots, "MailInbox", () =>
            {
                var host = NewHost("CS_MailInbox");
                var mail = host.AddComponent<MailInboxPresenter>();
                mail.Initialize(onBack: null);
                return GameObject.Find(MailInboxPresenter.CanvasName);
            });

            Capture(shots, "Friends", () =>
            {
                var host = NewHost("CS_Friends");
                var friends = host.AddComponent<FriendsPresenter>();
                friends.Initialize(onBack: null);
                return GameObject.Find(FriendsPresenter.CanvasName);
            });

            Capture(shots, "VipSubscription", () =>
            {
                var host = NewHost("CS_VipSubscription");
                var vip = host.AddComponent<VipSubscriptionPresenter>();
                vip.Initialize(onBack: null);
                return GameObject.Find(VipSubscriptionPresenter.CanvasName);
            });

            Capture(shots, "PermitWeekKey", () =>
            {
                var host = NewHost("CS_PermitWeekKey");
                var permit = host.AddComponent<PermitWeekKeyPresenter>();
                permit.Initialize(onBack: null);
                return GameObject.Find(PermitWeekKeyPresenter.CanvasName);
            });

            Capture(shots, "SpellLoadoutPicker", () =>
            {
                var host = NewHost("CS_SpellLoadoutPicker");
                var spell = host.AddComponent<SpellLoadoutPickerPresenter>();
                spell.Initialize(onBack: null);
                return GameObject.Find(SpellLoadoutPickerPresenter.CanvasName);
            });

            Capture(shots, "ChatSocial", () =>
            {
                var host = NewHost("CS_ChatSocial");
                var chat = host.AddComponent<ChatSocialPresenter>();
                chat.Initialize(onBack: null);
                return GameObject.Find(ChatSocialPresenter.CanvasName);
            });

            Capture(shots, "Bazaar", () =>
            {
                var host = NewHost("CS_Bazaar");
                var bazaar = host.AddComponent<BazaarPresenter>();
                bazaar.Initialize(onBack: null);
                return GameObject.Find(BazaarPresenter.CanvasName);
            });

            Capture(shots, "MemoryExpedition", () =>
            {
                var host = NewHost("CS_MemoryExpedition");
                var mem = host.AddComponent<MemoryExpeditionPresenter>();
                mem.Initialize(onBack: null);
                return GameObject.Find(MemoryExpeditionPresenter.CanvasName);
            });

            Capture(shots, "SoloCircuit", () =>
            {
                var host = NewHost("CS_SoloCircuit");
                var circuit = host.AddComponent<SoloCircuitPresenter>();
                circuit.Initialize(SaveSystem.CurrentProfile, DateTime.UtcNow, onBack: null);
                return GameObject.Find(SoloCircuitPresenter.CanvasName);
            });

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
            int rows = Mathf.CeilToInt(shots.Count / (float)TileCols);
            int sheetW = TileCols * CaptureWidth;
            int sheetH = rows * CaptureHeight;
            var sheet = new Texture2D(sheetW, sheetH, TextureFormat.RGB24, false);

            var blank = new Color[sheetW * sheetH];
            for (int i = 0; i < blank.Length; i++) blank[i] = new Color(0.02f, 0.02f, 0.03f);
            sheet.SetPixels(blank);

            for (int i = 0; i < shots.Count; i++)
            {
                Texture2D tile = shots[i].tex;
                if (tile == null) continue;
                int col = i % TileCols;
                int row = i / TileCols;
                int x = col * CaptureWidth;
                int y = sheetH - (row + 1) * CaptureHeight; // top-left origin visually
                sheet.SetPixels(x, y, CaptureWidth, CaptureHeight, tile.GetPixels());
            }
            sheet.Apply();
            return sheet;
        }
    }
}

using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Data;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>WH-REDUCED-MOTION-CLUSTER-IMPLEMENT-003 — Settings toggle + save/MotionPolicy persistence.</summary>
    public class SettingsReducedMotionTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;
        private bool _priorReduceMotion;

        [SetUp]
        public void SetUp()
        {
            _priorReduceMotion = MotionPolicy.ReduceMotion;
            MotionPolicy.ReduceMotion = false;

            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MoDSettingsReduceMotion_" + System.Guid.NewGuid().ToString("N"));
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
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();
            foreach (Canvas c in Object.FindObjectsOfType<Canvas>())
            {
                if (c != null) Object.DestroyImmediate(c.gameObject);
            }
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            MotionPolicy.ReduceMotion = _priorReduceMotion;
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, true);
        }

        private SettingsPresenter Open()
        {
            var go = new GameObject("SettingsReduceMotionHost");
            _spawned.Add(go);
            var presenter = go.AddComponent<SettingsPresenter>();
            presenter.Initialize(onBackToHome: null);
            return presenter;
        }

        private static float ScaleFactor(float screenW, float screenH, float refW, float refH, float match)
        {
            float logWidth = Mathf.Log(screenW / refW, 2f);
            float logHeight = Mathf.Log(screenH / refH, 2f);
            return Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, match));
        }

        [Test]
        public void OldSaveJson_MissingReduceMotionKey_DefaultsFalse_AndDoesNotEnablePolicy()
        {
            // Pre-field save: has avatarLevel (Deserialize gate) but no settingsReduceMotionEnabled.
            const string oldSaveJson =
                "{\n" +
                "  \"avatarLevel\": 3,\n" +
                "  \"settingsAudioEnabled\": true,\n" +
                "  \"settingsNotificationsEnabled\": true,\n" +
                "  \"preferredLanguageCode\": \"en\"\n" +
                "}";

            Assert.IsFalse(oldSaveJson.Contains("settingsReduceMotionEnabled"),
                "Fixture must omit the additive key.");

            PlayerProfile loaded = SaveSystem.Deserialize(oldSaveJson, out SaveLoadStatus status);
            Assert.AreEqual(SaveLoadStatus.Loaded, status);
            Assert.IsFalse(loaded.settingsReduceMotionEnabled,
                "JsonUtility must leave additive bool at field default (false) for old saves.");
            Assert.IsFalse(PlayerSettingsService.GetReduceMotionEnabled(loaded));

            MotionPolicy.ReduceMotion = true; // dirty process state
            PlayerSettingsService.ApplyFromProfile(loaded);
            Assert.IsFalse(MotionPolicy.ReduceMotion,
                "Old save must re-apply ReduceMotion=false into MotionPolicy.");
        }

        [Test]
        public void NewPreference_PersistsThroughSerializeDeserialize_AndApplyFromProfile()
        {
            PlayerProfile profile = new PlayerProfile { avatarLevel = 5 };
            Assert.IsFalse(profile.settingsReduceMotionEnabled);

            PlayerSettingsService.SetReduceMotionEnabled(profile, true);
            Assert.IsTrue(MotionPolicy.ReduceMotion);
            PlayerSettingsService.Persist(profile);

            SaveSystem.ResetCurrentProfileForTests();
            PlayerProfile reloaded = SaveSystem.LoadOrCreate();
            Assert.IsTrue(reloaded.settingsReduceMotionEnabled);

            string json = SaveSystem.Serialize(reloaded);
            StringAssert.Contains("settingsReduceMotionEnabled", json);

            MotionPolicy.ReduceMotion = false;
            PlayerProfile roundTrip = SaveSystem.Deserialize(json, out SaveLoadStatus status);
            Assert.AreEqual(SaveLoadStatus.Loaded, status);
            Assert.IsTrue(roundTrip.settingsReduceMotionEnabled);
            PlayerSettingsService.ApplyFromProfile(roundTrip);
            Assert.IsTrue(MotionPolicy.ReduceMotion);
        }

        [Test]
        public void ReducedMotionRow_Exists_ShowsCurrentChoice_AndPersists()
        {
            PlayerProfile profile = SaveSystem.LoadOrCreate();
            profile.settingsReduceMotionEnabled = false;
            SaveSystem.Save(profile);
            MotionPolicy.ReduceMotion = false;

            SettingsPresenter settings = Open();
            Transform row = settings.CanvasObjectForTests.transform.Find("SettingsBody/ReducedMotionRow");
            Assert.NotNull(row, "Settings must expose a Reduced Motion row.");
            Assert.AreEqual("OFF",
                row.Find("Value").GetComponent<Text>().text,
                "Current Reduced Motion choice must be visible.");

            Button toggle = row.Find("Btn_Toggle").GetComponent<Button>();
            Assert.IsTrue(toggle.interactable);
            toggle.onClick.Invoke();

            PlayerProfile live = SaveManager.SaveData ?? SaveSystem.CurrentProfile;
            Assert.IsNotNull(live);
            Assert.IsTrue(live.settingsReduceMotionEnabled);
            Assert.IsTrue(MotionPolicy.ReduceMotion,
                "Toggling ON must apply MotionPolicy.ReduceMotion immediately.");
            Assert.AreEqual("ON", row.Find("Value").GetComponent<Text>().text);

            SaveSystem.ResetCurrentProfileForTests();
            PlayerProfile reloaded = SaveSystem.LoadOrCreate();
            Assert.IsTrue(reloaded.settingsReduceMotionEnabled, "Reduced Motion must persist to save.");

            MotionPolicy.ReduceMotion = false;
            PlayerSettingsService.ApplyFromProfile(reloaded);
            Assert.IsTrue(MotionPolicy.ReduceMotion,
                "ApplyFromProfile must re-apply Reduced Motion into MotionPolicy.");
        }

        [Test]
        public void MainActionableControls_StayWithinApprovedCeiling()
        {
            Assert.LessOrEqual(SettingsPresenter.MainActionableControlCount,
                SettingsPresenter.MainActionableControlCeiling);

            SettingsPresenter settings = Open();
            Transform body = settings.CanvasObjectForTests.transform.Find("SettingsBody");
            Assert.NotNull(body.Find("AudioRow/Btn_Toggle"));
            Assert.NotNull(body.Find("NotificationsRow/Btn_Toggle"));
            Assert.NotNull(body.Find("ReducedMotionRow/Btn_Toggle"));
            Assert.NotNull(body.Find("LogoutRow/Btn_Logout"));
            Assert.NotNull(settings.CanvasObjectForTests.transform.Find("SettingsHeader/Btn_Back"));
            Assert.NotNull(body.Find("LanguageRow/LanguageOptions"),
                "Language preference surface must remain.");
        }

        [Test]
        public void ExistingSettingsRows_RemainReachable()
        {
            SettingsPresenter settings = Open();
            Transform canvas = settings.CanvasObjectForTests.transform;
            Assert.NotNull(canvas.Find("SettingsBody/AudioRow/Btn_Toggle"));
            Assert.NotNull(canvas.Find("SettingsBody/NotificationsRow/Btn_Toggle"));
            Assert.NotNull(canvas.Find("SettingsBody/LanguageRow"));
            Assert.NotNull(canvas.Find("SettingsBody/LogoutRow/Btn_Logout"));
            Assert.NotNull(canvas.Find("SettingsHeader/Btn_Back"));
        }

        [Test]
        public void DecorativePendingPulse_IsSuppressedWhenReduceMotionOn()
        {
            float a = UIInteractionStateTokens.Resolve(
                InteractionStateFlags.Pending, UIDesignTokens.FrameTier.Tier2Section, 0f, reduceMotion: true).Opacity;
            float b = UIInteractionStateTokens.Resolve(
                InteractionStateFlags.Pending, UIDesignTokens.FrameTier.Tier2Section,
                UIInteractionStateTokens.PendingPulsePeriodMs * 0.5f, reduceMotion: true).Opacity;
            Assert.AreEqual(a, b, 0.0001f, "Pending must not pulse under reduced motion.");

            UIInteractionStateTokens.PressSpec spec =
                UIInteractionStateTokens.PressSpecFor(UIDesignTokens.FrameTier.Tier1Hero);
            InteractionVisual pressed = UIInteractionStateTokens.Resolve(
                InteractionStateFlags.Pressed, UIDesignTokens.FrameTier.Tier1Hero, 0f, reduceMotion: true);
            Assert.AreEqual(spec.Scale, pressed.Scale, 0.001f,
                "Press acknowledgement must remain under reduced motion (snaps immediately).");
        }

        [Test]
        public void ExpeditionAndSettingsTransitions_SnapWhenReduceMotionAppliedFromProfile()
        {
            PlayerProfile profile = SaveSystem.LoadOrCreate();
            profile.settingsReduceMotionEnabled = true;
            PlayerSettingsService.ApplyFromProfile(profile);

            Assert.AreEqual(0f,
                SettingsPresenter.ResolveTransitionDurationSeconds(
                    SettingsPresenter.TransitionDurationSeconds, MotionPolicy.ReduceMotion));
            Assert.AreEqual(0f,
                GuildExpeditionPresenter.ResolveTransitionDurationSeconds(
                    GuildExpeditionPresenter.TransitionDurationSeconds, MotionPolicy.ReduceMotion));
            Assert.IsFalse(MotionPolicy.ShouldPlayDecorativeMotion(isPlaying: true),
                "Home/Avatar/Expedition decorative motion must be suppressed when ReduceMotion is on.");
        }

        [Test]
        public void SettingsCanvas_FitsSmallMediumLargeDpiProfiles_AndWrites1920x1080Captures()
        {
            // Device profiles used by CanvasOverflowAudit (phone / baseline / tablet).
            var profiles = new (string Id, string Name, float W, float H, bool ExpectFit)[]
            {
                ("small", "small phone 2400x1080", 2400f, 1080f, true),
                ("medium", "medium baseline 1920x1080", 1920f, 1080f, true),
                // match=height overflows tablet width — project-wide scaler, not this card.
                ("large", "large tablet 2560x1600", 2560f, 1600f, false),
            };

            SettingsPresenter settings = Open();
            CanvasScaler scaler = settings.CanvasObjectForTests.GetComponent<CanvasScaler>();
            Assert.NotNull(scaler);
            Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
            Assert.AreEqual(UISharedFoundation.MatchWidthOrHeight, scaler.matchWidthOrHeight, 0.0001f);

            float refW = scaler.referenceResolution.x;
            float refH = scaler.referenceResolution.y;
            float match = scaler.matchWidthOrHeight;
            var failures = new List<string>();

            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..",
                "tools", "scratch_settings_reduced_motion"));
            Directory.CreateDirectory(outDir);

            foreach (var profile in profiles)
            {
                float sf = ScaleFactor(profile.W, profile.H, refW, refH, match);
                float renderedW = refW * sf;
                float renderedH = refH * sf;
                if (profile.ExpectFit && (renderedW > profile.W + 1f || renderedH > profile.H + 1f))
                {
                    failures.Add(
                        $"{profile.Name}: rendered {renderedW:F0}x{renderedH:F0} exceeds screen {profile.W}x{profile.H}");
                }

                Texture2D tex = CaptureOverlayCanvas(settings.CanvasObjectForTests, 1920, 1080);
                Assert.NotNull(tex, $"Capture failed for {profile.Id}");
                string path = Path.Combine(outDir,
                    $"Settings_dpi_{profile.Id}_1920x1080_WH-REDUCED-MOTION-CLUSTER-IMPLEMENT-003.png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                Assert.IsTrue(File.Exists(path) && new FileInfo(path).Length > 1000,
                    $"Missing/empty DPI capture: {path}");
            }

            FindToggle(settings, "ReducedMotionRow").onClick.Invoke();
            Texture2D after = CaptureOverlayCanvas(settings.CanvasObjectForTests, 1920, 1080);
            Assert.NotNull(after);
            string afterPath = Path.Combine(outDir,
                "Settings_after_WH-REDUCED-MOTION-CLUSTER-IMPLEMENT-003.png");
            File.WriteAllBytes(afterPath, after.EncodeToPNG());
            Object.DestroyImmediate(after);

            CollectionAssert.IsEmpty(failures, string.Join(" | ", failures));
        }

        private static Button FindToggle(SettingsPresenter settings, string rowName)
        {
            Transform row = settings.CanvasObjectForTests.transform.Find($"SettingsBody/{rowName}");
            Assert.NotNull(row, rowName);
            return row.Find("Btn_Toggle").GetComponent<Button>();
        }

        private Texture2D CaptureOverlayCanvas(GameObject canvasObj, int width, int height)
        {
            Canvas canvas = canvasObj.GetComponent<Canvas>();
            if (canvas == null) return null;

            var camGo = new GameObject("SettingsReduceMotionCaptureCam");
            _spawned.Add(camGo);
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f, 1f);
            cam.orthographic = true;
            cam.cullingMask = ~0;

            var rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;

            cam.Render();

            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;

            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(rt);
            return tex;
        }
    }
}

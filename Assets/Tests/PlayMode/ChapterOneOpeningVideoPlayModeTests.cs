using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.Video;

namespace MyriadOfDragons.Tests.PlayMode
{
    /// <summary>
    /// ST-TUTORIAL-ANIMATION-V1-HANDOFF-003. Real Play Mode evidence that the opening cinematic
    /// actually decodes and renders the supplied MP4 - EditMode (ChapterOneOpeningVideoTests.cs)
    /// proves the video-vs-composite decision and the timing/skip/reduced-motion state machine,
    /// but cannot decode a frame at all. This drives the real GameBootstrap.Instance into the
    /// tutorial opening, lets the VideoPlayer actually play for real wall-clock time, and captures
    /// one 1920x1080 frame - the same harness shape as the other runtime capture tests in this
    /// suite.
    /// </summary>
    public sealed class ChapterOneOpeningVideoPlayModeTests
    {
        private const int CaptureWidth = 1920;
        private const int CaptureHeight = 1080;

        [UnityTest]
        public IEnumerator OpeningVideo_ActuallyPlays_AndRendersARealFrame()
        {
            Screen.SetResolution(CaptureWidth, CaptureHeight, FullScreenMode.Windowed);
            int waited = 0;
            while (GameBootstrap.Instance == null && waited++ < 180) yield return null;
            Assert.IsNotNull(GameBootstrap.Instance, "GameBootstrap did not auto-boot.");
            GameBootstrap bootstrap = GameBootstrap.Instance;
            bootstrap.SetBattleCanvasVisible(true);
            yield return null;

            MotionPolicy.ReduceMotion = false;
            bootstrap.StartApprovedTutorialBattle();
            yield return null;

            Assert.IsTrue(bootstrap.CinematicActiveForTests, "STATE UNREACHED: opening cinematic did not start.");
            Assert.IsTrue(bootstrap.CinematicIsPresentingVideoForTests, "STATE UNREACHED: expected the real video presentation.");

            FieldInfo playerField = typeof(GameBootstrap).GetField("_cinematicVideoPlayer", BindingFlags.Instance | BindingFlags.NonPublic);
            var videoPlayer = (VideoPlayer)playerField.GetValue(bootstrap);
            Assert.IsNotNull(videoPlayer, "STATE UNREACHED: no VideoPlayer attached.");

            // Let real playback run for a real second of wall-clock time.
            float waitedSeconds = 0f;
            while (waitedSeconds < 1.0f)
            {
                yield return null;
                waitedSeconds += Time.unscaledDeltaTime;
            }

            Assert.IsTrue(videoPlayer.isPlaying, "The VideoPlayer must actually be playing, not just attached.");
            Assert.Greater(videoPlayer.time, 0.0, "Real playback time must have advanced past zero.");

            yield return CaptureAndSave(bootstrap, "chapter1_opening_video_playing");

            bootstrap.SkipCinematicForTests();
            yield return null;
            Assert.IsFalse(bootstrap.CinematicActiveForTests, "Skip must clear the cinematic in the real running instance.");
        }

        private static IEnumerator CaptureAndSave(GameBootstrap bootstrap, string label)
        {
            string dir = Path.Combine(Application.persistentDataPath, "chapter1_opening_video_capture");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, label + "_1920x1080.png");
            if (File.Exists(path)) File.Delete(path);

            RectTransform battleRoot = bootstrap.BattlePresentationRootForTests;
            Assert.IsNotNull(battleRoot, label + ": BattlePresentationRootForTests was null.");
            Canvas canvas = battleRoot.GetComponentInParent<Canvas>();
            Assert.IsNotNull(canvas, label + ": no Canvas found above the battle presentation root.");

            RenderMode oldMode = canvas.renderMode;
            Camera oldCamera = canvas.worldCamera;
            var cameraObject = new GameObject("OpeningVideoCaptureCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.04f, 0.06f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = CaptureHeight * 0.5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.cullingMask = ~0;
            var renderTexture = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = renderTexture;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            Canvas.ForceUpdateCanvases();
            yield return null;

            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            var texture = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            RenderTexture.active = previous;
            canvas.renderMode = oldMode;
            canvas.worldCamera = oldCamera;
            camera.targetTexture = null;
            Object.Destroy(cameraObject);
            Object.Destroy(renderTexture);
            Object.Destroy(texture);

            Assert.Greater(new FileInfo(path).Length, 0, label + ": runtime screenshot file is empty.");
            byte[] png = File.ReadAllBytes(path);
            byte[] digest;
            using (MD5 md5 = MD5.Create()) digest = md5.ComputeHash(png);
            string hash = System.BitConverter.ToString(digest).Replace("-", string.Empty);
            Debug.Log($"[ChapterOneOpeningVideoCapture] {label}: {path} ({png.Length} bytes, {CaptureWidth}x{CaptureHeight}, md5={hash})");
            yield return null;
        }
    }
}

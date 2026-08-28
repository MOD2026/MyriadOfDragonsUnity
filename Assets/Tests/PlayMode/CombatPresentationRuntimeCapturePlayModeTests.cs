using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using MyriadOfDragons.Battle;
using MyriadOfDragons.UI;

namespace MyriadOfDragons.Tests.PlayMode
{
    /// <summary>Runtime evidence harness for the beta presentation pass. It drives the real
    /// GameBootstrap hooks and captures the loaded profile at the locked landscape resolution;
    /// it does not assert or alter combat outcomes.</summary>
    public sealed class CombatPresentationRuntimeCapturePlayModeTests
    {
        private const int CaptureWidth = 1920;
        private const int CaptureHeight = 1080;

        [UnityTest]
        public IEnumerator CaptureCardPlayDamageHealAndSpellImpact()
        {
            Screen.SetResolution(CaptureWidth, CaptureHeight, FullScreenMode.Windowed);
            int waited = 0;
            while (GameBootstrap.Instance == null && waited++ < 180) yield return null;
            Assert.IsNotNull(GameBootstrap.Instance, "GameBootstrap did not auto-boot.");
            GameBootstrap bootstrap = GameBootstrap.Instance;

            yield return null;
            yield return CaptureAndSave(bootstrap, "card_play");

            bootstrap.UseRecommendedLineupForTests();
            yield return null;
            yield return CaptureAndSave(bootstrap, "card_play");

            bootstrap.EndTurnForTests();
            yield return null;
            yield return CaptureAndSave(bootstrap, "damage_heal");

            // The authoritative spell list is read from the live controller. Reflection is
            // intentionally limited to invoking the existing private presentation hook; no
            // combat state or gateway call is changed by this harness.
            var controllerField = typeof(GameBootstrap).GetField("_battleController",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            BattleController controller = (BattleController)controllerField.GetValue(bootstrap);
            if (controller.Spellbook.Count > 0)
            {
                var method = typeof(GameBootstrap).GetMethod("PlayCastImpact",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                method.Invoke(bootstrap, new object[] { controller.Spellbook.First(), Lane.Front });
                yield return null;
                yield return CaptureAndSave(bootstrap, "spell_impact");
            }
            else
            {
                Assert.Fail("Loaded profile has no spell for the spell-impact capture.");
            }
        }

        private static IEnumerator CaptureAndSave(GameBootstrap bootstrap, string label)
        {
            string dir = Path.Combine(Application.persistentDataPath, "bs_animation_beta");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, label + "_1920x1080.png");
            if (File.Exists(path)) File.Delete(path);
            Canvas canvas = bootstrap.GetComponentInChildren<Canvas>(true);
            Assert.IsNotNull(canvas, "Battle canvas was not found.");
            RenderMode oldMode = canvas.renderMode;
            Camera oldCamera = canvas.worldCamera;
            GameObject cameraObject = new GameObject("CombatPresentationCaptureCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.04f, 0.06f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = CaptureHeight * 0.5f;
            RenderTexture renderTexture = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = renderTexture;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            Texture2D texture = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGBA32, false);
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
            Assert.Greater(new FileInfo(path).Length, 0, "Runtime screenshot file is empty.");
            Debug.Log($"[CombatPresentationCapture] {path} ({new FileInfo(path).Length} bytes)");
            yield return null;
        }
    }
}

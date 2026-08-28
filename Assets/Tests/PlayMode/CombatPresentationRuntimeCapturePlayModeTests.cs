using System.Collections;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
        private static readonly System.Collections.Generic.Dictionary<string, string> CaptureHashes =
            new System.Collections.Generic.Dictionary<string, string>();

        [UnityTest]
        public IEnumerator CaptureCardPlayDamageHealAndSpellImpact()
        {
            CaptureHashes.Clear();
            Screen.SetResolution(CaptureWidth, CaptureHeight, FullScreenMode.Windowed);
            int waited = 0;
            while (GameBootstrap.Instance == null && waited++ < 180) yield return null;
            Assert.IsNotNull(GameBootstrap.Instance, "GameBootstrap did not auto-boot.");
            GameBootstrap bootstrap = GameBootstrap.Instance;

            bootstrap.SetBattleCanvasVisible(true);
            yield return null;
            yield return CaptureAndSave(bootstrap, "card_play");

            bootstrap.EndTurnForTests();
            yield return null;
            // EndTurnForTests advances the authoritative tick and refreshes state. Replay the
            // same result through the existing presentation hook so this capture contains the
            // real damage feedback without resolving combat a second time.
            var controllerField = typeof(GameBootstrap).GetField("_battleController",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            BattleController controller = (BattleController)controllerField.GetValue(bootstrap);
            var result = controller.AdvanceCombatTick();
            var damageMethod = typeof(GameBootstrap).GetMethod("ShowTurnDamage",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            damageMethod.Invoke(bootstrap, new object[] { result });
            yield return null;
            yield return CaptureAndSave(bootstrap, "damage_heal");

            // The authoritative spell list is read from the live controller. Reflection is
            // intentionally limited to invoking the existing private presentation hook; no
            // combat state or gateway call is changed by this harness.
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

            Assert.AreEqual(3, CaptureHashes.Count, "Expected exactly three named captures.");
            Assert.AreEqual(3, CaptureHashes.Values.Distinct().Count(),
                "Card-play, damage/heal, and spell-impact captures must have distinct content hashes.");
        }

        private static IEnumerator CaptureAndSave(GameBootstrap bootstrap, string label)
        {
            string dir = Path.Combine(Application.persistentDataPath, "bs_animation_beta");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, label + "_1920x1080.png");
            if (File.Exists(path)) File.Delete(path);
            RectTransform battleRoot = bootstrap.BattlePresentationRootForTests;
            Assert.IsNotNull(battleRoot, "Battle presentation root was not found.");
            var canvasField = typeof(GameBootstrap).GetField("_canvasTransform",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(canvasField, "GameBootstrap battle canvas field was not found.");
            Transform canvasTransform = (Transform)canvasField.GetValue(bootstrap);
            Assert.IsNotNull(canvasTransform, "GameBootstrap battle canvas transform was not initialized.");
            Canvas canvas = canvasTransform.GetComponent<Canvas>();
            Assert.IsNotNull(canvas, "GameBootstrap battle canvas component was not found.");
            Assert.AreEqual("Canvas", canvas.gameObject.name, "Unexpected canvas selected for battle capture.");
            Assert.IsTrue(battleRoot.IsChildOf(canvas.transform), "Battle root is not owned by the battle canvas.");
            RenderMode oldMode = canvas.renderMode;
            Camera oldCamera = canvas.worldCamera;
            GameObject cameraObject = new GameObject("CombatPresentationCaptureCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.04f, 0.06f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = CaptureHeight * 0.5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            RenderTexture renderTexture = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = renderTexture;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            camera.cullingMask = ~0;
            Canvas.ForceUpdateCanvases();
            yield return null;
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
            UnityEngine.Object.Destroy(cameraObject);
            UnityEngine.Object.Destroy(renderTexture);
            UnityEngine.Object.Destroy(texture);
            Assert.Greater(new FileInfo(path).Length, 0, "Runtime screenshot file is empty.");
            byte[] png = File.ReadAllBytes(path);
            byte[] digest;
            using (MD5 md5 = MD5.Create()) digest = md5.ComputeHash(png);
            string hash = BitConverter.ToString(digest).Replace("-", string.Empty);
            CaptureHashes[label] = hash;
            Debug.Log($"[CombatPresentationCapture] {path} ({png.Length} bytes, {CaptureWidth}x{CaptureHeight}, md5={hash})");
            yield return null;
        }
    }
}

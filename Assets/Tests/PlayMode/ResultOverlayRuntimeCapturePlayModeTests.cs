using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MyriadOfDragons.AI;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests.PlayMode
{
    /// <summary>
    /// CR-RESULT-OVERLAY-CAPTURE-HARNESS-006. Closes the runtime-rendering evidence gap the
    /// EditMode ResultOverlayOutcomeCoverageTests.cs (accepted, CR-RESULT-CTA-TESTS-001/002)
    /// cannot close on its own - that file proves the CTA/outcome/teardown CONTRACT, but never
    /// renders a real frame, so text wrapping, real overlap, and clipping were never actually
    /// observed. This drives the real GameBootstrap.Instance through all four reachable result
    /// states (normal Victory/Defeat, tutorial Victory/Defeat) and captures each at the locked
    /// 1920x1080 landscape reference, plus proves Retry/Return-to-Home teardown against the real
    /// running instance rather than just its state flags.
    ///
    /// Zero reflection: GameBootstrap already exposes every seam this fixture needs (Battle,
    /// UseRecommendedLineupForTests, EndTurnForTests, StartApprovedTutorialBattle, RetryForTests,
    /// ReturnToCityForTests, ResultOverlayActiveForTests, ResultTextForTests, PlayAgainButton-
    /// ActiveForTests, ReturnToCityButtonActiveForTests, BattleCanvasVisibleForTests). The battle
    /// canvas itself is located by its own real, literal GameObject name ("Canvas") rather than
    /// the private _canvasTransform field the older CombatPresentationRuntimeCapturePlayMode-
    /// Tests.cs harness had to reflect before that exact seam existed in this form.
    ///
    /// UseRecommendedLineupForTests() starts a match via StartNewMatch(allowSavedDeck: false), so
    /// unlike the EditMode suite this fixture needs no SaveSystem override or pre-seeded deck -
    /// GameBootstrap.Instance's own auto-boot state is sufficient.
    /// </summary>
    public sealed class ResultOverlayRuntimeCapturePlayModeTests
    {
        private const int CaptureWidth = 1920;
        private const int CaptureHeight = 1080;
        private static readonly Dictionary<string, string> CaptureHashes = new Dictionary<string, string>();

        [UnityTest]
        public IEnumerator CaptureAllReachableResultStates_AndProveRetryReturnTeardown()
        {
            CaptureHashes.Clear();
            Screen.SetResolution(CaptureWidth, CaptureHeight, FullScreenMode.Windowed);
            int waited = 0;
            while (GameBootstrap.Instance == null && waited++ < 180) yield return null;
            Assert.IsNotNull(GameBootstrap.Instance, "GameBootstrap did not auto-boot.");
            GameBootstrap bootstrap = GameBootstrap.Instance;
            bootstrap.SetBattleCanvasVisible(true);
            yield return null;

            // ---------- 1. Normal Victory ----------
            // ResetLineupForTests (fresh match, empty board, no save-file dependency) +
            // AutoFormationForTests (basic one-card-per-lane placement) rather than
            // UseRecommendedLineupForTests, whose "strongest affordable squad" formation biases
            // the outcome regardless of forced HP (real runs 1-2 both resolved Victory even while
            // forcing PlayerState.AvatarHealth=1 for a Defeat, because combat is simultaneous -
            // MaxAvatarHealth is readonly so the winning side's AvatarHealth is instead forced far
            // above anything the tick cap could realistically deal, making only one outcome
            // possible regardless of relative formation strength).
            bootstrap.ResetLineupForTests();
            yield return null;
            bootstrap.AutoFormationForTests();
            yield return null;
            yield return ResolveMatch(bootstrap, () =>
            {
                bootstrap.Battle.PlayerState.AvatarHealth = 999999;
                bootstrap.Battle.EnemyState.AvatarHealth = 1;
            });
            Assert.IsTrue(bootstrap.ResultOverlayActiveForTests, "STATE UNREACHED: normal Victory produced no result overlay.");
            Assert.IsTrue(bootstrap.PlayAgainButtonActiveForTests, "Normal Victory must offer Play Again.");
            Assert.IsTrue(bootstrap.ReturnToCityButtonActiveForTests, "Normal Victory must offer Return to City.");
            StringAssert.Contains("VICTORY", bootstrap.ResultTextForTests);
            yield return CaptureAndValidate(bootstrap, "result_victory_normal");

            // ---------- 2. Retry rendered-state proof (no new capture - state assertion only) ----------
            bootstrap.RetryForTests();
            yield return null;
            Assert.IsFalse(bootstrap.ResultOverlayActiveForTests, "Retry must hide the result overlay in the real running instance.");
            Assert.AreNotEqual(BattlePhase.Resolved, bootstrap.Battle.Phase, "Retry must leave Resolved for a fresh match.");
            Debug.Log("[ResultOverlayCapture] Retry (normal): overlay hidden, phase=" + bootstrap.Battle.Phase);

            // ---------- 3. Normal Defeat ----------
            bootstrap.ResetLineupForTests();
            yield return null;
            bootstrap.AutoFormationForTests();
            yield return null;
            yield return ResolveMatch(bootstrap, () =>
            {
                bootstrap.Battle.EnemyState.AvatarHealth = 999999;
                bootstrap.Battle.PlayerState.AvatarHealth = 1;
            });
            Assert.IsTrue(bootstrap.ResultOverlayActiveForTests, "STATE UNREACHED: normal Defeat produced no result overlay.");
            Assert.IsTrue(bootstrap.PlayAgainButtonActiveForTests, "Normal Defeat must offer Play Again.");
            Assert.IsTrue(bootstrap.ReturnToCityButtonActiveForTests, "Normal Defeat must offer Return to City.");
            StringAssert.Contains("DEFEAT", bootstrap.ResultTextForTests);
            yield return CaptureAndValidate(bootstrap, "result_defeat_normal");

            // ---------- 4. Tutorial Victory ----------
            bootstrap.StartApprovedTutorialBattle(showOpeningCinematic: false);
            yield return null;
            PlaceApprovedTutorialFormation(bootstrap.Battle);
            yield return ResolveMatch(bootstrap, () =>
            {
                bootstrap.Battle.PlayerState.AvatarHealth = 999999;
                bootstrap.Battle.EnemyState.AvatarHealth = 1;
            });
            Assert.IsTrue(bootstrap.ResultOverlayActiveForTests, "STATE UNREACHED: tutorial Victory produced no result overlay.");
            Assert.IsTrue(bootstrap.ReturnToCityButtonActiveForTests, "Tutorial Victory must offer Return to Empire.");
            Assert.IsFalse(bootstrap.PlayAgainButtonActiveForTests, "Tutorial Victory must not also offer Retry.");
            // HandleMatchEnded fires a real 5-second blocking BeginVictoryCinematic() for
            // IsTutorialMatch && playerWon (GameBootstrap.cs:6403) - real first run caught it
            // rendering ON TOP of the actual result overlay in the captured frame (assertions on
            // the real overlay hierarchy still passed underneath it, but the PNG itself showed the
            // cinematic, not the CTAs it claims to validate). SkipCinematicForTests() is the
            // existing seam for exactly this - safe no-op if nothing is active - so the capture
            // reflects what it actually claims.
            bootstrap.SkipCinematicForTests();
            yield return null;
            yield return CaptureAndValidate(bootstrap, "result_victory_tutorial");

            // ---------- 5. Tutorial Defeat ----------
            bootstrap.StartApprovedTutorialBattle(showOpeningCinematic: false);
            yield return null;
            PlaceApprovedTutorialFormation(bootstrap.Battle);
            yield return ResolveMatch(bootstrap, () =>
            {
                bootstrap.Battle.EnemyState.AvatarHealth = 999999;
                bootstrap.Battle.PlayerState.AvatarHealth = 1;
            });
            Assert.IsTrue(bootstrap.ResultOverlayActiveForTests, "STATE UNREACHED: tutorial Defeat produced no result overlay.");
            Assert.IsTrue(bootstrap.PlayAgainButtonActiveForTests, "Tutorial Defeat must offer Retry Battle.");
            Assert.IsFalse(bootstrap.ReturnToCityButtonActiveForTests, "Tutorial Defeat must not also offer Return to Empire.");
            yield return CaptureAndValidate(bootstrap, "result_defeat_tutorial");

            // ---------- 6. Return-to-Home rendered teardown proof (no new capture) ----------
            bootstrap.ReturnToCityForTests();
            yield return null;
            Assert.IsFalse(bootstrap.ResultOverlayActiveForTests, "Return to City must hide the result overlay in the real running instance.");
            Assert.IsFalse(bootstrap.BattleCanvasVisibleForTests, "Return to City must hide the real battle canvas.");
            Debug.Log("[ResultOverlayCapture] Return to City: overlay hidden=" + !bootstrap.ResultOverlayActiveForTests +
                       ", canvas visible=" + bootstrap.BattleCanvasVisibleForTests);

            Assert.AreEqual(4, CaptureHashes.Count, "Expected exactly four named result-state captures.");
            Assert.AreEqual(4, CaptureHashes.Values.Distinct().Count(),
                "All four result-state captures must have distinct content hashes (genuinely different rendered frames).");
        }

        /// <summary>The exact tutorial formation BattleLogicTests.GameBootstrap_-
        /// StartApprovedTutorialBattle_ResolvesWithinTheTickCap already proves resolves within
        /// the tick cap.</summary>
        private static void PlaceApprovedTutorialFormation(BattleController controller)
        {
            foreach (Card card in controller.PlayerState.Hand.ToList())
            {
                Lane lane = card.Id switch { "warrior" => Lane.Front, "novice_knight" => Lane.Middle, _ => Lane.Back };
                controller.TryPlayCard(controller.PlayerState, card, lane);
            }
            SimpleAIOpponent.TakeTurn(controller, AIArchetype.Balanced);
            Assert.IsTrue(controller.ConfirmFormation(), "Setup: the approved tutorial Formation must lock legally.");
        }

        /// <summary>Runs one real tick (via EndTurnForTests, which locks Formation -> Combat using
        /// the live match's own real AI archetype - not reachable as a public seam on its own, so
        /// this is the only way to reach Combat without reflecting the private _aiProfile field)
        /// BEFORE applying forceOutcome, not before - ConfirmFormation happens inside that first
        /// call, and forcing AvatarHealth ahead of it risked an unverified reset if formation-lock
        /// ever touches Avatar stats. Forcing after one real, unforced tick sidesteps that risk
        /// entirely: starting HP is always far above one tick's real damage, so Phase reliably
        /// stays Combat at this point, and the forced side dies deterministically on the next
        /// tick this method drives.</summary>
        private static IEnumerator ResolveMatch(GameBootstrap bootstrap, Action forceOutcome)
        {
            int ticksRun = 0;
            bootstrap.EndTurnForTests();
            yield return null;
            Assert.AreEqual(BattlePhase.Combat, bootstrap.Battle.Phase,
                "Setup: expected Combat after one real tick, before the outcome is forced.");

            forceOutcome();

            while (bootstrap.Battle.Phase == BattlePhase.Combat)
            {
                bootstrap.Battle.AdvanceCombatTick();
                ticksRun++;
                Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks,
                    "STATE UNREACHED: the forced outcome did not resolve within the existing combat tick cap.");
                yield return null;
            }
            Assert.AreEqual(BattlePhase.Resolved, bootstrap.Battle.Phase, "STATE UNREACHED: match did not reach Resolved.");
        }

        private static Rect WorldRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            float xMin = c.Min(v => v.x), xMax = c.Max(v => v.x);
            float yMin = c.Min(v => v.y), yMax = c.Max(v => v.y);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private static bool Contains(Rect outer, Rect inner, float tolerance) =>
            inner.xMin >= outer.xMin - tolerance && inner.xMax <= outer.xMax + tolerance &&
            inner.yMin >= outer.yMin - tolerance && inner.yMax <= outer.yMax + tolerance;

        private static IEnumerator CaptureAndValidate(GameBootstrap bootstrap, string label)
        {
            string dir = Path.Combine(Application.persistentDataPath, "result_overlay_capture");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, label + "_1920x1080.png");
            if (File.Exists(path)) File.Delete(path);

            // GameObject.Find("Canvas") is not reliably unique in the live PlayMode scene (Home's
            // own canvas can share that literal name) - real failure caught on first run, not
            // assumed. BattlePresentationRootForTests is the actual battle-owned seam: walking up
            // from it to its Canvas always resolves the correct one, no reflection needed.
            RectTransform battleRoot = bootstrap.BattlePresentationRootForTests;
            Assert.IsNotNull(battleRoot, label + ": BattlePresentationRootForTests was null.");
            Canvas canvas = battleRoot.GetComponentInParent<Canvas>();
            Assert.IsNotNull(canvas, label + ": no Canvas found above the battle presentation root.");
            GameObject canvasObj = canvas.gameObject;
            Assert.AreEqual("Canvas", canvasObj.name, label + ": unexpected canvas selected for capture.");
            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            Assert.IsNotNull(scaler, label + ": battle canvas must carry a CanvasScaler.");
            Assert.AreEqual(1920f, scaler.referenceResolution.x, 0.01f, label + ": locked landscape reference must be 1920x1080.");
            Assert.AreEqual(1080f, scaler.referenceResolution.y, 0.01f);

            // ResultOverlay is parented directly under battleRoot (BattlePresentationRootForTests),
            // not under the top-level Canvas - both BuildResultOverlay(battleRoot, ...) and
            // _battlePresentationRoot = battleRoot use the same variable at the call site
            // (GameBootstrap.cs:738/818). Confirmed by a real failed run against canvasObj.transform
            // first, not assumed.
            Transform resultOverlay = battleRoot.Find("ResultOverlay");
            Assert.IsNotNull(resultOverlay, label + ": ResultOverlay was not found under the battle presentation root.");
            Transform resultPanel = resultOverlay.Find("ResultPanel");
            Assert.IsNotNull(resultPanel, label + ": ResultPanel was not found under ResultOverlay.");

            RenderMode oldMode = canvas.renderMode;
            Camera oldCamera = canvas.worldCamera;
            var cameraObject = new GameObject("ResultOverlayCaptureCamera");
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

            // ---------- geometry validation, taken while the canvas is in real capture mode ----------
            var panelRect = (RectTransform)resultPanel;
            Rect panelWorld = WorldRect(panelRect);
            Assert.Greater(panelWorld.width, 0f, label + ": ResultPanel must have a nonzero width.");
            Assert.Greater(panelWorld.height, 0f, label + ": ResultPanel must have a nonzero height.");

            Text resultText = resultPanel.GetComponentsInChildren<Text>(true)
                .FirstOrDefault(t => t.text == bootstrap.ResultTextForTests);
            Assert.IsNotNull(resultText, label + ": could not locate the real result Text component by its own content.");
            Rect textWorld = WorldRect(resultText.rectTransform);
            Assert.IsTrue(Contains(panelWorld, textWorld, 1f),
                label + ": result text must render inside its own panel, not clipped outside it.");

            var activeButtonRects = new List<Rect>();
            foreach (Button button in resultPanel.GetComponentsInChildren<Button>(true))
            {
                if (!button.gameObject.activeSelf) continue; // tutorial single-button states hide the other CTA
                Rect buttonWorld = WorldRect((RectTransform)button.transform);
                Assert.Greater(buttonWorld.width, 0f, label + ": " + button.name + " must have a nonzero hit width.");
                Assert.Greater(buttonWorld.height, 0f, label + ": " + button.name + " must have a nonzero hit height.");
                Assert.IsTrue(Contains(panelWorld, buttonWorld, 1f),
                    label + ": " + button.name + " must render inside its own panel, not clipped outside it.");
                activeButtonRects.Add(buttonWorld);
            }
            Assert.GreaterOrEqual(activeButtonRects.Count, 1, label + ": expected at least one active CTA.");
            if (activeButtonRects.Count == 2)
            {
                Assert.IsFalse(activeButtonRects[0].Overlaps(activeButtonRects[1]),
                    label + ": the two active CTAs must not overlap each other.");
            }

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
            UnityEngine.Object.Destroy(cameraObject);
            UnityEngine.Object.Destroy(renderTexture);
            UnityEngine.Object.Destroy(texture);

            Assert.Greater(new FileInfo(path).Length, 0, label + ": runtime screenshot file is empty.");
            byte[] png = File.ReadAllBytes(path);
            byte[] digest;
            using (MD5 md5 = MD5.Create()) digest = md5.ComputeHash(png);
            string hash = BitConverter.ToString(digest).Replace("-", string.Empty);
            CaptureHashes[label] = hash;
            Debug.Log($"[ResultOverlayCapture] {label}: {path} ({png.Length} bytes, {CaptureWidth}x{CaptureHeight}, " +
                      $"md5={hash}, resultText=\"{bootstrap.ResultTextForTests}\", " +
                      $"playAgainActive={bootstrap.PlayAgainButtonActiveForTests}, " +
                      $"returnToCityActive={bootstrap.ReturnToCityButtonActiveForTests})");
            yield return null;
        }
    }
}

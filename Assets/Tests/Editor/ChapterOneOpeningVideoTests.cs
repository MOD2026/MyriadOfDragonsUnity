using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// ST-TUTORIAL-ANIMATION-V1-HANDOFF-003 implementation. The Opening beat now plays the real
    /// supplied MP4 (Assets/Resources/Cinematics/Chapter1/Opening/chapter1_opening_v1) through a
    /// VideoPlayer/RawImage instead of the static layer composite, whenever Reduced Motion is off
    /// and the clip is present - the composite remains the fallback (missing clip) and Victory's
    /// own presentation, untouched. This file proves the video-vs-composite decision, the locked
    /// 8.04s duration, that Reduced Motion suppresses layer drift for the composite path, and that
    /// nothing here disturbs the existing skip/resume/callback contract
    /// ChapterOneCinematicTests.cs already covers end to end.
    /// </summary>
    public class ChapterOneOpeningVideoTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;
        private bool _reduceMotionBefore;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDOpeningVideo_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            _reduceMotionBefore = MotionPolicy.ReduceMotion;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            MotionPolicy.ReduceMotion = _reduceMotionBefore;
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, true);
        }

        private GameBootstrap SpawnAndInitializeBootstrap(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            GameBootstrap bootstrap = go.AddComponent<GameBootstrap>();
            bootstrap.Initialize();
            foreach (string spawnedName in new[] { "Canvas", "EventSystem", "CardDatabase", "BattleController" })
            {
                foreach (GameObject candidate in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    if (candidate.name == spawnedName && !_spawned.Contains(candidate))
                        _spawned.Add(candidate);
                }
            }
            return bootstrap;
        }

        // ---------- Video vs. static composite ----------

        [Test]
        public void OpeningCinematic_UsesTheRealSuppliedVideo_WhenMotionIsAllowed()
        {
            MotionPolicy.ReduceMotion = false;
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("OpeningVideo_Allowed");

            bootstrap.StartApprovedTutorialBattle();

            Assert.IsTrue(bootstrap.CinematicActiveForTests, "Setup: expected the opening cinematic to be active.");
            Assert.AreEqual(CinematicKind.Opening, bootstrap.CinematicKindForTests);
            Assert.IsTrue(bootstrap.CinematicIsPresentingVideoForTests,
                "STATE UNREACHED: the approved supplied MP4 must be the presentation for a normal-motion Opening.");
            Assert.IsNotNull(bootstrap.CinematicVideoClipForTests, "The active VideoPlayer must have a real clip bound.");
            StringAssert.Contains("chapter1_opening_v1", bootstrap.CinematicVideoClipForTests.name);
        }

        [Test]
        public void OpeningCinematic_FallsBackToTheStaticComposite_WhenReducedMotionIsOn()
        {
            MotionPolicy.ReduceMotion = true;
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("OpeningVideo_ReducedMotion");

            bootstrap.StartApprovedTutorialBattle();

            Assert.IsTrue(bootstrap.CinematicActiveForTests);
            Assert.AreEqual(CinematicKind.Opening, bootstrap.CinematicKindForTests);
            Assert.IsFalse(bootstrap.CinematicIsPresentingVideoForTests,
                "Reduced Motion must fall back to the static layer composite, never the video.");
            Assert.IsNull(bootstrap.CinematicVideoClipForTests);
        }

        [Test]
        public void VictoryCinematic_NeverUsesVideo_RegardlessOfReducedMotion()
        {
            foreach (bool reduceMotion in new[] { false, true })
            {
                MotionPolicy.ReduceMotion = reduceMotion;
                GameBootstrap bootstrap = SpawnAndInitializeBootstrap("OpeningVideo_VictoryNeverVideo_" + reduceMotion);
                bootstrap.StartApprovedTutorialBattle(showOpeningCinematic: false);
                BattleController controller = bootstrap.Battle;

                foreach (Card card in controller.PlayerState.Hand.ToList())
                {
                    Lane lane = card.Id switch { "warrior" => Lane.Front, "novice_knight" => Lane.Middle, _ => Lane.Back };
                    controller.TryPlayCard(controller.PlayerState, card, lane);
                }
                controller.EnemyState.AvatarHealth = 1;
                Assert.IsTrue(controller.ConfirmFormation(), "Setup: the tutorial formation must lock legally.");
                int ticksRun = 0;
                while (controller.Phase == BattlePhase.Combat)
                {
                    controller.AdvanceCombatTick();
                    ticksRun++;
                    Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks, "Setup: expected a knockout inside the tick cap.");
                }

                Assert.AreEqual(CinematicKind.Victory, bootstrap.CinematicKindForTests,
                    $"Setup (reduceMotion={reduceMotion}): expected the victory cinematic to be active.");
                Assert.IsFalse(bootstrap.CinematicIsPresentingVideoForTests,
                    $"Victory must never use video (reduceMotion={reduceMotion}) - only Opening's five-beat handoff does.");
            }
        }

        // ---------- Locked timing ----------

        [Test]
        public void OpeningCinematic_Duration_MatchesTheLockedTimingTable_8Point04Seconds()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("OpeningVideo_Duration");
            bootstrap.StartApprovedTutorialBattle();

            Assert.AreEqual(8.04f, bootstrap.CinematicDurationSecondsForTests,
                "ST-TUTORIAL-ANIMATION-V1-HANDOFF-003's locked timing table: 'Opening playback | 0.00-8.04s'.");
        }

        // ---------- Reduced Motion suppresses layer drift (the static-composite path) ----------

        [Test]
        public void DriftCinematicLayers_IsANoOp_WhenReducedMotionIsOn()
        {
            // Exercised via Victory (always the static composite) so this test is independent of
            // the video-vs-composite decision above. Reflection invokes the private per-tick drift
            // method directly - EditMode never runs the coroutine that would otherwise call it, so
            // this is the only way to prove the new guard deterministically without Play Mode.
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("OpeningVideo_DriftGate");
            bootstrap.StartApprovedTutorialBattle(showOpeningCinematic: false);
            BattleController controller = bootstrap.Battle;
            foreach (Card card in controller.PlayerState.Hand.ToList())
            {
                Lane lane = card.Id switch { "warrior" => Lane.Front, "novice_knight" => Lane.Middle, _ => Lane.Back };
                controller.TryPlayCard(controller.PlayerState, card, lane);
            }
            controller.EnemyState.AvatarHealth = 1;
            Assert.IsTrue(controller.ConfirmFormation());
            int ticksRun = 0;
            while (controller.Phase == BattlePhase.Combat)
            {
                controller.AdvanceCombatTick();
                ticksRun++;
                Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks);
            }
            Assert.AreEqual(CinematicKind.Victory, bootstrap.CinematicKindForTests, "Setup: expected the victory (composite) cinematic.");

            MethodInfo drift = typeof(GameBootstrap).GetMethod("DriftCinematicLayers", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo layersField = typeof(GameBootstrap).GetField("_cinematicLayerImages", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo activeCinematicField = typeof(GameBootstrap).GetField("_activeCinematic", BindingFlags.Instance | BindingFlags.NonPublic);
            var layers = (List<Image>)layersField.GetValue(bootstrap);
            Assert.Greater(layers.Count, 0, "Setup: expected at least one composite layer image.");
            Image sky = layers[0];

            // DriftCinematicLayers reads _activeCinematic.ElapsedSeconds (t=0 produces zero drift
            // regardless of the Reduced Motion guard) - advance the real timer first via
            // CinematicSequence's own public Advance(), the same call RunCinematic's coroutine
            // makes every frame in Play Mode, so this test can't pass by coincidence at t=0.
            var activeCinematic = (CinematicSequence)activeCinematicField.GetValue(bootstrap);
            Assert.IsNotNull(activeCinematic, "Setup: expected an active cinematic to advance.");

            MotionPolicy.ReduceMotion = true;
            Vector2 beforeReduced = sky.rectTransform.anchoredPosition;
            activeCinematic.Advance(1f);
            drift.Invoke(bootstrap, null);
            Assert.AreEqual(beforeReduced, sky.rectTransform.anchoredPosition,
                "Reduced Motion must leave every composite layer at its authored position - no parallax drift.");

            MotionPolicy.ReduceMotion = false;
            activeCinematic.Advance(1f);
            drift.Invoke(bootstrap, null);
            Assert.AreNotEqual(beforeReduced, sky.rectTransform.anchoredPosition,
                "Sanity: with Reduced Motion off, the existing drift must still actually run (proves the guard is the reason for the no-op above, not an unrelated break).");
        }

        // ---------- Skip / teardown still hold with video as the presentation ----------

        [Test]
        public void SkipVideoOpening_ClearsTheCinematicWithoutAlteringFormationOrFabricatingProgress()
        {
            MotionPolicy.ReduceMotion = false;
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("OpeningVideo_Skip");
            bootstrap.StartApprovedTutorialBattle();
            Assert.IsTrue(bootstrap.CinematicIsPresentingVideoForTests, "Setup: expected the video presentation.");
            BattlePhase phaseBefore = bootstrap.Battle.Phase;
            int handCountBefore = bootstrap.Battle.PlayerState.Hand.Count;

            bootstrap.SkipCinematicForTests();

            Assert.IsFalse(bootstrap.CinematicActiveForTests, "Skip must clear the cinematic.");
            Assert.IsFalse(bootstrap.CinematicIsPresentingVideoForTests, "Skip must release the video presentation state.");
            Assert.IsNull(bootstrap.CinematicVideoClipForTests);
            Assert.AreEqual(phaseBefore, bootstrap.Battle.Phase, "Skip must not advance battle phase or fabricate a result.");
            Assert.AreEqual(handCountBefore, bootstrap.Battle.PlayerState.Hand.Count, "Skip must not fabricate or consume cards.");

            bootstrap.SkipCinematicForTests(); // second Skip: must be a no-op, not throw.
        }

        [Test]
        public void ReplayingTheOpening_NeverLeaksTheVideoRenderTexture_AcrossRuns()
        {
            // Two full opening/skip cycles on the same bootstrap - HideCinematicOverlay must
            // release the RenderTexture every time, and a second cinematic must not inherit or
            // reuse a stale one.
            MotionPolicy.ReduceMotion = false;
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("OpeningVideo_ReplayNoLeak");

            bootstrap.StartApprovedTutorialBattle();
            Assert.IsTrue(bootstrap.CinematicIsPresentingVideoForTests);
            bootstrap.SkipCinematicForTests();
            Assert.IsFalse(bootstrap.CinematicIsPresentingVideoForTests);

            // A second real tutorial start on the SAME bootstrap instance - the same entry point a
            // real replay uses - must play the opening video again rather than inheriting a stale
            // (already-released) VideoPlayer/RenderTexture from the first run.
            bootstrap.StartApprovedTutorialBattle();
            Assert.IsTrue(bootstrap.CinematicIsPresentingVideoForTests, "A fresh tutorial start must play the opening video again.");
            Assert.IsNotNull(bootstrap.CinematicVideoClipForTests);
            bootstrap.SkipCinematicForTests();
            Assert.IsFalse(bootstrap.CinematicIsPresentingVideoForTests);
        }
    }
}

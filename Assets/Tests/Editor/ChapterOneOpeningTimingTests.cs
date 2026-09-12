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
    /// ST-TUTORIAL-ANIMATION-V1-HANDOFF-003, corrected direction: the existing static layer
    /// composite/parallax path stays the Opening presentation (no VideoPlayer, no new gameplay
    /// system). This file proves the two things that direction specifically asked for: the locked
    /// duration is centralized and cannot silently drift from the approved supplied source, and
    /// Reduced Motion turns the composite's parallax/drift into a genuine static hold. Skip,
    /// resume-to-Formation, and the return-callback chain are already covered end to end by
    /// ChapterOneCinematicTests.cs and are not re-tested here.
    /// </summary>
    public class ChapterOneOpeningTimingTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;
        private bool _reduceMotionBefore;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDOpeningTiming_" + System.Guid.NewGuid().ToString("N"));
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

        private static GameBootstrap StartTutorialToVictoryCinematic(List<GameObject> spawned, string name)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            GameBootstrap bootstrap = go.AddComponent<GameBootstrap>();
            bootstrap.Initialize();
            foreach (string spawnedName in new[] { "Canvas", "EventSystem", "CardDatabase", "BattleController" })
            {
                foreach (GameObject candidate in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    if (candidate.name == spawnedName && !spawned.Contains(candidate))
                        spawned.Add(candidate);
                }
            }

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
            Assert.AreEqual(CinematicKind.Victory, bootstrap.CinematicKindForTests, "Setup: expected the victory cinematic to be active.");
            return bootstrap;
        }

        // ---------- Locked duration, centralized and drift-guarded ----------

        [Test]
        public void OpeningCinematicDurationDoesNotDriftFromTheApprovedSource()
        {
            // ST-TUTORIAL-ANIMATION-V1-HANDOFF-003's locked timing table ("Opening playback |
            // 0.00-8.04s") plus the supplied MP4's own independently re-measured real runtime
            // (1280x720, 193 frames @ 24fps = 8.042s, via cv2.VideoCapture - not just relayed).
            // GameBootstrap.BeginOpeningCinematic reads a single centralized const for this value;
            // this test is what makes a future accidental edit to that literal a real, named
            // failure instead of a silent mismatch against the approved source.
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("OpeningTiming_Duration");
            bootstrap.StartApprovedTutorialBattle();

            Assert.AreEqual(CinematicKind.Opening, bootstrap.CinematicKindForTests);
            Assert.AreEqual(8.042f, bootstrap.CinematicDurationSecondsForTests,
                "The Opening cinematic's duration must stay exactly aligned to the approved supplied source (8.042s).");
        }

        // ---------- Reduced Motion: static holds instead of parallax/drift ----------

        [Test]
        public void ReducedMotion_SuppressesLayerDrift_OnTheOpeningCinematic()
        {
            MotionPolicy.ReduceMotion = true;
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("OpeningTiming_ReducedMotionOpening");
            bootstrap.StartApprovedTutorialBattle();
            Assert.AreEqual(CinematicKind.Opening, bootstrap.CinematicKindForTests);

            (MethodInfo drift, FieldInfo activeField, List<Image> layers) = ReflectDriftMembers(bootstrap);
            Assert.Greater(layers.Count, 0, "Setup: expected at least one composite layer image for the opening.");
            var activeCinematic = (CinematicSequence)activeField.GetValue(bootstrap);
            Image sky = layers[0];
            Vector2 before = sky.rectTransform.anchoredPosition;

            activeCinematic.Advance(2f);
            drift.Invoke(bootstrap, null);

            Assert.AreEqual(before, sky.rectTransform.anchoredPosition,
                "Reduced Motion must hold the opening's composite layers static - no parallax drift, " +
                "exactly the 'static hold' ST-TUTORIAL-ANIMATION-V1-HANDOFF-003 requires.");
        }

        [Test]
        public void ReducedMotion_SuppressesLayerDrift_OnTheVictoryCinematic()
        {
            MotionPolicy.ReduceMotion = true;
            GameBootstrap bootstrap = StartTutorialToVictoryCinematic(_spawned, "OpeningTiming_ReducedMotionVictory");

            (MethodInfo drift, FieldInfo activeField, List<Image> layers) = ReflectDriftMembers(bootstrap);
            Assert.Greater(layers.Count, 0, "Setup: expected at least one composite layer image for victory.");
            var activeCinematic = (CinematicSequence)activeField.GetValue(bootstrap);
            Image sky = layers[0];
            Vector2 before = sky.rectTransform.anchoredPosition;

            activeCinematic.Advance(2f);
            drift.Invoke(bootstrap, null);

            Assert.AreEqual(before, sky.rectTransform.anchoredPosition,
                "Reduced Motion must also hold the victory composite static.");
        }

        [Test]
        public void NormalMotion_StillDrivesRealLayerDrift_ProvingTheReducedMotionGuardIsTheReason()
        {
            // Sanity companion to the two tests above: with Reduced Motion off, the pre-existing
            // drift behavior must be completely unaffected by this change.
            MotionPolicy.ReduceMotion = false;
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("OpeningTiming_NormalMotion");
            bootstrap.StartApprovedTutorialBattle();

            (MethodInfo drift, FieldInfo activeField, List<Image> layers) = ReflectDriftMembers(bootstrap);
            var activeCinematic = (CinematicSequence)activeField.GetValue(bootstrap);
            Image sky = layers[0];
            Vector2 before = sky.rectTransform.anchoredPosition;

            activeCinematic.Advance(2f);
            drift.Invoke(bootstrap, null);

            Assert.AreNotEqual(before, sky.rectTransform.anchoredPosition,
                "With Reduced Motion off, the existing parallax drift must still actually run.");
        }

        private static (MethodInfo drift, FieldInfo activeField, List<Image> layers) ReflectDriftMembers(GameBootstrap bootstrap)
        {
            MethodInfo drift = typeof(GameBootstrap).GetMethod("DriftCinematicLayers", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo activeField = typeof(GameBootstrap).GetField("_activeCinematic", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo layersField = typeof(GameBootstrap).GetField("_cinematicLayerImages", BindingFlags.Instance | BindingFlags.NonPublic);
            var layers = (List<Image>)layersField.GetValue(bootstrap);
            return (drift, activeField, layers);
        }
    }
}

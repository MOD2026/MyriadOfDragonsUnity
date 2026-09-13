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
    /// system). Proves the locked duration is centralized and cannot silently drift from the
    /// approved supplied source (normal motion only).
    ///
    /// Reduced Motion: rc21 Option A (owner-resolved, tools/seat_reports/LK-RELEASE-042-rc21-
    /// HELD-NOT-FROZEN.md §4) - the cinematic is skipped entirely under Reduced Motion, never
    /// built at all, reinstating CC6-CR-TUTORIAL-COMBAT-ANIMATION-023's original accessibility fix
    /// this lineage never carried. This supersedes the handoff's literal "static hold" reading,
    /// which is what produced two tests here that contradicted an already-shipped skip test on a
    /// sibling line (LK caught it, held rc21, escalated - the owner chose to keep the skip). Skip,
    /// resume-to-Formation, and the return-callback chain are otherwise covered end to end by
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
            // Caller asserts its own expectation - under Reduced Motion, BeginVictoryCinematic
            // (fired synchronously by the knockout tick above) skips entirely per rc21 Option A, so
            // no cinematic of any kind is ever active here. Under normal motion it must be Victory.
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

        // ---------- Reduced Motion: skip entirely (rc21 Option A), never a static hold ----------

        [Test]
        public void ReducedMotion_SkipsTheOpeningCinematicEntirely_NeverBuildsIt()
        {
            MotionPolicy.ReduceMotion = true;
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("OpeningTiming_ReducedMotionOpening");
            bootstrap.StartApprovedTutorialBattle();

            Assert.IsFalse(bootstrap.CinematicActiveForTests,
                "rc21 Option A: a Reduced Motion player must never see the opening cinematic, not even as a static hold.");
            Assert.IsNull(bootstrap.CinematicKindForTests);
            // The real Formation state underneath is already fully built regardless of the
            // cinematic (this file's own header comment on the production code) - Reduced Motion
            // reaching it in the same frame is the whole point of the skip.
            Assert.AreEqual(BattlePhase.Formation, bootstrap.Battle.Phase);
        }

        [Test]
        public void ReducedMotion_SkipsTheVictoryCinematicEntirely_ReturnToEmpireImmediatelyWorks()
        {
            MotionPolicy.ReduceMotion = true;
            GameBootstrap bootstrap = StartTutorialToVictoryCinematic(_spawned, "OpeningTiming_ReducedMotionVictory");

            Assert.IsFalse(bootstrap.CinematicActiveForTests,
                "rc21 Option A: a Reduced Motion player must never see the victory cinematic either.");
            Assert.IsNull(bootstrap.CinematicKindForTests);
            // The result overlay is built and activated by HandleMatchEnded independent of the
            // cinematic - skipping the cinematic must not also skip or delay it.
            Assert.IsTrue(bootstrap.ResultOverlayActiveForTests,
                "STATE UNREACHED: the result overlay must already be visible with no cinematic covering it.");

            bootstrap.ReturnToCityForTests();
            Assert.IsFalse(bootstrap.ResultOverlayActiveForTests, "Return to Empire must still work immediately, uninterrupted by any cinematic.");
        }

        [Test]
        public void NormalMotion_StillShowsBothCinematics_UnaffectedByTheReducedMotionSkip()
        {
            // Sanity companion: rc21 Option A's skip must be conditioned strictly on
            // MotionPolicy.ReduceMotion - normal motion still gets both cinematics exactly as
            // before this change.
            MotionPolicy.ReduceMotion = false;
            GameBootstrap opening = SpawnAndInitializeBootstrap("OpeningTiming_NormalMotionOpeningStillShows");
            opening.StartApprovedTutorialBattle();
            Assert.AreEqual(CinematicKind.Opening, opening.CinematicKindForTests);

            GameBootstrap victory = StartTutorialToVictoryCinematic(_spawned, "OpeningTiming_NormalMotionVictoryStillShows");
            Assert.AreEqual(CinematicKind.Victory, victory.CinematicKindForTests);
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

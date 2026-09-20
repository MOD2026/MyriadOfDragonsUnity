using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// CR-ANIMATION-BETA-CONTRACT-VERIFY-001, 2026-08-28 - verifies commit 7115462's presentation
    /// interruption/teardown safety (CombatPresentationPolicy + GameBootstrap's
    /// _presentationCoroutines/_presentationObjects tracking + CancelPresentationEffects), the part
    /// CombatPresentationPolicyTests.cs (same commit) does not cover: whether the tracked-effect
    /// bookkeeping in GameBootstrap itself actually empties on every real teardown path, not just
    /// whether the pure interruption decision is correct in isolation.
    ///
    /// Gameplay state (BattleController/LaneBattleResolver) is untouched by 7115462 - confirmed by
    /// inspection of the commit diff, not re-tested here to avoid duplicating BattleLogicTests.cs.
    /// ReducedMotion_ResolvesEveryDurationImmediately (CombatPresentationPolicyTests.cs) already
    /// proves reduced motion collapses requested durations to zero. The decorative cast paths now
    /// short-circuit before creating a decorative effect or flash; informational floating labels
    /// are created directly at their final position and remain static until timed cleanup.
    /// </summary>
    public class CombatPresentationInterruptionSafetyTests
    {
        private GameObject _bootstrapGo;
        private GameBootstrap _bootstrap;
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDPresentationSafety_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();

            _bootstrapGo = new GameObject("PresentationSafetyBootstrap");
            _bootstrap = _bootstrapGo.AddComponent<GameBootstrap>();
            _bootstrap.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                Object.DestroyImmediate(go);
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            MotionPolicy.ReduceMotion = false;
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, recursive: true);
        }

        private static System.Collections.Generic.List<GameObject> PresentationObjects(GameBootstrap b) =>
            (System.Collections.Generic.List<GameObject>)typeof(GameBootstrap)
                .GetField("_presentationObjects", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(b);

        private static System.Collections.Generic.List<Coroutine> PresentationCoroutines(GameBootstrap b) =>
            (System.Collections.Generic.List<Coroutine>)typeof(GameBootstrap)
                .GetField("_presentationCoroutines", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(b);

        private static void InvokeCancelPresentationEffects(GameBootstrap b) =>
            typeof(GameBootstrap).GetMethod("CancelPresentationEffects", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(b, null);

        private static void InvokeShowTurnDamage(GameBootstrap b, TurnResolutionResult result) =>
            typeof(GameBootstrap).GetMethod("ShowTurnDamage", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(b, new object[] { result });

        // ---------- one presentation resolution per event, no duplicate emission ----------

        [Test]
        public void OneSidedDamageEvent_TracksExactlyOnePresentationObject_NotZeroNotTwo()
        {
            var result = new TurnResolutionResult { DamageDealtToSideA = 5, DamageDealtToSideB = 0 };

            InvokeShowTurnDamage(_bootstrap, result);

            Assert.AreEqual(1, PresentationObjects(_bootstrap).Count,
                "One damage side firing must produce exactly one tracked presentation effect, not zero and not a duplicate.");
            Assert.AreEqual(1, PresentationCoroutines(_bootstrap).Count);
        }

        [Test]
        public void TwoSidedDamageEvent_TracksExactlyTwoPresentationObjects_OnePerSide()
        {
            var result = new TurnResolutionResult { DamageDealtToSideA = 3, DamageDealtToSideB = 7 };

            InvokeShowTurnDamage(_bootstrap, result);

            Assert.AreEqual(2, PresentationObjects(_bootstrap).Count,
                "Each side that actually took damage gets exactly one effect - neither side is skipped nor doubled.");
        }

        [Test]
        public void NoDamageEvent_TracksNoPresentationObjects()
        {
            var result = new TurnResolutionResult { DamageDealtToSideA = 0, DamageDealtToSideB = 0 };

            InvokeShowTurnDamage(_bootstrap, result);

            CollectionAssert.IsEmpty(PresentationObjects(_bootstrap),
                "A tick with no damage must not fabricate a presentation effect.");
        }

        // ---------- interruption / teardown leaves no tracked transient effects ----------

        /// <summary>CancelPresentationEffects now uses DestroyImmediate outside Play mode (CLAUDE.md
        /// rule 7), so cancellation really destroys the tracked GameObjects even in EditMode. This
        /// replaces the old expectation of Unity's "Destroy may not be called from edit mode"
        /// error, which only ever proved the cleanup was ATTEMPTED - the objects were left alive.
        /// Returns the live objects to check afterwards.</summary>
        private static System.Collections.Generic.List<GameObject> TrackedLiveObjects(GameBootstrap bootstrap)
        {
            var live = new System.Collections.Generic.List<GameObject>();
            foreach (GameObject go in PresentationObjects(bootstrap)) if (go != null) live.Add(go);
            return live;
        }

        private static void AssertAllDestroyed(System.Collections.Generic.List<GameObject> before)
        {
            foreach (GameObject go in before)
                Assert.IsTrue(go == null, "Cancellation must actually destroy the tracked effect object, not just forget it.");
        }

        [Test]
        public void CancelPresentationEffects_ClearsAllTrackedObjects()
        {
            InvokeShowTurnDamage(_bootstrap, new TurnResolutionResult { DamageDealtToSideA = 1, DamageDealtToSideB = 1 });
            Assert.AreEqual(2, PresentationObjects(_bootstrap).Count, "Setup: two effects must be tracked before cancellation.");

            System.Collections.Generic.List<GameObject> before = TrackedLiveObjects(_bootstrap);
            InvokeCancelPresentationEffects(_bootstrap);

            AssertAllDestroyed(before);
            CollectionAssert.IsEmpty(PresentationObjects(_bootstrap), "Cancellation must leave no tracked transient objects.");
            CollectionAssert.IsEmpty(PresentationCoroutines(_bootstrap), "Cancellation must leave no tracked transient coroutines.");
        }

        [Test]
        public void ReplayIntro_CancelsAnyInFlightPresentationEffects()
        {
            InvokeShowTurnDamage(_bootstrap, new TurnResolutionResult { DamageDealtToSideA = 4, DamageDealtToSideB = 0 });
            Assert.AreEqual(1, PresentationObjects(_bootstrap).Count, "Setup: one effect must be tracked before replay.");

            System.Collections.Generic.List<GameObject> before = TrackedLiveObjects(_bootstrap);
            _bootstrap.ReplayIntro();

            AssertAllDestroyed(before);
            CollectionAssert.IsEmpty(PresentationObjects(_bootstrap),
                "ReplayIntro must not leave a stale in-flight effect from before the reset.");
            CollectionAssert.IsEmpty(PresentationCoroutines(_bootstrap));
        }

        [Test]
        public void HidingTheBattleCanvas_CancelsAnyInFlightPresentationEffects()
        {
            InvokeShowTurnDamage(_bootstrap, new TurnResolutionResult { DamageDealtToSideA = 0, DamageDealtToSideB = 6 });
            Assert.AreEqual(1, PresentationObjects(_bootstrap).Count, "Setup: one effect must be tracked before the canvas hides.");

            System.Collections.Generic.List<GameObject> before = TrackedLiveObjects(_bootstrap);
            _bootstrap.SetBattleCanvasVisible(false);

            AssertAllDestroyed(before);
            CollectionAssert.IsEmpty(PresentationObjects(_bootstrap),
                "Hiding the battle canvas must not leave a stale effect animating underneath.");
        }

        [Test]
        public void ShowingTheBattleCanvasAgain_DoesNotCancelFreshEffects()
        {
            _bootstrap.SetBattleCanvasVisible(true);
            InvokeShowTurnDamage(_bootstrap, new TurnResolutionResult { DamageDealtToSideA = 2, DamageDealtToSideB = 0 });

            Assert.AreEqual(1, PresentationObjects(_bootstrap).Count,
                "SetBattleCanvasVisible(true) must not cancel presentation - only hiding (false) does.");
        }

        // ---------- CR-ANIMATION-CARD-PLAY-POLISH-VERIFY-001, 2026-08-29: feaab75 tracks the hand
        // card's pop-scale coroutine in _presentationCoroutines (it previously was NOT tracked at
        // all, so CancelPresentationEffects never reached it). Unlike ShowTurnDamage's effects,
        // this coroutine has no matching _presentationObjects entry - it animates an existing hand
        // button in place rather than spawning a new GameObject.
        //
        // PopSelectedHandCard itself is gated behind Application.isPlaying (like every other
        // coroutine-driving GameBootstrap method - MotionPolicy.cs's own comment: "Coroutines only
        // run in Play mode... decorative motion is always off outside Play mode"), so it cannot be
        // driven directly from EditMode - confirmed by running it via reflection first, which
        // returned without tracking anything. That gate is correct production behavior, not a gap:
        // there is no per-frame host to advance the coroutine in EditMode anyway.
        //
        // What IS reachable and worth proving: whether CancelPresentationEffects's
        // _presentationCoroutines loop depends on a matching _presentationObjects entry to work.
        // It doesn't (the two loops are independent, not zipped, per source inspection) - proven
        // here with a real coroutine handle obtained the same way PopSelectedHandCard gets one
        // (MonoBehaviour.StartCoroutine, public, not gated), seeded with no matching object.

        private static IEnumerator NeverEndingCoroutine()
        {
            while (true) yield return null;
        }

        [Test]
        public void CancelPresentationEffects_StopsACoroutineOnlyEntry_WithNoMatchingObjectToDestroy()
        {
            Coroutine coroutineOnly = _bootstrap.StartCoroutine(NeverEndingCoroutine());
            PresentationCoroutines(_bootstrap).Add(coroutineOnly);
            Assert.AreEqual(1, PresentationCoroutines(_bootstrap).Count, "Setup: one coroutine-only entry must be tracked before cancellation.");
            CollectionAssert.IsEmpty(PresentationObjects(_bootstrap), "Setup: this entry must have no matching tracked object.");

            InvokeCancelPresentationEffects(_bootstrap);

            CollectionAssert.IsEmpty(PresentationCoroutines(_bootstrap),
                "A tracked coroutine with no backing GameObject must still be stopped and cleared - the same mechanism " +
                "feaab75's newly-tracked pop-scale coroutine now relies on.");
        }
    }
}

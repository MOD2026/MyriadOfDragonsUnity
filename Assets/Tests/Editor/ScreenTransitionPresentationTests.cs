using System;
using System.Collections;
using System.IO;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// CC9 metagame loading/screen-transition motion slice.
    ///
    /// Coroutines never run in EditMode (CLAUDE.md non-negotiable #6), so this file covers what
    /// EditMode actually CAN prove: the pure duration policy itself (Reduced Motion collapses
    /// every duration to exactly 0 - immediate/static, not a shorter animation), and that both
    /// wired presenters (Empire, Solo Circuit) build the expected CanvasGroup structure - the
    /// root and the lower action band - without altering any existing content, text, or teardown
    /// behavior. What a real fade actually looks like frame-to-frame is a Play Mode concern this
    /// suite cannot and does not claim to prove.
    /// </summary>
    public class ScreenTransitionPresentationTests
    {
        // ---------- Pure policy ----------

        [Test]
        public void ResolveDurationMs_NormalMotion_ReturnsTheRequestedDuration()
        {
            Assert.AreEqual(180, ScreenTransitionPresentation.ResolveDurationMs(180, reduceMotion: false));
        }

        [Test]
        public void ResolveDurationMs_ReducedMotion_CollapsesToZero_RegardlessOfRequestedValue()
        {
            Assert.AreEqual(0, ScreenTransitionPresentation.ResolveDurationMs(180, reduceMotion: true));
            Assert.AreEqual(0, ScreenTransitionPresentation.ResolveDurationMs(9999, reduceMotion: true));
        }

        [Test]
        public void ResolveDurationMs_NegativeRequest_NeverGoesBelowZero_EvenWithoutReducedMotion()
        {
            Assert.AreEqual(0, ScreenTransitionPresentation.ResolveDurationMs(-50, reduceMotion: false));
        }

        [Test]
        public void AllDurations_AreShortAndBounded()
        {
            // "Normal motion should be short and controlled" - a concrete, checkable ceiling
            // rather than a vibe. Half a second is generous for a metagame screen transition;
            // anything past it would read as sluggish, not "bounded, readable".
            const int ceilingMs = 500;
            Assert.LessOrEqual(ScreenTransitionPresentation.EntryFadeMs, ceilingMs);
            Assert.LessOrEqual(ScreenTransitionPresentation.ExitFadeMs, ceilingMs);
            Assert.LessOrEqual(ScreenTransitionPresentation.BackNavExitMs, ceilingMs);
            Assert.LessOrEqual(ScreenTransitionPresentation.ActionBandRevealDelayMs, ceilingMs);
            Assert.LessOrEqual(ScreenTransitionPresentation.ActionBandRevealMs, ceilingMs);

            Assert.Greater(ScreenTransitionPresentation.EntryFadeMs, 0);
            Assert.Greater(ScreenTransitionPresentation.ExitFadeMs, 0);
            Assert.Greater(ScreenTransitionPresentation.BackNavExitMs, 0);
        }

        /// <summary>Advances a coroutine-shaped IEnumerator by one real Unity "step", recursively
        /// entering any nested IEnumerator a `yield return childEnumerator` produces (exactly
        /// DelayedFadeIn's own `yield return FadeIn(...)` tail call) - a plain flat
        /// `enumerator.MoveNext()` loop does not reproduce that nesting on its own, since Unity's
        /// real coroutine runner is what recognises a yielded IEnumerator as "descend into this
        /// one now", not the IEnumerator machinery itself. Returns true if a real per-frame wait
        /// occurred anywhere in the chain, false once everything completed synchronously - which
        /// is exactly what Reduced Motion must do.</summary>
        private static bool DrainOneStep(IEnumerator routine)
        {
            if (!routine.MoveNext()) return false;
            return routine.Current is IEnumerator nested ? DrainOneStep(nested) : true;
        }

        // ---------- FadeIn/FadeOutThenInvoke: null-safety only (real ticking needs Play Mode) ----------

        [Test]
        public void FadeIn_NullCanvasGroup_DoesNotThrow()
        {
            var enumerator = ScreenTransitionPresentation.FadeIn(null, 180, false);
            Assert.DoesNotThrow(() => { while (enumerator.MoveNext()) { } });
        }

        [Test]
        public void FadeOutThenInvoke_NullCanvasGroup_StillInvokesCallback()
        {
            bool invoked = false;
            var enumerator = ScreenTransitionPresentation.FadeOutThenInvoke(null, 130, false, () => invoked = true);
            while (enumerator.MoveNext()) { }
            Assert.IsTrue(invoked, "A null CanvasGroup must never strand the caller without its completion callback.");
        }

        [Test]
        public void FadeOutThenInvoke_ReducedMotion_SetsAlphaToZero_AndInvokesImmediately_WithNoIntermediateFrame()
        {
            var go = new GameObject("Coverage_FadeOutReducedMotion");
            try
            {
                CanvasGroup group = go.AddComponent<CanvasGroup>();
                group.alpha = 1f;
                bool invoked = false;

                var enumerator = ScreenTransitionPresentation.FadeOutThenInvoke(group, 130, reduceMotion: true, () => invoked = true);
                bool hasMore = enumerator.MoveNext();

                Assert.AreEqual(0f, group.alpha, "Reduced Motion must snap straight to the end state - immediate, not eased.");
                Assert.IsTrue(invoked, "Reduced Motion must invoke completion on the very first step, not after a delay.");
                Assert.IsFalse(hasMore, "Reduced Motion must not yield any intermediate frame.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void FadeIn_ReducedMotion_SetsAlphaToOne_WithNoIntermediateFrame()
        {
            var go = new GameObject("Coverage_FadeInReducedMotion");
            try
            {
                CanvasGroup group = go.AddComponent<CanvasGroup>();
                group.alpha = 0f;

                var enumerator = ScreenTransitionPresentation.FadeIn(group, 180, reduceMotion: true);
                bool hasMore = enumerator.MoveNext();

                Assert.AreEqual(1f, group.alpha, "Reduced Motion must reach full visibility immediately.");
                Assert.IsFalse(hasMore, "Reduced Motion must not yield any intermediate frame.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DelayedFadeIn_ReducedMotion_CollapsesDelayAndFade_ToOneImmediateStep()
        {
            var go = new GameObject("Coverage_DelayedFadeInReducedMotion");
            try
            {
                CanvasGroup group = go.AddComponent<CanvasGroup>();
                group.alpha = 1f; // deliberately not the pre-reveal value, so a real reset is provable

                var enumerator = ScreenTransitionPresentation.DelayedFadeIn(
                    group, ScreenTransitionPresentation.ActionBandRevealDelayMs,
                    ScreenTransitionPresentation.ActionBandRevealMs, reduceMotion: true);
                bool hasMore = DrainOneStep(enumerator);

                Assert.AreEqual(1f, group.alpha, "Reduced Motion must land the action band at full visibility with no separate staggered state.");
                Assert.IsFalse(hasMore, "Reduced Motion must collapse delay+fade into a single immediate step.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

    }

    // ---------- Real presenter structure: CanvasGroups exist, nothing else changed ----------
    public class EmpireAndSoloCircuitTransitionStructureTests
    {
            private System.Collections.Generic.List<GameObject> _spawned;
            private string _scratchSaveDir;

            [SetUp]
            public void SetUp()
            {
                _spawned = new System.Collections.Generic.List<GameObject>();
                _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDScreenTransition_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_scratchSaveDir);
                SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
                SaveSystem.ResetCurrentProfileForTests();
            }

            [TearDown]
            public void TearDown()
            {
                foreach (GameObject go in _spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
                _spawned.Clear();
                SaveSystem.ClearRootDirectoryOverride();
                SaveSystem.ResetCurrentProfileForTests();
                if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
                    Directory.Delete(_scratchSaveDir, recursive: true);
            }

            [Test]
            public void Empire_BuildUI_AddsRootCanvasGroup_AtFullAlpha_InEditMode()
            {
                var go = new GameObject("Coverage_EmpireTransitionRoot");
                _spawned.Add(go);
                EmpirePresenter presenter = go.AddComponent<EmpirePresenter>();
                presenter.Initialize(onBackToHome: null);

                CanvasGroup rootGroup = presenter.CanvasObjectForTests.GetComponent<CanvasGroup>();
                Assert.IsNotNull(rootGroup, "Expected the Empire canvas root to carry a CanvasGroup for the entry-fade slice.");
                Assert.AreEqual(1f, rootGroup.alpha,
                    "EditMode never starts the fade coroutine, so alpha must stay at the untouched default - existing content-reading tests must see the screen exactly as before this slice.");
            }

            [Test]
            public void Empire_RemainingStructuresStrip_CarriesItsOwnCanvasGroup_ForTheActionBandReveal()
            {
                var go = new GameObject("Coverage_EmpireActionBand");
                _spawned.Add(go);
                EmpirePresenter presenter = go.AddComponent<EmpirePresenter>();
                presenter.Initialize(onBackToHome: null);

                Transform strip = presenter.CanvasObjectForTests.transform.Find("EmpireConstructionRoot/RemainingStructuresStrip")
                    ?? FindDeep(presenter.CanvasObjectForTests.transform, "RemainingStructuresStrip");
                Assert.IsNotNull(strip, "STATE UNREACHED: expected to find RemainingStructuresStrip somewhere under the Empire canvas.");
                Assert.IsNotNull(strip.GetComponent<CanvasGroup>(),
                    "RemainingStructuresStrip must carry its own CanvasGroup for the delayed action-band reveal.");
            }

            [Test]
            public void Empire_BackButton_InEditMode_StillTearsDownSynchronously_AndInvokesCallback()
            {
                var go = new GameObject("Coverage_EmpireBackNav");
                _spawned.Add(go);
                bool wentBack = false;
                EmpirePresenter presenter = go.AddComponent<EmpirePresenter>();
                presenter.Initialize(onBackToHome: () => wentBack = true);

                Button back = presenter.CanvasObjectForTests.transform.Find("EmpireHeader/Btn_Back")?.GetComponent<Button>();
                Assert.IsNotNull(back, "STATE UNREACHED: expected to find Btn_Back under EmpireHeader.");
                back.onClick.Invoke();

                Assert.IsTrue(wentBack, "The back callback must still fire synchronously in EditMode - CC9's transition slice must not strand the player or break existing tests.");
                Assert.IsNull(presenter.CanvasObjectForTests, "The canvas must still be torn down by the back action in EditMode.");
            }

            [Test]
            public void SoloCircuit_BuildUI_AddsRootCanvasGroup_AtFullAlpha_InEditMode()
            {
                var go = new GameObject("Coverage_SoloCircuitTransitionRoot");
                _spawned.Add(go);
                SoloCircuitPresenter presenter = go.AddComponent<SoloCircuitPresenter>();
                presenter.Initialize(new PlayerProfile(), DateTime.UtcNow, onBack: null);

                CanvasGroup rootGroup = presenter.CanvasObjectForTests.GetComponent<CanvasGroup>();
                Assert.IsNotNull(rootGroup, "Expected the Solo Circuit canvas root to carry a CanvasGroup for the entry-fade slice.");
                Assert.AreEqual(1f, rootGroup.alpha,
                    "EditMode never starts the fade coroutine, so alpha must stay at the untouched default.");
            }

            [Test]
            public void SoloCircuit_CycleRow_CarriesItsOwnCanvasGroup_ForTheActionBandReveal()
            {
                var go = new GameObject("Coverage_SoloCircuitActionBand");
                _spawned.Add(go);
                SoloCircuitPresenter presenter = go.AddComponent<SoloCircuitPresenter>();
                presenter.Initialize(new PlayerProfile(), DateTime.UtcNow, onBack: null);

                Transform cycleRow = FindDeep(presenter.CanvasObjectForTests.transform, "CycleRow");
                Assert.IsNotNull(cycleRow, "STATE UNREACHED: expected to find CycleRow somewhere under the Solo Circuit canvas.");
                Assert.IsNotNull(cycleRow.GetComponent<CanvasGroup>(),
                    "CycleRow must carry its own CanvasGroup for the delayed action-band reveal.");
            }

            [Test]
            public void SoloCircuit_Close_InEditMode_StillTearsDownSynchronously_AndInvokesCallback()
            {
                // Same behavior EditMode already relied on before this slice - re-asserted here as
                // a direct regression guard, alongside SoloCircuitPresenterTests' own coverage.
                var go = new GameObject("Coverage_SoloCircuitBackNav");
                _spawned.Add(go);
                bool wentBack = false;
                SoloCircuitPresenter presenter = go.AddComponent<SoloCircuitPresenter>();
                presenter.Initialize(new PlayerProfile(), DateTime.UtcNow, () => wentBack = true);

                presenter.PressBackForTests();

                Assert.IsTrue(wentBack, "The back callback must still fire synchronously in EditMode.");
                Assert.IsNull(presenter.CanvasObjectForTests, "The canvas must still be torn down synchronously in EditMode.");
            }

            private static Transform FindDeep(Transform root, string name)
            {
                if (root.name == name) return root;
                foreach (Transform child in root)
                {
                    Transform found = FindDeep(child, name);
                    if (found != null) return found;
                }
                return null;
            }
    }
}

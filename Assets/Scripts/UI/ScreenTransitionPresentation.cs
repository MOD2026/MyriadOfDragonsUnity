using System;
using System.Collections;
using UnityEngine;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// CC9 metagame loading/screen-transition motion slice. Bounded, readable fades for screen
    /// entry/exit, the loading-to-content handoff, a lower action band's delayed reveal, and
    /// back-navigation - reused by every metagame presenter that opts in (currently Empire and
    /// Solo Circuit; Campaign is Metagame-seat-owned and explicitly out of this Battle seat's
    /// authority to edit, per CLAUDE.md).
    ///
    /// Pure duration/timing policy plus coroutine bodies that only ever move a CanvasGroup's
    /// alpha - never position, scale, rotation, hue, or a new visual element. That keeps every
    /// transition here compatible with the task's constraints: no redesign, no PPT-style
    /// panels/crosshairs, no drift/flash/pulse/rotation under Reduced Motion.
    ///
    /// CLAUDE.md non-negotiable #6 ("MonoBehaviours supply only timing") applies here exactly as
    /// it does to Battle's own presentation coroutines (GameBootstrap.PlayEffect etc.): coroutines
    /// only run in Play Mode, so every call site in this project gates starting one on
    /// Application.isPlaying and otherwise leaves the CanvasGroup at its untouched default alpha
    /// (1) - an EditMode test that inspects content via transform.Find/GetComponent is completely
    /// unaffected by whether the fade coroutine would have run.
    /// </summary>
    public static class ScreenTransitionPresentation
    {
        /// <summary>Screen entry fade-in - also the loading-to-content handoff for these
        /// presenters, which build their content synchronously (no async loading phase to bridge
        /// separately): the canvas starts transparent with content already populated behind it,
        /// then reveals over this duration, so the player never sees a partially-built frame.</summary>
        public const int EntryFadeMs = 180;

        /// <summary>Screen exit fade, used for a plain (non-back-nav) teardown transition.</summary>
        public const int ExitFadeMs = 140;

        /// <summary>Back-navigation's own exit fade before the real teardown + callback fire -
        /// slightly shorter than a generic exit so returning never feels slower than entering.</summary>
        public const int BackNavExitMs = 130;

        /// <summary>Delay before the lower action band begins its own reveal, after the screen's
        /// main entry fade has started - a short, bounded stagger so the action band reads as
        /// "arriving after the content it acts on" rather than popping in simultaneously with
        /// everything else.</summary>
        public const int ActionBandRevealDelayMs = 90;

        /// <summary>The action band's own fade-in duration once its delay elapses.</summary>
        public const int ActionBandRevealMs = 140;

        /// <summary>Resolves a requested duration against Reduced Motion. Reduced Motion collapses
        /// to 0 - immediate and static, never a shorter version of the same drift/flash/pulse -
        /// matching every other ResolveDurationMs-shaped policy in this project
        /// (CombatPresentationPolicy.ResolveDurationMs is the Battle-side sibling; this is the
        /// metagame-side one, kept separate because the two domains' own duration tables must not
        /// silently cross-reference each other).</summary>
        public static int ResolveDurationMs(int requestedMs, bool reduceMotion) =>
            reduceMotion ? 0 : requestedMs < 0 ? 0 : requestedMs;

        /// <summary>Fades a CanvasGroup from 0 to 1. Under Reduced Motion (or a non-positive
        /// resolved duration), sets alpha to 1 immediately with no intermediate frame - "immediate/
        /// static", not a fast version of the same animation.</summary>
        public static IEnumerator FadeIn(CanvasGroup group, int requestedMs, bool reduceMotion)
        {
            if (group == null) yield break;
            int resolvedMs = ResolveDurationMs(requestedMs, reduceMotion);
            if (resolvedMs <= 0)
            {
                group.alpha = 1f;
                yield break;
            }

            group.alpha = 0f;
            float duration = resolvedMs / 1000f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                group.alpha = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            group.alpha = 1f;
        }

        /// <summary>Waits an optional delay, then fades a CanvasGroup from 0 to 1 - the lower
        /// action band's own delayed reveal. Under Reduced Motion the delay and the fade both
        /// collapse to 0, so the band simply appears at full alpha on the same frame as
        /// everything else - no separate "instant but still staggered" state.</summary>
        public static IEnumerator DelayedFadeIn(CanvasGroup group, int delayMs, int fadeMs, bool reduceMotion)
        {
            if (group == null) yield break;
            group.alpha = 0f;

            int resolvedDelayMs = ResolveDurationMs(delayMs, reduceMotion);
            if (resolvedDelayMs > 0)
            {
                float delaySeconds = resolvedDelayMs / 1000f;
                float waited = 0f;
                while (waited < delaySeconds)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }
            }

            yield return FadeIn(group, fadeMs, reduceMotion);
        }

        /// <summary>Fades a CanvasGroup from its current alpha to 0, then invokes
        /// <paramref name="onComplete"/> - the back-navigation / exit transition. Under Reduced
        /// Motion the alpha snaps to 0 and onComplete fires on the same frame, before this
        /// coroutine's first yield - calling it via StartCoroutine still executes that first
        /// segment synchronously (Unity coroutines run to their first `yield` immediately), so
        /// Reduced Motion callers observe no per-frame delay at all, matching "immediate/static".
        /// </summary>
        public static IEnumerator FadeOutThenInvoke(CanvasGroup group, int requestedMs, bool reduceMotion, Action onComplete)
        {
            if (group == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            int resolvedMs = ResolveDurationMs(requestedMs, reduceMotion);
            if (resolvedMs <= 0)
            {
                group.alpha = 0f;
                onComplete?.Invoke();
                yield break;
            }

            float startAlpha = group.alpha;
            float duration = resolvedMs / 1000f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                group.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
                yield return null;
            }
            group.alpha = 0f;
            onComplete?.Invoke();
        }
    }
}

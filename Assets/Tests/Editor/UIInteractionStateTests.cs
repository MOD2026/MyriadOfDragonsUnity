using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using MyriadOfDragons.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Interaction-state tokens (register: "Interaction states - LOCKED 2026-08-27"). Everything
    /// here calls the pure Resolve() function or the controller's explicit-time *At() methods
    /// directly - no Update(), no real Time.unscaledTime dependency, matching CLAUDE.md's "real
    /// logic in plain testable methods" rule.
    /// </summary>
    public class UIInteractionStateTests
    {
        [Test]
        public void Resolve_BaseState_IsFullyNeutral()
        {
            InteractionVisual v = UIInteractionStateTokens.Resolve(
                InteractionStateFlags.None, UIDesignTokens.FrameTier.Tier2Section, msSinceChange: 500f);

            Assert.AreEqual(1f, v.Scale);
            Assert.AreEqual(1f, v.Brightness);
            Assert.AreEqual(1f, v.Opacity);
            Assert.IsFalse(v.ShowNewBadge);
            Assert.IsNull(v.ErrorFlashTint);
        }

        [Test]
        public void Resolve_Pressed_ReachesTierTargetScaleAndDarkenAtDuration()
        {
            UIInteractionStateTokens.PressSpec spec = UIInteractionStateTokens.PressSpecFor(UIDesignTokens.FrameTier.Tier1Hero);
            InteractionVisual atStart = UIInteractionStateTokens.Resolve(
                InteractionStateFlags.Pressed, UIDesignTokens.FrameTier.Tier1Hero, msSinceChange: 0f);
            InteractionVisual atDuration = UIInteractionStateTokens.Resolve(
                InteractionStateFlags.Pressed, UIDesignTokens.FrameTier.Tier1Hero, msSinceChange: spec.DurationMs);

            Assert.AreEqual(1f, atStart.Scale, 0.001f, "Press feedback must start at rest, not snap instantly.");
            Assert.AreEqual(spec.Scale, atDuration.Scale, 0.001f);
            Assert.Less(atDuration.Brightness, 1f, "Pressed must darken the control.");
        }

        [Test]
        public void Resolve_Disabled_SuppressesPressFeedback_EvenIfPressedFlagIsSet()
        {
            // Register: "A disabled control must never animate as if it accepted input."
            InteractionVisual v = UIInteractionStateTokens.Resolve(
                InteractionStateFlags.Disabled | InteractionStateFlags.Pressed,
                UIDesignTokens.FrameTier.Tier1Hero, msSinceChange: 1000f);

            Assert.AreEqual(1f, v.Scale, "Disabled must not show the press squish.");
            Assert.AreEqual(UIInteractionStateTokens.DisabledBrightness, v.Brightness);
            Assert.AreEqual(UIInteractionStateTokens.DisabledOpacity, v.Opacity);
        }

        [Test]
        public void Resolve_Locked_SuppressesPressFeedback_ShowsLockedLook()
        {
            InteractionVisual v = UIInteractionStateTokens.Resolve(
                InteractionStateFlags.Locked | InteractionStateFlags.Pressed,
                UIDesignTokens.FrameTier.Tier1Hero, msSinceChange: 1000f);

            Assert.AreEqual(1f, v.Scale);
            Assert.AreEqual(UIInteractionStateTokens.LockedBrightness, v.Brightness);
            Assert.AreEqual(UIInteractionStateTokens.LockedOpacity, v.Opacity);
        }

        [Test]
        public void Resolve_Selected_UsesUnderlineOnly_NeverABox()
        {
            InteractionVisual v = UIInteractionStateTokens.Resolve(
                InteractionStateFlags.Selected, UIDesignTokens.FrameTier.Tier2Section, msSinceChange: 0f);

            Assert.IsTrue(v.AccentIsUnderlineOnly, "Register: 'accent underline or glow, NO box.'");
            Assert.Greater(v.AccentGlowStrength, 0f);
        }

        [Test]
        public void Resolve_LockedPlusNew_ComposesBothEffects()
        {
            // Register explicitly names this composition: "locked+new".
            InteractionVisual v = UIInteractionStateTokens.Resolve(
                InteractionStateFlags.Locked | InteractionStateFlags.New,
                UIDesignTokens.FrameTier.Tier3Utility, msSinceChange: 2000f);

            Assert.AreEqual(UIInteractionStateTokens.LockedBrightness, v.Brightness,
                "Long after the New reveal finishes, Locked's own brightness must still apply.");
            Assert.IsTrue(v.ShowNewBadge, "New's badge is persistent, not just the reveal animation.");
        }

        [Test]
        public void Resolve_New_RevealDecaysToRestAfterDuration_ButBadgePersists()
        {
            InteractionVisual mid = UIInteractionStateTokens.Resolve(
                InteractionStateFlags.New, UIDesignTokens.FrameTier.Tier2Section, msSinceChange: 0f);
            InteractionVisual after = UIInteractionStateTokens.Resolve(
                InteractionStateFlags.New, UIDesignTokens.FrameTier.Tier2Section,
                msSinceChange: UIInteractionStateTokens.NewRevealDurationMs + 50f);

            Assert.Greater(mid.Brightness, 1f, "Reveal must visibly brighten at the start.");
            Assert.AreEqual(1f, after.Brightness, 0.001f, "Reveal must fully decay once its one-shot duration passes.");
            Assert.IsTrue(after.ShowNewBadge, "The badge itself is not part of the one-shot reveal.");
        }

        [Test]
        public void Resolve_Pending_OscillatesOpacityWithinLockedRange()
        {
            for (float ms = 0f; ms <= UIInteractionStateTokens.PendingPulsePeriodMs * 2f; ms += 37f)
            {
                InteractionVisual v = UIInteractionStateTokens.Resolve(
                    InteractionStateFlags.Pending, UIDesignTokens.FrameTier.Tier2Section, msSinceChange: ms);
                Assert.GreaterOrEqual(v.Opacity, UIInteractionStateTokens.PendingOpacityMin - 0.001f);
                Assert.LessOrEqual(v.Opacity, UIInteractionStateTokens.PendingOpacityMax + 0.001f);
            }
        }

        [Test]
        public void Resolve_Error_FlashesThenStops()
        {
            float totalMs = UIInteractionStateTokens.ErrorFlashDurationMs * UIInteractionStateTokens.ErrorFlashCount * 2f;

            InteractionVisual firstFlash = UIInteractionStateTokens.Resolve(
                InteractionStateFlags.Error, UIDesignTokens.FrameTier.Tier2Section, msSinceChange: 1f);
            InteractionVisual afterAllFlashes = UIInteractionStateTokens.Resolve(
                InteractionStateFlags.Error, UIDesignTokens.FrameTier.Tier2Section, msSinceChange: totalMs + 10f);

            Assert.IsNotNull(firstFlash.ErrorFlashTint, "The first flash window must show the red-shift tint.");
            Assert.IsNull(afterAllFlashes.ErrorFlashTint, "Error must stop flashing once its sequence completes.");
        }

        [Test]
        public void PressSpecFor_Tier4_HasNoScaleChange()
        {
            // Register: "Tier4 no visible scaling on small icons - a tint/highlight only."
            UIInteractionStateTokens.PressSpec spec = UIInteractionStateTokens.PressSpecFor(UIDesignTokens.FrameTier.Tier4Surface);
            Assert.AreEqual(1f, spec.Scale);
        }

        // ------------------------------------------------------------------ controller

        private static InteractionStateController SpawnController()
        {
            var go = new GameObject("InteractionStateHarness", typeof(RectTransform), typeof(Image), typeof(Button));
            return go.AddComponent<InteractionStateController>();
        }

        [Test]
        public void Controller_TapInsideAndRelease_FiresActivatedOnce()
        {
            InteractionStateController c = SpawnController();
            int fireCount = 0;
            c.Activated += () => fireCount++;

            c.OnPointerDownAt(0f);
            c.OnPointerUpAt(0.05f);

            Assert.AreEqual(1, fireCount);
            Object.DestroyImmediate(c.gameObject);
        }

        [Test]
        public void Controller_DragOutsideThenRelease_DoesNotActivate()
        {
            InteractionStateController c = SpawnController();
            int fireCount = 0;
            c.Activated += () => fireCount++;

            c.OnPointerDownAt(0f);
            c.OnPointerExitAt(0.05f); // dragged off the control
            c.OnPointerUpAt(0.1f);    // released while outside

            Assert.AreEqual(0, fireCount, "Register: 'Drag outside cancels the press and does not activate.'");
            Object.DestroyImmediate(c.gameObject);
        }

        [Test]
        public void Controller_DragOutsideThenBackInsideBeforeRelease_DoesActivate()
        {
            InteractionStateController c = SpawnController();
            int fireCount = 0;
            c.Activated += () => fireCount++;

            c.OnPointerDownAt(0f);
            c.OnPointerExitAt(0.05f);
            c.OnPointerEnterAt(0.08f); // re-entered before release
            c.OnPointerUpAt(0.1f);

            Assert.AreEqual(1, fireCount, "Register: 're-entering before release restores it.'");
            Object.DestroyImmediate(c.gameObject);
        }

        [Test]
        public void Controller_FastDoubleTap_FiresActivatedOnlyOnce()
        {
            // The real bug class CC flagged: "several of our actions grant currency" - a fast
            // double-tap on the same control within the activation cooldown must not double-fire.
            InteractionStateController c = SpawnController();
            int fireCount = 0;
            c.Activated += () => fireCount++;

            c.OnPointerDownAt(0f);
            c.OnPointerUpAt(0.02f); // first tap, real activation

            c.OnPointerDownAt(0.05f);
            c.OnPointerUpAt(0.07f); // second tap, well inside ActivationCooldownMs

            Assert.AreEqual(1, fireCount, "A fast double-tap must not fire the action twice.");
            Object.DestroyImmediate(c.gameObject);
        }

        [Test]
        public void Controller_TapAfterCooldownElapsed_FiresAgain()
        {
            InteractionStateController c = SpawnController();
            int fireCount = 0;
            c.Activated += () => fireCount++;

            c.OnPointerDownAt(0f);
            c.OnPointerUpAt(0.02f);

            float afterCooldownSec = (UIInteractionStateTokens.ActivationCooldownMs / 1000f) + 0.05f;
            c.OnPointerDownAt(afterCooldownSec);
            c.OnPointerUpAt(afterCooldownSec + 0.02f);

            Assert.AreEqual(2, fireCount, "A genuinely later, separate tap after the cooldown must still work.");
            Object.DestroyImmediate(c.gameObject);
        }

        [Test]
        public void Controller_Disabled_NeverActivates_EvenOnAValidTap()
        {
            InteractionStateController c = SpawnController();
            int fireCount = 0;
            c.Activated += () => fireCount++;
            c.SetDisabled(true);

            c.OnPointerDownAt(0f);
            c.OnPointerUpAt(0.02f);

            Assert.AreEqual(0, fireCount);
            Object.DestroyImmediate(c.gameObject);
        }

        [Test]
        public void Controller_Locked_NeverActivates_EvenOnAValidTap()
        {
            InteractionStateController c = SpawnController();
            int fireCount = 0;
            c.Activated += () => fireCount++;
            c.SetLocked(true);

            c.OnPointerDownAt(0f);
            c.OnPointerUpAt(0.02f);

            Assert.AreEqual(0, fireCount);
            Object.DestroyImmediate(c.gameObject);
        }

        [Test]
        public void Controller_ApplyAt_DoesNotThrow_AndDrivesButtonInteractable()
        {
            InteractionStateController c = SpawnController();
            Button button = c.GetComponent<Button>();
            Assert.IsTrue(button.interactable, "Setup: fresh Button starts interactable.");

            c.SetDisabled(true);
            c.ApplyAt(0f);
            Assert.IsFalse(button.interactable, "Disabled must drive the real Button.interactable off.");

            c.SetDisabled(false);
            c.ApplyAt(0f);
            Assert.IsTrue(button.interactable);
            Object.DestroyImmediate(c.gameObject);
        }
    }
}

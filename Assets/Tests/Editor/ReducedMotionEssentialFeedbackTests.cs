using NUnit.Framework;
using MyriadOfDragons.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// CR-BETA-REDUCED-MOTION-CROSSCHECK-004, 2026-08-29 - verifying commit b930ca3
    /// (WH-REDUCED-MOTION-CLUSTER-IMPLEMENT-003). SettingsReducedMotionTests.cs (same commit)
    /// proves Pending's decorative pulse is suppressed and Press acknowledgement still reaches its
    /// target scale under reduced motion, but nothing asserts the one other branch that commit
    /// changed: Error under reduceMotion=true swaps a timed flash-then-stop sequence
    /// (UIInteractionStateTests.Resolve_Error_FlashesThenStops, unaffected by this commit - it
    /// calls Resolve with the reduceMotion parameter defaulted to false) for an unconditional solid
    /// tint. That solid-tint branch itself had no direct coverage anywhere.
    /// </summary>
    public class ReducedMotionEssentialFeedbackTests
    {
        [Test]
        public void Error_UnderReducedMotion_ShowsASolidTint_ThatNeverStops()
        {
            float totalMs = UIInteractionStateTokens.ErrorFlashDurationMs * UIInteractionStateTokens.ErrorFlashCount * 2f;

            InteractionVisual early = UIInteractionStateTokens.Resolve(
                InteractionStateFlags.Error, UIDesignTokens.FrameTier.Tier2Section,
                msSinceChange: 1f, reduceMotion: true);
            InteractionVisual wellPastWhereTheFlashSequenceWouldEnd = UIInteractionStateTokens.Resolve(
                InteractionStateFlags.Error, UIDesignTokens.FrameTier.Tier2Section,
                msSinceChange: totalMs + 10f, reduceMotion: true);

            Assert.IsNotNull(early.ErrorFlashTint, "Error feedback must still be visible under reduced motion.");
            Assert.IsNotNull(wellPastWhereTheFlashSequenceWouldEnd.ErrorFlashTint,
                "Unlike the normal flash-then-stop sequence, the reduced-motion tint must not disappear over time - " +
                "it is a static state indicator, not a decorative animation with an end.");
            Assert.AreEqual(early.ErrorFlashTint, wellPastWhereTheFlashSequenceWouldEnd.ErrorFlashTint,
                "The tint must be constant, not itself oscillating under reduced motion.");
        }
    }
}

using MyriadOfDragons.UI;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Relationship tests for the reduced-motion gate. They assert the decision table
    /// (play-mode x ReduceMotion), not any animation magnitude, so tuning the animations
    /// themselves never reads as a regression here.
    ///
    /// MotionPolicy.ReduceMotion is process-wide static state, so every test restores the default
    /// in TearDown to avoid leaking a setting into an unrelated test.
    /// </summary>
    public class MotionPolicyTests
    {
        [TearDown]
        public void TearDown()
        {
            MotionPolicy.ReduceMotion = false;
        }

        [Test]
        public void ReduceMotion_DefaultsToFalse()
        {
            Assert.IsFalse(MotionPolicy.ReduceMotion);
        }

        [Test]
        public void DecorativeMotion_PlaysInPlayMode_ByDefault()
        {
            Assert.IsTrue(MotionPolicy.ShouldPlayDecorativeMotion(isPlaying: true));
        }

        [Test]
        public void DecorativeMotion_IsSuppressedOutsidePlayMode()
        {
            // Coroutines cannot run outside Play mode, so decorative motion must be off there
            // whether or not ReduceMotion is set.
            Assert.IsFalse(MotionPolicy.ShouldPlayDecorativeMotion(isPlaying: false));

            MotionPolicy.ReduceMotion = true;
            Assert.IsFalse(MotionPolicy.ShouldPlayDecorativeMotion(isPlaying: false));
        }

        [Test]
        public void DecorativeMotion_IsSuppressedWhenReduceMotionOn_EvenInPlayMode()
        {
            MotionPolicy.ReduceMotion = true;
            Assert.IsFalse(MotionPolicy.ShouldPlayDecorativeMotion(isPlaying: true));
        }

        [Test]
        public void DecorativeMotion_TracksReduceMotion_NotLatched()
        {
            // The gate reflects the current setting on each call, so flipping the preference back
            // off re-enables motion in the same session.
            MotionPolicy.ReduceMotion = true;
            Assert.IsFalse(MotionPolicy.ShouldPlayDecorativeMotion(isPlaying: true));

            MotionPolicy.ReduceMotion = false;
            Assert.IsTrue(MotionPolicy.ShouldPlayDecorativeMotion(isPlaying: true));
        }
    }
}

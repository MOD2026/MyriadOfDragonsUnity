using NUnit.Framework;
using MyriadOfDragons.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>Relationship coverage for the pure range used by the real enemy health-bar
    /// damage-flash loop. These tests intentionally derive expectations from rendered segment
    /// counts rather than hardcoded damage thresholds.</summary>
    public class DamageFlashSegmentRangeTests
    {
        private static void AssertRange(int previousHp, int currentHp, int maxHp, int segments,
            bool expected, int expectedFirst = 0, int expectedEnd = 0)
        {
            bool actual = GameBootstrap.TryGetDamageFlashSegmentRange(previousHp, currentHp,
                maxHp, segments, out int first, out int end);
            Assert.AreEqual(expected, actual);
            Assert.AreEqual(expectedFirst, first);
            Assert.AreEqual(expectedEnd, end);
        }

        [Test]
        public void NoPreviousObservation_DoesNotFlash()
        {
            AssertRange(-1, 80, 100, 20, false);
        }

        [Test]
        public void UnchangedHp_DoesNotFlash()
        {
            AssertRange(80, 80, 100, 20, false);
        }

        [Test]
        public void Healing_DoesNotFlash()
        {
            AssertRange(40, 60, 100, 20, false);
        }

        [Test]
        public void OneSegmentLost_ReturnsOneSegmentHalfOpenRange()
        {
            AssertRange(100, 94, 100, 20, true, 19, 20);
        }

        [Test]
        public void MultipleContiguousSegmentsLost_ReturnsOnlyNewlyEmptyRange()
        {
            AssertRange(100, 70, 100, 20, true, 14, 20);
        }

        [Test]
        public void LethalTransition_ReturnsAllPreviouslyFilledSegments()
        {
            AssertRange(35, 0, 100, 20, true, 0, 7);
        }

        [Test]
        public void FullHealthInitialization_DoesNotFlash()
        {
            AssertRange(-1, 100, 100, 20, false);
        }

        [Test]
        public void ClampedOverkillValues_ReturnValidInBoundsRange()
        {
            AssertRange(100, -50, 100, 20, true, 0, 20);
        }

        [Test]
        public void InvalidBarDimensions_DoesNotFlash()
        {
            AssertRange(100, 50, 0, 20, false);
            AssertRange(100, 50, 100, 0, false);
        }

        [Test]
        public void VariableMaxHealth_UsesRenderedSegmentBoundaries()
        {
            int maxHp = 240;
            int segmentCount = 12;
            int previousHp = 200;
            int currentHp = 160;
            int previousFilled = GameBootstrap.ComputeFilledHealthSegments(previousHp, maxHp, segmentCount);
            int currentFilled = GameBootstrap.ComputeFilledHealthSegments(currentHp, maxHp, segmentCount);

            AssertRange(previousHp, currentHp, maxHp, segmentCount, currentFilled < previousFilled,
                currentFilled, previousFilled);
        }
    }
}

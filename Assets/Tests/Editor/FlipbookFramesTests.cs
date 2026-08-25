using MyriadOfDragons.Combat;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Real per-frame flipbook math (FlipbookFrames.UvRectForElapsed), the genuine AvatarStrike
    /// animation follow-up flagged in CombatPresentationBindingsTests' own class doc comment.
    /// Pure static method, zero MonoBehaviour - EditMode tests the real frame-selection logic
    /// directly, not through FlipbookRawImagePlayer's Update() timing shim.
    /// </summary>
    public class FlipbookFramesTests
    {
        private const int Columns = 4;
        private const int Rows = 4;
        private const float TotalMs = 150f;

        private static Rect ExpectedFrame(int frameIndex)
        {
            int col = frameIndex % Columns;
            int row = frameIndex / Columns;
            const float width = 1f / Columns;
            const float height = 1f / Rows;
            return new Rect(col * width, 1f - height - (row * height), width, height);
        }

        [Test]
        public void ElapsedZero_ReturnsFirstFrame_TopLeft()
        {
            Rect rect = FlipbookFrames.UvRectForElapsed(0f, TotalMs, Columns, Rows);
            // Real reference point from the already-authored bespoke_heavy.prefab's static
            // single-frame m_UVRect: {x: 0, y: 0.75, width: 0.25, height: 0.25}.
            Assert.AreEqual(0f, rect.x, 1e-5f);
            Assert.AreEqual(0.75f, rect.y, 1e-5f);
            Assert.AreEqual(0.25f, rect.width, 1e-5f);
            Assert.AreEqual(0.25f, rect.height, 1e-5f);
        }

        [Test]
        public void ElapsedAtTotalDuration_ReturnsLastFrame()
        {
            Rect rect = FlipbookFrames.UvRectForElapsed(TotalMs, TotalMs, Columns, Rows);
            Rect expected = ExpectedFrame(Columns * Rows - 1); // frame 15, bottom-right
            Assert.AreEqual(expected.x, rect.x, 1e-5f);
            Assert.AreEqual(expected.y, rect.y, 1e-5f);
        }

        [Test]
        public void ElapsedPastTotalDuration_ClampsToLastFrame_DoesNotWrap()
        {
            Rect atTotal = FlipbookFrames.UvRectForElapsed(TotalMs, TotalMs, Columns, Rows);
            Rect wayPast = FlipbookFrames.UvRectForElapsed(TotalMs * 50f, TotalMs, Columns, Rows);
            Assert.AreEqual(atTotal.x, wayPast.x, 1e-5f);
            Assert.AreEqual(atTotal.y, wayPast.y, 1e-5f);
        }

        [TestCase(0.0625f, 1)]   // just past the first frame boundary (1/16 of total)
        [TestCase(0.5f, 8)]      // exact midpoint - frame 8, row 2 col 0
        [TestCase(0.9999f, 15)]  // just before the end - still the last frame, not one before it
        public void MidpointElapsed_ReturnsCorrectFrame(float fraction, int expectedFrameIndex)
        {
            float elapsedMs = TotalMs * fraction;
            Rect rect = FlipbookFrames.UvRectForElapsed(elapsedMs, TotalMs, Columns, Rows);
            Rect expected = ExpectedFrame(expectedFrameIndex);
            Assert.AreEqual(expected.x, rect.x, 1e-4f, $"fraction={fraction}");
            Assert.AreEqual(expected.y, rect.y, 1e-4f, $"fraction={fraction}");
        }

        [Test]
        public void NegativeElapsed_ClampsToFirstFrame()
        {
            Rect rect = FlipbookFrames.UvRectForElapsed(-50f, TotalMs, Columns, Rows);
            Rect expected = ExpectedFrame(0);
            Assert.AreEqual(expected.x, rect.x, 1e-5f);
            Assert.AreEqual(expected.y, rect.y, 1e-5f);
        }

        [Test]
        public void NaNElapsed_ClampsToFirstFrame_NoException()
        {
            Rect rect = FlipbookFrames.UvRectForElapsed(float.NaN, TotalMs, Columns, Rows);
            Rect expected = ExpectedFrame(0);
            Assert.AreEqual(expected.x, rect.x, 1e-5f);
            Assert.AreEqual(expected.y, rect.y, 1e-5f);
        }

        [Test]
        public void NaNTotalDuration_DoesNotThrow_ReturnsFirstFrame()
        {
            Rect rect = FlipbookFrames.UvRectForElapsed(75f, float.NaN, Columns, Rows);
            Rect expected = ExpectedFrame(0);
            Assert.AreEqual(expected.x, rect.x, 1e-5f);
            Assert.AreEqual(expected.y, rect.y, 1e-5f);
        }

        [Test]
        public void ZeroOrNegativeTotalDuration_ReturnsFirstFrame_NoDivideByZeroException()
        {
            Rect zero = FlipbookFrames.UvRectForElapsed(50f, 0f, Columns, Rows);
            Rect negative = FlipbookFrames.UvRectForElapsed(50f, -10f, Columns, Rows);
            Rect expected = ExpectedFrame(0);
            Assert.AreEqual(expected.x, zero.x, 1e-5f);
            Assert.AreEqual(expected.x, negative.x, 1e-5f);
        }

        [Test]
        public void EveryFrameRect_StaysWithinUnitSquare_ForFullSweep()
        {
            // Real invariant: no frame's UV rect may ever reference outside the 0..1 texture
            // space, regardless of how elapsed/total are combined.
            for (int i = 0; i <= 32; i++)
            {
                float elapsedMs = TotalMs * i / 32f;
                Rect rect = FlipbookFrames.UvRectForElapsed(elapsedMs, TotalMs, Columns, Rows);
                Assert.GreaterOrEqual(rect.x, 0f);
                Assert.GreaterOrEqual(rect.y, 0f);
                Assert.LessOrEqual(rect.x + rect.width, 1f + 1e-5f);
                Assert.LessOrEqual(rect.y + rect.height, 1f + 1e-5f);
            }
        }

        [Test]
        public void NonSquareGrid_StillComputesCorrectFrame()
        {
            // Real defensive check: the method must not assume a square grid even though
            // AvatarStrike's real sheet is 4x4 - a 2x8 sheet's frame 5 is row 2, col 1.
            const int cols = 2;
            const int rows = 8;
            Rect rect = FlipbookFrames.UvRectForElapsed(0f, 100f, cols, rows);
            Assert.AreEqual(0f, rect.x, 1e-5f);
            Assert.AreEqual(1f - (1f / rows), rect.y, 1e-5f);
            Assert.AreEqual(1f / cols, rect.width, 1e-5f);
            Assert.AreEqual(1f / rows, rect.height, 1e-5f);
        }
    }
}

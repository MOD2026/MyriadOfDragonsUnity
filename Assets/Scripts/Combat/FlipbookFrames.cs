using UnityEngine;

namespace MyriadOfDragons.Combat
{
    /// <summary>
    /// Real per-frame flipbook animation math (CLAUDE.md non-negotiable #6: real logic in plain
    /// testable methods, MonoBehaviours supply only timing). Pure and static so it is fully
    /// EditMode testable with zero MonoBehaviour/RawImage involved - <see cref="FlipbookRawImagePlayer"/>
    /// is the thin timing shim that calls this every frame.
    /// </summary>
    public static class FlipbookFrames
    {
        /// <summary>The UV rect for whichever frame should be showing at <paramref name="elapsedMs"/>
        /// into a <paramref name="totalDurationMs"/> flipbook, laid out <paramref name="columns"/> x
        /// <paramref name="rows"/> (frame 0 = top-left, reading left-to-right then top-to-bottom -
        /// standard sprite-sheet order). NaN/negative-safe: a NaN or negative elapsed clamps to
        /// frame 0; elapsed at or past totalDurationMs clamps to the last frame, never wraps or goes
        /// out of range. Unity's UV v-axis is bottom-up (v=0 is the bottom of the texture), so row 0
        /// (the top row on screen) maps to the TOP of v-space, not v=0.</summary>
        public static Rect UvRectForElapsed(float elapsedMs, float totalDurationMs, int columns, int rows)
        {
            int safeColumns = Mathf.Max(1, columns);
            int safeRows = Mathf.Max(1, rows);
            int totalFrames = safeColumns * safeRows;
            float width = 1f / safeColumns;
            float height = 1f / safeRows;

            float safeElapsed = float.IsNaN(elapsedMs) ? 0f : Mathf.Max(0f, elapsedMs);
            float safeTotal = float.IsNaN(totalDurationMs) ? 0f : Mathf.Max(0f, totalDurationMs);

            int frameIndex;
            if (safeTotal <= 0f)
            {
                // No real duration to animate across (invalid/degenerate config) - show frame 0
                // rather than dividing by zero or guessing at a "finished" state.
                frameIndex = 0;
            }
            else
            {
                float progress = Mathf.Clamp01(safeElapsed / safeTotal);
                frameIndex = Mathf.Min(totalFrames - 1, Mathf.FloorToInt(progress * totalFrames));
            }

            int col = frameIndex % safeColumns;
            int row = frameIndex / safeColumns;

            float u = col * width;
            float v = 1f - height - (row * height);

            return new Rect(u, v, width, height);
        }
    }
}

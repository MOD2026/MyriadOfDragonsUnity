using System;

namespace MyriadOfDragons.Empire
{
    /// <summary>
    /// Locked Empire construction pacing curve (LOCKED_DECISIONS_REGISTER 2026-08-23 FINAL bands).
    /// Client-clock only — no speed-up purchase. Within each band, duration interpolates linearly
    /// by target level from band min → max (monotonic).
    /// </summary>
    public static class EmpireConstructionTimer
    {
        // (bandStartInclusive, bandEndInclusive, minSeconds, maxSeconds)
        private static readonly (int lo, int hi, int minSec, int maxSec)[] Bands =
        {
            (1, 5, 30 * 60, 60 * 60),                 // 30–60 min
            (6, 10, 4 * 3600, 8 * 3600),              // 4–8 h
            (11, 15, 1 * 86400, 3 * 86400),           // 1–3 d
            (16, 20, 4 * 86400, 8 * 86400),           // 4–8 d
            (21, 25, 8 * 86400, 12 * 86400),          // 8–12 d
            (26, 30, 7 * 86400, 14 * 86400),          // 7–14 d
        };

        /// <summary>Test seam — null means wall clock.</summary>
        public static long? UtcNowMsOverrideForTests;

        public static long UtcNowMs =>
            UtcNowMsOverrideForTests ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public static void ClearTestClock() => UtcNowMsOverrideForTests = null;

        /// <summary>Seconds of build time for a project whose <paramref name="targetLevel"/> is in 1..30.</summary>
        public static int DurationSecondsForTargetLevel(int targetLevel)
        {
            if (targetLevel < 1 || targetLevel > 30)
                return 0;

            foreach ((int lo, int hi, int minSec, int maxSec) in Bands)
            {
                if (targetLevel < lo || targetLevel > hi)
                    continue;

                if (hi == lo)
                    return minSec;

                double t = (targetLevel - lo) / (double)(hi - lo);
                return (int)Math.Round(minSec + t * (maxSec - minSec));
            }

            return 0;
        }

        public static string FormatDuration(int durationSeconds)
        {
            if (durationSeconds <= 0)
                return "0s";

            if (durationSeconds < 3600)
            {
                int minutes = Math.Max(1, (durationSeconds + 59) / 60);
                return $"{minutes}m";
            }

            if (durationSeconds < 86400)
            {
                double hours = durationSeconds / 3600.0;
                return hours >= 10 ? $"{hours:0}h" : $"{hours:0.#}h";
            }

            double days = durationSeconds / 86400.0;
            return days >= 10 ? $"{days:0}d" : $"{days:0.#}d";
        }

        public static string FormatRemaining(long endsAtUtcMs, long nowUtcMs)
        {
            long remainingMs = endsAtUtcMs - nowUtcMs;
            if (remainingMs <= 0)
                return "ready";
            int seconds = (int)Math.Ceiling(remainingMs / 1000.0);
            return FormatDuration(seconds);
        }
    }
}

using System;

namespace MyriadOfDragons.Empire
{
    /// <summary>
    /// The three deterministic daily trials of the Solo Collection Circuit.
    ///
    /// Deliberately an enum rather than loose ints: the claim guard is a bitmask, and a bare int
    /// index would make "trial 2" mean different things in the persistence and the UI the first
    /// time someone reorders the list.
    /// </summary>
    public enum SoloCircuitTrial
    {
        Formation = 0,
        Collection = 1,
        TacticalBrief = 2,
    }

    /// <summary>
    /// Persisted state for the Solo Collection Circuit.
    ///
    /// A single nested serializable object rather than seven loose fields on PlayerProfile, which
    /// is the same shape CollectionMaterialWallet already uses. That matters for the frozen-file
    /// sign-off: the ask is ONE additive field, not seven, and future Circuit state lands here
    /// without touching PlayerProfile again.
    ///
    /// Every default below is the correct reading for a save that predates this feature - empty
    /// keys mean "no circuit run yet", so no migration step is required.
    /// </summary>
    [Serializable]
    public class SoloCircuitProgress
    {
        /// <summary>UTC day the counters below belong to, "yyyy-MM-dd". Empty = never run.</summary>
        public string dayKeyUtc = string.Empty;

        /// <summary>Highest UTC day key ever observed. The rollback guard - see
        /// SoloCollectionCircuit.EnsureCurrentDay. Never decreases.</summary>
        public string highWaterDayKeyUtc = string.Empty;

        /// <summary>Bitmask of SoloCircuitTrial values already cleared TODAY (1|2|4).</summary>
        public int trialsClearedTodayMask = 0;

        /// <summary>Gold granted by the Circuit today. Exists to enforce the daily cap explicitly
        /// rather than trusting the per-trial arithmetic to add up.</summary>
        public int goldEarnedTodayUtc = 0;

        /// <summary>Construction Materials granted by the Circuit today. Same reason as
        /// goldEarnedTodayUtc - the cap is enforced explicitly rather than inferred.
        ///
        /// REPLACED Avatar XP (BS option (b), 2026-08-26). XP was counted for this daily cap and
        /// then DISCARDED - PlayerProfile has no XP field and nothing consumed it, so every trial
        /// clear was "granting" into a void. Materials has a real field (constructionMaterials,
        /// 2026-08-24) and a real sink, so the reward now actually arrives.</summary>
        public int materialsEarnedTodayUtc = 0;

        /// <summary>UTC day the current personal cycle began, "yyyy-MM-dd". Empty = no cycle.
        /// Replaces the old ISO-week key (LOCKED 2026-08-26): weeks began Monday, so a player who
        /// joined on any later day could not reach 7 circuits that week at all. The cycle now
        /// starts on the player's own first completed Circuit, so the join day stops mattering.</summary>
        public string cycleStartDayKeyUtc = string.Empty;

        /// <summary>Most recent UTC day on which all three trials were cleared. Used to tell a
        /// continued cycle from a broken one - the two differ by exactly one calendar day.</summary>
        public string lastCircuitDayKeyUtc = string.Empty;

        /// <summary>Completed Circuit days in the current cycle (1..7).</summary>
        public int circuitDaysInCycle = 0;

        /// <summary>True once the current cycle's bonus has been paid. One-time per cycle.</summary>
        public bool cycleBonusClaimed = false;
    }

    /// <summary>Outcome of a trial clear. Carries the refusal reasons explicitly so a caller can
    /// show the player why nothing was granted, instead of a silent no-op.</summary>
    public struct SoloCircuitClearResult
    {
        public bool Cleared;
        public SoloCircuitTrial Trial;
        public int GoldGranted;
        public int MaterialsGranted;
        public bool CompletedAllThreeToday;
        public int EventMedalsGranted;
        public bool CycleBonusPaid;
        public string Message;
    }

    /// <summary>
    /// Solo Collection Circuit - UTC-seeded daily trials, first-clear-per-day per trial,
    /// retryable on fail.
    ///
    /// Real logic in a plain testable class per CLAUDE.md non-negotiable #6. Nothing here reads the
    /// clock on its own: every entry point takes nowUtc, so a test can drive a rollback or a week
    /// boundary without touching the machine clock. It never saves - the caller persists, the same
    /// contract ShopLoyaltyService and TacticalPuzzleSlate use.
    ///
    /// REWARD RULES (locked): 250 Gold + 50 Construction Materials per trial clear; +500 Gold +1 Event Medal for
    /// clearing all 3 the same day; +2,500 Gold +125 Materials for 7 completed circuits in a PERSONAL
    /// 7-DAY CYCLE (LOCKED 2026-08-26, replacing a Monday-aligned ISO week that made the bonus
    /// unreachable for anyone who joined mid-week). Hard daily ceiling of 1,250 Gold / 150 Materials, enforced as its own check rather than
    /// inferred from the per-trial numbers. Nothing else is ever granted - no cards, packs, Forge
    /// Dust, Permits, Evolution materials or Market Credits - which keeps the Circuit out of the
    /// acquisition path, the same constraint the loyalty ladder carries.
    /// </summary>
    public static class SoloCollectionCircuit
    {
        public const int GoldPerTrialClear = 250;
        public const int MaterialsPerTrialClear = 50;
        public const int GoldForAllThreeSameDay = 500;
        public const int EventMedalsForAllThreeSameDay = 1;
        public const int GoldForSevenCircuitCycle = 2500;
        /// <summary>NOT SPECIFIED by BS's ruling, which covered the per-trial conversion only.
        /// Derived at the SAME 5x ratio as the specified 10 XP -> 50 Materials conversion, so the
        /// cycle bonus keeps its proportion to a trial clear. Flagged rather than silently chosen -
        /// if BS wants a different figure this is the one line to change.</summary>
        public const int MaterialsForSevenCircuitCycle = 125;
        public const int CircuitsRequiredForCycleBonus = 7;
        public const int CycleLengthDays = 7;

        /// <summary>Hard per-day ceilings. Stated as constants and checked directly, because the
        /// spec caps them explicitly - "don't let stacking exceed it" is a separate requirement
        /// from the per-trial amounts happening to sum correctly today.</summary>
        public const int MaxGoldPerDay = 1250;
        /// <summary>Also derived at the same 5x ratio (30 XP -> 150 Materials), for the same
        /// reason and with the same caveat.</summary>
        public const int MaxMaterialsPerDay = 150;

        public static string UtcDayKey(DateTime nowUtc) => nowUtc.ToString("yyyy-MM-dd");

        /// <summary>Parses a "yyyy-MM-dd" day key back to a date, or null if it is empty or
        /// malformed (an old save, or a hand-edited one).</summary>
        public static DateTime? ParseDayKey(string dayKeyUtc)
        {
            if (string.IsNullOrEmpty(dayKeyUtc)) return null;
            if (DateTime.TryParseExact(
                    dayKeyUtc, "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal |
                    System.Globalization.DateTimeStyles.AssumeUniversal,
                    out DateTime parsed))
            {
                return parsed.Date;
            }

            return null;
        }

        /// <summary>True when <paramref name="dayKeyUtc"/> is the calendar day immediately after
        /// <paramref name="previousDayKeyUtc"/>. Real date arithmetic, not string adjacency - the
        /// month and year boundaries are exactly where a naive check would break.</summary>
        public static bool IsNextCalendarDay(string previousDayKeyUtc, string dayKeyUtc)
        {
            DateTime? previous = ParseDayKey(previousDayKeyUtc);
            DateTime? current = ParseDayKey(dayKeyUtc);
            if (previous == null || current == null) return false;
            return (current.Value - previous.Value).TotalDays == 1.0;
        }

        /// <summary>
        /// Rolls the daily counters forward, and REFUSES to roll them backward.
        ///
        /// The plain key-mismatch reset every other daily system uses (see
        /// EmpireExpeditionDailyReset) is NOT sufficient here. That shape resets whenever the
        /// stored key differs from the current one - including when the current one is EARLIER,
        /// which is exactly what a client clock rollback produces. A player could clear all three
        /// trials, set the clock back a day, and clear them again for full rewards, indefinitely.
        ///
        /// So the guard is a high-water mark: a day key below the highest ever seen is treated as
        /// a rollback and the day is NOT reset - today's claims stay spent. The spec's wording is
        /// "clock rollback must INVALIDATE the claim attempt, never re-grant", and refusing the
        /// reset is what makes that true. Day keys are "yyyy-MM-dd", so ordinal string comparison
        /// is chronological.
        /// </summary>
        public static bool EnsureCurrentDay(SoloCircuitProgress progress, DateTime nowUtc)
        {
            if (progress == null) return false;

            string today = UtcDayKey(nowUtc);

            if (!string.IsNullOrEmpty(progress.highWaterDayKeyUtc) &&
                string.CompareOrdinal(today, progress.highWaterDayKeyUtc) < 0)
            {
                // Rolled back. Leave every counter exactly where it is.
                return false;
            }

            if (progress.dayKeyUtc != today)
            {
                progress.dayKeyUtc = today;
                progress.trialsClearedTodayMask = 0;
                progress.goldEarnedTodayUtc = 0;
                progress.materialsEarnedTodayUtc = 0;
            }

            progress.highWaterDayKeyUtc = today;

            // No cycle bookkeeping here on purpose. A cycle advances only when a Circuit is
            // actually COMPLETED, so it is settled in RecordClear rather than on every read - a
            // player who opens the app and clears nothing must not disturb their own streak.
            return true;
        }

        public static bool IsTrialClearedToday(SoloCircuitProgress progress, SoloCircuitTrial trial) =>
            progress != null && (progress.trialsClearedTodayMask & MaskOf(trial)) != 0;

        public static bool AllThreeClearedToday(SoloCircuitProgress progress) =>
            progress != null && (progress.trialsClearedTodayMask & AllTrialsMask) == AllTrialsMask;

        private const int AllTrialsMask = 1 | 2 | 4;

        private static int MaskOf(SoloCircuitTrial trial) => 1 << (int)trial;

        /// <summary>
        /// Records a first-clear of one trial today and returns what was granted.
        ///
        /// Retryable-on-fail lives in the caller: this is only ever called on a real clear. A
        /// second call for the same trial on the same day is refused, which is what makes the
        /// trial first-clear-per-day rather than farmable.
        /// </summary>
        public static SoloCircuitClearResult RecordClear(
            SoloCircuitProgress progress, SoloCircuitTrial trial, DateTime nowUtc)
        {
            var result = new SoloCircuitClearResult { Trial = trial };
            if (progress == null)
            {
                result.Message = "No circuit progress.";
                return result;
            }

            if (!EnsureCurrentDay(progress, nowUtc))
            {
                result.Message =
                    "Clock rollback detected (" + UtcDayKey(nowUtc) + " is before " +
                    progress.highWaterDayKeyUtc + "). The claim is invalidated, not re-granted.";
                return result;
            }

            if (IsTrialClearedToday(progress, trial))
            {
                result.Message = trial + " is already cleared today - first clear per day only.";
                return result;
            }

            progress.trialsClearedTodayMask |= MaskOf(trial);
            result.Cleared = true;

            GrantCapped(progress, GoldPerTrialClear, MaterialsPerTrialClear, ref result);

            if (AllThreeClearedToday(progress))
            {
                result.CompletedAllThreeToday = true;
                GrantCapped(progress, GoldForAllThreeSameDay, 0, ref result);
                result.EventMedalsGranted += EventMedalsForAllThreeSameDay;

                AdvanceCycle(progress, UtcDayKey(nowUtc));

                if (!progress.cycleBonusClaimed &&
                    progress.circuitDaysInCycle >= CircuitsRequiredForCycleBonus)
                {
                    progress.cycleBonusClaimed = true;
                    result.CycleBonusPaid = true;
                    GrantCapped(progress, GoldForSevenCircuitCycle, MaterialsForSevenCircuitCycle, ref result);
                }
            }

            result.Message = "Cleared " + trial + ".";
            return result;
        }

        /// <summary>
        /// Extends the personal cycle, or starts a fresh one.
        ///
        /// A completed Circuit on the calendar day immediately after the last one continues the
        /// cycle; anything else starts a new cycle at day 1. That is what "a missed day ends the
        /// cycle" means in practice - the cycle is a 7-consecutive-day streak that begins whenever
        /// the player's first Circuit lands, so the join day no longer decides whether the bonus is
        /// reachable at all.
        ///
        /// Same day twice cannot happen (each trial is first-clear-per-day, so all-three fires once
        /// per day), but it is guarded anyway rather than relying on a caller invariant that a
        /// future UI could break.
        /// </summary>
        private static void AdvanceCycle(SoloCircuitProgress progress, string today)
        {
            if (progress.lastCircuitDayKeyUtc == today) return;

            if (IsNextCalendarDay(progress.lastCircuitDayKeyUtc, today))
            {
                progress.circuitDaysInCycle++;
            }
            else
            {
                progress.cycleStartDayKeyUtc = today;
                progress.circuitDaysInCycle = 1;
                progress.cycleBonusClaimed = false;
            }

            progress.lastCircuitDayKeyUtc = today;
        }

        /// <summary>
        /// Adds a grant, clipped to whatever the day's ceiling still allows.
        ///
        /// The weekly bonus is why this clips rather than asserts: 3 trials (750) + the all-three
        /// bonus (500) is exactly 1,250, so on the one day a week the 2,500-Gold weekly bonus also
        /// lands, the uncapped total would be 3,750. The cap is the locked number, so the surplus
        /// is clipped and simply not granted.
        /// </summary>
        private static void GrantCapped(
            SoloCircuitProgress progress, int gold, int materials, ref SoloCircuitClearResult result)
        {
            int goldRoom = Math.Max(0, MaxGoldPerDay - progress.goldEarnedTodayUtc);
            int goldPaid = Math.Min(Math.Max(0, gold), goldRoom);
            progress.goldEarnedTodayUtc += goldPaid;
            result.GoldGranted += goldPaid;

            int materialsRoom = Math.Max(0, MaxMaterialsPerDay - progress.materialsEarnedTodayUtc);
            int materialsPaid = Math.Min(Math.Max(0, materials), materialsRoom);
            progress.materialsEarnedTodayUtc += materialsPaid;
            result.MaterialsGranted += materialsPaid;
        }
    }
}

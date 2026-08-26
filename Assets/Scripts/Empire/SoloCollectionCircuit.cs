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

        /// <summary>Avatar XP granted by the Circuit today. Same reason as goldEarnedTodayUtc.</summary>
        public int avatarXpEarnedTodayUtc = 0;

        /// <summary>ISO-ish UTC week key the weekly counters belong to. Empty = never run.</summary>
        public string weekKeyUtc = string.Empty;

        /// <summary>Days this UTC week on which all three trials were cleared.</summary>
        public int completedCircuitsThisWeek = 0;

        /// <summary>True once this week's 7-circuit bonus has been paid. One-time per week.</summary>
        public bool weeklyBonusClaimed = false;
    }

    /// <summary>Outcome of a trial clear. Carries the refusal reasons explicitly so a caller can
    /// show the player why nothing was granted, instead of a silent no-op.</summary>
    public struct SoloCircuitClearResult
    {
        public bool Cleared;
        public SoloCircuitTrial Trial;
        public int GoldGranted;
        public int AvatarXpGranted;
        public bool CompletedAllThreeToday;
        public int EventMedalsGranted;
        public bool WeeklyBonusPaid;
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
    /// REWARD RULES (locked): 250 Gold + 10 Avatar XP per trial clear; +500 Gold +1 Event Medal for
    /// clearing all 3 the same day; +2,500 Gold +25 Avatar XP for 7 completed circuits in the UTC
    /// week. Hard daily ceiling of 1,250 Gold / 30 Avatar XP, enforced as its own check rather than
    /// inferred from the per-trial numbers. Nothing else is ever granted - no cards, packs, Forge
    /// Dust, Permits, Evolution materials or Market Credits - which keeps the Circuit out of the
    /// acquisition path, the same constraint the loyalty ladder carries.
    /// </summary>
    public static class SoloCollectionCircuit
    {
        public const int GoldPerTrialClear = 250;
        public const int AvatarXpPerTrialClear = 10;
        public const int GoldForAllThreeSameDay = 500;
        public const int EventMedalsForAllThreeSameDay = 1;
        public const int GoldForSevenCircuitWeek = 2500;
        public const int AvatarXpForSevenCircuitWeek = 25;
        public const int CircuitsRequiredForWeeklyBonus = 7;

        /// <summary>Hard per-day ceilings. Stated as constants and checked directly, because the
        /// spec caps them explicitly - "don't let stacking exceed it" is a separate requirement
        /// from the per-trial amounts happening to sum correctly today.</summary>
        public const int MaxGoldPerDay = 1250;
        public const int MaxAvatarXpPerDay = 30;

        public static string UtcDayKey(DateTime nowUtc) => nowUtc.ToString("yyyy-MM-dd");

        /// <summary>UTC week key as "yyyy-Www". Uses the ISO-8601 week so a week boundary lands on
        /// Monday rather than "seven days after whenever the player started", matching how
        /// ascensionPermitWeekKey already partitions the shared weekly Permit budget.</summary>
        public static string UtcWeekKey(DateTime nowUtc)
        {
            var cal = System.Globalization.ISOWeek.GetYear(nowUtc);
            int week = System.Globalization.ISOWeek.GetWeekOfYear(nowUtc);
            return cal.ToString("D4") + "-W" + week.ToString("D2");
        }

        /// <summary>
        /// Rolls the daily and weekly counters forward, and REFUSES to roll them backward.
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
                progress.avatarXpEarnedTodayUtc = 0;
            }

            progress.highWaterDayKeyUtc = today;

            string week = UtcWeekKey(nowUtc);
            if (progress.weekKeyUtc != week)
            {
                progress.weekKeyUtc = week;
                progress.completedCircuitsThisWeek = 0;
                progress.weeklyBonusClaimed = false;
            }

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

            GrantCapped(progress, GoldPerTrialClear, AvatarXpPerTrialClear, ref result);

            if (AllThreeClearedToday(progress))
            {
                result.CompletedAllThreeToday = true;
                GrantCapped(progress, GoldForAllThreeSameDay, 0, ref result);
                result.EventMedalsGranted += EventMedalsForAllThreeSameDay;

                progress.completedCircuitsThisWeek++;

                if (!progress.weeklyBonusClaimed &&
                    progress.completedCircuitsThisWeek >= CircuitsRequiredForWeeklyBonus)
                {
                    progress.weeklyBonusClaimed = true;
                    result.WeeklyBonusPaid = true;
                    GrantCapped(progress, GoldForSevenCircuitWeek, AvatarXpForSevenCircuitWeek, ref result);
                }
            }

            result.Message = "Cleared " + trial + ".";
            return result;
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
            SoloCircuitProgress progress, int gold, int avatarXp, ref SoloCircuitClearResult result)
        {
            int goldRoom = Math.Max(0, MaxGoldPerDay - progress.goldEarnedTodayUtc);
            int goldPaid = Math.Min(Math.Max(0, gold), goldRoom);
            progress.goldEarnedTodayUtc += goldPaid;
            result.GoldGranted += goldPaid;

            int xpRoom = Math.Max(0, MaxAvatarXpPerDay - progress.avatarXpEarnedTodayUtc);
            int xpPaid = Math.Min(Math.Max(0, avatarXp), xpRoom);
            progress.avatarXpEarnedTodayUtc += xpPaid;
            result.AvatarXpGranted += xpPaid;
        }
    }
}

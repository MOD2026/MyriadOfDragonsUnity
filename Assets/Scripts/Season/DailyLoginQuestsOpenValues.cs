namespace MyriadOfDragons.Season
{
    public enum DailyLoginQuestClaimStatus
    {
        Applied,
        OpenValuesNotLocked,
    }

    public sealed class DailyLoginQuestClaimResult
    {
        public DailyLoginQuestClaimStatus Status;
        public string Message;
        public int SlotIndex;
    }

    /// <summary>
    /// Daily Login + Daily Quests — LOCKED 2026-08-24 structure only
    /// (docs/LOCKED_DECISIONS_REGISTER.md). Streak pauses (does not reset). 3 quests / UTC day.
    /// Login/quest reward amounts stay OPEN. No PlayerProfile streak fields yet (frozen save).
    /// </summary>
    public static class DailyLoginQuestsOpenValues
    {
        public const string RegisterCitation =
            "docs/LOCKED_DECISIONS_REGISTER.md — Daily Login + Daily Quests (LOCKED 2026-08-24, structure only)";

        /// <summary>Locked player-facing streak rule — matches Daily Login Quests Screen V1 copy.</summary>
        public const string StreakPausedCopy = "PAUSED — STREAK NOT RESET";

        /// <summary>Locked: 3 daily quests per UTC day.</summary>
        public const int DailyQuestSlots = 3;

        /// <summary>Visual login wells from the V1 shell — streak length is not a locked number.</summary>
        public const int ShellLoginWellCount = 6;

        /// <summary>OPEN — Gold/Materials/Stamina/Avatar XP/Event Medals/Pass XP per login node.</summary>
        public static readonly int? LoginRewardAmount = null;

        /// <summary>OPEN — per-quest reward amounts.</summary>
        public static readonly int? QuestRewardAmount = null;

        public static bool AreRewardsConfigured => LoginRewardAmount.HasValue && QuestRewardAmount.HasValue;

        public static string RuntimePlaceholder => "[runtime]";

        public static string StatusNote =>
            "Daily Login / Quests structure is locked (streak pauses and is not reset; 3 quests/UTC day; " +
            "Empire/Avatar-side rewards only). Reward amounts are still OPEN — " + RegisterCitation;

        public static DailyLoginQuestClaimResult TryClaimLogin(int wellIndex)
        {
            return new DailyLoginQuestClaimResult
            {
                SlotIndex = wellIndex,
                Status = DailyLoginQuestClaimStatus.OpenValuesNotLocked,
                Message = StatusNote,
            };
        }

        public static DailyLoginQuestClaimResult TryClaimQuest(int questIndex)
        {
            return new DailyLoginQuestClaimResult
            {
                SlotIndex = questIndex,
                Status = DailyLoginQuestClaimStatus.OpenValuesNotLocked,
                Message = StatusNote,
            };
        }
    }
}

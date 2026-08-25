namespace MyriadOfDragons.Season
{
    public enum DailyLoginQuestClaimStatus
    {
        Applied,
        AlreadyClaimed,
        NotComplete,
        InvalidProfile,
        /// <summary>Legacy refuse while reward magnitudes were OPEN — kept for old call sites.</summary>
        OpenValuesNotLocked,
    }

    public sealed class DailyLoginQuestClaimResult
    {
        public DailyLoginQuestClaimStatus Status;
        public string Message;
        public int SlotIndex;
        public int GoldGranted;
        public int MaterialsGranted;
        public int StaminaGranted;
        public int EventMedalsGranted;
        public int PassSeasonXpGranted;
    }

    /// <summary>
    /// Daily Login + Daily Quests — LOCKED 2026-08-24 structure
    /// (docs/LOCKED_DECISIONS_REGISTER.md). Streak pauses (does not reset). 3 quests / UTC day.
    /// Claim routing lives in <see cref="DailyLoginQuestsService"/>.
    /// </summary>
    public static class DailyLoginQuestsOpenValues
    {
        public const string RegisterCitation =
            "docs/LOCKED_DECISIONS_REGISTER.md — Daily Login + Daily Quests (LOCKED 2026-08-24, structure only)";

        /// <summary>Locked player-facing streak rule — matches Daily Login Quests Screen V1 copy.</summary>
        public const string StreakPausedCopy = "PAUSED — STREAK NOT RESET";

        /// <summary>Locked: 3 daily quests per UTC day.</summary>
        public const int DailyQuestSlots = DailyLoginQuestsService.QuestSlotCount;

        /// <summary>Visual login wells from the V1 shell — streak length is not a locked number.</summary>
        public const int ShellLoginWellCount = DailyLoginQuestsService.LoginWellCount;

        /// <summary>Phase-1 bounded login gold at tier 0 (service owns the full tier table).</summary>
        public static readonly int? LoginRewardAmount = DailyLoginQuestsService.LoginGoldBase;

        /// <summary>Phase-1 bounded quest gold grant.</summary>
        public static readonly int? QuestRewardAmount = DailyLoginQuestsService.QuestGold;

        public static bool AreRewardsConfigured => LoginRewardAmount.HasValue && QuestRewardAmount.HasValue;

        public static string RuntimePlaceholder => "[runtime]";

        public static string StatusNote =>
            "Daily Login / Quests live (streak pauses and is not reset; 3 quests/UTC day; " +
            "Empire/Avatar-side rewards only). " + RegisterCitation;

        public static DailyLoginQuestClaimResult TryClaimLogin(int wellIndex) =>
            DailyLoginQuestsService.ClaimLogin(
                MyriadOfDragons.Data.SaveManager.SaveData,
                System.DateTime.UtcNow);

        public static DailyLoginQuestClaimResult TryClaimQuest(int questIndex) =>
            DailyLoginQuestsService.ClaimQuest(
                MyriadOfDragons.Data.SaveManager.SaveData,
                questIndex,
                System.DateTime.UtcNow);
    }
}

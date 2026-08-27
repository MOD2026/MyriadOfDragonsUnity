namespace MyriadOfDragons.Metagame
{
    public enum MemoryExpeditionActionStatus
    {
        Applied,
        OpenValuesNotLocked,
    }

    public sealed class MemoryExpeditionActionResult
    {
        public MemoryExpeditionActionStatus Status;
        public int Index;
        public string Message;
    }

    /// <summary>
    /// MEMORY EXPEDITION art shell — structure/art only. Numbers and backend routes stay OPEN;
    /// production actions refuse. No PlayerProfile fields.
    /// </summary>
    public static class MemoryExpeditionOpenValues
    {
        public static string RuntimePlaceholder => "[runtime]";
        /// <summary>Short, player-facing status copy for this shell's status label. Deliberately
        /// separate from <see cref="StatusNote"/>: that string is a developer/transaction
        /// diagnostic consumed as the ActionResult Message, and at full length it overflows the
        /// band it is shown in. StatusNote stays byte-identical for every Message consumer; only
        /// the label reads this. Carries no design numbers - a shell line must not become a
        /// second source for live values. Same split as BattlePassOpenValues.PlayerStatus
        /// (02eb9f8) and the three shells in 7e14ab5.</summary>
        public static string PlayerStatus => "Memory Expedition isn't live yet.";

        public static string StatusNote =>
            "Memory Expedition route-choice shell is art-ready. Route outcomes, reward quantities, and daily entry remain OPEN/server-owned — no recommended route, no invented rewards.";

        public const int RouteCount = 3;
        public static readonly int? DailyEntryCost = null;
        public static readonly int? RewardGold = null;
        public static bool AreRewardsConfigured => DailyEntryCost.HasValue && RewardGold.HasValue;

        public static MemoryExpeditionActionResult TrySelectRoute(int routeIndex) => new MemoryExpeditionActionResult
        {
            Status = MemoryExpeditionActionStatus.OpenValuesNotLocked,
            Index = routeIndex,
            Message = StatusNote,
        };

    }
}

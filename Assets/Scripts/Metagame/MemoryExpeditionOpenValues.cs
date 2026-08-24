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

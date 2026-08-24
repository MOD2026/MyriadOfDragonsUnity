namespace MyriadOfDragons.Metagame
{
    public enum VipSubscriptionActionStatus
    {
        Applied,
        OpenValuesNotLocked,
    }

    public sealed class VipSubscriptionActionResult
    {
        public VipSubscriptionActionStatus Status;
        public int Index;
        public string Message;
    }

    /// <summary>
    /// VIP / Subscription art shell — convenience-only. Prices, durations, entitlements,
    /// and store outcomes stay OPEN/server-owned. Production subscribe/restore refuse.
    /// No PlayerProfile fields. Must not imply combat power or exclusive progression.
    /// </summary>
    public static class VipSubscriptionOpenValues
    {
        public static string RuntimePlaceholder => "[runtime]";
        public static string StatusNote =>
            "VIP / Subscription shell is art-ready (identity, six benefit wells, milestone strip). " +
            "Prices, durations, renewal, and store entitlements stay OPEN/server-owned — refuse local purchase truth.";

        public const int BenefitWellCount = 6;
        public const int StateSocketCount = 3;
        public static readonly int? WeeklyGemPrice = null;
        public static readonly int? FortnightGemPrice = null;
        public static readonly int? MonthlyGemPrice = null;
        public static bool ArePricesConfigured =>
            WeeklyGemPrice.HasValue && FortnightGemPrice.HasValue && MonthlyGemPrice.HasValue;

        public static VipSubscriptionActionResult TrySubscribe() => Refuse(-1);
        public static VipSubscriptionActionResult TryRestore() => Refuse(-2);

        private static VipSubscriptionActionResult Refuse(int index) => new VipSubscriptionActionResult
        {
            Status = VipSubscriptionActionStatus.OpenValuesNotLocked,
            Index = index,
            Message = StatusNote,
        };
    }
}

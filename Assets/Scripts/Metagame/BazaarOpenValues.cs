namespace MyriadOfDragons.Metagame
{
    public enum BazaarActionStatus
    {
        Applied,
        OpenValuesNotLocked,
    }

    public sealed class BazaarActionResult
    {
        public BazaarActionStatus Status;
        public int Index;
        public string Message;
    }

    /// <summary>
    /// BAZAAR art shell — structure/art only. Numbers and backend routes stay OPEN;
    /// production actions refuse. No PlayerProfile fields.
    /// </summary>
    public static class BazaarOpenValues
    {
        public static string RuntimePlaceholder => "[runtime]";
        public static string StatusNote =>
            "Bazaar shell is art-ready (Browse/Sell/My Listings/Wallet). Listing queries, prices, tax, holds, and Market Credit balances stay OPEN/server-owned — refuse local truth.";

        public const int ShellListingWellCount = 6;
        public static readonly int? ListingQueryPageSize = null;
        public static readonly int? MarketCreditBalance = null;
        public static bool AreTransactionsConfigured => ListingQueryPageSize.HasValue && MarketCreditBalance.HasValue;

        public static BazaarActionResult TrySelectTab(int tabIndex) => Refuse(tabIndex);
        public static BazaarActionResult TrySelectListing(int wellIndex) => Refuse(wellIndex);
        public static BazaarActionResult TryConfirmTransaction() => Refuse(-1);

        private static BazaarActionResult Refuse(int index) => new BazaarActionResult
        {
            Status = BazaarActionStatus.OpenValuesNotLocked,
            Index = index,
            Message = StatusNote,
        };

    }
}

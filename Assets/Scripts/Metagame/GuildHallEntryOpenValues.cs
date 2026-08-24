namespace MyriadOfDragons.Metagame
{
    public enum GuildHallEntryStatus
    {
        Applied,
        OpenValuesNotLocked,
    }

    public sealed class GuildHallEntryResult
    {
        public GuildHallEntryStatus Status;
        public int Index;
        public string Message;
    }

    /// <summary>
    /// GUILD HALL art shell — structure/art only. Numbers and backend routes stay OPEN;
    /// production actions refuse. No PlayerProfile fields.
    /// </summary>
    public static class GuildHallEntryOpenValues
    {
        public static string RuntimePlaceholder => "[runtime]";
        public static string StatusNote =>
            "Guild Hall flat-entry popup is art-ready. Find/Create/Enter/Retry routes stay OPEN until social backend eligibility is locked — no upgrade ladder, no invented guild state.";

        public static readonly int? FindCreateRouteEnabled = null;
        public static bool AreEntryRoutesConfigured => FindCreateRouteEnabled.HasValue;
        public static string FlatNonUpgradeCopy => "FLAT ENTRY — NO UPGRADE LADDER";

        public static GuildHallEntryResult TryEntryAction() => new GuildHallEntryResult
        {
            Status = GuildHallEntryStatus.OpenValuesNotLocked,
            Message = StatusNote,
        };

    }
}

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
        /// <summary>Short, player-facing status copy for the shell's StatusLine. Deliberately
        /// separate from <see cref="StatusNote"/>: that string is a developer/transaction
        /// diagnostic consumed as the ActionResult Message, and at full length it overflows the
        /// StatusLine band. StatusNote stays byte-identical for every Message consumer; only the
        /// label reads this. Carries no design numbers - a shell line must not become a second
        /// source for live values. Same split as BattlePassOpenValues.PlayerStatus (02eb9f8).</summary>
        public static string PlayerStatus => "Guild features aren't live yet.";

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

namespace MyriadOfDragons.Metagame
{
    public enum FriendsActionStatus
    {
        Applied,
        OpenValuesNotLocked,
    }

    public sealed class FriendsActionResult
    {
        public FriendsActionStatus Status;
        public int Index;
        public string Message;
    }

    /// <summary>
    /// FRIENDS art shell — structure/art only. Numbers and backend routes stay OPEN;
    /// production actions refuse. No PlayerProfile fields.
    /// </summary>
    public static class FriendsOpenValues
    {
        public static string RuntimePlaceholder => "[runtime]";
        /// <summary>Short, player-facing status copy for this shell's status label. Deliberately
        /// separate from <see cref="StatusNote"/>: that string is a developer/transaction
        /// diagnostic consumed as the ActionResult Message, and at full length it overflows the
        /// band it is shown in. StatusNote stays byte-identical for every Message consumer; only
        /// the label reads this. Carries no design numbers - a shell line must not become a
        /// second source for live values. Same split as BattlePassOpenValues.PlayerStatus
        /// (02eb9f8) and the three shells in 7e14ab5.</summary>
        public static string PlayerStatus => "Friends features aren't live yet.";

        public static string StatusNote =>
            "Friends shell is art-ready. Search/discovery, relationship limits, and presence policy stay OPEN/backend-owned — no gifting, no stamina sharing, public display name only.";

        public const int VisibleRowCount = 6;
        public static readonly int? RelationshipLimit = null;
        public static bool AreSocialRoutesConfigured => RelationshipLimit.HasValue;

        public static FriendsActionResult TrySelectTab(int index) => Refuse(index);
        public static FriendsActionResult TrySelectFriend(int row) => Refuse(row);
        public static FriendsActionResult TryMessage() => Refuse(-1);

        private static FriendsActionResult Refuse(int index) => new FriendsActionResult
        {
            Status = FriendsActionStatus.OpenValuesNotLocked,
            Index = index,
            Message = StatusNote,
        };

    }
}

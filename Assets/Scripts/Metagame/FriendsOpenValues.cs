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

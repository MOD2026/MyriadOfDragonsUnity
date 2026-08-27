namespace MyriadOfDragons.Metagame
{
    public enum ChatSocialActionStatus
    {
        Applied,
        OpenValuesNotLocked,
    }

    public sealed class ChatSocialActionResult
    {
        public ChatSocialActionStatus Status;
        public int Index;
        public string Message;
    }

    /// <summary>
    /// CHAT art shell — structure/art only. Numbers and backend routes stay OPEN;
    /// production actions refuse. No PlayerProfile fields.
    /// </summary>
    public static class ChatSocialOpenValues
    {
        public static string RuntimePlaceholder => "[runtime]";
        /// <summary>Short, player-facing status copy for this shell's status label. Deliberately
        /// separate from <see cref="StatusNote"/>: that string is a developer/transaction
        /// diagnostic consumed as the ActionResult Message, and at full length it overflows the
        /// band it is shown in. StatusNote stays byte-identical for every Message consumer; only
        /// the label reads this. Carries no design numbers - a shell line must not become a
        /// second source for live values. Same split as BattlePassOpenValues.PlayerStatus
        /// (02eb9f8) and the three shells in 7e14ab5.</summary>
        public static string PlayerStatus => "Chat isn't live yet.";

        public static string StatusNote =>
            "Chat / Social shell is art-ready (7 channels + DM chrome). Message send, unread counts, and moderation routes stay OPEN — Broadcast/System remain read-only; Trading cannot confirm Bazaar.";

        public static readonly string[] ChannelLabels =
            { "Guild", "Global", "Trading", "Event", "Broadcast", "System", "DM" };
        public static readonly int? MaxMessageLength = null;
        public static bool AreChatRoutesConfigured => MaxMessageLength.HasValue;

        public static ChatSocialActionResult TrySelectChannel(int index) => Refuse(index);
        public static ChatSocialActionResult TrySendMessage() => Refuse(-1);

        private static ChatSocialActionResult Refuse(int index) => new ChatSocialActionResult
        {
            Status = ChatSocialActionStatus.OpenValuesNotLocked,
            Index = index,
            Message = StatusNote,
        };

    }
}

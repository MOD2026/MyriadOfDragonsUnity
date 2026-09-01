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
        /// <summary>Chat authority contract version this client is bound to.
        /// See <c>docs/CHAT_AUTHORITY_CONTRACT_2026-09-01.md</c>. Bump only when that document's
        /// own contractVersion is bumped by BE.</summary>
        public const int ChatContractVersion = 1;

        /// <summary>AUTHORITATIVE per contractVersion 1. Traces to the real server constant
        /// <c>ChatOperations.MaxMessageLength</c> (<c>CloudCode/Chat/ChatOperations.cs:21</c>),
        /// which is enforced server-side on every PostMessage call today. This is the ONLY place
        /// the number lives on the client - the presenter reads it, never a literal, so a future
        /// contract bump is a one-line change here. Over-length posts are rejected server-side
        /// with the generic <c>INVALID_REQUEST</c> code (the contract is explicit that there is no
        /// dedicated MESSAGE_TOO_LONG code today).</summary>
        public static readonly int? MaxMessageLength = 500;

        /// <summary>AUTHORITATIVE per contractVersion 1: <c>ChatOperations.MaxStoredMessagesPerChannel</c>
        /// (<c>CloudCode/Chat/ChatOperations.cs:20</c>). Retention, NOT per-message length - a
        /// channel keeps at most this many messages server-side. Published for pagination/UX
        /// planning; nothing presents it to the player, and no copy describing it is approved.</summary>
        public static readonly int MaxStoredMessagesPerChannel = 200;

        /// <summary>Deliberately NOT derived from <see cref="MaxMessageLength"/> any more.
        /// A published length limit says nothing about whether the routes are configured, and the
        /// authority contract §2 is explicit that ACL is UNSUPPORTED in production: ChatOperations
        /// is composed with <c>AllowAllChatAccessControl</c>, which returns Allowed=true for every
        /// request, and no channel rule is consulted at all. Binding the limit must not silently
        /// flip this flag to "configured".</summary>
        public static bool AreChatRoutesConfigured => false;

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

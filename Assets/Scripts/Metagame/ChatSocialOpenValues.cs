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

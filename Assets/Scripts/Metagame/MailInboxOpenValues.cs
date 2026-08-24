namespace MyriadOfDragons.Metagame
{
    public enum MailInboxActionStatus
    {
        Applied,
        OpenValuesNotLocked,
    }

    public sealed class MailInboxActionResult
    {
        public MailInboxActionStatus Status;
        public int Index;
        public string Message;
    }

    /// <summary>
    /// MAIL art shell — structure/art only. Numbers and backend routes stay OPEN;
    /// production actions refuse. No PlayerProfile fields.
    /// </summary>
    public static class MailInboxOpenValues
    {
        public static string RuntimePlaceholder => "[runtime]";
        public static string StatusNote =>
            "Mail / Inbox shell is art-ready. Attachment claim, retention, and message classes stay OPEN/server-authored — no player-to-player mail, no duplicate receipt while reconciling.";

        public const int VisibleRowCount = 6;
        public static readonly int? AttachmentClaimEnabled = null;
        public static bool AreClaimsConfigured => AttachmentClaimEnabled.HasValue;

        public static MailInboxActionResult TrySelectMessage(int row) => Refuse(row);
        public static MailInboxActionResult TryClaimAttachment() => Refuse(-1);

        private static MailInboxActionResult Refuse(int index) => new MailInboxActionResult
        {
            Status = MailInboxActionStatus.OpenValuesNotLocked,
            Index = index,
            Message = StatusNote,
        };

    }
}

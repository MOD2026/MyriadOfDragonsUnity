using System;

namespace MyriadOfDragons.Frontier
{
    /// <summary>
    /// Beta Mail client DTOs, adapted to the real, published server contract: CloudCode/Mail
    /// (MyriadOfDragons.CloudCode.Mail), module MailModule (BE c2e7831b). A separate CloudCode
    /// module from CC10Frontier - its own module name, its own result base with NO
    /// serverUtcMs/stateVersion/authorityGeneration/receipt (the real wire shape carries none of
    /// those), and no CC10Request-style requestId/expectedStateVersion envelope on any request.
    /// Field names are camelCase copies of the server's [JsonProperty] names.
    ///
    /// Deliberately absent, per the approved scope (BE c2e7831b, WH-CC11-042): attachments,
    /// rewards (gold/materials/gems), player-to-player mail, threads/replies, delete/restore, and
    /// reporting. FetchMailInbox/FetchMailDetail/MarkMailRead are the only three endpoints - a
    /// fourth function on the real module would be a contract violation, not a client gap.
    /// </summary>
    public static class MailEndpoints
    {
        public const string ModuleName = "Mail";
        public const string FetchMailInbox = "FetchMailInbox";
        public const string FetchMailDetail = "FetchMailDetail";
        public const string MarkMailRead = "MarkMailRead";
    }

    public static class MailErrors
    {
        public const string AuthenticationRequired = "AUTHENTICATION_REQUIRED";
        public const string InvalidRequest = "INVALID_REQUEST";
        public const string MailNotFound = "MAIL_NOT_FOUND";
        /// <summary>Server-owned expiry only - this client never computes "is this expired"
        /// itself from expiresUtcMs; it only ever reacts to this code or to a message's absence
        /// from a freshly loaded inbox.</summary>
        public const string MailExpired = "MAIL_EXPIRED";
        public const string Conflict = "CONFLICT";
        public const string StorageUnavailable = "STORAGE_UNAVAILABLE";
        public const string RateLimited = "RATE_LIMITED";
    }

    [Serializable]
    public class MailResult
    {
        public bool success;
        public string errorCode = string.Empty;
    }

    [Serializable]
    public class MailSummaryDto
    {
        public string messageId = string.Empty;
        public string kind = string.Empty;
        public string subject = string.Empty;
        public long createdUtcMs;
        public long expiresUtcMs;
        public bool read;
    }

    /// <summary>FetchMailDetail's message shape. Not a subclass of MailSummaryDto client-side
    /// (JsonUtility field binding does not need it) - a flat mirror of the same fields plus body,
    /// matching every other DTO's convention in this codebase.</summary>
    [Serializable]
    public sealed class MailDetailDto
    {
        public string messageId = string.Empty;
        public string kind = string.Empty;
        public string subject = string.Empty;
        public string body = string.Empty;
        public long createdUtcMs;
        public long expiresUtcMs;
        public bool read;
    }

    [Serializable]
    public sealed class FetchMailInboxResult : MailResult
    {
        public MailSummaryDto[] messages = Array.Empty<MailSummaryDto>();
        public int unreadCount;
    }

    [Serializable]
    public sealed class FetchMailDetailResult : MailResult
    {
        public MailDetailDto message;
    }

    [Serializable]
    public sealed class MarkMailReadResult : MailResult
    {
        public string messageId;
        public bool alreadyRead;
    }
}

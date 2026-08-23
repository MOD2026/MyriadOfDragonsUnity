using System;
using System.Collections.Generic;

namespace MyriadOfDragons.Social
{
    [Serializable]
    public class AccountIdentity
    {
        public string accountId;
        public string displayName;
        public long createdAtUtcMs;
        public long lastSeenAtUtcMs;
        public string status;
    }

    [Serializable]
    public class GuildIdentity
    {
        public string guildId;
        public string guildName;
        public string ownerAccountId;
        public long createdAtUtcMs;
        public int memberCount;
        public string status;
    }

    [Serializable]
    public class GuildMembership
    {
        public string membershipId;
        public string accountId;
        public string guildId;
        public string roleId;
        public long joinedAtUtcMs;
        public string status;
    }

    [Serializable]
    public class GuildRole
    {
        public string roleId;
        public string guildId;
        public string roleName;
        public GuildPermissions permissions;
        public bool isDefault;
    }

    [Flags]
    public enum GuildPermissions
    {
        None = 0,
        InviteMembers = 1 << 0,
        RemoveMembers = 1 << 1,
        AssignRoles = 1 << 2,
        EditGuildDetails = 1 << 3,
        SendChatMessages = 1 << 4,
        ModerateChat = 1 << 5,
        DisbandGuild = 1 << 6,
        All = InviteMembers | RemoveMembers | AssignRoles | EditGuildDetails | SendChatMessages | ModerateChat | DisbandGuild,
    }

    [Serializable]
    public class GuildChatChannel
    {
        public string channelId;
        public string guildId;
        public string channelType;
        public string title;
        public long createdAtUtcMs;
        public string status;
    }

    [Serializable]
    public class ChatMessage
    {
        public string messageId;
        public string channelId;
        public string accountId;
        public string body;
        public long createdAtUtcMs;
        public long editedAtUtcMs;
        public long deletedAtUtcMs;
        public ModerationStatus moderationStatus;
    }

    [Serializable]
    public class DirectMessagePrivacySettings
    {
        public DirectMessagePrivacyMode whoMaySendRequest;
        public bool unrestrictedTextEligible;
        public bool presetMessageOnlyEligible;
        public bool r4R5AuthorityCannotBypass;
    }

    public enum DirectMessagePrivacyMode
    {
        Nobody = 0,
        GuildMembersOnly = 1,
        AcceptedContactsOnly = 2,
    }

    public enum DirectMessageFailureReason
    {
        None = 0,
        Duplicate = 1,
        RateLimited = 2,
        Blocked = 3,
        InvalidState = 4,
        InvalidRequest = 5,
        NotAllowed = 6,
        Unknown = 7,
    }

    public enum DirectMessageRequestStatus
    {
        None = 0,
        Pending = 1,
        Accepted = 2,
        Rejected = 3,
        Cancelled = 4,
        Ignored = 5,
    }

    public enum DirectMessageMessageState
    {
        Sent = 0,
        Edited = 1,
        Deleted = 2,
        Moderated = 3,
    }

    [Serializable]
    public class DirectMessageRequest
    {
        public string requestId;
        public string senderAccountId;
        public string recipientAccountId;
        public string text;
        public DirectMessageRequestStatus status;
        public long createdAtUtcMs;
        public long updatedAtUtcMs;
        public long reviewedAtUtcMs;
        public DirectMessageFailureReason failureReason;
    }

    [Serializable]
    public class DirectMessageConversation
    {
        public string conversationId;
        public string[] participantAccountIds;
        public long createdAtUtcMs;
        public long lastMessageAtUtcMs;
        public bool isMuted;
        public bool hiddenForRequester;
    }

    [Serializable]
    public class DirectMessageMessage
    {
        public string messageId;
        public string conversationId;
        public string senderAccountId;
        public string body;
        public string presetMessageId;
        public long createdAtUtcMs;
        public long editedAtUtcMs;
        public long deletedAtUtcMs;
        public long moderatedAtUtcMs;
        public DirectMessageMessageState state;
        public string moderationReason;
    }

    [Serializable]
    public class GetDirectMessagePrivacySettingsRequest
    {
    }

    [Serializable]
    public class DirectMessagePrivacySettingsResult
    {
        public DirectMessagePrivacySettings settings;
    }

    [Serializable]
    public class ListDirectMessageRequestsRequest
    {
        public bool incomingOnly;
        public int pageSize;
        public int pageNumber;
        public long requestedAtUtcMs;
    }

    [Serializable]
    public class ListDirectMessageRequestsResult
    {
        public List<DirectMessageRequest> requests;
        public int pageSize;
        public int pageNumber;
        public bool hasMore;
    }

    [Serializable]
    public class CreateDirectMessageRequestRequest
    {
        public string targetAccountId;
        public string text;
        public long requestedAtUtcMs;
    }

    [Serializable]
    public class CreateDirectMessageRequestResult
    {
        public DirectMessageRequest request;
    }

    [Serializable]
    public class AcceptDirectMessageRequestRequest
    {
        public string requestId;
        public long requestedAtUtcMs;
    }

    [Serializable]
    public class AcceptDirectMessageRequestResult
    {
        public DirectMessageRequest request;
    }

    [Serializable]
    public class RejectDirectMessageRequestRequest
    {
        public string requestId;
        public long requestedAtUtcMs;
    }

    [Serializable]
    public class RejectDirectMessageRequestResult
    {
        public DirectMessageRequest request;
    }

    [Serializable]
    public class CancelDirectMessageRequestRequest
    {
        public string requestId;
        public long requestedAtUtcMs;
    }

    [Serializable]
    public class CancelDirectMessageRequestResult
    {
        public DirectMessageRequest request;
    }

    [Serializable]
    public class ListDirectMessageConversationsRequest
    {
        public int pageSize;
        public int pageNumber;
        public bool includeHidden;
        public long requestedAtUtcMs;
    }

    [Serializable]
    public class ListDirectMessageConversationsResult
    {
        public List<DirectMessageConversation> conversations;
        public int pageSize;
        public int pageNumber;
        public bool hasMore;
    }

    [Serializable]
    public class FetchDirectMessageHistoryRequest
    {
        public string conversationId;
        public int pageSize;
        public int pageNumber;
        public long requestedAtUtcMs;
    }

    [Serializable]
    public class FetchDirectMessageHistoryResult
    {
        public string conversationId;
        public List<DirectMessageMessage> messages;
        public int pageSize;
        public int pageNumber;
        public bool hasMore;
    }

    [Serializable]
    public class SendDirectMessageRequest
    {
        public string conversationId;
        public string body;
        public string presetMessageId;
        public long requestedAtUtcMs;
    }

    [Serializable]
    public class SendDirectMessageResult
    {
        public DirectMessageMessage message;
    }

    [Serializable]
    public class MuteDirectMessageConversationRequest
    {
        public string conversationId;
        public long requestedAtUtcMs;
    }

    [Serializable]
    public class MuteDirectMessageConversationResult
    {
        public DirectMessageConversation conversation;
        public bool muted;
    }

    [Serializable]
    public class UnmuteDirectMessageConversationRequest
    {
        public string conversationId;
        public long requestedAtUtcMs;
    }

    [Serializable]
    public class UnmuteDirectMessageConversationResult
    {
        public DirectMessageConversation conversation;
        public bool unmuted;
    }

    [Serializable]
    public class HideDirectMessageConversationRequest
    {
        public string conversationId;
        public bool hidden;
        public long requestedAtUtcMs;
    }

    [Serializable]
    public class HideDirectMessageConversationResult
    {
        public DirectMessageConversation conversation;
        public bool hidden;
    }

    [Serializable]
    public class DirectMessageRetentionPolicy
    {
        public string policyId;
        public string policyName;
        public int retentionDays;
        public bool perUserHideDoesNotDeleteOtherParticipantHistory;
        public bool moderationEvidenceRetentionEnabled;
        public long createdAtUtcMs;
        public long updatedAtUtcMs;
        public long retentionCutoffUtcMs;
    }

    [Serializable]
    public class GuildChatRetentionPolicy
    {
        public int retentionDays;
        public bool enabled;
        public string policyName;
        public long updatedAtUtcMs;
    }

    [Serializable]
    public class BlockRecord
    {
        public string blockId;
        public string blockerAccountId;
        public string blockedAccountId;
        public long createdAtUtcMs;
        public string status;
    }

    [Serializable]
    public class MuteRecord
    {
        public string muteId;
        public string muterAccountId;
        public string mutedAccountId;
        public long createdAtUtcMs;
        public long muteUntilUtcMs;
        public string status;
    }

    [Serializable]
    public class ReportSubmission
    {
        public string reportId;
        public string reporterAccountId;
        public string targetAccountId;
        public string reportReason;
        public long createdAtUtcMs;
        public ModerationStatus moderationStatus;
    }

    public enum ModerationStatus
    {
        None,
        Pending,
        Reviewed,
        Actioned,
        Dismissed,
    }

    public enum ChatModerationAction
    {
        None,
        DeleteMessage,
        WarnUser,
        MuteUser,
        RemoveFromGuild,
    }

    public enum ReportResolution
    {
        None,
        Dismissed,
        Accepted,
        Actioned,
        Escalated,
    }

    [Serializable]
    public class BootstrapIdentityRequest
    {
        public string displayName;
        public long requestedAtUtcMs;
    }

    [Serializable]
    public class BootstrapIdentityResult
    {
        public AccountIdentity account;
        public bool created;
    }

    [Serializable]
    public class CreateGuildRequest
    {
        public string guildName;
        public string initialChannelName;
        public long requestedAtUtcMs;
    }

    [Serializable]
    public class CreateGuildResult
    {
        public GuildIdentity guild;
        public GuildMembership membership;
        public GuildChatChannel channel;
    }

    [Serializable]
    public class JoinGuildRequest
    {
        public string guildId;
        public long requestedAtUtcMs;
    }

    [Serializable]
    public class JoinGuildResult
    {
        public GuildMembership membership;
    }

    [Serializable]
    public class LeaveGuildRequest
    {
        public string membershipId;
        public long requestedAtUtcMs;
    }

    [Serializable]
    public class LeaveGuildResult
    {
        public bool left;
    }

    [Serializable]
    public class FetchMembershipRequest
    {
        public long requestedAtUtcMs;
    }

    [Serializable]
    public class FetchMembershipResult
    {
        public List<GuildMembership> memberships;
    }

    [Serializable]
    public class FetchChatHistoryRequest
    {
        public string guildId;
        public string channelId;
        public int pageSize;
        public int pageNumber;
        public long requestedAtUtcMs;
    }

    [Serializable]
    public class FetchChatHistoryResult
    {
        public List<ChatMessage> messages;
        public int pageSize;
        public int pageNumber;
        public bool hasMore;
    }

    [Serializable]
    public class SendChatMessageRequest
    {
        public string guildId;
        public string channelId;
        public string body;
        public long requestedAtUtcMs;
    }

    [Serializable]
    public class SendChatMessageResult
    {
        public ChatMessage message;
    }

    [Serializable]
    public class BlockAccountRequest
    {
        public string blockedAccountId;
        public long requestedAtUtcMs;
    }

    [Serializable]
    public class BlockAccountResult
    {
        public BlockRecord block;
    }

    [Serializable]
    public class UnblockAccountRequest
    {
        public string blockedAccountId;
        public long requestedAtUtcMs;
    }

    [Serializable]
    public class UnblockAccountResult
    {
        public bool unblocked;
    }

    [Serializable]
    public class MuteAccountRequest
    {
        public string mutedAccountId;
        public long requestedAtUtcMs;
    }

    [Serializable]
    public class MuteAccountResult
    {
        public MuteRecord mute;
    }

    [Serializable]
    public class UnmuteAccountRequest
    {
        public string mutedAccountId;
        public long requestedAtUtcMs;
    }

    [Serializable]
    public class UnmuteAccountResult
    {
        public bool unmuted;
    }

    [Serializable]
    public class SubmitReportRequest
    {
        public string targetAccountId;
        public string reportReason;
        public long requestedAtUtcMs;
    }

    [Serializable]
    public class SubmitReportResult
    {
        public ReportSubmission report;
    }

    [Serializable]
    public class TransferGuildOwnershipRequest
    {
        public string guildId;
        public string destinationAccountId;
        public long requestedAtUtcMs;
    }

    [Serializable]
    public class TransferGuildOwnershipResult
    {
        public GuildIdentity guild;
        public GuildMembership newOwnerMembership;
        public bool transferred;
    }

    [Serializable]
    public class RemoveGuildMemberRequest
    {
        public string guildId;
        public string membershipId;
        public long requestedAtUtcMs;
    }

    [Serializable]
    public class RemoveGuildMemberResult
    {
        public bool removed;
        public GuildMembership membership;
    }

    [Serializable]
    public class AssignGuildRoleRequest
    {
        public string guildId;
        public string membershipId;
        public string roleId;
        public long requestedAtUtcMs;
    }

    [Serializable]
    public class AssignGuildRoleResult
    {
        public GuildMembership membership;
        public GuildRole role;
        public bool assigned;
    }

    [Serializable]
    public class DisbandGuildRequest
    {
        public string guildId;
        public long requestedAtUtcMs;
    }

    [Serializable]
    public class DisbandGuildResult
    {
        public bool disbanded;
        public GuildIdentity guild;
    }

    [Serializable]
    public class ModerateChatMessageRequest
    {
        public string guildId;
        public string channelId;
        public string messageId;
        public ChatModerationAction action;
        public long requestedAtUtcMs;
    }

    [Serializable]
    public class ModerateChatMessageResult
    {
        public ChatMessage message;
        public bool moderated;
        public ChatModerationAction action;
    }

    [Serializable]
    public class ReviewReportRequest
    {
        public string reportId;
        public ReportResolution resolution;
        public long requestedAtUtcMs;
    }

    [Serializable]
    public class ReviewReportResult
    {
        public ReportSubmission report;
        public bool reviewed;
        public ReportResolution resolution;
    }
}

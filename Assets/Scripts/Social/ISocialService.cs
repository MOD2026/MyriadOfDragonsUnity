using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyriadOfDragons.Social
{
    /// <summary>
    /// Provider-neutral social service boundary for guild, chat, moderation, and account safety operations.
    /// Every implementation must derive the acting account exclusively from authenticated session context.
    /// Caller-supplied resource identifiers are untrusted input.
    /// The trusted service must verify that every referenced guild, membership, role, channel, message, and report exists.
    /// The trusted service must verify that membershipId and roleId belong to the specified guildId.
    /// The trusted service must verify that channelId belongs to guildId.
    /// The trusted service must verify that messageId belongs to channelId and therefore to the specified guild.
    /// The trusted service must verify that a report and its target resources belong to the correct moderation scope.
    /// The trusted service must reject cross-guild and cross-channel resource combinations.
    /// The trusted service must reject assigning any role whose permissions exceed the authenticated actor's effective permissions.
    /// The trusted service must reject self-promotion or any role assignment that would increase the actor's permissions beyond their current authority.
    /// The trusted service must prevent removal of the current guild owner through RemoveGuildMemberAsync.
    /// The trusted service must prevent the owner from leaving through LeaveGuildAsync until ownership is transferred or the guild is disbanded.
    /// Guild ownership grants full authority independently of roleId.
    /// All relationship and privilege checks are enforced in the trusted service, regardless of client validation.
    /// Expected failure conditions must return typed Authentication, Authorization, Validation, Conflict, or Unavailable failures instead of leaking internal exceptions.
    /// No method may infer authenticated identity from display names or target/resource identifiers.
    /// </summary>
    public interface ISocialService
    {
        Task<SocialResult<BootstrapIdentityResult>> BootstrapIdentityAsync(BootstrapIdentityRequest request, CancellationToken cancellationToken);
        Task<SocialResult<CreateGuildResult>> CreateGuildAsync(CreateGuildRequest request, CancellationToken cancellationToken);
        Task<SocialResult<JoinGuildResult>> JoinGuildAsync(JoinGuildRequest request, CancellationToken cancellationToken);
        // The current guild owner cannot leave until ownership is transferred or the guild is disbanded.
        Task<SocialResult<LeaveGuildResult>> LeaveGuildAsync(LeaveGuildRequest request, CancellationToken cancellationToken);
        Task<SocialResult<FetchMembershipResult>> FetchMembershipAsync(FetchMembershipRequest request, CancellationToken cancellationToken);
        Task<SocialResult<FetchChatHistoryResult>> FetchChatHistoryAsync(FetchChatHistoryRequest request, CancellationToken cancellationToken);
        Task<SocialResult<SendChatMessageResult>> SendChatMessageAsync(SendChatMessageRequest request, CancellationToken cancellationToken);
        Task<SocialResult<BlockAccountResult>> BlockAccountAsync(BlockAccountRequest request, CancellationToken cancellationToken);
        Task<SocialResult<UnblockAccountResult>> UnblockAccountAsync(UnblockAccountRequest request, CancellationToken cancellationToken);
        Task<SocialResult<MuteAccountResult>> MuteAccountAsync(MuteAccountRequest request, CancellationToken cancellationToken);
        Task<SocialResult<UnmuteAccountResult>> UnmuteAccountAsync(UnmuteAccountRequest request, CancellationToken cancellationToken);
        Task<SocialResult<SubmitReportResult>> SubmitReportAsync(SubmitReportRequest request, CancellationToken cancellationToken);
        Task<SocialResult<TransferGuildOwnershipResult>> TransferGuildOwnershipAsync(TransferGuildOwnershipRequest request, CancellationToken cancellationToken);
        // The current guild owner cannot be removed through this operation.
        Task<SocialResult<RemoveGuildMemberResult>> RemoveGuildMemberAsync(RemoveGuildMemberRequest request, CancellationToken cancellationToken);
        // Role assignment must target the same guild and must not allow self-escalation beyond the actor's current authority.
        Task<SocialResult<AssignGuildRoleResult>> AssignGuildRoleAsync(AssignGuildRoleRequest request, CancellationToken cancellationToken);
        Task<SocialResult<DisbandGuildResult>> DisbandGuildAsync(DisbandGuildRequest request, CancellationToken cancellationToken);
        // ModerateChatMessageAsync must validate the guild -> channel -> message relationship before applying moderation.
        Task<SocialResult<ModerateChatMessageResult>> ModerateChatMessageAsync(ModerateChatMessageRequest request, CancellationToken cancellationToken);
        // ReviewReportAsync may only transition a report within an authorized moderation context.
        Task<SocialResult<ReviewReportResult>> ReviewReportAsync(ReviewReportRequest request, CancellationToken cancellationToken);

        // Phase 1 direct messaging foundation.
        // The acting account is derived only from trusted authenticated session context and never from request DTOs.
        // Every request must be verified against conversation membership, relationship state, account eligibility, and moderation/privacy rules in the trusted service.
        Task<SocialResult<DirectMessagePrivacySettingsResult>> GetDirectMessagePrivacySettingsAsync(GetDirectMessagePrivacySettingsRequest request, CancellationToken cancellationToken);
        Task<SocialResult<CreateDirectMessageRequestResult>> CreateDirectMessageRequestAsync(CreateDirectMessageRequestRequest request, CancellationToken cancellationToken);
        Task<SocialResult<ListDirectMessageRequestsResult>> ListDirectMessageRequestsAsync(ListDirectMessageRequestsRequest request, CancellationToken cancellationToken);
        Task<SocialResult<AcceptDirectMessageRequestResult>> AcceptDirectMessageRequestAsync(AcceptDirectMessageRequestRequest request, CancellationToken cancellationToken);
        Task<SocialResult<RejectDirectMessageRequestResult>> RejectDirectMessageRequestAsync(RejectDirectMessageRequestRequest request, CancellationToken cancellationToken);
        Task<SocialResult<CancelDirectMessageRequestResult>> CancelDirectMessageRequestAsync(CancelDirectMessageRequestRequest request, CancellationToken cancellationToken);
        Task<SocialResult<ListDirectMessageConversationsResult>> ListDirectMessageConversationsAsync(ListDirectMessageConversationsRequest request, CancellationToken cancellationToken);
        Task<SocialResult<FetchDirectMessageHistoryResult>> FetchDirectMessageHistoryAsync(FetchDirectMessageHistoryRequest request, CancellationToken cancellationToken);
        Task<SocialResult<SendDirectMessageResult>> SendDirectMessageAsync(SendDirectMessageRequest request, CancellationToken cancellationToken);
        Task<SocialResult<MuteDirectMessageConversationResult>> MuteDirectMessageConversationAsync(MuteDirectMessageConversationRequest request, CancellationToken cancellationToken);
        Task<SocialResult<UnmuteDirectMessageConversationResult>> UnmuteDirectMessageConversationAsync(UnmuteDirectMessageConversationRequest request, CancellationToken cancellationToken);
        Task<SocialResult<HideDirectMessageConversationResult>> HideDirectMessageConversationAsync(HideDirectMessageConversationRequest request, CancellationToken cancellationToken);
    }

    [Serializable]
    public class SocialResult<T>
    {
        public bool Success;
        public T Data;
        public SocialFailure Failure;
    }

    [Serializable]
    public class SocialFailure
    {
        public SocialFailureCategory Category;
        public string Message;
        public string Detail;
    }

    public enum SocialFailureCategory
    {
        None,
        Authentication,
        Authorization,
        Validation,
        Conflict,
        RateLimit,
        Unavailable,
        Unknown,
    }
}

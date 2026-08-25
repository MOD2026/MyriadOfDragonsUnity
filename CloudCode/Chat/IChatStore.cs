using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace MyriadOfDragons.CloudCode.Chat;

/// <summary>Storage abstraction for Chat channel history and the SocialSafety read this module
/// needs to filter it. <see cref="CloudSaveChatStore"/> is the real implementation.
/// `Chat.ServerTests` exercises the full business logic against an in-memory reference
/// implementation of this same interface, matching the SocialSafety/Bazaar/Friends pattern.</summary>
public interface IChatStore
{
    Task<ChatChannelState> LoadChannelAsync(IExecutionContext context, IGameApiClient apiClient, string channelId);
    Task SaveChannelAsync(IExecutionContext context, IGameApiClient apiClient, string channelId, ChatChannelState state);

    /// <summary>Returns the subset of <paramref name="candidateSenderAccountIds"/> that
    /// <paramref name="actorAccountId"/> has currently blocked or actively muted (mute checked
    /// against <paramref name="nowUtcMs"/>, an expired mute does not filter), reading the SAME
    /// player-scoped Cloud Save records SocialSafety itself writes under the actor's own account -
    /// never another account's data, since the actor here is always the caller filtering their
    /// own view.</summary>
    Task<HashSet<string>> LoadBlockedOrMutedSendersAsync(IExecutionContext context, IGameApiClient apiClient, string actorAccountId, IReadOnlyList<string> candidateSenderAccountIds, long nowUtcMs);
}

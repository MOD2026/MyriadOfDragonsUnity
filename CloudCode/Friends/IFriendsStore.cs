using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace MyriadOfDragons.CloudCode.Friends;

/// <summary>Storage abstraction for the Friends graph. <see cref="CloudSaveFriendsStore"/> is the
/// real implementation. `Friends.ServerTests` exercises the full business logic against an
/// in-memory reference implementation of this same interface, matching the pattern
/// SocialSafety/Bazaar already use.</summary>
public interface IFriendsStore
{
    Task<FriendshipRecord?> LoadFriendshipAsync(IExecutionContext context, IGameApiClient apiClient, string accountA, string accountB);
    Task SaveFriendshipAsync(IExecutionContext context, IGameApiClient apiClient, string accountA, string accountB, FriendshipRecord record);
    Task DeleteFriendshipAsync(IExecutionContext context, IGameApiClient apiClient, string accountA, string accountB);

    Task<FriendsIndexState> LoadIndexAsync(IExecutionContext context, IGameApiClient apiClient, string accountId);
    Task SaveIndexAsync(IExecutionContext context, IGameApiClient apiClient, string accountId, FriendsIndexState index);

    /// <summary>Server-issued pseudonymous alias for a counterpart account - never the raw account
    /// id. Stable per account, same mechanism as Bazaar's LoadSellerAliasAsync/
    /// CloudCode/Chat's sender-alias resolution. Owner-authorized contract decision
    /// (2026-09-01): accepted-friend UI uses this alias, not the accepted account's raw id and not
    /// its free-text display name (which has no server-side cross-account resolution mechanism at
    /// all and is unvalidated user-supplied text) - see FriendSummary's own doc comment.</summary>
    Task<string?> LoadCounterpartAliasAsync(IExecutionContext context, IGameApiClient apiClient, string counterpartAccountId);
    Task SaveCounterpartAliasAsync(IExecutionContext context, IGameApiClient apiClient, string counterpartAccountId, string aliasId);

    /// <summary>TASK 020 (CC6-BE-FRIEND-GIFT-ROUTING-020): server-internal reverse index
    /// (alias -&gt; real account id), written once alongside the forward alias at creation time.
    /// Used ONLY inside FriendsOperations to resolve a client-supplied alias back to a real
    /// account for an already-established relationship (accept/decline/remove/gift) - never
    /// exposed through any client-facing DTO or CloudCodeFunction return type. This is what makes
    /// the alias a genuinely routable identifier without ever handing the client a raw account id:
    /// the resolution happens entirely server-side.</summary>
    Task<string?> ResolveAccountIdFromAliasAsync(IExecutionContext context, IGameApiClient apiClient, string aliasId);
    Task SaveAliasReverseMappingAsync(IExecutionContext context, IGameApiClient apiClient, string aliasId, string accountId);
}

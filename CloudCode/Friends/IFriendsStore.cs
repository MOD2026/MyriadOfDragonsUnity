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
}

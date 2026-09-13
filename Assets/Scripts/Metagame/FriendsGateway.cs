using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;

namespace MyriadOfDragons.Metagame
{
    [Serializable]
    public sealed class FriendGatewayResult
    {
        public bool success;
        public string status;
        public string errorCode;
    }

    [Serializable]
    public sealed class FriendSummaryDto
    {
        public string counterpartAliasId;
        public string status;
        public bool isOutgoingRequest;
        public bool canGiftToday;
        public long createdUtcMs;
    }

    [Serializable]
    public sealed class ListFriendsGatewayResult
    {
        public bool success;
        public List<FriendSummaryDto> friends;
        public string errorCode;
    }

    [Serializable]
    public sealed class GiftGatewayResult
    {
        public bool success;
        public string errorCode;
    }

    /// <summary>Real client gateway for the live, deployed Friends CloudCode module
    /// (nonprod-validation, see docs/LOCKED_DECISIONS_REGISTER.md "Beta social screens: OPTION B
    /// LOCKED"). Mirrors IBazaarGateway's own shape exactly: EnsureSignedInAsync before every
    /// call, {"request":{...}} wrapping the module's real parameter name.</summary>
    public interface IFriendsGateway
    {
        Task<FriendGatewayResult> AddFriendAsync(string targetAccountId, CancellationToken cancellationToken);
        Task<FriendGatewayResult> AcceptFriendAsync(string targetAccountId, CancellationToken cancellationToken);
        Task<FriendGatewayResult> DeclineFriendAsync(string targetAccountId, CancellationToken cancellationToken);
        Task<FriendGatewayResult> RemoveFriendAsync(string targetAccountId, CancellationToken cancellationToken);
        Task<ListFriendsGatewayResult> ListFriendsAsync(CancellationToken cancellationToken);
        Task<GiftGatewayResult> SendDailyGiftAsync(string targetAccountId, CancellationToken cancellationToken);
    }

    public sealed class UnityCloudCodeFriendsGateway : IFriendsGateway
    {
        private const string ModuleName = "Friends";

        public Task<FriendGatewayResult> AddFriendAsync(string targetAccountId, CancellationToken cancellationToken)
            => CallWithTargetAsync("AddFriend", targetAccountId, cancellationToken);

        public Task<FriendGatewayResult> AcceptFriendAsync(string targetAccountId, CancellationToken cancellationToken)
            => CallWithTargetAsync("AcceptFriend", targetAccountId, cancellationToken);

        public Task<FriendGatewayResult> DeclineFriendAsync(string targetAccountId, CancellationToken cancellationToken)
            => CallWithTargetAsync("DeclineFriend", targetAccountId, cancellationToken);

        public Task<FriendGatewayResult> RemoveFriendAsync(string targetAccountId, CancellationToken cancellationToken)
            => CallWithTargetAsync("RemoveFriend", targetAccountId, cancellationToken);

        public async Task<ListFriendsGatewayResult> ListFriendsAsync(CancellationToken cancellationToken)
        {
            await EnsureSignedInAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return await CloudCodeService.Instance.CallModuleEndpointAsync<ListFriendsGatewayResult>(
                ModuleName, "ListFriends", new Dictionary<string, object> { { "request", new Dictionary<string, object>() } }).ConfigureAwait(false);
        }

        public async Task<GiftGatewayResult> SendDailyGiftAsync(string targetAccountId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(targetAccountId))
                return new GiftGatewayResult { errorCode = "INVALID_REQUEST" };

            await EnsureSignedInAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return await CloudCodeService.Instance.CallModuleEndpointAsync<GiftGatewayResult>(
                ModuleName, "SendDailyGift",
                new Dictionary<string, object> { { "request", new Dictionary<string, object> { { "targetAccountId", targetAccountId } } } }).ConfigureAwait(false);
        }

        private static async Task<FriendGatewayResult> CallWithTargetAsync(string functionName, string targetAccountId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(targetAccountId))
                return new FriendGatewayResult { errorCode = "INVALID_REQUEST" };

            await EnsureSignedInAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return await CloudCodeService.Instance.CallModuleEndpointAsync<FriendGatewayResult>(
                ModuleName, functionName,
                new Dictionary<string, object> { { "request", new Dictionary<string, object> { { "targetAccountId", targetAccountId } } } }).ConfigureAwait(false);
        }

        private static async Task EnsureSignedInAsync(CancellationToken cancellationToken)
        {
            await UnityServices.InitializeAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync().ConfigureAwait(false);
            }
        }
    }
}

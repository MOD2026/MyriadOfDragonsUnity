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
    public sealed class PostChatMessageGatewayResult
    {
        public bool success;
        public string messageId;
        public string errorCode;
    }

    [Serializable]
    public sealed class ChatMessageDto
    {
        public string id;
        public string senderAccountId;
        public string text;
        public long sentUtcMs;
    }

    [Serializable]
    public sealed class FetchChatHistoryGatewayResult
    {
        public bool success;
        public List<ChatMessageDto> messages;
        public string errorCode;
    }

    /// <summary>Real client gateway for the live, deployed Chat CloudCode module
    /// (nonprod-validation, see docs/LOCKED_DECISIONS_REGISTER.md "Beta social screens: OPTION B
    /// LOCKED"). Mirrors IBazaarGateway's own shape exactly.</summary>
    public interface IChatSocialGateway
    {
        Task<PostChatMessageGatewayResult> PostMessageAsync(string channelId, string text, CancellationToken cancellationToken);
        Task<FetchChatHistoryGatewayResult> FetchChannelHistoryAsync(string channelId, int limit, CancellationToken cancellationToken);
    }

    public sealed class UnityCloudCodeChatSocialGateway : IChatSocialGateway
    {
        private const string ModuleName = "Chat";

        public async Task<PostChatMessageGatewayResult> PostMessageAsync(string channelId, string text, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(channelId) || string.IsNullOrWhiteSpace(text))
                return new PostChatMessageGatewayResult { errorCode = "INVALID_REQUEST" };

            await EnsureSignedInAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return await CloudCodeService.Instance.CallModuleEndpointAsync<PostChatMessageGatewayResult>(
                ModuleName, "PostChatMessage",
                new Dictionary<string, object> { { "request", new Dictionary<string, object> { { "channelId", channelId }, { "text", text } } } }).ConfigureAwait(false);
        }

        public async Task<FetchChatHistoryGatewayResult> FetchChannelHistoryAsync(string channelId, int limit, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(channelId))
                return new FetchChatHistoryGatewayResult { errorCode = "INVALID_REQUEST" };

            await EnsureSignedInAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return await CloudCodeService.Instance.CallModuleEndpointAsync<FetchChatHistoryGatewayResult>(
                ModuleName, "FetchChatHistory",
                new Dictionary<string, object> { { "request", new Dictionary<string, object> { { "channelId", channelId }, { "limit", limit } } } }).ConfigureAwait(false);
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

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

namespace MyriadOfDragons.CloudCode.Chat;

/// <summary>Real Cloud Save-backed <see cref="IChatStore"/>. Channel history lives in a shared
/// Custom Items bucket (<see cref="ChannelsCustomId"/>), one key per channel, following Bazaar's
/// board pattern - a channel is shared state, not any one player's own data. The block/mute read
/// targets ordinary player-scoped Cloud Save under the CALLING account's own id, using the exact
/// key format CloudSaveSocialSafetyStore.BuildKey computes ("ssr" + kind[0] + "_" +
/// first-32-hex-of-SHA256(actorId + "|" + kind + "|" + targetId)) - duplicated here rather than
/// shared via a project reference because each CloudCode module deploys as its own independent
/// assembly. If SocialSafety's key scheme changes, this must be updated to match by hand.
///
/// Custom Items writes (SetCustomItemAsync) use context.ServiceToken, not context.AccessToken -
/// see CloudSaveFriendsStore's own doc comment for the full, live-verified reasoning
/// (2026-08-25): AccessToken gets ApiException: Unauthorized on Custom Items writes specifically,
/// player-scoped writes and Custom Items reads are unaffected and correctly keep AccessToken.</summary>
public sealed class CloudSaveChatStore : IChatStore
{
    private const string ChannelsCustomId = "chat-channels";

    private static string ChannelKey(string channelId) => "chan_" + ShortHash(channelId);

    // Mirrors CloudSaveSocialSafetyStore.BuildKey exactly - see this class's own doc comment.
    private static string SocialSafetyKey(string actorAccountId, string relationshipKind, string targetAccountId)
    {
        string value = actorAccountId + "|" + relationshipKind + "|" + targetAccountId;
        return "ssr" + relationshipKind[0] + "_" + ShortHash(value);
    }

    private static string ShortHash(string value)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            return BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty).ToLowerInvariant().Substring(0, 32);
        }
    }

    public async Task<ChatChannelState> LoadChannelAsync(IExecutionContext context, IGameApiClient apiClient, string channelId)
    {
        try
        {
            var response = await apiClient.CloudSaveData.GetCustomItemsAsync(
                context,
                context.AccessToken ?? throw new InvalidOperationException("Missing authenticated access token."),
                context.ProjectId ?? throw new InvalidOperationException("Missing project context."),
                ChannelsCustomId,
                new List<string> { ChannelKey(channelId) });
            if (response.Data.Results.Count == 0)
            {
                return new ChatChannelState();
            }

            var item = response.Data.Results[0];
            var value = item.Value?.ToString();
            var state = string.IsNullOrWhiteSpace(value)
                ? new ChatChannelState()
                : JsonConvert.DeserializeObject<ChatChannelState>(value) ?? new ChatChannelState();
            state.WriteLock = item.WriteLock;
            return state;
        }
        catch (Exception exception)
        {
            throw new ChatStorageException(ClassifyStorageError(exception), exception);
        }
    }

    public async Task SaveChannelAsync(IExecutionContext context, IGameApiClient apiClient, string channelId, ChatChannelState state)
    {
        try
        {
            var body = new SetItemBody(ChannelKey(channelId), JsonConvert.SerializeObject(state));
            if (state.WriteLock != null)
            {
                body.WriteLock = state.WriteLock;
            }

            await apiClient.CloudSaveData.SetCustomItemAsync(
                context,
                context.ServiceToken ?? throw new InvalidOperationException("Missing service token."),
                context.ProjectId ?? throw new InvalidOperationException("Missing project context."),
                ChannelsCustomId,
                body);
        }
        catch (Exception exception)
        {
            throw new ChatStorageException(ClassifyStorageError(exception), exception);
        }
    }

    public async Task<HashSet<string>> LoadBlockedOrMutedSendersAsync(IExecutionContext context, IGameApiClient apiClient, string actorAccountId, IReadOnlyList<string> candidateSenderAccountIds, long nowUtcMs)
    {
        var filtered = new HashSet<string>(StringComparer.Ordinal);
        var distinctSenders = new List<string>();
        foreach (string sender in candidateSenderAccountIds)
        {
            if (sender != actorAccountId && !distinctSenders.Contains(sender))
            {
                distinctSenders.Add(sender);
            }
        }

        if (distinctSenders.Count == 0)
        {
            return filtered;
        }

        try
        {
            var keys = new List<string>(distinctSenders.Count * 2);
            foreach (string sender in distinctSenders)
            {
                keys.Add(SocialSafetyKey(actorAccountId, "block", sender));
                keys.Add(SocialSafetyKey(actorAccountId, "mute", sender));
            }

            var response = await apiClient.CloudSaveData.GetItemsAsync(
                context,
                context.AccessToken ?? throw new InvalidOperationException("Missing authenticated access token."),
                context.ProjectId ?? throw new InvalidOperationException("Missing project context."),
                actorAccountId,
                keys);

            var byKey = new Dictionary<string, SocialSafetyRelationshipSnapshot>(StringComparer.Ordinal);
            foreach (var item in response.Data.Results)
            {
                var value = item.Key != null ? item.Value?.ToString() : null;
                if (item.Key != null && !string.IsNullOrWhiteSpace(value))
                {
                    var snapshot = JsonConvert.DeserializeObject<SocialSafetyRelationshipSnapshot>(value);
                    if (snapshot != null)
                    {
                        byKey[item.Key] = snapshot;
                    }
                }
            }

            foreach (string sender in distinctSenders)
            {
                bool blocked = byKey.TryGetValue(SocialSafetyKey(actorAccountId, "block", sender), out var blockSnapshot)
                    && blockSnapshot.Status == "active";
                bool muted = byKey.TryGetValue(SocialSafetyKey(actorAccountId, "mute", sender), out var muteSnapshot)
                    && muteSnapshot.Status == "active" && muteSnapshot.MuteUntilUtcMs > nowUtcMs;
                if (blocked || muted)
                {
                    filtered.Add(sender);
                }
            }

            return filtered;
        }
        catch (Exception exception)
        {
            throw new ChatStorageException(ClassifyStorageError(exception), exception);
        }
    }

    private static string ClassifyStorageError(Exception exception)
    {
        string text = exception.GetType().Name + " " + exception.Message;
        return text.IndexOf("409", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Conflict", StringComparison.OrdinalIgnoreCase) >= 0
            ? "CONFLICT"
            : "STORAGE_UNAVAILABLE";
    }
}

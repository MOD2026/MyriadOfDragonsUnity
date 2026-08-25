using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

namespace MyriadOfDragons.CloudCode.Friends;

/// <summary>Real Cloud Save-backed <see cref="IFriendsStore"/>, following Bazaar's own board
/// design: a <see cref="FriendshipRecord"/> is jointly owned by two accounts, so it lives once
/// under a shared Custom Items bucket (<see cref="GraphCustomId"/>) keyed by a canonical (order-
/// independent) hash of the pair, rather than duplicated into either account's own player-scoped
/// save. The per-account <see cref="FriendsIndexState"/> that lets ListFriends find those records
/// (Custom Items has no query/filter, same limitation Bazaar's own board has) stays on ordinary
/// player-scoped Cloud Save, keyed by the account it belongs to - which is why this store's index
/// methods take an explicit accountId rather than always trusting context.PlayerId: accepting or
/// declining a request must update BOTH accounts' indices, not just the caller's own.
///
/// Custom Items writes (SetCustomItemAsync/DeleteCustomItemAsync) use context.ServiceToken, not
/// context.AccessToken - a real, live-verified requirement (2026-08-25): AccessToken (player-
/// scoped) gets ApiException: Unauthorized on Custom Items writes specifically, even though it
/// works fine for player-scoped Cloud Save (GetItemsAsync/SetItemsAsync) and for Custom Items
/// READS (GetCustomItemsAsync). Matches the documented Access Class model - "readable by any
/// player client-side, writeable only from a server" - server authority is asserted via
/// ServiceToken, not the caller's own AccessToken. GetCustomItemsAsync (reads) correctly keeps
/// AccessToken.</summary>
public sealed class CloudSaveFriendsStore : IFriendsStore
{
    private const string GraphCustomId = "friends-graph";

    // Cloud Save item keys must be 1-50 chars, [A-Za-z0-9_-] only - no dots. Account ids come from
    // outside this module, so their format isn't guaranteed - hash them instead of assuming.
    private static string FriendshipKey(string accountA, string accountB)
    {
        string canonical = CanonicalPair(accountA, accountB);
        return "friend_" + ShortHash(canonical);
    }

    private static string IndexKey() => "friends_index";

    private static string CanonicalPair(string accountA, string accountB) =>
        string.CompareOrdinal(accountA, accountB) <= 0 ? accountA + "|" + accountB : accountB + "|" + accountA;

    private static string ShortHash(string value)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            return BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty).ToLowerInvariant().Substring(0, 32);
        }
    }

    public async Task<FriendshipRecord?> LoadFriendshipAsync(IExecutionContext context, IGameApiClient apiClient, string accountA, string accountB)
    {
        try
        {
            var response = await apiClient.CloudSaveData.GetCustomItemsAsync(
                context,
                context.AccessToken ?? throw new InvalidOperationException("Missing authenticated access token."),
                context.ProjectId ?? throw new InvalidOperationException("Missing project context."),
                GraphCustomId,
                new List<string> { FriendshipKey(accountA, accountB) });
            if (response.Data.Results.Count == 0)
            {
                return null;
            }

            var item = response.Data.Results[0];
            var value = item.Value?.ToString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var record = JsonConvert.DeserializeObject<FriendshipRecord>(value);
            if (record != null)
            {
                record.WriteLock = item.WriteLock;
            }

            return record;
        }
        catch (Exception exception)
        {
            throw new FriendsStorageException(ClassifyStorageError(exception), exception);
        }
    }

    public async Task SaveFriendshipAsync(IExecutionContext context, IGameApiClient apiClient, string accountA, string accountB, FriendshipRecord record)
    {
        try
        {
            var body = new SetItemBody(FriendshipKey(accountA, accountB), JsonConvert.SerializeObject(record));
            if (record.WriteLock != null)
            {
                body.WriteLock = record.WriteLock;
            }

            await apiClient.CloudSaveData.SetCustomItemAsync(
                context,
                context.ServiceToken ?? throw new InvalidOperationException("Missing service token."),
                context.ProjectId ?? throw new InvalidOperationException("Missing project context."),
                GraphCustomId,
                body);
        }
        catch (Exception exception)
        {
            throw new FriendsStorageException(ClassifyStorageError(exception), exception);
        }
    }

    public async Task DeleteFriendshipAsync(IExecutionContext context, IGameApiClient apiClient, string accountA, string accountB)
    {
        try
        {
            await apiClient.CloudSaveData.DeleteCustomItemAsync(
                context,
                context.ServiceToken ?? throw new InvalidOperationException("Missing service token."),
                context.ProjectId ?? throw new InvalidOperationException("Missing project context."),
                GraphCustomId,
                FriendshipKey(accountA, accountB));
        }
        catch (Exception exception)
        {
            throw new FriendsStorageException(ClassifyStorageError(exception), exception);
        }
    }

    public async Task<FriendsIndexState> LoadIndexAsync(IExecutionContext context, IGameApiClient apiClient, string accountId)
    {
        try
        {
            var response = await apiClient.CloudSaveData.GetItemsAsync(
                context,
                context.AccessToken ?? throw new InvalidOperationException("Missing authenticated access token."),
                context.ProjectId ?? throw new InvalidOperationException("Missing project context."),
                accountId,
                new List<string> { IndexKey() });
            if (response.Data.Results.Count == 0)
            {
                return new FriendsIndexState();
            }

            var item = response.Data.Results[0];
            var value = item.Value?.ToString();
            var index = string.IsNullOrWhiteSpace(value)
                ? new FriendsIndexState()
                : JsonConvert.DeserializeObject<FriendsIndexState>(value) ?? new FriendsIndexState();
            index.WriteLock = item.WriteLock;
            return index;
        }
        catch (Exception exception)
        {
            throw new FriendsStorageException(ClassifyStorageError(exception), exception);
        }
    }

    public async Task SaveIndexAsync(IExecutionContext context, IGameApiClient apiClient, string accountId, FriendsIndexState index)
    {
        try
        {
            var body = new SetItemBody(IndexKey(), JsonConvert.SerializeObject(index));
            if (index.WriteLock != null)
            {
                body.WriteLock = index.WriteLock;
            }

            await apiClient.CloudSaveData.SetItemAsync(
                context,
                context.AccessToken ?? throw new InvalidOperationException("Missing authenticated access token."),
                context.ProjectId ?? throw new InvalidOperationException("Missing project context."),
                accountId,
                body);
        }
        catch (Exception exception)
        {
            throw new FriendsStorageException(ClassifyStorageError(exception), exception);
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

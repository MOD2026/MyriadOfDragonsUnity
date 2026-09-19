using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

namespace MyriadOfDragons.CloudCode.Bazaar;

/// <summary>
/// Real Cloud Save-backed <see cref="IBazaarStore"/>, closing the storage gap flagged when this
/// module was first authored. Verified against the actual installed
/// Com.Unity.Services.CloudCode.Apis 0.0.26 assembly via reflection (not assumed from the concept
/// docs alone) - <c>ICloudSaveDataApi.GetCustomItemsAsync</c>/<c>SetCustomItemAsync</c>/
/// <c>DeleteCustomItemAsync</c> take the same (executionContext, accessToken, projectId, customId,
/// ...) shape as this project's existing player-scoped calls, with <c>customId</c> replacing
/// <c>playerId</c>. Confirmed via Unity's own docs (docs.unity.com/en-us/cloud-save/concepts/
/// game-data): Custom Items' default Access Class is readable by any player client-side but
/// writeable only from a server - exactly "anyone can browse the board, only Cloud Code can
/// mutate a listing," the server-authoritative requirement the source packet calls for.
///
/// Listings AND item instances both live under one fixed shared customId
/// (<see cref="BoardCustomId"/>) rather than per-player storage, because ownership of an
/// ItemInstance changes hands between two different accounts on every sale - keeping it in a
/// single shared bucket avoids an awkward "move a record between two players' Cloud Save buckets"
/// step. A real launch with many concurrent listings would likely need to shard across multiple
/// customIds (each capped at 2,000 keys) or move to Game Data's query/index support - not
/// implemented here; see README.
///
/// Wallets and the idempotent-buy-result ledger stay on ordinary player-scoped Cloud Save (the
/// same proven pattern SocialSafety/PermitWeekKey/GuildExpedition already use), keyed by the
/// account that actually owns that data - which is why <see cref="IBazaarStore"/>'s wallet methods
/// take an explicit accountId rather than always trusting context.PlayerId: a purchase must credit
/// the SELLER's wallet too, not just the buyer's.
///
/// Custom Items writes (SetCustomItemAsync, in SaveCustomItemAsync below) use
/// context.ServiceToken, not context.AccessToken - live-verified (2026-08-25): AccessToken gets
/// ApiException: Unauthorized on Custom Items writes specifically, even though it works fine for
/// player-scoped Cloud Save and for Custom Items READS (GetCustomItemsAsync, both call sites
/// above). Matches this class's own "readable by any player client-side but writeable only from
/// a server" design note - server authority is asserted via ServiceToken.
/// </summary>
public sealed class CloudSaveBazaarStore : IBazaarStore
{
    private const string BoardCustomId = "bazaar-board";
    private const string WalletKey = "bazaar_wallet";
    private const string IndexKey = "bazaar_index";

    // Cloud Save item keys must be 1-50 chars, [A-Za-z0-9_-] only - no dots. instanceId/listingId/
    // idempotencyKey come from outside this module (or the client, for idempotencyKey), so their
    // format isn't guaranteed - hash them instead of assuming they're already compliant.
    private static string InstanceKey(string instanceId) => "instance_" + ShortHash(instanceId);
    private static string ListingKey(string listingId) => "listing_" + ShortHash(listingId);
    // Idempotency results and settlement journals are stored in the BUYER's player-scoped Cloud Save,
    // but they are read/written with ServiceToken, not AccessToken: BazaarReconciliationSweep (and any
    // support/recovery caller) reads them for buyers who are NOT the caller, and a cross-account
    // player-scoped call with AccessToken is rejected by Cloud Save (ApiException: Forbidden - the same
    // class as Friends BE-FRIENDS-STORAGE-014). Found by the live nonprod-validation run of the sweep;
    // in-memory fakes cannot reproduce token semantics. Server authority also covers the buyer's own data.
    private static string IdempotencyKey(string idempotencyKey) => "bazaar_idem_" + ShortHash(idempotencyKey);
    private static string SettlementKey(string idempotencyKey) => "bazaar_settle_" + ShortHash(idempotencyKey);

    private static string ShortHash(string value)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            return BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty).ToLowerInvariant().Substring(0, 32);
        }
    }

    public Task<ItemInstance?> LoadInstanceAsync(IExecutionContext context, IGameApiClient apiClient, string instanceId)
        => LoadCustomItemAsync<ItemInstance>(context, apiClient, InstanceKey(instanceId));

    public Task SaveInstanceAsync(IExecutionContext context, IGameApiClient apiClient, ItemInstance instance)
        => SaveCustomItemAsync(context, apiClient, InstanceKey(instance.InstanceId), instance, instance.WriteLock);

    public Task<BazaarListing?> LoadListingAsync(IExecutionContext context, IGameApiClient apiClient, string listingId)
        => LoadCustomItemAsync<BazaarListing>(context, apiClient, ListingKey(listingId));

    public Task SaveListingAsync(IExecutionContext context, IGameApiClient apiClient, BazaarListing listing)
        => SaveCustomItemAsync(context, apiClient, ListingKey(listing.ListingId), listing, listing.WriteLock);

    public async Task<WalletState> LoadWalletAsync(IExecutionContext context, IGameApiClient apiClient, string accountId)
    {
        try
        {
            var response = await apiClient.CloudSaveData.GetItemsAsync(
                context,
                context.ServiceToken ?? throw new InvalidOperationException("Missing service token."),
                context.ProjectId ?? throw new InvalidOperationException("Missing project context."),
                accountId,
                new List<string> { WalletKey });
            if (response.Data.Results.Count == 0)
            {
                return new WalletState { AccountId = accountId };
            }

            var item = response.Data.Results[0];
            var value = item.Value?.ToString();
            var wallet = string.IsNullOrWhiteSpace(value)
                ? new WalletState { AccountId = accountId }
                : JsonConvert.DeserializeObject<WalletState>(value) ?? new WalletState { AccountId = accountId };
            wallet.WriteLock = item.WriteLock;
            return wallet;
        }
        catch (Exception exception)
        {
            throw new BazaarStorageException(ClassifyStorageError(exception), exception);
        }
    }

    public async Task SaveWalletAsync(IExecutionContext context, IGameApiClient apiClient, WalletState wallet)
    {
        try
        {
            var body = new SetItemBody(WalletKey, JsonConvert.SerializeObject(wallet));
            if (wallet.WriteLock != null)
            {
                body.WriteLock = wallet.WriteLock;
            }

            await apiClient.CloudSaveData.SetItemAsync(
                context,
                context.ServiceToken ?? throw new InvalidOperationException("Missing service token."),
                context.ProjectId ?? throw new InvalidOperationException("Missing project context."),
                wallet.AccountId,
                body);
        }
        catch (Exception exception)
        {
            throw new BazaarStorageException(ClassifyStorageError(exception), exception);
        }
    }

    public async Task<BuyResult?> TryGetIdempotentBuyResultAsync(IExecutionContext context, IGameApiClient apiClient, string buyerId, string idempotencyKey)
    {
        try
        {
            var response = await apiClient.CloudSaveData.GetItemsAsync(
                context,
                context.ServiceToken ?? throw new InvalidOperationException("Missing service token."),
                context.ProjectId ?? throw new InvalidOperationException("Missing project context."),
                buyerId,
                new List<string> { IdempotencyKey(idempotencyKey) });
            if (response.Data.Results.Count == 0)
            {
                return null;
            }

            var value = response.Data.Results[0].Value?.ToString();
            return string.IsNullOrWhiteSpace(value) ? null : JsonConvert.DeserializeObject<BuyResult>(value);
        }
        catch (Exception exception)
        {
            throw new BazaarStorageException(ClassifyStorageError(exception), exception);
        }
    }

    public async Task SaveIdempotentBuyResultAsync(IExecutionContext context, IGameApiClient apiClient, string buyerId, string idempotencyKey, BuyResult result)
    {
        try
        {
            var body = new SetItemBody(IdempotencyKey(idempotencyKey), JsonConvert.SerializeObject(result));
            await apiClient.CloudSaveData.SetItemAsync(
                context,
                context.ServiceToken ?? throw new InvalidOperationException("Missing service token."),
                context.ProjectId ?? throw new InvalidOperationException("Missing project context."),
                buyerId,
                body);
        }
        catch (Exception exception)
        {
            throw new BazaarStorageException(ClassifyStorageError(exception), exception);
        }
    }

    public async Task<SettlementJournal?> TryGetSettlementJournalAsync(IExecutionContext context, IGameApiClient apiClient, string buyerId, string idempotencyKey)
    {
        try
        {
            var response = await apiClient.CloudSaveData.GetItemsAsync(
                context,
                context.ServiceToken ?? throw new InvalidOperationException("Missing service token."),
                context.ProjectId ?? throw new InvalidOperationException("Missing project context."),
                buyerId,
                new List<string> { SettlementKey(idempotencyKey) });
            if (response.Data.Results.Count == 0)
            {
                return null;
            }

            var value = response.Data.Results[0].Value?.ToString();
            return string.IsNullOrWhiteSpace(value) ? null : JsonConvert.DeserializeObject<SettlementJournal>(value);
        }
        catch (Exception exception)
        {
            throw new BazaarStorageException(ClassifyStorageError(exception), exception);
        }
    }

    public async Task SaveSettlementJournalAsync(IExecutionContext context, IGameApiClient apiClient, string buyerId, string idempotencyKey, SettlementJournal journal)
    {
        try
        {
            var body = new SetItemBody(SettlementKey(idempotencyKey), JsonConvert.SerializeObject(journal));
            await apiClient.CloudSaveData.SetItemAsync(
                context,
                context.ServiceToken ?? throw new InvalidOperationException("Missing service token."),
                context.ProjectId ?? throw new InvalidOperationException("Missing project context."),
                buyerId,
                body);
        }
        catch (Exception exception)
        {
            throw new BazaarStorageException(ClassifyStorageError(exception), exception);
        }
    }

    public async Task<BazaarListingIndex> LoadIndexAsync(IExecutionContext context, IGameApiClient apiClient)
    {
        var index = await LoadCustomItemAsync<BazaarListingIndex>(context, apiClient, IndexKey);
        return index ?? new BazaarListingIndex();
    }

    public Task SaveIndexAsync(IExecutionContext context, IGameApiClient apiClient, BazaarListingIndex index)
        => SaveCustomItemAsync(context, apiClient, IndexKey, index, index.WriteLock);

    public async Task<IReadOnlyList<BazaarListing>> LoadListingsBatchAsync(IExecutionContext context, IGameApiClient apiClient, IReadOnlyList<string> listingIds)
    {
        if (listingIds.Count == 0)
        {
            return Array.Empty<BazaarListing>();
        }

        try
        {
            var keys = new List<string>(listingIds.Count);
            foreach (string listingId in listingIds)
            {
                keys.Add(ListingKey(listingId));
            }

            var response = await apiClient.CloudSaveData.GetCustomItemsAsync(
                context,
                context.AccessToken ?? throw new InvalidOperationException("Missing authenticated access token."),
                context.ProjectId ?? throw new InvalidOperationException("Missing project context."),
                BoardCustomId,
                keys);

            var results = new List<BazaarListing>(response.Data.Results.Count);
            foreach (var item in response.Data.Results)
            {
                var value = item.Value?.ToString();
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var listing = JsonConvert.DeserializeObject<BazaarListing>(value);
                if (listing != null)
                {
                    listing.WriteLock = item.WriteLock;
                    results.Add(listing);
                }
            }

            return results;
        }
        catch (Exception exception)
        {
            throw new BazaarStorageException(ClassifyStorageError(exception), exception);
        }
    }

    private static async Task<T?> LoadCustomItemAsync<T>(IExecutionContext context, IGameApiClient apiClient, string key) where T : class
    {
        try
        {
            var response = await apiClient.CloudSaveData.GetCustomItemsAsync(
                context,
                context.AccessToken ?? throw new InvalidOperationException("Missing authenticated access token."),
                context.ProjectId ?? throw new InvalidOperationException("Missing project context."),
                BoardCustomId,
                new List<string> { key });
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

            var deserialized = JsonConvert.DeserializeObject<T>(value);
            if (deserialized == null)
            {
                return null;
            }

            SetWriteLock(deserialized, item.WriteLock);
            return deserialized;
        }
        catch (Exception exception)
        {
            throw new BazaarStorageException(ClassifyStorageError(exception), exception);
        }
    }

    private static async Task SaveCustomItemAsync<T>(IExecutionContext context, IGameApiClient apiClient, string key, T value, string? writeLock) where T : class
    {
        try
        {
            var body = new SetItemBody(key, JsonConvert.SerializeObject(value));
            if (writeLock != null)
            {
                body.WriteLock = writeLock;
            }

            await apiClient.CloudSaveData.SetCustomItemAsync(
                context,
                context.ServiceToken ?? throw new InvalidOperationException("Missing service token."),
                context.ProjectId ?? throw new InvalidOperationException("Missing project context."),
                BoardCustomId,
                body);
        }
        catch (Exception exception)
        {
            throw new BazaarStorageException(ClassifyStorageError(exception), exception);
        }
    }

    private static void SetWriteLock<T>(T state, string? writeLock) where T : class
    {
        switch (state)
        {
            case ItemInstance instance: instance.WriteLock = writeLock; break;
            case BazaarListing listing: listing.WriteLock = writeLock; break;
            case BazaarListingIndex index: index.WriteLock = writeLock; break;
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

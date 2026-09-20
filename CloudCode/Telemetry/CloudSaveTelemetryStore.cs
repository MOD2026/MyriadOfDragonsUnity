using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

namespace MyriadOfDragons.CloudCode.Telemetry;

/// <summary>Cloud Save-backed dedup ledger: one small item per player under its OWN key
/// (<see cref="SeenKey"/>) - NOT PlayerProfile and not part of any gameplay save schema. It holds a
/// bounded list of eventIds only (see <see cref="SeenEventIds"/>); raw events are never stored here.
/// The item is the caller's own player-scoped data, so the caller's AccessToken is the correct
/// credential (no cross-account access; contrast Friends/Bazaar wallets, which need ServiceToken).
/// Optimistic concurrency uses the item's WriteLock, like the other modules' stores.</summary>
public sealed class CloudSaveTelemetryStore : ITelemetryStore
{
    public const string SeenKey = "telemetry_seen";

    public async Task<SeenEventIds> LoadSeenAsync(IExecutionContext context, IGameApiClient apiClient, string playerId)
    {
        try
        {
            var response = await apiClient.CloudSaveData.GetItemsAsync(
                context,
                context.AccessToken ?? throw new InvalidOperationException("Missing authenticated access token."),
                context.ProjectId ?? throw new InvalidOperationException("Missing project context."),
                playerId,
                new List<string> { SeenKey });
            if (response.Data.Results.Count == 0)
            {
                return new SeenEventIds();
            }

            var item = response.Data.Results[0];
            var value = item.Value?.ToString();
            var seen = string.IsNullOrWhiteSpace(value)
                ? new SeenEventIds()
                : JsonConvert.DeserializeObject<SeenEventIds>(value) ?? new SeenEventIds();
            seen.WriteLock = item.WriteLock;
            return seen;
        }
        catch (Exception exception)
        {
            throw new TelemetryStorageException(ClassifyStorageError(exception), exception);
        }
    }

    public async Task SaveSeenAsync(IExecutionContext context, IGameApiClient apiClient, string playerId, SeenEventIds seen)
    {
        try
        {
            var body = new SetItemBody(SeenKey, JsonConvert.SerializeObject(seen));
            if (seen.WriteLock != null)
            {
                body.WriteLock = seen.WriteLock;
            }

            await apiClient.CloudSaveData.SetItemAsync(
                context,
                context.AccessToken ?? throw new InvalidOperationException("Missing authenticated access token."),
                context.ProjectId ?? throw new InvalidOperationException("Missing project context."),
                playerId,
                body);
        }
        catch (Exception exception)
        {
            throw new TelemetryStorageException(ClassifyStorageError(exception), exception);
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

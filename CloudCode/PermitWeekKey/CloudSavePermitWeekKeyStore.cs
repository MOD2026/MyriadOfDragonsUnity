using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

namespace MyriadOfDragons.CloudCode.PermitWeekKey;

public interface IPermitWeekKeyStore
{
    Task<PermitWeekKeyState> LoadAsync(IExecutionContext context, IGameApiClient apiClient, string activityId);
    Task SaveAsync(IExecutionContext context, IGameApiClient apiClient, string activityId, PermitWeekKeyState state);
}

public sealed class CloudSavePermitWeekKeyStore : IPermitWeekKeyStore
{
    private const string KeyPrefix = "permitWeekKey.";

    public async Task<PermitWeekKeyState> LoadAsync(IExecutionContext context, IGameApiClient apiClient, string activityId)
    {
        string key = BuildKey(context.PlayerId ?? string.Empty, activityId);
        try
        {
            var response = await apiClient.CloudSaveData.GetItemsAsync(
                context,
                context.AccessToken ?? throw new InvalidOperationException("Missing authenticated access token."),
                context.ProjectId ?? throw new InvalidOperationException("Missing project context."),
                context.PlayerId ?? throw new InvalidOperationException("Missing player context."),
                new List<string> { key });
            if (response.Data.Results.Count == 0)
            {
                return new PermitWeekKeyState();
            }

            var item = response.Data.Results[0];
            var value = item.Value?.ToString();
            var state = string.IsNullOrWhiteSpace(value)
                ? new PermitWeekKeyState()
                : JsonConvert.DeserializeObject<PermitWeekKeyState>(value) ?? new PermitWeekKeyState();
            state.WriteLock = item.WriteLock;
            return state;
        }
        catch (Exception exception)
        {
            throw new PermitWeekKeyStorageException(ClassifyStorageError(exception), exception);
        }
    }

    public async Task SaveAsync(IExecutionContext context, IGameApiClient apiClient, string activityId, PermitWeekKeyState state)
    {
        string key = BuildKey(context.PlayerId ?? string.Empty, activityId);
        try
        {
            var body = new SetItemBody(key, JsonConvert.SerializeObject(state));
            if (state.WriteLock != null)
            {
                body.WriteLock = state.WriteLock;
            }

            await apiClient.CloudSaveData.SetItemAsync(
                context,
                context.AccessToken ?? throw new InvalidOperationException("Missing authenticated access token."),
                context.ProjectId ?? throw new InvalidOperationException("Missing project context."),
                context.PlayerId ?? throw new InvalidOperationException("Missing player context."),
                body);
        }
        catch (Exception exception)
        {
            throw new PermitWeekKeyStorageException(ClassifyStorageError(exception), exception);
        }
    }

    /// <summary>Record identity per the design doc: accountId + activityId (the ISO week key
    /// itself is not part of the storage key - it lives inside the stored claim record and is
    /// compared against the server's current week key on each claim, which is what makes a
    /// repeat claim within the same week idempotent instead of creating a new record).</summary>
    internal static string BuildKey(string accountId, string activityId)
    {
        string value = accountId + "|" + activityId;
        using (SHA256 sha256 = SHA256.Create())
        {
            return KeyPrefix + BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty).ToLowerInvariant();
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

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;

namespace MyriadOfDragons.CloudCode.SocialSafety;

public interface ISocialSafetyStore
{
    Task<SocialSafetyState> LoadAsync(IExecutionContext context, IGameApiClient apiClient, string relationshipKind, string targetAccountId);
    Task SaveAsync(IExecutionContext context, IGameApiClient apiClient, string relationshipKind, string targetAccountId, SocialSafetyState state);
    Task<SocialSafetyRateState> LoadRateStateAsync(IExecutionContext context, IGameApiClient apiClient);
    Task SaveRateStateAsync(IExecutionContext context, IGameApiClient apiClient, SocialSafetyRateState state);
}

public sealed class CloudSaveSocialSafetyStore : ISocialSafetyStore
{
    private const string KeyPrefix = "socialSafety.relationship.";
    private const string RateLimitKeyPrefix = "socialSafety.rateLimit.";

    public async Task<SocialSafetyState> LoadAsync(IExecutionContext context, IGameApiClient apiClient, string relationshipKind, string targetAccountId)
    {
        string key = BuildKey(context.PlayerId ?? string.Empty, relationshipKind, targetAccountId);
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
                return new SocialSafetyState();
            }

            var item = response.Data.Results[0];
            var value = item.Value?.ToString();
            var state = string.IsNullOrWhiteSpace(value)
                ? new SocialSafetyState()
                : JsonConvert.DeserializeObject<SocialSafetyState>(value) ?? new SocialSafetyState();
            state.WriteLock = item.WriteLock;
            return state;
        }
        catch (Exception exception)
        {
            throw new SocialSafetyStorageException(ClassifyStorageError(exception), exception);
        }
    }

    public async Task SaveAsync(IExecutionContext context, IGameApiClient apiClient, string relationshipKind, string targetAccountId, SocialSafetyState state)
    {
        string key = BuildKey(context.PlayerId ?? string.Empty, relationshipKind, targetAccountId);
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
            throw new SocialSafetyStorageException(ClassifyStorageError(exception), exception);
        }
    }

    public Task<SocialSafetyRateState> LoadRateStateAsync(IExecutionContext context, IGameApiClient apiClient)
    {
        return LoadRateStateCoreAsync(context, apiClient);
    }

    private async Task<SocialSafetyRateState> LoadRateStateCoreAsync(IExecutionContext context, IGameApiClient apiClient)
    {
        string key = BuildRateLimitKey(context.PlayerId ?? string.Empty);
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
                return new SocialSafetyRateState();
            }

            var item = response.Data.Results[0];
            var value = item.Value?.ToString();
            var state = string.IsNullOrWhiteSpace(value)
                ? new SocialSafetyRateState()
                : JsonConvert.DeserializeObject<SocialSafetyRateState>(value) ?? new SocialSafetyRateState();
            state.WriteLock = item.WriteLock;
            return state;
        }
        catch (Exception exception)
        {
            throw new SocialSafetyStorageException(ClassifyStorageError(exception), exception);
        }
    }

    public async Task SaveRateStateAsync(IExecutionContext context, IGameApiClient apiClient, SocialSafetyRateState state)
    {
        string key = BuildRateLimitKey(context.PlayerId ?? string.Empty);
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
            throw new SocialSafetyStorageException(ClassifyStorageError(exception), exception);
        }
    }

    internal static string BuildKey(string actorAccountId, string relationshipKind, string targetAccountId)
    {
        string value = actorAccountId + "|" + relationshipKind + "|" + targetAccountId;
        using (SHA256 sha256 = SHA256.Create())
        {
            return KeyPrefix + relationshipKind + "." + BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    internal static string BuildRecordId(string actorAccountId, string relationshipKind, string targetAccountId)
    {
        string key = BuildKey(actorAccountId, relationshipKind, targetAccountId);
        return key.Substring((KeyPrefix + relationshipKind + ".").Length);
    }

    internal static string BuildRateLimitKey(string actorAccountId)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            return RateLimitKeyPrefix + BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(actorAccountId))).Replace("-", string.Empty).ToLowerInvariant();
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

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger? _logger;

    public CloudSaveSocialSafetyStore(ILogger? logger = null)
    {
        _logger = logger;
    }

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

    // Cloud Save item keys must be 1-50 chars, [A-Za-z0-9_-] only - no dots, no colons, no full
    // 64-char hashes. "ssr" + kind-char + "_" + 32 hex chars = 37 chars, 128 bits of collision
    // resistance, fully compliant.
    internal static string BuildKey(string actorAccountId, string relationshipKind, string targetAccountId)
    {
        string value = actorAccountId + "|" + relationshipKind + "|" + targetAccountId;
        return "ssr" + relationshipKind[0] + "_" + ShortHash(value);
    }

    internal static string BuildRecordId(string actorAccountId, string relationshipKind, string targetAccountId)
    {
        string value = actorAccountId + "|" + relationshipKind + "|" + targetAccountId;
        return ShortHash(value);
    }

    internal static string BuildRateLimitKey(string actorAccountId)
    {
        return "ssrl_" + ShortHash(actorAccountId);
    }

    private static string ShortHash(string value)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            return BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty).ToLowerInvariant().Substring(0, 32);
        }
    }

    private string ClassifyStorageError(Exception exception)
    {
        _logger?.LogError(exception, "SocialSafety storage error");
        string text = exception.GetType().Name + " " + exception.Message;
        return text.IndexOf("409", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Conflict", StringComparison.OrdinalIgnoreCase) >= 0
            ? "CONFLICT"
            : "STORAGE_UNAVAILABLE";
    }
}

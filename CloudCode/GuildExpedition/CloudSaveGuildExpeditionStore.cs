using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

namespace MyriadOfDragons.CloudCode.GuildExpedition;

public interface IGuildExpeditionStore
{
    Task<AttemptState> LoadAttemptStateAsync(IExecutionContext context, IGameApiClient apiClient);
    Task SaveAttemptStateAsync(IExecutionContext context, IGameApiClient apiClient, AttemptState state);
    Task<ContributionState> LoadContributionStateAsync(IExecutionContext context, IGameApiClient apiClient);
    Task SaveContributionStateAsync(IExecutionContext context, IGameApiClient apiClient, ContributionState state);
}

public sealed class CloudSaveGuildExpeditionStore : IGuildExpeditionStore
{
    private const string AttemptKey = "guildExpedition.attempts";
    private const string ContributionKey = "guildExpedition.contribution";

    public Task<AttemptState> LoadAttemptStateAsync(IExecutionContext context, IGameApiClient apiClient)
        => LoadAsync<AttemptState>(context, apiClient, AttemptKey);

    public Task SaveAttemptStateAsync(IExecutionContext context, IGameApiClient apiClient, AttemptState state)
        => SaveAsync(context, apiClient, AttemptKey, state, state.WriteLock);

    public Task<ContributionState> LoadContributionStateAsync(IExecutionContext context, IGameApiClient apiClient)
        => LoadAsync<ContributionState>(context, apiClient, ContributionKey);

    public Task SaveContributionStateAsync(IExecutionContext context, IGameApiClient apiClient, ContributionState state)
        => SaveAsync(context, apiClient, ContributionKey, state, state.WriteLock);

    private static async Task<T> LoadAsync<T>(IExecutionContext context, IGameApiClient apiClient, string key) where T : class, new()
    {
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
                return new T();
            }

            var item = response.Data.Results[0];
            var value = item.Value?.ToString();
            var state = string.IsNullOrWhiteSpace(value)
                ? new T()
                : JsonConvert.DeserializeObject<T>(value) ?? new T();
            SetWriteLock(state, item.WriteLock);
            return state;
        }
        catch (Exception exception)
        {
            throw new GuildExpeditionStorageException(ClassifyStorageError(exception), exception);
        }
    }

    private static async Task SaveAsync<T>(IExecutionContext context, IGameApiClient apiClient, string key, T state, string? writeLock) where T : class
    {
        try
        {
            var body = new SetItemBody(key, JsonConvert.SerializeObject(state));
            if (writeLock != null)
            {
                body.WriteLock = writeLock;
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
            throw new GuildExpeditionStorageException(ClassifyStorageError(exception), exception);
        }
    }

    private static void SetWriteLock<T>(T state, string? writeLock) where T : class
    {
        switch (state)
        {
            case AttemptState attempt: attempt.WriteLock = writeLock; break;
            case ContributionState contribution: contribution.WriteLock = writeLock; break;
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

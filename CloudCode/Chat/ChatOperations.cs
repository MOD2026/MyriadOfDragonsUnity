using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace MyriadOfDragons.CloudCode.Chat;

/// <summary>Chat storage module (docs/LOCKED_DECISIONS_REGISTER.md "Beta social screens: OPTION B
/// LOCKED 2026-08-25"): channel history read + post, filtered through the existing SocialSafety
/// block/mute state. Same module patterns as the 4 live-verified modules (single public
/// constructor, ICloudCodeSetup/AddGameApiClient, RFC-compliant Cloud Save keys,
/// {"request":{...}} call shape). No message schema existed anywhere in the repo before this
/// module - the ChatMessage/ChatChannelState shapes are this module's own design, following the
/// SocialSafety/Bazaar engineering patterns; only "channel history, post, SocialSafety-filtered"
/// was locked.</summary>
public sealed class ChatOperations
{
    public const int MaxStoredMessagesPerChannel = 200;
    public const int MaxMessageLength = 500;
    private const int MaxConflictReconciliations = 1;
    private const int MaxFetchLimit = 100;

    private readonly IChatStore _store;
    private readonly IChatClock _clock;

    public ChatOperations(IChatStore store, IChatClock clock)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<PostMessageResult> PostMessageAsync(IExecutionContext context, IGameApiClient apiClient, PostMessageRequest request)
    {
        if (context == null || string.IsNullOrWhiteSpace(context.PlayerId))
        {
            return new PostMessageResult { ErrorCode = "AUTHENTICATION_REQUIRED" };
        }

        string channelId = request?.ChannelId?.Trim() ?? string.Empty;
        string text = request?.Text?.Trim() ?? string.Empty;
        if (channelId.Length == 0 || text.Length == 0 || text.Length > MaxMessageLength)
        {
            return new PostMessageResult { ErrorCode = "INVALID_REQUEST" };
        }

        for (int attempt = 0; attempt <= MaxConflictReconciliations; attempt++)
        {
            try
            {
                var channel = await _store.LoadChannelAsync(context, apiClient, channelId);
                var message = new ChatMessage
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ChannelId = channelId,
                    SenderAccountId = context.PlayerId,
                    Text = text,
                    SentUtcMs = _clock.UtcNowMs,
                };

                channel.Messages.Add(message);
                if (channel.Messages.Count > MaxStoredMessagesPerChannel)
                {
                    channel.Messages.RemoveRange(0, channel.Messages.Count - MaxStoredMessagesPerChannel);
                }

                await _store.SaveChannelAsync(context, apiClient, channelId, channel);
                return new PostMessageResult { Success = true, MessageId = message.Id };
            }
            catch (ChatStorageException exception) when (exception.ErrorCode == "CONFLICT" && attempt < MaxConflictReconciliations)
            {
            }
            catch (ChatStorageException exception)
            {
                return new PostMessageResult { ErrorCode = exception.ErrorCode };
            }
        }

        return new PostMessageResult { ErrorCode = "CONFLICT" };
    }

    public async Task<FetchChannelHistoryResult> FetchChannelHistoryAsync(IExecutionContext context, IGameApiClient apiClient, FetchChannelHistoryRequest request)
    {
        if (context == null || string.IsNullOrWhiteSpace(context.PlayerId))
        {
            return new FetchChannelHistoryResult { ErrorCode = "AUTHENTICATION_REQUIRED" };
        }

        string channelId = request?.ChannelId?.Trim() ?? string.Empty;
        int limit = request?.Limit ?? 50;
        if (channelId.Length == 0 || limit <= 0 || limit > MaxFetchLimit)
        {
            return new FetchChannelHistoryResult { ErrorCode = "INVALID_REQUEST" };
        }

        try
        {
            var channel = await _store.LoadChannelAsync(context, apiClient, channelId);
            List<ChatMessage> mostRecentFirst = channel.Messages
                .OrderByDescending(m => m.SentUtcMs)
                .Take(limit)
                .ToList();

            var senderIds = mostRecentFirst.Select(m => m.SenderAccountId).Distinct().ToList();
            HashSet<string> filteredSenders = await _store.LoadBlockedOrMutedSendersAsync(context, apiClient, context.PlayerId, senderIds, _clock.UtcNowMs);

            var visible = mostRecentFirst
                .Where(m => !filteredSenders.Contains(m.SenderAccountId))
                .Select(m => new ChatMessageSummary
                {
                    Id = m.Id,
                    SenderAccountId = m.SenderAccountId,
                    Text = m.Text,
                    SentUtcMs = m.SentUtcMs,
                })
                .ToList();

            return new FetchChannelHistoryResult { Success = true, Messages = visible };
        }
        catch (ChatStorageException exception)
        {
            return new FetchChannelHistoryResult { ErrorCode = exception.ErrorCode };
        }
    }
}

public sealed class ChatModule
{
    private readonly ChatOperations _operations;

    public ChatModule()
        : this(new CloudSaveChatStore(), new SystemChatClock())
    {
    }

    internal ChatModule(IChatStore store, IChatClock clock)
    {
        _operations = new ChatOperations(store, clock);
    }

    [CloudCodeFunction("PostChatMessage")]
    public Task<PostMessageResult> PostChatMessage(IExecutionContext context, IGameApiClient apiClient, PostMessageRequest request)
        => _operations.PostMessageAsync(context, apiClient, request);

    [CloudCodeFunction("FetchChatHistory")]
    public Task<FetchChannelHistoryResult> FetchChatHistory(IExecutionContext context, IGameApiClient apiClient, FetchChannelHistoryRequest request)
        => _operations.FetchChannelHistoryAsync(context, apiClient, request);
}

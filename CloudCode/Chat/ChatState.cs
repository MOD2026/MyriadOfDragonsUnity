using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MyriadOfDragons.CloudCode.Chat;

public sealed class ChatMessage
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;
    [JsonProperty("channelId")]
    public string ChannelId { get; set; } = string.Empty;
    [JsonProperty("senderAccountId")]
    public string SenderAccountId { get; set; } = string.Empty;
    [JsonProperty("text")]
    public string Text { get; set; } = string.Empty;
    [JsonProperty("sentUtcMs")]
    public long SentUtcMs { get; set; }
}

/// <summary>One channel's stored history, capped at <see cref="ChatOperations.MaxStoredMessagesPerChannel"/>
/// (oldest trimmed on overflow) so a single Cloud Save item never grows unbounded - the same
/// "one shared bucket, not per-player" shape Bazaar's board uses, since a channel is inherently
/// shared state, not any one account's own data.</summary>
public sealed class ChatChannelState
{
    [JsonProperty("messages")]
    public List<ChatMessage> Messages { get; set; } = new();

    [JsonIgnore]
    public string? WriteLock { get; set; }
}

/// <summary>Minimal, independent mirror of SocialSafety's own RelationshipRecord shape (status +
/// muteUntilUtcMs) - kept as its own tiny DTO here rather than a project reference to
/// SocialSafety.csproj, because each CloudCode module deploys as its own independent Cloud Code
/// assembly (see CloudCode/README-level pattern: no module references another). If SocialSafety's
/// serialized shape ever changes, this must be updated to match by hand.</summary>
public sealed class SocialSafetyRelationshipSnapshot
{
    [JsonProperty("status")]
    public string Status { get; set; } = "inactive";
    [JsonProperty("muteUntilUtcMs")]
    public long MuteUntilUtcMs { get; set; }
}

public sealed class PostMessageRequest
{
    public string ChannelId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public sealed class PostMessageResult
{
    [JsonProperty("success")]
    public bool Success { get; set; }
    [JsonProperty("messageId")]
    public string? MessageId { get; set; }
    [JsonProperty("errorCode")]
    public string? ErrorCode { get; set; }
}

public sealed class FetchChannelHistoryRequest
{
    public string ChannelId { get; set; } = string.Empty;
    public int Limit { get; set; } = 50;
}

public sealed class ChatMessageSummary
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;
    [JsonProperty("senderAccountId")]
    public string SenderAccountId { get; set; } = string.Empty;
    [JsonProperty("text")]
    public string Text { get; set; } = string.Empty;
    [JsonProperty("sentUtcMs")]
    public long SentUtcMs { get; set; }
}

public sealed class FetchChannelHistoryResult
{
    [JsonProperty("success")]
    public bool Success { get; set; }
    [JsonProperty("messages")]
    public List<ChatMessageSummary> Messages { get; set; } = new();
    [JsonProperty("errorCode")]
    public string? ErrorCode { get; set; }
}

public sealed class ChatStorageException : Exception
{
    public ChatStorageException(string errorCode, Exception? innerException = null)
        : base("Chat storage failed.", innerException)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}

public interface IChatClock
{
    long UtcNowMs { get; }
}

public sealed class SystemChatClock : IChatClock
{
    public long UtcNowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

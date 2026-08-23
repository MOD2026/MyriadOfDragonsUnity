using System;
using Newtonsoft.Json;

namespace MyriadOfDragons.CloudCode.SocialSafety;

public sealed class SocialSafetyState
{
    public RelationshipRecord? Relationship { get; set; }

    [JsonIgnore]
    public string? WriteLock { get; set; }
}

public sealed class SocialSafetyRateState
{
    [JsonProperty("minuteWindowStartUtcMs")]
    public long MinuteWindowStartUtcMs { get; set; }
    [JsonProperty("minuteCount")]
    public int MinuteCount { get; set; }
    [JsonProperty("dayWindowStartUtcMs")]
    public long DayWindowStartUtcMs { get; set; }
    [JsonProperty("dayCount")]
    public int DayCount { get; set; }

    [JsonIgnore]
    public string? WriteLock { get; set; }
}

public sealed class RelationshipRecord
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;
    [JsonProperty("actorAccountId")]
    public string ActorAccountId { get; set; } = string.Empty;
    [JsonProperty("targetAccountId")]
    public string TargetAccountId { get; set; } = string.Empty;
    [JsonProperty("createdAtUtcMs")]
    public long CreatedAtUtcMs { get; set; }
    [JsonProperty("muteUntilUtcMs")]
    public long MuteUntilUtcMs { get; set; }
    [JsonProperty("status")]
    public string Status { get; set; } = "active";
}

public sealed class RelationshipResult
{
    [JsonProperty("success")]
    public bool Success { get; set; }
    [JsonProperty("relationship")]
    public RelationshipRecord? Relationship { get; set; }
    [JsonProperty("changed")]
    public bool Changed { get; set; }
    [JsonProperty("errorCode")]
    public string? ErrorCode { get; set; }
}

public interface ISocialSafetyPolicy
{
    long MuteDurationMs { get; }
    bool IsRateLimited(string actorAccountId, string operation, long utcNowMs);
}

public sealed class UnimplementedSocialSafetyPolicy : ISocialSafetyPolicy
{
    public long MuteDurationMs => TimeSpan.FromDays(30).Ticks / TimeSpan.TicksPerMillisecond;

    // Cloud Code instances are stateless; durable endpoint limiting needs shared storage or a platform limiter.
    public bool IsRateLimited(string actorAccountId, string operation, long utcNowMs) => false;
}

public sealed class SocialSafetyStorageException : Exception
{
    public SocialSafetyStorageException(string errorCode, Exception? innerException = null)
        : base("Social safety storage failed.", innerException)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}

public interface ISocialSafetyClock
{
    long UtcNowMs { get; }
}

public sealed class SystemSocialSafetyClock : ISocialSafetyClock
{
    public long UtcNowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

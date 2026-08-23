using System;
using Newtonsoft.Json;

namespace MyriadOfDragons.CloudCode.PermitWeekKey;

/// <summary>The most recent weekly claim for one accountId+activityId pair - the idempotency
/// anchor. A claim is only granted again once <see cref="WeekKey"/> no longer matches the
/// server's current ISO week key.</summary>
public sealed class PermitClaimRecord
{
    [JsonProperty("activityId")]
    public string ActivityId { get; set; } = string.Empty;
    [JsonProperty("weekKey")]
    public string WeekKey { get; set; } = string.Empty;
    [JsonProperty("grantedAmount")]
    public int GrantedAmount { get; set; }
    [JsonProperty("claimedAtUtcMs")]
    public long ClaimedAtUtcMs { get; set; }
}

public sealed class PermitWeekKeyState
{
    [JsonProperty("balance")]
    public int Balance { get; set; }
    [JsonProperty("lastClaim")]
    public PermitClaimRecord? LastClaim { get; set; }

    [JsonIgnore]
    public string? WriteLock { get; set; }
}

public sealed class PermitClaimResult
{
    [JsonProperty("success")]
    public bool Success { get; set; }
    [JsonProperty("granted")]
    public int Granted { get; set; }
    [JsonProperty("balance")]
    public int Balance { get; set; }
    [JsonProperty("weekKey")]
    public string WeekKey { get; set; } = string.Empty;
    [JsonProperty("alreadyClaimed")]
    public bool AlreadyClaimed { get; set; }
    [JsonProperty("errorCode")]
    public string? ErrorCode { get; set; }
}

public sealed class PermitStatusResult
{
    [JsonProperty("balance")]
    public int Balance { get; set; }
    [JsonProperty("currentWeekKey")]
    public string CurrentWeekKey { get; set; } = string.Empty;
    [JsonProperty("claimedThisWeek")]
    public bool ClaimedThisWeek { get; set; }
    [JsonProperty("weeklyRate")]
    public int WeeklyRate { get; set; }
    [JsonProperty("hoardCap")]
    public int HoardCap { get; set; }
    [JsonProperty("errorCode")]
    public string? ErrorCode { get; set; }
}

public sealed class PermitClaimRequest
{
    public string ActivityId { get; set; } = string.Empty;
}

public sealed class PermitStatusRequest
{
    public string ActivityId { get; set; } = string.Empty;
}

public interface IPermitWeekKeyClock
{
    long UtcNowMs { get; }
    string CurrentIsoWeekKey { get; }
}

public sealed class SystemPermitWeekKeyClock : IPermitWeekKeyClock
{
    public long UtcNowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public string CurrentIsoWeekKey => IsoWeekKey.FromUtcMs(UtcNowMs);
}

public sealed class PermitWeekKeyStorageException : Exception
{
    public PermitWeekKeyStorageException(string errorCode, Exception? innerException = null)
        : base("Permit week-key storage failed.", innerException)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}

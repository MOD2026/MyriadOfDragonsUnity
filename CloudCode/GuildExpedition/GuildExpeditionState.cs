using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MyriadOfDragons.CloudCode.GuildExpedition;

/// <summary>Daily-refilling attempt bank. §4.2: "3 per member per UTC day, maximum bank 6; no
/// Gem, Gold, real-money, ad or item refill" - modeled as a stamina-style regenerating pool
/// (capped, not a per-day-separate allotment) since that is the only reading of "3/day, bank 6"
/// that is well-defined across multiple missed days.</summary>
public sealed class AttemptState
{
    [JsonProperty("lastRefillDayKey")]
    public string LastRefillDayKey { get; set; } = string.Empty;
    [JsonProperty("available")]
    public int Available { get; set; }

    [JsonIgnore]
    public string? WriteLock { get; set; }
}

/// <summary>One account's Event Contribution ledger for one Expedition week key. §4.2: best
/// verified result per objective only (no repeat scoring), personal cap 1,000/week (§4.2), and
/// the five milestone claims of §5.1 - each independently idempotent per week.</summary>
public sealed class ContributionState
{
    [JsonProperty("weekKey")]
    public string WeekKey { get; set; } = string.Empty;
    [JsonProperty("totalPoints")]
    public int TotalPoints { get; set; }
    [JsonProperty("scoredObjectiveIds")]
    public List<string> ScoredObjectiveIds { get; set; } = new();
    [JsonProperty("claimedMilestoneThresholds")]
    public List<int> ClaimedMilestoneThresholds { get; set; } = new();

    [JsonIgnore]
    public string? WriteLock { get; set; }
}

public sealed class AttemptResult
{
    [JsonProperty("success")]
    public bool Success { get; set; }
    [JsonProperty("remaining")]
    public int Remaining { get; set; }
    [JsonProperty("errorCode")]
    public string? ErrorCode { get; set; }
}

public sealed class ObjectiveResult
{
    [JsonProperty("success")]
    public bool Success { get; set; }
    [JsonProperty("pointsAwarded")]
    public int PointsAwarded { get; set; }
    [JsonProperty("totalPoints")]
    public int TotalPoints { get; set; }
    [JsonProperty("weekKey")]
    public string WeekKey { get; set; } = string.Empty;
    [JsonProperty("alreadyScored")]
    public bool AlreadyScored { get; set; }
    [JsonProperty("errorCode")]
    public string? ErrorCode { get; set; }
}

public sealed class MilestoneClaimResult
{
    [JsonProperty("success")]
    public bool Success { get; set; }
    [JsonProperty("threshold")]
    public int Threshold { get; set; }
    [JsonProperty("guildContributionGranted")]
    public int GuildContributionGranted { get; set; }
    [JsonProperty("alreadyClaimed")]
    public bool AlreadyClaimed { get; set; }
    [JsonProperty("errorCode")]
    public string? ErrorCode { get; set; }
}

public sealed class ObjectiveResultRequest
{
    public string ObjectiveId { get; set; } = string.Empty;
}

public sealed class MilestoneClaimRequest
{
    public int Threshold { get; set; }
}

/// <summary>One personal milestone band from §5.1. Ascension Permit amounts from that table are
/// deliberately NOT modeled here - see README "Deferred" section: the doc states Expedition
/// Permit grants consume the same trusted weekly ceiling PermitWeekKey issues against, and
/// reconciling two modules' issuance against one ceiling is a real cross-service design decision,
/// not something to improvise inside a first-pass scaffold.</summary>
public sealed class MilestoneBand
{
    public MilestoneBand(int threshold, int guildContribution)
    {
        Threshold = threshold;
        GuildContribution = guildContribution;
    }

    public int Threshold { get; }
    public int GuildContribution { get; }
}

/// <summary>Placeholder stand-in for §4.1's future data-defined event manifest (objective IDs,
/// scenarios, contribution values, per-Expedition rotation) - that system does not exist yet.
/// A trusted server operation must never accept a client-supplied point value (that would let a
/// client mint its own score), so until a real manifest exists, this is the single place a
/// verified objective's point value comes from. Values here are illustrative placeholders, not
/// tuned economy numbers - picking real values is explicitly out of this scaffold's scope.</summary>
public interface IExpeditionManifest
{
    bool TryGetObjectivePoints(string objectiveId, out int points);
}

public sealed class StaticExpeditionManifest : IExpeditionManifest
{
    // One illustrative objective per route family from §4.3, plus a Builder-focus and a raid-phase
    // example - not a real tuned manifest. A production manifest would be per-Expedition-week
    // data, not a fixed in-code table.
    private static readonly Dictionary<string, int> ObjectivePoints = new(StringComparer.Ordinal)
    {
        ["scout.revealEnemyDeck"] = 40,
        ["scout.winWithInfoHandicap"] = 60,
        ["scout.defeatMarkedTarget"] = 80,
        ["supply.cooperativeDelivery"] = 50,
        ["supply.protectFormation"] = 70,
        ["assault.clearFormation"] = 90,
        ["assault.defeatObstacle"] = 100,
        ["builder.focusComplete"] = 60,
        ["raid.phaseOne"] = 120,
        ["raid.phaseTwo"] = 150,
        ["raid.phaseThree"] = 200,
    };

    public bool TryGetObjectivePoints(string objectiveId, out int points)
    {
        return ObjectivePoints.TryGetValue(objectiveId, out points);
    }
}

public sealed class GuildExpeditionStorageException : Exception
{
    public GuildExpeditionStorageException(string errorCode, Exception? innerException = null)
        : base("Guild Expedition storage failed.", innerException)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}

public interface IGuildExpeditionClock
{
    long UtcNowMs { get; }
    string CurrentDayKey { get; }
    string CurrentWeekKey { get; }
}

public sealed class SystemGuildExpeditionClock : IGuildExpeditionClock
{
    public long UtcNowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public string CurrentDayKey => DateTimeOffset.UtcNow.UtcDateTime.Date.ToString("yyyy-MM-dd");
    public string CurrentWeekKey => ExpeditionWeekKey.FromUtcMs(UtcNowMs);
}

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace MyriadOfDragons.CloudCode.Telemetry;

/// <summary>The exact wire shape the shipped client sends (Assets/Scripts/Metagame/
/// RetentionTelemetryGateway.cs, UnityCloudCodeRetentionTelemetryGateway.ToRequestDictionary): the
/// {"request": {...}} body of Telemetry.SendEvent. Nothing is added to this contract. PlayerId is
/// accepted only because the client sends it; it is NEVER trusted or forwarded - identity comes from
/// the authenticated execution context. ServerReceivedAtUtc is deliberately absent: it is
/// server-authoritative (locked register: "server receipt time is authoritative").</summary>
public sealed class SendEventRequest
{
    public string? EventId { get; set; }
    public string? PlayerId { get; set; }
    public string? EventType { get; set; }
    public long ClientOccurredAtUtc { get; set; }
    public int SchemaVersion { get; set; }
    public string? AppBuild { get; set; }
    public string? Mode { get; set; }
    public string? RunId { get; set; }
    public string? Outcome { get; set; }
}

/// <summary>Exactly {success, errorCode} - the client's RetentionTelemetryGatewayResult declares only
/// these two fields and its JSON deserializer THROWS on any unknown member, so nothing may be added
/// here. Deliberately carries no reward, balance, or player-facing text of any kind.</summary>
public sealed class SendEventResult
{
    [JsonProperty("success")]
    public bool Success { get; set; }
    [JsonProperty("errorCode")]
    public string? ErrorCode { get; set; }
}

/// <summary>The four LOCKED minimum event types (register: "Retention telemetry: Unity Analytics as
/// the sink" - "Telemetry events to send"), copied verbatim from the shipped client contract
/// (RetentionTelemetryEvents). No other event name is accepted.</summary>
public static class TelemetryEventTypes
{
    public const string ModeRunCompleted = "mode_run_completed";
    public const string ModeRewardClaimed = "mode_reward_claimed";
    public const string FeatureEntry = "feature_entry";
    public const string DailyCapReached = "daily_cap_reached";

    public static readonly IReadOnlyCollection<string> All = new[] { ModeRunCompleted, ModeRewardClaimed, FeatureEntry, DailyCapReached };
}

/// <summary>What is forwarded to the analytics sink - the privacy boundary. Only these fields ever
/// leave the gateway: a pseudonymous id (never the real PlayerId), the two timestamps (server one
/// authoritative, client one diagnostic only), and the schema-bound event fields. No free text, no
/// message content, no card lists, no economy values. Pinned by a reflection test.</summary>
public sealed class TelemetryRecord
{
    [JsonProperty("eventId")]
    public string EventId { get; set; } = string.Empty;
    [JsonProperty("pseudonymousId")]
    public string PseudonymousId { get; set; } = string.Empty;
    [JsonProperty("eventType")]
    public string EventType { get; set; } = string.Empty;
    [JsonProperty("serverReceivedAtUtcMs")]
    public long ServerReceivedAtUtcMs { get; set; }
    [JsonProperty("clientOccurredAtUtcMs")]
    public long ClientOccurredAtUtcMs { get; set; }
    [JsonProperty("schemaVersion")]
    public int SchemaVersion { get; set; }
    [JsonProperty("appBuild", NullValueHandling = NullValueHandling.Ignore)]
    public string? AppBuild { get; set; }
    [JsonProperty("mode", NullValueHandling = NullValueHandling.Ignore)]
    public string? Mode { get; set; }
    [JsonProperty("runId", NullValueHandling = NullValueHandling.Ignore)]
    public string? RunId { get; set; }
    [JsonProperty("outcome", NullValueHandling = NullValueHandling.Ignore)]
    public string? Outcome { get; set; }
}

/// <summary>Bounded per-player list of recently accepted eventIds - the server-side dedup ledger
/// ("eventId/runId dedup server-side"). It stores ids only, never events (the lock forbids using
/// Cloud Save as an event database). Oldest ids are evicted first once the bound is reached.</summary>
public sealed class SeenEventIds
{
    [JsonProperty("ids")]
    public List<string> Ids { get; set; } = new();

    [JsonIgnore]
    public string? WriteLock { get; set; }
}

public interface ITelemetryStore
{
    Task<SeenEventIds> LoadSeenAsync(IExecutionContext context, IGameApiClient apiClient, string playerId);
    Task SaveSeenAsync(IExecutionContext context, IGameApiClient apiClient, string playerId, SeenEventIds seen);
}

/// <summary>Analytics sink boundary. The locked design's sink is Unity Analytics (server-side REST
/// submission); the endpoint/credential/format details are an external dependency this repo does not
/// contain, so the shipped default is <see cref="NotConfiguredTelemetrySink"/> (fails closed).</summary>
public interface ITelemetrySink
{
    Task SubmitAsync(IExecutionContext context, IGameApiClient apiClient, TelemetryRecord record);
}

/// <summary>Fails closed: every submit reports SINK_NOT_CONFIGURED, so the client's outbox keeps the
/// event queued and retries - an unsent event is never reported as received.</summary>
public sealed class NotConfiguredTelemetrySink : ITelemetrySink
{
    public Task SubmitAsync(IExecutionContext context, IGameApiClient apiClient, TelemetryRecord record)
        => throw new TelemetrySinkException("SINK_NOT_CONFIGURED");
}

public interface ITelemetryClock
{
    long UtcNowMs { get; }
}

public sealed class SystemTelemetryClock : ITelemetryClock
{
    public long UtcNowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

public sealed class TelemetryStorageException : Exception
{
    public TelemetryStorageException(string errorCode, Exception? innerException = null)
        : base("Telemetry storage failed.", innerException)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}

public sealed class TelemetrySinkException : Exception
{
    public TelemetrySinkException(string errorCode, Exception? innerException = null)
        : base("Telemetry sink failed.", innerException)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}

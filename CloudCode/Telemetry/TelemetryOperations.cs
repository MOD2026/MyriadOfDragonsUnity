using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace MyriadOfDragons.CloudCode.Telemetry;

/// <summary>
/// Server-side retention-telemetry gateway: the locked architecture's "validate, deduplicate, and
/// forward" trust boundary between the client's bounded offline outbox and the analytics sink.
///
/// WHAT IT DOES: authenticates, validates the event against the shipped contract (4 locked event
/// types, schema version, safe-charset fields only), derives a pseudonymous id from the authenticated
/// context (the client-sent playerId is ignored), dedupes by eventId, stamps the authoritative server
/// receipt time, and forwards a minimal <see cref="TelemetryRecord"/> to the sink.
///
/// WHAT IT DELIBERATELY DOES NOT DO: store raw events (the lock forbids Cloud Save as an event DB),
/// precompute or store D1/D7/D14/D28 flags (cohorts are computed in analytics QUERIES from server
/// timestamps - see <see cref="RetentionCohorts"/>), touch PlayerProfile or any save schema, or
/// influence rewards/economy/stamina (analytics receives a COPY for reporting only). Responses carry
/// only {success, errorCode} - no player-facing claim of any kind.
///
/// CLIENT-OUTBOX CONTRACT (RetentionTelemetryOutbox.FlushAsync): the client removes an event only on
/// success=true and otherwise stops and retries later. So:
///  - transient failures (sink/storage down, unauthenticated) return success=false -> stays queued, retried;
///  - a duplicate of an already-accepted eventId returns success=true (idempotent) and forwards nothing;
///  - a PERMANENTLY invalid event returns success=true with a DROPPED_* errorCode and is NOT forwarded,
///    because success=false would leave a poison event at the head of the ordered queue forever and block
///    every event behind it. "Acknowledged and discarded" is explicit in the code, never "received".
///
/// DELIVERY SEMANTICS: at-least-once. The sink is called BEFORE the eventId is recorded as seen, so a
/// failure between the two re-forwards on retry (never loses the event); every record carries eventId so
/// the sink/queries can dedupe. Dedup window = <see cref="MaxRecentEventIds"/>, equal to the client
/// outbox bound: the outbox is ordered and stops at the first failure, so a retried event is always
/// within the last MaxQueuedEvents accepted ones.
/// </summary>
public sealed class TelemetryOperations
{
    /// <summary>Equals RetentionTelemetryOutbox.MaxQueuedEvents (500). Not an independent policy value:
    /// see the class comment for why a window of this size dedupes every possible retry.</summary>
    public const int MaxRecentEventIds = 500;

    /// <summary>Same as the client's RetentionTelemetryEvents.SchemaVersion.</summary>
    public const int SupportedSchemaVersion = 1;

    // Input-safety bounds (not product policy): no whitespace/'@'/free text can ride along, which is
    // what keeps message content, names, and card lists out of the payload.
    private const int MaxIdentifierLength = 64;
    private const int MaxAppBuildLength = 32;
    private static readonly Regex SafeToken = new("^[A-Za-z0-9._:+-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private const int MaxConflictReconciliations = 1;

    private readonly ITelemetryStore _store;
    private readonly ITelemetrySink _sink;
    private readonly ITelemetryClock _clock;

    public TelemetryOperations(ITelemetryStore store, ITelemetrySink sink, ITelemetryClock clock)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<SendEventResult> SendEventAsync(IExecutionContext context, IGameApiClient apiClient, SendEventRequest request)
    {
        if (context == null || string.IsNullOrWhiteSpace(context.PlayerId))
        {
            return Failure("AUTHENTICATION_REQUIRED");
        }

        string? invalid = Validate(request);
        if (invalid != null)
        {
            return Dropped(invalid);
        }

        string playerId = context.PlayerId;

        SeenEventIds seen;
        try
        {
            seen = await _store.LoadSeenAsync(context, apiClient, playerId);
        }
        catch (TelemetryStorageException exception)
        {
            return Failure(exception.ErrorCode);
        }

        if (seen.Ids.Contains(request.EventId!))
        {
            return new SendEventResult { Success = true };
        }

        var record = new TelemetryRecord
        {
            EventId = request.EventId!,
            PseudonymousId = PseudonymousIdFor(context),
            EventType = request.EventType!,
            ServerReceivedAtUtcMs = _clock.UtcNowMs,
            ClientOccurredAtUtcMs = request.ClientOccurredAtUtc,
            SchemaVersion = request.SchemaVersion,
            AppBuild = request.AppBuild,
            Mode = request.Mode,
            RunId = request.RunId,
            Outcome = request.Outcome,
        };

        try
        {
            await _sink.SubmitAsync(context, apiClient, record);
        }
        catch (TelemetrySinkException exception)
        {
            return Failure(exception.ErrorCode);
        }

        try
        {
            await MarkSeenAsync(context, apiClient, playerId, request.EventId!);
        }
        catch (TelemetryStorageException exception)
        {
            // Already forwarded; failing here makes the client retry, which re-forwards the same
            // eventId (at-least-once) rather than silently losing the dedup marker.
            return Failure(exception.ErrorCode);
        }

        return new SendEventResult { Success = true };
    }

    /// <summary>Stable pseudonymous id: the Unity Analytics user id when the context has one, else a
    /// one-way hash of the PlayerId. Never the raw PlayerId. Supports deletion by pseudonymous id.</summary>
    internal static string PseudonymousIdFor(IExecutionContext context)
    {
        string? analyticsUserId = context.AnalyticsUserId;
        if (!string.IsNullOrWhiteSpace(analyticsUserId))
        {
            return analyticsUserId!;
        }

        using SHA256 sha256 = SHA256.Create();
        string hex = BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(context.PlayerId))).Replace("-", string.Empty).ToLowerInvariant();
        return "p_" + hex.Substring(0, 32);
    }

    private async Task MarkSeenAsync(IExecutionContext context, IGameApiClient apiClient, string playerId, string eventId)
    {
        for (int attempt = 0; attempt <= MaxConflictReconciliations; attempt++)
        {
            try
            {
                var seen = await _store.LoadSeenAsync(context, apiClient, playerId);
                if (!seen.Ids.Contains(eventId))
                {
                    seen.Ids.Add(eventId);
                    if (seen.Ids.Count > MaxRecentEventIds)
                    {
                        seen.Ids.RemoveRange(0, seen.Ids.Count - MaxRecentEventIds);
                    }

                    await _store.SaveSeenAsync(context, apiClient, playerId, seen);
                }

                return;
            }
            catch (TelemetryStorageException exception) when (exception.ErrorCode == "CONFLICT" && attempt < MaxConflictReconciliations)
            {
            }
        }
    }

    /// <summary>Returns null when valid, else the DROPPED_* reason. Every string field must be a safe
    /// token (no whitespace, '@', or free text) and within its bound.</summary>
    private static string? Validate(SendEventRequest? request)
    {
        if (request == null
            || !IsSafeToken(request.EventId, MaxIdentifierLength, required: true)
            || string.IsNullOrEmpty(request.EventType)
            || !TelemetryEventTypes.All.Contains(request.EventType)
            || request.ClientOccurredAtUtc < 0
            || !IsSafeToken(request.AppBuild, MaxAppBuildLength, required: false)
            || !IsSafeToken(request.Mode, MaxIdentifierLength, required: false)
            || !IsSafeToken(request.RunId, MaxIdentifierLength, required: false)
            || !IsSafeToken(request.Outcome, MaxIdentifierLength, required: false))
        {
            return "DROPPED_INVALID_EVENT";
        }

        return request.SchemaVersion == SupportedSchemaVersion ? null : "DROPPED_UNSUPPORTED_SCHEMA";
    }

    private static bool IsSafeToken(string? value, int maxLength, bool required)
    {
        if (string.IsNullOrEmpty(value))
        {
            return !required;
        }

        return value!.Length <= maxLength && SafeToken.IsMatch(value);
    }

    private static SendEventResult Failure(string errorCode) => new() { Success = false, ErrorCode = errorCode };

    private static SendEventResult Dropped(string errorCode) => new() { Success = true, ErrorCode = errorCode };
}

public sealed class TelemetryModule
{
    private readonly TelemetryOperations _operations;

    public TelemetryModule()
        : this(new CloudSaveTelemetryStore(), new NotConfiguredTelemetrySink(), new SystemTelemetryClock())
    {
    }

    internal TelemetryModule(ITelemetryStore store, ITelemetrySink sink, ITelemetryClock clock)
    {
        _operations = new TelemetryOperations(store, sink, clock);
    }

    [CloudCodeFunction("SendEvent")]
    public Task<SendEventResult> SendEvent(IExecutionContext context, IGameApiClient apiClient, SendEventRequest request)
        => _operations.SendEventAsync(context, apiClient, request);
}

using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace MyriadOfDragons.CloudCode.Telemetry.Tests;

public sealed class TelemetryOperationsTests
{
    private const long ServerNow = 1_800_000_000_000L;

    // ---------------- enqueue / forward ----------------

    [Test]
    public async Task ValidEvent_IsForwardedOnce_WithTheServerTimestamp_AndAcknowledged()
    {
        var h = new Harness();

        var result = await h.SendAsync(Event("evt-1", clientAt: 5));

        Assert.That(result.Success, Is.True);
        Assert.That(result.ErrorCode, Is.Null);
        var record = h.Sink.Forwarded.Single();
        Assert.That(record.EventId, Is.EqualTo("evt-1"));
        Assert.That(record.ServerReceivedAtUtcMs, Is.EqualTo(ServerNow), "Receipt time must come from the server clock.");
        Assert.That(record.ClientOccurredAtUtcMs, Is.EqualTo(5), "The client timestamp is kept as an offline-delay diagnostic only.");
    }

    [TestCase(TelemetryEventTypes.ModeRunCompleted)]
    [TestCase(TelemetryEventTypes.ModeRewardClaimed)]
    [TestCase(TelemetryEventTypes.FeatureEntry)]
    [TestCase(TelemetryEventTypes.DailyCapReached)]
    public async Task EveryLockedEventType_IsAccepted(string eventType)
    {
        var h = new Harness();
        var result = await h.SendAsync(Event("evt-1", type: eventType, runId: null, outcome: null));
        Assert.That(result.Success, Is.True, result.ErrorCode);
        Assert.That(h.Sink.Forwarded.Single().EventType, Is.EqualTo(eventType));
    }

    // ---------------- retry ----------------

    [Test]
    public async Task SinkFailure_IsReportedForRetry_NothingMarkedSeen_ThenRetryForwardsExactlyOnce()
    {
        var h = new Harness();
        h.Sink.FailNextSubmits = 1;

        var failed = await h.SendAsync(Event("evt-1"));
        Assert.That(failed.Success, Is.False, "A failed forward must not be acknowledged - the client keeps the event queued.");
        Assert.That(failed.ErrorCode, Is.EqualTo("SINK_UNAVAILABLE"));
        Assert.That(h.Store.Ledger(PlayerId).Ids, Is.Empty, "An unforwarded event must not be recorded as seen.");

        var retried = await h.SendAsync(Event("evt-1"));
        Assert.That(retried.Success, Is.True, retried.ErrorCode);
        Assert.That(h.Sink.Forwarded.Select(r => r.EventId), Is.EqualTo(new[] { "evt-1" }));
        Assert.That(h.Store.Ledger(PlayerId).Ids, Is.EqualTo(new[] { "evt-1" }));
    }

    [Test]
    public async Task TheShippedDefaultSink_FailsClosed_SoTheClientKeepsEveryEventQueued()
    {
        var store = new FakeStore();
        var operations = new TelemetryOperations(store, new NotConfiguredTelemetrySink(), new FixedClock(ServerNow));

        var result = await operations.SendEventAsync(new Ctx(PlayerId), null!, Event("evt-1"));

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo("SINK_NOT_CONFIGURED"));
        Assert.That(store.Ledger(PlayerId).Ids, Is.Empty);
    }

    [Test]
    public async Task StorageLoadFailure_FailsClosed_NothingIsForwarded()
    {
        var h = new Harness();
        h.Store.FailLoads = true;

        var result = await h.SendAsync(Event("evt-1"));

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo("STORAGE_UNAVAILABLE"));
        Assert.That(h.Sink.Forwarded, Is.Empty, "Without a readable dedup ledger nothing may be forwarded.");
    }

    [Test]
    public async Task FailureAfterForwarding_RetryReforwardsTheSameEventId_AtLeastOnce_ThenDedupes()
    {
        var h = new Harness();
        h.Store.FailSaves = 1; // the sink accepts, then recording the eventId as seen fails

        var first = await h.SendAsync(Event("evt-1"));
        Assert.That(first.Success, Is.False);
        Assert.That(first.ErrorCode, Is.EqualTo("STORAGE_UNAVAILABLE"));

        var second = await h.SendAsync(Event("evt-1"));
        Assert.That(second.Success, Is.True, second.ErrorCode);
        Assert.That(h.Sink.Forwarded.Select(r => r.EventId), Is.EqualTo(new[] { "evt-1", "evt-1" }),
            "Documented at-least-once semantics: never lose the event; every record carries eventId so the sink can dedupe.");

        var third = await h.SendAsync(Event("evt-1"));
        Assert.That(third.Success, Is.True);
        Assert.That(h.Sink.Forwarded, Has.Count.EqualTo(2), "Once recorded, further retries are pure no-ops.");
    }

    [Test]
    public async Task ConflictWhileRecordingTheEventId_IsReconciledOnce_AndForwardsOnce()
    {
        var h = new Harness();
        h.Store.ConflictNextSaves = 1;

        var result = await h.SendAsync(Event("evt-1"));

        Assert.That(result.Success, Is.True, result.ErrorCode);
        Assert.That(h.Sink.Forwarded, Has.Count.EqualTo(1));
        Assert.That(h.Store.Ledger(PlayerId).Ids, Is.EqualTo(new[] { "evt-1" }));
    }

    // ---------------- deduplication ----------------

    [Test]
    public async Task DuplicateEventId_IsAcknowledgedAsSuccess_AndNeverReforwarded()
    {
        var h = new Harness();
        await h.SendAsync(Event("evt-1"));

        var duplicate = await h.SendAsync(Event("evt-1"));

        Assert.That(duplicate.Success, Is.True, "A lost-response retry must be acknowledged so the client can drop it.");
        Assert.That(duplicate.ErrorCode, Is.Null);
        Assert.That(h.Sink.Forwarded, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task TheSameEventId_FromDifferentPlayers_IsTrackedIndependently()
    {
        var h = new Harness();

        await h.SendAsync(Event("evt-1"), playerId: "player-a");
        await h.SendAsync(Event("evt-1"), playerId: "player-b");

        Assert.That(h.Sink.Forwarded, Has.Count.EqualTo(2));
        Assert.That(h.Sink.Forwarded.Select(r => r.PseudonymousId).Distinct().Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task InvalidEvents_AreNotRecordedAsSeen_SoAFixedEventIsNotShadowed()
    {
        var h = new Harness();
        await h.SendAsync(Event("evt-1", type: "not_a_locked_event"));
        Assert.That(h.Store.Ledger(PlayerId).Ids, Is.Empty);
    }

    // ---------------- bounds ----------------

    [Test]
    public async Task DedupLedger_IsBoundedToTheClientOutboxSize_EvictingTheOldestFirst()
    {
        var h = new Harness();
        int total = TelemetryOperations.MaxRecentEventIds + 5;
        for (int i = 0; i < total; i++)
        {
            var r = await h.SendAsync(Event($"evt-{i}"));
            Assert.That(r.Success, Is.True, r.ErrorCode);
        }

        var ledger = h.Store.Ledger(PlayerId).Ids;
        Assert.That(ledger, Has.Count.EqualTo(TelemetryOperations.MaxRecentEventIds));
        Assert.That(ledger.First(), Is.EqualTo("evt-5"), "The 5 oldest ids must have been evicted.");
        Assert.That(ledger.Last(), Is.EqualTo($"evt-{total - 1}"));

        int forwardedBefore = h.Sink.Forwarded.Count;
        await h.SendAsync(Event($"evt-{total - 1}"));
        Assert.That(h.Sink.Forwarded, Has.Count.EqualTo(forwardedBefore), "A recent id is still deduped.");
    }

    [Test]
    public void TheDedupWindow_EqualsTheClientOutboxBound()
    {
        // RetentionTelemetryOutbox.MaxQueuedEvents = 500 (Assets/Scripts/Metagame/RetentionTelemetryOutbox.cs). The
        // outbox is ordered and stops at the first failure, so a retried event is always within the last 500.
        Assert.That(TelemetryOperations.MaxRecentEventIds, Is.EqualTo(500));
    }

    // ---------------- persistence ----------------

    [Test]
    public async Task DedupLedger_IsPersisted_AndSurvivesANewOperationsInstance()
    {
        var store = new FakeStore();
        var sink = new FakeSink();
        await new TelemetryOperations(store, sink, new FixedClock(ServerNow)).SendEventAsync(new Ctx(PlayerId), null!, Event("evt-1"));

        // A fresh instance (a new Cloud Code invocation) sharing only the persisted store.
        var second = await new TelemetryOperations(store, sink, new FixedClock(ServerNow)).SendEventAsync(new Ctx(PlayerId), null!, Event("evt-1"));

        Assert.That(second.Success, Is.True);
        Assert.That(sink.Forwarded, Has.Count.EqualTo(1));
    }

    [Test]
    public void SeenLedger_RoundTripsThroughItsJsonContract_PreservingOrder_AndNeverSerializingTheWriteLock()
    {
        var seen = new SeenEventIds { Ids = { "a", "b", "c" }, WriteLock = "lock-123" };

        string json = JsonConvert.SerializeObject(seen);
        var back = JsonConvert.DeserializeObject<SeenEventIds>(json)!;

        Assert.That(json, Is.EqualTo("{\"ids\":[\"a\",\"b\",\"c\"]}"));
        Assert.That(back.Ids, Is.EqualTo(new[] { "a", "b", "c" }));
        Assert.That(back.WriteLock, Is.Null);
    }

    // ---------------- poison events (acknowledge + discard) ----------------

    [TestCase("unknown type", "not_a_locked_event", "evt-1", "empire_expedition", null)]
    [TestCase("blank eventId", TelemetryEventTypes.FeatureEntry, "", "empire_expedition", null)]
    [TestCase("eventId with a space", TelemetryEventTypes.FeatureEntry, "evt 1", "empire_expedition", null)]
    [TestCase("email in outcome", TelemetryEventTypes.ModeRunCompleted, "evt-1", "empire_expedition", "player@example.com")]
    [TestCase("free text in mode", TelemetryEventTypes.FeatureEntry, "evt-1", "my secret deck list", null)]
    public async Task InvalidEvent_IsAcknowledgedAndDiscarded_NeverForwarded_SoItCannotBlockTheClientQueue(string label, string type, string eventId, string mode, string? outcome)
    {
        var h = new Harness();

        var result = await h.SendAsync(Event(eventId, type: type, mode: mode, outcome: outcome));

        Assert.That(result.Success, Is.True, $"{label}: success=false would leave a poison event at the head of the ordered client queue forever.");
        Assert.That(result.ErrorCode, Is.EqualTo("DROPPED_INVALID_EVENT"), label);
        Assert.That(h.Sink.Forwarded, Is.Empty, label);
    }

    [Test]
    public async Task OverlongIdentifiersAndNegativeClientTime_AreDiscarded()
    {
        var h = new Harness();
        var longMode = await h.SendAsync(Event("evt-1", mode: new string('a', 65)));
        var negative = await h.SendAsync(Event("evt-2", clientAt: -1));
        Assert.That(longMode.ErrorCode, Is.EqualTo("DROPPED_INVALID_EVENT"));
        Assert.That(negative.ErrorCode, Is.EqualTo("DROPPED_INVALID_EVENT"));
        Assert.That(h.Sink.Forwarded, Is.Empty);
    }

    [Test]
    public async Task UnsupportedSchemaVersion_IsDiscardedWithItsOwnCode()
    {
        var h = new Harness();
        var request = Event("evt-1");
        request.SchemaVersion = 2;

        var result = await h.SendAsync(request);

        Assert.That(result.Success, Is.True);
        Assert.That(result.ErrorCode, Is.EqualTo("DROPPED_UNSUPPORTED_SCHEMA"));
        Assert.That(h.Sink.Forwarded, Is.Empty);
    }

    [Test]
    public async Task NullRequest_IsDiscarded()
    {
        var h = new Harness();
        var result = await new TelemetryOperations(h.Store, h.Sink, new FixedClock(ServerNow)).SendEventAsync(new Ctx(PlayerId), null!, null!);
        Assert.That(result.Success, Is.True);
        Assert.That(result.ErrorCode, Is.EqualTo("DROPPED_INVALID_EVENT"));
    }

    // ---------------- authentication ----------------

    [Test]
    public async Task UnauthenticatedCaller_IsRejectedForRetry_WithoutTouchingStoreOrSink()
    {
        var h = new Harness();

        var result = await h.SendAsync(Event("evt-1"), playerId: null);

        Assert.That(result.Success, Is.False, "Retryable: the queue keeps the event until the player is signed in.");
        Assert.That(result.ErrorCode, Is.EqualTo("AUTHENTICATION_REQUIRED"));
        Assert.That(h.Store.LoadCount, Is.EqualTo(0));
        Assert.That(h.Sink.Forwarded, Is.Empty);
    }

    // ---------------- privacy ----------------

    [Test]
    public void ForwardedRecord_HasOnlyTheApprovedFields_NoFreeTextNoPlayerIdNoEconomy()
    {
        var fields = typeof(TelemetryRecord).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(p => p.GetCustomAttribute<JsonPropertyAttribute>()!.PropertyName!)
            .OrderBy(n => n)
            .ToArray();

        Assert.That(fields, Is.EqualTo(new[]
        {
            "appBuild", "clientOccurredAtUtcMs", "eventId", "eventType", "mode", "outcome",
            "pseudonymousId", "runId", "schemaVersion", "serverReceivedAtUtcMs",
        }));
    }

    [Test]
    public async Task ClientSuppliedPlayerId_IsIgnored_TheIdentityComesFromTheAuthenticatedContext()
    {
        var h = new Harness();
        var request = Event("evt-1");
        request.PlayerId = "victim-player-id";

        await h.SendAsync(request, playerId: "real-player-id");

        var record = h.Sink.Forwarded.Single();
        string json = JsonConvert.SerializeObject(record);
        Assert.That(json, Does.Not.Contain("victim-player-id"));
        Assert.That(json, Does.Not.Contain("real-player-id"), "The raw PlayerId must never be forwarded either.");
        Assert.That(record.PseudonymousId, Does.StartWith("p_").And.Length.EqualTo(34));
    }

    [Test]
    public async Task PseudonymousId_PrefersTheAnalyticsUserId_AndIsStableAcrossCalls()
    {
        var store = new FakeStore();
        var sink = new FakeSink();
        var operations = new TelemetryOperations(store, sink, new FixedClock(ServerNow));

        await operations.SendEventAsync(new Ctx("real-player-id", analyticsUserId: "analytics-user-123"), null!, Event("evt-1"));
        await operations.SendEventAsync(new Ctx("real-player-id", analyticsUserId: "analytics-user-123"), null!, Event("evt-2"));

        Assert.That(sink.Forwarded.Select(r => r.PseudonymousId).Distinct(), Is.EqualTo(new[] { "analytics-user-123" }));
    }

    [Test]
    public async Task TheHashedFallbackPseudonym_IsStableForOnePlayer_AndDistinctBetweenPlayers()
    {
        var h = new Harness();
        await h.SendAsync(Event("evt-1"), playerId: "player-a");
        await h.SendAsync(Event("evt-2"), playerId: "player-a");
        await h.SendAsync(Event("evt-3"), playerId: "player-b");

        var ids = h.Sink.Forwarded.Select(r => r.PseudonymousId).ToArray();
        Assert.That(ids[0], Is.EqualTo(ids[1]));
        Assert.That(ids[0], Is.Not.EqualTo(ids[2]));
    }

    // ---------------- client wire contract ----------------

    [Test]
    public void ResponseShape_IsExactlySuccessAndErrorCode_BecauseTheClientDeserializerRejectsUnknownMembers()
    {
        var json = JObject.Parse(JsonConvert.SerializeObject(new SendEventResult { Success = true }));
        Assert.That(json.Properties().Select(p => p.Name).OrderBy(n => n), Is.EqualTo(new[] { "errorCode", "success" }));
    }

    [Test]
    public void RequestShape_MatchesTheShippedClientContract_Exactly()
    {
        // Assets/Scripts/Metagame/RetentionTelemetryGateway.cs ToRequestDictionary sends exactly these keys.
        var props = typeof(SendEventRequest).GetProperties().Select(p => char.ToLowerInvariant(p.Name[0]) + p.Name.Substring(1)).OrderBy(n => n);
        Assert.That(props, Is.EqualTo(new[] { "appBuild", "clientOccurredAtUtc", "eventId", "eventType", "mode", "outcome", "playerId", "runId", "schemaVersion" }));
    }

    [Test]
    public void EventTypesAndSchemaVersion_AreExactlyTheShippedClientContract_NoInventedNames()
    {
        Assert.That(TelemetryEventTypes.All, Is.EquivalentTo(new[] { "mode_run_completed", "mode_reward_claimed", "feature_entry", "daily_cap_reached" }));
        Assert.That(TelemetryOperations.SupportedSchemaVersion, Is.EqualTo(1));
    }

    [Test]
    public void TheModuleExposesExactlyOneEndpoint_SendEvent()
    {
        string[] endpoints = typeof(TelemetryOperations).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .SelectMany(m => m.CustomAttributes.Where(a => a.AttributeType.Name == "CloudCodeFunctionAttribute"))
            .Select(a => (string)a.ConstructorArguments[0].Value!)
            .ToArray();
        Assert.That(endpoints, Is.EqualTo(new[] { "SendEvent" }));
    }

    // ---------------- helpers ----------------

    private const string PlayerId = "player-1";

    private static SendEventRequest Event(string eventId, string type = TelemetryEventTypes.ModeRunCompleted, long clientAt = 1_799_999_999_000L,
        string? mode = "empire_expedition", string? runId = "run-1", string? outcome = "success") => new()
    {
        EventId = eventId,
        PlayerId = "client-side-id",
        EventType = type,
        ClientOccurredAtUtc = clientAt,
        SchemaVersion = 1,
        AppBuild = "0.1.0",
        Mode = mode,
        RunId = runId,
        Outcome = outcome,
    };

    private sealed class Harness
    {
        public FakeStore Store { get; } = new();
        public FakeSink Sink { get; } = new();

        public Task<SendEventResult> SendAsync(SendEventRequest request, string? playerId = PlayerId) =>
            new TelemetryOperations(Store, Sink, new FixedClock(ServerNow)).SendEventAsync(new Ctx(playerId), null!, request);
    }

    private sealed class FakeStore : ITelemetryStore
    {
        private readonly Dictionary<string, string> _persisted = new();
        public int LoadCount { get; private set; }
        public bool FailLoads { get; set; }
        public int FailSaves { get; set; }
        public int ConflictNextSaves { get; set; }

        public SeenEventIds Ledger(string playerId) =>
            _persisted.TryGetValue(playerId, out var json) ? JsonConvert.DeserializeObject<SeenEventIds>(json)! : new SeenEventIds();

        public Task<SeenEventIds> LoadSeenAsync(IExecutionContext context, IGameApiClient apiClient, string playerId)
        {
            LoadCount++;
            if (FailLoads)
            {
                throw new TelemetryStorageException("STORAGE_UNAVAILABLE");
            }

            return Task.FromResult(Ledger(playerId));
        }

        public Task SaveSeenAsync(IExecutionContext context, IGameApiClient apiClient, string playerId, SeenEventIds seen)
        {
            if (ConflictNextSaves > 0)
            {
                ConflictNextSaves--;
                throw new TelemetryStorageException("CONFLICT");
            }

            if (FailSaves > 0)
            {
                FailSaves--;
                throw new TelemetryStorageException("STORAGE_UNAVAILABLE");
            }

            _persisted[playerId] = JsonConvert.SerializeObject(seen);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSink : ITelemetrySink
    {
        public List<TelemetryRecord> Forwarded { get; } = new();
        public int FailNextSubmits { get; set; }

        public Task SubmitAsync(IExecutionContext context, IGameApiClient apiClient, TelemetryRecord record)
        {
            if (FailNextSubmits > 0)
            {
                FailNextSubmits--;
                throw new TelemetrySinkException("SINK_UNAVAILABLE");
            }

            Forwarded.Add(JsonConvert.DeserializeObject<TelemetryRecord>(JsonConvert.SerializeObject(record))!);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock : ITelemetryClock
    {
        public FixedClock(long now) => UtcNowMs = now;
        public long UtcNowMs { get; }
    }

    private sealed class Ctx : IExecutionContext
    {
        public Ctx(string? playerId, string? analyticsUserId = null)
        {
            PlayerId = playerId!;
            AnalyticsUserId = analyticsUserId!;
        }

        public string ProjectId => "project";
        public string PlayerId { get; }
        public string EnvironmentId => "environment";
        public string EnvironmentName => "nonprod-validation";
        public string AccessToken => "test-token";
        public string UserId => null!;
        public string Issuer => null!;
        public string ServiceToken => "test-service-token";
        public string AnalyticsUserId { get; }
        public string UnityInstallationId => null!;
        public string CorrelationId => null!;
        public string ScopeId => null!;
        public int CallDepth => 0;
        public ISession Session => null!;
    }
}

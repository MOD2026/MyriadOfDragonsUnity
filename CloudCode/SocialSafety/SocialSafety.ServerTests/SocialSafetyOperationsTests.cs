using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Core;

namespace MyriadOfDragons.CloudCode.SocialSafety.Tests;

public sealed class SocialSafetyOperationsTests
{
    [Test]
    public async Task MissingActorIdentityIsRejected()
    {
        var result = await Create().BlockAccountAsync(new FakeExecutionContext(null), null!, Request("target"));
        Assert.That(result.ErrorCode, Is.EqualTo("AUTHENTICATION_REQUIRED"));
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task BlankTargetIsRejected(string target)
    {
        var result = await Create().BlockAccountAsync(new FakeExecutionContext("actor"), null!, Request(target));
        Assert.That(result.ErrorCode, Is.EqualTo("INVALID_REQUEST"));
    }

    [Test]
    public async Task SelfBlockAndSelfMuteAreRejected()
    {
        var operations = Create();
        var block = await operations.BlockAccountAsync(new FakeExecutionContext("actor"), null!, Request("actor"));
        var mute = await operations.MuteAccountAsync(new FakeExecutionContext("actor"), null!, Request("actor"));
        Assert.That(block.ErrorCode, Is.EqualTo("SELF_TARGET_NOT_ALLOWED"));
        Assert.That(mute.ErrorCode, Is.EqualTo("SELF_TARGET_NOT_ALLOWED"));
    }

    [Test]
    public async Task FirstBlockCreatesOneCanonicalRecordAndDuplicateReturnsIt()
    {
        var store = new FakeStore();
        var operations = Create(store);
        var first = await operations.BlockAccountAsync(Context(), null!, Request("target"));
        var duplicate = await operations.BlockAccountAsync(Context(), null!, Request("target"));
        Assert.That(first.Relationship!.Id, Is.EqualTo(duplicate.Relationship!.Id));
        Assert.That(store.SaveCount, Is.EqualTo(2));
        Assert.That(duplicate.Changed, Is.False);
    }

    [Test]
    public async Task ConcurrentFirstBlocksShareOneCanonicalPersistedRecord()
    {
        var store = new FakeStore(concurrentFirstLoads: true);
        var operations = Create(store);
        var results = await Task.WhenAll(
            operations.BlockAccountAsync(Context(), null!, Request("target")),
            operations.BlockAccountAsync(Context(), null!, Request("target")));

        Assert.That(results, Has.All.Property(nameof(RelationshipResult.Success)).EqualTo(true));
        Assert.That(results[0].Relationship!.Id, Is.EqualTo(results[1].Relationship!.Id));
        Assert.That(store.Persisted.Relationship!.Id, Is.EqualTo(results[0].Relationship.Id));
        Assert.That(store.SaveCount, Is.EqualTo(2));
    }

    [Test]
    public async Task ConcurrentFirstMutesShareOneCanonicalPersistedRecord()
    {
        var store = new FakeStore(concurrentFirstLoads: true);
        var operations = Create(store, new FakeClock(1000), new FakePolicy(500));
        var results = await Task.WhenAll(
            operations.MuteAccountAsync(Context(), null!, Request("target")),
            operations.MuteAccountAsync(Context(), null!, Request("target")));

        Assert.That(results, Has.All.Property(nameof(RelationshipResult.Success)).EqualTo(true));
        Assert.That(results[0].Relationship!.Id, Is.EqualTo(results[1].Relationship!.Id));
        Assert.That(store.Persisted.Relationship!.Id, Is.EqualTo(results[0].Relationship.Id));
        Assert.That(store.Persisted.Relationship.Status, Is.EqualTo("active"));
    }

    [Test]
    public async Task StaleWriteIsReconciledAgainstAuthoritativeState()
    {
        var store = new FakeStore(concurrentFirstLoads: true);
        var operations = Create(store);
        var results = await Task.WhenAll(
            operations.BlockAccountAsync(Context(), null!, Request("target")),
            operations.BlockAccountAsync(Context(), null!, Request("target")));

        Assert.That(results, Has.All.Property(nameof(RelationshipResult.Success)).EqualTo(true));
        Assert.That(store.ConflictCount, Is.EqualTo(1));
        Assert.That(results[1].ErrorCode, Is.Null);
    }

    [Test]
    public async Task MuteAfterUnmuteSequentiallyReactivatesCanonicalRecord()
    {
        var store = new FakeStore();
        var operations = Create(store, new FakeClock(1000), new FakePolicy(500));
        var first = await operations.MuteAccountAsync(Context(), null!, Request("target"));
        await operations.UnmuteAccountAsync(Context(), null!, Request("target"));
        var remuted = await operations.MuteAccountAsync(Context(), null!, Request("target"));

        Assert.That(remuted.Relationship!.Id, Is.EqualTo(first.Relationship!.Id));
        Assert.That(remuted.Relationship.Status, Is.EqualTo("active"));
    }

    [Test]
    public async Task ConcurrentBlockAndUnblockRaceIsLinearizable()
    {
        var store = ActiveStore(concurrentFirstLoads: true, relationshipKind: "block");
        var operations = Create(store);
        var results = await Task.WhenAll(
            operations.UnblockAccountAsync(Context(), null!, Request("target")),
            operations.BlockAccountAsync(Context(), null!, Request("target")));

        AssertConcurrentResultsAreCanonical(results, store, "block", 1000);
        Assert.That(store.ConflictCount, Is.GreaterThanOrEqualTo(1));
        Assert.That(store.Persisted.Relationship!.Status, Is.AnyOf("active", "inactive"));
        Assert.That(store.Persisted.Relationship.MuteUntilUtcMs, Is.EqualTo(0));
    }

    [Test]
    public async Task ConcurrentMuteAndUnmuteRaceIsLinearizable()
    {
        var store = ActiveStore(concurrentFirstLoads: true, relationshipKind: "mute");
        var operations = Create(store, new FakeClock(1000), new FakePolicy(500));
        var results = await Task.WhenAll(
            operations.UnmuteAccountAsync(Context(), null!, Request("target")),
            operations.MuteAccountAsync(Context(), null!, Request("target")));

        AssertConcurrentResultsAreCanonical(results, store, "mute", 1000);
        Assert.That(store.ConflictCount, Is.GreaterThanOrEqualTo(1));
        Assert.That(store.Persisted.Relationship!.Status, Is.AnyOf("active", "inactive"));
        if (store.Persisted.Relationship.Status == "inactive")
        {
            Assert.That(store.Persisted.Relationship.MuteUntilUtcMs, Is.EqualTo(0));
        }
        else
        {
            Assert.That(store.Persisted.Relationship.MuteUntilUtcMs, Is.GreaterThan(1000));
        }
    }

    [Test]
    public async Task ExhaustedStaleWriteReturnsSanitizedConflictWithoutLooping()
    {
        var store = ActiveStore(forceConflictCount: 2);
        var operations = Create(store);
        var result = await operations.BlockAccountAsync(Context(), null!, Request("target"));

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo("CONFLICT"));
        Assert.That(result.Relationship, Is.Null);
        Assert.That(store.RateSaveAttemptCount, Is.EqualTo(2));
        Assert.That(store.RateLoadCount, Is.EqualTo(2));
        Assert.That(store.LoadCount, Is.EqualTo(0));
    }

    [Test]
    public async Task TenthMutationSucceedsAndEleventhIsRateLimited()
    {
        var store = new FakeStore();
        var operations = Create(store, config: new FixedRateLimitConfiguration(10, 100));

        for (int index = 0; index < 10; index++)
        {
            Assert.That((await operations.BlockAccountAsync(Context(), null!, Request("target"))).Success, Is.True);
        }

        var eleventh = await operations.BlockAccountAsync(Context(), null!, Request("target"));
        Assert.That(eleventh.ErrorCode, Is.EqualTo("RATE_LIMITED"));
    }

    [Test]
    public async Task AllFourOperationsShareOneMinuteCounter()
    {
        var store = new FakeStore();
        var operations = Create(store, config: new FixedRateLimitConfiguration(10, 100));
        var mutations = new Func<Task<RelationshipResult>>[]
        {
            () => operations.BlockAccountAsync(Context(), null!, Request("target")),
            () => operations.UnblockAccountAsync(Context(), null!, Request("target")),
            () => operations.MuteAccountAsync(Context(), null!, Request("target")),
            () => operations.UnmuteAccountAsync(Context(), null!, Request("target"))
        };

        for (int index = 0; index < 10; index++)
        {
            Assert.That((await mutations[index % mutations.Length]()).Success, Is.True);
        }

        Assert.That((await operations.UnmuteAccountAsync(Context(), null!, Request("target"))).ErrorCode, Is.EqualTo("RATE_LIMITED"));
    }

    [Test]
    public async Task DailyCounterRejectsTheOneHundredFirstMutation()
    {
        var store = new FakeStore();
        var operations = Create(store, config: new FixedRateLimitConfiguration(200, 100));
        for (int index = 0; index < 100; index++)
        {
            Assert.That((await operations.BlockAccountAsync(Context(), null!, Request("target"))).Success, Is.True);
        }

        Assert.That((await operations.BlockAccountAsync(Context(), null!, Request("target"))).ErrorCode, Is.EqualTo("RATE_LIMITED"));
    }

    [Test]
    public async Task AdvancingServerClockResetsMinuteWindow()
    {
        var clock = new FakeClock(1000);
        var operations = Create(new FakeStore(), clock, config: new FixedRateLimitConfiguration(1, 100));
        Assert.That((await operations.BlockAccountAsync(Context(), null!, Request("target"))).Success, Is.True);
        Assert.That((await operations.BlockAccountAsync(Context(), null!, Request("target"))).ErrorCode, Is.EqualTo("RATE_LIMITED"));

        clock.Now += 60_000;
        Assert.That((await operations.BlockAccountAsync(Context(), null!, Request("target"))).Success, Is.True);
    }

    [Test]
    public async Task AdvancingServerClockByOneDayResetsDailyWindow()
    {
        var clock = new FakeClock(1000);
        var operations = Create(new FakeStore(), clock, config: new FixedRateLimitConfiguration(200, 1));
        Assert.That((await operations.BlockAccountAsync(Context(), null!, Request("target"))).Success, Is.True);
        Assert.That((await operations.BlockAccountAsync(Context(), null!, Request("target"))).ErrorCode, Is.EqualTo("RATE_LIMITED"));

        clock.Now += 86_400_000;
        Assert.That((await operations.BlockAccountAsync(Context(), null!, Request("target"))).Success, Is.True);
    }

    [Test]
    public async Task RequestCannotSupplyAWindowTimestamp()
    {
        Assert.That(typeof(RelationshipRequest).GetProperties().Any(property => property.Name.Contains("Time", StringComparison.OrdinalIgnoreCase)), Is.False);
        var operations = Create(new FakeStore(), config: new FixedRateLimitConfiguration(1, 100));
        Assert.That((await operations.BlockAccountAsync(Context(), null!, Request("target"))).Success, Is.True);
        Assert.That((await operations.BlockAccountAsync(Context(), null!, Request("target"))).ErrorCode, Is.EqualTo("RATE_LIMITED"));
    }

    [Test]
    public async Task ConcurrentRequestsCannotExceedMinuteCap()
    {
        var store = new FakeStore(concurrentFirstLoads: true, concurrentRelationshipLoads: false);
        var operations = Create(store, config: new FixedRateLimitConfiguration(1, 100));
        var results = await Task.WhenAll(
            operations.BlockAccountAsync(Context(), null!, Request("target")),
            operations.MuteAccountAsync(Context(), null!, Request("target")));

        Assert.That(results.Count(result => result.Success), Is.EqualTo(1));
        Assert.That(results.Count(result => result.ErrorCode == "RATE_LIMITED"), Is.EqualTo(1));
        Assert.That(store.RateConflictCount, Is.GreaterThanOrEqualTo(1));
        Assert.That(store.RatePersisted!.MinuteCount, Is.EqualTo(1));
    }

    [Test]
    public async Task ConcurrentRateReservationsDoNotDoubleCountAfterStaleRetry()
    {
        var store = new FakeStore(concurrentFirstLoads: true);
        var operations = Create(store, config: new FixedRateLimitConfiguration(2, 100));
        var results = await Task.WhenAll(
            operations.BlockAccountAsync(Context(), null!, Request("target")),
            operations.MuteAccountAsync(Context(), null!, Request("target")));

        Assert.That(results, Has.All.Property(nameof(RelationshipResult.Success)).EqualTo(true));
        Assert.That(store.RateConflictCount, Is.GreaterThanOrEqualTo(1));
        Assert.That(store.RatePersisted!.MinuteCount, Is.EqualTo(2));
        Assert.That(store.RatePersisted.DayCount, Is.EqualTo(2));
        Assert.That(store.RateSaveCount, Is.EqualTo(2));
    }

    [Test]
    public async Task ValidationAndSelfTargetFailuresDoNotConsumeQuota()
    {
        var store = new FakeStore();
        var operations = Create(store, config: new FixedRateLimitConfiguration(1, 100));
        Assert.That((await operations.BlockAccountAsync(Context(), null!, Request(" "))).ErrorCode, Is.EqualTo("INVALID_REQUEST"));
        Assert.That((await operations.BlockAccountAsync(Context(), null!, Request("actor"))).ErrorCode, Is.EqualTo("SELF_TARGET_NOT_ALLOWED"));
        Assert.That(store.RateSaveCount, Is.EqualTo(0));
        Assert.That((await operations.BlockAccountAsync(Context(), null!, Request("target"))).Success, Is.True);
    }

    [Test]
    public async Task RelationshipFailureAfterReservationConsumesThatReservation()
    {
        var store = new FakeStore(relationshipFailure: "STORAGE_UNAVAILABLE");
        var operations = Create(store, config: new FixedRateLimitConfiguration(1, 100));
        var failed = await operations.BlockAccountAsync(Context(), null!, Request("target"));

        Assert.That(failed.ErrorCode, Is.EqualTo("STORAGE_UNAVAILABLE"));
        Assert.That(store.RatePersisted!.MinuteCount, Is.EqualTo(1));
        Assert.That((await operations.BlockAccountAsync(Context(), null!, Request("target"))).ErrorCode, Is.EqualTo("RATE_LIMITED"));
    }

    [TestCase(100, 10, 100)]
    [TestCase(101, 10, 100)]
    [TestCase(int.MaxValue, 10, 100)]
    [TestCase(0, 10, 100)]
    [TestCase(-1, 10, 100)]
    [TestCase("malformed", 10, 100)]
    public void PerMinuteConfigurationRespectsCeiling(object configured, int fallback, int maximum)
    {
        int result = ReadConfiguredInt(
            new Dictionary<string, object> { { RemoteConfigSocialSafetyRateLimitConfiguration.PerMinuteKey, configured } },
            RemoteConfigSocialSafetyRateLimitConfiguration.PerMinuteKey,
            fallback,
            maximum);

        Assert.That(result, Is.EqualTo(configured.Equals(maximum) ? maximum : fallback));
    }

    [TestCase(1000, 100, 1000)]
    [TestCase(1001, 100, 1000)]
    [TestCase(int.MaxValue, 100, 1000)]
    [TestCase(0, 100, 1000)]
    [TestCase(-1, 100, 1000)]
    [TestCase("malformed", 100, 1000)]
    public void PerDayConfigurationRespectsCeiling(object configured, int fallback, int maximum)
    {
        int result = ReadConfiguredInt(
            new Dictionary<string, object> { { RemoteConfigSocialSafetyRateLimitConfiguration.PerDayKey, configured } },
            RemoteConfigSocialSafetyRateLimitConfiguration.PerDayKey,
            fallback,
            maximum);

        Assert.That(result, Is.EqualTo(configured.Equals(maximum) ? maximum : fallback));
    }

    [Test]
    public async Task UnblockDeactivatesAndRepeatedUnblockIsNoChange()
    {
        var store = new FakeStore();
        var operations = Create(store);
        var created = await operations.BlockAccountAsync(Context(), null!, Request("target"));
        var removed = await operations.UnblockAccountAsync(Context(), null!, Request("target"));
        var repeated = await operations.UnblockAccountAsync(Context(), null!, Request("target"));
        Assert.That(removed.Relationship!.Id, Is.EqualTo(created.Relationship!.Id));
        Assert.That(removed.Relationship.Status, Is.EqualTo("inactive"));
        Assert.That(removed.Changed, Is.True);
        Assert.That(repeated.Changed, Is.False);
    }

    [Test]
    public async Task BlockAfterUnblockSequentiallyReactivatesCanonicalRecord()
    {
        var store = new FakeStore();
        var operations = Create(store);
        var first = await operations.BlockAccountAsync(Context(), null!, Request("target"));
        await operations.UnblockAccountAsync(Context(), null!, Request("target"));
        var reactivated = await operations.BlockAccountAsync(Context(), null!, Request("target"));
        Assert.That(reactivated.Relationship!.Id, Is.EqualTo(first.Relationship!.Id));
        Assert.That(reactivated.Relationship.Status, Is.EqualTo("active"));
    }

    [Test]
    public async Task FirstMuteCreatesCanonicalRecordAndRemuteUpdatesIt()
    {
        var store = new FakeStore();
        var clock = new FakeClock(1000);
        var operations = Create(store, clock, new FakePolicy(500));
        var first = await operations.MuteAccountAsync(Context(), null!, Request("target"));
        clock.Now = 1100;
        var remute = await operations.MuteAccountAsync(Context(), null!, Request("target"));
        Assert.That(remute.Relationship!.Id, Is.EqualTo(first.Relationship!.Id));
        Assert.That(remute.Relationship.MuteUntilUtcMs, Is.EqualTo(1600));
        Assert.That(store.SaveCount, Is.EqualTo(2));
    }

    [Test]
    public async Task MuteExpiryUsesInjectedClock()
    {
        var store = new FakeStore();
        var clock = new FakeClock(1000);
        var operations = Create(store, clock, new FakePolicy(500));
        var first = await operations.MuteAccountAsync(Context(), null!, Request("target"));
        clock.Now = 1501;
        var remute = await operations.MuteAccountAsync(Context(), null!, Request("target"));
        Assert.That(remute.Changed, Is.True);
        Assert.That(remute.Relationship!.Id, Is.EqualTo(first.Relationship!.Id));
    }

    [Test]
    public async Task UnmuteDeactivatesAndRepeatedUnmuteIsNoChange()
    {
        var store = new FakeStore();
        var operations = Create(store);
        await operations.MuteAccountAsync(Context(), null!, Request("target"));
        var removed = await operations.UnmuteAccountAsync(Context(), null!, Request("target"));
        var repeated = await operations.UnmuteAccountAsync(Context(), null!, Request("target"));
        Assert.That(removed.Relationship!.Status, Is.EqualTo("inactive"));
        Assert.That(removed.Changed, Is.True);
        Assert.That(repeated.Changed, Is.False);
    }

    [Test]
    public async Task RateLimitIsEnforcedByInjectedPolicy()
    {
        var result = await Create(policy: new FakePolicy(500, true)).BlockAccountAsync(Context(), null!, Request("target"));
        Assert.That(result.ErrorCode, Is.EqualTo("RATE_LIMITED"));
    }

    [Test]
    public async Task StorageUnavailableAndConflictAreSanitized()
    {
        var unavailable = await Create(new FakeStore("STORAGE_UNAVAILABLE")).BlockAccountAsync(Context(), null!, Request("target"));
        var conflict = await Create(new FakeStore("CONFLICT")).BlockAccountAsync(Context(), null!, Request("target"));
        Assert.That(unavailable.ErrorCode, Is.EqualTo("STORAGE_UNAVAILABLE"));
        Assert.That(conflict.ErrorCode, Is.EqualTo("CONFLICT"));
    }

    [Test]
    public void ResponseSerializationPreservesClientContractCasing()
    {
        var response = new RelationshipResult
        {
            Success = true,
            Changed = true,
            Relationship = new RelationshipRecord { Id = "record", Status = "active" },
            ErrorCode = null
        };

        string json = JsonConvert.SerializeObject(response);
        var roundTrip = JsonConvert.DeserializeObject<RelationshipResult>(json);

        Assert.That(json, Does.Contain("\"success\":true"));
        Assert.That(json, Does.Contain("\"changed\":true"));
        Assert.That(json, Does.Contain("\"relationship\""));
        Assert.That(json, Does.Contain("\"id\":\"record\""));
        Assert.That(roundTrip!.Success, Is.True);
        Assert.That(roundTrip.Changed, Is.True);
        Assert.That(roundTrip.Relationship!.Id, Is.EqualTo("record"));

        var failureJson = JsonConvert.SerializeObject(new RelationshipResult { ErrorCode = "CONFLICT" });
        Assert.That(failureJson, Does.Contain("\"errorCode\":\"CONFLICT\""));
        Assert.That(failureJson, Does.Not.Contain("failureCategory"));
        Assert.That(failureJson, Does.Not.Contain("failureCode"));
    }

    [Test]
    public void NoReportEndpointOrPrivateReportDataExists()
    {
        Assert.That(typeof(SocialSafetyModule).GetMethod("SubmitReport"), Is.Null);
        Assert.That(typeof(SocialSafetyState).GetProperty("Report"), Is.Null);
    }

    private static FakeStore ActiveStore(bool concurrentFirstLoads = false, int forceConflictCount = 0, string relationshipKind = "block")
    {
        var relationship = new RelationshipRecord
        {
            Id = CanonicalId(relationshipKind),
            ActorAccountId = "actor",
            TargetAccountId = "target",
            CreatedAtUtcMs = 900,
            MuteUntilUtcMs = relationshipKind == "mute" ? 1500 : 0,
            Status = "active"
        };
        return new FakeStore(relationship, relationshipKind, concurrentFirstLoads, forceConflictCount);
    }

    private static void AssertConcurrentResultsAreCanonical(RelationshipResult[] results, FakeStore store, string relationshipKind, long now)
    {
        string expectedId = CanonicalId(relationshipKind);
        Assert.That(results, Has.All.Property(nameof(RelationshipResult.Success)).EqualTo(true));
        Assert.That(results, Has.All.Property(nameof(RelationshipResult.Relationship)).Not.Null);
        foreach (var result in results)
        {
            Assert.That(result.Relationship!.Id, Is.EqualTo(expectedId));
            Assert.That(result.Relationship.ActorAccountId, Is.EqualTo("actor"));
            Assert.That(result.Relationship.TargetAccountId, Is.EqualTo("target"));
            Assert.That(result.Relationship.Status, Is.AnyOf("active", "inactive"));
            if (result.Relationship.Status == "inactive" || relationshipKind == "block")
            {
                Assert.That(result.Relationship.MuteUntilUtcMs, Is.EqualTo(0));
            }
            else
            {
                Assert.That(result.Relationship.MuteUntilUtcMs, Is.GreaterThan(now));
            }
        }

        Assert.That(store.PersistedCount, Is.EqualTo(1));
        Assert.That(store.SavedStatuses.Count, Is.EqualTo(2));
        foreach (var status in store.SavedStatuses)
        {
            Assert.That(status, Is.AnyOf("active", "inactive"));
        }
        Assert.That(store.Persisted.Relationship!.Status, Is.EqualTo(store.SavedStatuses[^1]));
        Assert.That(store.Persisted.Relationship.Id, Is.EqualTo(expectedId));
    }

    private static string CanonicalId(string relationshipKind)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            return BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes("actor|" + relationshipKind + "|target")))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }
    }

    private static int ReadConfiguredInt(Dictionary<string, object> settings, string key, int fallback, int maximum)
    {
        var method = typeof(RemoteConfigSocialSafetyRateLimitConfiguration).GetMethod("ReadPositiveInt", BindingFlags.Static | BindingFlags.NonPublic);
        return (int)method!.Invoke(null, new object[] { settings, key, fallback, maximum })!;
    }

    private static SocialSafetyOperations Create(FakeStore store = null, FakeClock clock = null, FakePolicy policy = null, ISocialSafetyRateLimitConfiguration config = null)
    {
        return new SocialSafetyOperations(store ?? new FakeStore(), clock ?? new FakeClock(1000), policy ?? new FakePolicy(500), config ?? new FixedRateLimitConfiguration(10, 100));
    }

    private static FakeExecutionContext Context() => new("actor");
    private static RelationshipRequest Request(string target) => new() { TargetAccountId = target };

    private sealed class FakeStore : ISocialSafetyStore
    {
        private readonly string _failure;
        private readonly string _relationshipFailure;
        private readonly Dictionary<string, SocialSafetyState> _states = new();
        private readonly object _storageGate = new();
        private readonly object _rateStorageGate = new();
        private SocialSafetyRateState? _rateState;
        private readonly bool _concurrentFirstLoads;
        private readonly bool _concurrentRelationshipLoads;
        private int _forcedConflictsRemaining;
        private int _forcedRateConflictsRemaining;
        private readonly TaskCompletionSource<bool> _firstLoadBarrier = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _firstSaveBarrier = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _firstRateLoadBarrier = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _firstRateSaveBarrier = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _loadCount;
        private int _saveAttemptCount;
        private int _rateLoadCount;
        private int _rateSaveAttemptCount;
        private int _version;
        private int _rateVersion;
        public int SaveCount { get; private set; }
        public int ConflictCount { get; private set; }
        public int RateConflictCount { get; private set; }
        public int RateSaveCount { get; private set; }
        public int LoadCount => _loadCount;
        public int SaveAttemptCount => _saveAttemptCount;
        public int RateLoadCount => _rateLoadCount;
        public int RateSaveAttemptCount => _rateSaveAttemptCount;
        public int PersistedCount { get { lock (_storageGate) return _states.Count; } }
        public List<string> SavedStatuses { get; } = new();
        public SocialSafetyState Persisted { get { lock (_storageGate) return _states.Values.First(); } }
        public SocialSafetyRateState? RatePersisted { get { lock (_rateStorageGate) return Clone(_rateState); } }
        public FakeStore(string failure = null, bool concurrentFirstLoads = false, string relationshipFailure = null, bool concurrentRelationshipLoads = true)
        {
            _failure = failure;
            _relationshipFailure = relationshipFailure;
            _concurrentFirstLoads = concurrentFirstLoads;
            _concurrentRelationshipLoads = concurrentRelationshipLoads;
        }
        public FakeStore(RelationshipRecord initialRelationship, string relationshipKind, bool concurrentFirstLoads, int forceConflictCount, bool concurrentRelationshipLoads = true)
        {
            _concurrentFirstLoads = concurrentFirstLoads;
            _concurrentRelationshipLoads = concurrentRelationshipLoads;
            _forcedRateConflictsRemaining = forceConflictCount;
            _states[relationshipKind + "target"] = new SocialSafetyState { Relationship = Clone(initialRelationship) };
            _version = 1;
        }
        public async Task<SocialSafetyState> LoadAsync(IExecutionContext context, Unity.Services.CloudCode.Apis.IGameApiClient apiClient, string kind, string target)
        {
            if (_failure != null) throw new SocialSafetyStorageException(_failure);
            int load = System.Threading.Interlocked.Increment(ref _loadCount);
            if (_concurrentFirstLoads && _concurrentRelationshipLoads && load <= 2)
            {
                if (load == 2) _firstLoadBarrier.TrySetResult(true);
                await _firstLoadBarrier.Task;
            }

            SocialSafetyState state;
            string? writeLock;
            lock (_storageGate)
            {
                _states.TryGetValue(kind + target, out state);
                writeLock = state == null ? null : _version.ToString();
            }

            var copy = Clone(state ?? new SocialSafetyState());
            copy.WriteLock = writeLock;
            return copy;
        }
        public Task SaveAsync(IExecutionContext context, Unity.Services.CloudCode.Apis.IGameApiClient apiClient, string kind, string target, SocialSafetyState state)
        {
            if (_relationshipFailure != null) throw new SocialSafetyStorageException(_relationshipFailure);
            if (_failure != null) throw new SocialSafetyStorageException(_failure);
            int saveAttempt = System.Threading.Interlocked.Increment(ref _saveAttemptCount);
            if (_concurrentFirstLoads && _concurrentRelationshipLoads && saveAttempt <= 2)
            {
                if (saveAttempt == 2) _firstSaveBarrier.TrySetResult(true);
                return SaveAfterBarrierAsync(context, apiClient, kind, target, state);
            }

            return SaveCoreAsync(kind, target, state);
        }

        public async Task<SocialSafetyRateState> LoadRateStateAsync(IExecutionContext context, Unity.Services.CloudCode.Apis.IGameApiClient apiClient)
        {
            if (_failure != null) throw new SocialSafetyStorageException(_failure);
            int load = System.Threading.Interlocked.Increment(ref _rateLoadCount);
            if (_concurrentFirstLoads && load <= 2)
            {
                if (load == 2) _firstRateLoadBarrier.TrySetResult(true);
                await _firstRateLoadBarrier.Task;
            }

            lock (_rateStorageGate)
            {
                var copy = Clone(_rateState) ?? new SocialSafetyRateState();
                copy.WriteLock = _rateState == null ? null : _rateVersion.ToString();
                return copy;
            }
        }

        public Task SaveRateStateAsync(IExecutionContext context, Unity.Services.CloudCode.Apis.IGameApiClient apiClient, SocialSafetyRateState state)
        {
            if (_failure != null) throw new SocialSafetyStorageException(_failure);
            int saveAttempt = System.Threading.Interlocked.Increment(ref _rateSaveAttemptCount);
            if (_concurrentFirstLoads && saveAttempt <= 2)
            {
                if (saveAttempt == 2) _firstRateSaveBarrier.TrySetResult(true);
                return SaveRateAfterBarrierAsync(state);
            }

            return SaveRateCoreAsync(state);
        }

        private async Task SaveRateAfterBarrierAsync(SocialSafetyRateState state)
        {
            await _firstRateSaveBarrier.Task;
            await SaveRateCoreAsync(state);
        }

        private Task SaveRateCoreAsync(SocialSafetyRateState state)
        {
            lock (_rateStorageGate)
            {
                if (_forcedRateConflictsRemaining > 0)
                {
                    _forcedRateConflictsRemaining--;
                    RateConflictCount++;
                    throw new SocialSafetyStorageException("CONFLICT", new InvalidOperationException("private rate storage detail"));
                }

                if (_rateState != null && state.WriteLock != _rateVersion.ToString())
                {
                    RateConflictCount++;
                    throw new SocialSafetyStorageException("CONFLICT", new InvalidOperationException("private rate storage detail"));
                }

                RateSaveCount++;
                _rateState = Clone(state);
                _rateVersion++;
            }
            return Task.CompletedTask;
        }

        private async Task SaveAfterBarrierAsync(IExecutionContext context, Unity.Services.CloudCode.Apis.IGameApiClient apiClient, string kind, string target, SocialSafetyState state)
        {
            await _firstSaveBarrier.Task;
            await SaveCoreAsync(kind, target, state);
        }

        private Task SaveCoreAsync(string kind, string target, SocialSafetyState state)
        {
            if (_failure != null) throw new SocialSafetyStorageException(_failure);
            lock (_storageGate)
            {
                if (_forcedConflictsRemaining > 0)
                {
                    _forcedConflictsRemaining--;
                    ConflictCount++;
                    throw new SocialSafetyStorageException("CONFLICT", new InvalidOperationException("private storage detail"));
                }

                string key = kind + target;
                if (_states.ContainsKey(key) && state.WriteLock != _version.ToString())
                {
                    ConflictCount++;
                    throw new SocialSafetyStorageException("CONFLICT", new InvalidOperationException("private storage detail"));
                }

                SaveCount++;
                _states[key] = Clone(state);
                SavedStatuses.Add(state.Relationship?.Status ?? string.Empty);
                _version++;
            }
            return Task.CompletedTask;
        }

        private static SocialSafetyState Clone(SocialSafetyState state)
        {
            return JsonConvert.DeserializeObject<SocialSafetyState>(JsonConvert.SerializeObject(state)) ?? new SocialSafetyState();
        }

        private static SocialSafetyRateState? Clone(SocialSafetyRateState? state)
        {
            return state == null ? null : JsonConvert.DeserializeObject<SocialSafetyRateState>(JsonConvert.SerializeObject(state));
        }

        private static RelationshipRecord Clone(RelationshipRecord record)
        {
            return JsonConvert.DeserializeObject<RelationshipRecord>(JsonConvert.SerializeObject(record))!;
        }
    }

    private sealed class FakeClock : ISocialSafetyClock
    {
        public FakeClock(long now) => Now = now;
        public long Now { get; set; }
        public long UtcNowMs => Now;
    }

    private sealed class FakePolicy : ISocialSafetyPolicy
    {
        private readonly bool _limited;
        public FakePolicy(long duration, bool limited = false) { MuteDurationMs = duration; _limited = limited; }
        public long MuteDurationMs { get; }
        public bool IsRateLimited(string actorAccountId, string operation, long utcNowMs) => _limited;
    }

    private sealed class FixedRateLimitConfiguration : ISocialSafetyRateLimitConfiguration
    {
        private readonly SocialSafetyRateLimitConfiguration _configuration;
        public FixedRateLimitConfiguration(int perMinute, int perDay)
        {
            _configuration = new SocialSafetyRateLimitConfiguration(perMinute, perDay);
        }

        public Task<SocialSafetyRateLimitConfiguration> LoadAsync(IExecutionContext context, Unity.Services.CloudCode.Apis.IGameApiClient apiClient)
        {
            return Task.FromResult(_configuration);
        }
    }

    private sealed class FakeExecutionContext : IExecutionContext
    {
        public FakeExecutionContext(string playerId) => PlayerId = playerId;
        public string ProjectId => "project";
        public string PlayerId { get; }
        public string EnvironmentId => "environment";
        public string EnvironmentName => "production";
        public string AccessToken => "token";
        public string UserId => null;
        public string Issuer => null;
        public string ServiceToken => "service-token";
        public string AnalyticsUserId => null;
        public string UnityInstallationId => null;
        public string CorrelationId => null;
        public string ScopeId => null;
        public int CallDepth => 0;
        public ISession Session => null;
    }
}

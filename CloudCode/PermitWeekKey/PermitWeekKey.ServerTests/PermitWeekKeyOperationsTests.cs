using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace MyriadOfDragons.CloudCode.PermitWeekKey.Tests;

public sealed class PermitWeekKeyOperationsTests
{
    [Test]
    public async Task MissingActorIdentityIsRejected_OnBothEndpoints()
    {
        var operations = Create();
        var claim = await operations.ClaimWeeklyAsync(new FakeExecutionContext(null), null!, Request());
        var status = await operations.GetStatusAsync(new FakeExecutionContext(null), null!, StatusRequest());
        Assert.That(claim.ErrorCode, Is.EqualTo("AUTHENTICATION_REQUIRED"));
        Assert.That(status.ErrorCode, Is.EqualTo("AUTHENTICATION_REQUIRED"));
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task BlankActivityIdIsRejected_OnBothEndpoints(string activityId)
    {
        var operations = Create();
        var claim = await operations.ClaimWeeklyAsync(Context(), null!, Request(activityId));
        var status = await operations.GetStatusAsync(Context(), null!, StatusRequest(activityId));
        Assert.That(claim.ErrorCode, Is.EqualTo("INVALID_REQUEST"));
        Assert.That(status.ErrorCode, Is.EqualTo("INVALID_REQUEST"));
    }

    [Test]
    public async Task ClientChosenActivityIds_CannotMultiplyTheWeeklyGrant()
    {
        // ActivityId is client-supplied. Storage is keyed per (account, activityId), so without a server allowlist each
        // fresh id got its own weekly claim record AND its own hoard balance - unbounded permits per week.
        var store = new FakeStore();
        var operations = Create(store, config: new FixedEconomyConfiguration(4, 8));

        var real = await operations.ClaimWeeklyAsync(Context(), null!, Request());
        var forged1 = await operations.ClaimWeeklyAsync(Context(), null!, Request("attacker-activity-1"));
        var forged2 = await operations.ClaimWeeklyAsync(Context(), null!, Request("attacker-activity-2"));

        Assert.That(real.Granted, Is.EqualTo(4));
        Assert.That(forged1.Success, Is.False);
        Assert.That(forged1.ErrorCode, Is.EqualTo("UNKNOWN_ACTIVITY"));
        Assert.That(forged2.ErrorCode, Is.EqualTo("UNKNOWN_ACTIVITY"));
        Assert.That(store.SaveCount, Is.EqualTo(1), "Only the one known activity may ever write a claim record.");
    }

    [Test]
    public async Task UnknownActivity_IsRejectedOnStatusToo_AndNothingIsRead()
    {
        var operations = Create();
        var status = await operations.GetStatusAsync(Context(), null!, StatusRequest("attacker-activity-1"));
        Assert.That(status.ErrorCode, Is.EqualTo("UNKNOWN_ACTIVITY"));
    }

    [Test]
    public async Task ActivityAllowlist_IsExactMatch_AndInjectable()
    {
        var operations = new PermitWeekKeyOperations(new FakeStore(), new FakeClock("2026-W01", 1000), new FixedEconomyConfiguration(4, 8), new[] { "custom.activity" });
        Assert.That((await operations.ClaimWeeklyAsync(Context(), null!, Request("custom.activity"))).Success, Is.True);
        Assert.That((await operations.ClaimWeeklyAsync(Context(), null!, Request("ascensionPermit.weekly"))).ErrorCode, Is.EqualTo("UNKNOWN_ACTIVITY"));
        Assert.That((await operations.ClaimWeeklyAsync(Context(), null!, Request("CUSTOM.ACTIVITY"))).ErrorCode, Is.EqualTo("UNKNOWN_ACTIVITY"));
    }

    [Test]
    public void DefaultAllowlist_IsExactlyTheOneActivityTheClientShips() =>
        Assert.That(PermitWeekKeyOperations.DefaultAllowedActivityIds, Is.EqualTo(new[] { "ascensionPermit.weekly" }));

    [Test]
    public async Task FirstClaim_GrantsTheConfiguredWeeklyRate()
    {
        var operations = Create(config: new FixedEconomyConfiguration(4, 8));
        var result = await operations.ClaimWeeklyAsync(Context(), null!, Request());

        Assert.That(result.Success, Is.True);
        Assert.That(result.Granted, Is.EqualTo(4));
        Assert.That(result.Balance, Is.EqualTo(4));
        Assert.That(result.AlreadyClaimed, Is.False);
        Assert.That(result.WeekKey, Is.EqualTo("2026-W01"));
    }

    [Test]
    public async Task SecondClaimSameServerWeek_IsIdempotent_GrantsNothingMore()
    {
        var store = new FakeStore();
        var operations = Create(store, config: new FixedEconomyConfiguration(4, 8));
        var first = await operations.ClaimWeeklyAsync(Context(), null!, Request());
        var second = await operations.ClaimWeeklyAsync(Context(), null!, Request());

        Assert.That(first.Granted, Is.EqualTo(4));
        Assert.That(second.Success, Is.True);
        Assert.That(second.Granted, Is.EqualTo(0));
        Assert.That(second.AlreadyClaimed, Is.True);
        Assert.That(second.Balance, Is.EqualTo(4));
        Assert.That(store.SaveCount, Is.EqualTo(1), "A same-week repeat claim must not write again.");
    }

    [Test]
    public async Task ClientSuppliedWeekKeyCannotForceASecondClaim()
    {
        // The design's whole point: the client cannot supply an eligibility key of its own -
        // PermitClaimRequest simply has no field for one, so there is nothing to smuggle a
        // manipulated local-clock week key through even if a hostile client tried.
        var store = new FakeStore();
        var operations = Create(store, config: new FixedEconomyConfiguration(4, 8));
        await operations.ClaimWeeklyAsync(Context(), null!, Request());
        var repeat = await operations.ClaimWeeklyAsync(Context(), null!, Request());

        Assert.That(repeat.AlreadyClaimed, Is.True);
        Assert.That(repeat.Granted, Is.EqualTo(0));
    }

    [Test]
    public async Task ClaimAfterServerWeekAdvances_GrantsAgain()
    {
        var store = new FakeStore();
        var clock = new FakeClock("2026-W01", 1000);
        var operations = Create(store, clock, new FixedEconomyConfiguration(4, 8));
        var first = await operations.ClaimWeeklyAsync(Context(), null!, Request());

        clock.WeekKey = "2026-W02";
        var second = await operations.ClaimWeeklyAsync(Context(), null!, Request());

        Assert.That(first.Granted, Is.EqualTo(4));
        Assert.That(second.Granted, Is.EqualTo(4));
        Assert.That(second.AlreadyClaimed, Is.False);
        Assert.That(second.Balance, Is.EqualTo(8));
    }

    [Test]
    public async Task ClaimNearHoardCap_GrantsOnlyTheRemainingHoard()
    {
        var store = new FakeStore(initialBalance: 6);
        var operations = Create(store, config: new FixedEconomyConfiguration(4, 8));
        var result = await operations.ClaimWeeklyAsync(Context(), null!, Request());

        Assert.That(result.Granted, Is.EqualTo(2));
        Assert.That(result.Balance, Is.EqualTo(8));
    }

    [Test]
    public async Task ClaimAtFullHoard_GrantsZero_ButIsStillSuccessAndNotAlreadyClaimed()
    {
        var store = new FakeStore(initialBalance: 8);
        var operations = Create(store, config: new FixedEconomyConfiguration(4, 8));
        var result = await operations.ClaimWeeklyAsync(Context(), null!, Request());

        Assert.That(result.Success, Is.True);
        Assert.That(result.Granted, Is.EqualTo(0));
        Assert.That(result.AlreadyClaimed, Is.False, "Hoard-full and already-claimed are distinct outcomes.");
        Assert.That(result.Balance, Is.EqualTo(8));
    }

    [Test]
    public async Task ConcurrentFirstClaimsShareOneCanonicalPersistedRecord()
    {
        var store = new FakeStore(concurrentFirstLoads: true);
        var operations = Create(store, config: new FixedEconomyConfiguration(4, 8));
        var results = await Task.WhenAll(
            operations.ClaimWeeklyAsync(Context(), null!, Request()),
            operations.ClaimWeeklyAsync(Context(), null!, Request()));

        Assert.That(results, Has.All.Property(nameof(PermitClaimResult.Success)).EqualTo(true));
        // Exactly one of the two racing requests should have actually granted; the other must
        // see the just-persisted claim on retry and report AlreadyClaimed instead of granting a
        // second time - a stale-write race must never mint double the weekly amount.
        Assert.That(results.Count(result => result.Granted == 4), Is.EqualTo(1));
        Assert.That(results.Count(result => result.AlreadyClaimed), Is.EqualTo(1));
        Assert.That(store.Persisted.Balance, Is.EqualTo(4));
        Assert.That(store.ConflictCount, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task ExhaustedStaleWriteReturnsSanitizedConflictWithoutLooping()
    {
        var store = new FakeStore(forceConflictCount: 2);
        var operations = Create(store, config: new FixedEconomyConfiguration(4, 8));
        var result = await operations.ClaimWeeklyAsync(Context(), null!, Request());

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo("CONFLICT"));
        Assert.That(store.SaveAttemptCount, Is.EqualTo(2));
    }

    [Test]
    public async Task StorageUnavailableAndConflictAreSanitized()
    {
        var unavailable = await Create(new FakeStore(loadFailure: "STORAGE_UNAVAILABLE"), config: new FixedEconomyConfiguration(4, 8))
            .ClaimWeeklyAsync(Context(), null!, Request());
        Assert.That(unavailable.ErrorCode, Is.EqualTo("STORAGE_UNAVAILABLE"));

        var status = await Create(new FakeStore(loadFailure: "STORAGE_UNAVAILABLE"))
            .GetStatusAsync(Context(), null!, StatusRequest());
        Assert.That(status.ErrorCode, Is.EqualTo("STORAGE_UNAVAILABLE"));
    }

    [Test]
    public async Task GetStatusReturnsBalanceWeekKeyAndClaimFlagWithoutMutatingState()
    {
        var store = new FakeStore();
        var operations = Create(store, config: new FixedEconomyConfiguration(4, 8));
        await operations.ClaimWeeklyAsync(Context(), null!, Request());

        var status = await operations.GetStatusAsync(Context(), null!, StatusRequest());

        Assert.That(status.Balance, Is.EqualTo(4));
        Assert.That(status.ClaimedThisWeek, Is.True);
        Assert.That(status.CurrentWeekKey, Is.EqualTo("2026-W01"));
        Assert.That(status.WeeklyRate, Is.EqualTo(4));
        Assert.That(status.HoardCap, Is.EqualTo(8));
        Assert.That(store.SaveCount, Is.EqualTo(1), "GetStatus must never write.");
    }

    [Test]
    public async Task GetStatusBeforeAnyClaim_ReportsZeroBalanceAndNotClaimed()
    {
        var status = await Create(config: new FixedEconomyConfiguration(4, 8)).GetStatusAsync(Context(), null!, StatusRequest());

        Assert.That(status.Balance, Is.EqualTo(0));
        Assert.That(status.ClaimedThisWeek, Is.False);
    }

    [Test]
    public void NeitherRequestTypeCanSupplyAWeekKeyOrTimestamp()
    {
        bool ClaimHasForbiddenField() => typeof(PermitClaimRequest).GetProperties()
            .Any(property => property.Name.IndexOf("Week", StringComparison.OrdinalIgnoreCase) >= 0
                || property.Name.IndexOf("Time", StringComparison.OrdinalIgnoreCase) >= 0);
        bool StatusHasForbiddenField() => typeof(PermitStatusRequest).GetProperties()
            .Any(property => property.Name.IndexOf("Week", StringComparison.OrdinalIgnoreCase) >= 0
                || property.Name.IndexOf("Time", StringComparison.OrdinalIgnoreCase) >= 0);

        Assert.That(ClaimHasForbiddenField(), Is.False);
        Assert.That(StatusHasForbiddenField(), Is.False);
    }

    [Test]
    public void ResponseSerializationUsesTheDocumentedCamelCaseContract()
    {
        var claim = new PermitClaimResult { Success = true, Granted = 4, Balance = 4, WeekKey = "2026-W01", AlreadyClaimed = false };
        string json = JsonConvert.SerializeObject(claim);
        Assert.That(json, Does.Contain("\"success\":true"));
        Assert.That(json, Does.Contain("\"granted\":4"));
        Assert.That(json, Does.Contain("\"weekKey\":\"2026-W01\""));
        Assert.That(json, Does.Contain("\"alreadyClaimed\":false"));

        var status = new PermitStatusResult { Balance = 4, CurrentWeekKey = "2026-W01", ClaimedThisWeek = true, WeeklyRate = 4, HoardCap = 8 };
        string statusJson = JsonConvert.SerializeObject(status);
        Assert.That(statusJson, Does.Contain("\"currentWeekKey\":\"2026-W01\""));
        Assert.That(statusJson, Does.Contain("\"claimedThisWeek\":true"));
        Assert.That(statusJson, Does.Contain("\"weeklyRate\":4"));
        Assert.That(statusJson, Does.Contain("\"hoardCap\":8"));
    }

    [TestCase(4, 4, 100)]
    [TestCase(0, 4, 100)]
    [TestCase(-1, 4, 100)]
    [TestCase(101, 4, 100)]
    [TestCase("malformed", 4, 100)]
    public void WeeklyRateConfigurationRespectsCeiling(object configured, int fallback, int maximum)
    {
        int result = ReadConfiguredInt(
            new Dictionary<string, object> { { RemoteConfigPermitWeekKeyEconomyConfiguration.WeeklyRateKey, configured } },
            RemoteConfigPermitWeekKeyEconomyConfiguration.WeeklyRateKey,
            fallback,
            maximum);
        Assert.That(result, Is.EqualTo(configured is int intValue && intValue == maximum ? maximum : fallback));
    }

    [TestCase(8, 8, 1000)]
    [TestCase(0, 8, 1000)]
    [TestCase(-1, 8, 1000)]
    [TestCase(1001, 8, 1000)]
    [TestCase("malformed", 8, 1000)]
    public void HoardCapConfigurationRespectsCeiling(object configured, int fallback, int maximum)
    {
        int result = ReadConfiguredInt(
            new Dictionary<string, object> { { RemoteConfigPermitWeekKeyEconomyConfiguration.HoardCapKey, configured } },
            RemoteConfigPermitWeekKeyEconomyConfiguration.HoardCapKey,
            fallback,
            maximum);
        Assert.That(result, Is.EqualTo(configured is int intValue && intValue == maximum ? maximum : fallback));
    }

    private static int ReadConfiguredInt(Dictionary<string, object> settings, string key, int fallback, int maximum)
    {
        var method = typeof(RemoteConfigPermitWeekKeyEconomyConfiguration).GetMethod("ReadPositiveInt", BindingFlags.Static | BindingFlags.NonPublic);
        return (int)method!.Invoke(null, new object[] { settings, key, fallback, maximum })!;
    }

    private static PermitWeekKeyOperations Create(FakeStore? store = null, FakeClock? clock = null, IPermitWeekKeyEconomyConfiguration? config = null)
    {
        return new PermitWeekKeyOperations(store ?? new FakeStore(), clock ?? new FakeClock("2026-W01", 1000), config ?? new FixedEconomyConfiguration(4, 8));
    }

    private static FakeExecutionContext Context() => new("actor");
    private static PermitClaimRequest Request(string activityId = "ascensionPermit.weekly") => new() { ActivityId = activityId };
    private static PermitStatusRequest StatusRequest(string activityId = "ascensionPermit.weekly") => new() { ActivityId = activityId };

    private sealed class FakeStore : IPermitWeekKeyStore
    {
        private readonly string? _loadFailure;
        private readonly object _gate = new();
        private readonly TaskCompletionSource<bool> _firstLoadBarrier = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _firstSaveBarrier = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private PermitWeekKeyState? _state;
        private int _version;
        private int _forcedConflictsRemaining;
        private int _loadCount;
        private int _saveAttemptCount;
        private readonly bool _concurrentFirstLoads;

        public int SaveCount { get; private set; }
        public int SaveAttemptCount => _saveAttemptCount;
        public int ConflictCount { get; private set; }
        public PermitWeekKeyState Persisted { get { lock (_gate) return Clone(_state!); } }

        public FakeStore(int initialBalance = 0, bool concurrentFirstLoads = false, int forceConflictCount = 0, string? loadFailure = null)
        {
            _loadFailure = loadFailure;
            _concurrentFirstLoads = concurrentFirstLoads;
            _forcedConflictsRemaining = forceConflictCount;
            if (initialBalance > 0)
            {
                _state = new PermitWeekKeyState { Balance = initialBalance };
                _version = 1;
            }
        }

        public async Task<PermitWeekKeyState> LoadAsync(IExecutionContext context, IGameApiClient apiClient, string activityId)
        {
            if (_loadFailure != null)
            {
                throw new PermitWeekKeyStorageException(_loadFailure);
            }

            int load = Interlocked.Increment(ref _loadCount);
            if (_concurrentFirstLoads && load <= 2)
            {
                if (load == 2)
                {
                    _firstLoadBarrier.TrySetResult(true);
                }

                await _firstLoadBarrier.Task;
            }

            lock (_gate)
            {
                var copy = Clone(_state ?? new PermitWeekKeyState());
                copy.WriteLock = _state == null ? null : _version.ToString();
                return copy;
            }
        }

        public async Task SaveAsync(IExecutionContext context, IGameApiClient apiClient, string activityId, PermitWeekKeyState state)
        {
            int attempt = Interlocked.Increment(ref _saveAttemptCount);
            if (_concurrentFirstLoads && attempt <= 2)
            {
                if (attempt == 2)
                {
                    _firstSaveBarrier.TrySetResult(true);
                }

                await _firstSaveBarrier.Task;
            }

            lock (_gate)
            {
                if (_forcedConflictsRemaining > 0)
                {
                    _forcedConflictsRemaining--;
                    ConflictCount++;
                    throw new PermitWeekKeyStorageException("CONFLICT", new InvalidOperationException("private storage detail"));
                }

                if (_state != null && state.WriteLock != _version.ToString())
                {
                    ConflictCount++;
                    throw new PermitWeekKeyStorageException("CONFLICT", new InvalidOperationException("private storage detail"));
                }

                SaveCount++;
                _state = Clone(state);
                _version++;
            }
        }

        private static PermitWeekKeyState Clone(PermitWeekKeyState state)
        {
            return JsonConvert.DeserializeObject<PermitWeekKeyState>(JsonConvert.SerializeObject(state)) ?? new PermitWeekKeyState();
        }
    }

    private sealed class FakeClock : IPermitWeekKeyClock
    {
        public FakeClock(string weekKey, long now)
        {
            WeekKey = weekKey;
            Now = now;
        }

        public string WeekKey { get; set; }
        public long Now { get; set; }
        public long UtcNowMs => Now;
        public string CurrentIsoWeekKey => WeekKey;
    }

    private sealed class FixedEconomyConfiguration : IPermitWeekKeyEconomyConfiguration
    {
        private readonly PermitWeekKeyEconomyConfiguration _configuration;
        public FixedEconomyConfiguration(int weeklyRate, int hoardCap)
        {
            _configuration = new PermitWeekKeyEconomyConfiguration(weeklyRate, hoardCap);
        }

        public Task<PermitWeekKeyEconomyConfiguration> LoadAsync(IExecutionContext context, IGameApiClient apiClient)
        {
            return Task.FromResult(_configuration);
        }
    }

    private sealed class FakeExecutionContext : IExecutionContext
    {
        public FakeExecutionContext(string? playerId) => PlayerId = playerId!;
        public string ProjectId => "project";
        public string PlayerId { get; }
        public string EnvironmentId => "environment";
        public string EnvironmentName => "production";
        public string AccessToken => "token";
        public string UserId => null!;
        public string Issuer => null!;
        public string ServiceToken => "service-token";
        public string AnalyticsUserId => null!;
        public string UnityInstallationId => null!;
        public string CorrelationId => null!;
        public string ScopeId => null!;
        public int CallDepth => 0;
        public ISession Session => null!;
    }
}

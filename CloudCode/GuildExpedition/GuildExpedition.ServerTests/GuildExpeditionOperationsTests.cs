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

namespace MyriadOfDragons.CloudCode.GuildExpedition.Tests;

public sealed class GuildExpeditionOperationsTests
{
    // ---------------- Attempts ----------------

    [Test]
    public async Task ConsumeAttempt_MissingActorIdentityIsRejected()
    {
        var result = await Create().ConsumeAttemptAsync(new FakeExecutionContext(null), null!);
        Assert.That(result.ErrorCode, Is.EqualTo("AUTHENTICATION_REQUIRED"));
    }

    [Test]
    public async Task FreshAccount_GetsExactlyOneDaysAttempts_NotTheFullBank()
    {
        // "3/day, max bank 6" means a brand-new account starts with one day's worth (3), not the
        // full bank - the bank only reaches 6 by saving up unused attempts across multiple days
        // (see SkippingMultipleDays_StillCapsAtMaxBanked_NoOverflow).
        var operations = Create(rules: new FixedRules(attemptsPerDay: 3, maxBanked: 6, weeklyCap: 1000));
        for (int i = 0; i < 3; i++)
        {
            var result = await operations.ConsumeAttemptAsync(Context(), null!);
            Assert.That(result.Success, Is.True, $"attempt {i}");
        }

        var fourth = await operations.ConsumeAttemptAsync(Context(), null!);
        Assert.That(fourth.Success, Is.False);
        Assert.That(fourth.ErrorCode, Is.EqualTo("NO_ATTEMPTS_REMAINING"));
    }

    [Test]
    public async Task NewUtcDay_RefillsByAttemptsPerDay()
    {
        var clock = new FakeClock("2026-08-24", "2026-W35", 1000);
        var store = new FakeStore();
        var operations = Create(store, clock, new FixedRules(attemptsPerDay: 3, maxBanked: 6, weeklyCap: 1000));

        for (int i = 0; i < 3; i++)
        {
            await operations.ConsumeAttemptAsync(Context(), null!);
        }

        Assert.That((await operations.ConsumeAttemptAsync(Context(), null!)).ErrorCode, Is.EqualTo("NO_ATTEMPTS_REMAINING"));

        clock.DayKey = "2026-08-25";
        var afterOneDay = await operations.ConsumeAttemptAsync(Context(), null!);
        Assert.That(afterOneDay.Success, Is.True);
        Assert.That(afterOneDay.Remaining, Is.EqualTo(2), "3 granted, 1 consumed by this call = 2 remaining.");
    }

    [Test]
    public async Task SkippingMultipleDays_StillCapsAtMaxBanked_NoOverflow()
    {
        var clock = new FakeClock("2026-08-24", "2026-W35", 1000);
        var operations = Create(clock: clock, rules: new FixedRules(attemptsPerDay: 3, maxBanked: 6, weeklyCap: 1000));
        await operations.ConsumeAttemptAsync(Context(), null!); // 3 -> 2 remaining

        clock.DayKey = "2026-08-30"; // several days later, still just one refill event
        var result = await operations.ConsumeAttemptAsync(Context(), null!);
        // min(6, 2 + 3) - 1 = 4
        Assert.That(result.Remaining, Is.EqualTo(4));
    }

    [Test]
    public async Task ConcurrentAttemptConsumesNeverGoNegative()
    {
        var store = new FakeStore(concurrentFirstLoads: true);
        var operations = Create(store, rules: new FixedRules(attemptsPerDay: 1, maxBanked: 1, weeklyCap: 1000));
        var results = await Task.WhenAll(
            operations.ConsumeAttemptAsync(Context(), null!),
            operations.ConsumeAttemptAsync(Context(), null!));

        Assert.That(results.Count(r => r.Success), Is.EqualTo(1), "Only one of two racing consumes may succeed against a single-attempt bank.");
        Assert.That(results.Count(r => r.ErrorCode == "NO_ATTEMPTS_REMAINING"), Is.EqualTo(1));
        Assert.That(store.AttemptPersisted!.Available, Is.EqualTo(0));
    }

    // ---------------- Objective scoring ----------------

    [Test]
    public async Task SubmitObjective_UnknownObjectiveIdIsRejected()
    {
        var result = await Create().SubmitObjectiveResultAsync(Context(), null!, new ObjectiveResultRequest { ObjectiveId = "not.a.real.objective" });
        Assert.That(result.ErrorCode, Is.EqualTo("UNKNOWN_OBJECTIVE"));
    }

    [Test]
    public async Task SubmitObjective_MissingActorOrBlankIdIsRejected()
    {
        var operations = Create();
        var noActor = await operations.SubmitObjectiveResultAsync(new FakeExecutionContext(null), null!, KnownRequest());
        var blank = await operations.SubmitObjectiveResultAsync(Context(), null!, new ObjectiveResultRequest { ObjectiveId = "  " });
        Assert.That(noActor.ErrorCode, Is.EqualTo("AUTHENTICATION_REQUIRED"));
        Assert.That(blank.ErrorCode, Is.EqualTo("INVALID_REQUEST"));
    }

    [Test]
    public async Task FirstSubmission_AwardsTheManifestPointValue()
    {
        var result = await Create().SubmitObjectiveResultAsync(Context(), null!, new ObjectiveResultRequest { ObjectiveId = "scout.revealEnemyDeck" });
        Assert.That(result.Success, Is.True);
        Assert.That(result.PointsAwarded, Is.EqualTo(40));
        Assert.That(result.TotalPoints, Is.EqualTo(40));
        Assert.That(result.AlreadyScored, Is.False);
    }

    [Test]
    public async Task SameObjectiveTwiceInOneWeek_IsIdempotent_AwardsNothingMore()
    {
        var store = new FakeStore();
        var operations = Create(store);
        var first = await operations.SubmitObjectiveResultAsync(Context(), null!, KnownRequest());
        var second = await operations.SubmitObjectiveResultAsync(Context(), null!, KnownRequest());

        Assert.That(first.PointsAwarded, Is.GreaterThan(0));
        Assert.That(second.Success, Is.True);
        Assert.That(second.PointsAwarded, Is.EqualTo(0));
        Assert.That(second.AlreadyScored, Is.True);
        Assert.That(second.TotalPoints, Is.EqualTo(first.TotalPoints));
        Assert.That(store.ContributionSaveCount, Is.EqualTo(1), "A same-week repeat submission must not write again.");
    }

    [Test]
    public async Task DifferentObjectivesInOneWeek_EachScoreIndependently()
    {
        var operations = Create();
        var first = await operations.SubmitObjectiveResultAsync(Context(), null!, new ObjectiveResultRequest { ObjectiveId = "scout.revealEnemyDeck" }); // 40
        var second = await operations.SubmitObjectiveResultAsync(Context(), null!, new ObjectiveResultRequest { ObjectiveId = "supply.cooperativeDelivery" }); // 50

        Assert.That(first.PointsAwarded, Is.EqualTo(40));
        Assert.That(second.PointsAwarded, Is.EqualTo(50));
        Assert.That(second.TotalPoints, Is.EqualTo(90));
    }

    [Test]
    public async Task PersonalWeeklyCap_ClampsTheFinalAward_NeverExceedsCap()
    {
        var operations = Create(rules: new FixedRules(attemptsPerDay: 3, maxBanked: 6, weeklyCap: 100));
        var first = await operations.SubmitObjectiveResultAsync(Context(), null!, new ObjectiveResultRequest { ObjectiveId = "assault.defeatObstacle" }); // manifest 100
        var second = await operations.SubmitObjectiveResultAsync(Context(), null!, new ObjectiveResultRequest { ObjectiveId = "scout.revealEnemyDeck" }); // manifest 40, but cap already hit

        Assert.That(first.TotalPoints, Is.EqualTo(100));
        Assert.That(second.PointsAwarded, Is.EqualTo(0), "Cap already reached - further useful submissions score zero, per §4.2.");
        Assert.That(second.AlreadyScored, Is.False, "Zero-because-capped is a distinct outcome from already-scored.");
        Assert.That(second.TotalPoints, Is.EqualTo(100));
    }

    [Test]
    public async Task PersonalWeeklyCap_PartiallyClampsAnAwardThatWouldOverflowIt()
    {
        var operations = Create(rules: new FixedRules(attemptsPerDay: 3, maxBanked: 6, weeklyCap: 70));
        var first = await operations.SubmitObjectiveResultAsync(Context(), null!, new ObjectiveResultRequest { ObjectiveId = "scout.revealEnemyDeck" }); // 40, cap 70 remaining 30 after
        var second = await operations.SubmitObjectiveResultAsync(Context(), null!, new ObjectiveResultRequest { ObjectiveId = "supply.cooperativeDelivery" }); // manifest 50, only 30 remains

        Assert.That(first.PointsAwarded, Is.EqualTo(40));
        Assert.That(second.PointsAwarded, Is.EqualTo(30));
        Assert.That(second.TotalPoints, Is.EqualTo(70));
    }

    [Test]
    public async Task NewExpeditionWeek_ResetsContributionAndAllowsRescoring()
    {
        var store = new FakeStore();
        var clock = new FakeClock("2026-08-24", "2026-W35", 1000);
        var operations = Create(store, clock);
        var first = await operations.SubmitObjectiveResultAsync(Context(), null!, KnownRequest());

        clock.WeekKey = "2026-W36";
        var second = await operations.SubmitObjectiveResultAsync(Context(), null!, KnownRequest());

        Assert.That(first.PointsAwarded, Is.GreaterThan(0));
        Assert.That(second.AlreadyScored, Is.False);
        Assert.That(second.PointsAwarded, Is.EqualTo(first.PointsAwarded));
        Assert.That(second.TotalPoints, Is.EqualTo(first.PointsAwarded), "New week's total starts fresh, not accumulated from last week.");
    }

    [Test]
    public async Task ConcurrentSubmissionsOfTheSameObjective_ScoreOnlyOnce()
    {
        var store = new FakeStore(concurrentFirstLoads: true);
        var operations = Create(store);
        var results = await Task.WhenAll(
            operations.SubmitObjectiveResultAsync(Context(), null!, KnownRequest()),
            operations.SubmitObjectiveResultAsync(Context(), null!, KnownRequest()));

        Assert.That(results, Has.All.Property(nameof(ObjectiveResult.Success)).EqualTo(true));
        Assert.That(results.Count(r => r.PointsAwarded > 0), Is.EqualTo(1));
        Assert.That(results.Count(r => r.AlreadyScored), Is.EqualTo(1));
        Assert.That(store.ContributionPersisted!.TotalPoints, Is.EqualTo(40));
    }

    // ---------------- Milestone claims ----------------

    [Test]
    public async Task ClaimMilestone_UnrecognisedThresholdIsRejected()
    {
        var result = await Create().ClaimMilestoneAsync(Context(), null!, new MilestoneClaimRequest { Threshold = 333 });
        Assert.That(result.ErrorCode, Is.EqualTo("INVALID_REQUEST"));
    }

    [Test]
    public async Task ClaimMilestone_BelowThreshold_IsRejectedWithoutGranting()
    {
        var result = await Create().ClaimMilestoneAsync(Context(), null!, new MilestoneClaimRequest { Threshold = 100 });
        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo("THRESHOLD_NOT_REACHED"));
    }

    [TestCase(100, 25)]
    [TestCase(250, 35)]
    [TestCase(400, 40)]
    [TestCase(700, 45)]
    [TestCase(1000, 55)]
    public async Task ClaimMilestone_AtOrAboveThreshold_GrantsTheDocumentedGuildContribution(int threshold, int expectedGrant)
    {
        var store = new FakeStore();
        var operations = Create(store, rules: new FixedRules(3, 6, threshold));
        await ScoreUpToWeeklyCap(operations, threshold);

        var result = await operations.ClaimMilestoneAsync(Context(), null!, new MilestoneClaimRequest { Threshold = threshold });

        Assert.That(result.Success, Is.True);
        Assert.That(result.GuildContributionGranted, Is.EqualTo(expectedGrant));
        Assert.That(result.AlreadyClaimed, Is.False);
    }

    [Test]
    public async Task ClaimMilestone_SameBandTwice_IsIdempotent_GrantsNothingMore()
    {
        var store = new FakeStore();
        var operations = Create(store, rules: new FixedRules(3, 6, 100));
        await ScoreUpToWeeklyCap(operations, 100);
        var first = await operations.ClaimMilestoneAsync(Context(), null!, new MilestoneClaimRequest { Threshold = 100 });
        var second = await operations.ClaimMilestoneAsync(Context(), null!, new MilestoneClaimRequest { Threshold = 100 });

        Assert.That(first.GuildContributionGranted, Is.EqualTo(25));
        Assert.That(second.AlreadyClaimed, Is.True);
        Assert.That(second.GuildContributionGranted, Is.EqualTo(0));
    }

    [Test]
    public async Task HigherBand_IsClaimableWithoutFirstClaimingALowerBand()
    {
        // §7: "lower personal bands remain independently earnable" - the converse also holds,
        // a higher band reached directly is not gated behind claiming a lower one first.
        var operations = Create(rules: new FixedRules(3, 6, 400));
        await ScoreUpToWeeklyCap(operations, 400);

        var result = await operations.ClaimMilestoneAsync(Context(), null!, new MilestoneClaimRequest { Threshold = 400 });
        Assert.That(result.Success, Is.True);
        Assert.That(result.GuildContributionGranted, Is.EqualTo(40));
    }

    [Test]
    public async Task AllFiveBands_AreIndependentlyClaimableOnceEachIsReached()
    {
        var operations = Create(rules: new FixedRules(3, 6, 1000));
        await ScoreUpToWeeklyCap(operations, 1000);

        int totalGranted = 0;
        foreach (int threshold in new[] { 100, 250, 400, 700, 1000 })
        {
            var result = await operations.ClaimMilestoneAsync(Context(), null!, new MilestoneClaimRequest { Threshold = threshold });
            Assert.That(result.Success, Is.True, $"threshold {threshold}");
            totalGranted += result.GuildContributionGranted;
        }

        Assert.That(totalGranted, Is.EqualTo(25 + 35 + 40 + 45 + 55), "Matches §5.1's stated 200 Guild Contribution maximum per Expedition week.");
    }

    [Test]
    public async Task NewExpeditionWeek_ResetsClaimedMilestones()
    {
        var store = new FakeStore();
        var clock = new FakeClock("2026-08-24", "2026-W35", 1000);
        var operations = Create(store, clock, new FixedRules(3, 6, 100));
        await ScoreUpToWeeklyCap(operations, 100);
        await operations.ClaimMilestoneAsync(Context(), null!, new MilestoneClaimRequest { Threshold = 100 });

        clock.WeekKey = "2026-W36";
        var beforeRescoring = await operations.ClaimMilestoneAsync(Context(), null!, new MilestoneClaimRequest { Threshold = 100 });
        Assert.That(beforeRescoring.ErrorCode, Is.EqualTo("THRESHOLD_NOT_REACHED"), "New week's contribution reset to zero along with the claim record.");
    }

    [Test]
    public async Task ConcurrentClaimsOfTheSameBand_GrantOnlyOnce()
    {
        var store = new FakeStore(concurrentFirstLoads: true, initialPoints: 100);
        var operations = Create(store);
        var results = await Task.WhenAll(
            operations.ClaimMilestoneAsync(Context(), null!, new MilestoneClaimRequest { Threshold = 100 }),
            operations.ClaimMilestoneAsync(Context(), null!, new MilestoneClaimRequest { Threshold = 100 }));

        Assert.That(results, Has.All.Property(nameof(MilestoneClaimResult.Success)).EqualTo(true));
        Assert.That(results.Count(r => r.GuildContributionGranted > 0), Is.EqualTo(1));
        Assert.That(results.Count(r => r.AlreadyClaimed), Is.EqualTo(1));
    }

    // ---------------- Storage / contract / config ----------------

    [Test]
    public async Task StorageUnavailableAndConflictAreSanitized()
    {
        var unavailable = await Create(new FakeStore(loadFailure: "STORAGE_UNAVAILABLE")).ConsumeAttemptAsync(Context(), null!);
        Assert.That(unavailable.ErrorCode, Is.EqualTo("STORAGE_UNAVAILABLE"));

        var operations = Create(new FakeStore(forceContributionConflictCount: 2));
        var exhausted = await operations.SubmitObjectiveResultAsync(Context(), null!, KnownRequest());
        Assert.That(exhausted.ErrorCode, Is.EqualTo("CONFLICT"));
    }

    [Test]
    public void NeitherRequestTypeCanSupplyAWeekKeyPointValueOrTimestamp()
    {
        bool HasForbiddenField(Type type) => type.GetProperties()
            .Any(p => p.Name.IndexOf("Week", StringComparison.OrdinalIgnoreCase) >= 0
                || p.Name.IndexOf("Time", StringComparison.OrdinalIgnoreCase) >= 0
                || p.Name.IndexOf("Point", StringComparison.OrdinalIgnoreCase) >= 0);

        Assert.That(HasForbiddenField(typeof(ObjectiveResultRequest)), Is.False);
        Assert.That(HasForbiddenField(typeof(MilestoneClaimRequest)), Is.False);
    }

    [Test]
    public void ResponseSerializationUsesTheDocumentedCamelCaseContract()
    {
        var attempt = new AttemptResult { Success = true, Remaining = 4 };
        Assert.That(JsonConvert.SerializeObject(attempt), Does.Contain("\"remaining\":4"));

        var objective = new ObjectiveResult { Success = true, PointsAwarded = 40, TotalPoints = 40, WeekKey = "2026-W35", AlreadyScored = false };
        string objectiveJson = JsonConvert.SerializeObject(objective);
        Assert.That(objectiveJson, Does.Contain("\"pointsAwarded\":40"));
        Assert.That(objectiveJson, Does.Contain("\"weekKey\":\"2026-W35\""));
        Assert.That(objectiveJson, Does.Contain("\"alreadyScored\":false"));

        var claim = new MilestoneClaimResult { Success = true, Threshold = 100, GuildContributionGranted = 25 };
        Assert.That(JsonConvert.SerializeObject(claim), Does.Contain("\"guildContributionGranted\":25"));
    }

    [TestCase(3, 3, 20)]
    [TestCase(0, 3, 20)]
    [TestCase(-1, 3, 20)]
    [TestCase(21, 3, 20)]
    [TestCase("malformed", 3, 20)]
    public void AttemptsPerDayConfigurationRespectsCeiling(object configured, int fallback, int maximum)
    {
        int result = ReadConfiguredInt(
            new Dictionary<string, object> { { RemoteConfigGuildExpeditionRulesConfiguration.AttemptsPerDayKey, configured } },
            RemoteConfigGuildExpeditionRulesConfiguration.AttemptsPerDayKey,
            fallback,
            maximum);
        Assert.That(result, Is.EqualTo(configured is int intValue && intValue == maximum ? maximum : fallback));
    }

    /// <summary>Submits every manifest objective (total value 1020, comfortably above every
    /// milestone threshold up to 1000) against an operations instance already configured with
    /// weeklyCap == <paramref name="expectedTotal"/> - the cap's own clamping (already covered
    /// directly by PersonalWeeklyCap_ClampsTheFinalAward_NeverExceedsCap) lands the total exactly
    /// on the target, rather than this fixture needing its own exact-subset-sum logic.</summary>
    private static async Task ScoreUpToWeeklyCap(GuildExpeditionOperations operations, int expectedTotal)
    {
        string[] objectiveIds =
        {
            "assault.defeatObstacle", "raid.phaseThree", "raid.phaseTwo", "raid.phaseOne",
            "assault.clearFormation", "supply.protectFormation", "scout.defeatMarkedTarget",
            "supply.cooperativeDelivery", "builder.focusComplete", "scout.winWithInfoHandicap",
            "scout.revealEnemyDeck",
        };

        ObjectiveResult? last = null;
        foreach (string id in objectiveIds)
        {
            last = await operations.SubmitObjectiveResultAsync(Context(), null!, new ObjectiveResultRequest { ObjectiveId = id });
        }

        Assert.That(last!.TotalPoints, Is.EqualTo(expectedTotal), "Fixture assumes the manifest's total (1020) exceeds every threshold used in tests.");
    }

    private static int ReadConfiguredInt(Dictionary<string, object> settings, string key, int fallback, int maximum)
    {
        var method = typeof(RemoteConfigGuildExpeditionRulesConfiguration).GetMethod("ReadPositiveInt", BindingFlags.Static | BindingFlags.NonPublic);
        return (int)method!.Invoke(null, new object[] { settings, key, fallback, maximum })!;
    }

    private static GuildExpeditionOperations Create(FakeStore? store = null, FakeClock? clock = null, IGuildExpeditionRulesConfiguration? rules = null)
    {
        return new GuildExpeditionOperations(
            store ?? new FakeStore(),
            clock ?? new FakeClock("2026-08-24", "2026-W35", 1000),
            rules ?? new FixedRules(3, 6, 1000),
            new StaticExpeditionManifest());
    }

    private static FakeExecutionContext Context() => new("actor");
    private static ObjectiveResultRequest KnownRequest() => new() { ObjectiveId = "scout.revealEnemyDeck" };

    private sealed class FakeStore : IGuildExpeditionStore
    {
        private readonly string? _loadFailure;
        private readonly object _gate = new();
        private readonly TaskCompletionSource<bool> _firstAttemptLoadBarrier = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _firstAttemptSaveBarrier = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _firstContributionLoadBarrier = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _firstContributionSaveBarrier = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly bool _concurrentFirstLoads;
        private int _forceContributionConflictsRemaining;
        private AttemptState? _attemptState;
        private int _attemptVersion;
        private int _attemptLoadCount;
        private int _attemptSaveAttemptCount;
        private ContributionState? _contributionState;
        private int _contributionVersion;
        private int _contributionLoadCount;
        private int _contributionSaveAttemptCount;

        public int ContributionSaveCount { get; private set; }
        public AttemptState? AttemptPersisted { get { lock (_gate) return _attemptState == null ? null : Clone(_attemptState); } }
        public ContributionState? ContributionPersisted { get { lock (_gate) return _contributionState == null ? null : Clone(_contributionState); } }

        public FakeStore(bool concurrentFirstLoads = false, string? loadFailure = null, int forceContributionConflictCount = 0, int initialPoints = 0)
        {
            _concurrentFirstLoads = concurrentFirstLoads;
            _loadFailure = loadFailure;
            _forceContributionConflictsRemaining = forceContributionConflictCount;
            if (initialPoints > 0)
            {
                _contributionState = new ContributionState { WeekKey = "2026-W35", TotalPoints = initialPoints };
                _contributionVersion = 1;
            }
        }

        public async Task<AttemptState> LoadAttemptStateAsync(IExecutionContext context, IGameApiClient apiClient)
        {
            if (_loadFailure != null) throw new GuildExpeditionStorageException(_loadFailure);
            int load = Interlocked.Increment(ref _attemptLoadCount);
            if (_concurrentFirstLoads && load <= 2)
            {
                if (load == 2) _firstAttemptLoadBarrier.TrySetResult(true);
                await _firstAttemptLoadBarrier.Task;
            }

            lock (_gate)
            {
                var copy = Clone(_attemptState ?? new AttemptState());
                copy.WriteLock = _attemptState == null ? null : _attemptVersion.ToString();
                return copy;
            }
        }

        public async Task SaveAttemptStateAsync(IExecutionContext context, IGameApiClient apiClient, AttemptState state)
        {
            int attempt = Interlocked.Increment(ref _attemptSaveAttemptCount);
            if (_concurrentFirstLoads && attempt <= 2)
            {
                if (attempt == 2) _firstAttemptSaveBarrier.TrySetResult(true);
                await _firstAttemptSaveBarrier.Task;
            }

            lock (_gate)
            {
                if (_attemptState != null && state.WriteLock != _attemptVersion.ToString())
                {
                    throw new GuildExpeditionStorageException("CONFLICT", new InvalidOperationException("private storage detail"));
                }

                _attemptState = Clone(state);
                _attemptVersion++;
            }
        }

        public async Task<ContributionState> LoadContributionStateAsync(IExecutionContext context, IGameApiClient apiClient)
        {
            if (_loadFailure != null) throw new GuildExpeditionStorageException(_loadFailure);
            int load = Interlocked.Increment(ref _contributionLoadCount);
            if (_concurrentFirstLoads && load <= 2)
            {
                if (load == 2) _firstContributionLoadBarrier.TrySetResult(true);
                await _firstContributionLoadBarrier.Task;
            }

            lock (_gate)
            {
                var copy = Clone(_contributionState ?? new ContributionState());
                copy.WriteLock = _contributionState == null ? null : _contributionVersion.ToString();
                return copy;
            }
        }

        public async Task SaveContributionStateAsync(IExecutionContext context, IGameApiClient apiClient, ContributionState state)
        {
            int attempt = Interlocked.Increment(ref _contributionSaveAttemptCount);
            if (_concurrentFirstLoads && attempt <= 2)
            {
                if (attempt == 2) _firstContributionSaveBarrier.TrySetResult(true);
                await _firstContributionSaveBarrier.Task;
            }

            lock (_gate)
            {
                if (_forceContributionConflictsRemaining > 0)
                {
                    _forceContributionConflictsRemaining--;
                    throw new GuildExpeditionStorageException("CONFLICT", new InvalidOperationException("private storage detail"));
                }

                if (_contributionState != null && state.WriteLock != _contributionVersion.ToString())
                {
                    throw new GuildExpeditionStorageException("CONFLICT", new InvalidOperationException("private storage detail"));
                }

                ContributionSaveCount++;
                _contributionState = Clone(state);
                _contributionVersion++;
            }
        }

        private static AttemptState Clone(AttemptState state) =>
            JsonConvert.DeserializeObject<AttemptState>(JsonConvert.SerializeObject(state)) ?? new AttemptState();

        private static ContributionState Clone(ContributionState state) =>
            JsonConvert.DeserializeObject<ContributionState>(JsonConvert.SerializeObject(state)) ?? new ContributionState();
    }

    private sealed class FakeClock : IGuildExpeditionClock
    {
        public FakeClock(string dayKey, string weekKey, long now)
        {
            DayKey = dayKey;
            WeekKey = weekKey;
            Now = now;
        }

        public string DayKey { get; set; }
        public string WeekKey { get; set; }
        public long Now { get; set; }
        public long UtcNowMs => Now;
        public string CurrentDayKey => DayKey;
        public string CurrentWeekKey => WeekKey;
    }

    private sealed class FixedRules : IGuildExpeditionRulesConfiguration
    {
        private readonly GuildExpeditionRulesConfiguration _configuration;
        public FixedRules(int attemptsPerDay, int maxBanked, int weeklyCap)
        {
            _configuration = new GuildExpeditionRulesConfiguration(attemptsPerDay, maxBanked, weeklyCap);
        }

        public Task<GuildExpeditionRulesConfiguration> LoadAsync(IExecutionContext context, IGameApiClient apiClient)
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

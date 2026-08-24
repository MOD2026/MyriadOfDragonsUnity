using System;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace MyriadOfDragons.CloudCode.GuildExpedition;

/// <summary>
/// First-pass scaffold covering only the personal attempt/score/claim loop from
/// GUILD_EXPEDITION_COMPETITION_RECONCILIATION_2026-08-23.md §4.2 and §5.1. Explicitly does NOT
/// cover (see README "Deferred"): guild membership/eligibility snapshots, shared route/node
/// meters, raid phases, guild-level collective rewards (§5.2), league/season aggregation,
/// offices, breadth bonus, or the Ascension Permit portion of the milestone bands (that requires
/// a deliberate decision about how this module's issuance interacts with PermitWeekKey's shared
/// weekly ceiling - not something to improvise here).
/// </summary>
public sealed class GuildExpeditionOperations
{
    private const int MaxConflictReconciliations = 1;
    private static readonly MilestoneBand[] MilestoneBands =
    {
        new(100, 25),  // Participated
        new(250, 35),  // Contributor
        new(400, 40),  // Active
        new(700, 45),  // Vanguard
        new(1000, 55), // Expeditionary
    };

    private readonly IGuildExpeditionStore _store;
    private readonly IGuildExpeditionClock _clock;
    private readonly IGuildExpeditionRulesConfiguration _rules;
    private readonly IExpeditionManifest _manifest;

    public GuildExpeditionOperations(
        IGuildExpeditionStore store,
        IGuildExpeditionClock clock,
        IGuildExpeditionRulesConfiguration? rules = null,
        IExpeditionManifest? manifest = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _rules = rules ?? new RemoteConfigGuildExpeditionRulesConfiguration();
        _manifest = manifest ?? new StaticExpeditionManifest();
    }

    /// <summary>Consumes one attempt from the account's regenerating bank (refills to
    /// <see cref="GuildExpeditionRulesConfiguration.AttemptsPerDay"/> more, capped at
    /// <see cref="GuildExpeditionRulesConfiguration.MaxBankedAttempts"/>, the first time this is
    /// called on a new UTC day - never on a client-supplied day). No refill path exists on this
    /// endpoint or anywhere else in this module, matching §4.2's "no Gem, Gold, real-money, ad or
    /// item refill".</summary>
    public async Task<AttemptResult> ConsumeAttemptAsync(IExecutionContext context, IGameApiClient apiClient)
    {
        if (context == null || string.IsNullOrWhiteSpace(context.PlayerId))
        {
            return new AttemptResult { ErrorCode = "AUTHENTICATION_REQUIRED" };
        }

        var rules = await _rules.LoadAsync(context, apiClient);
        string today = _clock.CurrentDayKey;

        for (int attempt = 0; attempt <= MaxConflictReconciliations; attempt++)
        {
            try
            {
                var state = await _store.LoadAttemptStateAsync(context, apiClient);
                RefillIfNewDay(state, today, rules);

                if (state.Available <= 0)
                {
                    return new AttemptResult { Success = false, Remaining = 0, ErrorCode = "NO_ATTEMPTS_REMAINING" };
                }

                state.Available -= 1;
                await _store.SaveAttemptStateAsync(context, apiClient, state);
                return new AttemptResult { Success = true, Remaining = state.Available };
            }
            catch (GuildExpeditionStorageException exception) when (exception.ErrorCode == "CONFLICT" && attempt < MaxConflictReconciliations)
            {
            }
            catch (GuildExpeditionStorageException exception)
            {
                return new AttemptResult { ErrorCode = exception.ErrorCode };
            }
        }

        return new AttemptResult { ErrorCode = "CONFLICT" };
    }

    private static void RefillIfNewDay(AttemptState state, string today, GuildExpeditionRulesConfiguration rules)
    {
        if (state.LastRefillDayKey == today)
        {
            return;
        }

        state.LastRefillDayKey = today;
        state.Available = Math.Min(rules.MaxBankedAttempts, state.Available + rules.AttemptsPerDay);
    }

    /// <summary>Records one verified objective completion. Idempotent per objectiveId per
    /// Expedition week (§4.2 "best verified result... no score proportional to raw damage"): a
    /// second submission of the same objective in the same week returns AlreadyScored, granting
    /// nothing further. The week key and the objective's point value are both server-resolved -
    /// <see cref="ObjectiveResultRequest"/> supplies only an objectiveId, never a week or a
    /// point amount, so a client cannot mint its own score.</summary>
    public async Task<ObjectiveResult> SubmitObjectiveResultAsync(IExecutionContext context, IGameApiClient apiClient, ObjectiveResultRequest request)
    {
        if (context == null || string.IsNullOrWhiteSpace(context.PlayerId))
        {
            return new ObjectiveResult { ErrorCode = "AUTHENTICATION_REQUIRED" };
        }

        if (request == null || string.IsNullOrWhiteSpace(request.ObjectiveId))
        {
            return new ObjectiveResult { ErrorCode = "INVALID_REQUEST" };
        }

        if (!_manifest.TryGetObjectivePoints(request.ObjectiveId, out int points))
        {
            return new ObjectiveResult { ErrorCode = "UNKNOWN_OBJECTIVE" };
        }

        var rules = await _rules.LoadAsync(context, apiClient);
        string weekKey = _clock.CurrentWeekKey;

        for (int attempt = 0; attempt <= MaxConflictReconciliations; attempt++)
        {
            try
            {
                var state = await _store.LoadContributionStateAsync(context, apiClient);
                ResetIfNewWeek(state, weekKey);

                if (state.ScoredObjectiveIds.Contains(request.ObjectiveId))
                {
                    return new ObjectiveResult
                    {
                        Success = true,
                        PointsAwarded = 0,
                        TotalPoints = state.TotalPoints,
                        WeekKey = weekKey,
                        AlreadyScored = true
                    };
                }

                int capRemaining = rules.PersonalWeeklyCap - state.TotalPoints;
                int awarded = capRemaining > 0 ? Math.Min(points, capRemaining) : 0;

                state.TotalPoints += awarded;
                state.ScoredObjectiveIds.Add(request.ObjectiveId);
                await _store.SaveContributionStateAsync(context, apiClient, state);

                return new ObjectiveResult
                {
                    Success = true,
                    PointsAwarded = awarded,
                    TotalPoints = state.TotalPoints,
                    WeekKey = weekKey,
                    AlreadyScored = false
                };
            }
            catch (GuildExpeditionStorageException exception) when (exception.ErrorCode == "CONFLICT" && attempt < MaxConflictReconciliations)
            {
            }
            catch (GuildExpeditionStorageException exception)
            {
                return new ObjectiveResult { ErrorCode = exception.ErrorCode, WeekKey = weekKey };
            }
        }

        return new ObjectiveResult { ErrorCode = "CONFLICT", WeekKey = weekKey };
    }

    /// <summary>Claims one of the five personal milestone bands (§5.1) - Guild Contribution only;
    /// see the class comment for why the Ascension Permit portion is deliberately not modeled
    /// here. Idempotent per threshold per Expedition week; requires the account's current-week
    /// total to have actually reached the threshold, and each band is independently claimable
    /// once that band's own threshold is met (claiming 400 does not require having claimed 100
    /// first, matching "lower personal bands remain independently earnable" in §7).</summary>
    public async Task<MilestoneClaimResult> ClaimMilestoneAsync(IExecutionContext context, IGameApiClient apiClient, MilestoneClaimRequest request)
    {
        if (context == null || string.IsNullOrWhiteSpace(context.PlayerId))
        {
            return new MilestoneClaimResult { ErrorCode = "AUTHENTICATION_REQUIRED" };
        }

        MilestoneBand? band = FindBand(request?.Threshold ?? 0);
        if (band == null)
        {
            return new MilestoneClaimResult { ErrorCode = "INVALID_REQUEST" };
        }

        string weekKey = _clock.CurrentWeekKey;

        for (int attempt = 0; attempt <= MaxConflictReconciliations; attempt++)
        {
            try
            {
                var state = await _store.LoadContributionStateAsync(context, apiClient);
                ResetIfNewWeek(state, weekKey);

                if (state.ClaimedMilestoneThresholds.Contains(band.Threshold))
                {
                    return new MilestoneClaimResult
                    {
                        Success = true,
                        Threshold = band.Threshold,
                        GuildContributionGranted = 0,
                        AlreadyClaimed = true
                    };
                }

                if (state.TotalPoints < band.Threshold)
                {
                    return new MilestoneClaimResult { ErrorCode = "THRESHOLD_NOT_REACHED", Threshold = band.Threshold };
                }

                state.ClaimedMilestoneThresholds.Add(band.Threshold);
                await _store.SaveContributionStateAsync(context, apiClient, state);

                return new MilestoneClaimResult
                {
                    Success = true,
                    Threshold = band.Threshold,
                    GuildContributionGranted = band.GuildContribution,
                    AlreadyClaimed = false
                };
            }
            catch (GuildExpeditionStorageException exception) when (exception.ErrorCode == "CONFLICT" && attempt < MaxConflictReconciliations)
            {
            }
            catch (GuildExpeditionStorageException exception)
            {
                return new MilestoneClaimResult { ErrorCode = exception.ErrorCode, Threshold = band.Threshold };
            }
        }

        return new MilestoneClaimResult { ErrorCode = "CONFLICT", Threshold = band.Threshold };
    }

    private static void ResetIfNewWeek(ContributionState state, string weekKey)
    {
        if (state.WeekKey == weekKey)
        {
            return;
        }

        state.WeekKey = weekKey;
        state.TotalPoints = 0;
        state.ScoredObjectiveIds.Clear();
        state.ClaimedMilestoneThresholds.Clear();
    }

    private static MilestoneBand? FindBand(int threshold)
    {
        foreach (var band in MilestoneBands)
        {
            if (band.Threshold == threshold)
            {
                return band;
            }
        }

        return null;
    }
}

public sealed class GuildExpeditionModule
{
    private readonly GuildExpeditionOperations _operations;

    public GuildExpeditionModule()
        : this(new CloudSaveGuildExpeditionStore(), new SystemGuildExpeditionClock())
    {
    }

    internal GuildExpeditionModule(
        IGuildExpeditionStore store,
        IGuildExpeditionClock clock,
        IGuildExpeditionRulesConfiguration? rules = null,
        IExpeditionManifest? manifest = null)
    {
        _operations = new GuildExpeditionOperations(store, clock, rules, manifest);
    }

    [CloudCodeFunction("ConsumeExpeditionAttempt")]
    public Task<AttemptResult> ConsumeExpeditionAttempt(IExecutionContext context, IGameApiClient apiClient)
    {
        return _operations.ConsumeAttemptAsync(context, apiClient);
    }

    [CloudCodeFunction("SubmitExpeditionObjectiveResult")]
    public Task<ObjectiveResult> SubmitExpeditionObjectiveResult(IExecutionContext context, IGameApiClient apiClient, ObjectiveResultRequest request)
    {
        return _operations.SubmitObjectiveResultAsync(context, apiClient, request);
    }

    [CloudCodeFunction("ClaimExpeditionMilestone")]
    public Task<MilestoneClaimResult> ClaimExpeditionMilestone(IExecutionContext context, IGameApiClient apiClient, MilestoneClaimRequest request)
    {
        return _operations.ClaimMilestoneAsync(context, apiClient, request);
    }
}

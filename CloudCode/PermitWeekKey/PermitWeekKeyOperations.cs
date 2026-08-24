using System;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace MyriadOfDragons.CloudCode.PermitWeekKey;

public sealed class PermitWeekKeyOperations
{
    private const int MaxConflictReconciliations = 1;
    private readonly IPermitWeekKeyStore _store;
    private readonly IPermitWeekKeyClock _clock;
    private readonly IPermitWeekKeyEconomyConfiguration _configuration;

    public PermitWeekKeyOperations(IPermitWeekKeyStore store, IPermitWeekKeyClock clock, IPermitWeekKeyEconomyConfiguration? configuration = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _configuration = configuration ?? new RemoteConfigPermitWeekKeyEconomyConfiguration();
    }

    /// <summary>Read-only status: current balance, the server's own current ISO week key (never
    /// the client's), whether this activity has already been claimed this week, and the active
    /// weekly rate/hoard cap. Never mutates state.</summary>
    public async Task<PermitStatusResult> GetStatusAsync(IExecutionContext context, IGameApiClient apiClient, PermitStatusRequest request)
    {
        var validation = ValidateStatusRequest(context, request);
        if (validation != null)
        {
            return validation;
        }

        string weekKey = _clock.CurrentIsoWeekKey;
        var config = await _configuration.LoadAsync(context, apiClient);

        try
        {
            var state = await _store.LoadAsync(context, apiClient, request.ActivityId);
            return new PermitStatusResult
            {
                Balance = state.Balance,
                CurrentWeekKey = weekKey,
                ClaimedThisWeek = state.LastClaim?.WeekKey == weekKey,
                WeeklyRate = config.WeeklyRate,
                HoardCap = config.HoardCap
            };
        }
        catch (PermitWeekKeyStorageException exception)
        {
            return new PermitStatusResult { ErrorCode = exception.ErrorCode, CurrentWeekKey = weekKey };
        }
    }

    /// <summary>Claims this week's Permit allotment for one activityId. Idempotent: a second call
    /// in the same server-computed ISO week returns the prior grant's outcome (AlreadyClaimed =
    /// true, Granted = 0) rather than granting again, no matter what the client's local clock
    /// says - the week key compared here is always <see cref="IPermitWeekKeyClock.CurrentIsoWeekKey"/>,
    /// never anything the caller supplies.</summary>
    public async Task<PermitClaimResult> ClaimWeeklyAsync(IExecutionContext context, IGameApiClient apiClient, PermitClaimRequest request)
    {
        var validation = ValidateClaimRequest(context, request);
        if (validation != null)
        {
            return validation;
        }

        string weekKey = _clock.CurrentIsoWeekKey;
        var config = await _configuration.LoadAsync(context, apiClient);

        for (int attempt = 0; attempt <= MaxConflictReconciliations; attempt++)
        {
            try
            {
                var state = await _store.LoadAsync(context, apiClient, request.ActivityId);

                if (state.LastClaim != null && state.LastClaim.WeekKey == weekKey)
                {
                    return new PermitClaimResult
                    {
                        Success = true,
                        Granted = 0,
                        Balance = state.Balance,
                        WeekKey = weekKey,
                        AlreadyClaimed = true
                    };
                }

                int hoardRemaining = config.HoardCap - state.Balance;
                int granted = hoardRemaining > 0 ? Math.Min(config.WeeklyRate, hoardRemaining) : 0;

                state.Balance += granted;
                state.LastClaim = new PermitClaimRecord
                {
                    ActivityId = request.ActivityId,
                    WeekKey = weekKey,
                    GrantedAmount = granted,
                    ClaimedAtUtcMs = _clock.UtcNowMs
                };
                await _store.SaveAsync(context, apiClient, request.ActivityId, state);

                return new PermitClaimResult
                {
                    Success = true,
                    Granted = granted,
                    Balance = state.Balance,
                    WeekKey = weekKey,
                    AlreadyClaimed = false
                };
            }
            catch (PermitWeekKeyStorageException exception) when (exception.ErrorCode == "CONFLICT" && attempt < MaxConflictReconciliations)
            {
                // Re-read authoritative state once, then apply the claim to that version - the
                // same stale-write reconciliation shape as SocialSafety's relationship writes.
            }
            catch (PermitWeekKeyStorageException exception)
            {
                return Failure(exception.ErrorCode, weekKey);
            }
        }

        return Failure("CONFLICT", weekKey);
    }

    private static PermitStatusResult? ValidateStatusRequest(IExecutionContext context, PermitStatusRequest request)
    {
        if (context == null || string.IsNullOrWhiteSpace(context.PlayerId))
        {
            return new PermitStatusResult { ErrorCode = "AUTHENTICATION_REQUIRED" };
        }

        if (request == null || string.IsNullOrWhiteSpace(request.ActivityId))
        {
            return new PermitStatusResult { ErrorCode = "INVALID_REQUEST" };
        }

        return null;
    }

    private static PermitClaimResult? ValidateClaimRequest(IExecutionContext context, PermitClaimRequest request)
    {
        if (context == null || string.IsNullOrWhiteSpace(context.PlayerId))
        {
            return Failure("AUTHENTICATION_REQUIRED", string.Empty);
        }

        if (request == null || string.IsNullOrWhiteSpace(request.ActivityId))
        {
            return Failure("INVALID_REQUEST", string.Empty);
        }

        return null;
    }

    private static PermitClaimResult Failure(string errorCode, string weekKey)
    {
        return new PermitClaimResult { Success = false, ErrorCode = errorCode, WeekKey = weekKey };
    }
}

public sealed class PermitWeekKeyModule
{
    private readonly PermitWeekKeyOperations _operations;

    public PermitWeekKeyModule()
        : this(new CloudSavePermitWeekKeyStore(), new SystemPermitWeekKeyClock(), new RemoteConfigPermitWeekKeyEconomyConfiguration())
    {
    }

    internal PermitWeekKeyModule(IPermitWeekKeyStore store, IPermitWeekKeyClock clock, IPermitWeekKeyEconomyConfiguration? configuration = null)
    {
        _operations = new PermitWeekKeyOperations(store, clock, configuration ?? new RemoteConfigPermitWeekKeyEconomyConfiguration());
    }

    [CloudCodeFunction("GetPermitStatus")]
    public Task<PermitStatusResult> GetPermitStatus(IExecutionContext context, IGameApiClient apiClient, PermitStatusRequest request)
    {
        return _operations.GetStatusAsync(context, apiClient, request);
    }

    [CloudCodeFunction("ClaimWeeklyPermit")]
    public Task<PermitClaimResult> ClaimWeeklyPermit(IExecutionContext context, IGameApiClient apiClient, PermitClaimRequest request)
    {
        return _operations.ClaimWeeklyAsync(context, apiClient, request);
    }
}

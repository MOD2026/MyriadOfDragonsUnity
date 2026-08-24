using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace MyriadOfDragons.CloudCode.SocialSafety;

public sealed class SocialSafetyOperations
{
    private const int MaxConflictReconciliations = 1;
    private readonly ISocialSafetyStore _store;
    private readonly ISocialSafetyClock _clock;
    private readonly ISocialSafetyPolicy _policy;
    private readonly SocialSafetyRateLimiter _rateLimiter;

    public SocialSafetyOperations(ISocialSafetyStore store, ISocialSafetyClock clock, ISocialSafetyPolicy? policy = null, ISocialSafetyRateLimitConfiguration? rateLimitConfiguration = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _policy = policy ?? new UnimplementedSocialSafetyPolicy();
        _rateLimiter = new SocialSafetyRateLimiter(store, rateLimitConfiguration ?? new RemoteConfigSocialSafetyRateLimitConfiguration(), clock);
    }

    public Task<RelationshipResult> BlockAccountAsync(IExecutionContext context, IGameApiClient apiClient, RelationshipRequest request)
    {
        return AddRelationshipAsync(context, apiClient, request, "block");
    }

    public Task<RelationshipResult> UnblockAccountAsync(IExecutionContext context, IGameApiClient apiClient, RelationshipRequest request)
    {
        return RemoveRelationshipAsync(context, apiClient, request, "block");
    }

    public Task<RelationshipResult> MuteAccountAsync(IExecutionContext context, IGameApiClient apiClient, RelationshipRequest request)
    {
        return AddRelationshipAsync(context, apiClient, request, "mute");
    }

    public Task<RelationshipResult> UnmuteAccountAsync(IExecutionContext context, IGameApiClient apiClient, RelationshipRequest request)
    {
        return RemoveRelationshipAsync(context, apiClient, request, "mute");
    }

    private async Task<RelationshipResult> AddRelationshipAsync(IExecutionContext context, IGameApiClient apiClient, RelationshipRequest request, string relationshipKind)
    {
        var validation = Validate(context, request);
        if (validation != null)
        {
            return validation;
        }

        if (_policy.IsRateLimited(context.PlayerId!, relationshipKind, _clock.UtcNowMs))
        {
            return Failure("RATE_LIMITED");
        }

        var reservation = await _rateLimiter.ReserveAsync(context, apiClient);
        if (!reservation.Allowed)
        {
            return Failure(reservation.ErrorCode!);
        }

        for (int attempt = 0; attempt <= MaxConflictReconciliations; attempt++)
        {
            try
            {
                var state = await _store.LoadAsync(context, apiClient, relationshipKind, request.TargetAccountId);
                var record = state.Relationship;
                bool wasActive = record != null && record.Status == "active" && (relationshipKind != "mute" || record.MuteUntilUtcMs > _clock.UtcNowMs);
                if (record == null)
                {
                    record = new RelationshipRecord
                    {
                        Id = CloudSaveSocialSafetyStore.BuildRecordId(context.PlayerId!, relationshipKind, request.TargetAccountId),
                        ActorAccountId = context.PlayerId!,
                        TargetAccountId = request.TargetAccountId,
                        CreatedAtUtcMs = _clock.UtcNowMs
                    };
                }

                record.Status = "active";
                record.MuteUntilUtcMs = relationshipKind == "mute" ? _clock.UtcNowMs + _policy.MuteDurationMs : 0;
                state.Relationship = record;
                await _store.SaveAsync(context, apiClient, relationshipKind, request.TargetAccountId, state);
                return new RelationshipResult { Success = true, Relationship = record, Changed = !wasActive };
            }
            catch (SocialSafetyStorageException exception) when (exception.ErrorCode == "CONFLICT" && attempt < MaxConflictReconciliations)
            {
                // Re-read authoritative state once, then apply the requested operation to that version.
            }
            catch (SocialSafetyStorageException exception)
            {
                return Failure(exception.ErrorCode);
            }
        }

        return Failure("CONFLICT");
    }

    private async Task<RelationshipResult> RemoveRelationshipAsync(IExecutionContext context, IGameApiClient apiClient, RelationshipRequest request, string relationshipKind)
    {
        var validation = Validate(context, request);
        if (validation != null)
        {
            return validation;
        }

        if (_policy.IsRateLimited(context.PlayerId!, relationshipKind, _clock.UtcNowMs))
        {
            return Failure("RATE_LIMITED");
        }

        var reservation = await _rateLimiter.ReserveAsync(context, apiClient);
        if (!reservation.Allowed)
        {
            return Failure(reservation.ErrorCode!);
        }

        for (int attempt = 0; attempt <= MaxConflictReconciliations; attempt++)
        {
            try
            {
                var state = await _store.LoadAsync(context, apiClient, relationshipKind, request.TargetAccountId);
                var record = state.Relationship;
                bool wasActive = record != null && record.Status == "active" && (relationshipKind != "mute" || record.MuteUntilUtcMs > _clock.UtcNowMs);
                if (record != null)
                {
                    record.Status = "inactive";
                    if (relationshipKind == "mute")
                    {
                        record.MuteUntilUtcMs = 0;
                    }
                    state.Relationship = record;
                    await _store.SaveAsync(context, apiClient, relationshipKind, request.TargetAccountId, state);
                }

                return new RelationshipResult { Success = true, Relationship = record, Changed = wasActive };
            }
            catch (SocialSafetyStorageException exception) when (exception.ErrorCode == "CONFLICT" && attempt < MaxConflictReconciliations)
            {
                // Re-read authoritative state once, then apply the requested operation to that version.
            }
            catch (SocialSafetyStorageException exception)
            {
                return Failure(exception.ErrorCode);
            }
        }

        return Failure("CONFLICT");
    }

    private static RelationshipResult? Validate(IExecutionContext context, RelationshipRequest request)
    {
        if (context == null || string.IsNullOrWhiteSpace(context.PlayerId))
        {
            return Failure("AUTHENTICATION_REQUIRED");
        }

        if (request == null || string.IsNullOrWhiteSpace(request.TargetAccountId))
        {
            return Failure("INVALID_REQUEST");
        }

        request.TargetAccountId = request.TargetAccountId.Trim();

        if (string.Equals(context.PlayerId, request.TargetAccountId, StringComparison.Ordinal))
        {
            return Failure("SELF_TARGET_NOT_ALLOWED");
        }

        return null;
    }

    private static RelationshipResult Failure(string errorCode)
    {
        return new RelationshipResult { Success = false, ErrorCode = errorCode, Changed = false };
    }
}

public sealed class RelationshipRequest
{
    public string TargetAccountId { get; set; } = string.Empty;
}

public sealed class SocialSafetyModule
{
    private readonly SocialSafetyOperations _operations;

    public SocialSafetyModule(Microsoft.Extensions.Logging.ILogger<SocialSafetyModule>? logger = null)
        : this(new CloudSaveSocialSafetyStore(logger), new SystemSocialSafetyClock(), new UnimplementedSocialSafetyPolicy(), new RemoteConfigSocialSafetyRateLimitConfiguration())
    {
    }

    internal SocialSafetyModule(ISocialSafetyStore store, ISocialSafetyClock clock, ISocialSafetyPolicy? policy = null, ISocialSafetyRateLimitConfiguration? rateLimitConfiguration = null)
    {
        _operations = new SocialSafetyOperations(store, clock, policy ?? new UnimplementedSocialSafetyPolicy(), rateLimitConfiguration ?? new RemoteConfigSocialSafetyRateLimitConfiguration());
    }

    [CloudCodeFunction("BlockAccount")]
    public Task<RelationshipResult> BlockAccount(IExecutionContext context, IGameApiClient apiClient, RelationshipRequest request)
    {
        return _operations.BlockAccountAsync(context, apiClient, request);
    }

    [CloudCodeFunction("UnblockAccount")]
    public Task<RelationshipResult> UnblockAccount(IExecutionContext context, IGameApiClient apiClient, RelationshipRequest request)
    {
        return _operations.UnblockAccountAsync(context, apiClient, request);
    }

    [CloudCodeFunction("MuteAccount")]
    public Task<RelationshipResult> MuteAccount(IExecutionContext context, IGameApiClient apiClient, RelationshipRequest request)
    {
        return _operations.MuteAccountAsync(context, apiClient, request);
    }

    [CloudCodeFunction("UnmuteAccount")]
    public Task<RelationshipResult> UnmuteAccount(IExecutionContext context, IGameApiClient apiClient, RelationshipRequest request)
    {
        return _operations.UnmuteAccountAsync(context, apiClient, request);
    }
}

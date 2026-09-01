using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace MyriadOfDragons.CloudCode.Friends;

/// <summary>Friends graph module (docs/LOCKED_DECISIONS_REGISTER.md "Beta social screens: OPTION
/// B LOCKED 2026-08-25"): add/accept/decline/list/remove plus a once-per-UTC-day gift per
/// friendship. Same module patterns as the 4 live-verified modules (single public constructor,
/// ICloudCodeSetup/AddGameApiClient, RFC-compliant Cloud Save keys, {"request":{...}} call shape).
/// No granular schema existed anywhere in the repo before this module - the FriendshipRecord/
/// FriendsIndexState shapes and the daily-gift rule (one gift per UTC calendar day per direction)
/// are this module's own design, following the SocialSafety/Bazaar engineering patterns; only the
/// verb surface itself ("add/accept/list/gift") was locked.</summary>
public sealed class FriendsOperations
{
    private const int MaxConflictReconciliations = 1;

    private readonly IFriendsStore _store;
    private readonly IFriendsClock _clock;

    public FriendsOperations(IFriendsStore store, IFriendsClock clock)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<FriendResult> AddFriendAsync(IExecutionContext context, IGameApiClient apiClient, FriendRequest request)
    {
        var validation = Validate(context, request);
        if (validation != null)
        {
            return validation;
        }

        string actorId = context.PlayerId!;
        string targetId = request.TargetAccountId;

        for (int attempt = 0; attempt <= MaxConflictReconciliations; attempt++)
        {
            try
            {
                var existing = await _store.LoadFriendshipAsync(context, apiClient, actorId, targetId);
                if (existing != null)
                {
                    return existing.Status == FriendshipStatus.Accepted
                        ? Failure("ALREADY_FRIENDS")
                        : Failure(existing.RequesterAccountId == actorId ? "ALREADY_REQUESTED" : "INCOMING_REQUEST_PENDING");
                }

                long now = _clock.UtcNowMs;
                var record = new FriendshipRecord
                {
                    Id = actorId + "|" + targetId,
                    RequesterAccountId = actorId,
                    RecipientAccountId = targetId,
                    Status = FriendshipStatus.Pending,
                    CreatedUtcMs = now,
                };

                await _store.SaveFriendshipAsync(context, apiClient, actorId, targetId, record);
                await AddToIndexAsync(context, apiClient, actorId, targetId);
                await AddToIndexAsync(context, apiClient, targetId, actorId);
                return new FriendResult { Success = true, Status = FriendshipStatus.Pending };
            }
            catch (FriendsStorageException exception) when (exception.ErrorCode == "CONFLICT" && attempt < MaxConflictReconciliations)
            {
            }
            catch (FriendsStorageException exception)
            {
                return Failure(exception.ErrorCode);
            }
        }

        return Failure("CONFLICT");
    }

    public Task<FriendResult> AcceptFriendAsync(IExecutionContext context, IGameApiClient apiClient, FriendRequest request)
        => RespondToRequestAsync(context, apiClient, request, accept: true);

    public Task<FriendResult> DeclineFriendAsync(IExecutionContext context, IGameApiClient apiClient, FriendRequest request)
        => RespondToRequestAsync(context, apiClient, request, accept: false);

    private async Task<FriendResult> RespondToRequestAsync(IExecutionContext context, IGameApiClient apiClient, FriendRequest request, bool accept)
    {
        var validation = Validate(context, request);
        if (validation != null)
        {
            return validation;
        }

        string actorId = context.PlayerId!;
        // TASK 020: the client only holds a pseudonymous alias for an existing (even
        // still-pending) relationship, per ab9ee64's ListFriends contract - resolve it back to
        // the real account id server-side before touching the friendship record.
        string targetId = await ResolveActionableTargetIdAsync(context, apiClient, request.TargetAccountId);

        for (int attempt = 0; attempt <= MaxConflictReconciliations; attempt++)
        {
            try
            {
                var record = await _store.LoadFriendshipAsync(context, apiClient, actorId, targetId);
                if (record == null || record.Status != FriendshipStatus.Pending)
                {
                    return Failure("REQUEST_NOT_FOUND");
                }

                if (record.RecipientAccountId != actorId)
                {
                    return Failure("NOT_RECIPIENT");
                }

                if (accept)
                {
                    record.Status = FriendshipStatus.Accepted;
                    record.RespondedUtcMs = _clock.UtcNowMs;
                    await _store.SaveFriendshipAsync(context, apiClient, actorId, targetId, record);
                    return new FriendResult { Success = true, Status = FriendshipStatus.Accepted };
                }

                await _store.DeleteFriendshipAsync(context, apiClient, actorId, targetId);
                await RemoveFromIndexAsync(context, apiClient, actorId, targetId);
                await RemoveFromIndexAsync(context, apiClient, targetId, actorId);
                return new FriendResult { Success = true };
            }
            catch (FriendsStorageException exception) when (exception.ErrorCode == "CONFLICT" && attempt < MaxConflictReconciliations)
            {
            }
            catch (FriendsStorageException exception)
            {
                return Failure(exception.ErrorCode);
            }
        }

        return Failure("CONFLICT");
    }

    /// <summary>Removes an accepted friendship (either side may call), or lets the original
    /// requester cancel their own still-pending outgoing request. The recipient of a pending
    /// request must use Decline, not Remove, to reject it - RemoveFriendAsync intentionally does
    /// not let a recipient silently delete an incoming request without the DECLINE audit trail
    /// this module's own tests assert on.</summary>
    /// <summary>No optimistic-lock retry here, unlike Add/Accept/Decline: DeleteFriendshipAsync
    /// carries no WriteLock, so there is no CONFLICT to retry against - meaning the load-then-
    /// delete below is not atomic. A concurrent Accept landing between the load and the delete
    /// could have its acceptance silently discarded by a stale Remove. Same accepted scope as
    /// Bazaar's own documented "no true cross-entity atomicity" limitation, not a workaround this
    /// module invents fixes for - flagging explicitly rather than leaving it implicit.</summary>
    public async Task<FriendResult> RemoveFriendAsync(IExecutionContext context, IGameApiClient apiClient, FriendRequest request)
    {
        var validation = Validate(context, request);
        if (validation != null)
        {
            return validation;
        }

        string actorId = context.PlayerId!;
        // TASK 020: same alias-resolution requirement as RespondToRequestAsync.
        string targetId = await ResolveActionableTargetIdAsync(context, apiClient, request.TargetAccountId);

        var record = await _store.LoadFriendshipAsync(context, apiClient, actorId, targetId);
        if (record == null)
        {
            return Failure("REQUEST_NOT_FOUND");
        }

        bool actorMayRemove = record.Status == FriendshipStatus.Accepted
            || (record.Status == FriendshipStatus.Pending && record.RequesterAccountId == actorId);
        if (!actorMayRemove)
        {
            return Failure("NOT_AUTHORIZED");
        }

        try
        {
            await _store.DeleteFriendshipAsync(context, apiClient, actorId, targetId);
            await RemoveFromIndexAsync(context, apiClient, actorId, targetId);
            await RemoveFromIndexAsync(context, apiClient, targetId, actorId);
        }
        catch (FriendsStorageException exception)
        {
            return Failure(exception.ErrorCode);
        }

        return new FriendResult { Success = true };
    }

    public async Task<ListFriendsResult> ListFriendsAsync(IExecutionContext context, IGameApiClient apiClient, ListFriendsRequest request)
    {
        if (context == null || string.IsNullOrWhiteSpace(context.PlayerId))
        {
            return new ListFriendsResult { ErrorCode = "AUTHENTICATION_REQUIRED" };
        }

        string actorId = context.PlayerId;
        try
        {
            var index = await _store.LoadIndexAsync(context, apiClient, actorId);
            long now = _clock.UtcNowMs;
            var summaries = new List<FriendSummary>();
            foreach (string counterpartId in index.CounterpartAccountIds)
            {
                var record = await _store.LoadFriendshipAsync(context, apiClient, actorId, counterpartId);
                if (record == null)
                {
                    continue; // index entry outlived its record (e.g. a prior best-effort cleanup failure) - skip, don't fail the whole list
                }

                bool actorIsRequester = record.RequesterAccountId == actorId;
                long actorLastGiftUtcMs = actorIsRequester ? record.RequesterLastGiftUtcMs : record.RecipientLastGiftUtcMs;
                summaries.Add(new FriendSummary
                {
                    CounterpartAliasId = await ResolveCounterpartAliasAsync(context, apiClient, counterpartId),
                    Status = record.Status,
                    IsOutgoingRequest = record.Status == FriendshipStatus.Pending && actorIsRequester,
                    CanGiftToday = record.Status == FriendshipStatus.Accepted && !IsSameUtcDay(actorLastGiftUtcMs, now),
                    CreatedUtcMs = record.CreatedUtcMs,
                });
            }

            return new ListFriendsResult { Success = true, Friends = summaries };
        }
        catch (FriendsStorageException exception)
        {
            return new ListFriendsResult { ErrorCode = exception.ErrorCode };
        }
    }

    /// <summary>BE-CC6-FRIENDS-IDENTITY-BOUNDARY-DECISION: resolves (creating on first use) the
    /// stable pseudonymous alias for a counterpart account - same load-or-create shape as Bazaar's
    /// ResolveSellerAliasAsync. Never derives the alias from the raw id itself.</summary>
    private async Task<string> ResolveCounterpartAliasAsync(IExecutionContext context, IGameApiClient apiClient, string counterpartAccountId)
    {
        string? existing = await _store.LoadCounterpartAliasAsync(context, apiClient, counterpartAccountId);
        if (existing != null)
        {
            return existing;
        }

        string aliasId = Guid.NewGuid().ToString("N");
        await _store.SaveCounterpartAliasAsync(context, apiClient, counterpartAccountId, aliasId);
        // TASK 020: write the reverse mapping at the same moment the alias is minted, so it is
        // resolvable server-side the very first time a client acts on it (accept/decline/remove/
        // gift) - never exposed through any client-facing type, only used by
        // ResolveActionableTargetIdAsync below.
        await _store.SaveAliasReverseMappingAsync(context, apiClient, aliasId, counterpartAccountId);
        return aliasId;
    }

    /// <summary>TASK 020 (CC6-BE-FRIEND-GIFT-ROUTING-020): resolves a client-supplied identifier
    /// that may be either a real account id (the only kind AddFriendAsync ever receives, since no
    /// alias exists before a relationship does) or a pseudonymous alias (the only kind ListFriends
    /// now hands back for an existing relationship, per ab9ee64). If the supplied value matches a
    /// known alias, the real account id is substituted server-side; otherwise the value is used
    /// as-is. This keeps the wire contract (FriendRequest.TargetAccountId) completely unchanged -
    /// no new field, no new endpoint - the resolution is purely an internal server-side step, per
    /// this task's "minimal contract change" requirement.</summary>
    private async Task<string> ResolveActionableTargetIdAsync(IExecutionContext context, IGameApiClient apiClient, string suppliedId)
    {
        string? resolvedAccountId = await _store.ResolveAccountIdFromAliasAsync(context, apiClient, suppliedId);
        return resolvedAccountId ?? suppliedId;
    }

    /// <summary>One gift per UTC calendar day per direction - e.g. A can gift B once today AND
    /// separately receive one from B the same day; A cannot gift B twice today.</summary>
    public async Task<GiftResult> SendDailyGiftAsync(IExecutionContext context, IGameApiClient apiClient, FriendRequest request)
    {
        var validation = Validate(context, request);
        if (validation != null)
        {
            return new GiftResult { Success = false, ErrorCode = validation.ErrorCode };
        }

        string actorId = context.PlayerId!;
        // TASK 020 (CC6-BE-FRIEND-GIFT-ROUTING-020): closes the exact gap FR flagged in ef8e0f3f -
        // FriendsPresenter now only ever holds a pseudonymous alias for an established
        // relationship (ab9ee64), never the raw TargetAccountId this call used to require. Resolve
        // it server-side before touching the friendship record; the wire contract itself is
        // unchanged.
        string targetId = await ResolveActionableTargetIdAsync(context, apiClient, request.TargetAccountId);

        for (int attempt = 0; attempt <= MaxConflictReconciliations; attempt++)
        {
            try
            {
                var record = await _store.LoadFriendshipAsync(context, apiClient, actorId, targetId);
                if (record == null || record.Status != FriendshipStatus.Accepted)
                {
                    return new GiftResult { ErrorCode = "NOT_FRIENDS" };
                }

                bool actorIsRequester = record.RequesterAccountId == actorId;
                long now = _clock.UtcNowMs;
                long lastGift = actorIsRequester ? record.RequesterLastGiftUtcMs : record.RecipientLastGiftUtcMs;
                if (IsSameUtcDay(lastGift, now))
                {
                    return new GiftResult { ErrorCode = "GIFT_ALREADY_SENT_TODAY" };
                }

                if (actorIsRequester)
                {
                    record.RequesterLastGiftUtcMs = now;
                }
                else
                {
                    record.RecipientLastGiftUtcMs = now;
                }

                await _store.SaveFriendshipAsync(context, apiClient, actorId, targetId, record);
                return new GiftResult { Success = true };
            }
            catch (FriendsStorageException exception) when (exception.ErrorCode == "CONFLICT" && attempt < MaxConflictReconciliations)
            {
            }
            catch (FriendsStorageException exception)
            {
                return new GiftResult { ErrorCode = exception.ErrorCode };
            }
        }

        return new GiftResult { ErrorCode = "CONFLICT" };
    }

    public static bool IsSameUtcDay(long firstUtcMs, long secondUtcMs)
    {
        if (firstUtcMs <= 0)
        {
            return false;
        }

        var first = DateTimeOffset.FromUnixTimeMilliseconds(firstUtcMs).UtcDateTime.Date;
        var second = DateTimeOffset.FromUnixTimeMilliseconds(secondUtcMs).UtcDateTime.Date;
        return first == second;
    }

    private static FriendResult? Validate(IExecutionContext context, FriendRequest request)
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

    private static FriendResult Failure(string errorCode) => new() { Success = false, ErrorCode = errorCode };

    /// <summary>Best-effort index bookkeeping, same reasoning as Bazaar's own
    /// AddToIndexAsync/RemoveFromIndexAsync: the FriendshipRecord write above is the real source
    /// of truth and has already succeeded by the time this runs, so a failure here only risks a
    /// stale/missing ListFriends entry (handled defensively in ListFriendsAsync above), never
    /// corrupts the relationship itself.</summary>
    private async Task AddToIndexAsync(IExecutionContext context, IGameApiClient apiClient, string ownerAccountId, string counterpartAccountId)
    {
        for (int attempt = 0; attempt <= MaxConflictReconciliations; attempt++)
        {
            try
            {
                var index = await _store.LoadIndexAsync(context, apiClient, ownerAccountId);
                if (!index.CounterpartAccountIds.Contains(counterpartAccountId))
                {
                    index.CounterpartAccountIds.Add(counterpartAccountId);
                    await _store.SaveIndexAsync(context, apiClient, ownerAccountId, index);
                }

                return;
            }
            catch (FriendsStorageException exception) when (exception.ErrorCode == "CONFLICT" && attempt < MaxConflictReconciliations)
            {
            }
            catch (FriendsStorageException)
            {
                return;
            }
        }
    }

    private async Task RemoveFromIndexAsync(IExecutionContext context, IGameApiClient apiClient, string ownerAccountId, string counterpartAccountId)
    {
        for (int attempt = 0; attempt <= MaxConflictReconciliations; attempt++)
        {
            try
            {
                var index = await _store.LoadIndexAsync(context, apiClient, ownerAccountId);
                if (index.CounterpartAccountIds.Remove(counterpartAccountId))
                {
                    await _store.SaveIndexAsync(context, apiClient, ownerAccountId, index);
                }

                return;
            }
            catch (FriendsStorageException exception) when (exception.ErrorCode == "CONFLICT" && attempt < MaxConflictReconciliations)
            {
            }
            catch (FriendsStorageException)
            {
                return;
            }
        }
    }
}

public sealed class FriendsModule
{
    private readonly FriendsOperations _operations;

    public FriendsModule()
        : this(new CloudSaveFriendsStore(), new SystemFriendsClock())
    {
    }

    internal FriendsModule(IFriendsStore store, IFriendsClock clock)
    {
        _operations = new FriendsOperations(store, clock);
    }

    [CloudCodeFunction("AddFriend")]
    public Task<FriendResult> AddFriend(IExecutionContext context, IGameApiClient apiClient, FriendRequest request)
        => _operations.AddFriendAsync(context, apiClient, request);

    [CloudCodeFunction("AcceptFriend")]
    public Task<FriendResult> AcceptFriend(IExecutionContext context, IGameApiClient apiClient, FriendRequest request)
        => _operations.AcceptFriendAsync(context, apiClient, request);

    [CloudCodeFunction("DeclineFriend")]
    public Task<FriendResult> DeclineFriend(IExecutionContext context, IGameApiClient apiClient, FriendRequest request)
        => _operations.DeclineFriendAsync(context, apiClient, request);

    [CloudCodeFunction("RemoveFriend")]
    public Task<FriendResult> RemoveFriend(IExecutionContext context, IGameApiClient apiClient, FriendRequest request)
        => _operations.RemoveFriendAsync(context, apiClient, request);

    [CloudCodeFunction("ListFriends")]
    public Task<ListFriendsResult> ListFriends(IExecutionContext context, IGameApiClient apiClient, ListFriendsRequest request)
        => _operations.ListFriendsAsync(context, apiClient, request);

    [CloudCodeFunction("SendDailyGift")]
    public Task<GiftResult> SendDailyGift(IExecutionContext context, IGameApiClient apiClient, FriendRequest request)
        => _operations.SendDailyGiftAsync(context, apiClient, request);
}

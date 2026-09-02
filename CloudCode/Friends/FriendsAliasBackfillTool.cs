using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace MyriadOfDragons.CloudCode.Friends;

/// <summary>
/// TEMPORARY, ONE-TIME BACKFILL TOOL — DELETE THIS FILE AND REDEPLOY THE Friends MODULE ONCE THE
/// BACKFILL HAS BEEN RUN AND ACCEPTED. This is not a permanent production endpoint: it exists
/// solely to populate the friends-alias-reverse index for aliases minted before 034b1141's
/// alias-to-account routing shipped (see docs/FRIENDS_UGS_EXECUTION_LIST_2026-09-02.md §2).
///
/// WHY THIS NEEDS AN ACCOUNT-ID LIST AS INPUT, NOT SELF-DISCOVERY: Cloud Save (and this repo's
/// established CloudCode call shape throughout) has no "enumerate every player" API - a
/// CloudCodeFunction only ever has direct addressable access to accounts it is TOLD about. This
/// tool cannot discover the account list itself; whoever runs it must supply one (e.g. an
/// analytics/Dashboard player-id export). This is stated explicitly rather than pretending a
/// global enumeration capability exists that this SDK does not have.
///
/// WHAT IT DOES, per account id in the supplied list:
/// 1. Ensures the account itself has a stable alias + a friends-alias-reverse entry (it may be
///    someone else's counterpart even if it has no friendships of its own).
/// 2. Loads its FriendsIndexState and, for every counterpart listed there, does the same for that
///    counterpart id - this is what covers "both sides of every counterpart pair": resolving the
///    scanned account AND every counterpart it lists is sufficient, because every real
///    relationship appears in at least one side's index (both sides for an accepted friendship,
///    the recipient's or requester's index for a pending request, per
///    CloudCode/Friends/FriendsOperations.AddFriendAsync's own AddToIndexAsync calls).
///
/// This deliberately reuses the exact same load-or-create logic
/// FriendsOperations.ResolveCounterpartAliasAsync already ships with (via the same, already-public
/// IFriendsStore members it calls) - it does not reimplement or alter that logic, and
/// FriendsOperations.cs itself is not touched by this file at all.
/// </summary>
public sealed class FriendsAliasBackfillOperations
{
    private readonly IFriendsStore _store;

    public FriendsAliasBackfillOperations(IFriendsStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<BackfillAliasReverseIndexResult> RunAsync(IExecutionContext context, IGameApiClient apiClient, BackfillAliasReverseIndexRequest request)
    {
        var result = new BackfillAliasReverseIndexResult();
        var accountIds = request?.AccountIds ?? new List<string>();
        var alreadyProcessed = new HashSet<string>(StringComparer.Ordinal);

        foreach (string rawAccountId in accountIds)
        {
            string accountId = (rawAccountId ?? string.Empty).Trim();
            if (accountId.Length == 0)
            {
                continue;
            }

            result.AccountsScanned++;

            try
            {
                // The scanned account may itself be someone else's counterpart - ensure it has an
                // alias regardless of whether it has any relationships of its own.
                await EnsureAliasAndReverseMappingAsync(context, apiClient, accountId, alreadyProcessed, result);

                var index = await _store.LoadIndexAsync(context, apiClient, accountId);
                foreach (string rawCounterpartId in index.CounterpartAccountIds)
                {
                    string counterpartId = (rawCounterpartId ?? string.Empty).Trim();
                    if (counterpartId.Length == 0)
                    {
                        continue;
                    }

                    await EnsureAliasAndReverseMappingAsync(context, apiClient, counterpartId, alreadyProcessed, result);
                }
            }
            catch (FriendsStorageException exception)
            {
                result.AccountsFailed++;
                result.FailedAccountIds.Add(accountId);
                // BE-FRIENDS-STORAGE-014 diagnostic addition: CloudSaveFriendsStore.ClassifyStorageError
                // collapses every non-409 exception into ErrorCode="STORAGE_UNAVAILABLE", discarding
                // the real cause. FriendsStorageException.InnerException still carries it (base(msg,
                // innerException) in FriendsState.cs) - surface it here so a live run reveals the
                // actual underlying exception instead of just the generic label.
                string innerDetail = exception.InnerException != null
                    ? $"{exception.InnerException.GetType().Name}: {exception.InnerException.Message}"
                    : "(no inner exception captured)";
                result.Errors.Add($"{accountId}: {exception.ErrorCode} | inner={innerDetail}");
            }
            catch (Exception exception)
            {
                result.AccountsFailed++;
                result.FailedAccountIds.Add(accountId);
                result.Errors.Add($"{accountId}: {exception.GetType().Name}: {exception.Message}");
            }
        }

        return result;
    }

    /// <summary>Idempotent per account: if this exact account id was already handled earlier in
    /// THIS run (e.g. it appears as one caller's account id and also as another caller's
    /// counterpart), it is counted once, not twice - this is what makes AliasesUpdated/
    /// AliasesSkipped accurate rather than double-counted within a single pass. Across separate
    /// runs, idempotency comes from the underlying load-or-create store calls themselves (the same
    /// ones FriendsOperations.ResolveCounterpartAliasAsync already uses in production) - a second
    /// full run always finds every alias/reverse-entry already present and reports it as skipped.</summary>
    private async Task EnsureAliasAndReverseMappingAsync(IExecutionContext context, IGameApiClient apiClient, string accountId, HashSet<string> alreadyProcessed, BackfillAliasReverseIndexResult result)
    {
        if (!alreadyProcessed.Add(accountId))
        {
            return;
        }

        bool wroteSomething = false;

        string? aliasId = await _store.LoadCounterpartAliasAsync(context, apiClient, accountId);
        if (aliasId == null)
        {
            aliasId = Guid.NewGuid().ToString("N");
            await _store.SaveCounterpartAliasAsync(context, apiClient, accountId, aliasId);
            wroteSomething = true;
        }

        string? reverseAccountId = await _store.ResolveAccountIdFromAliasAsync(context, apiClient, aliasId);
        if (reverseAccountId == null)
        {
            await _store.SaveAliasReverseMappingAsync(context, apiClient, aliasId, accountId);
            wroteSomething = true;
        }

        if (wroteSomething)
        {
            result.AliasesUpdated++;
        }
        else
        {
            result.AliasesSkipped++;
        }
    }
}

public sealed class BackfillAliasReverseIndexRequest
{
    public List<string> AccountIds { get; set; } = new();
}

public sealed class BackfillAliasReverseIndexResult
{
    /// <summary>Number of account ids in the input list actually processed (after trimming blanks
    /// and de-duplication is NOT applied here - duplicates in the input list are scanned once each,
    /// see AliasesUpdated/AliasesSkipped for the de-duplicated resolution counts).</summary>
    public int AccountsScanned { get; set; }

    /// <summary>Number of distinct accounts (scanned accounts + their counterparts, de-duplicated
    /// within this run) for which an alias and/or reverse-index entry was newly written.</summary>
    public int AliasesUpdated { get; set; }

    /// <summary>Number of distinct accounts that already had both an alias and a reverse-index
    /// entry - a true no-op for that account. On a second full run over the same input, this
    /// should equal AliasesUpdated + AliasesSkipped from the first run, and AliasesUpdated on the
    /// second run should be 0.</summary>
    public int AliasesSkipped { get; set; }

    public int AccountsFailed { get; set; }
    public List<string> FailedAccountIds { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>TEMPORARY module surface - see this file's own top-of-file doc comment. Mirrors
/// FriendsModule's exact constructor shape (single public parameterless constructor for Cloud
/// Code's own "exactly one public constructor" requirement, internal DI constructor for tests) -
/// same real deployment constraint the Aug-24 deployment pass found and fixed for all 4 modules,
/// per docs/LOCKED_DECISIONS_REGISTER.md.</summary>
public sealed class FriendsAliasBackfillModule
{
    private readonly FriendsAliasBackfillOperations _operations;

    public FriendsAliasBackfillModule()
        : this(new CloudSaveFriendsStore())
    {
    }

    internal FriendsAliasBackfillModule(IFriendsStore store)
    {
        _operations = new FriendsAliasBackfillOperations(store);
    }

    [CloudCodeFunction("BackfillFriendsAliasReverseIndex")]
    public Task<BackfillAliasReverseIndexResult> BackfillFriendsAliasReverseIndex(IExecutionContext context, IGameApiClient apiClient, BackfillAliasReverseIndexRequest request)
        => _operations.RunAsync(context, apiClient, request);
}

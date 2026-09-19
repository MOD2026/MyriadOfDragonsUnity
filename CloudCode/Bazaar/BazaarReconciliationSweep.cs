using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace MyriadOfDragons.CloudCode.Bazaar;

public enum ReconciliationFindingKind
{
    /// <summary>A settlement stuck InProgress was resumed through the saga and completed.</summary>
    RecoveredSettlement,
    /// <summary>Resuming reached the saga's own definitive abort (e.g. the buyer no longer has the credits).</summary>
    AbortedSettlement,
    /// <summary>Resuming hit a failure (typically storage); the journal is left InProgress for a later sweep.</summary>
    DeferredFailure,
    /// <summary>The journal is younger than the caller-supplied minimum age, so it was left alone.</summary>
    SkippedTooRecent,
    /// <summary>An Aborted journal still held its listing claim; the claim was released (listing is Active again).</summary>
    ReleasedAbortedClaim,
    /// <summary>An index entry for a listing that is no longer Active (sold/cancelled/missing) was removed.</summary>
    RemovedStaleIndexEntry,
    /// <summary>A claimed listing has no journal and no recorded result; nothing is guessed or mutated.</summary>
    UnrecoverableMissingJournal,
    /// <summary>A recovered sale failed the post-recovery consistency check; needs a human.</summary>
    InconsistentAfterRecovery,
}

public sealed class ReconciliationFinding
{
    public string ListingId { get; set; } = string.Empty;
    public ReconciliationFindingKind Kind { get; set; }
    public string Detail { get; set; } = string.Empty;
}

public sealed class ReconciliationSweepResult
{
    public int Scanned { get; set; }
    public List<ReconciliationFinding> Findings { get; } = new();

    public int Count(ReconciliationFindingKind kind) => Findings.Count(f => f.Kind == kind);

    /// <summary>True when the sweep found nothing to fix or flag - the state a second sweep over a
    /// healed board must report (idempotent replay).</summary>
    public bool IsClean => Findings.Count == 0;
}

/// <summary>
/// Deterministic reconciliation sweep for the Bazaar settlement saga. It heals exactly two kinds of
/// leftover state and flags what it must not guess at:
///  1. SettlementJournal records stuck InProgress (a buyer whose request failed mid-settlement and
///     never retried) - resumed through the saga's own idempotent path, so recovery can never charge
///     or credit twice.
///  2. Stale active-index entries left by the deliberately best-effort index removal (listing sold,
///     cancelled, missing, or legacy-sold without a journal).
///
/// DISCOVERY: Cloud Save has no "enumerate every player" API, so the sweep cannot scan buyers'
/// journals directly. It uses the active-listing index instead: the saga's LAST step is removing a
/// listing from the index, so every unfinished (or index-stale) settlement leaves a claimed listing
/// still indexed, and that listing's SettlementId encodes the "buyerId|idempotencyKey" needed to
/// find its journal. A journal orphaned BEFORE the listing was claimed changed nothing and leaves
/// no trace here - it is harmless and completes when the buyer retries.
///
/// DETERMINISM/IDEMPOTENCY: listings are processed in index order, findings are reported in that
/// order, and a second sweep over a healed board reports no findings. No clock, schedule, alert
/// threshold or age gate is invented here: <c>minJournalAgeMs</c> is a caller-supplied argument
/// (default 0 = recover everything), and how often to run the sweep is a deployment decision. This
/// is a library class, NOT a CloudCodeFunction - no endpoint is added or deployed by it.
/// </summary>
public sealed class BazaarReconciliationSweep
{
    // Internal batching detail for the shared listing load (same size as the board's default page),
    // not a policy value.
    private const int ListingBatchSize = 20;

    private readonly IBazaarStore _store;
    private readonly BazaarOperations _operations;
    private readonly IBazaarClock _clock;

    public BazaarReconciliationSweep(IBazaarStore store, BazaarOperations operations, IBazaarClock clock)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _operations = operations ?? throw new ArgumentNullException(nameof(operations));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<ReconciliationSweepResult> SweepAsync(IExecutionContext context, IGameApiClient apiClient, long minJournalAgeMs = 0)
    {
        var result = new ReconciliationSweepResult();
        var index = await _store.LoadIndexAsync(context, apiClient);
        var listingIds = index.ActiveListingIds.Distinct().ToList();

        var listingsById = new Dictionary<string, BazaarListing>();
        for (int offset = 0; offset < listingIds.Count; offset += ListingBatchSize)
        {
            var batch = listingIds.Skip(offset).Take(ListingBatchSize).ToList();
            foreach (var listing in await _store.LoadListingsBatchAsync(context, apiClient, batch))
            {
                listingsById[listing.ListingId] = listing;
            }
        }

        foreach (string listingId in listingIds)
        {
            result.Scanned++;

            if (!listingsById.TryGetValue(listingId, out var listing))
            {
                await RemoveFromIndexAsync(context, apiClient, listingId);
                result.Findings.Add(Finding(listingId, ReconciliationFindingKind.RemovedStaleIndexEntry, "listing record missing"));
                continue;
            }

            switch (listing.State)
            {
                case BazaarListingState.Active:
                    break; // healthy

                case BazaarListingState.Cancelled:
                    await RemoveFromIndexAsync(context, apiClient, listingId);
                    result.Findings.Add(Finding(listingId, ReconciliationFindingKind.RemovedStaleIndexEntry, "listing cancelled"));
                    break;

                case BazaarListingState.Sold:
                    await ReconcileSoldListingAsync(context, apiClient, listing, minJournalAgeMs, result);
                    break;
            }
        }

        return result;
    }

    private async Task ReconcileSoldListingAsync(IExecutionContext context, IGameApiClient apiClient, BazaarListing listing, long minJournalAgeMs, ReconciliationSweepResult result)
    {
        if (string.IsNullOrEmpty(listing.SettlementId) || !TryParseSettlementId(listing.SettlementId, out string buyerId, out string idempotencyKey))
        {
            // Sold before the saga existed (or without a journal): nothing to recover, only the index is stale.
            await RemoveFromIndexAsync(context, apiClient, listing.ListingId);
            result.Findings.Add(Finding(listing.ListingId, ReconciliationFindingKind.RemovedStaleIndexEntry, "sold without a settlement journal"));
            return;
        }

        var recorded = await _store.TryGetIdempotentBuyResultAsync(context, apiClient, buyerId, idempotencyKey);
        var journal = await _store.TryGetSettlementJournalAsync(context, apiClient, buyerId, idempotencyKey);

        if (recorded != null && recorded.Success)
        {
            // The sale finished (its result is durable); only the best-effort index removal is missing.
            await RemoveFromIndexAsync(context, apiClient, listing.ListingId);
            result.Findings.Add(Finding(listing.ListingId, ReconciliationFindingKind.RemovedStaleIndexEntry, "sale completed, index removal had not landed"));
            return;
        }

        if (journal == null)
        {
            result.Findings.Add(Finding(listing.ListingId, ReconciliationFindingKind.UnrecoverableMissingJournal,
                $"listing claimed by '{listing.SettlementId}' but no journal or result exists"));
            return;
        }

        if (journal.Phase == SettlementJournal.PhaseAborted)
        {
            if (journal.SettlementId == listing.SettlementId)
            {
                // Abort happens before any money moves; the claim release simply never landed.
                listing.State = BazaarListingState.Active;
                listing.SettlementId = null;
                await _store.SaveListingAsync(context, apiClient, listing);
                result.Findings.Add(Finding(listing.ListingId, ReconciliationFindingKind.ReleasedAbortedClaim, journal.FailureCode ?? "aborted"));
            }

            return;
        }

        if (journal.Phase == SettlementJournal.PhaseCompleted)
        {
            await RemoveFromIndexAsync(context, apiClient, listing.ListingId);
            result.Findings.Add(Finding(listing.ListingId, ReconciliationFindingKind.RemovedStaleIndexEntry, "journal completed, index removal had not landed"));
            return;
        }

        // InProgress.
        long age = _clock.UtcNowMs - journal.StartedUtcMs;
        if (age < minJournalAgeMs)
        {
            result.Findings.Add(Finding(listing.ListingId, ReconciliationFindingKind.SkippedTooRecent, $"journal age {age}ms < caller minimum {minJournalAgeMs}ms"));
            return;
        }

        var outcome = await _operations.ResumeSettlementAsync(context, apiClient, journal, idempotencyKey);
        if (outcome.Success)
        {
            await RemoveFromIndexAsync(context, apiClient, listing.ListingId);
            var violations = await VerifySettledSaleAsync(context, apiClient, journal, idempotencyKey);
            result.Findings.Add(violations.Count == 0
                ? Finding(listing.ListingId, ReconciliationFindingKind.RecoveredSettlement, journal.SettlementId)
                : Finding(listing.ListingId, ReconciliationFindingKind.InconsistentAfterRecovery, string.Join("; ", violations)));
            return;
        }

        if (outcome.ErrorCode == "INSUFFICIENT_CREDITS" || outcome.ErrorCode == "LISTING_NOT_AVAILABLE")
        {
            // The saga reached its own definitive abort (and released or never held the claim).
            var current = (await _store.LoadListingsBatchAsync(context, apiClient, new[] { listing.ListingId })).FirstOrDefault();
            if (current != null && current.State != BazaarListingState.Active)
            {
                await RemoveFromIndexAsync(context, apiClient, listing.ListingId);
            }

            result.Findings.Add(Finding(listing.ListingId, ReconciliationFindingKind.AbortedSettlement, outcome.ErrorCode));
            return;
        }

        result.Findings.Add(Finding(listing.ListingId, ReconciliationFindingKind.DeferredFailure, outcome.ErrorCode ?? "unknown"));
    }

    /// <summary>The store-level counterpart of the reconciliation definition used by the contract
    /// tests: a settled sale must have both wallets marked applied for THIS settlement, the instance
    /// owned by the buyer, the listing Sold by this settlement, a recorded result, and credits that
    /// balance (price = seller + burn + treasury). Returns violations; empty means consistent.</summary>
    internal async Task<List<string>> VerifySettledSaleAsync(IExecutionContext context, IGameApiClient apiClient, SettlementJournal journal, string idempotencyKey)
    {
        var violations = new List<string>();

        if (journal.SellerReceivesCredits + journal.TaxBurnCredits + journal.TaxTreasuryCredits != journal.PriceCredits)
        {
            violations.Add("price != seller + burn + treasury");
        }

        var buyerWallet = await _store.LoadWalletAsync(context, apiClient, journal.BuyerId);
        if (!buyerWallet.AppliedSettlementIds.Contains(journal.SettlementId)) violations.Add("buyer debit not applied");
        var sellerWallet = await _store.LoadWalletAsync(context, apiClient, journal.SellerId);
        if (!sellerWallet.AppliedSettlementIds.Contains(journal.SettlementId)) violations.Add("seller credit not applied");

        var instance = await _store.LoadInstanceAsync(context, apiClient, journal.InstanceId);
        if (instance == null || instance.OwnerId != journal.BuyerId || instance.State != ItemInstanceState.Owned) violations.Add("instance not owned by buyer");

        var listing = (await _store.LoadListingsBatchAsync(context, apiClient, new[] { journal.ListingId })).FirstOrDefault();
        if (listing == null || listing.State != BazaarListingState.Sold || listing.SettlementId != journal.SettlementId) violations.Add("listing not sold by this settlement");

        var recorded = await _store.TryGetIdempotentBuyResultAsync(context, apiClient, journal.BuyerId, idempotencyKey);
        if (recorded == null || !recorded.Success || recorded.PricePaidCredits != journal.PriceCredits) violations.Add("no recorded result for this settlement");

        var index = await _store.LoadIndexAsync(context, apiClient);
        if (index.ActiveListingIds.Contains(journal.ListingId)) violations.Add("listing still in the active index");

        return violations;
    }

    private static bool TryParseSettlementId(string settlementId, out string buyerId, out string idempotencyKey)
    {
        // SettlementId is "buyerId|idempotencyKey"; the key may itself contain '|', so split on the first one only.
        int separator = settlementId.IndexOf('|');
        if (separator <= 0 || separator == settlementId.Length - 1)
        {
            buyerId = string.Empty;
            idempotencyKey = string.Empty;
            return false;
        }

        buyerId = settlementId.Substring(0, separator);
        idempotencyKey = settlementId.Substring(separator + 1);
        return true;
    }

    private async Task RemoveFromIndexAsync(IExecutionContext context, IGameApiClient apiClient, string listingId)
    {
        // One conflict reconciliation, matching BazaarOperations' own index maintenance. A failure is
        // left for the next sweep (the entry is still stale-and-detectable), never thrown.
        for (int attempt = 0; attempt <= 1; attempt++)
        {
            try
            {
                var index = await _store.LoadIndexAsync(context, apiClient);
                if (index.ActiveListingIds.Remove(listingId))
                {
                    await _store.SaveIndexAsync(context, apiClient, index);
                }

                return;
            }
            catch (BazaarStorageException exception) when (exception.ErrorCode == "CONFLICT" && attempt < 1)
            {
            }
            catch (BazaarStorageException)
            {
                return;
            }
        }
    }

    private static ReconciliationFinding Finding(string listingId, ReconciliationFindingKind kind, string detail) =>
        new() { ListingId = listingId, Kind = kind, Detail = detail };
}

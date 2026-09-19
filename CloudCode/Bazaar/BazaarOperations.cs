using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace MyriadOfDragons.CloudCode.Bazaar;

/// <summary>
/// First-pass scaffold covering the core list/buy/cancel loop from
/// PHASE1_BAZAAR_SYSTEM_PACKET_2026-08-23.md §§1-3 and BAZAAR_PHASE1_CC_ACCEPT_2026-08-23.md's
/// locked decisions 1-2. Explicitly does NOT cover (see README "Deferred"): the one-time capped
/// Treasury genesis-liquidity reverse auction (locked decision 3/4 - a separate later module, per
/// CC's own instruction), the "seller retains one usable copy" check (needs real Collection
/// copy-count integration this module doesn't have), account-age/activity/step-up-auth gates,
/// linked-account/device-cluster/wash-pattern detection, listing expiry, and true cross-entity
/// transactional atomicity (§5 of the system packet lists this as a still-open backend
/// dependency, not something already solved - see BuyItemAsync's own comment).
/// </summary>
public sealed class BazaarOperations
{
    private const int MaxConflictReconciliations = 1;

    private readonly IBazaarStore _store;
    private readonly IBazaarClock _clock;
    private readonly IBazaarRulesConfiguration _rules;

    public BazaarOperations(IBazaarStore store, IBazaarClock clock, IBazaarRulesConfiguration? rules = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _rules = rules ?? new RemoteConfigBazaarRulesConfiguration();
    }

    /// <summary>Lists an owned, Tradeable, rarity 1-4 instance that isn't already listed and has
    /// cleared its acquisition/relist hold. Returns the Gold fee due (2% of ask, minimum 50) as a
    /// computed value only - this module does not debit Gold itself. See README "Deferred: Gold
    /// fee enforcement": Gold is currently Save-side only (frozen PlayerProfile), not part of any
    /// server-authoritative ledger this module can reach, so a trusted server-side Gold debit
    /// isn't possible until that changes.</summary>
    public async Task<ListingResult> ListItemAsync(IExecutionContext context, IGameApiClient apiClient, ListItemRequest request)
    {
        if (context == null || string.IsNullOrWhiteSpace(context.PlayerId))
        {
            return new ListingResult { ErrorCode = "AUTHENTICATION_REQUIRED" };
        }

        if (request == null || string.IsNullOrWhiteSpace(request.InstanceId) || request.AskCredits <= 0)
        {
            return new ListingResult { ErrorCode = "INVALID_REQUEST" };
        }

        var rules = await _rules.LoadAsync(context, apiClient);
        long now = _clock.UtcNowMs;

        for (int attempt = 0; attempt <= MaxConflictReconciliations; attempt++)
        {
            try
            {
                var instance = await _store.LoadInstanceAsync(context, apiClient, request.InstanceId);
                if (instance == null)
                {
                    return new ListingResult { ErrorCode = "INSTANCE_NOT_FOUND" };
                }

                if (instance.OwnerId != context.PlayerId)
                {
                    return new ListingResult { ErrorCode = "NOT_OWNER" };
                }

                if (!instance.Tradeable || instance.Rarity < 1 || instance.Rarity > 4)
                {
                    return new ListingResult { ErrorCode = "NOT_TRADEABLE" };
                }

                if (instance.State != ItemInstanceState.Owned)
                {
                    return new ListingResult { ErrorCode = "ALREADY_LISTED" };
                }

                long requiredHold = instance.LastAcquisitionWasPurchase ? rules.RelistHoldMs : rules.AcquisitionHoldMs;
                if (now - instance.LastAcquiredUtcMs < requiredHold)
                {
                    return new ListingResult { ErrorCode = "HOLD_NOT_ELAPSED" };
                }

                var listing = new BazaarListing
                {
                    ListingId = Guid.NewGuid().ToString("N"),
                    InstanceId = instance.InstanceId,
                    SellerId = context.PlayerId,
                    AskCredits = request.AskCredits,
                    State = BazaarListingState.Active,
                    CreatedUtcMs = now,
                };

                instance.State = ItemInstanceState.Listed;
                await _store.SaveInstanceAsync(context, apiClient, instance);
                await _store.SaveListingAsync(context, apiClient, listing);
                await AddToIndexAsync(context, apiClient, listing.ListingId);

                int goldFee = Math.Max(rules.ListingFeeMinimumGold, (request.AskCredits * rules.ListingFeePercent) / 100);
                return new ListingResult { Success = true, ListingId = listing.ListingId, GoldFeeDue = goldFee };
            }
            catch (BazaarStorageException exception) when (exception.ErrorCode == "CONFLICT" && attempt < MaxConflictReconciliations)
            {
            }
            catch (BazaarStorageException exception)
            {
                return new ListingResult { ErrorCode = exception.ErrorCode };
            }
        }

        return new ListingResult { ErrorCode = "CONFLICT" };
    }

    /// <summary>
    /// §2's transaction shape: verify listing and both accounts, debit buyer, apply the sale tax
    /// (12%: 6% burned, 6% Treasury), credit seller, transfer ownership, close the listing.
    ///
    /// SETTLEMENT IS A RESUMABLE FORWARD-RECOVERY SAGA (Cloud Save has no cross-entity transaction,
    /// so "atomic" here means: crash-safe, exactly-once, and never left needing manual repair).
    /// A durable <see cref="SettlementJournal"/> holding the frozen amounts is written BEFORE the
    /// first mutation. Each step is individually idempotent - the listing is claimed with a
    /// settlementId in one single-entity write, each wallet records the settlement ids already
    /// applied in the SAME save as the balance change, instance transfer and index removal are
    /// state-checked - so a retry with the same (buyer, idempotencyKey) resumes from the journal and
    /// can never debit or credit twice. Failure before the journal = nothing happened; failure
    /// after it = the retry completes the sale. The only compensation is the one case forward
    /// recovery cannot complete (buyer no longer has the credits after the listing was claimed):
    /// the claim is released and the journal aborted. A settlement whose client never retries stays
    /// InProgress and is the input to the reconciliation sweep (still a deployment prerequisite).
    /// </summary>
    public async Task<BuyResult> BuyItemAsync(IExecutionContext context, IGameApiClient apiClient, BuyItemRequest request)
    {
        if (context == null || string.IsNullOrWhiteSpace(context.PlayerId))
        {
            return new BuyResult { ErrorCode = "AUTHENTICATION_REQUIRED" };
        }

        if (request == null || string.IsNullOrWhiteSpace(request.ListingId) || string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return new BuyResult { ErrorCode = "INVALID_REQUEST" };
        }

        string buyerId = context.PlayerId;

        var existing = await _store.TryGetIdempotentBuyResultAsync(context, apiClient, buyerId, request.IdempotencyKey);
        if (existing != null)
        {
            return existing;
        }

        SettlementJournal? journal;
        try
        {
            journal = await _store.TryGetSettlementJournalAsync(context, apiClient, buyerId, request.IdempotencyKey);
        }
        catch (BazaarStorageException exception)
        {
            return new BuyResult { ErrorCode = exception.ErrorCode };
        }

        if (journal != null)
        {
            if (journal.Phase == SettlementJournal.PhaseAborted)
            {
                // A definitive, already-recorded outcome for this key - replay it, never re-attempt.
                return new BuyResult { ErrorCode = journal.FailureCode ?? "SETTLEMENT_ABORTED" };
            }

            // InProgress (or Completed whose result record was lost): resume from the frozen journal.
            return await ExecuteSettlementAsync(context, apiClient, journal, request.IdempotencyKey);
        }

        var rules = await _rules.LoadAsync(context, apiClient);
        long now = _clock.UtcNowMs;

        var listing = await _store.LoadListingAsync(context, apiClient, request.ListingId);
        if (listing == null || listing.State != BazaarListingState.Active)
        {
            return new BuyResult { ErrorCode = "LISTING_NOT_AVAILABLE" };
        }

        if (listing.SellerId == buyerId)
        {
            return new BuyResult { ErrorCode = "SELF_TRADE_NOT_ALLOWED" };
        }

        var buyerWallet = await _store.LoadWalletAsync(context, apiClient, buyerId);
        if (buyerWallet.BalanceCredits < listing.AskCredits)
        {
            return new BuyResult { ErrorCode = "INSUFFICIENT_CREDITS" };
        }

        var sellerWallet = await _store.LoadWalletAsync(context, apiClient, listing.SellerId);
        if (CountRecentSales(sellerWallet, now, rules.RollingSalesWindowMs) >= rules.MaxSalesPerRollingWindow)
        {
            return new BuyResult { ErrorCode = "SELLER_SALE_LIMIT_REACHED" };
        }

        var instance = await _store.LoadInstanceAsync(context, apiClient, listing.InstanceId);
        if (instance == null || instance.State != ItemInstanceState.Listed)
        {
            return new BuyResult { ErrorCode = "LISTING_NOT_AVAILABLE" };
        }

        int tax = (listing.AskCredits * rules.SaleTaxPercent) / 100;
        int taxBurn = (listing.AskCredits * rules.SaleTaxBurnPercent) / 100;

        journal = new SettlementJournal
        {
            SettlementId = buyerId + "|" + request.IdempotencyKey,
            Phase = SettlementJournal.PhaseInProgress,
            BuyerId = buyerId,
            SellerId = listing.SellerId,
            ListingId = listing.ListingId,
            InstanceId = listing.InstanceId,
            PriceCredits = listing.AskCredits,
            SellerReceivesCredits = listing.AskCredits - tax,
            TaxBurnCredits = taxBurn,
            TaxTreasuryCredits = tax - taxBurn,
            StartedUtcMs = now,
        };

        // Recorded BEFORE the first irreversible mutation: if this write fails nothing has changed
        // and the caller can simply retry; if it succeeds every later failure is resumable.
        try
        {
            await _store.SaveSettlementJournalAsync(context, apiClient, buyerId, request.IdempotencyKey, journal);
        }
        catch (BazaarStorageException exception)
        {
            return new BuyResult { ErrorCode = exception.ErrorCode };
        }

        return await ExecuteSettlementAsync(context, apiClient, journal, request.IdempotencyKey);
    }

    private const int MaxAppliedSettlementIds = 64;

    private static bool WasApplied(WalletState wallet, string settlementId) => wallet.AppliedSettlementIds.Contains(settlementId);

    private static void MarkApplied(WalletState wallet, string settlementId)
    {
        wallet.AppliedSettlementIds.Add(settlementId);
        if (wallet.AppliedSettlementIds.Count > MaxAppliedSettlementIds)
        {
            wallet.AppliedSettlementIds.RemoveRange(0, wallet.AppliedSettlementIds.Count - MaxAppliedSettlementIds);
        }
    }

    /// <summary>Runs (or resumes) every settlement step. Each step is idempotent, so this is safe
    /// to call again after any storage failure - including one that lands the write but still
    /// reports an error.</summary>
    private async Task<BuyResult> ExecuteSettlementAsync(IExecutionContext context, IGameApiClient apiClient, SettlementJournal journal, string idempotencyKey)
    {
        try
        {
            // 1. Claim the listing (single-entity write; the real store's WriteLock makes a
            //    concurrent second buyer lose with CONFLICT instead of double-selling).
            var listing = await _store.LoadListingAsync(context, apiClient, journal.ListingId);
            if (listing == null)
            {
                return await AbortSettlementAsync(context, apiClient, journal, idempotencyKey, "LISTING_NOT_AVAILABLE");
            }

            if (listing.State == BazaarListingState.Active)
            {
                listing.State = BazaarListingState.Sold;
                listing.SettlementId = journal.SettlementId;
                await _store.SaveListingAsync(context, apiClient, listing);
            }
            else if (!(listing.State == BazaarListingState.Sold && listing.SettlementId == journal.SettlementId))
            {
                // Sold to someone else, or cancelled, since validation - this settlement never owned it.
                return await AbortSettlementAsync(context, apiClient, journal, idempotencyKey, "LISTING_NOT_AVAILABLE");
            }

            // 2. Debit the buyer (idempotent via AppliedSettlementIds, saved with the balance).
            var buyerWallet = await _store.LoadWalletAsync(context, apiClient, journal.BuyerId);
            if (!WasApplied(buyerWallet, journal.SettlementId))
            {
                if (buyerWallet.BalanceCredits < journal.PriceCredits)
                {
                    // The one case forward recovery cannot complete: release the claim, abort.
                    listing.State = BazaarListingState.Active;
                    listing.SettlementId = null;
                    await _store.SaveListingAsync(context, apiClient, listing);
                    return await AbortSettlementAsync(context, apiClient, journal, idempotencyKey, "INSUFFICIENT_CREDITS");
                }

                buyerWallet.BalanceCredits -= journal.PriceCredits;
                MarkApplied(buyerWallet, journal.SettlementId);
                await _store.SaveWalletAsync(context, apiClient, buyerWallet);
            }

            // 3. Credit the seller (same idempotent-apply pattern).
            var sellerWallet = await _store.LoadWalletAsync(context, apiClient, journal.SellerId);
            if (!WasApplied(sellerWallet, journal.SettlementId))
            {
                sellerWallet.BalanceCredits += journal.SellerReceivesCredits;
                sellerWallet.RecentSaleUtcMs.Add(journal.StartedUtcMs);
                MarkApplied(sellerWallet, journal.SettlementId);
                await _store.SaveWalletAsync(context, apiClient, sellerWallet);
            }

            // 4. Transfer ownership (state-checked, so a re-run is a no-op).
            var instance = await _store.LoadInstanceAsync(context, apiClient, journal.InstanceId);
            if (instance == null)
            {
                return new BuyResult { ErrorCode = "INSTANCE_NOT_FOUND" };
            }

            if (!(instance.OwnerId == journal.BuyerId && instance.State == ItemInstanceState.Owned))
            {
                instance.OwnerId = journal.BuyerId;
                instance.State = ItemInstanceState.Owned;
                instance.LastAcquiredUtcMs = journal.StartedUtcMs;
                instance.LastAcquisitionWasPurchase = true;
                await _store.SaveInstanceAsync(context, apiClient, instance);
            }

            // 5. Drop from the active index (removing an absent id is a no-op).
            await RemoveFromIndexAsync(context, apiClient, journal.ListingId);

            var result = new BuyResult
            {
                Success = true,
                ListingId = journal.ListingId,
                PricePaidCredits = journal.PriceCredits,
                SellerReceivedCredits = journal.SellerReceivesCredits,
                TaxBurnedCredits = journal.TaxBurnCredits,
                TaxTreasuryCredits = journal.TaxTreasuryCredits,
            };

            await _store.SaveIdempotentBuyResultAsync(context, apiClient, journal.BuyerId, idempotencyKey, result);

            journal.Phase = SettlementJournal.PhaseCompleted;
            try
            {
                await _store.SaveSettlementJournalAsync(context, apiClient, journal.BuyerId, idempotencyKey, journal);
            }
            catch (BazaarStorageException)
            {
                // The idempotent result is already durable, so replays are served from it; a stale
                // InProgress journal is harmless (a resume finds every step already applied).
            }

            return result;
        }
        catch (BazaarStorageException exception)
        {
            // Journal stays InProgress: a retry with the same key resumes and completes the sale.
            return new BuyResult { ErrorCode = exception.ErrorCode };
        }
    }

    /// <summary>Recovery entry point used by <see cref="BazaarReconciliationSweep"/>: resumes a stuck
    /// InProgress journal through the exact same idempotent saga path a buyer's own retry takes, so
    /// recovery can never diverge from (or double-apply relative to) a normal retry.</summary>
    internal Task<BuyResult> ResumeSettlementAsync(IExecutionContext context, IGameApiClient apiClient, SettlementJournal journal, string idempotencyKey)
        => ExecuteSettlementAsync(context, apiClient, journal, idempotencyKey);

    private async Task<BuyResult> AbortSettlementAsync(IExecutionContext context, IGameApiClient apiClient, SettlementJournal journal, string idempotencyKey, string failureCode)
    {
        journal.Phase = SettlementJournal.PhaseAborted;
        journal.FailureCode = failureCode;
        try
        {
            await _store.SaveSettlementJournalAsync(context, apiClient, journal.BuyerId, idempotencyKey, journal);
        }
        catch (BazaarStorageException)
        {
            // Best effort: with no aborted marker a retry re-derives the same failure from state.
        }

        return new BuyResult { ErrorCode = failureCode };
    }

    public async Task<CancelListingResult> CancelListingAsync(IExecutionContext context, IGameApiClient apiClient, CancelListingRequest request)
    {
        if (context == null || string.IsNullOrWhiteSpace(context.PlayerId))
        {
            return new CancelListingResult { ErrorCode = "AUTHENTICATION_REQUIRED" };
        }

        if (request == null || string.IsNullOrWhiteSpace(request.ListingId))
        {
            return new CancelListingResult { ErrorCode = "INVALID_REQUEST" };
        }

        var listing = await _store.LoadListingAsync(context, apiClient, request.ListingId);
        if (listing == null || listing.State != BazaarListingState.Active)
        {
            return new CancelListingResult { ErrorCode = "LISTING_NOT_AVAILABLE" };
        }

        if (listing.SellerId != context.PlayerId)
        {
            return new CancelListingResult { ErrorCode = "NOT_SELLER" };
        }

        var instance = await _store.LoadInstanceAsync(context, apiClient, listing.InstanceId);
        if (instance == null)
        {
            return new CancelListingResult { ErrorCode = "INSTANCE_NOT_FOUND" };
        }

        instance.State = ItemInstanceState.Owned;
        listing.State = BazaarListingState.Cancelled;

        try
        {
            await _store.SaveInstanceAsync(context, apiClient, instance);
            await _store.SaveListingAsync(context, apiClient, listing);
            await RemoveFromIndexAsync(context, apiClient, listing.ListingId);
        }
        catch (BazaarStorageException exception)
        {
            return new CancelListingResult { ErrorCode = exception.ErrorCode };
        }

        return new CancelListingResult { Success = true };
    }

    /// <summary>Browse the active-listing board, batch-loading full listings for one page of the
    /// shared id index. PageToken is just the (string-encoded) offset into that index - the index
    /// is a simple ordered list, not a real cursor-stable store, so a listing added/removed by
    /// another caller between two pages can shift what a given token returns. Acceptable for a
    /// beta browse screen; not a durable pagination contract.</summary>
    public async Task<ListingsQueryResult> QueryListingsAsync(IExecutionContext context, IGameApiClient apiClient, QueryListingsRequest request)
    {
        if (context == null || string.IsNullOrWhiteSpace(context.PlayerId))
        {
            return new ListingsQueryResult { ErrorCode = "AUTHENTICATION_REQUIRED" };
        }

        int pageSize = request?.PageSize ?? 20;
        if (pageSize <= 0 || pageSize > 100)
        {
            return new ListingsQueryResult { ErrorCode = "INVALID_REQUEST" };
        }

        int offset = 0;
        if (!string.IsNullOrEmpty(request?.PageToken) && !int.TryParse(request!.PageToken, out offset))
        {
            return new ListingsQueryResult { ErrorCode = "INVALID_REQUEST" };
        }

        try
        {
            var index = await _store.LoadIndexAsync(context, apiClient);
            List<string> pageIds = index.ActiveListingIds.Skip(offset).Take(pageSize).ToList();
            IReadOnlyList<BazaarListing> listings = await _store.LoadListingsBatchAsync(context, apiClient, pageIds);

            var summaries = listings
                .Where(listing => listing.State == BazaarListingState.Active)
                .OrderByDescending(listing => listing.CreatedUtcMs)
                .Select(listing => new BazaarListingSummary
                {
                    ListingId = listing.ListingId,
                    InstanceId = listing.InstanceId,
                    SellerId = listing.SellerId,
                    AskCredits = listing.AskCredits,
                    CreatedUtcMs = listing.CreatedUtcMs,
                })
                .ToList();

            bool hasMore = offset + pageIds.Count < index.ActiveListingIds.Count;
            return new ListingsQueryResult
            {
                Success = true,
                Listings = summaries,
                NextPageToken = hasMore ? (offset + pageIds.Count).ToString() : null,
            };
        }
        catch (BazaarStorageException exception)
        {
            return new ListingsQueryResult { ErrorCode = exception.ErrorCode };
        }
    }

    /// <summary>Best-effort index bookkeeping: the listing/instance/wallet writes above are the
    /// operation's real source of truth and have already succeeded by the time this runs, so a
    /// failure here (e.g. exhausted conflict retries under heavy concurrent listing activity) is
    /// swallowed rather than surfaced as an operation failure - it can only make that one listing
    /// briefly miss QueryListings, never corrupt the listing/instance/wallet state itself.</summary>
    private async Task AddToIndexAsync(IExecutionContext context, IGameApiClient apiClient, string listingId)
    {
        for (int attempt = 0; attempt <= MaxConflictReconciliations; attempt++)
        {
            try
            {
                var index = await _store.LoadIndexAsync(context, apiClient);
                if (!index.ActiveListingIds.Contains(listingId))
                {
                    index.ActiveListingIds.Add(listingId);
                    await _store.SaveIndexAsync(context, apiClient, index);
                }

                return;
            }
            catch (BazaarStorageException exception) when (exception.ErrorCode == "CONFLICT" && attempt < MaxConflictReconciliations)
            {
            }
            catch (BazaarStorageException)
            {
                return;
            }
        }
    }

    private async Task RemoveFromIndexAsync(IExecutionContext context, IGameApiClient apiClient, string listingId)
    {
        for (int attempt = 0; attempt <= MaxConflictReconciliations; attempt++)
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
            catch (BazaarStorageException exception) when (exception.ErrorCode == "CONFLICT" && attempt < MaxConflictReconciliations)
            {
            }
            catch (BazaarStorageException)
            {
                return;
            }
        }
    }

    public async Task<WalletResult> GetWalletAsync(IExecutionContext context, IGameApiClient apiClient)
    {
        if (context == null || string.IsNullOrWhiteSpace(context.PlayerId))
        {
            return new WalletResult { ErrorCode = "AUTHENTICATION_REQUIRED" };
        }

        var wallet = await _store.LoadWalletAsync(context, apiClient, context.PlayerId);
        return new WalletResult { BalanceCredits = wallet.BalanceCredits };
    }

    private static int CountRecentSales(WalletState wallet, long now, long windowMs)
    {
        int count = 0;
        foreach (long saleUtcMs in wallet.RecentSaleUtcMs)
        {
            if (now - saleUtcMs < windowMs)
            {
                count++;
            }
        }

        return count;
    }
}

public sealed class BazaarModule
{
    private readonly BazaarOperations _operations;

    public BazaarModule()
        : this(new CloudSaveBazaarStore())
    {
    }

    internal BazaarModule(IBazaarStore store, IBazaarClock? clock = null, IBazaarRulesConfiguration? rules = null)
    {
        _operations = new BazaarOperations(store, clock ?? new SystemBazaarClock(), rules ?? new RemoteConfigBazaarRulesConfiguration());
    }

    [CloudCodeFunction("ListBazaarItem")]
    public Task<ListingResult> ListBazaarItem(IExecutionContext context, IGameApiClient apiClient, ListItemRequest request)
    {
        return _operations.ListItemAsync(context, apiClient, request);
    }

    [CloudCodeFunction("BuyBazaarItem")]
    public Task<BuyResult> BuyBazaarItem(IExecutionContext context, IGameApiClient apiClient, BuyItemRequest request)
    {
        return _operations.BuyItemAsync(context, apiClient, request);
    }

    [CloudCodeFunction("CancelBazaarListing")]
    public Task<CancelListingResult> CancelBazaarListing(IExecutionContext context, IGameApiClient apiClient, CancelListingRequest request)
    {
        return _operations.CancelListingAsync(context, apiClient, request);
    }

    [CloudCodeFunction("GetBazaarWallet")]
    public Task<WalletResult> GetBazaarWallet(IExecutionContext context, IGameApiClient apiClient)
    {
        return _operations.GetWalletAsync(context, apiClient);
    }

    [CloudCodeFunction("QueryBazaarListings")]
    public Task<ListingsQueryResult> QueryBazaarListings(IExecutionContext context, IGameApiClient apiClient, QueryListingsRequest request)
    {
        return _operations.QueryListingsAsync(context, apiClient, request);
    }
}

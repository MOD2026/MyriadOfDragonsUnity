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
    /// This scaffold saves each of the three entities (buyer wallet, seller wallet, instance +
    /// listing) with its own optimistic-lock conflict retry, but does NOT provide true
    /// all-or-nothing atomicity across all of them - §5 of the system packet lists "atomic
    /// database transactions and escrow" as a still-open backend dependency, not something this
    /// module invents a workaround for. What this scaffold DOES guarantee: idempotency - a
    /// retried call with the same (buyer, idempotencyKey) always returns the original outcome
    /// rather than re-debiting, so a dropped response can't be paid for twice by the client
    /// retrying.
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
        int taxTreasury = tax - taxBurn;
        int sellerReceives = listing.AskCredits - tax;

        buyerWallet.BalanceCredits -= listing.AskCredits;
        sellerWallet.BalanceCredits += sellerReceives;
        sellerWallet.RecentSaleUtcMs.Add(now);

        instance.OwnerId = buyerId;
        instance.State = ItemInstanceState.Owned;
        instance.LastAcquiredUtcMs = now;
        instance.LastAcquisitionWasPurchase = true;

        listing.State = BazaarListingState.Sold;

        try
        {
            await _store.SaveWalletAsync(context, apiClient, buyerWallet);
            await _store.SaveWalletAsync(context, apiClient, sellerWallet);
            await _store.SaveInstanceAsync(context, apiClient, instance);
            await _store.SaveListingAsync(context, apiClient, listing);
            await RemoveFromIndexAsync(context, apiClient, listing.ListingId);
        }
        catch (BazaarStorageException exception)
        {
            return new BuyResult { ErrorCode = exception.ErrorCode };
        }

        var result = new BuyResult
        {
            Success = true,
            ListingId = listing.ListingId,
            PricePaidCredits = listing.AskCredits,
            SellerReceivedCredits = sellerReceives,
            TaxBurnedCredits = taxBurn,
            TaxTreasuryCredits = taxTreasury,
        };

        await _store.SaveIdempotentBuyResultAsync(context, apiClient, buyerId, request.IdempotencyKey, result);
        return result;
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

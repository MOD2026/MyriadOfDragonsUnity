using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MyriadOfDragons.CloudCode.Bazaar;

public enum ItemInstanceState
{
    Owned = 0,
    Listed = 1,
}

/// <summary>One server-instanced tradeable card copy. PHASE1_BAZAAR_SYSTEM_PACKET_2026-08-23.md
/// §1/§2: only rarity 1-4 with an immutable `Tradeable` provenance may ever be listed; Gold/Gem
/// shop-pack output is Soulbound (Tradeable = false) so it can never reach this catalogue. This
/// scaffold does not model where a Tradeable instance actually comes from - see README "no mint
/// endpoint" - tests construct instances directly, the same way a future real mint service would
/// hand this module a pre-existing instance to trade.</summary>
public sealed class ItemInstance
{
    [JsonProperty("instanceId")]
    public string InstanceId { get; set; } = string.Empty;
    [JsonProperty("definitionId")]
    public string DefinitionId { get; set; } = string.Empty;
    [JsonProperty("ownerId")]
    public string OwnerId { get; set; } = string.Empty;
    [JsonProperty("rarity")]
    public int Rarity { get; set; }
    [JsonProperty("tradeable")]
    public bool Tradeable { get; set; }
    [JsonProperty("state")]
    public ItemInstanceState State { get; set; } = ItemInstanceState.Owned;
    /// <summary>When the current owner most recently acquired this instance - from mint, or from
    /// a Bazaar purchase (see <see cref="LastAcquisitionWasPurchase"/>). Determines which hold
    /// (§3: 7-day acquisition hold from mint, 72-hour relist hold after a purchase) gates the
    /// next listing.</summary>
    [JsonProperty("lastAcquiredUtcMs")]
    public long LastAcquiredUtcMs { get; set; }
    [JsonProperty("lastAcquisitionWasPurchase")]
    public bool LastAcquisitionWasPurchase { get; set; }

    [JsonIgnore]
    public string? WriteLock { get; set; }
}

public enum BazaarListingState
{
    Active = 0,
    Sold = 1,
    Cancelled = 2,
}

public sealed class BazaarListing
{
    [JsonProperty("listingId")]
    public string ListingId { get; set; } = string.Empty;
    [JsonProperty("instanceId")]
    public string InstanceId { get; set; } = string.Empty;
    [JsonProperty("sellerId")]
    public string SellerId { get; set; } = string.Empty;
    [JsonProperty("askCredits")]
    public int AskCredits { get; set; }
    [JsonProperty("state")]
    public BazaarListingState State { get; set; } = BazaarListingState.Active;
    [JsonProperty("createdUtcMs")]
    public long CreatedUtcMs { get; set; }
    /// <summary>Set (in the same single-entity write that flips the listing to Sold) to the id of
    /// the settlement that claimed it, so a resumed settlement can tell "sold by ME" from "sold to
    /// someone else" without any cross-entity transaction.</summary>
    [JsonProperty("settlementId", NullValueHandling = NullValueHandling.Ignore)]
    public string? SettlementId { get; set; }

    [JsonIgnore]
    public string? WriteLock { get; set; }
}

/// <summary>One account's Market Credit balance plus enough recent-sale history to enforce §3's
/// "maximum five completed sales per rolling seven days per account" - the wallet ledger itself
/// (append-only transaction history) is not fully modeled here; only what the business rules need
/// to function is kept, per the same "core loop first" scoping as the other holds/limits.</summary>
public sealed class WalletState
{
    [JsonProperty("accountId")]
    public string AccountId { get; set; } = string.Empty;
    [JsonProperty("balanceCredits")]
    public int BalanceCredits { get; set; }
    [JsonProperty("recentSaleUtcMs")]
    public List<long> RecentSaleUtcMs { get; set; } = new();
    /// <summary>Settlement ids already applied to this balance (bounded, most recent last). Written
    /// in the SAME single-entity save as the balance change, so applying a settlement's debit/credit
    /// is idempotent even if the process dies between the write and the journal update.</summary>
    [JsonProperty("appliedSettlementIds", NullValueHandling = NullValueHandling.Ignore)]
    public List<string> AppliedSettlementIds { get; set; } = new();

    [JsonIgnore]
    public string? WriteLock { get; set; }
}

public sealed class ListingResult
{
    [JsonProperty("success")]
    public bool Success { get; set; }
    [JsonProperty("listingId")]
    public string? ListingId { get; set; }
    [JsonProperty("goldFeeDue")]
    public int GoldFeeDue { get; set; }
    [JsonProperty("errorCode")]
    public string? ErrorCode { get; set; }
}

public sealed class BuyResult
{
    [JsonProperty("success")]
    public bool Success { get; set; }
    [JsonProperty("listingId")]
    public string? ListingId { get; set; }
    [JsonProperty("pricePaidCredits")]
    public int PricePaidCredits { get; set; }
    [JsonProperty("sellerReceivedCredits")]
    public int SellerReceivedCredits { get; set; }
    [JsonProperty("taxBurnedCredits")]
    public int TaxBurnedCredits { get; set; }
    [JsonProperty("taxTreasuryCredits")]
    public int TaxTreasuryCredits { get; set; }
    [JsonProperty("errorCode")]
    public string? ErrorCode { get; set; }
}

/// <summary>Durable settlement intent, saved BEFORE the first mutation of a purchase. Cloud Save has
/// no cross-entity transaction, so BuyItemAsync is a resumable forward-recovery saga: every step is
/// individually idempotent, and a retry with the same (buyer, idempotencyKey) resumes from this
/// record instead of starting a second, independent purchase. Amounts are frozen here so a resume
/// never recomputes against changed rules or wallets.</summary>
public sealed class SettlementJournal
{
    public const string PhaseInProgress = "InProgress";
    public const string PhaseAborted = "Aborted";
    public const string PhaseCompleted = "Completed";

    [JsonProperty("settlementId")]
    public string SettlementId { get; set; } = string.Empty;
    [JsonProperty("phase")]
    public string Phase { get; set; } = PhaseInProgress;
    [JsonProperty("buyerId")]
    public string BuyerId { get; set; } = string.Empty;
    [JsonProperty("sellerId")]
    public string SellerId { get; set; } = string.Empty;
    [JsonProperty("listingId")]
    public string ListingId { get; set; } = string.Empty;
    [JsonProperty("instanceId")]
    public string InstanceId { get; set; } = string.Empty;
    [JsonProperty("priceCredits")]
    public int PriceCredits { get; set; }
    [JsonProperty("sellerReceivesCredits")]
    public int SellerReceivesCredits { get; set; }
    [JsonProperty("taxBurnCredits")]
    public int TaxBurnCredits { get; set; }
    [JsonProperty("taxTreasuryCredits")]
    public int TaxTreasuryCredits { get; set; }
    [JsonProperty("startedUtcMs")]
    public long StartedUtcMs { get; set; }
    [JsonProperty("failureCode", NullValueHandling = NullValueHandling.Ignore)]
    public string? FailureCode { get; set; }
}

public sealed class CancelListingResult
{
    [JsonProperty("success")]
    public bool Success { get; set; }
    [JsonProperty("errorCode")]
    public string? ErrorCode { get; set; }
}

public sealed class WalletResult
{
    [JsonProperty("balanceCredits")]
    public int BalanceCredits { get; set; }
    [JsonProperty("errorCode")]
    public string? ErrorCode { get; set; }
}

/// <summary>Shared, single-key index of every currently-active listing id, so QueryListings can
/// find listings without a real Custom Items query/filter capability (see CloudSaveBazaarStore's
/// own doc comment on this limitation). Updated alongside the listing itself inside the same
/// optimistic-lock retry loop every time a listing becomes/stops being Active.</summary>
public sealed class BazaarListingIndex
{
    [JsonProperty("activeListingIds")]
    public List<string> ActiveListingIds { get; set; } = new();

    [JsonIgnore]
    public string? WriteLock { get; set; }
}

public sealed class QueryListingsRequest
{
    public int PageSize { get; set; } = 20;
    public string? PageToken { get; set; }
}

public sealed class BazaarListingSummary
{
    [JsonProperty("listingId")]
    public string ListingId { get; set; } = string.Empty;
    [JsonProperty("instanceId")]
    public string InstanceId { get; set; } = string.Empty;
    [JsonProperty("sellerId")]
    public string SellerId { get; set; } = string.Empty;
    [JsonProperty("askCredits")]
    public int AskCredits { get; set; }
    [JsonProperty("createdUtcMs")]
    public long CreatedUtcMs { get; set; }
}

public sealed class ListingsQueryResult
{
    [JsonProperty("success")]
    public bool Success { get; set; }
    [JsonProperty("listings")]
    public List<BazaarListingSummary> Listings { get; set; } = new();
    [JsonProperty("nextPageToken")]
    public string? NextPageToken { get; set; }
    [JsonProperty("errorCode")]
    public string? ErrorCode { get; set; }
}

public sealed class ListItemRequest
{
    public string InstanceId { get; set; } = string.Empty;
    public int AskCredits { get; set; }
}

public sealed class BuyItemRequest
{
    public string ListingId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class CancelListingRequest
{
    public string ListingId { get; set; } = string.Empty;
}

public sealed class BazaarStorageException : Exception
{
    public BazaarStorageException(string errorCode, Exception? innerException = null)
        : base("Bazaar storage failed.", innerException)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}

public interface IBazaarClock
{
    long UtcNowMs { get; }
}

public sealed class SystemBazaarClock : IBazaarClock
{
    public long UtcNowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

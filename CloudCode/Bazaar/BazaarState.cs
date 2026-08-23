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

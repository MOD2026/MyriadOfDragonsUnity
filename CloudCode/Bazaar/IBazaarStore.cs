using System.Threading.Tasks;

namespace MyriadOfDragons.CloudCode.Bazaar;

/// <summary>
/// Storage abstraction for Bazaar state. Deliberately NOT implemented against a concrete Unity
/// Cloud Save backend in this scaffold - see README "Deferred: persistence backend". Wallets and
/// item instances are naturally per-account data (the same player-scoped Cloud Save pattern
/// SocialSafety/PermitWeekKey/GuildExpedition already use and this scaffold could reuse with
/// confidence), but a <see cref="BazaarListing"/> must be visible to every prospective buyer, not
/// just its seller - that needs shared/cross-account storage (Cloud Save's Custom/non-player data
/// scope, or an external database), and this scaffold does not select or verify that API surface
/// against the real SDK. Getting an unverified API call wrong would silently produce a scaffold
/// that fails to build the moment someone actually deploys it - worse than being honest that this
/// piece isn't decided yet. `Bazaar.ServerTests` exercises the full business logic (fees, tax
/// math, holds, self-trade rejection, idempotency) against an in-memory reference implementation
/// of this same interface instead.
/// </summary>
public interface IBazaarStore
{
    Task<ItemInstance?> LoadInstanceAsync(string instanceId);
    Task SaveInstanceAsync(ItemInstance instance);

    Task<BazaarListing?> LoadListingAsync(string listingId);
    Task SaveListingAsync(BazaarListing listing);

    Task<WalletState> LoadWalletAsync(string accountId);
    Task SaveWalletAsync(WalletState wallet);

    /// <summary>Returns the stored result for (buyerId, idempotencyKey) if this exact buy was
    /// already processed, so a retried client call after a dropped response returns the original
    /// outcome instead of attempting the purchase again.</summary>
    Task<BuyResult?> TryGetIdempotentBuyResultAsync(string buyerId, string idempotencyKey);
    Task SaveIdempotentBuyResultAsync(string buyerId, string idempotencyKey, BuyResult result);
}

using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace MyriadOfDragons.CloudCode.Bazaar;

/// <summary>
/// Storage abstraction for Bazaar state. <see cref="CloudSaveBazaarStore"/> is the real
/// implementation - see its own doc comment for the Game Data / Custom Items design.
/// `Bazaar.ServerTests` additionally exercises the full business logic against an in-memory
/// reference implementation of this same interface, so operations logic is verified independent
/// of the real Cloud Save wiring.
/// </summary>
public interface IBazaarStore
{
    Task<ItemInstance?> LoadInstanceAsync(IExecutionContext context, IGameApiClient apiClient, string instanceId);
    Task SaveInstanceAsync(IExecutionContext context, IGameApiClient apiClient, ItemInstance instance);

    Task<BazaarListing?> LoadListingAsync(IExecutionContext context, IGameApiClient apiClient, string listingId);
    Task SaveListingAsync(IExecutionContext context, IGameApiClient apiClient, BazaarListing listing);

    /// <summary>Loads the wallet for <paramref name="accountId"/> - NOT necessarily
    /// <c>context.PlayerId</c>. A purchase must credit the seller's wallet too, and Cloud Code
    /// runs with server authority precisely so it can read/write another account's player-scoped
    /// data on the caller's behalf for a trusted transaction like this one.</summary>
    Task<WalletState> LoadWalletAsync(IExecutionContext context, IGameApiClient apiClient, string accountId);
    Task SaveWalletAsync(IExecutionContext context, IGameApiClient apiClient, WalletState wallet);

    /// <summary>Returns the stored result for (buyerId, idempotencyKey) if this exact buy was
    /// already processed, so a retried client call after a dropped response returns the original
    /// outcome instead of attempting the purchase again.</summary>
    Task<BuyResult?> TryGetIdempotentBuyResultAsync(IExecutionContext context, IGameApiClient apiClient, string buyerId, string idempotencyKey);
    Task SaveIdempotentBuyResultAsync(IExecutionContext context, IGameApiClient apiClient, string buyerId, string idempotencyKey, BuyResult result);
}

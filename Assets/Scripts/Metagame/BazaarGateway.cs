using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;

namespace MyriadOfDragons.Metagame
{
    [Serializable]
    public sealed class BazaarListingResult
    {
        public bool success;
        public string listingId;
        public int goldFeeDue;
        public string errorCode;
    }

    [Serializable]
    public sealed class BazaarBuyResult
    {
        public bool success;
        public string listingId;
        public int pricePaidCredits;
        public int sellerReceivedCredits;
        public int taxBurnedCredits;
        public int taxTreasuryCredits;
        public string errorCode;
    }

    [Serializable]
    public sealed class BazaarCancelResult
    {
        public bool success;
        public string errorCode;
    }

    [Serializable]
    public sealed class BazaarWalletResult
    {
        public int balanceCredits;
        public string errorCode;
    }

    [Serializable]
    public sealed class BazaarListingSummaryDto
    {
        public string listingId;
        public string instanceId;
        public string sellerId;
        public int askCredits;
        public long createdUtcMs;
    }

    [Serializable]
    public sealed class BazaarListingsQueryResult
    {
        public bool success;
        public List<BazaarListingSummaryDto> listings;
        public string nextPageToken;
        public string errorCode;
    }

    /// <summary>Real client gateway for the live, deployed Bazaar CloudCode module
    /// (nonprod-validation, see docs/LOCKED_DECISIONS_REGISTER.md). GetWallet is fully usable now;
    /// ListItem/BuyItem/CancelListing are real and correct but have nothing to actually list yet -
    /// the module has no endpoint to create an ItemInstance (that's the Collection system's job,
    /// not built here) - see CloudCode/Bazaar's own README "Deferred" section. Request shape
    /// matches the module's real parameter name ("request", wrapping each call's fields).</summary>
    public interface IBazaarGateway
    {
        Task<BazaarWalletResult> GetWalletAsync(CancellationToken cancellationToken);
        Task<BazaarListingResult> ListItemAsync(string instanceId, int askCredits, CancellationToken cancellationToken);
        Task<BazaarBuyResult> BuyItemAsync(string listingId, string idempotencyKey, CancellationToken cancellationToken);
        Task<BazaarCancelResult> CancelListingAsync(string listingId, CancellationToken cancellationToken);
        Task<BazaarListingsQueryResult> QueryListingsAsync(int pageSize, string pageToken, CancellationToken cancellationToken);
    }

    public sealed class UnityCloudCodeBazaarGateway : IBazaarGateway
    {
        private const string ModuleName = "Bazaar";

        public async Task<BazaarWalletResult> GetWalletAsync(CancellationToken cancellationToken)
        {
            await EnsureSignedInAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return await CloudCodeService.Instance.CallModuleEndpointAsync<BazaarWalletResult>(
                ModuleName, "GetBazaarWallet", new Dictionary<string, object>()).ConfigureAwait(false);
        }

        public async Task<BazaarListingResult> ListItemAsync(string instanceId, int askCredits, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(instanceId) || askCredits <= 0)
                return new BazaarListingResult { errorCode = "INVALID_REQUEST" };

            await EnsureSignedInAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return await CloudCodeService.Instance.CallModuleEndpointAsync<BazaarListingResult>(
                ModuleName, "ListBazaarItem",
                new Dictionary<string, object> { { "request", new Dictionary<string, object> { { "instanceId", instanceId }, { "askCredits", askCredits } } } }).ConfigureAwait(false);
        }

        public async Task<BazaarBuyResult> BuyItemAsync(string listingId, string idempotencyKey, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(listingId) || string.IsNullOrWhiteSpace(idempotencyKey))
                return new BazaarBuyResult { errorCode = "INVALID_REQUEST" };

            await EnsureSignedInAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return await CloudCodeService.Instance.CallModuleEndpointAsync<BazaarBuyResult>(
                ModuleName, "BuyBazaarItem",
                new Dictionary<string, object> { { "request", new Dictionary<string, object> { { "listingId", listingId }, { "idempotencyKey", idempotencyKey } } } }).ConfigureAwait(false);
        }

        public async Task<BazaarCancelResult> CancelListingAsync(string listingId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(listingId))
                return new BazaarCancelResult { errorCode = "INVALID_REQUEST" };

            await EnsureSignedInAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return await CloudCodeService.Instance.CallModuleEndpointAsync<BazaarCancelResult>(
                ModuleName, "CancelBazaarListing",
                new Dictionary<string, object> { { "request", new Dictionary<string, object> { { "listingId", listingId } } } }).ConfigureAwait(false);
        }

        public async Task<BazaarListingsQueryResult> QueryListingsAsync(int pageSize, string pageToken, CancellationToken cancellationToken)
        {
            await EnsureSignedInAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var requestFields = new Dictionary<string, object> { { "pageSize", pageSize } };
            if (!string.IsNullOrEmpty(pageToken))
            {
                requestFields["pageToken"] = pageToken;
            }

            return await CloudCodeService.Instance.CallModuleEndpointAsync<BazaarListingsQueryResult>(
                ModuleName, "QueryBazaarListings",
                new Dictionary<string, object> { { "request", requestFields } }).ConfigureAwait(false);
        }

        private static async Task EnsureSignedInAsync(CancellationToken cancellationToken)
        {
            await UnityServices.InitializeAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync().ConfigureAwait(false);
            }
        }
    }
}

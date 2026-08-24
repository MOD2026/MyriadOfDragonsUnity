using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine.TestTools;

namespace MyriadOfDragons.Tests.PlayMode
{
    /// <summary>
    /// One-off live validation against the deployed Bazaar CloudCode module. Requires the
    /// nonprod-validation environment. This module has no endpoint to create an ItemInstance
    /// (that's owned by the Collection system, not this scaffold), so the full list/buy happy path
    /// can't be exercised from here - this validates wallet reads and the not-found/validation
    /// paths, which is real coverage of the storage + request-shape wiring without needing seed
    /// data this module can't create for itself.
    /// </summary>
    public sealed class BazaarLiveValidationPlayModeTests
    {
        [UnityTest]
        public IEnumerator LiveValidation_RunsAgainstDeployedModule()
        {
            var task = RunAsync();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                throw task.Exception ?? new Exception("Live validation task faulted with no exception.");
            }

            var (results, passCount) = task.Result;

            foreach (var (check, pass, detail) in results)
            {
                UnityEngine.Debug.Log($"[{(pass ? "PASS" : "FAIL")}] {check}{(string.IsNullOrEmpty(detail) ? "" : " - " + detail)}");
            }
            UnityEngine.Debug.Log($"===== {passCount}/{results.Count} passed =====");

            Assert.AreEqual(results.Count, passCount, $"{results.Count - passCount} live validation check(s) failed - see log above.");
        }

        private static async Task<(List<(string, bool, string)> results, int passCount)> RunAsync()
        {
            var results = new List<(string, bool, string)>();

            var options = new InitializationOptions();
            options.SetEnvironmentName("nonprod-validation");
            await UnityServices.InitializeAsync(options);
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            UnityEngine.Debug.Log($"Signed in as: {AuthenticationService.Instance.PlayerId}");

            await CheckAsync(results, "GetBazaarWallet returns a clean state, no error", async () =>
            {
                var r = await CallNoRequestAsync<WalletResponse>("GetBazaarWallet");
                return r != null && string.IsNullOrEmpty(r.errorCode) && r.balanceCredits >= 0;
            });

            await CheckAsync(results, "ListBazaarItem rejects invalid AskCredits", async () =>
            {
                var r = await CallAsync<ListingResponse>("ListBazaarItem",
                    new Dictionary<string, object> { { "instanceId", "livevalidation-instance" }, { "askCredits", 0 } });
                return r != null && r.errorCode == "INVALID_REQUEST";
            });

            await CheckAsync(results, "ListBazaarItem reports INSTANCE_NOT_FOUND for a nonexistent instance", async () =>
            {
                var r = await CallAsync<ListingResponse>("ListBazaarItem",
                    new Dictionary<string, object> { { "instanceId", "livevalidation-instance-" + Guid.NewGuid().ToString("N") }, { "askCredits", 100 } });
                return r != null && r.errorCode == "INSTANCE_NOT_FOUND";
            });

            await CheckAsync(results, "BuyBazaarItem reports LISTING_NOT_AVAILABLE for a nonexistent listing", async () =>
            {
                var r = await CallAsync<BuyResponse>("BuyBazaarItem",
                    new Dictionary<string, object> { { "listingId", "livevalidation-listing-" + Guid.NewGuid().ToString("N") }, { "idempotencyKey", Guid.NewGuid().ToString("N") } });
                return r != null && r.errorCode == "LISTING_NOT_AVAILABLE";
            });

            await CheckAsync(results, "CancelBazaarListing reports LISTING_NOT_AVAILABLE for a nonexistent listing", async () =>
            {
                var r = await CallAsync<CancelResponse>("CancelBazaarListing",
                    new Dictionary<string, object> { { "listingId", "livevalidation-listing-" + Guid.NewGuid().ToString("N") } });
                return r != null && r.errorCode == "LISTING_NOT_AVAILABLE";
            });

            await CheckAsync(results, "CancelBazaarListing rejects a blank listingId", async () =>
            {
                var r = await CallAsync<CancelResponse>("CancelBazaarListing",
                    new Dictionary<string, object> { { "listingId", "" } });
                return r != null && r.errorCode == "INVALID_REQUEST";
            });

            int passCount = 0;
            foreach (var (_, pass, _) in results)
            {
                if (pass) passCount++;
            }

            return (results, passCount);
        }

        private static async Task CheckAsync(List<(string, bool, string)> results, string name, Func<Task<bool>> check)
        {
            try
            {
                bool pass = await check();
                results.Add((name, pass, null));
            }
            catch (Exception exception)
            {
                results.Add((name, false, exception.Message));
            }
        }

        private static async Task<T> CallNoRequestAsync<T>(string functionName)
        {
            return await CloudCodeService.Instance.CallModuleEndpointAsync<T>("Bazaar", functionName, new Dictionary<string, object>());
        }

        private static async Task<T> CallAsync<T>(string functionName, Dictionary<string, object> requestFields)
        {
            return await CloudCodeService.Instance.CallModuleEndpointAsync<T>(
                "Bazaar",
                functionName,
                new Dictionary<string, object> { { "request", requestFields } });
        }

        [Serializable]
        private sealed class WalletResponse
        {
            public int balanceCredits;
            public string errorCode;
        }

        [Serializable]
        private sealed class ListingResponse
        {
            public bool success;
            public string listingId;
            public int goldFeeDue;
            public string errorCode;
        }

        [Serializable]
        private sealed class BuyResponse
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
        private sealed class CancelResponse
        {
            public bool success;
            public string errorCode;
        }
    }
}

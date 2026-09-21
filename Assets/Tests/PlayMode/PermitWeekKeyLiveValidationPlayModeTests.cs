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
    /// One-off live validation against the deployed PermitWeekKey CloudCode module. Requires the
    /// nonprod-validation environment. Real network calls, not part of the regular EditMode suite.
    /// </summary>
    public sealed class PermitWeekKeyLiveValidationPlayModeTests
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
            // The server allowlists activityId (PermitWeekKeyOperations.DefaultAllowedActivityIds); a random id is now
            // rejected as UNKNOWN_ACTIVITY (checked below), so the happy path uses the one shipped activity.
            string activityId = "ascensionPermit.weekly";
            bool playerAlreadyClaimedThisWeek = false;

            PermitStatusResponse? firstStatus = null;
            await CheckAsync(results, "GetPermitStatus before claim", async () =>
            {
                var r = await CallAsync<PermitStatusResponse>("GetPermitStatus", activityId);
                firstStatus = r;
                playerAlreadyClaimedThisWeek = r != null && r.claimedThisWeek;
                return r != null && string.IsNullOrEmpty(r.errorCode);
            });

            PermitClaimResponse? firstClaim = null;
            await CheckAsync(results, "ClaimWeeklyPermit first claim grants a nonzero amount", async () =>
            {
                var r = await CallAsync<PermitClaimResponse>("ClaimWeeklyPermit", activityId);
                firstClaim = r;
                // A fresh anonymous player grants; a cached session that already claimed this week must report it.
                return r != null && r.success && (playerAlreadyClaimedThisWeek ? (r.alreadyClaimed && r.granted == 0) : (!r.alreadyClaimed && r.granted > 0));
            });

            await CheckAsync(results, "ClaimWeeklyPermit second claim same week is idempotent (no double grant)", async () =>
            {
                var r = await CallAsync<PermitClaimResponse>("ClaimWeeklyPermit", activityId);
                return r != null && r.success && r.alreadyClaimed && r.granted == 0
                       && firstClaim != null && r.balance == firstClaim.balance;
            });

            await CheckAsync(results, "GetPermitStatus after claim reflects claimedThisWeek", async () =>
            {
                var r = await CallAsync<PermitStatusResponse>("GetPermitStatus", activityId);
                return r != null && string.IsNullOrEmpty(r.errorCode) && r.claimedThisWeek
                       && firstClaim != null && r.balance == firstClaim.balance;
            });

            await CheckAsync(results, "ClaimWeeklyPermit with a client-invented activityId is rejected (no minting via new ids)", async () =>
            {
                var r = await CallAsync<PermitClaimResponse>("ClaimWeeklyPermit", "livevalidation-activity-" + Guid.NewGuid().ToString("N"));
                return r != null && !r.success && r.errorCode == "UNKNOWN_ACTIVITY";
            });

            await CheckAsync(results, "GetPermitStatus with blank activityId is rejected", async () =>
            {
                var r = await CallAsync<PermitStatusResponse>("GetPermitStatus", "");
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

        private static async Task<T> CallAsync<T>(string functionName, string activityId)
        {
            var response = await CloudCodeService.Instance.CallModuleEndpointAsync<T>(
                "PermitWeekKey",
                functionName,
                new Dictionary<string, object> { { "request", new Dictionary<string, object> { { "activityId", activityId } } } });
            return response;
        }

        [Serializable]
        private sealed class PermitStatusResponse
        {
            public int balance;
            public string currentWeekKey;
            public bool claimedThisWeek;
            public int weeklyRate;
            public int hoardCap;
            public string errorCode;
        }

        [Serializable]
        private sealed class PermitClaimResponse
        {
            public bool success;
            public int granted;
            public int balance;
            public string weekKey;
            public bool alreadyClaimed;
            public string errorCode;
        }
    }
}

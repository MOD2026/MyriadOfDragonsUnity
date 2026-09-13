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
    /// One-off live validation against the deployed Friends CloudCode module. The client only has
    /// one real identity per anonymous sign-in in this environment, so the request/accept happy
    /// path (which needs two distinct accounts) can't be exercised from here - this validates the
    /// real deployed request/response shape and every single-account validation path (self-target,
    /// blank target, not-found-on-respond), matching BazaarLiveValidationPlayModeTests' own scope
    /// discipline for the same reason.
    /// </summary>
    public sealed class FriendsLiveValidationPlayModeTests
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

            await CheckAsync(results, "ListFriends returns success with no error for a fresh account", async () =>
            {
                var r = await CallAsync<ListFriendsResponse>("ListFriends", new Dictionary<string, object>());
                return r != null && r.success && string.IsNullOrEmpty(r.errorCode);
            });

            await CheckAsync(results, "AddFriend rejects a blank targetAccountId", async () =>
            {
                var r = await CallAsync<FriendResponse>("AddFriend", new Dictionary<string, object> { { "targetAccountId", "" } });
                return r != null && r.errorCode == "INVALID_REQUEST";
            });

            await CheckAsync(results, "AddFriend rejects self as target", async () =>
            {
                var r = await CallAsync<FriendResponse>("AddFriend", new Dictionary<string, object> { { "targetAccountId", AuthenticationService.Instance.PlayerId } });
                return r != null && r.errorCode == "SELF_TARGET_NOT_ALLOWED";
            });

            await CheckAsync(results, "AcceptFriend reports REQUEST_NOT_FOUND for a nonexistent relationship", async () =>
            {
                var r = await CallAsync<FriendResponse>("AcceptFriend",
                    new Dictionary<string, object> { { "targetAccountId", "livevalidation-target-" + Guid.NewGuid().ToString("N") } });
                return r != null && r.errorCode == "REQUEST_NOT_FOUND";
            });

            await CheckAsync(results, "RemoveFriend reports REQUEST_NOT_FOUND for a nonexistent relationship", async () =>
            {
                var r = await CallAsync<FriendResponse>("RemoveFriend",
                    new Dictionary<string, object> { { "targetAccountId", "livevalidation-target-" + Guid.NewGuid().ToString("N") } });
                return r != null && r.errorCode == "REQUEST_NOT_FOUND";
            });

            await CheckAsync(results, "SendDailyGift reports NOT_FRIENDS for a nonexistent relationship", async () =>
            {
                var r = await CallAsync<GiftResponse>("SendDailyGift",
                    new Dictionary<string, object> { { "targetAccountId", "livevalidation-target-" + Guid.NewGuid().ToString("N") } });
                return r != null && r.errorCode == "NOT_FRIENDS";
            });

            await CheckAsync(results, "AddFriend to a real new target succeeds and appears Pending in ListFriends", async () =>
            {
                string target = "livevalidation-target-" + Guid.NewGuid().ToString("N");
                var add = await CallAsync<FriendResponse>("AddFriend", new Dictionary<string, object> { { "targetAccountId", target } });
                UnityEngine.Debug.Log($"DIAG AddFriend: add==null={add == null} success={add?.success} status={add?.status} errorCode={add?.errorCode}");
                if (add == null || !add.success || add.status != "Pending") return false;

                var list = await CallAsync<ListFriendsResponse>("ListFriends", new Dictionary<string, object>());
                bool found = false;
                if (list?.friends != null)
                {
                    foreach (var friend in list.friends)
                    {
                        if (friend.counterpartAliasId == target && friend.isOutgoingRequest) found = true;
                    }
                }

                // Clean up so repeated runs of this test don't accumulate stale pending requests.
                await CallAsync<FriendResponse>("RemoveFriend", new Dictionary<string, object> { { "targetAccountId", target } });
                return found;
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

        private static async Task<T> CallAsync<T>(string functionName, Dictionary<string, object> requestFields)
        {
            return await CloudCodeService.Instance.CallModuleEndpointAsync<T>(
                "Friends",
                functionName,
                new Dictionary<string, object> { { "request", requestFields } });
        }

        [Serializable]
        private sealed class FriendResponse
        {
            public bool success;
            public string status;
            public string errorCode;
        }

        [Serializable]
        private sealed class FriendSummaryResponse
        {
            public string counterpartAliasId;
            public string status;
            public bool isOutgoingRequest;
            public bool canGiftToday;
            public long createdUtcMs;
        }

        [Serializable]
        private sealed class ListFriendsResponse
        {
            public bool success;
            public List<FriendSummaryResponse> friends;
            public string errorCode;
        }

        [Serializable]
        private sealed class GiftResponse
        {
            public bool success;
            public string errorCode;
        }
    }
}

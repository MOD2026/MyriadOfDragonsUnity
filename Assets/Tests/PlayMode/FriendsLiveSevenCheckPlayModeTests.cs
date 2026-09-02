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
    /// TASK BE-FRIENDS-LIVE-007: the 7 real friend/gift checks, run against the deployed Friends
    /// module in nonprod-validation using two genuinely distinct authenticated player identities
    /// (obtained the same way FriendsBackfillLiveExecutionPlayModeTests does - sign in, record
    /// PlayerId, SignOut(true), sign in again). FriendsLiveValidationPlayModeTests could only cover
    /// single-account/error paths for this exact reason ("client only has one real identity per
    /// anonymous sign-in"); this test exercises the real two-account happy path end to end.
    /// </summary>
    public sealed class FriendsLiveSevenCheckPlayModeTests
    {
        [UnityTest]
        public IEnumerator RunSevenChecksWithTwoRealPlayerIds()
        {
            var task = RunAsync();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                throw task.Exception ?? new Exception("Seven-check live run faulted with no exception.");
            }

            var (results, passCount) = task.Result;
            foreach (var (check, pass, detail) in results)
            {
                UnityEngine.Debug.Log($"[{(pass ? "PASS" : "FAIL")}] {check}{(string.IsNullOrEmpty(detail) ? "" : " - " + detail)}");
            }
            UnityEngine.Debug.Log($"===== {passCount}/{results.Count} passed =====");

            Assert.AreEqual(results.Count, passCount, $"{results.Count - passCount} check(s) failed - see log above.");
        }

        private static async Task<(List<(string, bool, string)> results, int passCount)> RunAsync()
        {
            var results = new List<(string, bool, string)>();

            var options = new InitializationOptions();
            options.SetEnvironmentName("nonprod-validation");
            await UnityServices.InitializeAsync(options);

            if (AuthenticationService.Instance.IsSignedIn)
            {
                AuthenticationService.Instance.SignOut(true);
            }

            // Distinct, independently-resumable identities: SwitchProfile gives each a separate
            // cached anonymous session. SignOut(false) (no credential clear) between switches
            // preserves each profile's session so signing back into "a" RESUMES playerIdA rather
            // than minting a third identity - unlike the forward-only SignOut(true) cycle
            // FriendsBackfillLiveExecutionPlayModeTests uses, this test needs real bidirectional
            // switching (act as A, then B, then A again) to exercise the two-account happy path.
            AuthenticationService.Instance.SwitchProfile("be-live-007-a");
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            string playerIdA = AuthenticationService.Instance.PlayerId;
            UnityEngine.Debug.Log($"EVIDENCE playerIdA={playerIdA}");

            AuthenticationService.Instance.SignOut(false);
            AuthenticationService.Instance.SwitchProfile("be-live-007-b");
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            string playerIdB = AuthenticationService.Instance.PlayerId;
            UnityEngine.Debug.Log($"EVIDENCE playerIdB={playerIdB}");

            if (string.IsNullOrEmpty(playerIdA) || string.IsNullOrEmpty(playerIdB) || playerIdA == playerIdB)
            {
                results.Add(("Obtain two distinct real player ids", false, $"A={playerIdA} B={playerIdB}"));
                return (results, 0);
            }

            UnityEngine.Debug.Log($"EVIDENCE TIMESTAMP_UTC={DateTime.UtcNow:O}");
            UnityEngine.Debug.Log("EVIDENCE ENVIRONMENT=nonprod-validation");

            // --- Check 1: AddFriend, signed in as A targeting B ---
            await SwitchToAsync(playerIdA, playerIdB, isA: true);
            var add = await CallAsync<FriendResponse>("AddFriend", new Dictionary<string, object> { { "targetAccountId", playerIdB } });
            LogRaw("Check1_AddFriend_A_to_B", add);
            await CheckAsync(results, "1. AddFriend(A->B) succeeds with status Pending",
                () => Task.FromResult(add != null && add.success && add.status == "Pending"));

            // --- Check 2: ListFriends as B shows the incoming pending request via alias ---
            await SwitchToAsync(playerIdA, playerIdB, isA: false);
            var listB1 = await CallAsync<ListFriendsResponse>("ListFriends", new Dictionary<string, object>());
            LogRaw("Check2_ListFriends_B_seesIncoming", listB1);
            string aliasOfAFromB = FindAlias(listB1);
            await CheckAsync(results, "2. ListFriends(B) shows incoming pending request with a non-empty alias",
                () => Task.FromResult(listB1 != null && listB1.success && !string.IsNullOrEmpty(aliasOfAFromB) && FindIsOutgoing(listB1) == false));

            // --- Check 3: AcceptFriend, signed in as B targeting A ---
            var accept = await CallAsync<FriendResponse>("AcceptFriend", new Dictionary<string, object> { { "targetAccountId", playerIdA } });
            LogRaw("Check3_AcceptFriend_B_accepts_A", accept);
            await CheckAsync(results, "3. AcceptFriend(B accepts A) succeeds with status Accepted",
                () => Task.FromResult(accept != null && accept.success && accept.status == "Accepted"));

            // --- Check 4: ListFriends as A shows accepted friend via alias ---
            await SwitchToAsync(playerIdA, playerIdB, isA: true);
            var listA1 = await CallAsync<ListFriendsResponse>("ListFriends", new Dictionary<string, object>());
            LogRaw("Check4_ListFriends_A_seesAccepted", listA1);
            await CheckAsync(results, "4. ListFriends(A) shows the friend as Accepted with a non-empty alias",
                () => Task.FromResult(listA1 != null && listA1.success && FindStatus(listA1) == "Accepted" && !string.IsNullOrEmpty(FindAlias(listA1))));

            // --- Check 5: SendDailyGift, A -> B, first time today succeeds ---
            var gift1 = await CallAsync<GiftResponse>("SendDailyGift", new Dictionary<string, object> { { "targetAccountId", playerIdB } });
            LogRaw("Check5_SendDailyGift_A_to_B_first", gift1);
            await CheckAsync(results, "5. SendDailyGift(A->B) first call today succeeds",
                () => Task.FromResult(gift1 != null && gift1.success));

            // --- Check 6: SendDailyGift again same day is rejected GIFT_ALREADY_SENT_TODAY ---
            var gift2 = await CallAsync<GiftResponse>("SendDailyGift", new Dictionary<string, object> { { "targetAccountId", playerIdB } });
            LogRaw("Check6_SendDailyGift_A_to_B_second", gift2);
            await CheckAsync(results, "6. SendDailyGift(A->B) second call same day reports GIFT_ALREADY_SENT_TODAY",
                () => Task.FromResult(gift2 != null && !gift2.success && gift2.errorCode == "GIFT_ALREADY_SENT_TODAY"));

            // --- Check 7: RemoveFriend, A removes B, then ListFriends(A) no longer shows B ---
            var remove = await CallAsync<FriendResponse>("RemoveFriend", new Dictionary<string, object> { { "targetAccountId", playerIdB } });
            LogRaw("Check7_RemoveFriend_A_removes_B", remove);
            var listA2 = await CallAsync<ListFriendsResponse>("ListFriends", new Dictionary<string, object>());
            LogRaw("Check7_ListFriends_A_afterRemove", listA2);
            await CheckAsync(results, "7. RemoveFriend(A removes B) succeeds and B no longer appears in ListFriends(A)",
                () => Task.FromResult(remove != null && remove.success && (listA2 == null || listA2.friends == null || listA2.friends.Count == 0)));

            int passCount = 0;
            foreach (var (_, pass, _) in results)
            {
                if (pass) passCount++;
            }
            return (results, passCount);
        }

        private static async Task SwitchToAsync(string playerIdA, string playerIdB, bool isA)
        {
            string target = isA ? playerIdA : playerIdB;
            if (AuthenticationService.Instance.PlayerId == target)
            {
                return;
            }

            // Resume the other profile's cached session (created earlier this run with
            // SwitchProfile + SignInAnonymouslyAsync, preserved via SignOut(false)) rather than
            // minting a new identity - real switch, not a no-op.
            AuthenticationService.Instance.SignOut(false);
            AuthenticationService.Instance.SwitchProfile(isA ? "be-live-007-a" : "be-live-007-b");
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

            if (AuthenticationService.Instance.PlayerId != target)
            {
                throw new InvalidOperationException(
                    $"Profile switch did not resume the expected identity: expected={target} actual={AuthenticationService.Instance.PlayerId}");
            }
        }

        private static string FindAlias(ListFriendsResponse response) =>
            response?.friends != null && response.friends.Count > 0 ? response.friends[0].counterpartAliasId : null;

        private static string FindStatus(ListFriendsResponse response) =>
            response?.friends != null && response.friends.Count > 0 ? response.friends[0].status : null;

        private static bool? FindIsOutgoing(ListFriendsResponse response) =>
            response?.friends != null && response.friends.Count > 0 ? response.friends[0].isOutgoingRequest : (bool?)null;

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

        private static void LogRaw(string label, object response)
        {
            UnityEngine.Debug.Log($"EVIDENCE RAW {label} = {JsonUtilitySafeDump(response)}");
        }

        private static string JsonUtilitySafeDump(object response)
        {
            if (response == null)
            {
                return "null";
            }
            try
            {
                return UnityEngine.JsonUtility.ToJson(response);
            }
            catch (Exception exception)
            {
                return $"(dump failed: {exception.Message})";
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

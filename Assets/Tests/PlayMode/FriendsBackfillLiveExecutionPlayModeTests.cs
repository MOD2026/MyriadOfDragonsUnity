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
    /// TASK BE-FRIENDS-EXECUTION-013: real, authenticated-player invocation of the deployed
    /// BackfillFriendsAliasReverseIndex function against nonprod-validation - via the actual Unity
    /// Authentication + CloudCode client SDK, not the admin ugs CLI (which has no invoke
    /// capability, confirmed in the prior task's report).
    ///
    /// Two distinct real PlayerIds are obtained SEQUENTIALLY in one PlayMode run: sign in
    /// anonymously (real identity A), record its PlayerId, sign out with credential clearing, sign
    /// in anonymously again (Unity Authentication mints a genuinely new anonymous identity once
    /// the cached session is cleared - real identity B), record its PlayerId. Both are real,
    /// live-registered player accounts in nonprod-validation, not invented strings.
    ///
    /// This is a ONE-OFF LIVE EXECUTION TEST, not a permanent regression test - it exists to
    /// capture real evidence for this task, matching FriendsLiveValidationPlayModeTests.cs's own
    /// established "one-off live validation" precedent and scope discipline.
    /// </summary>
    public sealed class FriendsBackfillLiveExecutionPlayModeTests
    {
        [UnityTest]
        public IEnumerator RunBackfillTwiceWithTwoRealPlayerIds()
        {
            var task = RunAsync();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                throw task.Exception ?? new Exception("Live backfill execution task faulted with no exception.");
            }
        }

        private static async Task RunAsync()
        {
            var options = new InitializationOptions();
            options.SetEnvironmentName("nonprod-validation");
            await UnityServices.InitializeAsync(options);

            // --- Real identity A ---
            if (AuthenticationService.Instance.IsSignedIn)
            {
                AuthenticationService.Instance.SignOut(true);
            }
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            string playerIdA = AuthenticationService.Instance.PlayerId;
            UnityEngine.Debug.Log($"EVIDENCE playerIdA={playerIdA}");

            // --- Real identity B: clear the cached session so a genuinely new anonymous identity is minted ---
            AuthenticationService.Instance.SignOut(true);
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            string playerIdB = AuthenticationService.Instance.PlayerId;
            UnityEngine.Debug.Log($"EVIDENCE playerIdB={playerIdB}");

            if (string.IsNullOrEmpty(playerIdA) || string.IsNullOrEmpty(playerIdB) || playerIdA == playerIdB)
            {
                UnityEngine.Debug.Log($"EVIDENCE FAILURE: could not obtain two distinct real player ids (A={playerIdA}, B={playerIdB}).");
                Assert.Fail("Could not obtain two distinct real player ids from Unity Authentication in this environment.");
                return;
            }

            var accountIds = new List<object> { playerIdA, playerIdB };
            var requestFields = new Dictionary<string, object> { { "accountIds", accountIds } };

            // First attempt (see this file's git history / prior run log) proved the real wire
            // shape is camelCase with the full field set: a strict-deserializer error on an
            // earlier, incomplete mirror class reported "Could not find member 'errors'" - real,
            // live confirmation the server actually returned a well-formed response containing
            // that key. This mirror now declares every field the real server type has.
            var run1 = await CallAsync<BackfillResultCamel>(requestFields);
            LogEvidence("RUN1", run1);

            var run2 = await CallAsync<BackfillResultCamel>(requestFields);
            LogEvidence("RUN2", run2);

            UnityEngine.Debug.Log($"EVIDENCE TIMESTAMP_UTC={DateTime.UtcNow:O}");
            UnityEngine.Debug.Log("EVIDENCE ENVIRONMENT=nonprod-validation");

            Assert.AreEqual(0, run2.aliasesUpdated, "Second identical backfill run must report aliasesUpdated: 0 (no-op). See EVIDENCE RUN2 log lines above for the full real response.");
        }

        private static async Task<T> CallAsync<T>(Dictionary<string, object> requestFields)
        {
            return await CloudCodeService.Instance.CallModuleEndpointAsync<T>(
                "Friends",
                "BackfillFriendsAliasReverseIndex",
                new Dictionary<string, object> { { "request", requestFields } });
        }

        private static void LogEvidence(string label, BackfillResultCamel result)
        {
            string failedIds = result.failedAccountIds != null ? string.Join(",", result.failedAccountIds) : "";
            string errors = result.errors != null ? string.Join(" | ", result.errors) : "";
            UnityEngine.Debug.Log($"EVIDENCE {label} accountsScanned={result.accountsScanned} aliasesUpdated={result.aliasesUpdated} aliasesSkipped={result.aliasesSkipped} accountsFailed={result.accountsFailed} failedAccountIds=[{failedIds}] errors=[{errors}]");
        }

        [Serializable]
        private sealed class BackfillResultCamel
        {
            public int accountsScanned;
            public int aliasesUpdated;
            public int aliasesSkipped;
            public int accountsFailed;
            public List<string> failedAccountIds;
            public List<string> errors;
        }
    }
}

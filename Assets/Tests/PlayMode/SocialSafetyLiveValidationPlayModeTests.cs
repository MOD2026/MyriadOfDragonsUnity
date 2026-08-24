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
    /// One-off live validation against the deployed SocialSafety CloudCode module. Requires the
    /// Editor's active Services environment to be nonprod-validation. Real network calls, not part
    /// of the regular EditMode regression suite - run explicitly, not via run_editmode_tests.ps1.
    /// </summary>
    public sealed class SocialSafetyLiveValidationPlayModeTests
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

            string selfId = AuthenticationService.Instance.PlayerId;
            UnityEngine.Debug.Log($"Signed in as account A: {selfId}");
            string targetA = "livevalidation-target-" + Guid.NewGuid().ToString("N");

            await CheckAsync(results, "Block", async () =>
            {
                var r = await CallAsync("BlockAccount", targetA);
                return r.success && r.changed;
            });

            await CheckAsync(results, "Duplicate block idempotent", async () =>
            {
                var r = await CallAsync("BlockAccount", targetA);
                return r.success && !r.changed;
            });

            await CheckAsync(results, "Unblock", async () =>
            {
                var r = await CallAsync("UnblockAccount", targetA);
                return r.success && r.changed;
            });

            await CheckAsync(results, "Repeated unblock reports no additional change", async () =>
            {
                var r = await CallAsync("UnblockAccount", targetA);
                return r.success && !r.changed;
            });

            string targetB = "livevalidation-target-" + Guid.NewGuid().ToString("N");

            await CheckAsync(results, "Mute", async () =>
            {
                var r = await CallAsync("MuteAccount", targetB);
                return r.success && r.changed;
            });

            await CheckAsync(results, "Duplicate mute idempotent", async () =>
            {
                var r = await CallAsync("MuteAccount", targetB);
                return r.success && !r.changed;
            });

            await CheckAsync(results, "Unmute", async () =>
            {
                var r = await CallAsync("UnmuteAccount", targetB);
                return r.success && r.changed;
            });

            await CheckAsync(results, "Repeated unmute reports no additional change", async () =>
            {
                var r = await CallAsync("UnmuteAccount", targetB);
                return r.success && !r.changed;
            });

            await CheckAsync(results, "Self-target request is rejected", async () =>
            {
                var r = await CallAsync("BlockAccount", selfId);
                return !r.success && r.errorCode == "SELF_TARGET_NOT_ALLOWED";
            });

            await CheckAsync(results, "Blank target request is rejected", async () =>
            {
                var r = await CallAsync("BlockAccount", "");
                return !r.success;
            });

            await CheckAsync(results, "Rate-limit exhaustion returns RATE_LIMITED", async () =>
            {
                bool sawRateLimited = false;
                for (int i = 0; i < 15; i++)
                {
                    string t = "livevalidation-ratelimit-" + i;
                    var r = await CallAsync("BlockAccount", t);
                    if (!r.success && r.errorCode == "RATE_LIMITED")
                    {
                        sawRateLimited = true;
                        break;
                    }
                }
                return sawRateLimited;
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

        private static async Task<(bool success, bool changed, string errorCode)> CallAsync(string functionName, string targetAccountId)
        {
            try
            {
                var response = await CloudCodeService.Instance.CallModuleEndpointAsync<Response>(
                    "SocialSafety",
                    functionName,
                    new Dictionary<string, object> { { "request", new Dictionary<string, object> { { "targetAccountId", targetAccountId } } } });
                UnityEngine.Debug.Log($"RAW {functionName}({targetAccountId}) -> success={response.success} changed={response.changed} errorCode={response.errorCode}");
                return (response.success, response.changed, response.errorCode);
            }
            catch (CloudCodeException exception)
            {
                UnityEngine.Debug.Log($"RAW {functionName}({targetAccountId}) -> CloudCodeException code={exception.ErrorCode} message={exception.Message}");
                return (false, false, exception.ErrorCode.ToString());
            }
        }

        [Serializable]
        private sealed class Response
        {
            public bool success;
            public object relationship;
            public bool changed;
            public string errorCode;
        }
    }
}

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
    /// One-off live validation against the deployed GuildExpedition CloudCode module. Requires the
    /// nonprod-validation environment. This account's week-scoped state persists between test
    /// runs, so assertions focus on idempotency (two calls in a row are consistent) rather than
    /// assuming a fresh "first ever call" state.
    /// </summary>
    public sealed class GuildExpeditionLiveValidationPlayModeTests
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

            await CheckAsync(results, "ConsumeExpeditionAttempt returns a valid outcome", async () =>
            {
                var r = await CallNoRequestAsync<AttemptResponse>("ConsumeExpeditionAttempt");
                return r != null && (r.errorCode == "NO_ATTEMPTS_REMAINING" || (r.success && r.remaining >= 0));
            });

            ObjectiveResponse? firstSubmit = null;
            await CheckAsync(results, "SubmitExpeditionObjectiveResult accepts a known objective", async () =>
            {
                var r = await CallAsync<ObjectiveResponse>("SubmitExpeditionObjectiveResult", "objectiveId", "scout.revealEnemyDeck");
                firstSubmit = r;
                return r != null && r.success && string.IsNullOrEmpty(r.errorCode);
            });

            await CheckAsync(results, "SubmitExpeditionObjectiveResult is idempotent (repeat call in a row is alreadyScored)", async () =>
            {
                var r = await CallAsync<ObjectiveResponse>("SubmitExpeditionObjectiveResult", "objectiveId", "scout.revealEnemyDeck");
                return r != null && r.success && r.alreadyScored && r.pointsAwarded == 0
                       && firstSubmit != null && r.totalPoints == firstSubmit.totalPoints;
            });

            await CheckAsync(results, "SubmitExpeditionObjectiveResult rejects an unknown objective", async () =>
            {
                var r = await CallAsync<ObjectiveResponse>("SubmitExpeditionObjectiveResult", "objectiveId", "not-a-real-objective");
                return r != null && r.errorCode == "UNKNOWN_OBJECTIVE";
            });

            // Score enough additional objectives to guarantee TotalPoints >= 100 before claiming
            // the lowest milestone band, regardless of what this account already had this week.
            await CheckAsync(results, "SubmitExpeditionObjectiveResult (second objective, to clear the 100 milestone)", async () =>
            {
                var r = await CallAsync<ObjectiveResponse>("SubmitExpeditionObjectiveResult", "objectiveId", "scout.winWithInfoHandicap");
                return r != null && string.IsNullOrEmpty(r.errorCode) && r.totalPoints >= 100;
            });

            MilestoneResponse? firstClaim = null;
            await CheckAsync(results, "ClaimExpeditionMilestone accepts a reached threshold", async () =>
            {
                var r = await CallMilestoneAsync(100);
                firstClaim = r;
                return r != null && r.success && string.IsNullOrEmpty(r.errorCode);
            });

            await CheckAsync(results, "ClaimExpeditionMilestone is idempotent (repeat call in a row is alreadyClaimed)", async () =>
            {
                var r = await CallMilestoneAsync(100);
                return r != null && r.success && r.alreadyClaimed;
            });

            await CheckAsync(results, "ClaimExpeditionMilestone rejects an invalid threshold", async () =>
            {
                var r = await CallMilestoneAsync(999);
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
            return await CloudCodeService.Instance.CallModuleEndpointAsync<T>("GuildExpedition", functionName, new Dictionary<string, object>());
        }

        private static async Task<T> CallAsync<T>(string functionName, string fieldName, string fieldValue)
        {
            return await CloudCodeService.Instance.CallModuleEndpointAsync<T>(
                "GuildExpedition",
                functionName,
                new Dictionary<string, object> { { "request", new Dictionary<string, object> { { fieldName, fieldValue } } } });
        }

        private static async Task<MilestoneResponse> CallMilestoneAsync(int threshold)
        {
            return await CloudCodeService.Instance.CallModuleEndpointAsync<MilestoneResponse>(
                "GuildExpedition",
                "ClaimExpeditionMilestone",
                new Dictionary<string, object> { { "request", new Dictionary<string, object> { { "threshold", threshold } } } });
        }

        [Serializable]
        private sealed class AttemptResponse
        {
            public bool success;
            public int remaining;
            public string errorCode;
        }

        [Serializable]
        private sealed class ObjectiveResponse
        {
            public bool success;
            public int pointsAwarded;
            public int totalPoints;
            public string weekKey;
            public bool alreadyScored;
            public string errorCode;
        }

        [Serializable]
        private sealed class MilestoneResponse
        {
            public bool success;
            public int threshold;
            public int guildContributionGranted;
            public bool alreadyClaimed;
            public string errorCode;
        }
    }
}

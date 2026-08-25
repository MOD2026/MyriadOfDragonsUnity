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
    /// <summary>One-off live validation against the deployed Chat CloudCode module. Posts real
    /// messages to a run-unique channel (so repeated runs never see stale history from a prior
    /// run) and reads them back, proving the full post -> history round trip against the real
    /// deployed environment, not mocked.</summary>
    public sealed class ChatLiveValidationPlayModeTests
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
            string channelId = "livevalidation-" + Guid.NewGuid().ToString("N");

            var options = new InitializationOptions();
            options.SetEnvironmentName("nonprod-validation");
            await UnityServices.InitializeAsync(options);
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            UnityEngine.Debug.Log($"Signed in as: {AuthenticationService.Instance.PlayerId}");

            await CheckAsync(results, "PostChatMessage rejects a blank channelId", async () =>
            {
                var r = await CallAsync<PostResponse>("PostChatMessage", new Dictionary<string, object> { { "channelId", "" }, { "text", "hi" } });
                return r != null && r.errorCode == "INVALID_REQUEST";
            });

            await CheckAsync(results, "PostChatMessage rejects blank text", async () =>
            {
                var r = await CallAsync<PostResponse>("PostChatMessage", new Dictionary<string, object> { { "channelId", channelId }, { "text", "  " } });
                return r != null && r.errorCode == "INVALID_REQUEST";
            });

            await CheckAsync(results, "FetchChatHistory on a brand-new channel returns success with no messages", async () =>
            {
                var r = await CallAsync<HistoryResponse>("FetchChatHistory", new Dictionary<string, object> { { "channelId", channelId }, { "limit", 20 } });
                return r != null && r.success && (r.messages == null || r.messages.Count == 0);
            });

            string firstMessageId = null;
            await CheckAsync(results, "PostChatMessage to the run-unique channel succeeds", async () =>
            {
                var r = await CallAsync<PostResponse>("PostChatMessage", new Dictionary<string, object> { { "channelId", channelId }, { "text", "live validation message 1" } });
                firstMessageId = r?.messageId;
                UnityEngine.Debug.Log($"DIAG PostChatMessage: r==null={r == null} success={r?.success} messageId={r?.messageId} errorCode={r?.errorCode}");
                return r != null && r.success && !string.IsNullOrEmpty(r.messageId);
            });

            await CallAsync<PostResponse>("PostChatMessage", new Dictionary<string, object> { { "channelId", channelId }, { "text", "live validation message 2" } });

            await CheckAsync(results, "FetchChatHistory returns both posted messages, most recent first", async () =>
            {
                var r = await CallAsync<HistoryResponse>("FetchChatHistory", new Dictionary<string, object> { { "channelId", channelId }, { "limit", 20 } });
                if (r == null || !r.success || r.messages == null || r.messages.Count != 2) return false;
                return r.messages[0].text == "live validation message 2" && r.messages[1].text == "live validation message 1"
                    && r.messages[1].id == firstMessageId
                    && r.messages[0].senderAccountId == AuthenticationService.Instance.PlayerId;
            });

            await CheckAsync(results, "FetchChatHistory respects a smaller limit", async () =>
            {
                var r = await CallAsync<HistoryResponse>("FetchChatHistory", new Dictionary<string, object> { { "channelId", channelId }, { "limit", 1 } });
                return r != null && r.success && r.messages != null && r.messages.Count == 1 && r.messages[0].text == "live validation message 2";
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
                "Chat",
                functionName,
                new Dictionary<string, object> { { "request", requestFields } });
        }

        [Serializable]
        private sealed class PostResponse
        {
            public bool success;
            public string messageId;
            public string errorCode;
        }

        [Serializable]
        private sealed class MessageResponse
        {
            public string id;
            public string senderAccountId;
            public string text;
            public long sentUtcMs;
        }

        [Serializable]
        private sealed class HistoryResponse
        {
            public bool success;
            public List<MessageResponse> messages;
            public string errorCode;
        }
    }
}

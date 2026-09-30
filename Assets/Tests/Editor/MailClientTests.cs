using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyriadOfDragons.Frontier;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>Beta Mail client (BE c2e7831b, WH-CC11-042) against the real, published server
    /// contract (CloudCode/Mail). Server is faked; nothing here asserts server behaviour. Covers
    /// loading, stale/offline cached read-only state, idempotent mark-read, conflict-mapped
    /// storage errors, expiry, and reconnect.</summary>
    public class MailClientTests
    {
        private sealed class FakeMailGateway : IMailGateway
        {
            public Func<string, Dictionary<string, object>, object> Handler;
            public readonly List<KeyValuePair<string, Dictionary<string, object>>> Calls =
                new List<KeyValuePair<string, Dictionary<string, object>>>();

            public Task<T> CallAsync<T>(string endpoint, Dictionary<string, object> request, CancellationToken ct)
                where T : MailResult
            {
                Calls.Add(new KeyValuePair<string, Dictionary<string, object>>(endpoint, request));
                object r = Handler(endpoint, request);
                if (r is Exception ex) throw ex;
                return Task.FromResult((T)r);
            }
        }

        private static MailSummaryDto Summary(string id, bool read = false, long expires = 0) =>
            new MailSummaryDto { messageId = id, kind = "System", subject = "subject", createdUtcMs = 1, expiresUtcMs = expires, read = read };

        [Test]
        public async Task RefreshInbox_Accepted_ExposesServerOwnedMessagesAndUnreadCountVerbatim()
        {
            var gw = new FakeMailGateway { Handler = (e, r) => new FetchMailInboxResult
            {
                success = true,
                messages = new[] { Summary("m1"), Summary("m2", read: true) },
                unreadCount = 1,
            } };
            var client = new MailClient(gw);
            Assert.IsTrue(await client.RefreshInboxAsync());
            Assert.AreEqual(MailInboxRefresh.Accepted, client.LastInboxRefresh);
            Assert.AreEqual(2, client.Inbox.messages.Length);
            Assert.AreEqual(1, client.UnreadCount);
            Assert.IsNull(gw.Calls.Single(c => c.Key == MailEndpoints.FetchMailInbox).Value, "zero-arg call - authenticated-player scope only, no id ever sent");
        }

        [Test]
        public async Task RefreshInbox_TransportFailure_KeepsLastGoodInbox_OfflineCachedReadOnly()
        {
            var gw = new FakeMailGateway { Handler = (e, r) => new FetchMailInboxResult { success = true, messages = new[] { Summary("m1") }, unreadCount = 1 } };
            var client = new MailClient(gw);
            await client.RefreshInboxAsync();

            gw.Handler = (e, r) => new InvalidOperationException("network");
            Assert.IsFalse(await client.RefreshInboxAsync());
            Assert.AreEqual(MailInboxRefresh.Offline, client.LastInboxRefresh);
            Assert.AreEqual(1, client.Inbox.messages.Length, "the cached inbox stays visible while offline");
            Assert.AreEqual(1, client.UnreadCount);
        }

        [Test]
        public async Task RefreshInbox_StorageUnavailable_KeepsLastGoodInbox()
        {
            var gw = new FakeMailGateway { Handler = (e, r) => new FetchMailInboxResult { success = true, messages = new[] { Summary("m1") }, unreadCount = 1 } };
            var client = new MailClient(gw);
            await client.RefreshInboxAsync();

            gw.Handler = (e, r) => new FetchMailInboxResult { success = false, errorCode = MailErrors.StorageUnavailable };
            Assert.IsFalse(await client.RefreshInboxAsync());
            Assert.AreEqual(1, client.Inbox.messages.Length);
        }

        [Test]
        public async Task FetchMailDetail_Accepted_ReturnsBodyVerbatim()
        {
            var gw = new FakeMailGateway { Handler = (e, r) =>
                e == MailEndpoints.FetchMailInbox ? (object)new FetchMailInboxResult { success = true }
                : new FetchMailDetailResult { success = true, message = new MailDetailDto { messageId = "m1", body = "hello" } } };
            var client = new MailClient(gw);
            await client.RefreshInboxAsync();

            FetchMailDetailResult result = await client.FetchMailDetailAsync("m1");
            Assert.IsTrue(result.success);
            Assert.AreEqual("hello", result.message.body);
            var request = gw.Calls.Single(c => c.Key == MailEndpoints.FetchMailDetail).Value;
            Assert.AreEqual("m1", request["messageId"]);
        }

        [Test]
        public async Task FetchMailDetail_Expired_ReturnsMailExpiredVerbatim_NeverComputedLocally()
        {
            var gw = new FakeMailGateway { Handler = (e, r) =>
                e == MailEndpoints.FetchMailInbox ? (object)new FetchMailInboxResult { success = true }
                : new FetchMailDetailResult { success = false, errorCode = MailErrors.MailExpired } };
            var client = new MailClient(gw);
            await client.RefreshInboxAsync();

            FetchMailDetailResult result = await client.FetchMailDetailAsync("expired");
            Assert.IsFalse(result.success);
            Assert.AreEqual(MailErrors.MailExpired, result.errorCode);
        }

        [Test]
        public async Task FetchMailDetail_Offline_FailsClosed_NeverSendsARequest()
        {
            var gw = new FakeMailGateway { Handler = (e, r) => throw new InvalidOperationException("must not be called while offline") };
            var client = new MailClient(gw);

            FetchMailDetailResult result = await client.FetchMailDetailAsync("m1");
            Assert.IsFalse(result.success);
            Assert.IsEmpty(gw.Calls, "offline must fail closed - there is no cached detail to fall back to");
        }

        [Test]
        public async Task FetchMailDetail_BlankMessageId_IsRejectedWithoutCallingTheGateway()
        {
            var gw = new FakeMailGateway { Handler = (e, r) => new FetchMailInboxResult { success = true } };
            var client = new MailClient(gw);
            await client.RefreshInboxAsync();

            FetchMailDetailResult result = await client.FetchMailDetailAsync(null);
            Assert.AreEqual(MailErrors.InvalidRequest, result.errorCode);
            Assert.IsFalse(gw.Calls.Any(c => c.Key == MailEndpoints.FetchMailDetail));
        }

        [Test]
        public async Task MarkMailRead_Success_ReloadsInboxForAuthoritativeUnreadCount()
        {
            int inboxCalls = 0;
            var gw = new FakeMailGateway { Handler = (e, r) =>
            {
                if (e == MailEndpoints.FetchMailInbox)
                {
                    inboxCalls++;
                    return new FetchMailInboxResult { success = true, unreadCount = inboxCalls == 1 ? 1 : 0 };
                }
                return new MarkMailReadResult { success = true, messageId = "m1" };
            } };
            var client = new MailClient(gw);
            await client.RefreshInboxAsync();
            Assert.AreEqual(1, client.UnreadCount);

            MailMarkReadOutcome outcome = await client.MarkMailReadAsync("m1");
            Assert.AreEqual(MailOutcome.Applied, outcome.Outcome);
            Assert.AreEqual(0, client.UnreadCount, "unreadCount is server-authoritative, never decremented locally");
            Assert.AreEqual(2, inboxCalls);
        }

        [Test]
        public async Task MarkMailRead_RepeatedCall_IsIdempotent_SurfacesAsAlreadyReadNotAnError()
        {
            var gw = new FakeMailGateway { Handler = (e, r) =>
                e == MailEndpoints.FetchMailInbox ? (object)new FetchMailInboxResult { success = true }
                : new MarkMailReadResult { success = true, messageId = "m1", alreadyRead = true } };
            var client = new MailClient(gw);
            await client.RefreshInboxAsync();

            MailMarkReadOutcome outcome = await client.MarkMailReadAsync("m1");
            Assert.AreEqual(MailOutcome.AlreadyRead, outcome.Outcome);
            Assert.IsTrue(outcome.Response.alreadyRead);
        }

        [Test]
        public async Task MarkMailRead_ConflictThenServerRetrySucceeds_ClientJustSeesTheFinalSuccess()
        {
            // The server itself retries its own CAS conflict once (MailOperations.MarkMailReadAsync);
            // this client never re-implements that retry - it only ever sees the final answer.
            var gw = new FakeMailGateway { Handler = (e, r) =>
                e == MailEndpoints.FetchMailInbox ? (object)new FetchMailInboxResult { success = true }
                : new MarkMailReadResult { success = true, messageId = "m1" } };
            var client = new MailClient(gw);
            await client.RefreshInboxAsync();

            MailMarkReadOutcome outcome = await client.MarkMailReadAsync("m1");
            Assert.AreEqual(MailOutcome.Applied, outcome.Outcome);
        }

        [Test]
        public async Task MarkMailRead_RateLimited_IsRejected()
        {
            var gw = new FakeMailGateway { Handler = (e, r) =>
                e == MailEndpoints.FetchMailInbox ? (object)new FetchMailInboxResult { success = true }
                : new MarkMailReadResult { success = false, errorCode = MailErrors.RateLimited } };
            var client = new MailClient(gw);
            await client.RefreshInboxAsync();

            MailMarkReadOutcome outcome = await client.MarkMailReadAsync("m1");
            Assert.AreEqual(MailOutcome.Rejected, outcome.Outcome);
            Assert.AreEqual(MailCopy.RateLimitedMessage, outcome.Message);
        }

        [Test]
        public async Task MarkMailRead_StorageUnavailable_IsRejected_NeverTreatedAsSuccess()
        {
            var gw = new FakeMailGateway { Handler = (e, r) =>
                e == MailEndpoints.FetchMailInbox ? (object)new FetchMailInboxResult { success = true }
                : new MarkMailReadResult { success = false, errorCode = MailErrors.StorageUnavailable } };
            var client = new MailClient(gw);
            await client.RefreshInboxAsync();

            MailMarkReadOutcome outcome = await client.MarkMailReadAsync("m1");
            Assert.AreEqual(MailOutcome.Rejected, outcome.Outcome);
        }

        [Test]
        public async Task MarkMailRead_MailExpired_IsRejected()
        {
            var gw = new FakeMailGateway { Handler = (e, r) =>
                e == MailEndpoints.FetchMailInbox ? (object)new FetchMailInboxResult { success = true }
                : new MarkMailReadResult { success = false, errorCode = MailErrors.MailExpired } };
            var client = new MailClient(gw);
            await client.RefreshInboxAsync();

            MailMarkReadOutcome outcome = await client.MarkMailReadAsync("expired");
            Assert.AreEqual(MailOutcome.Rejected, outcome.Outcome);
            Assert.AreEqual(MailCopy.MailExpiredMessage, outcome.Message);
        }

        [Test]
        public async Task MarkMailRead_Offline_FailsClosed_NeverSendsARequest()
        {
            var gw = new FakeMailGateway { Handler = (e, r) => throw new InvalidOperationException("must not be called while offline") };
            var client = new MailClient(gw);

            MailMarkReadOutcome outcome = await client.MarkMailReadAsync("m1");
            Assert.AreEqual(MailOutcome.Offline, outcome.Outcome);
            Assert.IsEmpty(gw.Calls, "offline must fail closed before any request is sent - never an optimistic local read flip");
        }

        [Test]
        public async Task MarkMailRead_BlankMessageId_IsRejectedWithoutCallingTheGateway()
        {
            var gw = new FakeMailGateway { Handler = (e, r) => new FetchMailInboxResult { success = true } };
            var client = new MailClient(gw);
            await client.RefreshInboxAsync();

            MailMarkReadOutcome outcome = await client.MarkMailReadAsync(null);
            Assert.AreEqual(MailOutcome.Rejected, outcome.Outcome);
            Assert.IsFalse(gw.Calls.Any(c => c.Key == MailEndpoints.MarkMailRead));
        }

        [Test]
        public async Task MarkMailRead_Reconnect_ReOpensAfterAnOfflineRejection()
        {
            var gw = new FakeMailGateway { Handler = (e, r) => throw new InvalidOperationException("offline") };
            var client = new MailClient(gw);
            MailMarkReadOutcome offlineOutcome = await client.MarkMailReadAsync("m1");
            Assert.AreEqual(MailOutcome.Offline, offlineOutcome.Outcome);

            gw.Handler = (e, r) => e == MailEndpoints.FetchMailInbox ? (object)new FetchMailInboxResult { success = true } : new MarkMailReadResult { success = true, messageId = "m1" };
            Assert.IsTrue(await client.RefreshInboxAsync());

            MailMarkReadOutcome outcome = await client.MarkMailReadAsync("m1");
            Assert.AreEqual(MailOutcome.Applied, outcome.Outcome);
        }

        [Test]
        public async Task RefreshInbox_TransportFailure_ThenReconnect_AcceptsFreshInbox()
        {
            var gw = new FakeMailGateway { Handler = (e, r) => new FetchMailInboxResult { success = true, messages = new[] { Summary("m1") }, unreadCount = 1 } };
            var client = new MailClient(gw);
            await client.RefreshInboxAsync();

            gw.Handler = (e, r) => new InvalidOperationException("network");
            await client.RefreshInboxAsync();
            Assert.AreEqual(Cc10Connection.Offline, client.Connection);

            gw.Handler = (e, r) => new FetchMailInboxResult { success = true, messages = new[] { Summary("m1", read: true) }, unreadCount = 0 };
            Assert.IsTrue(await client.RefreshInboxAsync());
            Assert.AreEqual(Cc10Connection.Online, client.Connection);
            Assert.AreEqual(0, client.UnreadCount);
        }

        [Test]
        public void MailResultBase_HasExactlyTheApprovedFields_NoServerUtcOrReceiptInvented()
        {
            // The real Mail wire result carries no serverUtcMs/stateVersion/authorityGeneration/
            // receipt (unlike CC10Frontier) - this guards against silently inventing one.
            CollectionAssert.AreEqual(new[] { "errorCode", "success" },
                typeof(MailResult).GetFields().Select(f => f.Name).OrderBy(n => n).ToArray());
        }

        [Test]
        public void MailDetailDto_HasNoAttachmentOrRewardFields()
        {
            string[] names = typeof(MailDetailDto).GetFields().Select(f => f.Name.ToLowerInvariant()).ToArray();
            Assert.IsFalse(names.Any(n => n.Contains("attachment") || n.Contains("reward") || n is "gold" or "materials" or "gems"));
        }
    }
}

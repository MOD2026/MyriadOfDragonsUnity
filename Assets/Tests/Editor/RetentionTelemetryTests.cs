using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MyriadOfDragons.Metagame;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Real tests for the retention-telemetry event pipeline (register: "Retention telemetry
    /// architecture - LOCKED, do NOT touch PlayerProfile" / "Retention telemetry: Unity Analytics
    /// as the sink"). RetentionTelemetryEvents is plain/pure (CLAUDE.md non-negotiable #6) and
    /// tested directly with no fakes needed. RetentionTelemetryOutbox is tested against a fake
    /// IRetentionTelemetryGateway (same pattern as FakeBazaarGateway/FakeChatSocialGateway) so
    /// its own real logic - bounded capacity, ordered flush, never-throws-into-caller,
    /// disk persistence - is exercised deterministically, not against the real network-backed
    /// gateway.
    /// </summary>
    public class RetentionTelemetryTests
    {
        private string _scratchDir;

        [SetUp]
        public void SetUp()
        {
            _scratchDir = Path.Combine(Path.GetTempPath(), "MoDRetentionTelemetry_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_scratchDir)) Directory.Delete(_scratchDir, recursive: true);
        }

        // ---------- RetentionTelemetryEvents: plain, pure, real ----------

        [Test]
        public void ModeRunCompleted_BuildsARealEvent_WithTheRightType()
        {
            RetentionTelemetryEvent evt = RetentionTelemetryEvents.ModeRunCompleted("player-1", "empire_expedition", "run-1", "success");
            Assert.AreEqual(RetentionTelemetryEvents.EventTypeModeRunCompleted, evt.eventType);
            Assert.AreEqual("mode_run_completed", evt.eventType);
            Assert.AreEqual("player-1", evt.playerId);
            Assert.AreEqual("empire_expedition", evt.mode);
            Assert.AreEqual("run-1", evt.runId);
            Assert.AreEqual("success", evt.outcome);
        }

        [Test]
        public void ModeRewardClaimed_BuildsARealEvent_WithTheRightType()
        {
            RetentionTelemetryEvent evt = RetentionTelemetryEvents.ModeRewardClaimed("player-1", "battle_pass", "run-2", "claimed");
            Assert.AreEqual("mode_reward_claimed", evt.eventType);
            Assert.AreEqual("run-2", evt.runId);
        }

        [Test]
        public void FeatureEntry_BuildsARealEvent_WithNoRunIdOrOutcome()
        {
            RetentionTelemetryEvent evt = RetentionTelemetryEvents.FeatureEntry("player-1", "guild_expedition");
            Assert.AreEqual("feature_entry", evt.eventType);
            Assert.IsNull(evt.runId, "A feature-entry event marks reaching a screen, not completing a run.");
            Assert.IsNull(evt.outcome);
        }

        [Test]
        public void DailyCapReached_BuildsARealEvent_WithNoRunId_ButRealOutcome()
        {
            RetentionTelemetryEvent evt = RetentionTelemetryEvents.DailyCapReached("player-1", "daily_login", "cap_hit");
            Assert.AreEqual("daily_cap_reached", evt.eventType);
            Assert.IsNull(evt.runId, "A daily-cap event isn't tied to one specific run.");
            Assert.AreEqual("cap_hit", evt.outcome);
        }

        [Test]
        public void EveryEvent_GetsAReal_NonEmpty_UniqueEventId()
        {
            RetentionTelemetryEvent a = RetentionTelemetryEvents.FeatureEntry("player-1", "mail_inbox");
            RetentionTelemetryEvent b = RetentionTelemetryEvents.FeatureEntry("player-1", "mail_inbox");
            Assert.IsFalse(string.IsNullOrEmpty(a.eventId));
            Assert.IsFalse(string.IsNullOrEmpty(b.eventId));
            Assert.AreNotEqual(a.eventId, b.eventId, "Two events must never share an idempotent dedup key.");
        }

        [Test]
        public void EveryEvent_CarriesTheRealSchemaVersion_AndARealRecentClientTimestamp()
        {
            long beforeUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            RetentionTelemetryEvent evt = RetentionTelemetryEvents.FeatureEntry("player-1", "vip_subscription");
            long afterUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            Assert.AreEqual(RetentionTelemetryEvents.SchemaVersion, evt.schemaVersion);
            Assert.IsNotNull(evt.appBuild);
            Assert.GreaterOrEqual(evt.clientOccurredAtUtc, beforeUtc);
            Assert.LessOrEqual(evt.clientOccurredAtUtc, afterUtc);
        }

        [Test]
        public void RetentionTelemetryEvent_NeverCarriesAServerReceivedTimestampField()
        {
            // Real design invariant, not just an omission: serverReceivedAtUtc is
            // server-authoritative (register's own clock-tampering concern - "server receipt
            // time is authoritative"). A client-settable field with that name would let a client
            // lie about when the server received an event. Reflection check so this fails loudly
            // if a future edit ever adds one back.
            var fields = typeof(RetentionTelemetryEvent).GetFields();
            foreach (var f in fields)
                StringAssert.DoesNotContain("serverReceivedAt", f.Name,
                    "RetentionTelemetryEvent must never carry a client-settable server-receipt-time field.");
        }

        // ---------- RetentionTelemetryOutbox: bounded, ordered, never blocks the caller ----------

        private sealed class FakeRetentionTelemetryGateway : IRetentionTelemetryGateway
        {
            public readonly List<string> SentEventIdsInOrder = new List<string>();
            public int FailAtCallNumber = -1; // -1 = never fail
            public bool ThrowInsteadOfFail;
            private int _callCount;

            public Task<RetentionTelemetryGatewayResult> SendEventAsync(RetentionTelemetryEvent evt, CancellationToken cancellationToken)
            {
                _callCount++;
                if (_callCount == FailAtCallNumber)
                {
                    if (ThrowInsteadOfFail) throw new InvalidOperationException("Simulated network failure.");
                    return Task.FromResult(new RetentionTelemetryGatewayResult { success = false, errorCode = "SIMULATED_FAILURE" });
                }
                SentEventIdsInOrder.Add(evt.eventId);
                return Task.FromResult(new RetentionTelemetryGatewayResult { success = true });
            }
        }

        [Test]
        public void Enqueue_AddsToTheQueue_NeverThrows()
        {
            var outbox = new RetentionTelemetryOutbox(new FakeRetentionTelemetryGateway(), _scratchDir);
            Assert.DoesNotThrow(() => outbox.Enqueue(RetentionTelemetryEvents.FeatureEntry("p1", "avatar")));
            Assert.AreEqual(1, outbox.QueuedEventsForTests.Count);
        }

        [Test]
        public void Enqueue_NullEvent_IsASafeNoOp()
        {
            var outbox = new RetentionTelemetryOutbox(new FakeRetentionTelemetryGateway(), _scratchDir);
            Assert.DoesNotThrow(() => outbox.Enqueue(null));
            Assert.AreEqual(0, outbox.QueuedEventsForTests.Count);
        }

        [Test]
        public void Enqueue_PastMaxQueuedEvents_DropsTheOldest_NotTheNewest()
        {
            var outbox = new RetentionTelemetryOutbox(new FakeRetentionTelemetryGateway(), _scratchDir);
            for (int i = 0; i < RetentionTelemetryOutbox.MaxQueuedEvents + 5; i++)
                outbox.Enqueue(RetentionTelemetryEvents.FeatureEntry("p1", "screen_" + i));

            Assert.AreEqual(RetentionTelemetryOutbox.MaxQueuedEvents, outbox.QueuedEventsForTests.Count,
                "Queue must stay bounded, never grow past the real limit.");
            Assert.AreEqual("screen_5", outbox.QueuedEventsForTests[0].mode,
                "The oldest 5 events (screen_0..screen_4) should have been dropped, screen_5 should now be the front.");
            Assert.AreEqual("screen_" + (RetentionTelemetryOutbox.MaxQueuedEvents + 4), outbox.QueuedEventsForTests[^1].mode,
                "The newest event must always survive.");
        }

        [Test]
        public async Task FlushAsync_WithAWorkingGateway_SendsEveryEvent_InOrder_AndEmptiesTheQueue()
        {
            var fake = new FakeRetentionTelemetryGateway();
            var outbox = new RetentionTelemetryOutbox(fake, _scratchDir);
            RetentionTelemetryEvent e1 = RetentionTelemetryEvents.FeatureEntry("p1", "a");
            RetentionTelemetryEvent e2 = RetentionTelemetryEvents.FeatureEntry("p1", "b");
            RetentionTelemetryEvent e3 = RetentionTelemetryEvents.FeatureEntry("p1", "c");
            outbox.Enqueue(e1);
            outbox.Enqueue(e2);
            outbox.Enqueue(e3);

            int sent = await outbox.FlushAsync(CancellationToken.None);

            Assert.AreEqual(3, sent);
            Assert.AreEqual(0, outbox.QueuedEventsForTests.Count, "A fully successful flush must empty the queue.");
            CollectionAssert.AreEqual(new[] { e1.eventId, e2.eventId, e3.eventId }, fake.SentEventIdsInOrder,
                "Events must be sent in the order they were enqueued.");
        }

        [Test]
        public async Task FlushAsync_StopsAtTheFirstFailure_AndLeavesTheRestQueued()
        {
            var fake = new FakeRetentionTelemetryGateway { FailAtCallNumber = 2 };
            var outbox = new RetentionTelemetryOutbox(fake, _scratchDir);
            outbox.Enqueue(RetentionTelemetryEvents.FeatureEntry("p1", "a"));
            outbox.Enqueue(RetentionTelemetryEvents.FeatureEntry("p1", "b"));
            outbox.Enqueue(RetentionTelemetryEvents.FeatureEntry("p1", "c"));

            int sent = await outbox.FlushAsync(CancellationToken.None);

            Assert.AreEqual(1, sent, "Only the first event (before the failure) should have been sent.");
            Assert.AreEqual(2, outbox.QueuedEventsForTests.Count,
                "The failed event and everything after it must stay queued, not be lost.");
            Assert.AreEqual("b", outbox.QueuedEventsForTests[0].mode);
        }

        [Test]
        public void FlushAsync_WhenTheGatewayThrows_DoesNotThrowIntoTheCaller()
        {
            // Real requirement: "analytics must never block gameplay/rewards/saves". A caller
            // opportunistically flushing on session start must never see an exception from this,
            // regardless of what the network/gateway does underneath.
            var fake = new FakeRetentionTelemetryGateway { FailAtCallNumber = 1, ThrowInsteadOfFail = true };
            var outbox = new RetentionTelemetryOutbox(fake, _scratchDir);
            outbox.Enqueue(RetentionTelemetryEvents.FeatureEntry("p1", "a"));

            Assert.DoesNotThrowAsync(async () => await outbox.FlushAsync(CancellationToken.None));
            Assert.AreEqual(1, outbox.QueuedEventsForTests.Count, "The event must stay queued after a thrown exception, not be lost.");
        }

        [Test]
        public async Task QueuedEvents_SurviveAcrossOutboxInstances_ViaRealDiskPersistence()
        {
            var fake = new FakeRetentionTelemetryGateway();
            var first = new RetentionTelemetryOutbox(fake, _scratchDir);
            first.Enqueue(RetentionTelemetryEvents.FeatureEntry("p1", "before_restart"));

            // A fresh instance pointed at the SAME directory simulates the next app session -
            // this is the real "queuing until the next authenticated session" behavior, not just
            // an in-memory list that would silently lose everything on app close.
            var second = new RetentionTelemetryOutbox(fake, _scratchDir);
            Assert.AreEqual(1, second.QueuedEventsForTests.Count);
            Assert.AreEqual("before_restart", second.QueuedEventsForTests[0].mode);

            await second.FlushAsync(CancellationToken.None);
            var third = new RetentionTelemetryOutbox(fake, _scratchDir);
            Assert.AreEqual(0, third.QueuedEventsForTests.Count,
                "A successfully flushed event must not reappear on the next load.");
        }

        [Test]
        public void ACorruptOutboxFile_NeverCrashesConstruction_StartsWithAnEmptyQueue()
        {
            string filePath = Path.Combine(_scratchDir, "retention_telemetry_outbox.json");
            File.WriteAllText(filePath, "{ this is not valid json !! ");

            RetentionTelemetryOutbox outbox = null;
            Assert.DoesNotThrow(() => outbox = new RetentionTelemetryOutbox(new FakeRetentionTelemetryGateway(), _scratchDir));
            Assert.AreEqual(0, outbox.QueuedEventsForTests.Count,
                "A corrupt file must never block startup - fall back to an empty queue.");
        }
    }
}

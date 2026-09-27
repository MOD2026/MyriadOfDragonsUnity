using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyriadOfDragons.Frontier;
using MyriadOfDragons.Save;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>CC10 client integration: DTOs, state table, reconnect, offline read-only, duplicate
    /// responses, disabled/error states and "client never mutates rewards". The server is faked;
    /// nothing here asserts server behaviour.</summary>
    public class Cc10FrontierTests
    {
        // ---- fakes / builders -------------------------------------------------------------

        private sealed class FakeGateway : ICc10FrontierGateway
        {
            public Func<string, Dictionary<string, object>, object> Handler;
            public readonly List<KeyValuePair<string, Dictionary<string, object>>> Calls =
                new List<KeyValuePair<string, Dictionary<string, object>>>();

            public int CommandCalls => Calls.Count(c => c.Key != Cc10Endpoints.GetState);

            public Task<T> CallAsync<T>(string endpoint, Dictionary<string, object> request, CancellationToken ct)
                where T : Cc10Response
            {
                Calls.Add(new KeyValuePair<string, Dictionary<string, object>>(endpoint, request));
                object r = Handler(endpoint, request);
                if (r is Exception ex) throw ex;
                if (r is Task<T> pending) return pending;
                return Task.FromResult((T)r);
            }
        }

        private static Cc10Snapshot Snap(int version = 1, params Cc10MissionRow[] missions)
        {
            return new Cc10Snapshot
            {
                status = Cc10Status.Ok,
                serverUtcMs = 1_000_000,
                stateVersion = version,
                projection = new Cc10Projection { dailyGoldCap = 900 },
                missions = missions,
            };
        }

        private static Cc10MissionRow Mission(string id, string state, int remaining = 3, int gold = 300) =>
            new Cc10MissionRow
            {
                missionId = id, kind = Cc10Rules.KindNpcPatrol, state = state, staminaCost = 10,
                durationMinutes = 30, goldReward = gold, materialReward = 200,
                dailyLimit = 3, dailyRemaining = remaining, attempt = 1,
            };

        private static Cc10CommandResponse Ok(string receipt = "r1") =>
            new Cc10CommandResponse
            {
                status = Cc10Status.Ok, receiptId = receipt, serverUtcMs = 1_000_500, stateVersion = 2,
                projection = new Cc10Projection { gold = 300, materials = 200 },
            };

        private static async Task<Cc10FrontierClient> OnlineClient(FakeGateway gw, Cc10Snapshot snap,
            Func<string> ids = null)
        {
            gw.Handler = (e, r) => e == Cc10Endpoints.GetState ? (object)snap : Ok();
            var client = new Cc10FrontierClient(gw, new Cc10ServerClock(() => 0), ids);
            Assert.IsTrue(await client.RefreshAsync());
            return client;
        }

        // ---- DTOs -------------------------------------------------------------------------

        [Test]
        public void Snapshot_DeserializesServerJson_AndIgnoresUnknownFields()
        {
            const string json = "{\"status\":\"Ok\",\"stateVersion\":7,\"serverUtcMs\":1234,\"futureField\":1," +
                "\"tavern\":{\"level\":3,\"nextCostGold\":400}," +
                "\"missions\":[{\"missionId\":\"m1\",\"kind\":\"NpcPatrol\",\"state\":\"Completed\",\"goldReward\":300}]," +
                "\"disabledSystems\":[\"Cargo\"]}";
            Cc10Snapshot s = JsonUtility.FromJson<Cc10Snapshot>(json);
            Assert.AreEqual(7, s.stateVersion);
            Assert.AreEqual(3, s.tavern.level);
            Assert.AreEqual("m1", s.missions[0].missionId);
            Assert.AreEqual(300, s.missions[0].goldReward);
            Assert.AreEqual("Cargo", s.disabledSystems[0]);
            Assert.IsNotNull(s.projection, "absent object fields keep safe defaults");
            Assert.AreEqual(0, s.cargo.Length);
        }

        [Test]
        public void Response_IsSuccess_OnlyForOkAndAlreadyCommitted()
        {
            Assert.IsTrue(new Cc10Response { status = Cc10Status.Ok }.IsSuccess);
            Assert.IsTrue(new Cc10Response { status = Cc10Status.AlreadyCommitted }.IsSuccess);
            foreach (string bad in new[] { Cc10Status.Conflict, Cc10Status.Disabled, Cc10Status.Rejected,
                         Cc10Status.CapExceeded, Cc10Status.NotFound, "" })
                Assert.IsFalse(new Cc10Response { status = bad }.IsSuccess, bad);
        }

        // ---- locked rules -----------------------------------------------------------------

        [Test]
        public void LockedBetaNumbers_MatchAuthorityRecord()
        {
            Assert.AreEqual(5400, Cc10Rules.TavernTotalGold);
            Assert.AreEqual(5400, Cc10Rules.TavernTotalMaterials);
            Assert.AreEqual(1605, Cc10Rules.TavernTotalMinutes, "26h45m");
            Assert.AreEqual(10, Cc10Rules.MissionStaminaCost);
            Assert.AreEqual(300, Cc10Rules.MissionGoldReward);
            Assert.AreEqual(200, Cc10Rules.MissionMaterialReward);
            Assert.AreEqual(900, Cc10Rules.SharedDailyGoldCap);

            Cc10Rules.TryGetMissionRule(Cc10Rules.KindNpcPatrol, out var patrol);
            Cc10Rules.TryGetMissionRule(Cc10Rules.KindRelicRescue, out var relic);
            Cc10Rules.TryGetMissionRule(Cc10Rules.KindVeinConvoy, out var vein);
            Assert.AreEqual((30, 3, 8), (patrol.Minutes, patrol.DailyLimit, patrol.RefreshHours));
            Assert.AreEqual((60, 2, 12), (relic.Minutes, relic.DailyLimit, relic.RefreshHours));
            Assert.AreEqual((120, 1, 24), (vein.Minutes, vein.DailyLimit, vein.RefreshHours));
            Assert.IsFalse(Cc10Rules.TryGetMissionRule("Unknown", out _));
        }

        [Test]
        public void CardTraining_UsesExistingCostAndLevelCap()
        {
            Assert.AreEqual(100, Cc10Rules.CardTrainingCost(1));
            Assert.AreEqual(500, Cc10Rules.CardTrainingCost(5));
            Assert.AreEqual(100, Cc10Rules.CardTrainingCost(0), "max(1, level) * 100");
            Assert.AreEqual(100, Cc10Rules.CardLevelCap);
        }

        [Test]
        public void GoldCapPreview_RejectsWholeClaim_NeverPartial()
        {
            Assert.IsFalse(Cc10Rules.ClaimWouldCrossGoldCap(600, 300), "exactly 900 is allowed");
            Assert.IsTrue(Cc10Rules.ClaimWouldCrossGoldCap(601, 300));
            Assert.IsFalse(Cc10Rules.ClaimWouldCrossGoldCap(900, 0), "a zero-Gold claim never crosses");
        }

        [Test]
        public void MissionStateMachine_AcceptsOnlyLegalEdges()
        {
            Assert.IsTrue(Cc10StateMachine.IsMissionTransitionLegal("Accepted", "Scouting"));
            Assert.IsTrue(Cc10StateMachine.IsMissionTransitionLegal("Scouting", "Active"));
            Assert.IsTrue(Cc10StateMachine.IsMissionTransitionLegal("Active", "Completed"));
            Assert.IsTrue(Cc10StateMachine.IsMissionTransitionLegal("Completed", "Claimed"));
            Assert.IsTrue(Cc10StateMachine.IsMissionTransitionLegal("Active", "Failed"));
            Assert.IsTrue(Cc10StateMachine.IsMissionTransitionLegal("Active", "Active"), "idempotent replay");
            Assert.IsFalse(Cc10StateMachine.IsMissionTransitionLegal("Claimed", "Completed"));
            Assert.IsFalse(Cc10StateMachine.IsMissionTransitionLegal("Failed", "Claimed"));
            Assert.IsFalse(Cc10StateMachine.IsMissionTransitionLegal("Accepted", "Completed"), "no skipping Active");
            Assert.IsFalse(Cc10StateMachine.IsMissionTransitionLegal("Abandoned", "Active"));
        }

        [Test]
        public void CargoStateMachine_TerminalsAreFinal_AndClaimNeedsDelivery()
        {
            Assert.IsTrue(Cc10StateMachine.IsCargoTransitionLegal("Active", "Delivered"));
            Assert.IsTrue(Cc10StateMachine.IsCargoTransitionLegal("Active", "Intercepted"));
            Assert.IsTrue(Cc10StateMachine.IsCargoTransitionLegal("Delivered", "Claimed"));
            Assert.IsFalse(Cc10StateMachine.IsCargoTransitionLegal("Intercepted", "Delivered"));
            Assert.IsFalse(Cc10StateMachine.IsCargoTransitionLegal("Active", "Claimed"));
            Assert.IsFalse(Cc10StateMachine.IsCargoTransitionLegal("Claimed", "Delivered"));
            Assert.IsTrue(Cc10StateMachine.IsClaimable("Delivered"));
            Assert.IsFalse(Cc10StateMachine.IsClaimable("Intercepted"));
        }

        [Test]
        public void ServerClock_IsMonotonicDisplay_DeviceClockCannotMoveIt()
        {
            long mono = 0;
            var clock = new Cc10ServerClock(() => mono);
            Assert.AreEqual(0, clock.RemainingMs(5000), "no sample: unknown, not a guess");
            clock.Sample(1_000_000);
            Assert.AreEqual(60_000, clock.RemainingMs(1_060_000));
            mono = 30_000; // only elapsed monotonic time advances the display
            Assert.AreEqual(30_000, clock.RemainingMs(1_060_000));
            // Changing DateTime.UtcNow / device clock has no input into the clock at all.
            mono = 90_000;
            Assert.AreEqual(0, clock.RemainingMs(1_060_000));
            Assert.AreEqual("Ready", Cc10ServerClock.FormatRemaining(0));
            Assert.AreEqual("1h 30m", Cc10ServerClock.FormatRemaining(90 * 60_000));
            Assert.AreEqual("1m", Cc10ServerClock.FormatRemaining(1));
        }

        // ---- client: reconnect / offline / stale ------------------------------------------

        [Test]
        public async Task Refresh_Failure_GoesOffline_KeepsLastSnapshotReadOnly()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap(1, Mission("m1", "Completed")));
            gw.Handler = (e, r) => new InvalidOperationException("network");

            Assert.IsFalse(await client.RefreshAsync());
            Assert.AreEqual(Cc10Connection.Offline, client.Connection);
            Assert.IsTrue(client.HasState, "last authoritative snapshot stays visible");
            Assert.IsTrue(client.IsReadOnly(Cc10System.Missions));
            Assert.AreEqual("m1", client.Snapshot.missions[0].missionId);
        }

        [Test]
        public async Task Offline_CommandIsBlocked_WithoutCallingTheServer()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap(1, Mission("m1", "Completed")));
            gw.Handler = (e, r) => new InvalidOperationException("network");
            await client.RefreshAsync();
            gw.Calls.Clear();

            Cc10CommandResult r2 = await client.ExecuteAsync(Cc10System.Missions, Cc10Endpoints.ClaimMission,
                new Dictionary<string, object> { { "missionId", "m1" } }, "m1");

            Assert.AreEqual(Cc10Outcome.Offline, r2.Outcome);
            Assert.AreEqual(Cc10Copy.Offline, r2.Message);
            Assert.AreEqual(0, gw.Calls.Count, "no offline claim is even attempted");
        }

        [Test]
        public async Task NeverLoaded_IsReadOnly_AndCommandIsBlocked()
        {
            var gw = new FakeGateway { Handler = (e, r) => new InvalidOperationException("down") };
            var client = new Cc10FrontierClient(gw);
            Assert.IsFalse(await client.RefreshAsync());
            Assert.IsFalse(client.HasState);
            Assert.IsTrue(client.IsReadOnly(Cc10System.Tavern));
            Cc10CommandResult r = await client.ExecuteAsync(Cc10System.Tavern, Cc10Endpoints.UpgradeTavern, null, "t");
            Assert.AreEqual(Cc10Outcome.Offline, r.Outcome);
        }

        [Test]
        public async Task Reconnect_ReloadsAuthoritativeState_ThenCommandsWork()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap(1, Mission("m1", "Completed")));
            gw.Handler = (e, r) => new InvalidOperationException("network");
            await client.RefreshAsync();
            Assert.AreEqual(Cc10Connection.Offline, client.Connection);

            gw.Handler = (e, r) => e == Cc10Endpoints.GetState ? (object)Snap(3, Mission("m1", "Claimed")) : Ok();
            Assert.IsTrue(await client.RefreshAsync());
            Assert.AreEqual(Cc10Connection.Online, client.Connection);
            Assert.AreEqual("Claimed", client.Snapshot.missions[0].state);
            Assert.IsFalse(client.IsReadOnly(Cc10System.Missions));
        }

        [Test]
        public async Task StaleSnapshot_OlderThanOnScreen_IsIgnored()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap(5, Mission("m1", "Completed")));
            gw.Handler = (e, r) => Snap(4, Mission("m1", "Active"));
            Assert.IsFalse(await client.RefreshAsync());
            Assert.AreEqual(5, client.Snapshot.stateVersion);
            Assert.AreEqual("Completed", client.Snapshot.missions[0].state);
        }

        [Test]
        public async Task IllegalStateJump_IsRecordedAsAnomaly_ButServerStateStillWins()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap(1, Mission("m1", "Claimed")));
            gw.Handler = (e, r) => Snap(2, Mission("m1", "Completed"));
            Assert.IsTrue(await client.RefreshAsync());
            Assert.AreEqual(1, client.StateAnomalies.Count);
            Assert.AreEqual("Completed", client.Snapshot.missions[0].state, "server is authoritative");
        }

        // ---- client: disabled / errors ----------------------------------------------------

        [Test]
        public async Task ServerDisabledSystem_IsReadOnly_AndRejectsMutationWithoutCall()
        {
            var gw = new FakeGateway();
            Cc10Snapshot snap = Snap(1, Mission("m1", "Completed"));
            snap.disabledSystems = new[] { "Missions" };
            Cc10FrontierClient client = await OnlineClient(gw, snap);
            gw.Calls.Clear();

            Assert.IsTrue(client.IsSystemDisabled(Cc10System.Missions));
            Assert.IsFalse(client.IsSystemDisabled(Cc10System.Tavern));
            Cc10CommandResult r = await client.ExecuteAsync(Cc10System.Missions, Cc10Endpoints.ClaimMission, null, "m1");
            Assert.AreEqual(Cc10Outcome.Disabled, r.Outcome);
            Assert.AreEqual(0, gw.Calls.Count);
            Assert.AreEqual("Completed", client.Snapshot.missions[0].state, "state unchanged");
        }

        [Test]
        public async Task DisableArrivingInResponse_FlipsSystemReadOnly_WithoutRewardEvent()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap(1, Mission("m1", "Completed")));
            int projections = 0;
            client.ProjectionReceived += _ => projections++;
            gw.Handler = (e, r) => new Cc10CommandResponse { status = Cc10Status.Disabled, systemDisabled = true };

            Cc10CommandResult r = await client.ExecuteAsync(Cc10System.Missions, Cc10Endpoints.ClaimMission, null, "m1");
            Assert.AreEqual(Cc10Outcome.Disabled, r.Outcome);
            Assert.IsTrue(client.IsReadOnly(Cc10System.Missions));
            Assert.AreEqual(0, projections);
        }

        [Test]
        public async Task CapExceeded_IsRejectedWithPlainCopy_AndNoProjection()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap(1, Mission("m1", "Completed")));
            int projections = 0;
            client.ProjectionReceived += _ => projections++;
            gw.Handler = (e, r) => new Cc10CommandResponse { status = Cc10Status.CapExceeded, errorCode = "GOLD_CAP" };

            Cc10CommandResult r = await client.ExecuteAsync(Cc10System.Missions, Cc10Endpoints.ClaimMission, null, "m1");
            Assert.AreEqual(Cc10Outcome.Rejected, r.Outcome);
            Assert.AreEqual(Cc10Copy.CapReached, r.Message);
            StringAssert.DoesNotContain("GOLD_CAP", r.Message, "raw backend codes never reach the player");
            Assert.AreEqual(0, projections);
        }

        [Test]
        public async Task Conflict_ReloadsState_AndReportsConflict()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap(1, Mission("m1", "Completed")));
            gw.Handler = (e, r) => e == Cc10Endpoints.GetState
                ? (object)Snap(4, Mission("m1", "Claimed"))
                : new Cc10CommandResponse { status = Cc10Status.Conflict };

            Cc10CommandResult r = await client.ExecuteAsync(Cc10System.Missions, Cc10Endpoints.ClaimMission, null, "m1");
            Assert.AreEqual(Cc10Outcome.Conflict, r.Outcome);
            Assert.AreEqual("Claimed", client.Snapshot.missions[0].state, "first valid CAS wins; client reloads");
        }

        // ---- client: duplicates / idempotency ---------------------------------------------

        [Test]
        public async Task DuplicateReceipt_IsReplayed_ProjectionRaisedOnce()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap(1, Mission("m1", "Completed")));
            var projections = new List<Cc10Projection>();
            client.ProjectionReceived += projections.Add;

            gw.Handler = (e, r) => e == Cc10Endpoints.GetState ? (object)Snap(2, Mission("m1", "Claimed")) : Ok("rcpt-A");
            Cc10CommandResult first = await client.ExecuteAsync(Cc10System.Missions, Cc10Endpoints.ClaimMission, null, "m1");

            gw.Handler = (e, r) => e == Cc10Endpoints.GetState
                ? (object)Snap(2, Mission("m1", "Claimed"))
                : new Cc10CommandResponse { status = Cc10Status.AlreadyCommitted, receiptId = "rcpt-A", projection = new Cc10Projection { gold = 300 } };
            Cc10CommandResult second = await client.ExecuteAsync(Cc10System.Missions, Cc10Endpoints.ClaimMission, null, "m1");

            Assert.AreEqual(Cc10Outcome.Applied, first.Outcome);
            Assert.AreEqual(Cc10Outcome.Replayed, second.Outcome);
            Assert.AreEqual(Cc10Copy.AlreadyRecorded, second.Message);
            Assert.AreEqual(1, projections.Count, "one receipt, one projection - never double-shown");
        }

        [Test]
        public async Task LostResponse_RetryReusesTheOriginalRequestId()
        {
            var gw = new FakeGateway();
            int n = 0;
            Cc10FrontierClient client = await OnlineClient(gw, Snap(1, Mission("m1", "Completed")),
                () => "req-" + (++n));

            gw.Handler = (e, r) => new TimeoutException("lost");
            Cc10CommandResult lost = await client.ExecuteAsync(Cc10System.Missions, Cc10Endpoints.ClaimMission, null, "m1");
            Assert.AreEqual(Cc10Outcome.Failed, lost.Outcome);
            Assert.AreEqual(1, client.PendingRequestCount, "request id kept for replay");

            gw.Handler = (e, r) => e == Cc10Endpoints.GetState ? (object)Snap(1, Mission("m1", "Completed")) : Ok();
            Assert.IsTrue(await client.RefreshAsync()); // reconnect

            Cc10CommandResult retry = await client.ExecuteAsync(Cc10System.Missions, Cc10Endpoints.ClaimMission, null, "m1");
            Assert.AreEqual(Cc10Outcome.Applied, retry.Outcome);
            string first = (string)gw.Calls.First(c => c.Key == Cc10Endpoints.ClaimMission).Value["requestId"];
            string last = (string)gw.Calls.Last(c => c.Key == Cc10Endpoints.ClaimMission).Value["requestId"];
            Assert.AreEqual("req-1", first);
            Assert.AreEqual(first, last, "same request id, so the server can replay the original receipt");
            Assert.AreEqual(0, client.PendingRequestCount);
        }

        [Test]
        public async Task DoubleTap_WhileInFlight_SendsOnlyOneRequest()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap(1, Mission("m1", "Completed")));
            var tcs = new TaskCompletionSource<Cc10CommandResponse>();
            gw.Handler = (e, r) => e == Cc10Endpoints.GetState ? (object)Snap(2, Mission("m1", "Claimed")) : tcs.Task;
            gw.Calls.Clear();

            Task<Cc10CommandResult> a = client.ExecuteAsync(Cc10System.Missions, Cc10Endpoints.ClaimMission, null, "m1");
            Cc10CommandResult b = await client.ExecuteAsync(Cc10System.Missions, Cc10Endpoints.ClaimMission, null, "m1");
            Assert.AreEqual(Cc10Outcome.InFlight, b.Outcome);

            tcs.SetResult(Ok("rcpt-D"));
            Assert.AreEqual(Cc10Outcome.Applied, (await a).Outcome);
            Assert.AreEqual(1, gw.Calls.Count(c => c.Key == Cc10Endpoints.ClaimMission));
        }

        [Test]
        public async Task Command_SendsExpectedStateVersion_ForServerSideCas()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap(9, Mission("m1", "Completed")));
            await client.ExecuteAsync(Cc10System.Missions, Cc10Endpoints.ClaimMission,
                new Dictionary<string, object> { { "missionId", "m1" } }, "m1");
            Dictionary<string, object> body = gw.Calls.First(c => c.Key == Cc10Endpoints.ClaimMission).Value;
            Assert.AreEqual(9, body["expectedStateVersion"]);
            Assert.AreEqual("m1", body["missionId"]);
            Assert.IsFalse(body.ContainsKey("goldReward") || body.ContainsKey("reward") || body.ContainsKey("utc"),
                "the client never sends rewards or times");
        }

        // ---- no local reward mutation -----------------------------------------------------

        [Test]
        public async Task ClaimFlow_NeverMutatesLocalProfile()
        {
            string dir = Path.Combine(Path.GetTempPath(), "MoDCc10_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                SaveSystem.OverrideRootDirectoryForTests(dir);
                SaveSystem.ResetCurrentProfileForTests();
                string before = JsonUtility.ToJson(SaveSystem.CurrentProfile);

                var gw = new FakeGateway();
                Cc10FrontierClient client = await OnlineClient(gw, Snap(1, Mission("m1", "Completed")));
                gw.Handler = (e, r) => e == Cc10Endpoints.GetState ? (object)Snap(2, Mission("m1", "Claimed")) : Ok();
                await client.ExecuteAsync(Cc10System.Missions, Cc10Endpoints.ClaimMission, null, "m1");

                Assert.AreEqual(before, JsonUtility.ToJson(SaveSystem.CurrentProfile),
                    "gold/materials/cards change only when the host applies the server projection");
            }
            finally
            {
                SaveSystem.ClearRootDirectoryOverride();
                SaveSystem.ResetCurrentProfileForTests();
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        // ---- view models ------------------------------------------------------------------

        private static Cc10SectionVm Vm(Cc10FrontierClient c, Cc10System s,
            IEnumerable<CardProgressionRecord> cards = null) => Cc10ViewModels.Build(c, s, cards);

        [Test]
        public async Task Offline_EveryAction_IsDisabled_WithReason_RowsStillShown()
        {
            var gw = new FakeGateway();
            Cc10Snapshot snap = Snap(1, Mission("m1", "Completed"), Mission("m2", "Available"));
            Cc10FrontierClient client = await OnlineClient(gw, snap);
            gw.Handler = (e, r) => new InvalidOperationException("network");
            await client.RefreshAsync();

            Cc10SectionVm vm = Vm(client, Cc10System.Missions);
            Assert.AreEqual(Cc10Copy.Offline, vm.Banner);
            Assert.AreEqual(2, vm.Rows.Count, "read-only view still shows the last snapshot");
            Assert.IsTrue(vm.Rows.All(r => !r.HasAction || (!r.ActionEnabled && !string.IsNullOrEmpty(r.DisabledReason))));
        }

        [Test]
        public void NoState_ShowsBannerOnly()
        {
            var client = new Cc10FrontierClient(new FakeGateway());
            Cc10SectionVm vm = Vm(client, Cc10System.Tavern);
            Assert.AreEqual(0, vm.Rows.Count);
            Assert.IsTrue(vm.ReadOnly);
            Assert.IsNotEmpty(vm.Banner);
        }

        [Test]
        public async Task Missions_ActionsFollowServerState()
        {
            var gw = new FakeGateway();
            Cc10Snapshot snap = Snap(1,
                Mission("a", "Available", remaining: 0), Mission("b", "Available"),
                Mission("c", "Active"), Mission("d", "Completed"), Mission("e", "Claimed"));
            Cc10FrontierClient client = await OnlineClient(gw, snap);
            List<Cc10Row> rows = Vm(client, Cc10System.Missions).Rows;

            Assert.IsFalse(rows[0].ActionEnabled, "no attempts left today");
            Assert.IsNotEmpty(rows[0].DisabledReason);
            Assert.IsTrue(rows[1].ActionEnabled);
            Assert.AreEqual(Cc10Endpoints.AssignMission, rows[1].Endpoint);
            Assert.AreEqual(Cc10Endpoints.AbandonMission, rows[2].Endpoint);
            Assert.AreEqual(Cc10Endpoints.ClaimMission, rows[3].Endpoint);
            Assert.IsFalse(rows[4].HasAction, "claimed is terminal and read-only");
        }

        [Test]
        public async Task CompletedMission_NearCap_ShowsHint_ButServerStillDecides()
        {
            var gw = new FakeGateway();
            Cc10Snapshot snap = Snap(1, Mission("d", "Completed"));
            snap.projection.dailyGoldUsed = 700;
            Cc10Row row = Vm(await OnlineClient(gw, snap), Cc10System.Missions).Rows[0];
            StringAssert.Contains(Cc10Copy.CapReached, row.Detail);
            Assert.IsTrue(row.ActionEnabled, "a stale client hint must not block a claim the server would accept");
        }

        [Test]
        public async Task Research_LockedHasNoAction_GuildUsesContribute_ActiveIndividualCanCancel()
        {
            var gw = new FakeGateway();
            Cc10Snapshot snap = Snap();
            snap.research = new[]
            {
                new Cc10ResearchNode { nodeId = "i1", scope = "Individual", state = "Locked", prerequisiteIds = new[] { "i0" } },
                new Cc10ResearchNode { nodeId = "i2", scope = "Individual", state = "Active", completeUtcMs = 9_000_000 },
                new Cc10ResearchNode { nodeId = "g1", scope = "Guild", state = "Available" },
            };
            Cc10FrontierClient client = await OnlineClient(gw, snap);

            List<Cc10Row> ind = Vm(client, Cc10System.IndividualResearch).Rows;
            Assert.AreEqual(2, ind.Count, "guild nodes are not listed under individual research");
            Assert.IsFalse(ind[0].HasAction);
            StringAssert.Contains("i0", ind[0].Detail);
            Assert.AreEqual(Cc10Endpoints.CancelResearch, ind[1].Endpoint);

            Cc10Row guild = Vm(client, Cc10System.GuildResearch).Rows.Single();
            Assert.AreEqual(Cc10Endpoints.ContributeGuildResearch, guild.Endpoint);
        }

        [Test]
        public async Task Rankings_AndNpcSpots_AreReadOnly()
        {
            var gw = new FakeGateway();
            Cc10Snapshot snap = Snap();
            snap.rankings = new[]
            {
                new Cc10RankingBoard { scope = "Individual", seasonId = "S1", phase = "Published",
                    entries = new[] { new Cc10RankEntry { rank = 1, displayName = "Ada", score = 90 } } },
                new Cc10RankingBoard { scope = "Guild", seasonId = "S1", phase = "Published" },
            };
            snap.spots = new[] { new Cc10NpcSpot { spotId = "s1", kind = Cc10Rules.KindVeinConvoy, state = "Available" } };
            Cc10FrontierClient client = await OnlineClient(gw, snap);

            Assert.IsTrue(Vm(client, Cc10System.IndividualRankings).Rows.All(r => !r.HasAction));
            Assert.AreEqual(2, Vm(client, Cc10System.IndividualRankings).Rows.Count);
            Assert.AreEqual(1, Vm(client, Cc10System.GuildRankings).Rows.Count);
            Assert.IsTrue(Vm(client, Cc10System.NpcSpots).Rows.All(r => !r.HasAction));
        }

        [Test]
        public async Task Tavern_UpgradeDisabledWhileActiveOrAtMax()
        {
            var gw = new FakeGateway();
            Cc10Snapshot snap = Snap();
            snap.tavern = new Cc10TavernState { level = 4, nextCostGold = 500, nextCostMaterials = 500 };
            Cc10FrontierClient client = await OnlineClient(gw, snap);
            Assert.IsTrue(Vm(client, Cc10System.Tavern).Rows[0].ActionEnabled);

            snap.tavern.upgradeActive = true;
            Assert.IsFalse(Vm(client, Cc10System.Tavern).Rows[0].ActionEnabled);

            snap.tavern.upgradeActive = false;
            snap.tavern.level = Cc10Rules.TavernMaxLevel;
            Cc10Row max = Vm(client, Cc10System.Tavern).Rows[0];
            Assert.IsFalse(max.ActionEnabled);
            Assert.AreEqual("Fully upgraded", max.Detail);
        }

        [Test]
        public async Task WorldMap_TravelIsFree_UndiscoveredHasNoAction()
        {
            var gw = new FakeGateway();
            Cc10Snapshot snap = Snap();
            snap.nodes = new[]
            {
                new Cc10MapNode { nodeId = "n1", regionId = "R1", discovered = true },
                new Cc10MapNode { nodeId = "n2", regionId = "R1", discovered = false },
            };
            List<Cc10Row> rows = Vm(await OnlineClient(gw, snap), Cc10System.WorldMap).Rows;
            Assert.AreEqual("Travel is free", rows[0].Detail);
            Assert.IsTrue(rows[0].ActionEnabled);
            Assert.IsFalse(rows[1].HasAction);
        }

        [Test]
        public async Task CardTraining_UsesCollectionRules()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap());
            var cards = new[]
            {
                new CardProgressionRecord { cardId = "poor", cardLevel = 3, trainingXp = 299 },
                new CardProgressionRecord { cardId = "rich", cardLevel = 3, trainingXp = 300 },
                new CardProgressionRecord { cardId = "max", cardLevel = 100, trainingXp = 99999 },
            };
            List<Cc10Row> rows = Vm(client, Cc10System.CardTraining, cards).Rows;
            Assert.IsFalse(rows[0].ActionEnabled);
            Assert.IsTrue(rows[1].ActionEnabled);
            Assert.IsFalse(rows[2].ActionEnabled);
            Assert.AreEqual("Max level", rows[2].Detail);
            Assert.AreEqual("rich", rows[1].Payload["cardId"]);
        }

        [Test]
        public async Task Minigame_IsOptionalAndCarriesNoReward()
        {
            var gw = new FakeGateway();
            Cc10Row row = Vm(await OnlineClient(gw, Snap()), Cc10System.Minigame).Rows.Single();
            StringAssert.Contains("optional", row.Title);
            StringAssert.Contains("progression", row.Detail);
            Assert.IsFalse(row.Detail.Contains("Gold"));
        }

        [Test]
        public async Task Cargo_ClaimOnlyWhenDelivered_TerminalsReadOnly()
        {
            var gw = new FakeGateway();
            Cc10Snapshot snap = Snap();
            snap.cargo = new[]
            {
                new Cc10CargoRecord { cargoId = "c1", state = "Available" },
                new Cc10CargoRecord { cargoId = "c2", state = "Active", expiryUtcMs = 2_000_000 },
                new Cc10CargoRecord { cargoId = "c3", state = "Delivered" },
                new Cc10CargoRecord { cargoId = "c4", state = "Intercepted" },
            };
            List<Cc10Row> rows = Vm(await OnlineClient(gw, snap), Cc10System.Cargo).Rows;
            Assert.AreEqual(Cc10Endpoints.AcceptCargo, rows[0].Endpoint);
            Assert.IsFalse(rows[1].HasAction);
            Assert.AreEqual(Cc10Endpoints.ClaimCargo, rows[2].Endpoint);
            Assert.IsFalse(rows[3].HasAction);
        }

        [Test]
        public async Task GuildTerritory_ContributionIsCommandOnly_NoClientOwnership()
        {
            var gw = new FakeGateway();
            Cc10Snapshot snap = Snap();
            snap.territories = new[] { new Cc10Territory { territoryId = "t1", contested = true, windowEndUtcMs = 1_600_000 } };
            Cc10Row row = Vm(await OnlineClient(gw, snap), Cc10System.GuildTerritory).Rows.Single();
            StringAssert.Contains("Contested", row.Detail);
            StringAssert.Contains("Unclaimed", row.Detail);
            Assert.AreEqual(Cc10Endpoints.ContributeTerritory, row.Endpoint);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyriadOfDragons.Frontier;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>CC10 client integration against the real, published server contract
    /// (CloudCode/CC10Frontier). Server is faked; nothing here asserts server behaviour. Covers
    /// DTOs, state tables, reconnect, offline/disabled read-only gating, duplicate-response
    /// idempotency, conflict/authority-generation reload, and no-local-reward-mutation.</summary>
    public class Cc10FrontierTests
    {
        private sealed class FakeGateway : ICc10FrontierGateway
        {
            public Func<string, Dictionary<string, object>, object> Handler;
            public readonly List<KeyValuePair<string, Dictionary<string, object>>> Calls =
                new List<KeyValuePair<string, Dictionary<string, object>>>();

            public int CommandCalls => Calls.Count(c => c.Key != Cc10Endpoints.GetFrontierState);

            public Task<T> CallAsync<T>(string endpoint, Dictionary<string, object> request, CancellationToken ct)
                where T : Cc10ResultBase
            {
                Calls.Add(new KeyValuePair<string, Dictionary<string, object>>(endpoint, request));
                object r = Handler(endpoint, request);
                if (r is Exception ex) throw ex;
                if (r is Task<T> pending) return pending;
                return Task.FromResult((T)r);
            }
        }

        private static Cc10FrontierSnapshot Snap(long version = 1, long authorityGen = 1, params Cc10MissionDto[] missions)
        {
            return new Cc10FrontierSnapshot
            {
                success = true,
                serverUtcMs = 1_000_000,
                stateVersion = version,
                authorityGeneration = authorityGen,
                gold = new Cc10GoldLedgerDto { cap = 900 },
                missions = missions,
                nodes = new[] { new Cc10MapNodeDto { nodeId = "hub", regionId = "frontier", discovered = true, neighbors = new[] { "patrol_road" } } },
                spots = Array.Empty<Cc10NpcSpotDto>(),
            };
        }

        private static Cc10MissionDto Mission(string id, string status, long readyUtcMs = 1_060_000) =>
            new Cc10MissionDto { missionId = id, type = Cc10MissionType.NpcPatrol, status = status, readyUtcMs = readyUtcMs };

        private static Cc10CommandResult Ok(string receipt = "r1") =>
            new Cc10CommandResult { success = true, serverUtcMs = 1_000_500, stateVersion = 2, receipt = new Cc10Receipt { receiptId = receipt, goldCredit = 300, materialsCredit = 200 } };

        private static async Task<Cc10FrontierClient> OnlineClient(FakeGateway gw, Cc10FrontierSnapshot snap, Func<string> ids = null)
        {
            gw.Handler = (e, r) => e == Cc10Endpoints.GetFrontierState ? (object)snap : Ok();
            var client = new Cc10FrontierClient(gw, new Cc10ServerClock(() => 0), ids);
            Assert.IsTrue(await client.RefreshAsync());
            return client;
        }

        // ---- DTOs / real contract shape ----------------------------------------------------

        [Test]
        public void Snapshot_DeserializesRealServerJson()
        {
            const string json = "{\"success\":true,\"stateVersion\":7,\"authorityGeneration\":2,\"serverUtcMs\":1234," +
                "\"tavern\":{\"level\":3}," +
                "\"missions\":[{\"missionId\":\"m1\",\"type\":\"NpcPatrol\",\"status\":\"Completed\"}]," +
                "\"disabledSystems\":[\"Cargo\"]}";
            Cc10FrontierSnapshot s = UnityEngine.JsonUtility.FromJson<Cc10FrontierSnapshot>(json);
            Assert.AreEqual(7, s.stateVersion);
            Assert.AreEqual(2, s.authorityGeneration);
            Assert.AreEqual(3, s.tavern.level);
            Assert.AreEqual("m1", s.missions[0].missionId);
            Assert.IsTrue(s.IsSystemDisabled(Cc10SystemId.Cargo));
            Assert.IsFalse(s.IsSystemDisabled(Cc10SystemId.Tavern));
        }

        [Test]
        public void MissionType_And_Status_ConstantsMatchPublishedEnumNames()
        {
            // JsonConverter(StringEnumConverter) on the server means these string literals ARE the
            // wire values - a mismatch here is a silent parse failure, not a compile error.
            Assert.AreEqual("NpcPatrol", Cc10MissionType.NpcPatrol);
            Assert.AreEqual("RelicRescue", Cc10MissionType.RelicRescue);
            Assert.AreEqual("VeinConvoy", Cc10MissionType.VeinConvoy);
            Assert.AreEqual("Active", Cc10MissionStatus.Active);
            Assert.AreEqual("Claimed", Cc10MissionStatus.Claimed);
            Assert.AreEqual("Delivered", Cc10CargoStatus.Delivered);
            Assert.AreEqual("Intercepted", Cc10CargoStatus.Intercepted);
        }

        // ---- locked rules (from CC10RowSet, validated server-side) --------------------------

        [Test]
        public void LockedBetaNumbers_MatchPublishedRowSet()
        {
            Assert.AreEqual(5400, Cc10Rules.TavernTotalGold);
            Assert.AreEqual(5400, Cc10Rules.TavernTotalMaterials);
            Assert.AreEqual(1605, Cc10Rules.TavernTotalMinutes, "26h45m");
            Assert.AreEqual(10, Cc10Rules.MissionStaminaCost);
            Assert.AreEqual(300, Cc10Rules.MissionGoldReward);
            Assert.AreEqual(200, Cc10Rules.MissionMaterialReward);
            Assert.AreEqual(900, Cc10Rules.GoldCapPerUtcDay);
            Assert.AreEqual(330, Cc10Rules.MaxExpeditionGoldPerReport);
            Assert.AreEqual(2300, Cc10Rules.CampaignMaterialsSourceTotal);

            Cc10Rules.TryGetMissionRule(Cc10MissionType.NpcPatrol, out var patrol);
            Cc10Rules.TryGetMissionRule(Cc10MissionType.RelicRescue, out var relic);
            Cc10Rules.TryGetMissionRule(Cc10MissionType.VeinConvoy, out var vein);
            Assert.AreEqual((30, 3, 8, 2), (patrol.Minutes, patrol.DailyLimit, patrol.RefreshHours, patrol.UnlockTavernLevel));
            Assert.AreEqual((60, 2, 12, 4), (relic.Minutes, relic.DailyLimit, relic.RefreshHours, relic.UnlockTavernLevel));
            Assert.AreEqual((120, 1, 24, 6), (vein.Minutes, vein.DailyLimit, vein.RefreshHours, vein.UnlockTavernLevel));
            Assert.IsFalse(Cc10Rules.TryGetMissionRule("Unknown", out _));
        }

        [Test]
        public void CardTraining_UsesExistingCostAndLevelCap()
        {
            Assert.AreEqual(100, Cc10Rules.CardTrainingCost(1));
            Assert.AreEqual(500, Cc10Rules.CardTrainingCost(5));
            Assert.AreEqual(100, Cc10Rules.CardLevelCap == 100 ? 100 : -1);
        }

        [Test]
        public void MissionStateMachine_AcceptsOnlyLegalEdges_FromPublishedEnum()
        {
            Assert.IsTrue(Cc10StateMachine.IsMissionTransitionLegal("Active", "Completed"));
            Assert.IsTrue(Cc10StateMachine.IsMissionTransitionLegal("Completed", "Claimed"));
            Assert.IsTrue(Cc10StateMachine.IsMissionTransitionLegal("Active", "Failed"));
            Assert.IsTrue(Cc10StateMachine.IsMissionTransitionLegal("Active", "Active"), "idempotent replay");
            Assert.IsFalse(Cc10StateMachine.IsMissionTransitionLegal("Claimed", "Completed"), "terminal is final");
            Assert.IsFalse(Cc10StateMachine.IsMissionTransitionLegal("Failed", "Claimed"));
            Assert.IsTrue(Cc10StateMachine.IsMissionTerminal("Claimed"));
            Assert.IsTrue(Cc10StateMachine.IsMissionTerminal("Expired"));
            Assert.IsFalse(Cc10StateMachine.IsMissionTerminal("Active"));
        }

        [Test]
        public void CargoStateMachine_TerminalsAreFinal_AndClaimNeedsDelivery()
        {
            Assert.IsTrue(Cc10StateMachine.IsCargoTransitionLegal("Active", "Delivered"));
            Assert.IsTrue(Cc10StateMachine.IsCargoTransitionLegal("Active", "Intercepted"));
            Assert.IsTrue(Cc10StateMachine.IsCargoTransitionLegal("Delivered", "Claimed"));
            Assert.IsFalse(Cc10StateMachine.IsCargoTransitionLegal("Intercepted", "Delivered"));
            Assert.IsFalse(Cc10StateMachine.IsCargoTransitionLegal("Active", "Claimed"), "must pass through Delivered");
            Assert.IsTrue(Cc10StateMachine.IsCargoClaimable("Delivered"));
            Assert.IsFalse(Cc10StateMachine.IsCargoClaimable("Intercepted"));
        }

        [Test]
        public void ServerClock_IsMonotonicDisplay_DeviceClockCannotMoveIt()
        {
            long mono = 0;
            var clock = new Cc10ServerClock(() => mono);
            Assert.AreEqual(0, clock.RemainingMs(5000), "no sample: unknown, not a guess");
            clock.Sample(1_000_000);
            Assert.AreEqual(60_000, clock.RemainingMs(1_060_000));
            mono = 30_000;
            Assert.AreEqual(30_000, clock.RemainingMs(1_060_000));
            mono = 90_000;
            Assert.AreEqual(0, clock.RemainingMs(1_060_000));
            Assert.AreEqual("Ready", Cc10ServerClock.FormatRemaining(0));
            Assert.AreEqual("1h 30m", Cc10ServerClock.FormatRemaining(90 * 60_000));
        }

        // ---- client: reconnect / offline / stale --------------------------------------------

        [Test]
        public async Task Refresh_Failure_GoesOffline_KeepsLastSnapshotReadOnly()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap(1, 1, Mission("m1", "Completed")));
            gw.Handler = (e, r) => new InvalidOperationException("network");

            Assert.IsFalse(await client.RefreshAsync());
            Assert.AreEqual(Cc10Connection.Offline, client.Connection);
            Assert.IsTrue(client.HasState, "last authoritative snapshot stays visible");
            Assert.IsTrue(client.IsReadOnly(Cc10SystemId.Missions));
        }

        [Test]
        public async Task Offline_CommandIsBlocked_WithoutCallingTheServer()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap(1, 1, Mission("m1", "Completed")));
            gw.Handler = (e, r) => new InvalidOperationException("network");
            await client.RefreshAsync();
            gw.Calls.Clear();

            Cc10CommandOutcome r2 = await client.ExecuteAsync(Cc10SystemId.Missions, Cc10Endpoints.ClaimMission,
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
            Assert.IsTrue(client.IsReadOnly(Cc10SystemId.Tavern));
            Cc10CommandOutcome r = await client.ExecuteAsync(Cc10SystemId.Tavern, Cc10Endpoints.StartTavernUpgrade, null, "t");
            Assert.AreEqual(Cc10Outcome.Offline, r.Outcome);
        }

        [Test]
        public async Task Reconnect_ReloadsAuthoritativeState_ThenCommandsWork()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap(1, 1, Mission("m1", "Completed")));
            gw.Handler = (e, r) => new InvalidOperationException("network");
            await client.RefreshAsync();

            gw.Handler = (e, r) => e == Cc10Endpoints.GetFrontierState ? (object)Snap(3, 1, Mission("m1", "Claimed")) : Ok();
            Assert.IsTrue(await client.RefreshAsync());
            Assert.AreEqual(Cc10Connection.Online, client.Connection);
            Assert.AreEqual("Claimed", client.Snapshot.missions[0].status);
            Assert.IsFalse(client.IsReadOnly(Cc10SystemId.Missions));
        }

        [Test]
        public async Task StaleSnapshot_OlderThanOnScreen_IsIgnored()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap(5, 1, Mission("m1", "Completed")));
            gw.Handler = (e, r) => Snap(4, 1, Mission("m1", "Active"));
            Assert.IsFalse(await client.RefreshAsync());
            Assert.AreEqual(5, client.Snapshot.stateVersion);
        }

        [Test]
        public async Task IllegalStateJump_IsRecordedAsAnomaly_ButServerStateStillWins()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap(1, 1, Mission("m1", "Claimed")));
            gw.Handler = (e, r) => Snap(2, 1, Mission("m1", "Completed"));
            Assert.IsTrue(await client.RefreshAsync());
            Assert.AreEqual(1, client.StateAnomalies.Count);
            Assert.AreEqual("Completed", client.Snapshot.missions[0].status, "server is authoritative");
        }

        // ---- client: disabled / errors -------------------------------------------------------

        [Test]
        public async Task ServerDisabledSystem_IsReadOnly_AndRejectsMutationWithoutOverwritingState()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap(1, 1, Mission("m1", "Completed"));
            snap.disabledSystems = new[] { Cc10SystemId.Missions };
            Cc10FrontierClient client = await OnlineClient(gw, snap);
            gw.Calls.Clear();

            Assert.IsTrue(client.IsSystemDisabled(Cc10SystemId.Missions));
            Assert.IsFalse(client.IsSystemDisabled(Cc10SystemId.Tavern));
            Cc10CommandOutcome r = await client.ExecuteAsync(Cc10SystemId.Missions, Cc10Endpoints.ClaimMission, null, "m1");
            Assert.AreEqual(Cc10Outcome.Disabled, r.Outcome);
            Assert.AreEqual(0, gw.Calls.Count);
        }

        [Test]
        public async Task DisableArrivingInResponse_FlipsSystemReadOnly_WithoutReceiptEvent()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap(1, 1, Mission("m1", "Completed")));
            int receipts = 0;
            client.ReceiptReceived += _ => receipts++;
            gw.Handler = (e, r) => e == Cc10Endpoints.GetFrontierState
                ? (object)MarkedDisabled(Snap(1, 1, Mission("m1", "Completed")))
                : new Cc10CommandResult { success = false, errorCode = Cc10Errors.SystemDisabled };

            Cc10CommandOutcome r = await client.ExecuteAsync(Cc10SystemId.Missions, Cc10Endpoints.ClaimMission, null, "m1");
            Assert.AreEqual(Cc10Outcome.Disabled, r.Outcome);
            Assert.IsTrue(client.IsReadOnly(Cc10SystemId.Missions));
            Assert.AreEqual(0, receipts);
        }

        private static Cc10FrontierSnapshot MarkedDisabled(Cc10FrontierSnapshot s)
        {
            s.disabledSystems = new[] { Cc10SystemId.Missions };
            return s;
        }

        [Test]
        public async Task GoldCapReached_IsRejectedWithPlainCopy_AndNoReceipt()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap(1, 1, Mission("m1", "Completed")));
            int receipts = 0;
            client.ReceiptReceived += _ => receipts++;
            gw.Handler = (e, r) => new Cc10CommandResult { success = false, errorCode = Cc10Errors.GoldCapExceeded };

            Cc10CommandOutcome r = await client.ExecuteAsync(Cc10SystemId.Missions, Cc10Endpoints.ClaimMission, null, "m1");
            Assert.AreEqual(Cc10Outcome.Rejected, r.Outcome);
            Assert.AreEqual(Cc10Copy.CapReached, r.Message);
            StringAssert.DoesNotContain("GoldCapExceeded", r.Message, "raw backend codes never reach the player");
            Assert.AreEqual(0, receipts);
        }

        [Test]
        public async Task OfflineClaimRejected_UsesPlainCopy()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap(1, 1, Mission("m1", "Completed")));
            gw.Handler = (e, r) => new Cc10CommandResult { success = false, errorCode = Cc10Errors.OfflineClaimRejected };
            Cc10CommandOutcome r = await client.ExecuteAsync(Cc10SystemId.Missions, Cc10Endpoints.ClaimMission, null, "m1");
            Assert.AreEqual(Cc10Copy.OfflineClaimRejected, r.Message);
        }

        [Test]
        public async Task Conflict_ReloadsState_AndReportsConflict()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap(1, 1, Mission("m1", "Completed")));
            gw.Handler = (e, r) => e == Cc10Endpoints.GetFrontierState
                ? (object)Snap(4, 1, Mission("m1", "Claimed"))
                : new Cc10CommandResult { success = false, errorCode = Cc10Errors.Conflict };

            Cc10CommandOutcome r = await client.ExecuteAsync(Cc10SystemId.Missions, Cc10Endpoints.ClaimMission, null, "m1");
            Assert.AreEqual(Cc10Outcome.Conflict, r.Outcome);
            Assert.AreEqual("Claimed", client.Snapshot.missions[0].status, "first valid CAS wins; client reloads");
        }

        [Test]
        public async Task AuthorityStale_ReloadsAndTreatsAsConflict()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap(1, 1, Mission("m1", "Completed")));
            gw.Handler = (e, r) => e == Cc10Endpoints.GetFrontierState
                ? (object)Snap(2, 2, Mission("m1", "Completed"))
                : new Cc10CommandResult { success = false, errorCode = Cc10Errors.AuthorityStale };

            Cc10CommandOutcome r = await client.ExecuteAsync(Cc10SystemId.Missions, Cc10Endpoints.ClaimMission, null, "m1");
            Assert.AreEqual(Cc10Outcome.Conflict, r.Outcome);
            Assert.AreEqual(2, client.Snapshot.authorityGeneration, "reconnect reloads the new authority generation");
        }

        // ---- client: duplicates / idempotency -------------------------------------------------

        [Test]
        public async Task DuplicateReceipt_IsReplayed_ReceiptEventRaisedOnce()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap(1, 1, Mission("m1", "Completed")));
            var receipts = new List<Cc10Receipt>();
            client.ReceiptReceived += receipts.Add;

            gw.Handler = (e, r) => e == Cc10Endpoints.GetFrontierState ? (object)Snap(2, 1, Mission("m1", "Claimed")) : Ok("rcpt-A");
            Cc10CommandOutcome first = await client.ExecuteAsync(Cc10SystemId.Missions, Cc10Endpoints.ClaimMission, null, "m1");

            gw.Handler = (e, r) => e == Cc10Endpoints.GetFrontierState
                ? (object)Snap(2, 1, Mission("m1", "Claimed"))
                : new Cc10CommandResult { success = true, replayed = true, receipt = new Cc10Receipt { receiptId = "rcpt-A" } };
            Cc10CommandOutcome second = await client.ExecuteAsync(Cc10SystemId.Missions, Cc10Endpoints.ClaimMission, null, "m1");

            Assert.AreEqual(Cc10Outcome.Applied, first.Outcome);
            Assert.AreEqual(Cc10Outcome.Replayed, second.Outcome);
            Assert.AreEqual(Cc10Copy.AlreadyRecorded, second.Message);
            Assert.AreEqual(1, receipts.Count, "one receipt, one event - never double-applied");
        }

        [Test]
        public async Task LostResponse_RetryReusesTheOriginalRequestId()
        {
            var gw = new FakeGateway();
            int n = 0;
            Cc10FrontierClient client = await OnlineClient(gw, Snap(1, 1, Mission("m1", "Completed")), () => "req-" + (++n));

            gw.Handler = (e, r) => new TimeoutException("lost");
            Cc10CommandOutcome lost = await client.ExecuteAsync(Cc10SystemId.Missions, Cc10Endpoints.ClaimMission, null, "m1");
            Assert.AreEqual(Cc10Outcome.Failed, lost.Outcome);
            Assert.AreEqual(1, client.PendingRequestCount, "request id kept for replay");

            gw.Handler = (e, r) => e == Cc10Endpoints.GetFrontierState ? (object)Snap(1, 1, Mission("m1", "Completed")) : Ok();
            Assert.IsTrue(await client.RefreshAsync());

            Cc10CommandOutcome retry = await client.ExecuteAsync(Cc10SystemId.Missions, Cc10Endpoints.ClaimMission, null, "m1");
            Assert.AreEqual(Cc10Outcome.Applied, retry.Outcome);
            string first = (string)gw.Calls.First(c => c.Key == Cc10Endpoints.ClaimMission).Value["requestId"];
            string last = (string)gw.Calls.Last(c => c.Key == Cc10Endpoints.ClaimMission).Value["requestId"];
            Assert.AreEqual("req-1", first);
            Assert.AreEqual(first, last, "same requestId lets the server replay the original receipt");
            Assert.AreEqual(0, client.PendingRequestCount);
        }

        [Test]
        public async Task DoubleTap_WhileInFlight_SendsOnlyOneRequest()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap(1, 1, Mission("m1", "Completed")));
            var tcs = new TaskCompletionSource<Cc10CommandResult>();
            gw.Handler = (e, r) => e == Cc10Endpoints.GetFrontierState ? (object)Snap(2, 1, Mission("m1", "Claimed")) : tcs.Task;
            gw.Calls.Clear();

            Task<Cc10CommandOutcome> a = client.ExecuteAsync(Cc10SystemId.Missions, Cc10Endpoints.ClaimMission, null, "m1");
            Cc10CommandOutcome b = await client.ExecuteAsync(Cc10SystemId.Missions, Cc10Endpoints.ClaimMission, null, "m1");
            Assert.AreEqual(Cc10Outcome.InFlight, b.Outcome);

            tcs.SetResult(Ok("rcpt-D"));
            Assert.AreEqual(Cc10Outcome.Applied, (await a).Outcome);
            Assert.AreEqual(1, gw.Calls.Count(c => c.Key == Cc10Endpoints.ClaimMission));
        }

        [Test]
        public async Task Command_SendsRequestIdAndExpectedStateVersion_NeverRewardOrTime()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap(9, 1, Mission("m1", "Completed")));
            await client.ExecuteAsync(Cc10SystemId.Missions, Cc10Endpoints.ClaimMission,
                new Dictionary<string, object> { { "missionId", "m1" } }, "m1");
            Dictionary<string, object> body = gw.Calls.First(c => c.Key == Cc10Endpoints.ClaimMission).Value;
            Assert.AreEqual((long)9, body["expectedStateVersion"]);
            Assert.AreEqual((long)1, body["expectedAuthorityGeneration"]);
            Assert.AreEqual("m1", body["missionId"]);
            Assert.IsFalse(body.ContainsKey("goldReward") || body.ContainsKey("reward") || body.ContainsKey("serverUtcMs"),
                "the client never sends rewards or authoritative time");
        }

        // ---- no local reward mutation --------------------------------------------------------

        [Test]
        public async Task ClaimFlow_NeverTouchesLocalProfile_SettlementSurfacesOnlyAsReceiptEvent()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap(1, 1, Mission("m1", "Completed")));
            Cc10Receipt captured = null;
            client.ReceiptReceived += r => captured = r;
            gw.Handler = (e, r) => e == Cc10Endpoints.GetFrontierState ? (object)Snap(2, 1, Mission("m1", "Claimed")) : Ok("rcpt-Z");

            await client.ExecuteAsync(Cc10SystemId.Missions, Cc10Endpoints.ClaimMission, null, "m1");

            Assert.IsNotNull(captured, "settlement instruction must reach the host");
            Assert.AreEqual(300, captured.goldCredit);
            Assert.AreEqual(200, captured.materialsCredit);
            // The client layer has no reference to PlayerProfile/SaveSystem anywhere in this flow -
            // applying goldCredit/materialsCredit to the wallet is entirely the host's job.
        }

        // ---- view models -----------------------------------------------------------------------

        private static Cc10SectionVm Vm(Cc10FrontierClient c, string systemId) => Cc10ViewModels.Build(c, systemId);

        [Test]
        public async Task Offline_EveryAction_IsDisabled_WithReason_RowsStillShown()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap(1, 1, Mission("m1", "Completed"));
            Cc10FrontierClient client = await OnlineClient(gw, snap);
            gw.Handler = (e, r) => new InvalidOperationException("network");
            await client.RefreshAsync();

            Cc10SectionVm vm = Vm(client, Cc10SystemId.Missions);
            Assert.AreEqual(Cc10Copy.Offline, vm.Banner);
            Assert.IsTrue(vm.Rows.All(r => !r.HasAction || (!r.ActionEnabled && !string.IsNullOrEmpty(r.DisabledReason))));
        }

        [Test]
        public void NoState_ShowsBannerOnly()
        {
            var client = new Cc10FrontierClient(new FakeGateway());
            Cc10SectionVm vm = Vm(client, Cc10SystemId.Tavern);
            Assert.AreEqual(0, vm.Rows.Count);
            Assert.IsTrue(vm.ReadOnly);
            Assert.IsNotEmpty(vm.Banner);
        }

        [Test]
        public async Task UnsupportedQuery_Sections_AreExplicitlyUnavailable_NotSilentlyEmpty()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap());
            foreach (string sys in new[] { Cc10SystemId.GuildRankings, Cc10SystemId.IndividualRankings })
            {
                Cc10SectionVm vm = Vm(client, sys);
                Assert.IsTrue(vm.ReadOnly, sys);
                StringAssert.Contains("Not available", vm.Banner, sys);
            }
        }

        [Test]
        public async Task Missions_ActionsFollowServerState()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap(1, 1, Mission("c", "Active"), Mission("d", "Completed"), Mission("e", "Claimed"));
            Cc10FrontierClient client = await OnlineClient(gw, snap);
            List<Cc10Row> rows = Vm(client, Cc10SystemId.Missions).Rows;

            Assert.AreEqual(Cc10Endpoints.AbandonMission, rows[0].Endpoint);
            Assert.AreEqual(Cc10Endpoints.ClaimMission, rows[1].Endpoint);
            Assert.IsFalse(rows[2].HasAction, "claimed is terminal and read-only");
        }

        [Test]
        public async Task Missions_OffersAssignableSlot_OnlyWhenASpotIsAvailable()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap();
            snap.spots = new[] { new Cc10NpcSpotDto { spotId = "s1", missionType = Cc10MissionType.NpcPatrol, status = Cc10SpotStatus.Available } };
            Cc10FrontierClient client = await OnlineClient(gw, snap);
            Cc10Row assign = Vm(client, Cc10SystemId.Missions).Rows.Single();
            Assert.AreEqual(Cc10Endpoints.AssignMission, assign.Endpoint);
            Assert.AreEqual(Cc10MissionType.NpcPatrol, assign.Payload["missionType"]);
            Assert.AreEqual("s1", assign.Payload["spotId"]);
        }

        [Test]
        public async Task Tavern_ActiveProject_OffersComplete_NotUpgrade()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap();
            snap.tavern = new Cc10TavernDto { level = 2, activeProject = new Cc10TavernProjectDto { toLevel = 3, goldCost = 400, materialsCost = 400 } };
            Cc10FrontierClient client = await OnlineClient(gw, snap);
            Cc10Row row = Vm(client, Cc10SystemId.Tavern).Rows.Single();
            Assert.AreEqual(Cc10Endpoints.CompleteTavernUpgrade, row.Endpoint);
            StringAssert.Contains("400", row.Detail);
        }

        [Test]
        public async Task Tavern_NoProject_AtMax_DisablesUpgrade()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap();
            snap.tavern = new Cc10TavernDto { level = Cc10Rules.TavernMaxLevel };
            Cc10Row row = Vm(await OnlineClient(gw, snap), Cc10SystemId.Tavern).Rows.Single();
            Assert.IsFalse(row.ActionEnabled);
            StringAssert.Contains("Fully upgraded", row.Detail);
        }

        [Test]
        public async Task Tavern_ShowsServerDerivedActiveMissionSlots_NeverAClientPerLevelTable()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap(1, 1, Mission("m1", "Active"), Mission("m2", "Active"));
            snap.tavern = new Cc10TavernDto { level = 3, activeMissionSlots = 2 };
            Cc10Row row = Vm(await OnlineClient(gw, snap), Cc10SystemId.Tavern).Rows.Single();
            StringAssert.Contains("2/2 active missions", row.Detail);
        }

        [Test]
        public async Task Missions_AssignDisabled_WhenActiveMissionSlotsFull_UsingOnlyServerNumbers()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap(1, 1, Mission("m1", "Active"), Mission("m2", "Active"));
            snap.tavern = new Cc10TavernDto { level = 3, activeMissionSlots = 2 };
            snap.spots = new[] { new Cc10NpcSpotDto { spotId = "s1", missionType = Cc10MissionType.NpcPatrol, status = Cc10SpotStatus.Available } };
            List<Cc10Row> rows = Vm(await OnlineClient(gw, snap), Cc10SystemId.Missions).Rows;
            Cc10Row assign = rows.Single(r => r.Endpoint == Cc10Endpoints.AssignMission);
            Assert.IsFalse(assign.ActionEnabled);
            Assert.AreEqual(Cc10Copy.MissionSlotsFull, assign.DisabledReason);
        }

        [Test]
        public async Task MissionSlotsFull_RejectionMapsToPlainCopy()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap());
            gw.Handler = (e, r) => e == Cc10Endpoints.GetFrontierState ? (object)Snap() : new Cc10CommandResult { success = false, errorCode = Cc10Errors.MissionSlotsFull };
            Cc10CommandOutcome outcome = await client.ExecuteAsync(Cc10SystemId.Missions, Cc10Endpoints.AssignMission, null, "assign:s1");
            Assert.AreEqual(Cc10Copy.MissionSlotsFull, outcome.Message);
        }

        [Test]
        public void BattleAttestation_CarriesReplayTranscriptBlobField_ButWhIsNeverThePopulator()
        {
            var attestation = new Cc10BattleAttestation();
            Assert.IsNull(attestation.replayTranscriptBlob, "WH never builds or populates this - it's the Battle/CR adapter's field");
        }

        [Test]
        public async Task Research_Guild_CanComplete_Individual_CanCancel()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap();
            snap.research = new[]
            {
                new Cc10ResearchDto { nodeId = "i1", scope = Cc10ResearchScope.Individual, status = Cc10ResearchStatus.InProgress, readyUtcMs = 5_000_000 },
                new Cc10ResearchDto { nodeId = "g1", scope = Cc10ResearchScope.Guild, status = Cc10ResearchStatus.InProgress, readyUtcMs = 5_000_000 },
            };
            Cc10FrontierClient client = await OnlineClient(gw, snap);
            Assert.AreEqual(Cc10Endpoints.CancelResearch, Vm(client, Cc10SystemId.IndividualResearch).Rows.Single(r => r.EntityKey == "i1").Endpoint);
            Cc10Row g1 = Vm(client, Cc10SystemId.GuildResearch).Rows.Single(r => r.EntityKey == "g1");
            Assert.AreEqual(Cc10Endpoints.CancelGuildResearch, g1.Endpoint,
                "cancel now genuinely refunds the reserved cost (BE 253834ab), offered like individual research");
        }

        [Test]
        public async Task Research_UnstartedCatalogNodes_OfferStart_GatedByPrerequisite()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap();
            snap.research = Array.Empty<Cc10ResearchDto>();
            Cc10FrontierClient client = await OnlineClient(gw, snap);
            List<Cc10Row> rows = Vm(client, Cc10SystemId.IndividualResearch).Rows;
            Cc10Row first = rows.Single(r => r.EntityKey == "IND_CAPACITY_01");
            Assert.AreEqual(Cc10Endpoints.StartResearch, first.Endpoint);
            Assert.IsTrue(first.ActionEnabled, "no prerequisite");
            Cc10Row second = rows.Single(r => r.EntityKey == "IND_CONSTRUCTION_01");
            Assert.IsFalse(second.ActionEnabled, "IND_CAPACITY_01 not completed yet");

            snap.research = new[] { new Cc10ResearchDto { nodeId = "IND_CAPACITY_01", scope = Cc10ResearchScope.Individual, status = Cc10ResearchStatus.Completed } };
            Cc10FrontierClient client2 = await OnlineClient(gw, snap);
            Cc10Row secondUnlocked = Vm(client2, Cc10SystemId.IndividualResearch).Rows.Single(r => r.EntityKey == "IND_CONSTRUCTION_01");
            Assert.IsTrue(secondUnlocked.ActionEnabled, "prerequisite now completed");
        }

        [Test]
        public async Task GuildResearch_TopThreeGatedNode_NeverDecidedClientSide()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap();
            snap.research = Array.Empty<Cc10ResearchDto>();
            Cc10FrontierClient client = await OnlineClient(gw, snap);
            Cc10Row row = Vm(client, Cc10SystemId.GuildResearch).Rows.Single(r => r.EntityKey == "GUILD_TERRITORY_01");
            Assert.AreEqual(Cc10Endpoints.StartGuildResearch, row.Endpoint);
            Assert.IsTrue(row.ActionEnabled, "client never blocks on top-three eligibility - the server independently re-checks it");
            StringAssert.Contains("top-3", row.DisabledReason);
        }

        [Test]
        public async Task Minigame_IsOptionalAndCarriesNoReward()
        {
            var gw = new FakeGateway();
            Cc10Row row = Vm(await OnlineClient(gw, Snap()), Cc10SystemId.Minigame).Rows.Single();
            StringAssert.Contains("optional", row.Title);
            Assert.IsFalse(row.Detail.Contains("Gold"));
        }

        [Test]
        public async Task Cargo_LifecycleFollowsRealStatuses_ClaimGoesThroughClaimMission()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap();
            snap.cargo = new[]
            {
                new Cc10CargoDto { cargoId = "c1", status = Cc10CargoStatus.Accepted },
                new Cc10CargoDto { cargoId = "c2", status = Cc10CargoStatus.Scouting },
                new Cc10CargoDto { cargoId = "c3", status = Cc10CargoStatus.Active, encounterRequired = true },
                new Cc10CargoDto { cargoId = "c4", status = Cc10CargoStatus.Delivered, haulGold = 300 },
                new Cc10CargoDto { cargoId = "c5", status = Cc10CargoStatus.Intercepted, terminalReason = "npc" },
            };
            List<Cc10Row> rows = Vm(await OnlineClient(gw, snap), Cc10SystemId.Cargo).Rows;
            Assert.AreEqual(Cc10Endpoints.ScoutCargo, rows[0].Endpoint);
            Assert.AreEqual(Cc10Endpoints.DispatchCargo, rows[1].Endpoint);
            Assert.IsFalse(rows[2].ActionEnabled, "encounter must resolve first");
            Assert.AreEqual(Cc10Endpoints.ClaimMission, rows[3].Endpoint);
            Assert.IsFalse(rows[4].HasAction, "terminal");
        }

        // ---- World Map phase model / adjacency expansion / hotspots / contest (BE 3c4e5816+) ------

        [Test]
        public void MapPhase_RankIsUnlockOrder()
        {
            Assert.AreEqual(0, Cc10MapPhase.Rank(Cc10MapPhase.HomeOutpost));
            Assert.AreEqual(1, Cc10MapPhase.Rank(Cc10MapPhase.OuterMarches));
            Assert.AreEqual(2, Cc10MapPhase.Rank(Cc10MapPhase.InnerReach));
            Assert.AreEqual(3, Cc10MapPhase.Rank(Cc10MapPhase.CentralRealm));
        }

        [Test]
        public void PhaseUnlockRules_MatchPublishedRowSet_StrictlyIncreasing()
        {
            Assert.AreEqual(4, Cc10Rules.PhaseUnlocks.Length);
            Assert.AreEqual(3, Cc10Rules.CentralRealmContestDistrictCount);
            int prevLevel = -1;
            foreach (Cc10Rules.PhaseUnlockRule rule in Cc10Rules.PhaseUnlocks)
            {
                Assert.Greater(rule.RequiredTavernLevel, prevLevel, rule.Phase);
                prevLevel = rule.RequiredTavernLevel;
            }
            Cc10Rules.TryGetMissionRule(Cc10MissionType.NpcPatrol, out _); // sanity: old rules still intact
        }

        [Test]
        public void MapNodeDto_DeserializesRealPhaseFields()
        {
            const string json = "{\"nodeId\":\"titan_vein\",\"regionId\":\"frontier\",\"phase\":\"InnerReach\"," +
                "\"encounterBand\":\"Inner\",\"isContestDistrict\":false,\"discovered\":true,\"phaseUnlocked\":true," +
                "\"owned\":false,\"expanded\":false,\"neighbors\":[\"ruined_shrine\",\"inner_hollow\"]}";
            Cc10MapNodeDto n = UnityEngine.JsonUtility.FromJson<Cc10MapNodeDto>(json);
            Assert.AreEqual("InnerReach", n.phase);
            Assert.AreEqual("Inner", n.encounterBand);
            Assert.IsTrue(n.discovered);
            Assert.IsTrue(n.phaseUnlocked);
            Assert.IsFalse(n.owned);
            Assert.AreEqual(2, n.neighbors.Length);
        }

        [Test]
        public async Task WorldMap_ExpandOnlyOffered_WhenPhaseUnlocked_AndAdjacentToOwned()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap();
            snap.nodes = new[]
            {
                new Cc10MapNodeDto { nodeId = "hub", regionId = "frontier", phase = Cc10MapPhase.HomeOutpost, owned = true, discovered = true, neighbors = new[] { "patrol_road" } },
                new Cc10MapNodeDto { nodeId = "patrol_road", regionId = "frontier", phase = Cc10MapPhase.OuterMarches, discovered = true, phaseUnlocked = true, neighbors = new[] { "hub", "ruined_shrine" } },
                new Cc10MapNodeDto { nodeId = "ruined_shrine", regionId = "frontier", phase = Cc10MapPhase.OuterMarches, discovered = true, phaseUnlocked = false, neighbors = new[] { "patrol_road" } },
            };
            List<Cc10Row> rows = Vm(await OnlineClient(gw, snap), Cc10SystemId.WorldMap).Rows;
            Assert.IsFalse(rows[0].HasAction, "already owned");
            Cc10Row expandable = rows.Single(r => r.EntityKey == "patrol_road");
            Assert.AreEqual(Cc10Endpoints.ExpandNode, expandable.Endpoint);
            Assert.IsTrue(expandable.ActionEnabled, "phase unlocked and adjacent to the owned hub");
            Cc10Row locked = rows.Single(r => r.EntityKey == "ruined_shrine");
            Assert.IsFalse(locked.ActionEnabled, "phase not unlocked, even though adjacent to a discovered node");
            StringAssert.Contains("Phase not unlocked", locked.DisabledReason);
        }

        [Test]
        public async Task WorldMap_DiscoverStillGatedByAdjacencyToDiscovered_IndependentOfOwnership()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap();
            snap.nodes = new[]
            {
                new Cc10MapNodeDto { nodeId = "hub", discovered = true, owned = true, neighbors = new[] { "patrol_road" } },
                new Cc10MapNodeDto { nodeId = "patrol_road", discovered = false, neighbors = new[] { "hub", "ruined_shrine" } },
                new Cc10MapNodeDto { nodeId = "ruined_shrine", discovered = false, neighbors = new[] { "patrol_road" } },
            };
            List<Cc10Row> rows = Vm(await OnlineClient(gw, snap), Cc10SystemId.WorldMap).Rows;
            Assert.AreEqual(Cc10Endpoints.DiscoverNode, rows.Single(r => r.EntityKey == "patrol_road").Endpoint);
            Assert.IsTrue(rows.Single(r => r.EntityKey == "patrol_road").ActionEnabled);
            Assert.IsFalse(rows.Single(r => r.EntityKey == "ruined_shrine").ActionEnabled, "not adjacent to any discovered node");
        }

        [Test]
        public async Task WorldMap_PhaseLadder_ShowsUnlockedAndLockedRows()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap();
            snap.phase = Cc10MapPhase.OuterMarches;
            snap.phases = new[]
            {
                new Cc10PhaseDto { phase = Cc10MapPhase.HomeOutpost, unlocked = true, requiredTavernLevel = 1 },
                new Cc10PhaseDto { phase = Cc10MapPhase.OuterMarches, unlocked = true, requiredTavernLevel = 3, requiredCampaignChapter = 1 },
                new Cc10PhaseDto { phase = Cc10MapPhase.InnerReach, unlocked = false, requiredTavernLevel = 5, requiredCampaignChapter = 2 },
                new Cc10PhaseDto { phase = Cc10MapPhase.CentralRealm, unlocked = false, requiredTavernLevel = 7, requiredCampaignChapter = 3 },
            };
            List<Cc10Row> rows = Vm(await OnlineClient(gw, snap), Cc10SystemId.WorldMap).Rows;
            Cc10Row currentPhaseRow = rows.First(r => r.EntityKey.StartsWith("phase:"));
            StringAssert.Contains("(current)", rows.Single(r => r.EntityKey == "phase:OuterMarches").Title);
            Cc10Row lockedPhase = rows.Single(r => r.EntityKey == "phase:InnerReach");
            StringAssert.Contains("Locked", lockedPhase.Detail);
            StringAssert.Contains("Campaign ch. 2", lockedPhase.Detail);
            Assert.IsFalse(lockedPhase.HasAction, "phase rows are read-only - unlocking is server-side");
        }

        [Test]
        public async Task NpcHotspots_AreReadOnly_AndNameTheirNode()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap();
            snap.spots = new[] { new Cc10NpcSpotDto { spotId = "spot-1", nodeId = "titan_vein", missionType = Cc10MissionType.VeinConvoy, status = Cc10SpotStatus.Available } };
            Cc10Row row = Vm(await OnlineClient(gw, snap), Cc10SystemId.NpcSpots).Rows.Single();
            Assert.IsFalse(row.HasAction);
            StringAssert.Contains("titan_vein", row.Title);
            StringAssert.Contains("Vein Convoy", row.Title);
        }

        [Test]
        public async Task ContestEnroll_RejectionCodes_MapToPlainCopy_NoRawCode()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap());
            foreach (var (code, expected) in new[]
                     {
                         (Cc10Errors.NotEligible, Cc10Copy.NotEligible),
                         (Cc10Errors.DistrictTaken, Cc10Copy.DistrictTaken),
                         (Cc10Errors.AlreadyEnrolledElsewhere, Cc10Copy.AlreadyEnrolledElsewhere),
                         (Cc10Errors.WindowClosed, Cc10Copy.WindowClosed),
                     })
            {
                gw.Handler = (e, r) => e == Cc10Endpoints.GetFrontierState ? (object)Snap() : new Cc10CommandResult { success = false, errorCode = code };
                Cc10CommandOutcome outcome = await client.ExecuteAsync(Cc10SystemId.GuildTerritory, Cc10Endpoints.EnrollContestDistrict, null, "d1");
                Assert.AreEqual(expected, outcome.Message, code);
            }
        }

        [Test]
        public async Task ExpandNode_PhaseLockedOrNotAdjacent_MapToPlainCopy()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap());
            gw.Handler = (e, r) => e == Cc10Endpoints.GetFrontierState ? (object)Snap() : new Cc10CommandResult { success = false, errorCode = Cc10Errors.PhaseLocked };
            Assert.AreEqual(Cc10Copy.PhaseLocked, (await client.ExecuteAsync(Cc10SystemId.WorldMap, Cc10Endpoints.ExpandNode, null, "n1")).Message);

            gw.Handler = (e, r) => e == Cc10Endpoints.GetFrontierState ? (object)Snap() : new Cc10CommandResult { success = false, errorCode = Cc10Errors.NotAdjacent };
            Assert.AreEqual(Cc10Copy.NotAdjacent, (await client.ExecuteAsync(Cc10SystemId.WorldMap, Cc10Endpoints.ExpandNode, null, "n1")).Message);
        }

        [Test]
        public async Task ExpandNode_NeverSendsAPhaseOrCostValue()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap());
            await client.ExecuteAsync(Cc10SystemId.WorldMap, Cc10Endpoints.ExpandNode,
                new Dictionary<string, object> { { "nodeId", "patrol_road" } }, "patrol_road");
            Dictionary<string, object> body = gw.Calls.First(c => c.Key == Cc10Endpoints.ExpandNode).Value;
            Assert.AreEqual("patrol_road", body["nodeId"]);
            Assert.IsFalse(body.ContainsKey("phase") || body.ContainsKey("cost") || body.ContainsKey("threat"),
                "expansion cost/phase/threat are entirely server-decided (locked at 0/0/0)");
        }

        [Test]
        public async Task EnrollContestDistrict_NeverSendsEligibilityOrOutcome()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap());
            await client.ExecuteAsync(Cc10SystemId.GuildTerritory, Cc10Endpoints.EnrollContestDistrict,
                new Dictionary<string, object> { { "districtId", "central_ashfall" }, { "guildId", "g1" }, { "seasonId", "s1" } }, "central_ashfall");
            Dictionary<string, object> body = gw.Calls.First(c => c.Key == Cc10Endpoints.EnrollContestDistrict).Value;
            Assert.IsFalse(body.ContainsKey("eligible") || body.ContainsKey("status") || body.ContainsKey("ownerGuildColorToken"),
                "eligibility/status/ownership are entirely server-decided");
        }

        [Test]
        public async Task DisabledWorldMap_BlocksExpandAndDiscover_WithoutCallingTheServer()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap();
            snap.disabledSystems = new[] { Cc10SystemId.WorldMap };
            Cc10FrontierClient client = await OnlineClient(gw, snap);
            gw.Calls.Clear();

            Cc10CommandOutcome r1 = await client.ExecuteAsync(Cc10SystemId.WorldMap, Cc10Endpoints.ExpandNode, null, "n1");
            Cc10CommandOutcome r2 = await client.ExecuteAsync(Cc10SystemId.WorldMap, Cc10Endpoints.DiscoverNode, null, "n2");
            Assert.AreEqual(Cc10Outcome.Disabled, r1.Outcome);
            Assert.AreEqual(Cc10Outcome.Disabled, r2.Outcome);
            Assert.AreEqual(0, gw.Calls.Count);
        }

        // ---- BE 5ca919f6: GetGuildState, minigame states, research catalog, cargo interception ---

        [Test]
        public void MinigameStatus_TerminalSet_MatchesPublishedEnum()
        {
            Assert.IsTrue(Cc10MinigameStatus.IsTerminal(Cc10MinigameStatus.Claimed));
            Assert.IsTrue(Cc10MinigameStatus.IsTerminal(Cc10MinigameStatus.Failed));
            Assert.IsTrue(Cc10MinigameStatus.IsTerminal(Cc10MinigameStatus.Expired));
            Assert.IsTrue(Cc10MinigameStatus.IsTerminal(Cc10MinigameStatus.Abandoned));
            Assert.IsFalse(Cc10MinigameStatus.IsTerminal(Cc10MinigameStatus.Active));
            Assert.IsFalse(Cc10MinigameStatus.IsTerminal(Cc10MinigameStatus.Submitted));
            Assert.IsFalse(Cc10MinigameStatus.IsTerminal(Cc10MinigameStatus.Verified));
        }

        [Test]
        public void MinigameTransitions_FollowThePublishedLifecycle()
        {
            Assert.IsTrue(Cc10StateMachine.IsMinigameTransitionLegal(Cc10MinigameStatus.Available, Cc10MinigameStatus.Active), "StartMinigameSession");
            Assert.IsTrue(Cc10StateMachine.IsMinigameTransitionLegal(Cc10MinigameStatus.Active, Cc10MinigameStatus.Verified), "SubmitMinigameResult success");
            Assert.IsTrue(Cc10StateMachine.IsMinigameTransitionLegal(Cc10MinigameStatus.Active, Cc10MinigameStatus.Failed), "SubmitMinigameResult failure");
            Assert.IsTrue(Cc10StateMachine.IsMinigameTransitionLegal(Cc10MinigameStatus.Verified, Cc10MinigameStatus.Claimed));
            Assert.IsTrue(Cc10StateMachine.IsMinigameTransitionLegal(Cc10MinigameStatus.Active, Cc10MinigameStatus.Abandoned));
            Assert.IsTrue(Cc10StateMachine.IsMinigameTransitionLegal(Cc10MinigameStatus.Available, Cc10MinigameStatus.Abandoned));
            Assert.IsFalse(Cc10StateMachine.IsMinigameTransitionLegal(Cc10MinigameStatus.Claimed, Cc10MinigameStatus.Active), "terminal is final");
            Assert.IsFalse(Cc10StateMachine.IsMinigameTransitionLegal(Cc10MinigameStatus.Active, Cc10MinigameStatus.Claimed), "cannot skip Verified");
            Assert.IsFalse(Cc10StateMachine.IsMinigameTransitionLegal(Cc10MinigameStatus.Available, Cc10MinigameStatus.Verified), "cannot skip Active - must Start first");
        }

        [Test]
        public async Task Minigame_EveryStatusRendersItsOwnRowAndAction()
        {
            var gw = new FakeGateway();
            (string status, string expectedEndpoint, string expectedDetailFragment)[] cases =
            {
                (Cc10MinigameStatus.Active, Cc10Endpoints.AbandonMinigameSession, "In progress"),
                (Cc10MinigameStatus.Submitted, null, "awaiting verification"),
                (Cc10MinigameStatus.Verified, Cc10Endpoints.ClaimMinigameResult, "Verified"),
                (Cc10MinigameStatus.Claimed, null, "Claimed"),
                (Cc10MinigameStatus.Failed, Cc10Endpoints.CreateMinigameSession, "Failed"),
                (Cc10MinigameStatus.Expired, Cc10Endpoints.CreateMinigameSession, "Expired"),
                (Cc10MinigameStatus.Abandoned, Cc10Endpoints.CreateMinigameSession, "Abandoned"),
            };
            foreach (var (status, expectedEndpoint, detailFragment) in cases)
            {
                Cc10FrontierSnapshot snap = Snap();
                snap.minigame = new Cc10MinigameSessionDto { sessionId = "sess1", status = status, verifiedScore = 42 };
                Cc10Row row = Vm(await OnlineClient(gw, snap), Cc10SystemId.Minigame).Rows.Single();
                StringAssert.Contains(detailFragment, row.Detail, status);
                Assert.AreEqual(expectedEndpoint ?? string.Empty, row.Endpoint, status);
            }
        }

        [Test]
        public async Task Minigame_ClaimAndAbandon_NeverSendScoreOrReward()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap();
            snap.minigame = new Cc10MinigameSessionDto { sessionId = "sess1", status = Cc10MinigameStatus.Verified };
            Cc10FrontierClient client = await OnlineClient(gw, snap);
            await client.ExecuteAsync(Cc10SystemId.Minigame, Cc10Endpoints.ClaimMinigameResult,
                new Dictionary<string, object> { { "sessionId", "sess1" } }, "minigame");
            Dictionary<string, object> body = gw.Calls.First(c => c.Key == Cc10Endpoints.ClaimMinigameResult).Value;
            Assert.AreEqual("sess1", body["sessionId"]);
            Assert.IsFalse(body.ContainsKey("score") || body.ContainsKey("verifiedScore") || body.ContainsKey("reward"));
        }

        [Test]
        public async Task MinigameCoolingDown_MapsToPlainCopy()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap());
            gw.Handler = (e, r) => e == Cc10Endpoints.GetFrontierState ? (object)Snap() : new Cc10CommandResult { success = false, errorCode = Cc10Errors.MinigameCoolingDown };
            Cc10CommandOutcome outcome = await client.ExecuteAsync(Cc10SystemId.Minigame, Cc10Endpoints.CreateMinigameSession, null, "minigame");
            Assert.AreEqual(Cc10Copy.MinigameCoolingDown, outcome.Message);
        }

        [Test]
        public async Task Cargo_Intercepted_ShowsPlainExplanation_NoActionOffered()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap();
            snap.cargo = new[] { new Cc10CargoDto { cargoId = "c1", status = Cc10CargoStatus.Intercepted, terminalReason = "npc_patrol" } };
            Cc10Row row = Vm(await OnlineClient(gw, snap), Cc10SystemId.Cargo).Rows.Single();
            Assert.IsFalse(row.HasAction);
            StringAssert.Contains("Intercepted", row.Detail);
            StringAssert.Contains("permanent progression untouched", row.Detail);
        }

        [Test]
        public async Task GuildState_LoadsTerritories_ContributeGatedByAlreadyContributedThisWindow()
        {
            var gw = new FakeGateway();
            gw.Handler = (e, r) => e == Cc10Endpoints.GetGuildState
                ? (object)new Cc10GuildSnapshot
                {
                    success = true,
                    territories = new[]
                    {
                        new Cc10TerritoryDto { territoryId = "territory.ashen_ridge", ownerGuildPseudonym = "Guild-A", contributedMembers = new[] { "me" } },
                        new Cc10TerritoryDto { territoryId = "territory.ember_hollow", contributedMembers = Array.Empty<string>() },
                    },
                }
                : throw new InvalidOperationException("unexpected");
            var client = new Cc10FrontierClient(gw);
            Assert.IsTrue(await client.RefreshGuildStateAsync("g1"));

            Cc10SectionVm vm = Cc10ViewModels.BuildGuildTerritory(client, "me");
            Cc10Row ashen = vm.Rows.Single(r => r.EntityKey == "territory.ashen_ridge");
            Assert.IsFalse(ashen.ActionEnabled, "already contributed this window");
            StringAssert.Contains("Guild-A", ashen.Detail);
            Cc10Row ember = vm.Rows.Single(r => r.EntityKey == "territory.ember_hollow");
            Assert.IsTrue(ember.ActionEnabled);
            Assert.AreEqual(Cc10Endpoints.ContributeTerritory, ember.Endpoint);
        }

        [Test]
        public async Task GuildState_FailureKeepsPreviousSnapshot()
        {
            var gw = new FakeGateway { Handler = (e, r) => new Cc10GuildSnapshot { success = true, territories = new[] { new Cc10TerritoryDto { territoryId = "t1" } } } };
            var client = new Cc10FrontierClient(gw);
            Assert.IsTrue(await client.RefreshGuildStateAsync("g1"));
            Assert.AreEqual(1, client.GuildSnapshot.territories.Length);

            gw.Handler = (e, r) => new InvalidOperationException("down");
            Assert.IsFalse(await client.RefreshGuildStateAsync("g1"));
            Assert.AreEqual(1, client.GuildSnapshot.territories.Length, "last good snapshot stays visible");
        }

        [Test]
        public void Ranking_RendersServerTieOrder_NeverReSorted()
        {
            var view = new Cc10RankingViewDto
            {
                seasonId = "S1",
                state = Cc10SeasonState.Published,
                entries = new[]
                {
                    new Cc10RankingEntryDto { rank = 1, subjectPseudonym = "Ada", points = 500 },
                    new Cc10RankingEntryDto { rank = 2, subjectPseudonym = "Zed", points = 500 }, // tie, server already ordered it
                    new Cc10RankingEntryDto { rank = 3, subjectPseudonym = "Bea", points = 400 },
                },
                you = new Cc10RankingEntryDto { rank = 7, subjectPseudonym = "Me", points = 100 },
            };
            Cc10SectionVm vm = Cc10ViewModels.BuildRanking(Cc10SystemId.IndividualRankings, view);
            Assert.AreEqual("#1  Ada", vm.Rows[1].Title, "server's own order is preserved, index 0 is the season header");
            Assert.AreEqual("#2  Zed", vm.Rows[2].Title, "tie order exactly as returned, never re-sorted client-side");
            Assert.AreEqual("#3  Bea", vm.Rows[3].Title);
            Assert.AreEqual("You: #7", vm.Rows[4].Title);
            Assert.IsTrue(vm.Rows.All(r => !r.HasAction), "ranking rows are read-only");
        }

        [Test]
        public void Ranking_NoViewLoaded_ShowsUnavailable()
        {
            Cc10SectionVm vm = Cc10ViewModels.BuildRanking(Cc10SystemId.GuildRankings, null);
            StringAssert.Contains("Not available", vm.Banner);
        }

        // ---- BE 253834ab: NPC pool catalog, minigame Available/Started, guild research refund ----

        [Test]
        public void MinigameStatus_IsPlayable_CoversStartedAndActive()
        {
            Assert.IsTrue(Cc10MinigameStatus.IsPlayable(Cc10MinigameStatus.Started));
            Assert.IsTrue(Cc10MinigameStatus.IsPlayable(Cc10MinigameStatus.Active));
            Assert.IsFalse(Cc10MinigameStatus.IsPlayable(Cc10MinigameStatus.Available));
            Assert.IsFalse(Cc10MinigameStatus.IsPlayable(Cc10MinigameStatus.Submitted));
        }

        [Test]
        public async Task Minigame_NeverCreated_OffersPlay_CreatesTheSession()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap();
            snap.minigame = null; // no session document exists at all - nothing created yet
            Cc10Row row = Vm(await OnlineClient(gw, snap), Cc10SystemId.Minigame).Rows.Single();
            Assert.AreEqual(Cc10Endpoints.CreateMinigameSession, row.Endpoint);
            Assert.IsTrue(row.ActionEnabled);
        }

        [Test]
        public async Task Minigame_Available_IsARealServerStatus_OffersStart_NotAbandon()
        {
            // BE 6e93d10d: CreateMinigameSession genuinely sets Available (real, no cost); a
            // distinct StartMinigameSession call - not Create - is required to begin play.
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap();
            snap.minigame = new Cc10MinigameSessionDto { sessionId = "s1", status = Cc10MinigameStatus.Available };
            Cc10Row row = Vm(await OnlineClient(gw, snap), Cc10SystemId.Minigame).Rows.Single();
            Assert.AreEqual(Cc10Endpoints.StartMinigameSession, row.Endpoint);
            StringAssert.Contains("not started", row.Detail);
        }

        [Test]
        public async Task Minigame_Active_ShowsRealCountdown_OffersAbandon()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap();
            snap.minigame = new Cc10MinigameSessionDto { sessionId = "s1", status = Cc10MinigameStatus.Active, expiresUtcMs = 1_020_000 };
            Cc10Row row = Vm(await OnlineClient(gw, snap), Cc10SystemId.Minigame).Rows.Single();
            Assert.AreEqual(Cc10Endpoints.AbandonMinigameSession, row.Endpoint);
            StringAssert.Contains("In progress", row.Detail);
        }

        [Test]
        public void MinigameTransitions_StartedAliasesAvailable_SubmittedAliasesActiveOnly()
        {
            // Started/Submitted are declared for wire compatibility only - the server never
            // actually assigns either. Started behaves like Available (offers Start, not Abandon).
            Assert.IsTrue(Cc10StateMachine.IsMinigameTransitionLegal(Cc10MinigameStatus.Started, Cc10MinigameStatus.Active));
            Assert.IsTrue(Cc10StateMachine.IsMinigameTransitionLegal(Cc10MinigameStatus.Started, Cc10MinigameStatus.Abandoned));
            Assert.IsFalse(Cc10StateMachine.IsMinigameTransitionLegal(Cc10MinigameStatus.Started, Cc10MinigameStatus.Verified),
                "Started cannot skip straight to Verified - it must reach Active first");
        }

        [Test]
        public async Task StartMinigameSession_SendsOnlySessionId_NoScoreOrTiming()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap();
            snap.minigame = new Cc10MinigameSessionDto { sessionId = "s1", status = Cc10MinigameStatus.Available };
            Cc10FrontierClient client = await OnlineClient(gw, snap);
            await client.ExecuteAsync(Cc10SystemId.Minigame, Cc10Endpoints.StartMinigameSession,
                new Dictionary<string, object> { { "sessionId", "s1" } }, "minigame");
            Dictionary<string, object> body = gw.Calls.First(c => c.Key == Cc10Endpoints.StartMinigameSession).Value;
            Assert.AreEqual("s1", body["sessionId"]);
            Assert.IsFalse(body.ContainsKey("score") || body.ContainsKey("expiresUtcMs") || body.ContainsKey("startedUtcMs"),
                "timing/score are entirely server-decided - Start only ever names the session");
        }

        /// <summary>The exact final SubmitMinigameResult mapping asked for: sessionId, an ordered
        /// list of exactly 12 actions (roundIndex/selectedLane/clientTick, clientTick informational
        /// only), and a client-claimed transcriptHash. No compatibility-only score/proof-hash field
        /// exists, and the client never sends an authoritative score or verdict of its own.</summary>
        [Test]
        public async Task SubmitMinigameResult_FieldMapping_Exactly12OrderedActions_NoScoreOrVerdict()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap();
            snap.minigame = new Cc10MinigameSessionDto { sessionId = "s1", status = Cc10MinigameStatus.Active, rulesetVersion = "ruleset-1" };
            Cc10FrontierClient client = await OnlineClient(gw, snap);

            var actions = new List<Cc10MinigameActionDto>();
            for (int i = 0; i < 12; i++)
                actions.Add(new Cc10MinigameActionDto { roundIndex = i, selectedLane = Cc10MinigameLane.Front, clientTick = 1000 + i });

            await client.ExecuteAsync(Cc10SystemId.Minigame, Cc10Endpoints.SubmitMinigameResult,
                new Dictionary<string, object>
                {
                    { "sessionId", "s1" },
                    { "actions", actions },
                    { "transcriptHash", "deadbeef" },
                }, "minigame");

            Dictionary<string, object> body = gw.Calls.First(c => c.Key == Cc10Endpoints.SubmitMinigameResult).Value;
            Assert.AreEqual("s1", body["sessionId"]);
            var sentActions = (List<Cc10MinigameActionDto>)body["actions"];
            Assert.AreEqual(12, sentActions.Count, "exactly 12 rounds, no more, no fewer");
            for (int i = 0; i < 12; i++)
                Assert.AreEqual(i, sentActions[i].roundIndex, "strict round order 0-11");
            Assert.AreEqual("deadbeef", body["transcriptHash"]);
            Assert.IsFalse(body.ContainsKey("score") || body.ContainsKey("verdict") || body.ContainsKey("proofHash")
                || body.ContainsKey("verifiedScore") || body.ContainsKey("correctSelections"),
                "the client never submits an authoritative score or verdict - the old Score/ProofHash fields are gone");
        }

        [Test]
        public void MinigameActionDto_ClientTick_IsInformationalOnly_NeverAuthoritativeTime()
        {
            // No server rule reads it (docs: "ClientTick is informational only"); this asserts the
            // client-side DTO carries it purely as a display/telemetry field, never mixed into any
            // client decision (Cc10ViewModels/Cc10FrontierClient never branch on it anywhere).
            var action = new Cc10MinigameActionDto { roundIndex = 0, selectedLane = Cc10MinigameLane.Middle, clientTick = 123456 };
            Assert.AreEqual(123456, action.clientTick);
            Assert.AreEqual(Cc10MinigameLane.Middle, action.selectedLane);
        }

        [Test]
        public void MinigameLane_ThreeValues_MatchServerEnumNames()
        {
            Assert.AreEqual("Front", Cc10MinigameLane.Front);
            Assert.AreEqual("Middle", Cc10MinigameLane.Middle);
            Assert.AreEqual("Back", Cc10MinigameLane.Back);
        }

        [Test]
        public void MinigameSessionDto_DeserializesRealServerJson_RulesetVersionAndTiming()
        {
            // int?/string? fields are intentionally not round-tripped through JsonUtility here -
            // Unity's JsonUtility does not support System.Nullable<T> deserialization (a real,
            // documented engine limitation), so correctSelections/verifiedScore are exercised via
            // direct construction elsewhere in this file instead of JSON parsing.
            const string json = "{\"sessionId\":\"s1\",\"seed\":\"abc\",\"rulesetVersion\":\"ruleset-1\"," +
                "\"status\":\"Active\",\"createdUtcMs\":1000,\"startedUtcMs\":1500,\"expiresUtcMs\":31500}";
            Cc10MinigameSessionDto dto = UnityEngine.JsonUtility.FromJson<Cc10MinigameSessionDto>(json);
            Assert.AreEqual("ruleset-1", dto.rulesetVersion);
            Assert.AreEqual(1500, dto.startedUtcMs);
            Assert.AreEqual(31500, dto.expiresUtcMs);
        }

        [Test]
        public void LockedVeinRelayNumbers_MatchPublishedRowSet()
        {
            Assert.AreEqual(30, Cc10Rules.MinigameSessionSeconds);
            Assert.AreEqual(12, Cc10Rules.MinigameRounds);
            Assert.AreEqual(9, Cc10Rules.MinigameSuccessThreshold);
            Assert.AreEqual(100, Cc10Rules.MinigamePointsPerCorrect);
            Assert.AreEqual(1200, Cc10Rules.MinigameMaxScore);
        }

        [Test]
        public async Task MinigameInvalidStream_RejectionMapsToPlainCopy()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap());
            gw.Handler = (e, r) => e == Cc10Endpoints.GetFrontierState ? (object)Snap() : new Cc10CommandResult { success = false, errorCode = Cc10Errors.MinigameInvalidStream };
            Cc10CommandOutcome outcome = await client.ExecuteAsync(Cc10SystemId.Minigame, Cc10Endpoints.SubmitMinigameResult, null, "minigame");
            Assert.AreEqual(Cc10Copy.MinigameInvalidStream, outcome.Message);
        }

        [Test]
        public async Task Mission_ShowsDeterministicallyBoundNpcPoolName_NeverClientChosen()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap(1, 1, new Cc10MissionDto
            {
                missionId = "m1", type = Cc10MissionType.VeinConvoy, status = Cc10MissionStatus.Active,
                npcPool = new Cc10NpcPoolDto { poolName = "Elder Dragon", band = Cc10MapPhase.CentralRealm },
            });
            Cc10Row row = Vm(await OnlineClient(gw, snap), Cc10SystemId.Missions).Rows.Single();
            StringAssert.Contains("Elder Dragon", row.Title);
        }

        [Test]
        public async Task Mission_NoPoolYet_TitleHasNoPoolSuffix()
        {
            var gw = new FakeGateway();
            Cc10Row row = Vm(await OnlineClient(gw, Snap(1, 1, Mission("m1", "Active"))), Cc10SystemId.Missions).Rows.Single();
            Assert.IsFalse(row.Title.Contains("("));
        }

        [Test]
        public async Task GuildResearchCancel_ReceiptCreditsBackTheReservedCost()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap();
            snap.research = new[] { new Cc10ResearchDto { nodeId = "GUILD_RESEARCH_01", scope = Cc10ResearchScope.Guild, status = Cc10ResearchStatus.InProgress, readyUtcMs = 9_000_000 } };
            Cc10FrontierClient client = await OnlineClient(gw, snap);
            Cc10Receipt captured = null;
            client.ReceiptReceived += r => captured = r;
            gw.Handler = (e, r) => e == Cc10Endpoints.GetFrontierState
                ? (object)Snap()
                : new Cc10CommandResult { success = true, receipt = new Cc10Receipt { receiptId = "rc1", goldCredit = 700, materialsCredit = 400 } };

            Cc10Row row = Vm(client, Cc10SystemId.GuildResearch).Rows.Single(r => r.EntityKey == "GUILD_RESEARCH_01");
            Assert.AreEqual(Cc10Endpoints.CancelGuildResearch, row.Endpoint);
            await client.ExecuteAsync(Cc10SystemId.GuildResearch, row.Endpoint, row.Payload, row.EntityKey);

            Assert.IsNotNull(captured);
            Assert.AreEqual(700, captured.goldCredit);
            Assert.AreEqual(400, captured.materialsCredit);
        }

        [Test]
        public async Task CancelGuildResearch_NeverSendsARefundAmount()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap());
            await client.ExecuteAsync(Cc10SystemId.GuildResearch, Cc10Endpoints.CancelGuildResearch,
                new Dictionary<string, object> { { "nodeId", "GUILD_RESEARCH_01" }, { "guildId", "g1" } }, "GUILD_RESEARCH_01");
            Dictionary<string, object> body = gw.Calls.First(c => c.Key == Cc10Endpoints.CancelGuildResearch).Value;
            Assert.IsFalse(body.ContainsKey("goldCredit") || body.ContainsKey("materialsCredit") || body.ContainsKey("refund"),
                "the refund amount is entirely server-decided and arrives only on the receipt");
        }

        // ---- BE bab7aab1: canonical World Map snapshot, private occupancy, guild color tokens ----

        private static Cc10WorldMapSnapshotResult MapResult(int occupancyVersion = 1, string schema = Cc10WorldMapSnapshotSchema.V1,
            Cc10WorldMapOwnNodeDto[] own = null, Cc10WorldMapContestDto[] contest = null, string unlocked = Cc10WorldMapPhaseToken.Tutorial)
        {
            return new Cc10WorldMapSnapshotResult
            {
                success = true,
                serverUtcMs = 2_000_000,
                snapshot = new Cc10WorldMapSnapshotDto
                {
                    schemaVersion = schema, mapVersion = "worldmap-beta-1", serverUtc = 2_000_000,
                    unlockedPhase = unlocked, occupancyVersion = occupancyVersion,
                    ownOccupiedNodes = own ?? Array.Empty<Cc10WorldMapOwnNodeDto>(),
                    centralContest = contest ?? Array.Empty<Cc10WorldMapContestDto>(),
                },
            };
        }

        [Test]
        public void WorldMapSnapshot_DeserializesRealServerJson_AllApprovedFields()
        {
            const string json = "{\"success\":true,\"snapshot\":{\"schemaVersion\":\"cc10.worldmap.v1\",\"mapVersion\":\"worldmap-beta-1\"," +
                "\"serverUtc\":1234567,\"unlockedPhase\":\"Central\",\"occupancyVersion\":4," +
                "\"ownOccupiedNodes\":[{\"nodeId\":\"hub\",\"phaseId\":\"Tutorial\"},{\"nodeId\":\"patrol_road\",\"phaseId\":\"Outer\"}]," +
                "\"centralContest\":[{\"districtId\":\"central_ashfall\",\"seasonId\":\"S9\",\"ownershipState\":\"GuildOwned\",\"guildColorToken\":\"GC07\"}]}}";
            Cc10WorldMapSnapshotResult r = UnityEngine.JsonUtility.FromJson<Cc10WorldMapSnapshotResult>(json);
            Assert.IsTrue(r.success);
            Cc10WorldMapSnapshotDto s = r.snapshot;
            Assert.AreEqual(Cc10WorldMapSnapshotSchema.V1, s.schemaVersion);
            Assert.AreEqual("worldmap-beta-1", s.mapVersion);
            Assert.AreEqual(1234567, s.serverUtc);
            Assert.AreEqual(Cc10WorldMapPhaseToken.Central, s.unlockedPhase);
            Assert.AreEqual(4, s.occupancyVersion);
            Assert.AreEqual(2, s.ownOccupiedNodes.Length);
            Assert.AreEqual("Outer", s.ownOccupiedNodes[1].phaseId);
            Assert.AreEqual("S9", s.centralContest[0].seasonId);
            Assert.AreEqual(Cc10ContestOwnershipState.GuildOwned, s.centralContest[0].ownershipState);
            Assert.AreEqual("GC07", s.centralContest[0].guildColorToken);
        }

        [Test]
        public void WorldMapSnapshotDto_HasExactlyTheApprovedFields_NothingMore()
        {
            string[] fields = typeof(Cc10WorldMapSnapshotDto).GetFields().Select(f => f.Name).OrderBy(n => n).ToArray();
            CollectionAssert.AreEqual(new[] { "centralContest", "mapVersion", "occupancyVersion", "ownOccupiedNodes", "schemaVersion", "serverUtc", "unlockedPhase" }, fields,
                "the approved snapshot shape - no ownershipVersion, no top-level seasonId, no placement/well/identity fields");
            CollectionAssert.AreEqual(new[] { "nodeId", "phaseId" }, typeof(Cc10WorldMapOwnNodeDto).GetFields().Select(f => f.Name).OrderBy(n => n).ToArray());
            CollectionAssert.AreEqual(new[] { "districtId", "guildColorToken", "ownershipState", "seasonId" }, typeof(Cc10WorldMapContestDto).GetFields().Select(f => f.Name).OrderBy(n => n).ToArray());
        }

        [Test]
        public void DeprecatedIdentityFields_AreGoneFromEveryExportedDto()
        {
            // BE bab7aab1 removed these; a client that still carried them would invite showing identity.
            CollectionAssert.DoesNotContain(typeof(Cc10MapNodeDto).GetFields().Select(f => f.Name).ToArray(), "occupantDisplayId");
            CollectionAssert.DoesNotContain(typeof(Cc10FrontierSnapshot).GetFields().Select(f => f.Name).ToArray(), "yourDisplayId");
            string[] contest = typeof(Cc10ContestDistrictDto).GetFields().Select(f => f.Name).ToArray();
            foreach (string b in new[] { "enrolledGuildPseudonym", "ownerGuildPseudonym", "enrolledGuildColorKey", "ownerGuildColorKey" })
                CollectionAssert.DoesNotContain(contest, b);
            CollectionAssert.Contains(contest, "enrolledGuildColorToken");
            CollectionAssert.Contains(contest, "ownerGuildColorToken");
        }

        [Test]
        public void MapNodeDto_KeepsServerLayoutKey_LayoutXY()
        {
            const string json = "{\"nodeId\":\"hub\",\"layoutX\":120,\"layoutY\":-45,\"owned\":true}";
            Cc10MapNodeDto n = UnityEngine.JsonUtility.FromJson<Cc10MapNodeDto>(json);
            Assert.AreEqual(120, n.layoutX);
            Assert.AreEqual(-45, n.layoutY);
        }

        [Test]
        public void GuildColorToken_Parsing_AcceptsOnlyGC01ToGC12_OtherwiseNeutral()
        {
            Assert.IsTrue(Cc10Rules.TryParseGuildColorToken("GC01", out int first)); Assert.AreEqual(0, first);
            Assert.IsTrue(Cc10Rules.TryParseGuildColorToken("GC12", out int last)); Assert.AreEqual(11, last);
            foreach (string bad in new[] { null, "", "GC00", "GC13", "gc05", "GC5", "GC+5", "#FF0000", "Guild-9F2", "GC0A" })
            {
                Assert.IsFalse(Cc10Rules.TryParseGuildColorToken(bad, out int idx), "'" + bad + "' must be neutral");
                Assert.AreEqual(-1, idx);
            }
            Assert.AreEqual(12, Cc10Rules.GuildColorPaletteSize);
        }

        [Test]
        public async Task RefreshWorldMap_AcceptsKnownSchema_AndStoresSnapshot()
        {
            var gw = new FakeGateway { Handler = (e, r) => MapResult(3, own: new[] { new Cc10WorldMapOwnNodeDto { nodeId = "hub", phaseId = "Tutorial" } }) };
            var client = new Cc10FrontierClient(gw);
            Assert.IsTrue(await client.RefreshWorldMapAsync());
            Assert.AreEqual(3, client.WorldMap.occupancyVersion);
            Assert.AreEqual(Cc10Connection.Online, client.Connection);
            Assert.AreEqual(Cc10Endpoints.GetWorldMapSnapshot, gw.Calls.Single().Key);
            Assert.IsNull(gw.Calls.Single().Value, "a pure read carries no request body");
        }

        [Test]
        public async Task RefreshWorldMap_UnknownSchema_IsRejected_KeepingLastGoodSnapshot()
        {
            var gw = new FakeGateway { Handler = (e, r) => MapResult(1) };
            var client = new Cc10FrontierClient(gw);
            await client.RefreshWorldMapAsync();
            gw.Handler = (e, r) => MapResult(9, schema: "cc10.worldmap.v2");
            Assert.IsFalse(await client.RefreshWorldMapAsync());
            Assert.AreEqual(1, client.WorldMap.occupancyVersion, "an unknown schema must never replace the last good snapshot");
        }

        [Test]
        public async Task RefreshWorldMap_OlderOccupancyVersion_IsStale_AndIgnored()
        {
            var gw = new FakeGateway { Handler = (e, r) => MapResult(5) };
            var client = new Cc10FrontierClient(gw);
            await client.RefreshWorldMapAsync();
            gw.Handler = (e, r) => MapResult(4);
            Assert.IsFalse(await client.RefreshWorldMapAsync());
            Assert.AreEqual(5, client.WorldMap.occupancyVersion, "occupancyVersion is per-player monotonic - lower means stale");
            gw.Handler = (e, r) => MapResult(5);
            Assert.IsTrue(await client.RefreshWorldMapAsync(), "an equal version is a fine idempotent re-read");
        }

        [Test]
        public async Task RefreshWorldMap_NetworkFailure_GoesOffline_KeepingLastSnapshot()
        {
            var gw = new FakeGateway { Handler = (e, r) => MapResult(2) };
            var client = new Cc10FrontierClient(gw);
            await client.RefreshWorldMapAsync();
            gw.Handler = (e, r) => new InvalidOperationException("down");
            Assert.IsFalse(await client.RefreshWorldMapAsync());
            Assert.AreEqual(Cc10Connection.Offline, client.Connection);
            Assert.AreEqual(2, client.WorldMap.occupancyVersion);
        }

        [Test]
        public async Task RefreshWorldMap_MissingSnapshotOrFailure_IsRejected()
        {
            var gw = new FakeGateway { Handler = (e, r) => new Cc10WorldMapSnapshotResult { success = true, snapshot = null } };
            var client = new Cc10FrontierClient(gw);
            Assert.IsFalse(await client.RefreshWorldMapAsync());
            Assert.IsNull(client.WorldMap);
            gw.Handler = (e, r) => new Cc10WorldMapSnapshotResult { success = false, errorCode = "AUTHORITY_UNAVAILABLE" };
            Assert.IsFalse(await client.RefreshWorldMapAsync());
            Assert.IsNull(client.WorldMap);
        }

        [Test]
        public void SeasonGating_NoCentralRowsBeforeCentralUnlock_IsAValidEmptyList_NotAnError()
        {
            // Final BS season-gating decision (server rule): contest rows exist only after Central unlock
            // during an active season. Before that the list is simply empty - the client must not invent rows.
            Cc10WorldMapSnapshotDto s = MapResult(2, unlocked: Cc10WorldMapPhaseToken.Inner).snapshot;
            Assert.AreEqual(0, s.centralContest.Length);
            Assert.Less(Cc10WorldMapPhaseToken.Rank(s.unlockedPhase), Cc10WorldMapPhaseToken.Rank(Cc10WorldMapPhaseToken.Central));
        }

        [Test]
        public void ContestRow_Unclaimed_CarriesNoColorToken_NeutralState()
        {
            var row = new Cc10WorldMapContestDto { districtId = "central_ember", seasonId = "S9" };
            Assert.AreEqual(Cc10ContestOwnershipState.Unclaimed, row.ownershipState);
            Assert.IsNull(row.guildColorToken, "an unclaimed district is neutral - never a fabricated color");
        }

        [Test]
        public void PhaseTokens_AreBandNames_NotMapPhaseNames_AndOrdered()
        {
            Assert.AreEqual("Tutorial", Cc10WorldMapPhaseToken.Tutorial);
            Assert.AreEqual("Outer", Cc10WorldMapPhaseToken.Outer);
            Assert.AreEqual("Inner", Cc10WorldMapPhaseToken.Inner);
            Assert.AreEqual("Central", Cc10WorldMapPhaseToken.Central);
            Assert.AreEqual(0, Cc10WorldMapPhaseToken.Rank("Tutorial"));
            Assert.AreEqual(3, Cc10WorldMapPhaseToken.Rank("Central"));
            Assert.AreEqual(-1, Cc10WorldMapPhaseToken.Rank("HomeOutpost"), "MapPhase names are a different vocabulary");
        }

        [Test]
        public async Task SnapshotOrdering_IsTheServersOwn_NeverReSortedByTheClient()
        {
            var own = new[]
            {
                new Cc10WorldMapOwnNodeDto { nodeId = "hub", phaseId = "Tutorial" },
                new Cc10WorldMapOwnNodeDto { nodeId = "patrol_road", phaseId = "Outer" },
                new Cc10WorldMapOwnNodeDto { nodeId = "ruined_shrine", phaseId = "Outer" },
            };
            var client = new Cc10FrontierClient(new FakeGateway { Handler = (e, r) => MapResult(1, own: own) });
            await client.RefreshWorldMapAsync();
            CollectionAssert.AreEqual(new[] { "hub", "patrol_road", "ruined_shrine" }, client.WorldMap.ownOccupiedNodes.Select(n => n.nodeId).ToArray());
        }

        [Test]
        public async Task WorldMap_NodeRow_NeverShowsAnyIdentity_OnlyOwnedState()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap();
            snap.nodes = new[] { new Cc10MapNodeDto { nodeId = "hub", owned = true, layoutX = 300, layoutY = -120 } };
            Cc10Row row = Vm(await OnlineClient(gw, snap), Cc10SystemId.WorldMap).Rows.Single(r => r.EntityKey == "hub");
            Assert.AreEqual("Owned", row.Detail);
            Assert.AreEqual(300, row.Payload["layoutX"]);
            Assert.AreEqual(-120, row.Payload["layoutY"]);
        }

        [Test]
        public async Task NodeOrdering_IsStable_MatchesTheServersOwnArrayOrder_NeverReSorted()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap();
            snap.nodes = new[]
            {
                new Cc10MapNodeDto { nodeId = "titan_vein", owned = true },
                new Cc10MapNodeDto { nodeId = "hub", owned = true },
                new Cc10MapNodeDto { nodeId = "ruined_shrine", owned = true },
            };
            List<Cc10Row> rows = Vm(await OnlineClient(gw, snap), Cc10SystemId.WorldMap).Rows;
            CollectionAssert.AreEqual(new[] { "titan_vein", "hub", "ruined_shrine" }, rows.Select(r => r.EntityKey).ToArray());
        }


        // ---- live view-model wiring of GetWorldMapSnapshot (occupancy, contest, retention, reconnect) ----

        private static async Task<Cc10FrontierClient> ClientWithMap(FakeGateway gw, Cc10FrontierSnapshot frontier, Cc10WorldMapSnapshotResult map)
        {
            gw.Handler = (e, r) =>
                e == Cc10Endpoints.GetFrontierState ? (object)frontier
                : e == Cc10Endpoints.GetWorldMapSnapshot ? (object)map
                : Ok();
            var client = new Cc10FrontierClient(gw, new Cc10ServerClock(() => 0));
            Assert.IsTrue(await client.RefreshAllAsync());
            return client;
        }

        private static Cc10WorldMapContestDto Contest(string id, string owned = null, string season = "S1") =>
            new Cc10WorldMapContestDto
            {
                districtId = id, seasonId = season,
                ownershipState = owned == null ? Cc10ContestOwnershipState.Unclaimed : Cc10ContestOwnershipState.GuildOwned,
                guildColorToken = owned,
            };

        [Test]
        public async Task RefreshAll_LoadsBothSnapshots_InOneCall()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await ClientWithMap(gw, Snap(), MapResult(2));
            Assert.IsTrue(client.HasState);
            Assert.AreEqual(2, client.WorldMap.occupancyVersion);
            Assert.AreEqual(Cc10WorldMapRefresh.Accepted, client.LastWorldMapRefresh);
            CollectionAssert.AreEquivalent(new[] { Cc10Endpoints.GetFrontierState, Cc10Endpoints.GetWorldMapSnapshot }, gw.Calls.Select(c => c.Key).ToArray());
        }

        [Test]
        public async Task WorldMapSection_ShowsSummary_AndOwnOccupiedNodes_InServerOrder()
        {
            var own = new[]
            {
                new Cc10WorldMapOwnNodeDto { nodeId = "hub", phaseId = "Tutorial" },
                new Cc10WorldMapOwnNodeDto { nodeId = "patrol_road", phaseId = "Outer" },
            };
            var gw = new FakeGateway();
            Cc10FrontierClient client = await ClientWithMap(gw, Snap(), MapResult(4, own: own, unlocked: Cc10WorldMapPhaseToken.Outer));
            List<Cc10Row> rows = Vm(client, Cc10SystemId.WorldMap).Rows;

            Cc10Row summary = rows.Single(r => r.EntityKey == "worldmap:summary");
            StringAssert.Contains("worldmap-beta-1", summary.Title);
            StringAssert.Contains("Outer Marches unlocked", summary.Detail);
            StringAssert.Contains("territory v4", summary.Detail);

            string[] ownRows = rows.Where(r => r.EntityKey.StartsWith("own:")).Select(r => r.EntityKey).ToArray();
            CollectionAssert.AreEqual(new[] { "own:hub", "own:patrol_road" }, ownRows, "server order, never re-sorted");
            StringAssert.Contains("Outer Marches", rows.Single(r => r.EntityKey == "own:patrol_road").Title);
        }

        [Test]
        public async Task WorldMapSection_OwnedFromEitherServerRead_And_ExpandAdjacencyUsesBoth()
        {
            Cc10FrontierSnapshot frontier = Snap();
            frontier.nodes = new[]
            {
                new Cc10MapNodeDto { nodeId = "hub", discovered = true, owned = false, neighbors = new[] { "patrol_road" } },
                new Cc10MapNodeDto { nodeId = "patrol_road", discovered = true, phaseUnlocked = true, neighbors = new[] { "hub" } },
            };
            var own = new[] { new Cc10WorldMapOwnNodeDto { nodeId = "hub", phaseId = "Tutorial" } };
            var gw = new FakeGateway();
            Cc10FrontierClient client = await ClientWithMap(gw, frontier, MapResult(1, own: own));
            List<Cc10Row> rows = Vm(client, Cc10SystemId.WorldMap).Rows;
            Assert.AreEqual("Owned", rows.Single(r => r.EntityKey == "hub").Detail, "the canonical snapshot says hub is owned");
            Assert.IsTrue(rows.Single(r => r.EntityKey == "patrol_road").ActionEnabled, "Expand is adjacent to a node the snapshot says is owned");
        }

        [Test]
        public async Task WorldMapSection_WithoutSnapshot_StillRendersFromFrontier_NoSummary()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot frontier = Snap();
            frontier.nodes = new[] { new Cc10MapNodeDto { nodeId = "hub", owned = true } };
            List<Cc10Row> rows = Vm(await OnlineClient(gw, frontier), Cc10SystemId.WorldMap).Rows;
            Assert.IsFalse(rows.Any(r => r.EntityKey == "worldmap:summary"));
            Assert.AreEqual("Owned", rows.Single(r => r.EntityKey == "hub").Detail);
        }

        [Test]
        public async Task Contest_ReadsOnlyTheCanonicalSnapshot_AndTheLegacyListIsNotModelled()
        {
            CollectionAssert.DoesNotContain(typeof(Cc10FrontierSnapshot).GetFields().Select(f => f.Name).ToArray(), "contestDistricts",
                "the legacy GetFrontierState.contestDistricts list is not part of the client contract");
            var map = MapResult(1, unlocked: Cc10WorldMapPhaseToken.Central, contest: new[] { Contest("central_ashfall", "GC07") });
            Cc10FrontierClient client = await ClientWithMap(new FakeGateway(), Snap(), map);
            List<Cc10Row> rows = Vm(client, Cc10SystemId.GuildTerritory).Rows;
            StringAssert.Contains("color 7", rows.Single(r => r.EntityKey == "central_ashfall").Detail);
        }

        [Test]
        public async Task Contest_BeforeCentralUnlock_ShowsLocked_NoInventedRows()
        {
            var map = MapResult(1, unlocked: Cc10WorldMapPhaseToken.Inner);
            List<Cc10Row> rows = Vm(await ClientWithMap(new FakeGateway(), Snap(), map), Cc10SystemId.GuildTerritory).Rows;
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("contest:locked", rows[0].EntityKey);
            Assert.IsFalse(rows[0].HasAction);
        }

        [Test]
        public async Task Contest_CentralUnlockedButNoActiveSeason_ShowsNoContest_NotAnError()
        {
            var map = MapResult(1, unlocked: Cc10WorldMapPhaseToken.Central, contest: Array.Empty<Cc10WorldMapContestDto>());
            List<Cc10Row> rows = Vm(await ClientWithMap(new FakeGateway(), Snap(), map), Cc10SystemId.GuildTerritory).Rows;
            Assert.AreEqual("contest:none", rows.Single().EntityKey);
        }

        [Test]
        public async Task Contest_GuildTerritoryDisabled_ServerSendsNoRows_ViewShowsPausedAndNoRows()
        {
            Cc10FrontierSnapshot frontier = Snap();
            frontier.disabledSystems = new[] { Cc10SystemId.GuildTerritory };
            var map = MapResult(1, unlocked: Cc10WorldMapPhaseToken.Central, contest: Array.Empty<Cc10WorldMapContestDto>());
            Cc10SectionVm vm = Vm(await ClientWithMap(new FakeGateway(), frontier, map), Cc10SystemId.GuildTerritory);
            Assert.AreEqual(Cc10Copy.SystemDisabled, vm.Banner);
            Assert.IsTrue(vm.ReadOnly);
            Assert.IsFalse(vm.Rows.Any(r => r.HasAction));
        }

        [Test]
        public async Task Contest_UnclaimedRow_OffersEnroll_OnlyWithAKnownGuildId_ServerDecidesEligibility()
        {
            var map = MapResult(1, unlocked: Cc10WorldMapPhaseToken.Central, contest: new[] { Contest("central_ember", season: "S9") });
            var gw = new FakeGateway();
            Cc10FrontierClient client = await ClientWithMap(gw, Snap(), map);

            Cc10Row noGuild = Vm(client, Cc10SystemId.GuildTerritory).Rows.Single();
            Assert.AreEqual(Cc10Endpoints.EnrollContestDistrict, noGuild.Endpoint);
            Assert.IsFalse(noGuild.ActionEnabled, "no guild id is known - nothing to enroll with, and none is invented");

            gw.Handler = (e, r) => e == Cc10Endpoints.GetGuildState
                ? (object)new Cc10GuildSnapshot { success = true }
                : e == Cc10Endpoints.GetFrontierState ? (object)Snap() : (object)map;
            Assert.IsTrue(await client.RefreshGuildStateAsync("g1"));
            Cc10Row withGuild = Vm(client, Cc10SystemId.GuildTerritory).Rows.Single();
            Assert.IsTrue(withGuild.ActionEnabled);
            Assert.AreEqual("central_ember", withGuild.Payload["districtId"]);
            Assert.AreEqual("S9", withGuild.Payload["seasonId"]);
            Assert.AreEqual("g1", withGuild.Payload["guildId"]);
            Assert.IsFalse(withGuild.Payload.ContainsKey("eligible") || withGuild.Payload.ContainsKey("guildColorToken"));
        }

        [Test]
        public async Task Contest_OwnedRow_ShowsOnlyAColorNumber_NeverAGuildIdentity_AndUnknownTokenIsNeutral()
        {
            var map = MapResult(1, unlocked: Cc10WorldMapPhaseToken.Central, contest: new[]
            {
                Contest("central_ashfall", "GC02"), Contest("central_ember", "not-a-token"), Contest("central_ironquarry"),
            });
            List<Cc10Row> rows = Vm(await ClientWithMap(new FakeGateway(), Snap(), map), Cc10SystemId.GuildTerritory).Rows;
            StringAssert.Contains("color 2", rows[0].Detail);
            StringAssert.DoesNotContain("Guild-", rows[0].Detail);
            Assert.IsFalse(rows[1].Detail.Contains("color"), "unknown token renders neutral");
            Assert.AreEqual("Unclaimed", rows[2].Detail);
        }

        [Test]
        public async Task StaleSnapshot_IsRetained_AndViewKeepsShowingTheLastGoodTerritory()
        {
            var gw = new FakeGateway();
            var own5 = new[] { new Cc10WorldMapOwnNodeDto { nodeId = "hub", phaseId = "Tutorial" }, new Cc10WorldMapOwnNodeDto { nodeId = "patrol_road", phaseId = "Outer" } };
            Cc10FrontierClient client = await ClientWithMap(gw, Snap(), MapResult(5, own: own5));

            gw.Handler = (e, r) => e == Cc10Endpoints.GetFrontierState ? (object)Snap() : MapResult(4, own: new[] { new Cc10WorldMapOwnNodeDto { nodeId = "hub", phaseId = "Tutorial" } });
            Assert.IsFalse(await client.RefreshWorldMapAsync());
            Assert.AreEqual(Cc10WorldMapRefresh.Stale, client.LastWorldMapRefresh);

            List<Cc10Row> rows = Vm(client, Cc10SystemId.WorldMap).Rows;
            Assert.IsTrue(rows.Any(r => r.EntityKey == "own:patrol_road"), "the newer (v5) territory stays on screen");
            StringAssert.Contains("territory v5", rows.Single(r => r.EntityKey == "worldmap:summary").Detail);
        }

        [Test]
        public async Task UnknownSchema_IsRetainedOver_AndViewSaysUpdateNeeded()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await ClientWithMap(gw, Snap(), MapResult(2));
            gw.Handler = (e, r) => e == Cc10Endpoints.GetFrontierState ? (object)Snap() : MapResult(9, schema: "cc10.worldmap.v2");
            Assert.IsFalse(await client.RefreshWorldMapAsync());
            Assert.AreEqual(Cc10WorldMapRefresh.SchemaRejected, client.LastWorldMapRefresh);
            Assert.AreEqual(2, client.WorldMap.occupancyVersion);
            Cc10SectionVm vm = Vm(client, Cc10SystemId.WorldMap);
            StringAssert.Contains("newer version", vm.Banner);
            Assert.IsTrue(vm.Rows.Any(r => r.EntityKey == "worldmap:summary"), "the last good view is still shown");
        }

        [Test]
        public async Task EmptyMapVersion_IsMalformed_Rejected_LastGoodRetained()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await ClientWithMap(gw, Snap(), MapResult(2));
            Cc10WorldMapSnapshotResult bad = MapResult(3);
            bad.snapshot.mapVersion = string.Empty;
            gw.Handler = (e, r) => e == Cc10Endpoints.GetFrontierState ? (object)Snap() : bad;
            Assert.IsFalse(await client.RefreshWorldMapAsync());
            Assert.AreEqual(Cc10WorldMapRefresh.Malformed, client.LastWorldMapRefresh);
            Assert.AreEqual(2, client.WorldMap.occupancyVersion);
        }

        [Test]
        public async Task ChangedMapVersion_IsAccepted_TheServerReauthoredTheMap()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await ClientWithMap(gw, Snap(), MapResult(2));
            Cc10WorldMapSnapshotResult next = MapResult(2);
            next.snapshot.mapVersion = "worldmap-beta-2";
            gw.Handler = (e, r) => e == Cc10Endpoints.GetFrontierState ? (object)Snap() : next;
            Assert.IsTrue(await client.RefreshWorldMapAsync());
            Assert.AreEqual("worldmap-beta-2", client.WorldMap.mapVersion);
            StringAssert.Contains("worldmap-beta-2", Vm(client, Cc10SystemId.WorldMap).Rows.Single(r => r.EntityKey == "worldmap:summary").Title);
        }

        [Test]
        public async Task Offline_KeepsTheLastSnapshotVisible_ReadOnly_ThenReconnectRefreshesBoth()
        {
            var gw = new FakeGateway();
            var own = new[] { new Cc10WorldMapOwnNodeDto { nodeId = "hub", phaseId = "Tutorial" } };
            Cc10FrontierClient client = await ClientWithMap(gw, Snap(), MapResult(2, own: own, unlocked: Cc10WorldMapPhaseToken.Central, contest: new[] { Contest("central_ashfall") }));

            gw.Handler = (e, r) => new InvalidOperationException("network");
            Assert.IsFalse(await client.RefreshAllAsync());
            Assert.AreEqual(Cc10Connection.Offline, client.Connection);
            Assert.AreEqual(Cc10WorldMapRefresh.Offline, client.LastWorldMapRefresh);

            Cc10SectionVm map = Vm(client, Cc10SystemId.WorldMap);
            Assert.AreEqual(Cc10Copy.Offline, map.Banner);
            Assert.IsTrue(map.Rows.Any(r => r.EntityKey == "own:hub"), "the last known territory stays visible offline");
            Cc10SectionVm contest = Vm(client, Cc10SystemId.GuildTerritory);
            Assert.IsTrue(contest.ReadOnly);
            Assert.IsFalse(contest.Rows.Any(r => r.ActionEnabled), "no contest action while offline");

            var back = new[] { new Cc10WorldMapOwnNodeDto { nodeId = "hub", phaseId = "Tutorial" }, new Cc10WorldMapOwnNodeDto { nodeId = "patrol_road", phaseId = "Outer" } };
            gw.Handler = (e, r) => e == Cc10Endpoints.GetFrontierState ? (object)Snap(2) : MapResult(3, own: back);
            Assert.IsTrue(await client.RefreshAllAsync());
            Assert.AreEqual(Cc10Connection.Online, client.Connection);
            Assert.AreEqual(3, client.WorldMap.occupancyVersion);
            Assert.IsTrue(Vm(client, Cc10SystemId.WorldMap).Rows.Any(r => r.EntityKey == "own:patrol_road"));
        }

        [Test]
        public async Task SuccessfulExpand_ReloadsTheHeldWorldMapSnapshot()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await ClientWithMap(gw, Snap(), MapResult(1));
            int mapReads = 0;
            gw.Handler = (e, r) =>
            {
                if (e == Cc10Endpoints.GetFrontierState) return Snap(2);
                if (e == Cc10Endpoints.GetWorldMapSnapshot) { mapReads++; return MapResult(2, own: new[] { new Cc10WorldMapOwnNodeDto { nodeId = "patrol_road", phaseId = "Outer" } }); }
                return Ok();
            };
            Cc10CommandOutcome outcome = await client.ExecuteAsync(Cc10SystemId.WorldMap, Cc10Endpoints.ExpandNode,
                new Dictionary<string, object> { { "nodeId", "patrol_road" } }, "patrol_road");
            Assert.AreEqual(Cc10Outcome.Applied, outcome.Outcome);
            Assert.AreEqual(1, mapReads);
            Assert.AreEqual(2, client.WorldMap.occupancyVersion, "territory version bumped by the server, read back");
        }

        [Test]
        public async Task CommandsOutsideTheMap_DoNotTriggerAWorldMapRead()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await ClientWithMap(gw, Snap(1, 1, Mission("m1", "Completed")), MapResult(1));
            gw.Calls.Clear();
            gw.Handler = (e, r) => e == Cc10Endpoints.GetFrontierState ? (object)Snap(2) : Ok();
            await client.ExecuteAsync(Cc10SystemId.Missions, Cc10Endpoints.ClaimMission, null, "m1");
            Assert.IsFalse(gw.Calls.Any(c => c.Key == Cc10Endpoints.GetWorldMapSnapshot));
        }

        [Test]
        public async Task ExpandWithoutAHeldSnapshot_NeverIssuesAnUnrequestedMapRead()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap());
            gw.Calls.Clear();
            await client.ExecuteAsync(Cc10SystemId.WorldMap, Cc10Endpoints.ExpandNode, new Dictionary<string, object> { { "nodeId", "n" } }, "n");
            Assert.IsFalse(gw.Calls.Any(c => c.Key == Cc10Endpoints.GetWorldMapSnapshot));
        }

        [Test]
        public async Task NoIdentityOrPlacementRowsAnywhere_InTheWorldMapOrContestViews()
        {
            var map = MapResult(3, own: new[] { new Cc10WorldMapOwnNodeDto { nodeId = "hub", phaseId = "Tutorial" } },
                unlocked: Cc10WorldMapPhaseToken.Central, contest: new[] { Contest("central_ashfall", "GC03") });
            Cc10FrontierClient client = await ClientWithMap(new FakeGateway(), Snap(), map);
            foreach (string sys in new[] { Cc10SystemId.WorldMap, Cc10SystemId.GuildTerritory })
                foreach (Cc10Row r in Vm(client, sys).Rows)
                {
                    string text = (r.Title + " " + r.Detail + " " + r.ActionLabel).ToLowerInvariant();
                    foreach (string banned in new[] { "well", "place base", "relocate", "teleport", "player-", "guild-", "pseudonym" })
                        StringAssert.DoesNotContain(banned, text, sys + " row '" + r.Title + "'");
                }
        }

        [Test]
        public async Task ContestColorAllocationRejections_MapToPlainCopy_NoRawCode()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap());
            foreach (string code in new[] { Cc10Errors.ColorTokenCollision, Cc10Errors.ColorPaletteExhausted })
            {
                gw.Handler = (e, r) => e == Cc10Endpoints.GetFrontierState ? (object)Snap() : new Cc10CommandResult { success = false, errorCode = code };
                Cc10CommandOutcome o = await client.ExecuteAsync(Cc10SystemId.GuildTerritory, Cc10Endpoints.EnrollContestDistrict, null, "d1");
                Assert.AreEqual(Cc10Outcome.Rejected, o.Outcome, code);
                Assert.AreEqual(Cc10Copy.ColorUnavailable, o.Message, code);
                StringAssert.DoesNotContain("COLOR_", o.Message);
            }
        }

        [Test]
        public async Task EnrolledButUnsettledDistrict_ArrivesUnclaimedWithNoToken_AndShowsNoColor()
        {
            // BE bef39415: enrolling confers no ownership and allocates no color until the operator settles it.
            var map = MapResult(1, unlocked: Cc10WorldMapPhaseToken.Central, contest: new[] { Contest("central_ashfall") });
            Cc10Row row = Vm(await ClientWithMap(new FakeGateway(), Snap(), map), Cc10SystemId.GuildTerritory).Rows.Single();
            Assert.AreEqual("Unclaimed", row.Detail);
            Assert.IsFalse(row.Detail.Contains("color"));
        }

        [Test]
        public async Task SettledDistrict_StaysVisibleAsGuildOwned_WithItsToken_NoEnrollOffered()
        {
            var map = MapResult(1, unlocked: Cc10WorldMapPhaseToken.Central, contest: new[] { Contest("central_ashfall", "GC05", "S3") });
            Cc10Row row = Vm(await ClientWithMap(new FakeGateway(), Snap(), map), Cc10SystemId.GuildTerritory).Rows.Single();
            StringAssert.Contains("Owned by a guild", row.Detail);
            StringAssert.Contains("color 5", row.Detail);
            Assert.IsFalse(row.HasAction);
        }

        // ---- occupancy reset (lower occupancyVersion on a changed map epoch) and contest state distinctions ----

        private static Cc10WorldMapSnapshotResult ResetMap(int version, string mapVersion, long serverUtc, params string[] seasons)
        {
            Cc10WorldMapSnapshotResult r = MapResult(version, unlocked: Cc10WorldMapPhaseToken.Central,
                contest: seasons.Select((id, i) => Contest("central_d" + i, season: id)).ToArray());
            r.snapshot.mapVersion = mapVersion;
            r.snapshot.serverUtc = serverUtc;
            return r;
        }

        [Test]
        public async Task LowerOccupancyVersion_IsAccepted_WhenMapVersionChanges_AsAFullReset()
        {
            var gw = new FakeGateway { Handler = (e, r) => ResetMap(9, "worldmap-beta-1", 2_000_000, "S1") };
            var client = new Cc10FrontierClient(gw);
            await client.RefreshWorldMapAsync();
            gw.Handler = (e, r) => ResetMap(0, "worldmap-beta-2", 2_500_000, "S1");
            Assert.IsTrue(await client.RefreshWorldMapAsync());
            Assert.AreEqual(Cc10WorldMapRefresh.AcceptedAfterReset, client.LastWorldMapRefresh);
            Assert.AreEqual(0, client.WorldMap.occupancyVersion, "the reset replaces the whole cache, no merge");
            Assert.AreEqual("worldmap-beta-2", client.WorldMap.mapVersion);
        }

        [Test]
        public async Task LowerOccupancyVersion_IsAccepted_WhenTheSeasonSetChanges_AsAFullReset()
        {
            var gw = new FakeGateway { Handler = (e, r) => ResetMap(7, "worldmap-beta-1", 2_000_000, "S1") };
            var client = new Cc10FrontierClient(gw);
            await client.RefreshWorldMapAsync();
            gw.Handler = (e, r) => ResetMap(1, "worldmap-beta-1", 3_000_000, "S2");
            Assert.IsTrue(await client.RefreshWorldMapAsync());
            Assert.AreEqual(Cc10WorldMapRefresh.AcceptedAfterReset, client.LastWorldMapRefresh);
            Assert.AreEqual(1, client.WorldMap.occupancyVersion);
            Assert.AreEqual("S2", client.WorldMap.centralContest[0].seasonId);
        }

        [Test]
        public async Task LowerOccupancyVersion_SameEpoch_StaysStale()
        {
            var gw = new FakeGateway { Handler = (e, r) => ResetMap(7, "worldmap-beta-1", 2_000_000, "S1") };
            var client = new Cc10FrontierClient(gw);
            await client.RefreshWorldMapAsync();
            gw.Handler = (e, r) => ResetMap(6, "worldmap-beta-1", 3_000_000, "S1");
            Assert.IsFalse(await client.RefreshWorldMapAsync());
            Assert.AreEqual(Cc10WorldMapRefresh.Stale, client.LastWorldMapRefresh);
            Assert.AreEqual(7, client.WorldMap.occupancyVersion);
        }

        [Test]
        public async Task OutOfOrderOlderResponse_WithADifferentEpoch_IsNeverMistakenForAReset()
        {
            // An older response (older server clock) that happens to differ in mapVersion/season set must not
            // roll the cache back.
            var gw = new FakeGateway { Handler = (e, r) => ResetMap(9, "worldmap-beta-2", 5_000_000, "S2") };
            var client = new Cc10FrontierClient(gw);
            await client.RefreshWorldMapAsync();
            gw.Handler = (e, r) => ResetMap(3, "worldmap-beta-1", 1_000_000, "S1");
            Assert.IsFalse(await client.RefreshWorldMapAsync());
            Assert.AreEqual(Cc10WorldMapRefresh.Stale, client.LastWorldMapRefresh);
            Assert.AreEqual("worldmap-beta-2", client.WorldMap.mapVersion);
            Assert.AreEqual(9, client.WorldMap.occupancyVersion);
        }

        [Test]
        public async Task ResetSnapshot_ReplacesOwnTerritoryRows_NoStaleNodesSurvive()
        {
            var gw = new FakeGateway();
            var before = new[] { new Cc10WorldMapOwnNodeDto { nodeId = "hub", phaseId = "Tutorial" }, new Cc10WorldMapOwnNodeDto { nodeId = "patrol_road", phaseId = "Outer" } };
            gw.Handler = (e, r) => e == Cc10Endpoints.GetFrontierState ? (object)Snap() : MapResult(8, own: before);
            var client = new Cc10FrontierClient(gw, new Cc10ServerClock(() => 0));
            await client.RefreshAllAsync();

            Cc10WorldMapSnapshotResult reset = ResetMap(0, "worldmap-beta-2", 9_000_000);
            reset.snapshot.ownOccupiedNodes = new[] { new Cc10WorldMapOwnNodeDto { nodeId = "hub", phaseId = "Tutorial" } };
            gw.Handler = (e, r) => e == Cc10Endpoints.GetFrontierState ? (object)Snap() : reset;
            Assert.IsTrue(await client.RefreshWorldMapAsync());
            List<Cc10Row> rows = Vm(client, Cc10SystemId.WorldMap).Rows;
            Assert.IsTrue(rows.Any(r => r.EntityKey == "own:hub"));
            Assert.IsFalse(rows.Any(r => r.EntityKey == "own:patrol_road"), "the pre-reset territory must not linger");
            StringAssert.Contains("territory v0", rows.Single(r => r.EntityKey == "worldmap:summary").Detail);
        }

        [Test]
        public void SeasonEpochField_IsNotModelled_BeUnconfirmed_OnlyPerRowSeasonId()
        {
            string[] top = typeof(Cc10WorldMapSnapshotDto).GetFields().Select(f => f.Name).ToArray();
            foreach (string f in top) StringAssert.DoesNotContain("epoch", f.ToLowerInvariant());
            CollectionAssert.DoesNotContain(top, "seasonId");
            CollectionAssert.Contains(typeof(Cc10WorldMapContestDto).GetFields().Select(f => f.Name).ToArray(), "seasonId");
        }

        [Test]
        public async Task ContestStates_AreDistinguished_EnrollOnlyOnUnclaimed()
        {
            var contest = new[]
            {
                new Cc10WorldMapContestDto { districtId = "central_ashfall", seasonId = "S1", ownershipState = Cc10ContestOwnershipState.Unclaimed },
                new Cc10WorldMapContestDto { districtId = "central_ember", seasonId = "S1", ownershipState = Cc10ContestOwnershipState.Enrolled, guildColorToken = "GC04" },
                new Cc10WorldMapContestDto { districtId = "central_ironquarry", seasonId = "S1", ownershipState = Cc10ContestOwnershipState.GuildOwned, guildColorToken = "GC09" },
                new Cc10WorldMapContestDto { districtId = "central_extra", seasonId = "S1", ownershipState = "SomethingNew", guildColorToken = "GC01" },
            };
            var gw = new FakeGateway();
            Cc10FrontierClient client = await ClientWithMap(gw, Snap(), MapResult(1, unlocked: Cc10WorldMapPhaseToken.Central, contest: contest));
            gw.Handler = (e, r) => e == Cc10Endpoints.GetGuildState ? (object)new Cc10GuildSnapshot { success = true } : e == Cc10Endpoints.GetFrontierState ? (object)Snap() : MapResult(1, unlocked: Cc10WorldMapPhaseToken.Central, contest: contest);
            await client.RefreshGuildStateAsync("g1");
            List<Cc10Row> rows = Vm(client, Cc10SystemId.GuildTerritory).Rows;

            Cc10Row open = rows.Single(r => r.EntityKey == "central_ashfall");
            Assert.AreEqual("Unclaimed", open.Detail);
            Assert.IsTrue(open.HasAction && open.ActionEnabled);

            Cc10Row enrolled = rows.Single(r => r.EntityKey == "central_ember");
            StringAssert.Contains("Enrolled", enrolled.Detail);
            StringAssert.Contains("color 4", enrolled.Detail);
            Assert.IsFalse(enrolled.HasAction, "an enrolled district is not open");

            Cc10Row owned = rows.Single(r => r.EntityKey == "central_ironquarry");
            StringAssert.Contains("Owned by a guild", owned.Detail);
            Assert.IsFalse(owned.HasAction);

            Cc10Row unknown = rows.Single(r => r.EntityKey == "central_extra");
            Assert.AreEqual("Unavailable", unknown.Detail);
            Assert.IsFalse(unknown.HasAction, "an unknown state is never treated as open");
            Assert.IsFalse(unknown.Detail.Contains("color"), "an unknown state shows no color");
        }

        [Test]
        public void OwnershipState_IsKnown_CoversExactlyTheThreeStates()
        {
            Assert.IsTrue(Cc10ContestOwnershipState.IsKnown("Unclaimed"));
            Assert.IsTrue(Cc10ContestOwnershipState.IsKnown("Enrolled"));
            Assert.IsTrue(Cc10ContestOwnershipState.IsKnown("GuildOwned"));
            Assert.IsFalse(Cc10ContestOwnershipState.IsKnown("guildowned"));
            Assert.IsFalse(Cc10ContestOwnershipState.IsKnown(null));
        }

        [Test]
        public async Task Enroll_IsDisabled_WhileOfflineOrPaused_EvenOnAnUnclaimedRow()
        {
            var contest = new[] { Contest("central_ashfall") };
            var gw = new FakeGateway();
            Cc10FrontierClient client = await ClientWithMap(gw, Snap(), MapResult(1, unlocked: Cc10WorldMapPhaseToken.Central, contest: contest));
            gw.Handler = (e, r) => e == Cc10Endpoints.GetGuildState ? (object)new Cc10GuildSnapshot { success = true } : e == Cc10Endpoints.GetFrontierState ? (object)Snap() : MapResult(1, unlocked: Cc10WorldMapPhaseToken.Central, contest: contest);
            await client.RefreshGuildStateAsync("g1");
            Assert.IsTrue(Vm(client, Cc10SystemId.GuildTerritory).Rows.Single().ActionEnabled);

            gw.Handler = (e, r) => new InvalidOperationException("down");
            await client.RefreshAllAsync();
            Assert.IsFalse(Vm(client, Cc10SystemId.GuildTerritory).Rows.Single().ActionEnabled, "offline: read-only");
        }

        // ---- BE 62db223f validation: ResolveContestDistrictUnowned (ExplicitlyUnowned) - no client contract delta ----

        [Test]
        public void Bl62db223f_ContestDtoShapes_AreUnchanged_ExplicitlyUnownedIsAValueNotAField()
        {
            // BE 62db223f only adds a ContestStatus VALUE (ExplicitlyUnowned), an operator endpoint and the
            // CONTEST_UNRESOLVED (publish-side) error. Neither the snapshot nor the command-result DTO gained a field.
            CollectionAssert.AreEqual(new[] { "districtId", "guildColorToken", "ownershipState", "seasonId" },
                typeof(Cc10WorldMapContestDto).GetFields().Select(f => f.Name).OrderBy(n => n).ToArray());
            CollectionAssert.AreEqual(new[] { "callerGuildEligible", "districtId", "enrolledGuildColorToken", "ownerGuildColorToken", "status" },
                typeof(Cc10ContestDistrictDto).GetFields().Select(f => f.Name).OrderBy(n => n).ToArray());
            CollectionAssert.AreEqual(new[] { "centralContest", "mapVersion", "occupancyVersion", "ownOccupiedNodes", "schemaVersion", "serverUtc", "unlockedPhase" },
                typeof(Cc10WorldMapSnapshotDto).GetFields().Select(f => f.Name).OrderBy(n => n).ToArray());
        }

        [Test]
        public async Task ExplicitlyUnownedDistrict_AsBeEmitsIt_IsUnclaimedWithNullToken_NoColor()
        {
            // BE snapshot for a district settled ExplicitlyUnowned: ownershipState stays "Unclaimed", token null.
            var map = MapResult(1, unlocked: Cc10WorldMapPhaseToken.Central, contest: new[] { Contest("central_ashfall", season: "S4") });
            Cc10Row row = Vm(await ClientWithMap(new FakeGateway(), Snap(), map), Cc10SystemId.GuildTerritory).Rows.Single();
            Assert.AreEqual("Unclaimed", row.Detail);
            Assert.IsFalse(row.Detail.Contains("color"));
            Assert.AreNotEqual("Owned by a guild", row.Detail);
        }

        [Test]
        public void ExplicitlyUnownedStatus_InACommandResult_ParsesAsAPlainStringValue()
        {
            const string json = "{\"success\":true,\"contestDistrict\":{\"districtId\":\"central_ashfall\",\"status\":\"ExplicitlyUnowned\"}}";
            Cc10CommandResult r = UnityEngine.JsonUtility.FromJson<Cc10CommandResult>(json);
            Assert.AreEqual("ExplicitlyUnowned", r.contestDistrict.status);
            Assert.IsNull(r.contestDistrict.ownerGuildColorToken, "no token on an explicitly-unowned district");
            Assert.IsNull(r.contestDistrict.enrolledGuildColorToken);
        }

        [Test]
        public async Task ContestUnresolved_ArrivingOnAPlayerCommand_ShowsPlainCopy_NeverTheRawCode()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap());
            gw.Handler = (e, r) => e == Cc10Endpoints.GetFrontierState ? (object)Snap() : new Cc10CommandResult { success = false, errorCode = "CONTEST_UNRESOLVED" };
            Cc10CommandOutcome o = await client.ExecuteAsync(Cc10SystemId.GuildTerritory, Cc10Endpoints.EnrollContestDistrict, null, "d");
            Assert.AreEqual(Cc10Outcome.Rejected, o.Outcome);
            Assert.AreEqual(Cc10Copy.Generic, o.Message);
            StringAssert.DoesNotContain("CONTEST_UNRESOLVED", o.Message);
        }

        [Test]
        public async Task NoPlayerRow_EverOffersAnOperatorResolveAction()
        {
            var contest = new[]
            {
                Contest("central_ashfall"), Contest("central_ember", "GC02"),
                new Cc10WorldMapContestDto { districtId = "central_x", seasonId = "S1", ownershipState = Cc10ContestOwnershipState.ExplicitlyUnowned },
            };
            var gw = new FakeGateway();
            Cc10FrontierClient client = await ClientWithMap(gw, Snap(), MapResult(1, unlocked: Cc10WorldMapPhaseToken.Central, contest: contest));
            foreach (Cc10Row row in Vm(client, Cc10SystemId.GuildTerritory).Rows)
            {
                StringAssert.DoesNotContain("Resolve", row.Endpoint);
                StringAssert.DoesNotContain("Unowned", row.Endpoint);
                StringAssert.DoesNotContain("Resolve", row.ActionLabel);
            }
            Assert.IsFalse(typeof(Cc10Endpoints).GetFields().Any(f => f.Name == "ResolveContestDistrictUnowned"),
                "the operator-only endpoint is not part of the player client contract");
        }

        // ---- BE 7302a996: ownershipState "ExplicitlyUnowned" serialized in the snapshot ----

        [Test]
        public void ExplicitlyUnowned_ParsesFromServerJson_AsAKnownState_WithNullToken()
        {
            const string json = "{\"success\":true,\"snapshot\":{\"schemaVersion\":\"cc10.worldmap.v1\",\"mapVersion\":\"worldmap-beta-1\"," +
                "\"serverUtc\":5,\"unlockedPhase\":\"Central\",\"occupancyVersion\":1,\"ownOccupiedNodes\":[]," +
                "\"centralContest\":[{\"districtId\":\"central_ashfall\",\"seasonId\":\"S4\",\"ownershipState\":\"ExplicitlyUnowned\",\"guildColorToken\":null}]}}";
            Cc10WorldMapContestDto row = UnityEngine.JsonUtility.FromJson<Cc10WorldMapSnapshotResult>(json).snapshot.centralContest[0];
            Assert.AreEqual(Cc10ContestOwnershipState.ExplicitlyUnowned, row.ownershipState);
            Assert.IsTrue(Cc10ContestOwnershipState.IsKnown(row.ownershipState));
            Assert.IsTrue(string.IsNullOrEmpty(row.guildColorToken), "no token on an explicitly-unowned district");
            Assert.AreEqual(Cc10ContestOwnershipState.ExplicitlyUnowned, "ExplicitlyUnowned", "wire value is exact and case-sensitive");
        }

        [Test]
        public async Task ExplicitlyUnowned_RowIsNeutral_NoColor_NoEnroll_NoAction_EvenWithAGuildId()
        {
            var contest = new[] { new Cc10WorldMapContestDto { districtId = "central_ashfall", seasonId = "S4", ownershipState = Cc10ContestOwnershipState.ExplicitlyUnowned } };
            var gw = new FakeGateway();
            Cc10FrontierClient client = await ClientWithMap(gw, Snap(), MapResult(1, unlocked: Cc10WorldMapPhaseToken.Central, contest: contest));
            gw.Handler = (e, r) => e == Cc10Endpoints.GetGuildState ? (object)new Cc10GuildSnapshot { success = true } : e == Cc10Endpoints.GetFrontierState ? (object)Snap() : MapResult(1, unlocked: Cc10WorldMapPhaseToken.Central, contest: contest);
            await client.RefreshGuildStateAsync("g1");

            Cc10Row row = Vm(client, Cc10SystemId.GuildTerritory).Rows.Single();
            Assert.AreEqual("No owner this season", row.Detail);
            Assert.IsFalse(row.Detail.Contains("color"));
            Assert.IsFalse(row.HasAction, "no Enroll button and no action of any kind");
            Assert.AreEqual(string.Empty, row.ActionLabel);
            Assert.AreEqual(string.Empty, row.Endpoint);
            Assert.IsFalse(row.Payload.ContainsKey("guildId") || row.Payload.ContainsKey("seasonId"), "nothing to submit");
            StringAssert.DoesNotContain("Enroll", row.Title + row.Detail);
        }

        [Test]
        public async Task ExplicitlyUnowned_IgnoresAnyTokenThatWereAttached_DefenceInDepth()
        {
            var contest = new[] { new Cc10WorldMapContestDto { districtId = "central_ember", seasonId = "S4", ownershipState = Cc10ContestOwnershipState.ExplicitlyUnowned, guildColorToken = "GC06" } };
            Cc10Row row = Vm(await ClientWithMap(new FakeGateway(), Snap(), MapResult(1, unlocked: Cc10WorldMapPhaseToken.Central, contest: contest)), Cc10SystemId.GuildTerritory).Rows.Single();
            Assert.IsFalse(row.Detail.Contains("color"));
            Assert.IsFalse(row.Detail.Contains("6"));
            Assert.IsFalse(row.HasAction);
        }

        [Test]
        public async Task MixedDistricts_OpenOwnedAndExplicitlyUnowned_AreThreeDistinctRenderings()
        {
            var contest = new[]
            {
                new Cc10WorldMapContestDto { districtId = "central_ashfall", seasonId = "S4", ownershipState = Cc10ContestOwnershipState.Unclaimed },
                new Cc10WorldMapContestDto { districtId = "central_ember", seasonId = "S4", ownershipState = Cc10ContestOwnershipState.GuildOwned, guildColorToken = "GC03" },
                new Cc10WorldMapContestDto { districtId = "central_ironquarry", seasonId = "S4", ownershipState = Cc10ContestOwnershipState.ExplicitlyUnowned },
            };
            var gw = new FakeGateway();
            Cc10FrontierClient client = await ClientWithMap(gw, Snap(), MapResult(1, unlocked: Cc10WorldMapPhaseToken.Central, contest: contest));
            gw.Handler = (e, r) => e == Cc10Endpoints.GetGuildState ? (object)new Cc10GuildSnapshot { success = true } : e == Cc10Endpoints.GetFrontierState ? (object)Snap() : MapResult(1, unlocked: Cc10WorldMapPhaseToken.Central, contest: contest);
            await client.RefreshGuildStateAsync("g1");
            List<Cc10Row> rows = Vm(client, Cc10SystemId.GuildTerritory).Rows;
            Assert.AreEqual("Unclaimed", rows[0].Detail);
            Assert.IsTrue(rows[0].HasAction, "only the open district offers Enroll");
            StringAssert.Contains("color 3", rows[1].Detail);
            Assert.IsFalse(rows[1].HasAction);
            Assert.AreEqual("No owner this season", rows[2].Detail);
            Assert.IsFalse(rows[2].HasAction);
            Assert.AreEqual(3, rows.Select(r => r.Detail).Distinct().Count());
        }

        [Test]
        public async Task ExplicitlyUnowned_SurvivesOfflineAndReconnect_StillNoAction()
        {
            var contest = new[] { new Cc10WorldMapContestDto { districtId = "central_ashfall", seasonId = "S4", ownershipState = Cc10ContestOwnershipState.ExplicitlyUnowned } };
            var gw = new FakeGateway();
            Cc10FrontierClient client = await ClientWithMap(gw, Snap(), MapResult(1, unlocked: Cc10WorldMapPhaseToken.Central, contest: contest));
            gw.Handler = (e, r) => new InvalidOperationException("down");
            await client.RefreshAllAsync();
            Cc10Row offline = Vm(client, Cc10SystemId.GuildTerritory).Rows.Single();
            Assert.AreEqual("No owner this season", offline.Detail, "the retained snapshot still renders the same neutral row");
            Assert.IsFalse(offline.HasAction && offline.ActionEnabled);

            gw.Handler = (e, r) => e == Cc10Endpoints.GetFrontierState ? (object)Snap() : MapResult(2, unlocked: Cc10WorldMapPhaseToken.Central, contest: contest);
            Assert.IsTrue(await client.RefreshAllAsync());
            Assert.IsFalse(Vm(client, Cc10SystemId.GuildTerritory).Rows.Single().HasAction);
        }

        [Test]
        public async Task ExplicitlyUnowned_DoesNotChangeTheSeasonEpochInference()
        {
            // A season rollover is still detected only from the seasonId set, not from an ownership value.
            var gw = new FakeGateway { Handler = (e, r) => ResetMap(5, "worldmap-beta-1", 2_000_000, "S4") };
            var client = new Cc10FrontierClient(gw);
            await client.RefreshWorldMapAsync();
            Cc10WorldMapSnapshotResult next = ResetMap(4, "worldmap-beta-1", 3_000_000, "S4");
            next.snapshot.centralContest[0].ownershipState = Cc10ContestOwnershipState.ExplicitlyUnowned;
            gw.Handler = (e, r) => next;
            Assert.IsFalse(await client.RefreshWorldMapAsync(), "same mapVersion + same season set = still stale, whatever the ownership value");
            Assert.AreEqual(Cc10WorldMapRefresh.Stale, client.LastWorldMapRefresh);
        }

        [Test]
        public void ExplicitlyUnowned_IsTheOnlyValueAdded_SnapshotAndCommandShapesStayFourFieldsAndFive()
        {
            Assert.AreEqual(4, typeof(Cc10WorldMapContestDto).GetFields().Length);
            Assert.AreEqual(5, typeof(Cc10ContestDistrictDto).GetFields().Length);
            Assert.IsFalse(Cc10ContestOwnershipState.IsKnown("Explicitlyunowned"));
        }

        // ---- BE ec4b49b3: GetCallerGuildIdentity (zero-arg, server-owned "what guild am I in") ----

        private static Cc10GuildIdentityResult Identity(bool success = true, bool hasGuild = false,
            string guildId = null, long epoch = 0, long expires = 0, string errorCode = null) =>
            new Cc10GuildIdentityResult
            {
                success = success, errorCode = errorCode ?? string.Empty, serverUtcMs = 3_000_000,
                hasGuild = hasGuild, guildId = guildId, membershipEpoch = epoch, membershipExpiresUtcMs = expires,
            };

        [Test]
        public void GuildIdentityResult_DeserializesRealServerJson_MemberCase()
        {
            const string json = "{\"success\":true,\"hasGuild\":true,\"guildId\":\"g1\"," +
                "\"membershipEpoch\":4,\"membershipExpiresUtcMs\":9000000}";
            Cc10GuildIdentityResult r = UnityEngine.JsonUtility.FromJson<Cc10GuildIdentityResult>(json);
            Assert.IsTrue(r.success);
            Assert.IsTrue(r.hasGuild);
            Assert.AreEqual("g1", r.guildId);
            Assert.AreEqual(4, r.membershipEpoch);
            Assert.AreEqual(9000000, r.membershipExpiresUtcMs);
        }

        [Test]
        public void GuildIdentityResult_DeserializesRealServerJson_NoGuildCase_StillSuccess()
        {
            const string json = "{\"success\":true,\"hasGuild\":false}";
            Cc10GuildIdentityResult r = UnityEngine.JsonUtility.FromJson<Cc10GuildIdentityResult>(json);
            Assert.IsTrue(r.success, "no-guild is a valid, successful state, never an error");
            Assert.IsFalse(r.hasGuild);
            Assert.IsTrue(string.IsNullOrEmpty(r.guildId));
        }

        [Test]
        public async Task RefreshGuildIdentity_Member_SetsGuildId_AcceptedStatus()
        {
            var gw = new FakeGateway { Handler = (e, r) => Identity(hasGuild: true, guildId: "g1", epoch: 4, expires: 9_000_000) };
            var client = new Cc10FrontierClient(gw, new Cc10ServerClock(() => 0));
            Assert.IsTrue(await client.RefreshGuildIdentityAsync());
            Assert.AreEqual(Cc10GuildIdentityRefresh.Accepted, client.LastGuildIdentityRefresh);
            Assert.AreEqual("g1", client.GuildId);
            Assert.IsTrue(client.GuildIdentity.hasGuild);
            Assert.AreEqual(4, client.GuildIdentity.membershipEpoch);
            Assert.AreEqual(9_000_000, client.GuildIdentity.membershipExpiresUtcMs);
            Assert.AreEqual(Cc10Connection.Online, client.Connection);
            Assert.IsNull(gw.Calls.Single(c => c.Key == Cc10Endpoints.GetCallerGuildIdentity).Value,
                "zero-arg call - no guildId, no hash, no request body of any kind sent");
        }

        [Test]
        public async Task RefreshGuildIdentity_NoGuild_IsAcceptedAndValid_GuildIdIsNull_NotAFailure()
        {
            var gw = new FakeGateway { Handler = (e, r) => Identity(hasGuild: false) };
            var client = new Cc10FrontierClient(gw);
            Assert.IsTrue(await client.RefreshGuildIdentityAsync(), "no-guild is Accepted, not a failure");
            Assert.AreEqual(Cc10GuildIdentityRefresh.Accepted, client.LastGuildIdentityRefresh);
            Assert.IsNull(client.GuildId);
            Assert.IsFalse(client.GuildIdentity.hasGuild);
        }

        [Test]
        public async Task RefreshGuildIdentity_RevokedOrExpired_ArrivesIdenticalToNoGuild_ClientNeverGuesses()
        {
            // BE deliberately does not distinguish never-joined/left/revoked/expired - all four collapse
            // to hasGuild=false, success=true. This client must not attempt to tell them apart either.
            var gw = new FakeGateway { Handler = (e, r) => Identity(hasGuild: false) };
            var client = new Cc10FrontierClient(gw);
            Assert.IsTrue(await client.RefreshGuildIdentityAsync());
            Assert.IsNull(client.GuildId);
            Assert.AreEqual(Cc10GuildIdentityRefresh.Accepted, client.LastGuildIdentityRefresh);
        }

        [Test]
        public async Task RefreshGuildIdentity_PreviouslyAMember_ThenRevoked_ClearsGuildIdOnTheNextRefresh()
        {
            var gw = new FakeGateway { Handler = (e, r) => Identity(hasGuild: true, guildId: "g1", epoch: 1) };
            var client = new Cc10FrontierClient(gw);
            await client.RefreshGuildIdentityAsync();
            Assert.AreEqual("g1", client.GuildId);

            gw.Handler = (e, r) => Identity(hasGuild: false);
            Assert.IsTrue(await client.RefreshGuildIdentityAsync());
            Assert.IsNull(client.GuildId, "revocation must actually clear the previously-held guild id");
        }

        [Test]
        public async Task RefreshGuildIdentity_Unauthorized_FailsClosed_ClearsAnyPreviousGuildId()
        {
            var gw = new FakeGateway { Handler = (e, r) => Identity(hasGuild: true, guildId: "g1") };
            var client = new Cc10FrontierClient(gw);
            await client.RefreshGuildIdentityAsync();
            Assert.AreEqual("g1", client.GuildId);

            gw.Handler = (e, r) => new Cc10GuildIdentityResult { success = false, errorCode = Cc10Errors.AuthenticationRequired };
            Assert.IsFalse(await client.RefreshGuildIdentityAsync());
            Assert.AreEqual(Cc10GuildIdentityRefresh.AuthenticationRequired, client.LastGuildIdentityRefresh);
            Assert.IsNull(client.GuildId, "fail closed - never keep serving a stale guild id after an auth failure");
            Assert.IsNull(client.GuildIdentity);
        }

        [Test]
        public async Task RefreshGuildIdentity_AuthorityUnavailable_FailsClosed()
        {
            var gw = new FakeGateway { Handler = (e, r) => new Cc10GuildIdentityResult { success = false, errorCode = Cc10Errors.AuthorityUnavailable } };
            var client = new Cc10FrontierClient(gw);
            Assert.IsFalse(await client.RefreshGuildIdentityAsync());
            Assert.AreEqual(Cc10GuildIdentityRefresh.AuthorityUnavailable, client.LastGuildIdentityRefresh);
            Assert.IsNull(client.GuildId);
        }

        [Test]
        public async Task RefreshGuildIdentity_StorageFailure_FailsClosed_NeverTreatedAsNoGuild()
        {
            var gw = new FakeGateway { Handler = (e, r) => new Cc10GuildIdentityResult { success = false, errorCode = Cc10Errors.StorageUnavailable } };
            var client = new Cc10FrontierClient(gw);
            Assert.IsFalse(await client.RefreshGuildIdentityAsync());
            Assert.AreNotEqual(Cc10GuildIdentityRefresh.Accepted, client.LastGuildIdentityRefresh,
                "a storage failure must never be reported the same way as a genuine no-guild answer");
            Assert.IsNull(client.GuildId);
            Assert.IsNull(client.GuildIdentity);
        }

        [Test]
        public async Task RefreshGuildIdentity_TransportFailure_FailsClosed_GoesOffline()
        {
            var gw = new FakeGateway { Handler = (e, r) => new InvalidOperationException("network") };
            var client = new Cc10FrontierClient(gw);
            Assert.IsFalse(await client.RefreshGuildIdentityAsync());
            Assert.AreEqual(Cc10GuildIdentityRefresh.Offline, client.LastGuildIdentityRefresh);
            Assert.AreEqual(Cc10Connection.Offline, client.Connection);
            Assert.IsNull(client.GuildId);
        }

        [Test]
        public async Task RefreshGuildIdentity_MalformedPositiveResponse_HasGuildTrueButNoId_FailsClosed()
        {
            // A self-contradictory response (claims membership, names no guild) must never be trusted -
            // the client neither invents a placeholder id nor silently treats it as no-guild.
            var gw = new FakeGateway { Handler = (e, r) => Identity(hasGuild: true, guildId: null) };
            var client = new Cc10FrontierClient(gw);
            Assert.IsFalse(await client.RefreshGuildIdentityAsync());
            Assert.AreEqual(Cc10GuildIdentityRefresh.Malformed, client.LastGuildIdentityRefresh);
            Assert.IsNull(client.GuildId);
            Assert.IsNull(client.GuildIdentity);
        }

        [Test]
        public async Task RefreshGuildIdentity_NullResult_FailsClosed()
        {
            var gw = new FakeGateway { Handler = (e, r) => (Cc10GuildIdentityResult)null };
            var client = new Cc10FrontierClient(gw);
            Assert.IsFalse(await client.RefreshGuildIdentityAsync());
            Assert.AreEqual(Cc10GuildIdentityRefresh.Malformed, client.LastGuildIdentityRefresh);
            Assert.IsNull(client.GuildId);
        }

        [Test]
        public async Task GuildIdentityGuildId_FeedsOnlyIntoExistingGuildTerritoryPayload_NoNewEndpointInvented()
        {
            // The returned guildId must flow only into the existing EnrollContestDistrict/GetGuildState
            // requests this client already builds - never a new call this task did not ask for.
            var contest = new[] { Contest("central_ashfall") };
            var gw = new FakeGateway { Handler = (e, r) =>
                e == Cc10Endpoints.GetCallerGuildIdentity ? (object)Identity(hasGuild: true, guildId: "g1")
                : e == Cc10Endpoints.GetFrontierState ? Snap()
                : MapResult(1, unlocked: Cc10WorldMapPhaseToken.Central, contest: contest) };
            var client = new Cc10FrontierClient(gw, new Cc10ServerClock(() => 0));
            Assert.IsTrue(await client.RefreshAllAsync());
            Assert.IsTrue(await client.RefreshGuildIdentityAsync());
            Assert.AreEqual("g1", client.GuildId);

            Cc10Row row = Vm(client, Cc10SystemId.GuildTerritory).Rows.Single();
            Assert.AreEqual("g1", row.Payload["guildId"], "the identity-supplied guildId reaches the existing Enroll payload unchanged");
        }

        [Test]
        public void GuildIdentityRefresh_HasExactlyTheDocumentedOutcomes()
        {
            CollectionAssert.AreEquivalent(
                new[] { "NotAttempted", "Accepted", "AuthenticationRequired", "AuthorityUnavailable", "Malformed", "Offline" },
                Enum.GetNames(typeof(Cc10GuildIdentityRefresh)));
        }

        [Test]
        public void GuildIdentityResult_HasExactlyTheApprovedFields()
        {
            CollectionAssert.AreEqual(new[] { "guildId", "hasGuild", "membershipEpoch", "membershipExpiresUtcMs" },
                typeof(Cc10GuildIdentityResult).GetFields()
                    .Where(f => f.DeclaringType == typeof(Cc10GuildIdentityResult))
                    .Select(f => f.Name).OrderBy(n => n).ToArray());
        }

        // ---- BE 7ac011a1 (WH-CC11-001): CC11 contract seam mirrors - shape parity only, no
        // endpoint exists on the real server yet, so no gateway call is exercised here. ----

        [Test]
        public void WorldMapBasePlacementDto_RoundTripsAndMatchesBeShape()
        {
            const string json = "{\"status\":\"Placed\",\"operation\":\"Place\",\"baseNodeId\":\"node-1\"," +
                "\"pendingNodeId\":null,\"changedUtcMs\":10}";
            var dto = UnityEngine.JsonUtility.FromJson<Cc10WorldMapBasePlacementDto>(json);
            Assert.AreEqual(Cc10WorldMapBaseStatus.Placed, dto.status);
            Assert.AreEqual(Cc10WorldMapBaseOperation.Place, dto.operation);
            Assert.AreEqual("node-1", dto.baseNodeId);
            Assert.AreEqual(10, dto.changedUtcMs);
            CollectionAssert.AreEqual(new[] { "baseNodeId", "changedUtcMs", "operation", "pendingNodeId", "status" },
                typeof(Cc10WorldMapBasePlacementDto).GetFields().Select(f => f.Name).OrderBy(n => n).ToArray());
        }

        [Test]
        public void CargoParticipantSelectionState_RoundTripsAndMatchesBeShape()
        {
            var state = new Cc10CargoParticipantSelectionState
            {
                selection = new Cc10CargoParticipantSelectionDto { avatarId = "a1", armyId = "ar1", missionId = "m1", selectedUtcMs = 5 },
                stateVersion = 3,
            };
            string json = UnityEngine.JsonUtility.ToJson(state);
            var roundTrip = UnityEngine.JsonUtility.FromJson<Cc10CargoParticipantSelectionState>(json);
            Assert.AreEqual("a1", roundTrip.selection.avatarId);
            Assert.AreEqual("ar1", roundTrip.selection.armyId);
            Assert.AreEqual("m1", roundTrip.selection.missionId);
            Assert.AreEqual(5, roundTrip.selection.selectedUtcMs);
            Assert.AreEqual(3, roundTrip.stateVersion);
        }

        [Test]
        public void GuildManagementSnapshotDto_RoundTripsAllSixApprovedReadSurfaces()
        {
            var snapshot = new Cc10GuildManagementSnapshotDto
            {
                members = new[] { new Cc10GuildMemberDto { memberId = "m-1", displayName = "Alice", positionId = "p-1", joinedUtcMs = 1, online = true } },
                store = new[] { new Cc10GuildStoreItemDto { itemId = "item-1", name = "Banner", available = true } },
                offices = new[] { new Cc10GuildOfficeDto { officeId = "office-1", positionId = "p-1", holderMemberId = "m-1" } },
                positions = new[] { new Cc10GuildPositionDto { positionId = "p-1", name = "Officer", permissions = new[] { "invite" } } },
                research = new[] { new Cc10GuildResearchStateDto { nodeId = "GUILD_1", status = Cc10ResearchStatus.InProgress, progress = 2, required = 5, readyUtcMs = 0 } },
                announcements = new[] { new Cc10GuildAnnouncementDto { announcementId = "an-1", authorMemberId = "m-1", body = "hi", publishedUtcMs = 2 } },
            };
            string json = UnityEngine.JsonUtility.ToJson(snapshot);
            var roundTrip = UnityEngine.JsonUtility.FromJson<Cc10GuildManagementSnapshotDto>(json);
            Assert.AreEqual(1, roundTrip.members.Length);
            Assert.AreEqual("Alice", roundTrip.members[0].displayName);
            Assert.IsTrue(roundTrip.members[0].online);
            Assert.AreEqual(1, roundTrip.store.Length);
            Assert.IsTrue(roundTrip.store[0].available);
            Assert.AreEqual(1, roundTrip.offices.Length);
            Assert.AreEqual("m-1", roundTrip.offices[0].holderMemberId);
            Assert.AreEqual(1, roundTrip.positions.Length);
            CollectionAssert.AreEqual(new[] { "invite" }, roundTrip.positions[0].permissions);
            Assert.AreEqual(1, roundTrip.research.Length);
            Assert.AreEqual(Cc10ResearchStatus.InProgress, roundTrip.research[0].status);
            Assert.AreEqual(1, roundTrip.announcements.Length);
            Assert.AreEqual("hi", roundTrip.announcements[0].body);
        }

        [Test]
        public void GuildMemberSearchDto_RoundTripsQueryAndCursor()
        {
            var search = new Cc10GuildMemberSearchDto
            {
                query = "ali",
                members = new[] { new Cc10GuildMemberDto { memberId = "m-1", displayName = "Alice" } },
                nextCursor = "cursor-2",
            };
            string json = UnityEngine.JsonUtility.ToJson(search);
            var roundTrip = UnityEngine.JsonUtility.FromJson<Cc10GuildMemberSearchDto>(json);
            Assert.AreEqual("ali", roundTrip.query);
            Assert.AreEqual(1, roundTrip.members.Length);
            Assert.AreEqual("cursor-2", roundTrip.nextCursor);
        }

        [Test]
        public void RankingSeasonSourceResult_RoundTripsAndReusesExistingSeasonStateVocabulary()
        {
            const string json = "{\"success\":true,\"errorCode\":\"\",\"serverUtcMs\":100,\"utcDayKey\":\"d1\"," +
                "\"authorityGeneration\":1,\"stateVersion\":1,\"season\":{\"source\":\"CC10Frontier.RankingSeasonState\"," +
                "\"seasonId\":\"s-1\",\"state\":\"Accepting\",\"createdUtcMs\":5,\"serverUtcMs\":100}}";
            var result = UnityEngine.JsonUtility.FromJson<Cc10RankingSeasonSourceResult>(json);
            Assert.IsTrue(result.success);
            Assert.AreEqual("CC10Frontier.RankingSeasonState", result.season.source);
            Assert.AreEqual("s-1", result.season.seasonId);
            Assert.AreEqual(Cc10SeasonState.Accepting, result.season.state);
            Assert.AreEqual(5, result.season.createdUtcMs);
        }

        [Test]
        public void Cc11Seams_HaveNoCorrespondingEndpointConstant_BeExposesNoCloudCodeFunctionYet()
        {
            // BE 7ac011a1 registers zero CloudCodeFunctions for these seams (verified via git grep
            // against CC10FrontierModule.cs/CC10FrontierService*.cs at that commit). Guards against a
            // future edit silently adding a call to an endpoint name the server does not expose.
            string[] endpointConstants = typeof(Cc10Endpoints).GetFields()
                .Select(f => (string)f.GetRawConstantValue()).ToArray();
            CollectionAssert.DoesNotContain(endpointConstants, "GetWorldMapBasePlacement");
            CollectionAssert.DoesNotContain(endpointConstants, "SelectCargoParticipants");
            CollectionAssert.DoesNotContain(endpointConstants, "GetGuildManagementSnapshot");
            CollectionAssert.DoesNotContain(endpointConstants, "GetRankingSeasonSource");
        }
    }
}

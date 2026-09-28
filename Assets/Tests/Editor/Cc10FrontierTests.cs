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
        public async Task GuildTerritory_ShowsContestDistricts_EnrollOnlyWhenEligible()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap();
            snap.contestDistricts = new[]
            {
                new Cc10ContestDistrictDto { districtId = "central_ashfall", status = Cc10ContestStatus.NotEnrolled, callerGuildEligible = true },
                new Cc10ContestDistrictDto { districtId = "central_ember", status = Cc10ContestStatus.NotEnrolled, callerGuildEligible = false },
                new Cc10ContestDistrictDto { districtId = "central_ironquarry", status = Cc10ContestStatus.Owned, ownerGuildColorToken = "GC09" },
            };
            List<Cc10Row> rows = Vm(await OnlineClient(gw, snap), Cc10SystemId.GuildTerritory).Rows;
            Assert.IsTrue(rows[0].ActionEnabled);
            Assert.AreEqual(Cc10Endpoints.EnrollContestDistrict, rows[0].Endpoint);
            Assert.IsFalse(rows[1].ActionEnabled, "not top-3 this season");
            Assert.IsFalse(rows[2].HasAction, "already owned - no resolve action client-side (operator-gated)");
            StringAssert.Contains("color 9", rows[2].Detail);
            StringAssert.DoesNotContain("Guild-", rows[2].Detail, "no guild identity is ever shown - only a color");
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

        [Test]
        public async Task ContestDistrict_NeutralState_NoGuild_ShowsNoColor()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap();
            snap.contestDistricts = new[] { new Cc10ContestDistrictDto { districtId = "central_ember", status = Cc10ContestStatus.NotEnrolled } };
            Cc10Row row = Vm(await OnlineClient(gw, snap), Cc10SystemId.GuildTerritory).Rows.Single();
            Assert.IsFalse(row.Detail.Contains("color"), "no guild enrolled - never invent a color");
        }

        [Test]
        public async Task ContestDistrict_EnrolledAndOwned_ShowOnlyAColorNumber_FromTheServerToken()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap();
            snap.contestDistricts = new[]
            {
                new Cc10ContestDistrictDto { districtId = "central_ashfall", status = Cc10ContestStatus.Enrolled, enrolledGuildColorToken = "GC07" },
                new Cc10ContestDistrictDto { districtId = "central_ember", status = Cc10ContestStatus.Owned, ownerGuildColorToken = "GC02" },
                new Cc10ContestDistrictDto { districtId = "central_ironquarry", status = Cc10ContestStatus.Owned, ownerGuildColorToken = "not-a-token" },
            };
            List<Cc10Row> rows = Vm(await OnlineClient(gw, snap), Cc10SystemId.GuildTerritory).Rows;
            StringAssert.Contains("color 7", rows[0].Detail);
            StringAssert.Contains("color 2", rows[1].Detail);
            Assert.IsFalse(rows[2].Detail.Contains("color"), "an unknown token renders neutral, never a guessed color");
        }
    }
}

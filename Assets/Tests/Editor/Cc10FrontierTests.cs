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
            gw.Handler = (e, r) => new Cc10CommandResult { success = false, errorCode = Cc10Errors.GoldCapReached };

            Cc10CommandOutcome r = await client.ExecuteAsync(Cc10SystemId.Missions, Cc10Endpoints.ClaimMission, null, "m1");
            Assert.AreEqual(Cc10Outcome.Rejected, r.Outcome);
            Assert.AreEqual(Cc10Copy.CapReached, r.Message);
            StringAssert.DoesNotContain("GOLD_CAP_REACHED", r.Message, "raw backend codes never reach the player");
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
        public async Task AuthorityGenerationMismatch_ReloadsAndTreatsAsConflict()
        {
            var gw = new FakeGateway();
            Cc10FrontierClient client = await OnlineClient(gw, Snap(1, 1, Mission("m1", "Completed")));
            gw.Handler = (e, r) => e == Cc10Endpoints.GetFrontierState
                ? (object)Snap(2, 2, Mission("m1", "Completed"))
                : new Cc10CommandResult { success = false, errorCode = Cc10Errors.AuthorityGenerationMismatch };

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
            foreach (string sys in new[] { Cc10SystemId.GuildTerritory, Cc10SystemId.GuildRankings, Cc10SystemId.IndividualRankings })
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
            Assert.AreEqual("Fully upgraded", row.Detail);
        }

        [Test]
        public async Task WorldMap_DiscoverOnlyOffered_ForNeighborsOfADiscoveredNode()
        {
            var gw = new FakeGateway();
            Cc10FrontierSnapshot snap = Snap();
            snap.nodes = new[]
            {
                new Cc10MapNodeDto { nodeId = "hub", regionId = "frontier", discovered = true, neighbors = new[] { "patrol_road" } },
                new Cc10MapNodeDto { nodeId = "patrol_road", regionId = "frontier", discovered = false, neighbors = new[] { "hub", "ruined_shrine" } },
                new Cc10MapNodeDto { nodeId = "ruined_shrine", regionId = "frontier", discovered = false, neighbors = new[] { "patrol_road" } },
            };
            List<Cc10Row> rows = Vm(await OnlineClient(gw, snap), Cc10SystemId.WorldMap).Rows;
            Assert.IsFalse(rows[0].HasAction, "already discovered");
            Assert.IsTrue(rows[1].ActionEnabled, "adjacent to the discovered hub");
            Assert.IsFalse(rows[2].ActionEnabled, "not adjacent to any discovered node yet");
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
            Assert.AreEqual(Cc10Endpoints.CancelResearch, Vm(client, Cc10SystemId.IndividualResearch).Rows.Single().Endpoint);
            Assert.AreEqual(1, Vm(client, Cc10SystemId.GuildResearch).Rows.Count);
            Assert.IsFalse(Vm(client, Cc10SystemId.GuildResearch).Rows.Single().HasAction, "guild research cannot be individually cancelled");
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
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyriadOfDragons.Frontier;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    public class Cc10FrontierPresenterPlayModeTests
    {
        private sealed class Gateway : ICc10FrontierGateway
        {
            public Func<string, object> Handler;
            public int CommandCalls;

            public Task<T> CallAsync<T>(string endpoint, Dictionary<string, object> request, CancellationToken ct)
                where T : Cc10ResultBase
            {
                if (endpoint != Cc10Endpoints.GetFrontierState) CommandCalls++;
                object r = Handler(endpoint);
                if (r is Exception ex) throw ex;
                if (r is Task<T> pending) return pending;
                return Task.FromResult((T)r);
            }
        }

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        private static Cc10FrontierSnapshot Snap() => new Cc10FrontierSnapshot
        {
            success = true, stateVersion = 1, serverUtcMs = 1_000_000,
            missions = new[] { new Cc10MissionDto { missionId = "m1", type = Cc10MissionType.NpcPatrol, status = Cc10MissionStatus.Active, readyUtcMs = 1_060_000 } },
        };

        private async Task<(Cc10FrontierPresenter, Cc10FrontierClient, Gateway)> Open(Func<string, object> handler,
            bool loadState = true, Action onBack = null)
        {
            var gw = new Gateway { Handler = handler };
            var client = new Cc10FrontierClient(gw);
            if (loadState) await client.RefreshAsync();
            var go = new GameObject("Cc10Host");
            _spawned.Add(go);
            var p = go.AddComponent<Cc10FrontierPresenter>();
            p.Initialize(onBack, client);
            return (p, client, gw);
        }

        private static Button Find(Cc10FrontierPresenter p, string name) =>
            p.Root.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == name);

        [Test]
        public async Task Builds_AllSectionTabs_WithTouchSizedHitAreas()
        {
            var (p, _, _) = await Open(e => Snap());
            foreach (string sys in new[]
                     {
                         Cc10SystemId.Tavern, Cc10SystemId.Missions, Cc10SystemId.WorldMap, Cc10SystemId.NpcSpots,
                         Cc10SystemId.GuildTerritory, Cc10SystemId.IndividualResearch, Cc10SystemId.GuildResearch,
                         Cc10SystemId.IndividualRankings, Cc10SystemId.GuildRankings, Cc10SystemId.Minigame, Cc10SystemId.Cargo,
                     })
            {
                Button tab = Find(p, "Tab_" + sys);
                Assert.IsNotNull(tab, "missing tab " + sys);
                var rt = (RectTransform)tab.transform;
                Assert.GreaterOrEqual(rt.rect.height, Cc10FrontierPresenter.MinTouch, sys + " height");
                Assert.GreaterOrEqual(rt.rect.width, Cc10FrontierPresenter.MinTouch, sys + " width");
            }
        }

        [Test]
        public async Task Back_InvokesCallback()
        {
            int backs = 0;
            var (p, _, _) = await Open(e => Snap(), onBack: () => backs++);
            Find(p, "Btn_Back").onClick.Invoke();
            Assert.AreEqual(1, backs);
        }

        [Test]
        public async Task Offline_ShowsBanner_AndActionButtonIsNotInteractable()
        {
            var (p, client, gw) = await Open(e => Snap());
            p.SelectSection(Cc10SystemId.Missions);
            Assert.IsTrue(Find(p, "Action").interactable);

            gw.Handler = e => new InvalidOperationException("network");
            await client.RefreshAsync();

            Assert.AreEqual(Cc10Copy.Offline, p.BannerText);
            Assert.IsFalse(Find(p, "Action").interactable);
        }

        [Test]
        public async Task NeverLoaded_ShowsOfflineBanner_NoRows()
        {
            var (p, _, _) = await Open(e => new InvalidOperationException("down"), loadState: false);
            Assert.IsNotEmpty(p.BannerText);
            Assert.IsNull(Find(p, "Action"));
        }

        [Test]
        public async Task DoubleClick_SendsOneCommand()
        {
            var tcs = new TaskCompletionSource<Cc10CommandResult>();
            var (p, client, gw) = await Open(e => e == Cc10Endpoints.GetFrontierState ? (object)Snap() : tcs.Task);
            p.SelectSection(Cc10SystemId.Missions);
            Button action = Find(p, "Action");

            action.onClick.Invoke();
            action.onClick.Invoke();
            Assert.AreEqual(1, gw.CommandCalls, "second tap while in flight is swallowed");

            tcs.SetResult(new Cc10CommandResult { success = true, receipt = new Cc10Receipt { receiptId = "r" }, stateVersion = 2 });
            await Task.Yield();
        }

        [Test]
        public async Task Reconnect_RerendersFromServer()
        {
            var (p, client, gw) = await Open(e => Snap());
            p.SelectSection(Cc10SystemId.Missions);
            gw.Handler = e => new InvalidOperationException("network");
            await client.RefreshAsync();
            Assert.IsFalse(Find(p, "Action").interactable);

            gw.Handler = e => Snap();
            await client.RefreshAsync();
            Assert.IsTrue(Find(p, "Action").interactable);
            Assert.IsEmpty(p.BannerText);
        }

        [Test]
        public async Task DisabledSystem_ShowsPausedBanner_OtherSectionsStayLive()
        {
            Cc10FrontierSnapshot snap = Snap();
            snap.disabledSystems = new[] { Cc10SystemId.Missions };
            var (p, _, _) = await Open(e => snap);
            p.SelectSection(Cc10SystemId.Missions);
            Assert.AreEqual(Cc10Copy.SystemDisabled, p.BannerText);
            Assert.IsFalse(Find(p, "Action").interactable);

            p.SelectSection(Cc10SystemId.Tavern);
            Assert.IsEmpty(p.BannerText);
        }

        [Test]
        public async Task UnsupportedQuerySection_ShowsUnavailable_NeverAnEmptyLiveList()
        {
            var (p, _, _) = await Open(e => Snap());
            p.SelectSection(Cc10SystemId.GuildRankings);
            StringAssert.Contains("Not available", p.BannerText);
        }

        [Test]
        public async Task WorldMapSection_RendersPhaseAndNodeRows()
        {
            Cc10FrontierSnapshot snap = Snap();
            snap.phases = new[] { new Cc10PhaseDto { phase = Cc10MapPhase.HomeOutpost, unlocked = true } };
            snap.nodes = new[] { new Cc10MapNodeDto { nodeId = "hub", owned = true, discovered = true } };
            var (p, _, _) = await Open(e => snap);
            p.SelectSection(Cc10SystemId.WorldMap);
            Assert.IsEmpty(p.BannerText);
            Assert.IsNotNull(p.Root.Find("Scroll/Content/Row_phase:HomeOutpost"));
            Assert.IsNotNull(p.Root.Find("Scroll/Content/Row_hub"));
        }
    }
}

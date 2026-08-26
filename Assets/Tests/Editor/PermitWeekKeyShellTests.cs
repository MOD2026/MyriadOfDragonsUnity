using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    public class PermitWeekKeyShellTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsPermitWeekKeyShell_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, true);
        }

        [Test]
        public void Presenter_BuildsArtShell_AndStateIcon()
        {
            var go = new GameObject("PermitWeekKeyArtHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<PermitWeekKeyPresenter>();
            presenter.Initialize(onBack: null, gateway: new FakePermitWeekKeyGateway());

            GameObject canvas = presenter.CanvasObjectForTests;
            Assert.IsTrue(PermitWeekKeyUiLibrary.HasPermitWeekKeyV1Pack);
            Assert.AreEqual(PermitWeekKeyUiLibrary.ScreenShellName,
                canvas.transform.Find("Background")?.GetComponent<Image>()?.sprite?.name);
            Assert.IsNotNull(canvas.transform.Find("PermitPanel/PermitStateIcon")?.GetComponent<Image>()?.sprite);
        }

        [Test]
        public async Task Presenter_StatusAndClaim_UseInjectedGateway()
        {
            var go = new GameObject("PermitWeekKeyHarness");
            _spawned.Add(go);
            var fake = new FakePermitWeekKeyGateway();
            var presenter = go.AddComponent<PermitWeekKeyPresenter>();
            presenter.Initialize(onBack: null, gateway: fake);

            Assert.NotNull(presenter.CanvasObjectForTests);
            Assert.AreEqual(PermitWeekKeyPresenter.DefaultActivityId, presenter.ActivityIdForTests);
            Assert.NotNull(presenter.CanvasObjectForTests.transform.Find("PermitPanel/Btn_ClaimWeekly"));

            // Initialize already kicked off a status refresh; wait and assert again via explicit call.
            PermitStatusResult status = await presenter.RefreshStatusForTests();
            Assert.AreEqual(3, status.balance);
            Assert.AreEqual("2026-W34", status.currentWeekKey);
            Assert.GreaterOrEqual(fake.StatusCalls, 1);

            PermitClaimResult claim = await presenter.ClaimWeeklyForTests();
            Assert.IsTrue(claim.success);
            Assert.AreEqual(4, claim.granted);
            Assert.AreEqual(1, fake.ClaimCalls);
            Assert.AreEqual(PermitWeekKeyPresenter.DefaultActivityId, fake.LastActivityId);
        }

        [Test]
        public void Home_ServerKeyButton_OpensShell_AndBackReturnsHome()
        {
            // Real reachability path since the Home IA rebuild - SERVER-KEY and the weekly permit
            // claim merge into ONE Quests/Events "PERMIT" tab (register: "no two permanent Home
            // buttons for what's really one logical feature"). The tab runs both sub-states
            // (local claim, then this real server-authoritative screen).
            var go = new GameObject("HomePermitWeekKeyReach");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            home.BuildHomePageUIForTests();
            GameObject homeCanvas = home.HomeCanvasObjectForTests;

            home.OpenQuestsEventsHubForTests();
            GameObject hub = home.TabHubObjectForTests;
            Assert.NotNull(hub, "Setup: expected the Quests/Events hub to open.");
            Button openBtn = hub.transform.Find("Dest_PERMIT")?.GetComponent<Button>();
            Assert.NotNull(openBtn, "Setup: expected a PERMIT tab inside the Quests/Events hub.");
            openBtn.onClick.Invoke();
            Assert.IsFalse(homeCanvas.activeSelf);
            Assert.NotNull(GameObject.Find(PermitWeekKeyPresenter.CanvasName));

            Button back = GameObject.Find(PermitWeekKeyPresenter.CanvasName).transform
                .Find("PermitWeekKeyHeader/Btn_Back")?.GetComponent<Button>();
            Assert.NotNull(back);
            back.onClick.Invoke();
            Assert.IsTrue(homeCanvas.activeSelf);
            Assert.IsNull(go.GetComponent<PermitWeekKeyPresenter>());
        }

        private sealed class FakePermitWeekKeyGateway : IPermitWeekKeyGateway
        {
            public int StatusCalls;
            public int ClaimCalls;
            public string LastActivityId;

            public Task<PermitStatusResult> GetStatusAsync(string activityId, CancellationToken cancellationToken)
            {
                StatusCalls++;
                LastActivityId = activityId;
                return Task.FromResult(new PermitStatusResult
                {
                    balance = 3,
                    currentWeekKey = "2026-W34",
                    claimedThisWeek = false,
                    weeklyRate = 4,
                    hoardCap = 8,
                });
            }

            public Task<PermitClaimResult> ClaimWeeklyAsync(string activityId, CancellationToken cancellationToken)
            {
                ClaimCalls++;
                LastActivityId = activityId;
                return Task.FromResult(new PermitClaimResult
                {
                    success = true,
                    granted = 4,
                    balance = 7,
                    weekKey = "2026-W34",
                    alreadyClaimed = false,
                });
            }
        }
    }
}

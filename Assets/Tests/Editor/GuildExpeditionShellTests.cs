using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Metagame;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    public class GuildExpeditionShellTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsGuildExpeditionShell_" + System.Guid.NewGuid().ToString("N"));
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
        public void Catalog_MatchesCloudCodeScaffoldBandsAndObjectives()
        {
            Assert.AreEqual(11, GuildExpeditionPresenter.ScaffoldObjectiveIds.Length);
            CollectionAssert.Contains(GuildExpeditionPresenter.ScaffoldObjectiveIds, "scout.revealEnemyDeck");
            CollectionAssert.AreEqual(new[] { 100, 250, 400, 700, 1000 }, GuildExpeditionPresenter.MilestoneThresholds);
        }

        [Test]
        public void Presenter_BuildsArtShell_AndStageIcons()
        {
            var go = new GameObject("GuildExpeditionArtHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<GuildExpeditionPresenter>();
            presenter.Initialize(onBack: null, gateway: new FakeGuildExpeditionGateway());

            GameObject canvas = presenter.CanvasObjectForTests;
            Assert.IsTrue(GuildExpeditionUiLibrary.HasGuildExpeditionV1Pack);
            Assert.AreEqual(GuildExpeditionUiLibrary.ScreenShellName,
                canvas.transform.Find("Background")?.GetComponent<Image>()?.sprite?.name);
            Assert.IsNotNull(canvas.transform.Find("ExpeditionPanel/ObjectiveGrid/Objective_0/StageIcon")
                ?.GetComponent<Image>()?.sprite);
            Assert.AreEqual(GuildExpeditionUiLibrary.StageAvailableName,
                canvas.transform.Find("ExpeditionPanel/ObjectiveGrid/Objective_0/StageIcon")
                    ?.GetComponent<Image>()?.sprite?.name);
        }

        [Test]
        public async Task Presenter_GatewayActions_UseInjectedGateway()
        {
            var go = new GameObject("GuildExpeditionHarness");
            _spawned.Add(go);
            var fake = new FakeGuildExpeditionGateway();
            var presenter = go.AddComponent<GuildExpeditionPresenter>();
            presenter.Initialize(onBack: null, gateway: fake);

            Assert.NotNull(presenter.CanvasObjectForTests);
            Assert.NotNull(presenter.CanvasObjectForTests.transform.Find("ExpeditionPanel/Btn_ConsumeAttempt"));
            Assert.NotNull(presenter.CanvasObjectForTests.transform.Find("ExpeditionPanel/MilestoneStrip/Milestone_1000"));

            GuildExpeditionAttemptResult attempt = await presenter.ConsumeAttemptForTests();
            Assert.IsTrue(attempt.success);
            Assert.AreEqual(2, attempt.remaining);
            Assert.AreEqual(1, fake.ConsumeCalls);

            GuildExpeditionObjectiveResult scored = await presenter.SubmitSelectedObjectiveForTests();
            Assert.IsTrue(scored.success);
            Assert.AreEqual("scout.revealEnemyDeck", fake.LastObjectiveId);
            Assert.AreEqual(40, scored.pointsAwarded);

            presenter.CanvasObjectForTests.transform
                .Find("ExpeditionPanel/MilestoneStrip/Milestone_400").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(400, presenter.SelectedMilestoneForTests);
            GuildExpeditionMilestoneResult claimed = await presenter.ClaimSelectedMilestoneForTests();
            Assert.IsTrue(claimed.success);
            Assert.AreEqual(400, claimed.threshold);
            Assert.AreEqual(1, fake.ClaimCalls);
        }

        /// <summary>
        /// Real bug fix (CC 2026-08-27): a real player reaching this screen via the only
        /// production call site (GuildHallEntryPresenter.OpenGuildExpedition, which never
        /// injects a gateway) previously hit the real, deployed CloudCode module directly and
        /// could see raw backend failure text ("Consume failed: unknown", "Consume: null
        /// response.") instead of an honest "not live yet" message. This proves the gate: with
        /// NO gateway injected (the real production path), the StatusLine already reads the
        /// deferred-feature message before any action is taken, and every action refuses with
        /// errorCode NOT_LIVE - the real gateway is never constructed into a live call.
        /// </summary>
        [Test]
        public async Task Presenter_NoGatewayInjected_RefusesBeforeAnyRealCall()
        {
            var go = new GameObject("GuildExpeditionRealPath");
            _spawned.Add(go);
            var presenter = go.AddComponent<GuildExpeditionPresenter>();
            presenter.Initialize(onBack: null); // No gateway - exactly what GuildHallEntryPresenter does.

            Assert.AreEqual(GuildExpeditionOpenValues.PlayerStatus, presenter.StatusTextForTests,
                "STATE UNREACHED: the real production path must show the deferred-feature status " +
                "before any action is taken, not 'Ready.'.");

            GuildExpeditionAttemptResult attempt = await presenter.ConsumeAttemptForTests();
            Assert.IsFalse(attempt.success);
            Assert.AreEqual("NOT_LIVE", attempt.errorCode);
            Assert.AreEqual(GuildExpeditionOpenValues.PlayerStatus, presenter.StatusTextForTests,
                "STATE UNREACHED: ConsumeAttempt must refuse with the player-facing message, not a raw backend result.");

            GuildExpeditionObjectiveResult submit = await presenter.SubmitSelectedObjectiveForTests();
            Assert.IsFalse(submit.success);
            Assert.AreEqual("NOT_LIVE", submit.errorCode);

            GuildExpeditionMilestoneResult claim = await presenter.ClaimSelectedMilestoneForTests();
            Assert.IsFalse(claim.success);
            Assert.AreEqual("NOT_LIVE", claim.errorCode);
        }

        [Test]
        public void GuildHallEntry_OpensGuildExpedition()
        {
            var go = new GameObject("GuildHallToExpedition");
            _spawned.Add(go);
            var hall = go.AddComponent<GuildHallEntryPresenter>();
            hall.Initialize(onBack: null);
            Assert.NotNull(GameObject.Find(GuildHallEntryPresenter.CanvasName));

            hall.PressEntryForTests();
            Assert.IsNull(GameObject.Find(GuildHallEntryPresenter.CanvasName));
            Assert.NotNull(go.GetComponent<GuildExpeditionPresenter>());
            Assert.NotNull(GameObject.Find(GuildExpeditionPresenter.CanvasName));
        }

        private sealed class FakeGuildExpeditionGateway : IGuildExpeditionGateway
        {
            public int ConsumeCalls;
            public int ClaimCalls;
            public string LastObjectiveId;

            public Task<GuildExpeditionAttemptResult> ConsumeAttemptAsync(CancellationToken cancellationToken)
            {
                ConsumeCalls++;
                return Task.FromResult(new GuildExpeditionAttemptResult { success = true, remaining = 2 });
            }

            public Task<GuildExpeditionObjectiveResult> SubmitObjectiveResultAsync(string objectiveId, CancellationToken cancellationToken)
            {
                LastObjectiveId = objectiveId;
                return Task.FromResult(new GuildExpeditionObjectiveResult
                {
                    success = true,
                    pointsAwarded = 40,
                    totalPoints = 40,
                    weekKey = "2026-W34",
                });
            }

            public Task<GuildExpeditionMilestoneResult> ClaimMilestoneAsync(int threshold, CancellationToken cancellationToken)
            {
                ClaimCalls++;
                return Task.FromResult(new GuildExpeditionMilestoneResult
                {
                    success = true,
                    threshold = threshold,
                    guildContributionGranted = 10,
                });
            }
        }
    }
}

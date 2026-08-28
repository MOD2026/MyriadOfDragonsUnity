using System.IO;
using System.Threading.Tasks;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// WH-UI-METAGAME-TRANSITION-PROTOTYPE-001 — Guild Expedition canvas fade open/close.
    /// </summary>
    public class GuildExpeditionTransitionTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            MotionPolicy.ReduceMotion = false;
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MoDGuildExpeditionTransition_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            MotionPolicy.ReduceMotion = false;
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

        private GuildExpeditionPresenter Open()
        {
            var go = new GameObject("GuildExpeditionTransitionHost");
            _spawned.Add(go);
            var presenter = go.AddComponent<GuildExpeditionPresenter>();
            presenter.Initialize(onBack: null, gateway: new FakeGateway());
            return presenter;
        }

        [Test]
        public async Task OpenTransition_ReachesVisibleState()
        {
            GuildExpeditionPresenter presenter = Open();
            await presenter.WaitForOpenTransitionForTests();
            Assert.IsNotNull(presenter.CanvasObjectForTests);
            Assert.AreEqual(1f, presenter.CanvasAlphaForTests, 0.001f);
        }

        [Test]
        public async Task BackTransition_CompletesBeforeTeardown()
        {
            GuildExpeditionPresenter presenter = Open();
            await presenter.WaitForOpenTransitionForTests();
            Assert.IsNotNull(presenter.CanvasObjectForTests);

            await presenter.PressBackForTests();
            Assert.IsTrue(presenter.BackFadeCompletedBeforeTeardownForTests,
                "Fade-out must finish (alpha 0) while the canvas still exists, before TeardownUI.");
            Assert.IsNull(presenter.CanvasObjectForTests);
        }

        [Test]
        public void DuplicateTeardown_IsHarmless()
        {
            GuildExpeditionPresenter presenter = Open();
            Assert.IsNotNull(presenter.CanvasObjectForTests);
            Assert.DoesNotThrow(() =>
            {
                presenter.TeardownUI();
                presenter.TeardownUI();
                presenter.TeardownUI();
            });
            Assert.IsNull(presenter.CanvasObjectForTests);
            Assert.AreEqual(-1f, presenter.CanvasAlphaForTests);
        }

        [Test]
        public void ReducedMotion_CompletesImmediately()
        {
            Assert.AreEqual(0f,
                GuildExpeditionPresenter.ResolveTransitionDurationSeconds(
                    GuildExpeditionPresenter.TransitionDurationSeconds, reduceMotion: true));
            Assert.Greater(
                GuildExpeditionPresenter.ResolveTransitionDurationSeconds(
                    GuildExpeditionPresenter.TransitionDurationSeconds, reduceMotion: false),
                0f);

            MotionPolicy.ReduceMotion = true;
            GuildExpeditionPresenter presenter = Open();
            Assert.AreEqual(1f, presenter.CanvasAlphaForTests, 0.001f,
                "With ReduceMotion, open fade must snap to visible without waiting.");
        }

        private sealed class FakeGateway : IGuildExpeditionGateway
        {
            public Task<GuildExpeditionAttemptResult> ConsumeAttemptAsync(
                System.Threading.CancellationToken cancellationToken) =>
                Task.FromResult(new GuildExpeditionAttemptResult { success = true, remaining = 1 });

            public Task<GuildExpeditionObjectiveResult> SubmitObjectiveResultAsync(
                string objectiveId, System.Threading.CancellationToken cancellationToken) =>
                Task.FromResult(new GuildExpeditionObjectiveResult { success = true });

            public Task<GuildExpeditionMilestoneResult> ClaimMilestoneAsync(
                int threshold, System.Threading.CancellationToken cancellationToken) =>
                Task.FromResult(new GuildExpeditionMilestoneResult { success = true, threshold = threshold });
        }
    }
}

using System.IO;
using System.Threading.Tasks;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// WH-UI-SETTINGS-TRANSITION-001 — Settings canvas fade open/close (same pattern as Guild Expedition).
    /// </summary>
    public class SettingsTransitionTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            MotionPolicy.ReduceMotion = false;
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MoDSettingsTransition_" + System.Guid.NewGuid().ToString("N"));
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

        private SettingsPresenter Open()
        {
            var go = new GameObject("SettingsTransitionHost");
            _spawned.Add(go);
            var presenter = go.AddComponent<SettingsPresenter>();
            presenter.Initialize(onBackToHome: null);
            return presenter;
        }

        [Test]
        public async Task OpenTransition_ReachesVisibleState()
        {
            SettingsPresenter presenter = Open();
            await presenter.WaitForOpenTransitionForTests();
            Assert.IsNotNull(presenter.CanvasObjectForTests);
            Assert.AreEqual(1f, presenter.CanvasAlphaForTests, 0.001f);
        }

        [Test]
        public async Task BackTransition_CompletesBeforeTeardown()
        {
            SettingsPresenter presenter = Open();
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
            SettingsPresenter presenter = Open();
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
                SettingsPresenter.ResolveTransitionDurationSeconds(
                    SettingsPresenter.TransitionDurationSeconds, reduceMotion: true));
            Assert.Greater(
                SettingsPresenter.ResolveTransitionDurationSeconds(
                    SettingsPresenter.TransitionDurationSeconds, reduceMotion: false),
                0f);

            MotionPolicy.ReduceMotion = true;
            SettingsPresenter presenter = Open();
            Assert.AreEqual(1f, presenter.CanvasAlphaForTests, 0.001f,
                "With ReduceMotion, open fade must snap to visible without waiting.");
        }
    }
}

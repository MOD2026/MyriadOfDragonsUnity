using System.IO;
using MyriadOfDragons.Metagame;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    public class MemoryExpeditionShellTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsMemoryExpeditionShell_" + System.Guid.NewGuid().ToString("N"));
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
        public void OpenValues_RefuseWhileUnset()
        {
            StringAssert.Contains("OPEN", MemoryExpeditionOpenValues.StatusNote.ToUpperInvariant());
            Assert.AreEqual("[runtime]", MemoryExpeditionOpenValues.RuntimePlaceholder);
        }

        [Test]
        public void Presenter_BuildsArtShell_AndActionsRefuse()
        {
            var go = new GameObject("MemoryExpeditionHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<MemoryExpeditionPresenter>();
            presenter.Initialize(onBack: null);

            GameObject canvas = presenter.CanvasObjectForTests;
            Assert.NotNull(canvas);

            Assert.IsTrue(MemoryExpeditionUiLibrary.HasMemoryExpeditionV1Pack);
            Assert.AreEqual(MemoryExpeditionUiLibrary.RouteChoiceShellName,
                canvas.transform.Find("Background")?.GetComponent<Image>()?.sprite?.name);
            Assert.NotNull(canvas.transform.Find("RouteRow/RouteWell_2"));
            StringAssert.Contains("OPEN",
                canvas.transform.Find("RouteRow/RouteWell_0/Placeholder")?.GetComponent<Text>()?.text);
            var refuse = presenter.SelectRouteForTests(1);
            Assert.AreEqual(MemoryExpeditionActionStatus.OpenValuesNotLocked, refuse.Status);

        }

        [Test]
        public void Home_MemoryExpeditionButton_OpensShell_AndBackReturnsHome()
        {
            var go = new GameObject("HomeMemoryExpeditionReach");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            home.BuildHomePageUIForTests();
            GameObject homeCanvas = home.HomeCanvasObjectForTests;

            Button openBtn = homeCanvas.transform.Find("Btn_MemoryExpedition")?.GetComponent<Button>();
            Assert.NotNull(openBtn);
            openBtn.onClick.Invoke();
            Assert.IsFalse(homeCanvas.activeSelf);
            Assert.NotNull(GameObject.Find(MemoryExpeditionPresenter.CanvasName));

            Button back = GameObject.Find(MemoryExpeditionPresenter.CanvasName).transform
                .Find("MemoryExpeditionHeader/Btn_Back")?.GetComponent<Button>();
            Assert.NotNull(back);
            back.onClick.Invoke();
            Assert.IsTrue(homeCanvas.activeSelf);
            Assert.IsNull(go.GetComponent<MemoryExpeditionPresenter>());
        }

    }
}

using System.IO;
using MyriadOfDragons.Metagame;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    public class BazaarShellTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsBazaarShell_" + System.Guid.NewGuid().ToString("N"));
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
            StringAssert.Contains("OPEN", BazaarOpenValues.StatusNote.ToUpperInvariant());
            Assert.AreEqual("[runtime]", BazaarOpenValues.RuntimePlaceholder);
        }

        [Test]
        public void Presenter_BuildsArtShell_AndActionsRefuse()
        {
            var go = new GameObject("BazaarHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<BazaarPresenter>();
            presenter.Initialize(onBack: null);

            GameObject canvas = presenter.CanvasObjectForTests;
            Assert.NotNull(canvas);

            Assert.IsTrue(BazaarUiLibrary.HasBazaarV1Pack);
            Assert.AreEqual(BazaarUiLibrary.CatalogShellName,
                canvas.transform.Find("Background")?.GetComponent<Image>()?.sprite?.name);
            Assert.NotNull(canvas.transform.Find("ListingGrid/ListingWell_5"));
            Assert.NotNull(canvas.transform.Find("TabStrip/Tab_Browse"));
            var refuse = presenter.ConfirmTransactionForTests();
            Assert.AreEqual(BazaarActionStatus.OpenValuesNotLocked, refuse.Status);

        }

        [Test]
        public void Home_BazaarButton_OpensShell_AndBackReturnsHome()
        {
            var go = new GameObject("HomeBazaarReach");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            home.BuildHomePageUIForTests();
            GameObject homeCanvas = home.HomeCanvasObjectForTests;

            Button openBtn = homeCanvas.transform.Find("Btn_Bazaar")?.GetComponent<Button>();
            Assert.NotNull(openBtn);
            openBtn.onClick.Invoke();
            Assert.IsFalse(homeCanvas.activeSelf);
            Assert.NotNull(GameObject.Find(BazaarPresenter.CanvasName));

            Button back = GameObject.Find(BazaarPresenter.CanvasName).transform
                .Find("BazaarHeader/Btn_Back")?.GetComponent<Button>();
            Assert.NotNull(back);
            back.onClick.Invoke();
            Assert.IsTrue(homeCanvas.activeSelf);
            Assert.IsNull(go.GetComponent<BazaarPresenter>());
        }

    }
}

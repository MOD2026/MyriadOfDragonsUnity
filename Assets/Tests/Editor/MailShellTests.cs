using System.IO;
using MyriadOfDragons.Metagame;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    public class MailShellTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsMailShell_" + System.Guid.NewGuid().ToString("N"));
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
            StringAssert.Contains("OPEN", MailInboxOpenValues.StatusNote.ToUpperInvariant());
            Assert.AreEqual("[runtime]", MailInboxOpenValues.RuntimePlaceholder);
        }

        [Test]
        public void Presenter_BuildsArtShell_AndActionsRefuse()
        {
            var go = new GameObject("MailHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<MailInboxPresenter>();
            presenter.Initialize(onBack: null);

            GameObject canvas = presenter.CanvasObjectForTests;
            Assert.NotNull(canvas);

            Assert.IsTrue(MailInboxUiLibrary.HasMailInboxV1Pack);
            Assert.AreEqual(MailInboxUiLibrary.InboxShellName,
                canvas.transform.Find("Background")?.GetComponent<Image>()?.sprite?.name);
            Assert.NotNull(canvas.transform.Find("MessageList/MailRow_5"));
            StringAssert.Contains("Empty inbox",
                canvas.transform.Find("MessageList/MailRow_0/Subject")?.GetComponent<Text>()?.text);
            var refuse = presenter.ClaimAttachmentForTests();
            Assert.AreEqual(MailInboxActionStatus.OpenValuesNotLocked, refuse.Status);

        }

        [Test]
        public void Home_MailButton_OpensShell_AndBackReturnsHome()
        {
            var go = new GameObject("HomeMailReach");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            home.BuildHomePageUIForTests();
            GameObject homeCanvas = home.HomeCanvasObjectForTests;

            Button openBtn = homeCanvas.transform.Find("Btn_Mail")?.GetComponent<Button>();
            Assert.NotNull(openBtn);
            openBtn.onClick.Invoke();
            Assert.IsFalse(homeCanvas.activeSelf);
            Assert.NotNull(GameObject.Find(MailInboxPresenter.CanvasName));

            Button back = GameObject.Find(MailInboxPresenter.CanvasName).transform
                .Find("MailInboxHeader/Btn_Back")?.GetComponent<Button>();
            Assert.NotNull(back);
            back.onClick.Invoke();
            Assert.IsTrue(homeCanvas.activeSelf);
            Assert.IsNull(go.GetComponent<MailInboxPresenter>());
        }

    }
}

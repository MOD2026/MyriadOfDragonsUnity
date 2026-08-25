using System.IO;
using MyriadOfDragons.Metagame;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    public class ChatShellTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsChatShell_" + System.Guid.NewGuid().ToString("N"));
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
            StringAssert.Contains("OPEN", ChatSocialOpenValues.StatusNote.ToUpperInvariant());
            Assert.AreEqual("[runtime]", ChatSocialOpenValues.RuntimePlaceholder);
        }

        [Test]
        public void Presenter_BuildsArtShell_AndActionsRefuse()
        {
            var go = new GameObject("ChatHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<ChatSocialPresenter>();
            presenter.Initialize(onBack: null);

            GameObject canvas = presenter.CanvasObjectForTests;
            Assert.NotNull(canvas);

            Assert.IsTrue(ChatSocialUiLibrary.HasChatSocialV1Pack);
            Assert.AreEqual(ChatSocialUiLibrary.ShellName,
                canvas.transform.Find("Background")?.GetComponent<Image>()?.sprite?.name);
            Assert.NotNull(canvas.transform.Find("ChannelRail/Channel_Guild"));
            Assert.AreEqual(7, ChatSocialOpenValues.ChannelLabels.Length);
            StringAssert.Contains("You:",
                canvas.transform.Find("MessageStream/Placeholder")?.GetComponent<Text>()?.text);
            var refuse = presenter.SendMessageForTests();
            Assert.AreEqual(ChatSocialActionStatus.OpenValuesNotLocked, refuse.Status);

        }

        [Test]
        public void Home_ChatButton_OpensShell_AndBackReturnsHome()
        {
            var go = new GameObject("HomeChatReach");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            home.BuildHomePageUIForTests();
            GameObject homeCanvas = home.HomeCanvasObjectForTests;

            Button openBtn = homeCanvas.transform.Find("TopHud/Btn_Chat")?.GetComponent<Button>();
            Assert.NotNull(openBtn);
            openBtn.onClick.Invoke();
            Assert.IsFalse(homeCanvas.activeSelf);
            Assert.NotNull(GameObject.Find(ChatSocialPresenter.CanvasName));

            Button back = GameObject.Find(ChatSocialPresenter.CanvasName).transform
                .Find("ChatSocialHeader/Btn_Back")?.GetComponent<Button>();
            Assert.NotNull(back);
            back.onClick.Invoke();
            Assert.IsTrue(homeCanvas.activeSelf);
            Assert.IsNull(go.GetComponent<ChatSocialPresenter>());
        }

    }
}

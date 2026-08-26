using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
            // Real regression found in tonight's full-suite baseline: with no gateway injected,
            // Initialize defaults to UnityCloudCodeChatSocialGateway (bb74809, "Beta social
            // screens Option B... live-deployed") - a REAL Unity Gaming Services client
            // (auth + CloudCode module calls) that cannot reach a real backend inside an EditMode
            // sandbox. It throws, and the presenter's own correct error path sets the stream to
            // "Load failed." instead of the success-path text this test checks for. A fake
            // gateway (same pattern as Bazaar/GuildExpedition/PermitWeekKey's own shell tests)
            // exercises the real success path deterministically instead of always losing to a
            // real network call it can never win in this environment.
            var go = new GameObject("ChatHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<ChatSocialPresenter>();
            presenter.Initialize(onBack: null, gateway: new FakeChatSocialGateway());

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
            // Real reachability path since the Home IA rebuild - Chat/Mail/Friends collapse into
            // ONE global Social drawer (register: "ChatSocial / MailInbox / Friends -> ONE
            // global Social drawer... three tabs inside it"), not separate Home buttons. Chat is
            // the drawer's default tab.
            var go = new GameObject("HomeChatReach");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            home.BuildHomePageUIForTests();
            GameObject homeCanvas = home.HomeCanvasObjectForTests;

            home.OpenSocialDrawerForTests();
            Assert.NotNull(home.SocialDrawerObjectForTests, "Setup: expected the Social drawer to open.");
            Assert.IsFalse(homeCanvas.activeSelf);
            Assert.NotNull(GameObject.Find(ChatSocialPresenter.CanvasName));

            Button back = GameObject.Find(ChatSocialPresenter.CanvasName).transform
                .Find("ChatSocialHeader/Btn_Back")?.GetComponent<Button>();
            Assert.NotNull(back);
            back.onClick.Invoke();
            Assert.IsTrue(homeCanvas.activeSelf);
            Assert.IsNull(go.GetComponent<ChatSocialPresenter>());
            Assert.IsNull(home.SocialDrawerObjectForTests,
                "The sub-presenter's own Back button must close the whole drawer, not strand it empty.");
        }

        /// <summary>Same pattern as BazaarShellTests' own FakeBazaarGateway - a benign, real
        /// success result returned synchronously (Task.FromResult), so tests exercise the
        /// presenter's success-path logic deterministically instead of the real network-backed
        /// UnityCloudCodeChatSocialGateway, which cannot succeed inside an EditMode sandbox.</summary>
        private sealed class FakeChatSocialGateway : IChatSocialGateway
        {
            public Task<PostChatMessageGatewayResult> PostMessageAsync(string channelId, string text, CancellationToken cancellationToken) =>
                Task.FromResult(new PostChatMessageGatewayResult { success = true, messageId = "M1" });

            public Task<FetchChatHistoryGatewayResult> FetchChannelHistoryAsync(string channelId, int limit, CancellationToken cancellationToken) =>
                Task.FromResult(new FetchChatHistoryGatewayResult { success = true, messages = new List<ChatMessageDto>() });
        }
    }
}

using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// <summary>Mail/Friends/Chat Back-callback lifecycle. In play mode TeardownUI uses a deferred Destroy, so a Back
    /// button stays tappable until end of frame: a double tap ran the host's onBack twice, and a button left over from
    /// before a reopen could close the NEW screen. EditMode reproduces both by re-invoking the Button's onClick after
    /// the canvas it belonged to was torn down (the C# Button/UnityEvent object outlives DestroyImmediate).</summary>
    public class SocialBackLifecycleTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsSocialBack_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
            foreach (GameObject go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (Directory.Exists(_scratchSaveDir)) Directory.Delete(_scratchSaveDir, true);
        }

        private static Button BackButton(GameObject canvas) =>
            canvas.GetComponentsInChildren<Button>(true).Single(b => b.name == "Btn_Back");

        private sealed class Screen
        {
            public System.Func<int, GameObject> Open;   // (re)opens; returns the canvas
            public int BackCalls;
        }

        private Screen Mail()
        {
            var s = new Screen();
            var p = Host("MailBackHarness").AddComponent<MailInboxPresenter>();
            s.Open = _ => { p.Initialize(() => s.BackCalls++); return p.CanvasObjectForTests; };
            return s;
        }

        private Screen Friends()
        {
            var s = new Screen();
            var p = Host("FriendsBackHarness").AddComponent<FriendsPresenter>();
            s.Open = _ => { p.Initialize(() => s.BackCalls++); return p.CanvasObjectForTests; };
            return s;
        }

        private Screen Chat()
        {
            var s = new Screen();
            var p = Host("ChatBackHarness").AddComponent<ChatSocialPresenter>();
            s.Open = _ => { p.Initialize(() => s.BackCalls++, new QuietChatGateway()); return p.CanvasObjectForTests; };
            return s;
        }

        private GameObject Host(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        private static IEnumerable<TestCaseData> Screens()
        {
            yield return new TestCaseData("Mail").SetName("Mail");
            yield return new TestCaseData("Friends").SetName("Friends");
            yield return new TestCaseData("Chat").SetName("Chat");
        }

        private Screen Make(string which) => which == "Mail" ? Mail() : which == "Friends" ? Friends() : Chat();

        [TestCaseSource(nameof(Screens))]
        public void Back_TappedTwice_RunsTheHostCallbackOnce(string which)
        {
            Screen s = Make(which);
            Button back = BackButton(s.Open(0));

            back.onClick.Invoke();
            back.onClick.Invoke(); // same-frame double tap: the deferred-Destroy button is still live

            Assert.AreEqual(1, s.BackCalls, "Back must be one-shot per opened screen.");
        }

        [TestCaseSource(nameof(Screens))]
        public void Back_StaleButtonFromBeforeReopen_DoesNotCloseTheNewScreen(string which)
        {
            Screen s = Make(which);
            Button staleBack = BackButton(s.Open(0));
            GameObject reopened = s.Open(1); // reopen; the first canvas is torn down

            staleBack.onClick.Invoke();

            Assert.AreEqual(0, s.BackCalls, "A button from a torn-down canvas must not fire Back.");
            Assert.IsTrue(reopened != null, "The reopened screen must survive a stale Back tap.");

            BackButton(reopened).onClick.Invoke();
            Assert.AreEqual(1, s.BackCalls, "The live Back button still works after a reopen.");
        }

        [TestCaseSource(nameof(Screens))]
        public void Reopen_LeavesExactlyOneCanvasAndOneBackButton(string which)
        {
            Screen s = Make(which);
            s.Open(0);
            GameObject reopened = s.Open(1);

            string canvasName = reopened.name;
            Assert.AreEqual(1, Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).Count(c => c.name == canvasName),
                "Reopen must not leave a duplicate canvas (and its raycast blockers) behind.");
            Assert.AreEqual(1, reopened.GetComponentsInChildren<Button>(true).Count(b => b.name == "Btn_Back"));
        }

        [TestCaseSource(nameof(Screens))]
        public void Back_OnALiveScreen_TearsDownAndCallsBackOnce(string which)
        {
            Screen s = Make(which);
            GameObject canvas = s.Open(0);

            BackButton(canvas).onClick.Invoke();

            Assert.AreEqual(1, s.BackCalls);
            Assert.IsTrue(canvas == null, "Back must still tear the canvas down (existing behavior).");
        }

        private sealed class QuietChatGateway : IChatSocialGateway
        {
            public Task<PostChatMessageGatewayResult> PostMessageAsync(string channelId, string text, CancellationToken cancellationToken) =>
                Task.FromResult(new PostChatMessageGatewayResult { success = true, messageId = "M1" });

            public Task<FetchChatHistoryGatewayResult> FetchChannelHistoryAsync(string channelId, int limit, CancellationToken cancellationToken) =>
                Task.FromResult(new FetchChatHistoryGatewayResult { success = true, messages = new List<ChatMessageDto>() });
        }
    }
}

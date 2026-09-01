using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MyriadOfDragons.Metagame;
using System.Linq;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// ChatSocial screen layout/geometry coverage - one of 16 screens with zero prior layout
    /// regression coverage. Same method as EmpireBuildingDetailLayoutTests/TacticalPuzzleLayoutTests:
    /// measure the BUILT hierarchy's real world rects, only flag art that draws AFTER a button it
    /// geometrically overlaps (real depth-first paint/raycast order).
    /// </summary>
    public class ChatSocialLayoutTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDChatSocialLayout_" + System.Guid.NewGuid().ToString("N"));
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
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir)) Directory.Delete(_scratchSaveDir, true);
        }

        private ChatSocialPresenter Open()
        {
            var go = new GameObject("ChatSocialLayoutHost");
            _spawned.Add(go);
            var presenter = go.AddComponent<ChatSocialPresenter>();
            presenter.Initialize(onBack: null);
            foreach (RectTransform rt in presenter.CanvasObjectForTests.GetComponentsInChildren<RectTransform>(true)
                         .OrderByDescending(r => r.GetComponentsInParent<Transform>(true).Length))
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            return presenter;
        }

        private static Rect WorldRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            float xMin = c.Min(v => v.x), xMax = c.Max(v => v.x);
            float yMin = c.Min(v => v.y), yMax = c.Max(v => v.y);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        [Test]
        public void ChatSocial_ActuallyBuildsItsCanvas()
        {
            ChatSocialPresenter presenter = Open();
            Assert.IsNotNull(presenter.CanvasObjectForTests, "ChatSocial built no canvas.");
            Assert.Greater(presenter.CanvasObjectForTests.GetComponentsInChildren<RectTransform>(true).Length, 3,
                "ChatSocial built a suspiciously empty canvas.");
        }

        [Test]
        public void ChatSocial_NeverDrawsArtOnTopOfAnInteractiveControl()
        {
            ChatSocialPresenter presenter = Open();
            Transform canvas = presenter.CanvasObjectForTests.transform;

            Transform[] drawOrder = canvas.GetComponentsInChildren<Transform>(true);
            var indexOf = new Dictionary<Transform, int>();
            for (int i = 0; i < drawOrder.Length; i++) indexOf[drawOrder[i]] = i;

            var collisions = new List<string>();
            foreach (Button button in canvas.GetComponentsInChildren<Button>(true))
            {
                if (!button.gameObject.activeInHierarchy) continue;
                Rect btn = WorldRect(button.GetComponent<RectTransform>());
                int buttonIndex = indexOf[button.transform];

                foreach (Image img in canvas.GetComponentsInChildren<Image>(true))
                {
                    if (img.sprite == null || !img.gameObject.activeInHierarchy) continue;
                    if (img.GetComponent<Button>() != null) continue;
                    if (img.transform.IsChildOf(button.transform)) continue;
                    if (indexOf[img.transform] <= buttonIndex) continue;

                    Rect art = WorldRect(img.rectTransform);
                    if (art.width <= 0f || art.height <= 0f) continue;
                    if (art.Overlaps(btn))
                        collisions.Add($"'{img.name}' {art} overlaps '{button.name}' {btn} and draws AFTER it");
                }
            }

            CollectionAssert.IsEmpty(collisions,
                "Art draws on top of an interactive control, so a tap would land on art instead of the button: " +
                string.Join("  |  ", collisions));
        }

        // ------------------------------------------------------------------ Revamp V2 gates

        private sealed class FakeGateway : IChatSocialGateway
        {
            public readonly List<ChatMessageDto> Messages = new List<ChatMessageDto>();
            public string PostedMessageId = "M1";

            public Task<PostChatMessageGatewayResult> PostMessageAsync(string channelId, string text, CancellationToken ct) =>
                Task.FromResult(new PostChatMessageGatewayResult { success = true, messageId = PostedMessageId });

            public Task<FetchChatHistoryGatewayResult> FetchChannelHistoryAsync(string channelId, int limit, CancellationToken ct) =>
                Task.FromResult(new FetchChatHistoryGatewayResult { success = true, messages = new List<ChatMessageDto>(Messages) });
        }

        private ChatSocialPresenter Open(IChatSocialGateway gateway)
        {
            var go = new GameObject("ChatSocialLayoutHost");
            _spawned.Add(go);
            var presenter = go.AddComponent<ChatSocialPresenter>();
            presenter.Initialize(onBack: null, gateway: gateway);
            return presenter;
        }

        private static int IndexOfChannel(string label) =>
            System.Array.IndexOf(ChatSocialOpenValues.ChannelLabels, label);

        /// <summary>The V1 defect this gate exists for: a full-screen raycasting Image swallows
        /// every tap on the screen underneath it. Chat builds none of its own, and the only
        /// raycast surface large enough to matter is the thread column a scroll drag needs.</summary>
        [Test]
        public void ChatSocial_BuildsNoFullScreenRaycastBlocker()
        {
            ChatSocialPresenter presenter = Open(new FakeGateway());
            var canvasRect = presenter.CanvasObjectForTests.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1920f, 1080f);
            foreach (RectTransform rt in presenter.CanvasObjectForTests.GetComponentsInChildren<RectTransform>(true)
                         .OrderByDescending(r => r.GetComponentsInParent<Transform>(true).Length))
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            Rect canvas = WorldRect(canvasRect);
            float canvasArea = canvas.width * canvas.height;
            var blockers = new List<string>();
            foreach (Graphic g in presenter.CanvasObjectForTests.GetComponentsInChildren<Graphic>(true))
            {
                if (!g.raycastTarget || !g.gameObject.activeInHierarchy) continue;
                if (g.GetComponentInParent<Selectable>() != null) continue;
                if (g.GetComponentInParent<ScrollRect>() != null) continue;
                Rect r = WorldRect(g.rectTransform);
                if (canvasArea > 0f && r.width * r.height > canvasArea * 0.5f)
                    blockers.Add("'" + g.name + "' " + r);
            }

            CollectionAssert.IsEmpty(blockers,
                "A non-interactive graphic covers more than half the screen while still accepting " +
                "raycasts, so taps land on it instead of the controls beneath: " + string.Join("  |  ", blockers));
        }

        /// <summary>Reopening the screen must never leave two ChatSocialCanvas objects stacked -
        /// two live canvases double every raycast and strand a teardown.</summary>
        [Test]
        public void ChatSocial_ReopenLeavesExactlyOneCanvas()
        {
            Open(new FakeGateway());
            Open(new FakeGateway());
            int canvases = Object.FindObjectsOfType<Canvas>()
                .Count(c => c.gameObject.name == ChatSocialPresenter.CanvasName);
            Assert.AreEqual(1, canvases, "Reopening Chat stacked duplicate canvases.");
        }

        /// <summary>Drawer open/close teardown: closing must destroy the canvas outright, and an
        /// in-flight refresh resuming afterwards must drop rather than write into it.</summary>
        [Test]
        public void ChatSocial_TeardownDestroysCanvas_AndLateRefreshIsDropped()
        {
            ChatSocialPresenter presenter = Open(new FakeGateway());
            GameObject canvas = presenter.CanvasObjectForTests;
            presenter.TeardownUI();

            Assert.IsTrue(canvas == null, "TeardownUI left the Chat canvas alive.");
            Assert.IsNull(presenter.CanvasObjectForTests);
            Assert.IsNull(GameObject.Find(ChatSocialPresenter.CanvasName));

            // Must not throw, and must not resurrect any UI.
            Assert.DoesNotThrow(() => presenter.RefreshHistoryForTests().GetAwaiter().GetResult());
            Assert.IsNull(presenter.CanvasObjectForTests);
        }

        /// <summary>Read-only channels get NO composer, not a disabled one, and DM renders the
        /// unavailable state instead of a blank panel.</summary>
        [Test]
        public void ChatSocial_ReadOnlyAndUnavailableChannelsPresentTruthfully()
        {
            ChatSocialPresenter presenter = Open(new FakeGateway());

            presenter.SelectChannelForTests(IndexOfChannel("Broadcast"));
            Assert.IsFalse(presenter.ComposerVisibleForTests,
                "Broadcast is read-only and must have no composer at all.");

            presenter.SelectChannelForTests(IndexOfChannel("DM"));
            Assert.IsFalse(presenter.ComposerVisibleForTests);
            StringAssert.Contains("Direct Messages Are Not Available Yet",
                presenter.StreamStateTitleForTests ?? string.Empty);
            Assert.AreEqual(0, presenter.MessageRowCountForTests,
                "An unavailable channel must not render a thread.");

            presenter.SelectChannelForTests(IndexOfChannel("Guild"));
            Assert.IsTrue(presenter.ComposerVisibleForTests,
                "A writable channel must get its composer back after leaving a locked one.");
        }

        /// <summary>Thread hierarchy + alias-only identity: one row per message, and no raw
        /// backend account id anywhere in the rendered text.</summary>
        [Test]
        public void ChatSocial_RendersOneRowPerMessage_WithAliasOnlyIdentity()
        {
            var gateway = new FakeGateway();
            const string RawAccountId = "acct-4d3f9c11-secret";
            gateway.Messages.Add(new ChatMessageDto { id = "B", senderAccountId = RawAccountId, text = "Second", sentUtcMs = 1700000060000L });
            gateway.Messages.Add(new ChatMessageDto { id = "A", senderAccountId = RawAccountId, text = "First", sentUtcMs = 1700000000000L });

            ChatSocialPresenter presenter = Open(gateway);
            Assert.AreEqual(2, presenter.MessageRowCountForTests, "Expected one row per message.");
            Assert.IsNull(presenter.StreamStateTitleForTests,
                "A populated thread must not also show an empty/error state.");

            var rendered = presenter.CanvasObjectForTests.GetComponentsInChildren<Text>(true)
                .Select(t => t.text ?? string.Empty).ToList();
            CollectionAssert.IsEmpty(rendered.Where(t => t.Contains(RawAccountId)).ToList(),
                "A raw backend account id reached the screen - alias-only identity is a capture-gate rule.");
            Assert.IsTrue(rendered.Any(t => t.StartsWith("Rider ")),
                "Other players must render as a neutral generated alias marker.");

            // Oldest first: a thread reads top-to-bottom, newest at the bottom.
            Transform content = presenter.CanvasObjectForTests.transform.Find("MessageStream/Viewport/Content");
            Assert.NotNull(content);
            Assert.AreEqual("First", content.GetChild(0).Find("Bubble/Body").GetComponent<Text>().text);
            Assert.AreEqual("Second", content.GetChild(1).Find("Bubble/Body").GetComponent<Text>().text);
        }

        /// <summary>A message this session posted reads "You", never a generated stranger alias.</summary>
        [Test]
        public void ChatSocial_OwnPostedMessageRendersAsYou()
        {
            var gateway = new FakeGateway { PostedMessageId = "MINE" };
            ChatSocialPresenter presenter = Open(gateway);
            gateway.Messages.Add(new ChatMessageDto { id = "MINE", senderAccountId = "acct-me", text = "Hello", sentUtcMs = 1700000000000L });

            presenter.ComposedText = "Hello";
            presenter.SendComposedForTests().GetAwaiter().GetResult();

            Transform content = presenter.CanvasObjectForTests.transform.Find("MessageStream/Viewport/Content");
            Assert.NotNull(content);
            StringAssert.StartsWith("You", content.GetChild(0).Find("Bubble/Meta").GetComponent<Text>().text);
        }
    }
}

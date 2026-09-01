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
            public string LastPostedText;

            public Task<PostChatMessageGatewayResult> PostMessageAsync(string channelId, string text, CancellationToken ct)
            {
                LastPostedText = text;
                return Task.FromResult(new PostChatMessageGatewayResult { success = true, messageId = PostedMessageId });
            }

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
            StringAssert.Contains("Direct messages aren't available yet.",
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

        /// <summary>Stands in for Home's Social drawer WITHOUT touching HomePagePresenter: the
        /// only thing Chat reads is "is there a canvas named SocialDrawer", and the chrome bands
        /// asserted below are the exact rects Home builds (tab bar 0,0-1920,100; CLOSE
        /// 1780,970-1896,1080).</summary>
        private GameObject SpawnHostDrawerStub()
        {
            var drawer = new GameObject("SocialDrawer", typeof(RectTransform), typeof(Canvas));
            _spawned.Add(drawer);
            Canvas c = drawer.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 20;
            return drawer;
        }

        /// <summary>Maps a 1920x1080 top-left design rect onto the built canvas's world rect.</summary>
        private static Rect DesignRectToWorld(Rect canvas, float left, float top, float width, float height)
        {
            float sx = canvas.width / 1920f, sy = canvas.height / 1080f;
            return new Rect(canvas.xMin + left * sx,
                            canvas.yMax - (top + height) * sy,
                            width * sx, height * sy);
        }

        /// <summary>Hosted inside the drawer, Chat must sort above the drawer's full-screen
        /// dimmer (or nothing on this screen is tappable) AND must not paint over the drawer's own
        /// tab bar or CLOSE target (or those controls read as dead). Both at once is the whole
        /// constraint - clearing only one of them trades one blocked control for another.</summary>
        [Test]
        public void ChatSocial_HostedInDrawer_SortsAboveDimmer_AndClearsDrawerChrome()
        {
            SpawnHostDrawerStub();
            ChatSocialPresenter presenter = Open(new FakeGateway());

            Assert.IsTrue(presenter.HostedInSocialDrawerForTests,
                "Chat did not detect the host drawer, so it cannot adapt to its chrome.");
            Canvas chatCanvas = presenter.CanvasObjectForTests.GetComponent<Canvas>();
            Assert.Greater(chatCanvas.sortingOrder, 20,
                "Chat sorts under the drawer's full-screen dimmer, so no control on it is tappable.");

            var canvasRect = presenter.CanvasObjectForTests.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1920f, 1080f);
            foreach (RectTransform rt in presenter.CanvasObjectForTests.GetComponentsInChildren<RectTransform>(true)
                         .OrderByDescending(r => r.GetComponentsInParent<Transform>(true).Length))
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            Rect canvas = WorldRect(canvasRect);
            Rect tabBar = DesignRectToWorld(canvas, 0f, 0f, 1920f, 100f);
            Rect closeBtn = DesignRectToWorld(canvas, 1780f, 970f, 116f, 110f);

            var covered = new List<string>();
            foreach (Graphic g in presenter.CanvasObjectForTests.GetComponentsInChildren<Graphic>(true))
            {
                if (!g.gameObject.activeInHierarchy) continue;
                Rect r = WorldRect(g.rectTransform);
                if (r.width <= 0f || r.height <= 0f) continue;
                if (r.Overlaps(tabBar)) covered.Add($"'{g.name}' covers the drawer TAB BAR {r}");
                if (r.Overlaps(closeBtn)) covered.Add($"'{g.name}' covers the drawer CLOSE target {r}");
            }

            CollectionAssert.IsEmpty(covered,
                "Chat paints over the host drawer's own controls while sorting above them, so the " +
                "tab bar / CLOSE read as dead: " + string.Join("  |  ", covered));
        }

        /// <summary>One shell, not two. The opaque backing and the preserveAspect art are a
        /// deliberate pair; a SECOND copy of the shell sprite means a duplicated build.</summary>
        [Test]
        public void ChatSocial_DrawsExactlyOneShellSprite()
        {
            ChatSocialPresenter presenter = Open(new FakeGateway());
            int shells = presenter.CanvasObjectForTests.GetComponentsInChildren<Image>(true)
                .Count(i => i.sprite != null && i.sprite.name == ChatSocialUiLibrary.ShellName);
            Assert.AreEqual(1, shells, "Chat drew the fullscreen shell sprite more than once.");
        }

        /// <summary>REPLACES ChatSocial_RailRemainderCarriesContent_NotDeadSpace, which asserted
        /// the OPPOSITE and would have locked an unsupported claim in place. That gate demanded the
        /// rail's leftover space carry copy, and the copy it was satisfied by read "Unread counts
        /// are not connected to this screen yet" - an unread/capability claim, RENDER-NOTHING under
        /// CC6-CHAT-RENDER-NOTHING-CLOSURE-009. The truthful answer is an empty remainder, and an
        /// empty FRAME there would be the filler UIEmptyState's locked ranking rejects.</summary>
        [Test]
        public void ChatSocial_RailRemainderCarriesNoUnsupportedClaim()
        {
            ChatSocialPresenter presenter = Open(new FakeGateway());
            Transform rail = presenter.CanvasObjectForTests.transform.Find("ChannelRail");
            Assert.NotNull(rail);

            Assert.IsNull(rail.Find("RailNote"),
                "The rail note is back. Its only copy was an unread/capability claim, which is " +
                "RENDER-NOTHING until an ACL contract exists.");

            // Every remaining rail child must be a real channel row, not a decorative filler panel.
            string[] channels = ChatSocialOpenValues.ChannelLabels;
            Assert.AreEqual(channels.Length, rail.childCount,
                "The rail carries something other than its channel rows.");
            for (int i = 0; i < rail.childCount; i++)
                Assert.NotNull(rail.GetChild(i).GetComponent<Button>(),
                    $"Rail child '{rail.GetChild(i).name}' is not a channel row - decorative filler " +
                    "is exactly what the collapse-beats-filler ranking rejects.");
        }

        /// <summary>The three body columns are laid out from hand-computed pixel constants
        /// (SafeMargin/RailWidth/Gutter/StreamWidth/ContextWidth), so a single edited constant
        /// silently pushes one column into its neighbour or past the right safe margin. Nothing
        /// measured the columns against each other - the paint-order gate only catches ART over a
        /// CONTROL, and none of these three panels is a control.</summary>
        [Test]
        public void ChatSocial_RailStreamAndContextColumnsDoNotOverlap_AndStayInsideSafeMargins()
        {
            ChatSocialPresenter presenter = Open(new FakeGateway());
            var canvasRect = presenter.CanvasObjectForTests.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1920f, 1080f);
            foreach (RectTransform rt in presenter.CanvasObjectForTests.GetComponentsInChildren<RectTransform>(true)
                         .OrderByDescending(r => r.GetComponentsInParent<Transform>(true).Length))
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            Transform root = presenter.CanvasObjectForTests.transform;
            Rect rail = WorldRect(root.Find("ChannelRail").GetComponent<RectTransform>());
            Rect stream = WorldRect(root.Find("MessageStream").GetComponent<RectTransform>());
            Rect bar = WorldRect(root.Find("ChannelBar").GetComponent<RectTransform>());
            Rect composer = WorldRect(root.Find("Composer").GetComponent<RectTransform>());

            // The context column is COLLAPSED while chat.channel.capability is RENDER-NOTHING, and
            // an empty framed panel left in its place is the filler the locked ranking rejects.
            Assert.IsNull(root.Find("ContextPanel"),
                "The context column is back, but its only copy (chat.channel.capability) is still " +
                "RENDER-NOTHING - so it can only be an empty frame.");

            Assert.LessOrEqual(rail.xMax, stream.xMin + 0.5f, $"Channel rail overlaps the stream: rail={rail} stream={stream}");
            Assert.LessOrEqual(rail.xMax, bar.xMin + 0.5f, $"Channel rail overlaps the channel bar: rail={rail} bar={bar}");
            Assert.LessOrEqual(rail.xMax, composer.xMin + 0.5f, $"Channel rail overlaps the composer: rail={rail} composer={composer}");

            // Vertical stacking inside the stream column: bar, then thread, then composer.
            Assert.GreaterOrEqual(bar.yMin, stream.yMax - 0.5f, $"Channel bar overlaps the thread: bar={bar} stream={stream}");
            Assert.GreaterOrEqual(stream.yMin, composer.yMax - 0.5f, $"Thread overlaps the composer: stream={stream} composer={composer}");

            // 38px safe margins, both sides.
            Rect canvas = WorldRect(canvasRect);
            float safe = 38f * (canvas.width / 1920f);
            Assert.GreaterOrEqual(rail.xMin, canvas.xMin + safe - 0.5f, $"Channel rail breaks the left safe margin: rail={rail}");
            foreach (var (name, r2) in new[] { ("stream", stream), ("channel bar", bar), ("composer", composer) })
                Assert.LessOrEqual(r2.xMax, canvas.xMax - safe + 0.5f,
                    $"The {name} breaks the right safe margin after the context column was reclaimed: {r2}");
        }

        private sealed class FailingGateway : IChatSocialGateway
        {
            public Task<PostChatMessageGatewayResult> PostMessageAsync(string channelId, string text, CancellationToken ct) =>
                Task.FromResult(new PostChatMessageGatewayResult { success = false, errorCode = "UNAVAILABLE" });

            public Task<FetchChatHistoryGatewayResult> FetchChannelHistoryAsync(string channelId, int limit, CancellationToken ct) =>
                Task.FromResult(new FetchChatHistoryGatewayResult { success = false, errorCode = "UNAVAILABLE" });
        }

        /// <summary>RETRY SAFELY is the only way out of the error state, and it is sized from
        /// UIEmptyState's NORMALISED proportions against whatever region it lands in - so its real
        /// pixel size is a property of the Chat stream column, not of the button. The reference's
        /// primary-target floor is 64x160; a narrower stream column would silently shrink it below
        /// that. Also asserts it is genuinely tappable rather than a covered dead control.</summary>
        [Test]
        public void ChatSocial_ErrorState_RetryTargetIsRealAndMeetsTheTouchFloor()
        {
            ChatSocialPresenter presenter = Open(new FailingGateway());
            presenter.RefreshHistoryForTests().GetAwaiter().GetResult();

            var canvasRect = presenter.CanvasObjectForTests.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1920f, 1080f);
            foreach (RectTransform rt in presenter.CanvasObjectForTests.GetComponentsInChildren<RectTransform>(true)
                         .OrderByDescending(r => r.GetComponentsInParent<Transform>(true).Length))
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            Assert.AreEqual("Messages could not be loaded.", presenter.StreamStateTitleForTests,
                "A failed history load did not present the error state.");

            Transform action = presenter.CanvasObjectForTests.transform.Find("StreamState/EmptyState_Action");
            Assert.NotNull(action, "The error state offers no RETRY control.");
            Button retry = action.GetComponent<Button>();
            Assert.NotNull(retry);
            Assert.IsTrue(retry.interactable, "RETRY is present but not interactable, which is a dead end wearing a control's clothes.");

            Rect canvas = WorldRect(canvasRect);
            Rect r = WorldRect(action.GetComponent<RectTransform>());
            float sx = canvas.width / 1920f, sy = canvas.height / 1080f;
            Assert.GreaterOrEqual(r.width, 160f * sx - 0.5f, $"RETRY is narrower than the 160px primary-target floor: {r}");
            Assert.GreaterOrEqual(r.height, 64f * sy - 0.5f, $"RETRY is shorter than the 64px primary-target floor: {r}");

            // The retry target must not sit under the composer or outside the stream column.
            Rect stream = WorldRect(presenter.CanvasObjectForTests.transform.Find("StreamState").GetComponent<RectTransform>());
            Assert.IsTrue(stream.Contains(new Vector2(r.xMin, r.yMin)) && stream.Contains(new Vector2(r.xMax, r.yMax)),
                $"RETRY escapes the stream state region: retry={r} region={stream}");
        }

        /// <summary>The composer cap and any sentence describing it are a BACKEND capability claim,
        /// and ChatSocialOpenValues is the channel/config authority for it. A hardcoded 500 shipped
        /// in both places while MaxMessageLength was null - a limit silently enforced on the player
        /// and stated to them as fact, neither of which the authority supports. This gate fails if
        /// either one is reintroduced independently of OpenValues, and it keeps passing unchanged
        /// the day BE publishes a real value.</summary>
        [Test]
        public void ChatSocial_MessageLengthCapAndItsCopyComeOnlyFromTheConfigAuthority()
        {
            ChatSocialPresenter presenter = Open(new FakeGateway());

            int expected = ChatSocialOpenValues.MaxMessageLength ?? 0;
            Assert.NotNull(presenter.ComposerInputForTests, "Chat built no composer on an open channel.");
            Assert.AreEqual(expected, presenter.ComposerInputForTests.characterLimit,
                "The composer's character limit does not come from ChatSocialOpenValues.MaxMessageLength. " +
                "A presenter-local number is a second source of truth for a backend capability.");

            // No rendered string may state a message-length capability the authority does not make.
            var claims = presenter.CanvasObjectForTests.GetComponentsInChildren<Text>(true)
                .Select(t => t.text)
                .Where(t => !string.IsNullOrEmpty(t) && t.Contains("characters"))
                .ToList();
            CollectionAssert.IsEmpty(claims,
                "Chat states a message-length capability to the player while ChatSocialOpenValues " +
                "publishes none: " + string.Join("  |  ", claims));
        }

        /// <summary>The RENDER-NOTHING contract (CC6-CHAT-RENDER-NOTHING-CLOSURE-009), asserted as
        /// one gate because the failure mode is always the same: a suppressed row quietly acquires
        /// a "helpful" replacement sentence. Blocked rows must be ABSENT, not reworded - so this
        /// checks for the semantic claim, not for one exact old string.</summary>
        [Test]
        public void ChatSocial_BlockedCopyRowsRenderNothing_AndAreNotReplaced()
        {
            ChatSocialPresenter presenter = Open(new FakeGateway());

            // chat.input.placeholder — SOURCE-BLOCKED. Present as a control, with no hint text.
            Assert.NotNull(presenter.ComposerInputForTests);
            Text placeholder = presenter.ComposerInputForTests.placeholder as Text;
            Assert.NotNull(placeholder, "The composer lost its placeholder object entirely.");
            Assert.IsTrue(string.IsNullOrEmpty(placeholder.text),
                $"The composer shows placeholder copy '{placeholder.text}'. chat.input.placeholder is " +
                "SOURCE-BLOCKED: it implies posting permission and length support that no contract confirms.");

            // chat.channel.capability — no permission / unread / delivery / moderation sentence
            // anywhere on the screen, on any channel.
            string[] banned = { "unread", "member list", "moderation", "pinned", "delivery", "permission", "read-only." };
            var offenders = new List<string>();
            for (int i = 0; i < ChatSocialOpenValues.ChannelLabels.Length; i++)
            {
                presenter.SelectChannelForTests(i);
                foreach (Text t in presenter.CanvasObjectForTests.GetComponentsInChildren<Text>(true))
                {
                    if (string.IsNullOrEmpty(t.text)) continue;
                    string lower = t.text.ToLowerInvariant();
                    foreach (string b in banned)
                        if (lower.Contains(b) && t.name != "StatusLine")
                            offenders.Add($"[{ChatSocialOpenValues.ChannelLabels[i]}] '{t.name}' = \"{t.text}\"");
                }
            }
            CollectionAssert.IsEmpty(offenders,
                "A capability claim is rendered while chat.channel.capability is RENDER-NOTHING: " +
                string.Join("  |  ", offenders));
        }

        /// <summary>chat.dm_unavailable is the ONE approved DM string. The state must carry it and
        /// nothing else - the old body enumerated conversation lists, requests, privacy settings,
        /// unread state and delivery, all of which are capability claims.</summary>
        [Test]
        public void ChatSocial_DmUnavailableState_CarriesTheApprovedStringAndNoBody()
        {
            ChatSocialPresenter presenter = Open(new FakeGateway());
            int dm = IndexOfChannel("DM");
            Assert.GreaterOrEqual(dm, 0, "No DM channel in the approved channel list.");
            presenter.SelectChannelForTests(dm);

            Assert.AreEqual("Direct messages aren't available yet.", presenter.StreamStateTitleForTests,
                "The DM state does not render the approved chat.dm_unavailable string.");
            Assert.IsNull(presenter.CanvasObjectForTests.transform.Find("StreamState/EmptyState_Explanation"),
                "The DM state carries an explanation body again. No DM body copy is approved.");
            Assert.IsFalse(presenter.ComposerVisibleForTests,
                "The unavailable channel must have no composer in the visual or accessibility tree.");
        }

        /// <summary>chat.reconnect is RENDER-NOTHING: the error state may promise no recovery. The
        /// approved title and a real RETRY SAFELY control are the whole state.</summary>
        [Test]
        public void ChatSocial_ErrorState_MakesNoRecoveryPromise()
        {
            ChatSocialPresenter presenter = Open(new FailingGateway());
            presenter.RefreshHistoryForTests().GetAwaiter().GetResult();

            Assert.AreEqual("Messages could not be loaded.", presenter.StreamStateTitleForTests);
            Assert.IsNull(presenter.CanvasObjectForTests.transform.Find("StreamState/EmptyState_Explanation"),
                "The error state carries a body again. The former one promised the channel would be " +
                "'reachable again as soon as the connection recovers' - a recovery promise with no " +
                "recovery authority behind it.");
            Assert.NotNull(presenter.CanvasObjectForTests.transform.Find("StreamState/EmptyState_Action"),
                "RETRY SAFELY is the approved way out of the error state and must remain.");
        }

        /// <summary>contractVersion 1 binding. The number lives in ChatSocialOpenValues and nowhere
        /// else - asserted against the authority, not against the literal 500, so this keeps passing
        /// on a future contract bump and fails the moment a presenter-local number reappears.</summary>
        [Test]
        public void ChatSocial_BindsTheAuthoritativeMessageLength_AndEnforcesIt()
        {
            Assert.AreEqual(500, ChatSocialOpenValues.MaxMessageLength,
                "The bound contractVersion 1 value drifted from the published authority.");
            Assert.AreEqual(200, ChatSocialOpenValues.MaxStoredMessagesPerChannel,
                "MaxStoredMessagesPerChannel drifted from the published authority.");

            var gateway = new FakeGateway();
            ChatSocialPresenter presenter = Open(gateway);
            int limit = ChatSocialOpenValues.MaxMessageLength.Value;

            Assert.AreEqual(limit, presenter.ComposerInputForTests.characterLimit,
                "The composer does not enforce the authoritative limit from ChatSocialOpenValues.");

            // Enforcement is observable at the boundary: over-length text is cut to the limit
            // BEFORE it can reach the gateway, so nothing over-length is ever posted. Asserted on
            // what the gateway actually received, not on an internal branch.
            presenter.ComposedText = new string('x', limit + 50);
            Assert.AreEqual(limit, presenter.ComposedText.Length,
                "Over-length text was accepted into the composer, so the authoritative limit is not enforced.");
            presenter.SendComposedForTests().GetAwaiter().GetResult();
            Assert.NotNull(gateway.LastPostedText, "Nothing reached the gateway.");
            Assert.AreEqual(limit, gateway.LastPostedText.Length,
                "An over-length message reached the gateway despite the authoritative limit.");

            // Exactly at the limit is allowed - the boundary belongs to the player.
            presenter.ComposedText = new string('y', limit);
            PostChatMessageGatewayResult atLimit = presenter.SendComposedForTests().GetAwaiter().GetResult();
            Assert.IsTrue(atLimit.success, "A message exactly at the authoritative limit was refused.");
            Assert.AreEqual(limit, gateway.LastPostedText.Length);

            // chat.message.length is RENDER-NOTHING: no numeric limit may reach the player.
            var numeric = presenter.CanvasObjectForTests.GetComponentsInChildren<Text>(true)
                .Select(t => t.text)
                .Where(t => !string.IsNullOrEmpty(t) && t.Contains(limit.ToString()))
                .ToList();
            CollectionAssert.IsEmpty(numeric,
                "The authoritative limit is shown to the player, but chat.message.length is " +
                "RENDER-NOTHING until localized copy is approved: " + string.Join("  |  ", numeric));
        }

        /// <summary>chat.unavailable — APPROVED, and the ONLY string a disabled composer may show.
        /// Per AD-009 the composer and SEND stay PRESENT but DISABLED on channels where sending is
        /// not allowed, rather than being removed - so this asserts presence AND non-interactivity
        /// together. Either half alone passes for the wrong reason.</summary>
        [Test]
        public void ChatSocial_LockedChannels_ShowOnlyTheApprovedChatUnavailableLabel()
        {
            ChatSocialPresenter presenter = Open(new FakeGateway());

            foreach (string label in new[] { "Broadcast", "System", "DM" })
            {
                int idx = IndexOfChannel(label);
                Assert.GreaterOrEqual(idx, 0, $"'{label}' is not in the approved channel list.");
                presenter.SelectChannelForTests(idx);

                Assert.IsFalse(presenter.ComposerVisibleForTests,
                    $"'{label}' offers a writable composer.");
                Assert.AreEqual("Chat unavailable", presenter.ComposerUnavailableLabelForTests,
                    $"'{label}' does not show the approved chat.unavailable label.");

                // AD-009: PRESENT but DISABLED, not removed.
                Transform root = presenter.CanvasObjectForTests.transform;
                Transform input = root.Find("Composer/ComposerInput");
                Transform send = root.Find("Composer/Btn_ComposerSend");
                Assert.IsTrue(input.gameObject.activeInHierarchy,
                    $"'{label}' removed the composer input; AD-009 keeps it present but disabled.");
                Assert.IsTrue(send.gameObject.activeInHierarchy,
                    $"'{label}' removed SEND; AD-009 keeps it present but disabled.");
                Assert.IsFalse(input.GetComponent<InputField>().interactable,
                    $"'{label}' leaves the composer input interactable.");
                Assert.IsTrue(input.GetComponent<InputField>().readOnly,
                    $"'{label}' leaves the composer input writable, so a soft keyboard can still open on it.");
                Assert.IsFalse(send.GetComponent<Button>().interactable,
                    $"'{label}' leaves SEND interactable while sending is not allowed.");
            }

            // A writable channel gets the composer back and NO unavailable label.
            presenter.SelectChannelForTests(IndexOfChannel("Guild"));
            Assert.IsTrue(presenter.ComposerVisibleForTests, "A writable channel lost its composer.");
            Assert.IsNull(presenter.ComposerUnavailableLabelForTests,
                "A writable channel still shows the unavailable label.");
        }
    }
}

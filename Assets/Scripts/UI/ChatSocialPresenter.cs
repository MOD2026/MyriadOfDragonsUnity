using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyriadOfDragons.Metagame;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// CHAT — Revamp V2 presentation, built to the approved Social reference
    /// (<c>tools/seat_reports/UI-CHAT-SOCIAL-MOBILE-REVAMP.md</c>), wired to the live
    /// <see cref="IChatSocialGateway"/> Chat CloudCode module.
    ///
    /// What V2 changes, and why each one was a real defect rather than a polish item:
    ///
    ///   * THREAD HIERARCHY. V1 rendered the whole channel as one <see cref="Text"/> blob, so
    ///     every message had the same weight and nothing separated one speaker from the next.
    ///     V2 builds one real row per message inside a scrollable thread: alias + timestamp on a
    ///     quiet meta line, the message body beneath it, own messages aligned right against a
    ///     different panel tone. Bubble width is capped (72% of the stream column) so a long
    ///     message wraps into a column instead of a full-width band.
    ///   * DRAWER CHROME. Chat is a tab inside Home's global Social drawer, whose canvas sorts at
    ///     20 over a FULL-SCREEN raycasting dimmer. V1's canvas sorted at 12, so every control on
    ///     this screen sat under that dimmer and could not be tapped at all. V2 detects the host
    ///     drawer and sorts above it, then reserves the drawer's own chrome bands (the 100px
    ///     CHAT/MAIL/FRIENDS tab bar at the top, the CLOSE target bottom-right) so the two never
    ///     paint over each other. Home's drawer implementation is NOT touched — the adaptation is
    ///     entirely on this side of the boundary.
    ///   * NO FULL-SCREEN BLOCKERS OF OUR OWN. Every decorative image this screen builds is
    ///     raycastTarget=false; the only raycast surfaces are real controls and the thread
    ///     viewport that a scroll drag needs.
    ///   * ALIAS-ONLY IDENTITY. <c>senderAccountId</c> is a raw backend account id: rendering it
    ///     is an explicit capture-gate failure, and there is still no public display-name
    ///     contract. Rows show "You" for messages this session posted and a neutral generated
    ///     marker otherwise — never the id, never a fabricated player name.
    ///   * TRUTHFUL STATES. Loading / empty / error / read-only / DM-unavailable each render a
    ///     real state region with the reference's exact copy, instead of leaving a blank panel or
    ///     a bare sentence floating in an empty column. Read-only and unavailable channels have
    ///     NO composer at all rather than a disabled one.
    /// </summary>
    public class ChatSocialPresenter : MonoBehaviour
    {
        public const string CanvasName = "ChatSocialCanvas";

        /// <summary>Home's Social drawer root. Looked up by name, never edited from here — the
        /// drawer is Home's implementation and this screen only adapts around it.</summary>
        public const string HostDrawerObjectName = "SocialDrawer";

        private const int HistoryLimit = 20;

        // --- Layout, in 1920x1080 top-left pixel space (approved reference §"Layout and touch
        // geometry": 38px safe margins, 108px header, 16/63/20 columns on 19px gutters, 86px
        // composer, channel rows 84-94, Send >= 64x160).
        private const float SafeMargin = 38f;
        private const float HeaderHeight = 108f;
        private const float Gutter = 19f;
        private const float RailWidth = 295f;
        private const float StreamWidth = 1162f;
        private const float ContextWidth = 349f;
        private const float ComposerHeight = 86f;
        private const float ChannelBarHeight = 56f;
        private const float MaxChannelRowHeight = 94f;
        private const float SendWidth = 176f;
        private const float BubbleWidthFraction = 0.72f;
        private const float DesignWidth = 1920f;
        private const float DesignHeight = 1080f;

        /// <summary>Height of the host drawer's own tab bar (CHAT / MAIL / FRIENDS). Reserved, not
        /// drawn — the drawer owns those tabs and this screen must not paint over them.</summary>
        private const float HostTabBarHeight = 100f;

        /// <summary>Bottom band the host drawer's CLOSE target occupies, plus one gutter.</summary>
        private const float HostCloseReserve = 129f;

        // Player-facing copy. Locked to the approved reference's "Exact state copy" table.
        // Deliberately carries no error code, field name, account id or endpoint name: the capture
        // gate fails a screen that shows a developer diagnostic to a player. The real codes still
        // reach the console through Debug.LogWarning below.
        private const string ComposerHint = "Type a message…";
        private const string SendLabel = "SEND";
        private const string LoadingTitle = "Loading messages…";
        private const string LoadingBody = "Fetching the most recent messages for this channel.";
        private const string EmptyTitle = "No messages yet.";
        private const string ErrorTitle = "Messages could not be loaded.";
        private const string ErrorBody = "The channel is reachable again as soon as the connection recovers.";
        private const string RetryLabel = "RETRY SAFELY";
        private const string DmTitle = "Direct Messages Are Not Available Yet";
        private const string DmBody =
            "Conversation lists, requests, privacy settings, unread state, and message delivery " +
            "are not connected to this screen.";
        private const string SendFailedStatus = "Message was not sent.";
        private const string SendEmptyStatus = "Type a message first.";
        private const string HistoryUnavailableStatus = "Couldn't load messages.";
        private const string UnavailableStatus = "Direct messages aren't available yet.";
        private const string ReadOnlyStatus = "This channel is read-only.";

        private GameObject _canvasObj;
        private Action _onBack;
        private Text _statusText;
        private Text _channelBarText;
        private Text _capabilityText;
        private RectTransform _threadViewport;
        private RectTransform _threadContent;
        private GameObject _threadScrollObj;
        private RectTransform _stateRegion;
        private GameObject _composerRoot;
        private InputField _composerInput;
        private Button _sendButton;
        private Text _sendLabel;
        private IChatSocialGateway _gateway;
        private CancellationTokenSource _cts;
        private string _selectedChannelId = string.Empty;
        private string _selectedChannelLabel = string.Empty;
        private int _messageRowCount;
        private bool _hostedInDrawer;

        /// <summary>Message ids this session posted. The only honest own/other signal available:
        /// there is no public identity contract, and comparing raw account ids would mean holding
        /// (and eventually rendering) exactly the id the capture gate forbids.</summary>
        private readonly HashSet<string> _ownMessageIds = new HashSet<string>();

        public GameObject CanvasObjectForTests => _canvasObj;
        public string StatusTextForTests => _statusText != null ? _statusText.text : null;
        public string SelectedChannelIdForTests => _selectedChannelId;
        public InputField ComposerInputForTests => _composerInput;

        /// <summary>Number of real per-message rows currently in the thread.</summary>
        public int MessageRowCountForTests => _messageRowCount;

        /// <summary>Title of the state region currently shown, or null when the thread is showing
        /// real messages instead of a state.</summary>
        public string StreamStateTitleForTests =>
            _stateRegion != null && _stateRegion.gameObject.activeSelf
                ? _stateRegion.Find("EmptyState_Title")?.GetComponent<Text>()?.text
                : null;

        /// <summary>Read-only and unavailable channels get NO composer, not a disabled one.</summary>
        public bool ComposerVisibleForTests => _composerRoot != null && _composerRoot.activeSelf;

        public bool HostedInSocialDrawerForTests => _hostedInDrawer;

        /// <summary>Text SEND posts. Reads/writes the real composer InputField directly.</summary>
        public string ComposedText
        {
            get => _composerInput != null ? _composerInput.text : string.Empty;
            set { if (_composerInput != null) _composerInput.text = value ?? string.Empty; }
        }

        public void Initialize(Action onBack, IChatSocialGateway gateway = null)
        {
            _onBack = onBack;
            _gateway = gateway ?? new UnityCloudCodeChatSocialGateway();
            BuildUI();
        }

        public ChatSocialActionResult SendMessageForTests()
        {
            var r = ChatSocialOpenValues.TrySendMessage();
            SetStatus(r.Message);
            return r;
        }

        public Task<PostChatMessageGatewayResult> SendComposedForTests() => SendComposedAsync();

        public Task<FetchChatHistoryGatewayResult> RefreshHistoryForTests() => RefreshHistoryAsync();

        public void SelectChannelForTests(int index) => SelectChannel(index);

        // ------------------------------------------------------------------ build

        private void BuildUI()
        {
            TeardownUI();
            _cts = new CancellationTokenSource();

            // Kills any stranded metagame canvas, including a previous ChatSocialCanvas, so a
            // re-open (drawer tab switch, back-and-forward) can never leave two of this screen
            // stacked and double-raycasting.
            CampaignMapPresenter.CleanupStaleMetagameCanvases();

            _hostedInDrawer = DetectHostDrawer();

            Canvas canvas = UISharedFoundation.CreateScreenCanvas(CanvasName, new Vector2(DesignWidth, DesignHeight));
            _canvasObj = canvas.gameObject;

            // Above the host drawer (sortingOrder 20) when hosted: its dimmer is a full-screen
            // raycast target, and under it not one control on this screen is tappable. Unhosted,
            // the original secondary-shell order is kept.
            canvas.sortingOrder = _hostedInDrawer ? 22 : 12;

            float topInset = _hostedInDrawer ? HostTabBarHeight : 0f;

            // Opaque theme backing under the preserveAspect shell art — kills camera clear-colour
            // (sky-blue) letterbox bleed on non-16:9 viewports. Inset below the host tab bar so
            // the drawer's own CHAT/MAIL/FRIENDS row stays visible and hittable.
            Color shellFallback = new Color(0.08f, 0.09f, 0.12f);
            GameObject backing = new GameObject("BackgroundBacking", typeof(RectTransform), typeof(Image));
            backing.transform.SetParent(_canvasObj.transform, false);
            SetPx(backing.GetComponent<RectTransform>(), 0f, topInset, DesignWidth, DesignHeight - topInset);
            Image backingImg = backing.GetComponent<Image>();
            backingImg.color = new Color(shellFallback.r, shellFallback.g, shellFallback.b, 1f);
            backingImg.raycastTarget = false;

            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(_canvasObj.transform, false);
            SetPx(bg.GetComponent<RectTransform>(), 0f, topInset, DesignWidth, DesignHeight - topInset);
            ChatSocialUiLibrary.ApplyFullscreenShell(bg.GetComponent<Image>(), shellFallback);

            BuildHeader(topInset);
            float bodyTop = topInset + HeaderHeight + Gutter;
            float bodyBottom = DesignHeight - SafeMargin;
            BuildChannelRail(bodyTop, bodyBottom);
            BuildStreamColumn(bodyTop, bodyBottom);
            BuildContextColumn(bodyTop, bodyBottom);

            if (ChatSocialOpenValues.ChannelLabels.Length > 0)
            {
                SelectChannel(0);
            }
        }

        /// <summary>True when this screen is running as a tab inside Home's Social drawer. Read
        /// only — the drawer object is never modified from here.</summary>
        private static bool DetectHostDrawer()
        {
            GameObject drawer = GameObject.Find(HostDrawerObjectName);
            return drawer != null && drawer.GetComponent<Canvas>() != null;
        }

        private void BuildHeader(float topInset)
        {
            GameObject topBar = new GameObject("ChatSocialHeader", typeof(RectTransform), typeof(Image));
            topBar.transform.SetParent(_canvasObj.transform, false);
            Image topBg = topBar.GetComponent<Image>();
            topBg.color = UIFrozenTokens.ColorHeader;
            topBg.raycastTarget = false;
            SetPx(topBar.GetComponent<RectTransform>(), 0f, topInset, DesignWidth, HeaderHeight);
            Image scrim = UISharedFoundation.AddLocalGradientScrim(topBar.transform, Vector2.zero,
                new Vector2(DesignWidth, HeaderHeight), UISharedFoundation.GradientDirection.TopToBottom, 0.95f);
            if (scrim != null) scrim.raycastTarget = false;

            GameObject backBtn = new GameObject("Btn_Back", typeof(RectTransform), typeof(Image), typeof(Button));
            backBtn.transform.SetParent(topBar.transform, false);
            Image backImg = backBtn.GetComponent<Image>();
            Button back = backBtn.GetComponent<Button>();
            HomeV3UiLibrary.ApplyNavTileButton(back, backImg);
            backImg.color = new Color(0.3f, 0.2f, 0.2f);
            back.onClick.AddListener(() =>
            {
                TeardownUI();
                _onBack?.Invoke();
            });
            // 160x56 keeps the reference's 48-56px utility target floor.
            SetPxIn(backBtn.GetComponent<RectTransform>(), SafeMargin, (HeaderHeight - 56f) * 0.5f, 160f, 56f);
            Text backTxt = UISharedFoundation.CreateText(backBtn.transform, "Text", "< BACK", UITextRole.Body,
                TextAnchor.MiddleCenter, Color.white, true, new Vector2(140f, 44f));
            backTxt.fontSize = 22;
            backTxt.fontStyle = FontStyle.Bold;
            backTxt.verticalOverflow = VerticalWrapMode.Truncate;
            backTxt.raycastTarget = false;

            // Header order per the reference: Back, CHAT identity, then selected-channel context
            // (the context/status band on the right).
            Text title = UISharedFoundation.CreateText(topBar.transform, "Title", "CHAT",
                UITextRole.Display, TextAnchor.MiddleLeft, Color.white, true, new Vector2(200f, 48f));
            title.fontSize = 30;
            title.fontStyle = FontStyle.Bold;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            title.raycastTarget = false;
            SetPxIn(title.rectTransform, SafeMargin + 160f + Gutter, 12f, 200f, 44f);

            // Alias only — MetagameShellProfileBinding.PlayerDisplayName is the player's own
            // chosen name, never a backend account id.
            Text identity = UISharedFoundation.CreateText(topBar.transform, "SelfIdentity",
                MetagameShellProfileBinding.SelfIdentityLine(), UITextRole.Caption, TextAnchor.MiddleLeft,
                Color.white, true, new Vector2(360f, 28f));
            identity.fontSize = 22;
            identity.fontStyle = FontStyle.Bold;
            identity.verticalOverflow = VerticalWrapMode.Truncate;
            identity.raycastTarget = false;
            SetPxIn(identity.rectTransform, SafeMargin + 160f + Gutter, 58f, 360f, 32f);

            _statusText = UISharedFoundation.CreateText(topBar.transform, "StatusLine", ChatSocialOpenValues.PlayerStatus,
                UITextRole.Caption, TextAnchor.MiddleRight, Color.white, true, new Vector2(520f, 40f));
            _statusText.fontSize = 22;
            _statusText.fontStyle = FontStyle.Bold;
            _statusText.verticalOverflow = VerticalWrapMode.Truncate;
            _statusText.raycastTarget = false;
            SetPxIn(_statusText.rectTransform, DesignWidth - SafeMargin - 620f, (HeaderHeight - 40f) * 0.5f, 620f, 40f);
        }

        private void BuildChannelRail(float bodyTop, float bodyBottom)
        {
            GameObject rail = new GameObject("ChannelRail", typeof(RectTransform));
            rail.transform.SetParent(_canvasObj.transform, false);
            SetPx(rail.GetComponent<RectTransform>(), SafeMargin, bodyTop, RailWidth, bodyBottom - bodyTop);

            string[] channels = ChatSocialOpenValues.ChannelLabels;
            const float rowGap = 6f;
            float available = bodyBottom - bodyTop;
            float rowHeight = Mathf.Min(MaxChannelRowHeight,
                (available - rowGap * (channels.Length - 1)) / channels.Length);

            for (int i = 0; i < channels.Length; i++)
            {
                int idx = i;
                GameObject row = new GameObject($"Channel_{channels[i]}", typeof(RectTransform), typeof(Image), typeof(Button));
                row.transform.SetParent(rail.transform, false);
                Image img = row.GetComponent<Image>();
                Button btn = row.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => SelectChannel(idx));
                SetPxIn(row.GetComponent<RectTransform>(), 0f, i * (rowHeight + rowGap), RailWidth, rowHeight);
                // Applied AFTER final positioning - see EmpirePresenter's same fix for why.
                UISharedFoundation.ApplyFramedPanel(img, null,
                    UIFrozenTokens.ColorPanel, UIFrozenTokens.ColorHeader,
                    kind: UISharedFoundation.FramedPanelKind.ListRow);

                Text label = UISharedFoundation.CreateText(row.transform, "Label", channels[i], UITextRole.Body,
                    TextAnchor.MiddleLeft, UIFrozenTokens.ColorTextPrimary, true, new Vector2(200f, 30f));
                label.fontStyle = FontStyle.Bold;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.raycastTarget = false;
                SetPxIn(label.rectTransform, 18f, (rowHeight - 56f) * 0.5f, RailWidth - 36f, 30f);

                // Capability sub-line instead of an unread badge: an unread count is a backend
                // claim this screen cannot make, and the reference forbids inventing one (a
                // missing count is ABSENT, never "0").
                Text sub = UISharedFoundation.CreateText(row.transform, "State",
                    ChannelStateLabel(channels[i]), UITextRole.Caption,
                    TextAnchor.MiddleLeft, new Color(0.75f, 0.73f, 0.66f), true, new Vector2(200f, 24f));
                sub.fontSize = 18;
                sub.verticalOverflow = VerticalWrapMode.Truncate;
                sub.raycastTarget = false;
                SetPxIn(sub.rectTransform, 18f, (rowHeight - 56f) * 0.5f + 32f, RailWidth - 36f, 24f);

                // Selection is carried by an explicit marker glyph as well as tone, because
                // colour alone is not an accessible state signal.
                Text marker = UISharedFoundation.CreateText(row.transform, "SelectionMarker", string.Empty,
                    UITextRole.Body, TextAnchor.MiddleCenter, UIFrozenTokens.ColorAccentCyan, true, new Vector2(20f, 30f));
                marker.fontStyle = FontStyle.Bold;
                marker.verticalOverflow = VerticalWrapMode.Truncate;
                marker.raycastTarget = false;
                SetPxIn(marker.rectTransform, RailWidth - 34f, (rowHeight - 30f) * 0.5f, 24f, 30f);
            }
        }

        private void BuildStreamColumn(float bodyTop, float bodyBottom)
        {
            float streamX = SafeMargin + RailWidth + Gutter;

            // Channel/context bar: which channel is open and what it can actually do.
            GameObject bar = new GameObject("ChannelBar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(_canvasObj.transform, false);
            SetPx(bar.GetComponent<RectTransform>(), streamX, bodyTop, StreamWidth, ChannelBarHeight);
            Image barImg = bar.GetComponent<Image>();
            UISharedFoundation.ApplyFramedPanel(barImg, null,
                UIFrozenTokens.ColorHeader, UIFrozenTokens.ColorPanel,
                kind: UISharedFoundation.FramedPanelKind.ListRow);
            barImg.raycastTarget = false;
            _channelBarText = UISharedFoundation.CreateText(bar.transform, "Label", string.Empty, UITextRole.Body,
                TextAnchor.MiddleLeft, UIFrozenTokens.ColorTextPrimary, true, new Vector2(StreamWidth - 40f, 30f));
            _channelBarText.fontStyle = FontStyle.Bold;
            _channelBarText.verticalOverflow = VerticalWrapMode.Truncate;
            _channelBarText.raycastTarget = false;
            SetPxIn(_channelBarText.rectTransform, 20f, (ChannelBarHeight - 30f) * 0.5f, StreamWidth - 40f, 30f);

            float threadTop = bodyTop + ChannelBarHeight + 12f;
            float threadBottom = bodyBottom - ComposerHeight - Gutter;
            float threadHeight = threadBottom - threadTop;

            GameObject scrollObj = new GameObject("MessageStream", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollObj.transform.SetParent(_canvasObj.transform, false);
            _threadScrollObj = scrollObj;
            SetPx(scrollObj.GetComponent<RectTransform>(), streamX, threadTop, StreamWidth, threadHeight);
            Image scrollBg = scrollObj.GetComponent<Image>();
            scrollBg.color = new Color(0f, 0f, 0f, 0.20f);
            // The one intentional raycast surface here: a ScrollRect drag needs a hit target, and
            // it is confined to the thread column, never the screen.
            scrollBg.raycastTarget = true;

            GameObject viewportObj = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportObj.transform.SetParent(scrollObj.transform, false);
            Image viewportImg = viewportObj.GetComponent<Image>();
            viewportImg.color = new Color(1f, 1f, 1f, 0.02f);
            viewportObj.GetComponent<Mask>().showMaskGraphic = false;
            _threadViewport = viewportObj.GetComponent<RectTransform>();
            _threadViewport.anchorMin = Vector2.zero;
            _threadViewport.anchorMax = Vector2.one;
            _threadViewport.offsetMin = new Vector2(12f, 12f);
            _threadViewport.offsetMax = new Vector2(-12f, -12f);

            GameObject contentObj = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObj.transform.SetParent(viewportObj.transform, false);
            _threadContent = contentObj.GetComponent<RectTransform>();
            _threadContent.anchorMin = new Vector2(0f, 1f);
            _threadContent.anchorMax = new Vector2(1f, 1f);
            _threadContent.pivot = new Vector2(0.5f, 1f);
            _threadContent.anchoredPosition = Vector2.zero;
            _threadContent.sizeDelta = Vector2.zero;

            VerticalLayoutGroup layout = contentObj.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childAlignment = TextAnchor.UpperLeft;

            ContentSizeFitter fitter = contentObj.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = scrollObj.GetComponent<ScrollRect>();
            scroll.viewport = _threadViewport;
            scroll.content = _threadContent;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            // Loading/empty/error/DM all render here, over the same band the thread occupies, so
            // a state never leaves a blank panel behind. Built as a sibling rather than inside the
            // masked viewport: a state is not a scrolling thread item.
            GameObject stateObj = new GameObject("StreamState", typeof(RectTransform));
            stateObj.transform.SetParent(_canvasObj.transform, false);
            _stateRegion = stateObj.GetComponent<RectTransform>();
            SetPx(_stateRegion, streamX, threadTop, StreamWidth, threadHeight);
            stateObj.SetActive(false);

            BuildComposer(streamX, bodyBottom - ComposerHeight);
        }

        private void BuildComposer(float streamX, float composerTop)
        {
            GameObject root = new GameObject("Composer", typeof(RectTransform));
            root.transform.SetParent(_canvasObj.transform, false);
            _composerRoot = root;
            SetPx(root.GetComponent<RectTransform>(), streamX, composerTop, StreamWidth, ComposerHeight);

            _composerInput = UISharedFoundation.CreateInputField(root.transform, "ComposerInput",
                ComposerHint, new Color(0.9f, 0.88f, 0.75f),
                new Vector2(StreamWidth - SendWidth - Gutter, ComposerHeight), characterLimit: 500);
            SetPxIn(_composerInput.GetComponent<RectTransform>(), 0f, 0f,
                StreamWidth - SendWidth - Gutter, ComposerHeight);

            GameObject send = new GameObject("Btn_ComposerSend", typeof(RectTransform), typeof(Image), typeof(Button));
            send.transform.SetParent(root.transform, false);
            Image sendImg = send.GetComponent<Image>();
            _sendButton = send.GetComponent<Button>();
            HomeV3UiLibrary.ApplyNeutralActionButton(_sendButton, sendImg, new Color(0.2f, 0.35f, 0.4f));
            _sendButton.onClick.AddListener(() => _ = SendComposedAsync());
            // 176x86 clears the reference's >= 64x160 primary-target floor.
            SetPxIn(send.GetComponent<RectTransform>(), StreamWidth - SendWidth, 0f, SendWidth, ComposerHeight);
            _sendLabel = UISharedFoundation.CreateText(send.transform, "Text", SendLabel, UITextRole.Body,
                TextAnchor.MiddleCenter, Color.white, true, new Vector2(SendWidth - 20f, 36f));
            _sendLabel.verticalOverflow = VerticalWrapMode.Truncate;
            _sendLabel.raycastTarget = false;
            // Without this, a disabled SEND is label-only: ApplyNeutralActionButton sets
            // disabledSprite = normal, so Unity's own SpriteSwap shows no difference.
            send.AddComponent<InteractionStateController>().Tier = UIDesignTokens.FrameTier.Tier3Utility;
        }

        private void BuildContextColumn(float bodyTop, float bodyBottom)
        {
            float contextX = SafeMargin + RailWidth + Gutter + StreamWidth + Gutter;
            // Reserve the host drawer's CLOSE target when hosted - this column is the only one
            // that reaches into that corner.
            float bottom = bodyBottom - (_hostedInDrawer ? HostCloseReserve : 0f);

            GameObject panel = new GameObject("ContextPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(_canvasObj.transform, false);
            SetPx(panel.GetComponent<RectTransform>(), contextX, bodyTop, ContextWidth, bottom - bodyTop);
            Image panelImg = panel.GetComponent<Image>();
            UISharedFoundation.ApplyFramedPanel(panelImg, null,
                UIFrozenTokens.ColorPanel, UIFrozenTokens.ColorHeader,
                kind: UISharedFoundation.FramedPanelKind.ListRow);
            panelImg.raycastTarget = false;

            Text heading = UISharedFoundation.CreateText(panel.transform, "Heading", "CHANNEL", UITextRole.Body,
                TextAnchor.MiddleLeft, UIFrozenTokens.ColorTextPrimary, true, new Vector2(ContextWidth - 40f, 30f));
            heading.fontStyle = FontStyle.Bold;
            heading.verticalOverflow = VerticalWrapMode.Truncate;
            heading.raycastTarget = false;
            SetPxIn(heading.rectTransform, 20f, 20f, ContextWidth - 40f, 30f);

            // Replaces V1's unused right-hand panel with truthful capability information — what
            // this channel can do right now, never an unread/member/moderation claim.
            _capabilityText = UISharedFoundation.CreateText(panel.transform, "Capability", string.Empty,
                UITextRole.Caption, TextAnchor.UpperLeft, new Color(0.82f, 0.80f, 0.72f), true,
                new Vector2(ContextWidth - 40f, bottom - bodyTop - 80f));
            _capabilityText.fontSize = 19;
            _capabilityText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _capabilityText.verticalOverflow = VerticalWrapMode.Truncate;
            _capabilityText.raycastTarget = false;
            SetPxIn(_capabilityText.rectTransform, 20f, 62f, ContextWidth - 40f, bottom - bodyTop - 82f);
        }

        // ------------------------------------------------------------------ channel state

        private static string ChannelIdFor(string channelLabel) => channelLabel.ToLowerInvariant().Replace(" ", "-");

        /// <summary>Broadcast and System are read-only by the shell contract - see
        /// <see cref="ChatSocialOpenValues.StatusNote"/>, "Broadcast/System remain read-only".
        /// Matched against <see cref="ChatSocialOpenValues.ChannelLabels"/>, the only approved
        /// channel list, so this never becomes a second source of channel truth.</summary>
        private static bool IsReadOnlyChannel(string channelLabel) =>
            channelLabel == "Broadcast" || channelLabel == "System";

        /// <summary>DM has no live route. It is presented as unavailable rather than posted to.
        /// This is presentation only - no DM behaviour is implemented here.</summary>
        private static bool IsUnavailableChannel(string channelLabel) => channelLabel == "DM";

        private static string ChannelStateLabel(string channelLabel) =>
            IsUnavailableChannel(channelLabel) ? "Unavailable"
            : IsReadOnlyChannel(channelLabel) ? "Read-only"
            : "Open";

        private static string CapabilityCopyFor(string channelLabel)
        {
            if (IsUnavailableChannel(channelLabel))
                return "Direct messages are not connected to this screen.\n\n" +
                       "Conversation lists, requests, privacy settings and delivery state are not available yet.";
            if (IsReadOnlyChannel(channelLabel))
                return $"{channelLabel} is read-only.\n\nMessages here are posted by the game. " +
                       "There is no composer on this channel.";
            return $"{channelLabel} accepts messages up to 500 characters.\n\n" +
                   "Unread counts, member lists, pinned announcements and moderation are not " +
                   "connected to this screen yet.";
        }

        private void SelectChannel(int index)
        {
            string[] channels = ChatSocialOpenValues.ChannelLabels;
            if (index < 0 || index >= channels.Length) return;

            _selectedChannelLabel = channels[index];
            _selectedChannelId = ChannelIdFor(_selectedChannelLabel);

            ApplyChannelSelectionVisuals(index);
            ApplyComposerAvailability(_selectedChannelLabel);

            if (_channelBarText != null)
                _channelBarText.text = $"{_selectedChannelLabel}  ·  {ChannelStateLabel(_selectedChannelLabel)}";
            if (_capabilityText != null)
                _capabilityText.text = CapabilityCopyFor(_selectedChannelLabel);

            if (IsUnavailableChannel(_selectedChannelLabel))
            {
                // Truthful unavailable state: no history call is made for a route that does not
                // exist, so the player never sees a load that cannot resolve.
                ClearThread();
                ShowState(EmptyStateKind.Locked, DmTitle, DmBody, null, null, null,
                    ChatSocialUiLibrary.DirectMessagesChromeResourcePath);
                SetStatus(UnavailableStatus);
                return;
            }

            _ = RefreshHistoryAsync();
        }

        private void ApplyChannelSelectionVisuals(int selectedIndex)
        {
            if (_canvasObj == null) return;
            Transform rail = _canvasObj.transform.Find("ChannelRail");
            if (rail == null) return;

            string[] channels = ChatSocialOpenValues.ChannelLabels;
            for (int i = 0; i < rail.childCount && i < channels.Length; i++)
            {
                Transform row = rail.GetChild(i);
                bool selected = i == selectedIndex;
                Image img = row.GetComponent<Image>();
                if (img != null)
                    img.color = selected ? new Color(1f, 1f, 1f, 1f) : new Color(0.72f, 0.72f, 0.76f, 1f);
                Text marker = row.Find("SelectionMarker")?.GetComponent<Text>();
                // Glyph + tone together: a selected row must not be signalled by colour alone.
                if (marker != null) marker.text = selected ? "▶" : string.Empty;
                Text label = row.Find("Label")?.GetComponent<Text>();
                if (label != null)
                    label.color = selected ? Color.white : UIFrozenTokens.ColorTextPrimary;
            }
        }

        /// <summary>Read-only and unavailable channels get NO composer at all — the reference is
        /// explicit that Broadcast/System have no composer, and a disabled control is the same
        /// dead end wearing a control's clothes. The control is never merely covered: a scrim over
        /// a live button passes the layout gate and still lets a tap through.</summary>
        private void ApplyComposerAvailability(string channelLabel)
        {
            bool locked = IsUnavailableChannel(channelLabel) || IsReadOnlyChannel(channelLabel);

            if (_composerInput != null && locked) _composerInput.text = string.Empty;
            if (_composerRoot != null) _composerRoot.SetActive(!locked);
            if (_sendButton != null) _sendButton.interactable = !locked;
            if (_sendLabel != null) _sendLabel.text = SendLabel;
        }

        // ------------------------------------------------------------------ thread rendering

        private void ClearThread()
        {
            _messageRowCount = 0;
            if (_threadContent == null) return;
            for (int i = _threadContent.childCount - 1; i >= 0; i--)
            {
                GameObject child = _threadContent.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
        }

        private void ShowState(EmptyStateKind kind, string title, string explanation, string statusLine,
            string actionLabel, Action onAction, string illustrationPath = null)
        {
            if (_stateRegion == null) return;
            for (int i = _stateRegion.childCount - 1; i >= 0; i--)
            {
                GameObject child = _stateRegion.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }

            _stateRegion.gameObject.SetActive(true);
            if (_threadScrollObj != null) _threadScrollObj.SetActive(false);
            UIEmptyState.Build(_stateRegion, kind, title, explanation, statusLine, actionLabel, onAction, illustrationPath);
        }

        private void HideState()
        {
            if (_stateRegion != null) _stateRegion.gameObject.SetActive(false);
            if (_threadScrollObj != null) _threadScrollObj.SetActive(true);
        }

        /// <summary>Alias-only sender label. <c>senderAccountId</c> is a raw backend account id and
        /// rendering it is an explicit capture-gate failure, while there is still no approved
        /// public identity contract to substitute. Own messages (posted this session) read "You";
        /// everyone else gets a neutral generated marker derived from the id, which is stable
        /// within a channel without ever showing the id itself.</summary>
        private string AliasFor(ChatMessageDto message)
        {
            if (message != null && !string.IsNullOrEmpty(message.id) && _ownMessageIds.Contains(message.id))
                return "You";
            if (message == null || string.IsNullOrEmpty(message.senderAccountId))
                return "Unknown rider";

            unchecked
            {
                int hash = 17;
                foreach (char c in message.senderAccountId) hash = hash * 31 + c;
                return "Rider " + ((hash & 0xFFF).ToString("X3"));
            }
        }

        private static string TimestampFor(ChatMessageDto message)
        {
            if (message == null || message.sentUtcMs <= 0L) return string.Empty;
            return DateTimeOffset.FromUnixTimeMilliseconds(message.sentUtcMs).UtcDateTime.ToString("HH:mm");
        }

        /// <summary>One real row per message: a quiet alias/timestamp meta line above the message
        /// body, own messages aligned right against a different panel tone. Row height is measured
        /// from the wrapped body so a long message grows its own row instead of spilling past it.</summary>
        private void BuildMessageRow(ChatMessageDto message, int index)
        {
            if (_threadContent == null) return;

            bool own = message != null && !string.IsNullOrEmpty(message.id) && _ownMessageIds.Contains(message.id);
            float contentWidth = StreamWidth - 24f - 12f;
            float bubbleWidth = Mathf.Floor(contentWidth * BubbleWidthFraction);
            float innerWidth = bubbleWidth - 32f;

            GameObject rowObj = new GameObject($"MessageRow_{index}", typeof(RectTransform),
                typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            rowObj.transform.SetParent(_threadContent, false);
            HorizontalLayoutGroup rowLayout = rowObj.GetComponent<HorizontalLayoutGroup>();
            rowLayout.childControlHeight = true;
            rowLayout.childControlWidth = true;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childAlignment = own ? TextAnchor.UpperRight : TextAnchor.UpperLeft;

            GameObject bubble = new GameObject("Bubble", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            bubble.transform.SetParent(rowObj.transform, false);
            Image bubbleImg = bubble.GetComponent<Image>();
            UISharedFoundation.ApplyFramedPanel(bubbleImg, null,
                own ? UIFrozenTokens.ColorSecondary : UIFrozenTokens.ColorPanel,
                own ? UIFrozenTokens.ColorPanel : UIFrozenTokens.ColorHeader,
                kind: UISharedFoundation.FramedPanelKind.ListRow);
            bubbleImg.raycastTarget = false;
            LayoutElement bubbleLayout = bubble.GetComponent<LayoutElement>();
            bubbleLayout.preferredWidth = bubbleWidth;
            bubbleLayout.flexibleWidth = 0f;

            string alias = AliasFor(message);
            string stamp = TimestampFor(message);
            Text meta = UISharedFoundation.CreateText(bubble.transform, "Meta",
                string.IsNullOrEmpty(stamp) ? alias : $"{alias}  ·  {stamp}",
                UITextRole.Caption, own ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft,
                own ? UIFrozenTokens.ColorAccentCyan : new Color(0.74f, 0.72f, 0.64f), false,
                new Vector2(innerWidth, 24f));
            meta.fontSize = 18;
            meta.fontStyle = FontStyle.Bold;
            meta.verticalOverflow = VerticalWrapMode.Truncate;
            meta.raycastTarget = false;

            Text body = UISharedFoundation.CreateText(bubble.transform, "Body",
                message?.text ?? string.Empty, UITextRole.Body,
                own ? TextAnchor.UpperRight : TextAnchor.UpperLeft,
                UIFrozenTokens.ColorTextPrimary, false, new Vector2(innerWidth, 28f));
            body.fontSize = 22;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;
            body.raycastTarget = false;

            // Measure the wrapped body at its real width, then size the bubble to it: a row that
            // is not tall enough for its own text is exactly the geometry regression the standing
            // UI gate exists to catch.
            body.rectTransform.sizeDelta = new Vector2(innerWidth, 28f);
            float bodyHeight = Mathf.Max(28f, body.preferredHeight);
            body.rectTransform.sizeDelta = new Vector2(innerWidth, bodyHeight);

            SetPxIn(meta.rectTransform, 16f, 12f, innerWidth, 24f);
            SetPxIn(body.rectTransform, 16f, 40f, innerWidth, bodyHeight);

            float bubbleHeight = bodyHeight + 52f;
            bubbleLayout.preferredHeight = bubbleHeight;
            bubbleLayout.minHeight = bubbleHeight;
            LayoutElement rowElement = rowObj.GetComponent<LayoutElement>();
            rowElement.preferredHeight = bubbleHeight;
            rowElement.minHeight = bubbleHeight;

            _messageRowCount++;
        }

        private void RenderThread(List<ChatMessageDto> messages)
        {
            ClearThread();
            HideState();
            // Oldest first, newest at the bottom - the order a thread is actually read in. The
            // gateway returns newest-first.
            List<ChatMessageDto> ordered = messages.AsEnumerable().Reverse().ToList();
            for (int i = 0; i < ordered.Count; i++) BuildMessageRow(ordered[i], i);

            if (_threadContent != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_threadContent);
                // Newest message in view, which is where a reader expects a thread to open.
                _threadContent.anchoredPosition = new Vector2(_threadContent.anchoredPosition.x,
                    Mathf.Max(0f, _threadContent.rect.height - (_threadViewport != null ? _threadViewport.rect.height : 0f)));
            }
        }

        // ------------------------------------------------------------------ gateway

        private async Task<FetchChatHistoryGatewayResult> RefreshHistoryAsync()
        {
            if (string.IsNullOrEmpty(_selectedChannelId))
                return new FetchChatHistoryGatewayResult { errorCode = "INVALID_REQUEST" };

            string requestedChannelId = _selectedChannelId;
            ClearThread();
            ShowState(EmptyStateKind.Waiting, LoadingTitle, LoadingBody, null, null, null);

            try
            {
                FetchChatHistoryGatewayResult result =
                    await _gateway.FetchChannelHistoryAsync(requestedChannelId, HistoryLimit, Token).ConfigureAwait(true);

                // The screen may have been torn down (drawer closed, tab switched) or moved to a
                // different channel while this was in flight. Writing into a destroyed hierarchy,
                // or painting a stale channel's messages over the current one, are both real
                // teardown bugs - drop the response instead.
                if (IsStale(requestedChannelId))
                    return result ?? new FetchChatHistoryGatewayResult { errorCode = "CANCELLED" };

                if (result == null)
                {
                    Debug.LogWarning($"[ChatSocial] History returned no response for channel '{requestedChannelId}'.");
                    ShowHistoryError();
                    return new FetchChatHistoryGatewayResult { errorCode = "NULL_RESPONSE" };
                }

                if (!result.success)
                {
                    Debug.LogWarning($"[ChatSocial] History failed for channel '{requestedChannelId}': errorCode={result.errorCode ?? "unknown"}.");
                    ShowHistoryError();
                    return result;
                }

                List<ChatMessageDto> messages = result.messages ?? new List<ChatMessageDto>();
                if (messages.Count == 0)
                {
                    ClearThread();
                    // Waiting, not Actionable: nothing is wrong and nothing is owed. The status
                    // line carries the player's own alias so an empty channel still says who is
                    // about to speak in it.
                    ShowState(EmptyStateKind.Waiting, EmptyTitle,
                        $"Messages sent in {_selectedChannelLabel} appear here.",
                        MetagameShellProfileBinding.SelfIdentityLine(), null, null);
                    SetStatus(EmptyTitle);
                }
                else
                {
                    RenderThread(messages);
                    SetStatus($"{messages.Count} message(s).");
                }

                return result;
            }
            catch (Exception ex)
            {
                if (IsStale(requestedChannelId))
                    return new FetchChatHistoryGatewayResult { errorCode = "CANCELLED" };
                Debug.LogWarning($"[ChatSocial] History threw for channel '{requestedChannelId}': {ex.Message}");
                ShowHistoryError();
                return new FetchChatHistoryGatewayResult { errorCode = "CLIENT_EXCEPTION" };
            }
        }

        private void ShowHistoryError()
        {
            ClearThread();
            // Actionable, and the retry is real: the reference allows RETRY SAFELY only as a
            // genuine re-request of the same channel.
            ShowState(EmptyStateKind.Actionable, ErrorTitle, ErrorBody, null,
                RetryLabel, () => _ = RefreshHistoryAsync());
            SetStatus(HistoryUnavailableStatus);
        }

        /// <summary>True when a response should be dropped: the screen was torn down, the request
        /// was cancelled, or the player moved to a different channel while it was in flight.</summary>
        private bool IsStale(string requestedChannelId) =>
            _canvasObj == null || _cts == null || _cts.IsCancellationRequested ||
            !string.Equals(requestedChannelId, _selectedChannelId, StringComparison.Ordinal);

        private async Task<PostChatMessageGatewayResult> SendComposedAsync()
        {
            if (IsUnavailableChannel(_selectedChannelLabel))
            {
                // Presentation guard only: an unsupported route is never called, so the player
                // cannot be shown a failure that a working feature would not produce.
                SetStatus(UnavailableStatus);
                return new PostChatMessageGatewayResult { errorCode = "INVALID_REQUEST" };
            }

            if (IsReadOnlyChannel(_selectedChannelLabel))
            {
                SetStatus(ReadOnlyStatus);
                return new PostChatMessageGatewayResult { errorCode = "INVALID_REQUEST" };
            }

            if (string.IsNullOrEmpty(_selectedChannelId) || string.IsNullOrWhiteSpace(ComposedText))
            {
                SetStatus(SendEmptyStatus);
                return new PostChatMessageGatewayResult { errorCode = "INVALID_REQUEST" };
            }

            string requestedChannelId = _selectedChannelId;
            try
            {
                PostChatMessageGatewayResult result =
                    await _gateway.PostMessageAsync(requestedChannelId, ComposedText, Token).ConfigureAwait(true);

                if (IsStale(requestedChannelId))
                    return result ?? new PostChatMessageGatewayResult { errorCode = "CANCELLED" };

                if (result != null && result.success)
                {
                    if (!string.IsNullOrEmpty(result.messageId)) _ownMessageIds.Add(result.messageId);
                    SetStatus("Sent.");
                    ComposedText = string.Empty;
                    await RefreshHistoryAsync().ConfigureAwait(true);
                }
                else
                {
                    Debug.LogWarning($"[ChatSocial] Send failed on channel '{requestedChannelId}': errorCode={result?.errorCode ?? "unknown"}.");
                    // Draft deliberately NOT cleared: the reference requires the draft to survive
                    // a failed send.
                    SetStatus(SendFailedStatus);
                }

                return result;
            }
            catch (Exception ex)
            {
                if (IsStale(requestedChannelId))
                    return new PostChatMessageGatewayResult { errorCode = "CANCELLED" };
                Debug.LogWarning($"[ChatSocial] Send threw on channel '{requestedChannelId}': {ex.Message}");
                SetStatus(SendFailedStatus);
                return new PostChatMessageGatewayResult { errorCode = "CLIENT_EXCEPTION" };
            }
        }

        private CancellationToken Token => _cts != null ? _cts.Token : CancellationToken.None;

        private void SetStatus(string message)
        {
            if (_statusText == null) return;
            // The OpenValues diagnostic is mapped to its short player-facing form: the
            // ActionResult Message carries the full StatusNote when values are not locked,
            // which overflows this band.
            _statusText.text = message == ChatSocialOpenValues.StatusNote
                ? ChatSocialOpenValues.PlayerStatus
                : (message ?? string.Empty);
        }

        // ------------------------------------------------------------------ geometry helpers

        /// <summary>Places a rect in 1920x1080 top-left pixel space against the canvas.</summary>
        private static void SetPx(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(left, -top);
        }

        /// <summary>Same, but relative to the parent's own top-left corner.</summary>
        private static void SetPxIn(RectTransform rect, float left, float top, float width, float height)
            => SetPx(rect, left, top, width, height);

        public void TeardownUI()
        {
            if (_cts != null)
            {
                // Cancel BEFORE destroying the hierarchy: an in-flight history/send continuation
                // that resumes after teardown must find a cancelled token and drop, not write
                // into destroyed Text components.
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            _statusText = null;
            _channelBarText = null;
            _capabilityText = null;
            _threadViewport = null;
            _threadContent = null;
            _threadScrollObj = null;
            _stateRegion = null;
            _composerRoot = null;
            _composerInput = null;
            _sendButton = null;
            _sendLabel = null;
            _messageRowCount = 0;

            if (_canvasObj == null) return;
            if (Application.isPlaying) Destroy(_canvasObj);
            else DestroyImmediate(_canvasObj);
            _canvasObj = null;
        }

        private void OnDestroy() => TeardownUI();
    }
}

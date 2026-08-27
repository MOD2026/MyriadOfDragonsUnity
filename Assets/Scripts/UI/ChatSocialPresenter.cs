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
    /// <summary>CHAT V1 art shell wired to <see cref="IChatSocialGateway"/> (live Chat CloudCode
    /// module, nonprod-validation). Channel select, SEND, and the composer text field are all
    /// real - SEND posts whatever is currently typed into <see cref="_composerInput"/>.</summary>
    public class ChatSocialPresenter : MonoBehaviour
    {
        public const string CanvasName = "ChatSocialCanvas";
        private const int HistoryLimit = 20;

        private GameObject _canvasObj;
        private Action _onBack;
        private Text _statusText;
        private Text _streamText;
        private InputField _composerInput;
        private IChatSocialGateway _gateway;
        private CancellationTokenSource _cts;
        private string _selectedChannelId = string.Empty;

        public GameObject CanvasObjectForTests => _canvasObj;
        public string StatusTextForTests => _statusText != null ? _statusText.text : null;
        public string SelectedChannelIdForTests => _selectedChannelId;
        public InputField ComposerInputForTests => _composerInput;

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

        private void BuildUI()
        {
            TeardownUI();
            _cts = new CancellationTokenSource();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();

            Canvas canvas = UISharedFoundation.CreateScreenCanvas(CanvasName, new Vector2(1920, 1080));
            _canvasObj = canvas.gameObject;
            canvas.sortingOrder = 12;

            // Opaque theme backing under preserveAspect shell art — kills camera clear-color
            // (sky-blue) letterbox bleed on non-16:9 viewports (same class of fix as e57aa02).
            Color shellFallback = new Color(0.08f, 0.09f, 0.12f);
            GameObject backing = new GameObject("BackgroundBacking", typeof(RectTransform), typeof(Image));
            backing.transform.SetParent(_canvasObj.transform, false);
            UISharedFoundation.StretchFull(backing.GetComponent<RectTransform>());
            Image backingImg = backing.GetComponent<Image>();
            backingImg.color = new Color(shellFallback.r, shellFallback.g, shellFallback.b, 1f);
            backingImg.raycastTarget = false;

            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(_canvasObj.transform, false);
            UISharedFoundation.StretchFull(bg.GetComponent<RectTransform>());
            ChatSocialUiLibrary.ApplyFullscreenShell(bg.GetComponent<Image>(), shellFallback);

            BuildHeader();
            BuildChannels(); BuildStream();

            if (ChatSocialOpenValues.ChannelLabels.Length > 0)
            {
                _selectedChannelId = ChannelIdFor(ChatSocialOpenValues.ChannelLabels[0]);
                _ = RefreshHistoryAsync();
            }
        }

        private void BuildHeader()
        {
            GameObject topBar = new GameObject("ChatSocialHeader", typeof(RectTransform), typeof(Image));
            topBar.transform.SetParent(_canvasObj.transform, false);
            Image topBg = topBar.GetComponent<Image>();
            topBg.color = UIFrozenTokens.ColorHeader;
            topBg.raycastTarget = false;
            SetNorm(topBar.GetComponent<RectTransform>(), 0f, 0.90f, 1f, 1f);
            UISharedFoundation.AddLocalGradientScrim(topBar.transform, Vector2.zero, new Vector2(1920f, 108f), UISharedFoundation.GradientDirection.TopToBottom, 0.95f);

            GameObject backBtn = new GameObject("Btn_Back", typeof(RectTransform), typeof(Image), typeof(Button));
            backBtn.transform.SetParent(topBar.transform, false);
            Image backImg = backBtn.GetComponent<Image>();
            HomeV3UiLibrary.ApplyNavTileButton(backBtn.GetComponent<Button>(), backImg);
            backImg.color = new Color(0.3f, 0.2f, 0.2f);
            backBtn.GetComponent<Button>().onClick.AddListener(() =>
            {
                TeardownUI();
                _onBack?.Invoke();
            });
            RectTransform backRect = backBtn.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0f, 0.5f);
            backRect.anchorMax = new Vector2(0f, 0.5f);
            backRect.pivot = new Vector2(0f, 0.5f);
            backRect.anchoredPosition = new Vector2(30f, 0f);
            backRect.sizeDelta = new Vector2(160f, 56f);
            UISharedFoundation.AddLocalGradientScrim(backBtn.transform, Vector2.zero, new Vector2(160f, 56f), UISharedFoundation.GradientDirection.TopToBottom, 0.95f);
            Text backTxt = UISharedFoundation.CreateText(backBtn.transform, "Text", "< BACK", UITextRole.Body,
                TextAnchor.MiddleCenter, Color.white, true, new Vector2(140f, 44f));
            backTxt.fontSize = 22;
            backTxt.fontStyle = FontStyle.Bold;

            Text title = UISharedFoundation.CreateText(topBar.transform, "Title", "CHAT",
                UITextRole.Display, TextAnchor.MiddleCenter, Color.white, true,
                new Vector2(640f, 48f));
            title.fontSize = 30;
            title.fontStyle = FontStyle.Bold;
            SetNorm(title.rectTransform, 0.28f, 0.15f, 0.72f, 0.9f);

            Text identity = UISharedFoundation.CreateText(topBar.transform, "SelfIdentity",
                MetagameShellProfileBinding.SelfIdentityLine(), UITextRole.Caption, TextAnchor.MiddleLeft,
                Color.white, true, new Vector2(280f, 28f));
            identity.fontSize = 22;
            identity.fontStyle = FontStyle.Bold;
            SetNorm(identity.rectTransform, 0.18f, 0.15f, 0.35f, 0.85f);

            _statusText = UISharedFoundation.CreateText(topBar.transform, "StatusLine", ChatSocialOpenValues.StatusNote,
                UITextRole.Caption, TextAnchor.MiddleRight, Color.white, true,
                new Vector2(520f, 40f));
            _statusText.fontSize = 22;
            _statusText.fontStyle = FontStyle.Bold;
            SetNorm(_statusText.rectTransform, 0.72f, 0.1f, 0.98f, 0.9f);
        }

        private void BuildChannels()
        {
            GameObject rail = new GameObject("ChannelRail", typeof(RectTransform));
            rail.transform.SetParent(_canvasObj.transform, false);
            SetNorm(rail.GetComponent<RectTransform>(), 0.02f, 0.14f, 0.16f, 0.88f);
            string[] channels = ChatSocialOpenValues.ChannelLabels;
            float h = 1f / channels.Length;
            for (int i = 0; i < channels.Length; i++)
            {
                int idx = i;
                GameObject row = new GameObject($"Channel_{channels[i]}", typeof(RectTransform), typeof(Image), typeof(Button));
                row.transform.SetParent(rail.transform, false);
                Image img = row.GetComponent<Image>();
                Button btn = row.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() =>
                {
                    _selectedChannelId = ChannelIdFor(channels[idx]);
                    SetStatus(ChatSocialOpenValues.TrySelectChannel(idx).Message);
                    _ = RefreshHistoryAsync();
                });
                SetNorm(row.GetComponent<RectTransform>(), 0.05f, 1f - (i + 1) * h + 0.02f, 0.95f, 1f - i * h - 0.02f);
                // Applied AFTER final positioning - see EmpirePresenter's same fix for why.
                UISharedFoundation.ApplyFramedPanel(img, null,
                    UIFrozenTokens.ColorPanel, UIFrozenTokens.ColorHeader,
                    kind: UISharedFoundation.FramedPanelKind.ListRow);
                UISharedFoundation.CreateText(row.transform, "Label", channels[i], UITextRole.Caption,
                    TextAnchor.MiddleCenter, Color.white, true, new Vector2(140f, 28f));
            }
        }

        private void BuildStream()
        {
            GameObject stream = new GameObject("MessageStream", typeof(RectTransform));
            stream.transform.SetParent(_canvasObj.transform, false);
            SetNorm(stream.GetComponent<RectTransform>(), 0.18f, 0.20f, 0.72f, 0.84f);
            _streamText = UISharedFoundation.CreateText(stream.transform, "Placeholder",
                "Loading…",
                UITextRole.Body, TextAnchor.UpperLeft,
                new Color(0.9f, 0.88f, 0.75f), true, new Vector2(900f, 400f));
            SetNorm(_streamText.rectTransform, 0.04f, 0.05f, 0.96f, 0.95f);

            _composerInput = UISharedFoundation.CreateInputField(_canvasObj.transform, "ComposerInput",
                "Type a message…", new Color(0.9f, 0.88f, 0.75f), new Vector2(340f, 44f), characterLimit: 500);
            SetNorm(_composerInput.GetComponent<RectTransform>(), 0.18f, 0.06f, 0.56f, 0.14f);

            GameObject composer = new GameObject("Btn_ComposerSend", typeof(RectTransform), typeof(Image), typeof(Button));
            composer.transform.SetParent(_canvasObj.transform, false);
            Image cImg = composer.GetComponent<Image>();
            HomeV3UiLibrary.ApplyNeutralActionButton(composer.GetComponent<Button>(), cImg, new Color(0.2f, 0.35f, 0.4f));
            composer.GetComponent<Button>().onClick.AddListener(() => _ = SendComposedAsync());
            SetNorm(composer.GetComponent<RectTransform>(), 0.58f, 0.06f, 0.72f, 0.14f);
            UISharedFoundation.CreateText(composer.transform, "Text", "SEND", UITextRole.Body,
                TextAnchor.MiddleCenter, Color.white, true, new Vector2(200f, 36f));
        }

        private static string ChannelIdFor(string channelLabel) => channelLabel.ToLowerInvariant().Replace(" ", "-");

        private async Task<FetchChatHistoryGatewayResult> RefreshHistoryAsync()
        {
            if (string.IsNullOrEmpty(_selectedChannelId))
                return new FetchChatHistoryGatewayResult { errorCode = "INVALID_REQUEST" };

            SetStream("Loading…");
            try
            {
                FetchChatHistoryGatewayResult result = await _gateway.FetchChannelHistoryAsync(_selectedChannelId, HistoryLimit, Token).ConfigureAwait(true);
                if (result == null)
                {
                    SetStream("No response.");
                    SetStatus("History: null response.");
                    return new FetchChatHistoryGatewayResult { errorCode = "NULL_RESPONSE" };
                }

                if (!result.success)
                {
                    SetStream($"History error: {result.errorCode}");
                    SetStatus($"History failed: {result.errorCode ?? "unknown"}");
                    return result;
                }

                List<ChatMessageDto> messages = result.messages ?? new List<ChatMessageDto>();
                SetStream(messages.Count == 0
                    ? $"Channel: {_selectedChannelId}\n{MetagameShellProfileBinding.SelfIdentityLine()}\n\nNo messages yet."
                    : string.Join("\n", messages.AsEnumerable().Reverse().Select(m => $"{m.senderAccountId}: {m.text}")));
                SetStatus($"History: {messages.Count} message(s).");
                return result;
            }
            catch (Exception ex)
            {
                SetStream("Load failed.");
                SetStatus($"History failed: {ex.Message}");
                return new FetchChatHistoryGatewayResult { errorCode = "CLIENT_EXCEPTION" };
            }
        }

        private async Task<PostChatMessageGatewayResult> SendComposedAsync()
        {
            if (string.IsNullOrEmpty(_selectedChannelId) || string.IsNullOrWhiteSpace(ComposedText))
            {
                SetStatus("Send: no channel or empty message.");
                return new PostChatMessageGatewayResult { errorCode = "INVALID_REQUEST" };
            }

            try
            {
                PostChatMessageGatewayResult result = await _gateway.PostMessageAsync(_selectedChannelId, ComposedText, Token).ConfigureAwait(true);
                if (result != null && result.success)
                {
                    SetStatus("Sent.");
                    ComposedText = string.Empty;
                    await RefreshHistoryAsync().ConfigureAwait(true);
                }
                else
                {
                    SetStatus($"Send failed: {result?.errorCode ?? "unknown"}");
                }

                return result;
            }
            catch (Exception ex)
            {
                SetStatus($"Send failed: {ex.Message}");
                return new PostChatMessageGatewayResult { errorCode = "CLIENT_EXCEPTION" };
            }
        }

        private CancellationToken Token => _cts != null ? _cts.Token : CancellationToken.None;

        private void SetStatus(string message)
        {
            if (_statusText != null)
                _statusText.text = message ?? string.Empty;
        }

        private void SetStream(string message)
        {
            if (_streamText != null)
                _streamText.text = message ?? string.Empty;
        }

        private static void SetNorm(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public void TeardownUI()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            if (_canvasObj == null) return;
            if (Application.isPlaying) Destroy(_canvasObj);
            else DestroyImmediate(_canvasObj);
            _canvasObj = null;
        }

        private void OnDestroy() => TeardownUI();
    }
}

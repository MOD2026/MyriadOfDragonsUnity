using System;
using MyriadOfDragons.Metagame;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>CHAT V1 art shell. Actions refuse while OpenValues stay OPEN.</summary>
    public class ChatSocialPresenter : MonoBehaviour
    {
        public const string CanvasName = "ChatSocialCanvas";

        private GameObject _canvasObj;
        private Action _onBack;
        private Text _statusText;
        private Text _streamText;

        public GameObject CanvasObjectForTests => _canvasObj;
        public string StatusTextForTests => _statusText != null ? _statusText.text : null;

        public void Initialize(Action onBack)
        {
            _onBack = onBack;
            BuildUI();
        }


        public ChatSocialActionResult SendMessageForTests()
        {
            var r = ChatSocialOpenValues.TrySendMessage();
            SetStatus(r.Message);
            return r;
        }


        private void BuildUI()
        {
            TeardownUI();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();

            Canvas canvas = UISharedFoundation.CreateScreenCanvas(CanvasName, new Vector2(1920, 1080));
            _canvasObj = canvas.gameObject;
            canvas.sortingOrder = 12;

            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(_canvasObj.transform, false);
            UISharedFoundation.StretchFull(bg.GetComponent<RectTransform>());
            ChatSocialUiLibrary.ApplyFullscreenShell(bg.GetComponent<Image>(), new Color(0.08f, 0.09f, 0.12f));

            BuildHeader();
            BuildChannels(); BuildStream();
        }

        private void BuildHeader()
        {
            GameObject topBar = new GameObject("ChatSocialHeader", typeof(RectTransform));
            topBar.transform.SetParent(_canvasObj.transform, false);
            SetNorm(topBar.GetComponent<RectTransform>(), 0f, 0.90f, 1f, 1f);

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
            UISharedFoundation.CreateText(backBtn.transform, "Text", "< BACK", UITextRole.Body,
                TextAnchor.MiddleCenter, Color.white, true, new Vector2(140f, 44f));

            Text title = UISharedFoundation.CreateText(topBar.transform, "Title", "CHAT",
                UITextRole.Display, TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 0.82f), true,
                new Vector2(640f, 48f));
            title.fontSize = 30;
            SetNorm(title.rectTransform, 0.28f, 0.15f, 0.72f, 0.9f);

            Text identity = UISharedFoundation.CreateText(topBar.transform, "SelfIdentity",
                MetagameShellProfileBinding.SelfIdentityLine(), UITextRole.Caption, TextAnchor.MiddleLeft,
                new Color(0.8f, 0.85f, 0.7f), true, new Vector2(280f, 28f));
            SetNorm(identity.rectTransform, 0.18f, 0.15f, 0.35f, 0.85f);

            _statusText = UISharedFoundation.CreateText(topBar.transform, "StatusLine", ChatSocialOpenValues.StatusNote,
                UITextRole.Caption, TextAnchor.MiddleRight, new Color(0.85f, 0.75f, 0.5f), true,
                new Vector2(520f, 40f));
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
                img.color = new Color(0.12f, 0.14f, 0.18f, 0.4f);
                Button btn = row.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() =>
                {
                    SetStream(
                        $"Channel: {channels[idx]}\n{MetagameShellProfileBinding.SelfIdentityLine()}\n\n" +
                        "No history — send/history OPEN (Social identity bootstrap exists; chat transport does not).");
                    SetStatus(ChatSocialOpenValues.TrySelectChannel(idx).Message);
                });
                SetNorm(row.GetComponent<RectTransform>(), 0.05f, 1f - (i + 1) * h + 0.02f, 0.95f, 1f - i * h - 0.02f);
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
                $"Select a channel.\n{MetagameShellProfileBinding.SelfIdentityLine()}\n\n" +
                "No channel history — send/history OPEN.",
                UITextRole.Body, TextAnchor.UpperLeft,
                new Color(0.9f, 0.88f, 0.75f), true, new Vector2(900f, 400f));
            SetNorm(_streamText.rectTransform, 0.04f, 0.05f, 0.96f, 0.95f);

            GameObject composer = new GameObject("Btn_ComposerSend", typeof(RectTransform), typeof(Image), typeof(Button));
            composer.transform.SetParent(_canvasObj.transform, false);
            Image cImg = composer.GetComponent<Image>();
            HomeV3UiLibrary.ApplyNeutralActionButton(composer.GetComponent<Button>(), cImg, new Color(0.2f, 0.35f, 0.4f));
            composer.GetComponent<Button>().onClick.AddListener(() => SetStatus(ChatSocialOpenValues.TrySendMessage().Message));
            SetNorm(composer.GetComponent<RectTransform>(), 0.18f, 0.06f, 0.72f, 0.14f);
            UISharedFoundation.CreateText(composer.transform, "Text", "SEND", UITextRole.Body,
                TextAnchor.MiddleCenter, Color.white, true, new Vector2(200f, 36f));
        }

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
            if (_canvasObj == null) return;
            if (Application.isPlaying) Destroy(_canvasObj);
            else DestroyImmediate(_canvasObj);
            _canvasObj = null;
        }

        private void OnDestroy() => TeardownUI();
    }
}

using System;
using MyriadOfDragons.Metagame;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>MAIL V1 art shell. Actions refuse while OpenValues stay OPEN.</summary>
    public class MailInboxPresenter : MonoBehaviour
    {
        public const string CanvasName = "MailInboxCanvas";

        private GameObject _canvasObj;
        private Action _onBack;
        private Text _statusText;

        public GameObject CanvasObjectForTests => _canvasObj;
        public string StatusTextForTests => _statusText != null ? _statusText.text : null;

        public void Initialize(Action onBack)
        {
            _onBack = onBack;
            BuildUI();
        }


        public MailInboxActionResult ClaimAttachmentForTests()
        {
            var r = MailInboxOpenValues.TryClaimAttachment();
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
            MailInboxUiLibrary.ApplyFullscreenShell(bg.GetComponent<Image>(), new Color(0.08f, 0.09f, 0.12f));

            BuildHeader();
            BuildMessageList(); BuildDetail();
        }

        private void BuildHeader()
        {
            GameObject topBar = new GameObject("MailInboxHeader", typeof(RectTransform), typeof(Image));
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
            GameObject builtCanvas = _canvasObj;
            backBtn.GetComponent<Button>().onClick.AddListener(() =>
            {
                // A Back tap on a canvas that was already torn down (double tap while a deferred Destroy
                // is pending, or a stale button from before a reopen) must not run Back again.
                if (_canvasObj == null || _canvasObj != builtCanvas) return;
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

            Text title = UISharedFoundation.CreateText(topBar.transform, "Title", "MAIL",
                UITextRole.Display, TextAnchor.MiddleCenter, Color.white, true,
                new Vector2(640f, 48f));
            title.fontSize = 30;
            title.fontStyle = FontStyle.Bold;
            SetNorm(title.rectTransform, 0.28f, 0.15f, 0.72f, 0.9f);

            Text wallet = UISharedFoundation.CreateText(topBar.transform, "WalletLine",
                MetagameShellProfileBinding.WalletLine(), UITextRole.Caption, TextAnchor.MiddleLeft,
                Color.white, true, new Vector2(420f, 28f));
            wallet.fontSize = 22;
            wallet.fontStyle = FontStyle.Bold;
            SetNorm(wallet.rectTransform, 0.18f, 0.12f, 0.48f, 0.88f);

            _statusText = UISharedFoundation.CreateText(topBar.transform, "StatusLine", MailInboxOpenValues.PlayerStatus,
                UITextRole.Caption, TextAnchor.MiddleRight, Color.white, true,
                new Vector2(520f, 40f));
            _statusText.fontSize = 22;
            _statusText.fontStyle = FontStyle.Bold;
            SetNorm(_statusText.rectTransform, 0.72f, 0.1f, 0.98f, 0.9f);
        }

        private void BuildMessageList()
        {
            GameObject list = new GameObject("MessageList", typeof(RectTransform));
            list.transform.SetParent(_canvasObj.transform, false);
            SetNorm(list.GetComponent<RectTransform>(), 0.02f, 0.08f, 0.36f, 0.86f);
            for (int i = 0; i < MailInboxOpenValues.VisibleRowCount; i++)
            {
                int row = i;
                float h = 1f / MailInboxOpenValues.VisibleRowCount;
                GameObject well = new GameObject($"MailRow_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                well.transform.SetParent(list.transform, false);
                Image img = well.GetComponent<Image>();
                Button btn = well.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => SetStatus(MailInboxOpenValues.TrySelectMessage(row).Message));
                SetNorm(well.GetComponent<RectTransform>(), 0.04f, 1f - (i + 1) * h + 0.02f, 0.96f, 1f - i * h - 0.02f);
                // Applied AFTER final positioning - see EmpirePresenter's same fix for why.
                UISharedFoundation.ApplyFramedPanel(img, null,
                    UIFrozenTokens.ColorPanel, UIFrozenTokens.ColorHeader,
                    kind: UISharedFoundation.FramedPanelKind.ListRow);
                // Trimmed to the leading phrase only: the old
                // "Empty inbox - mail backend not live" wrapped to two lines (50px in a 28px
                // band) and failed the geometry gate - the trailing "mail backend not live" was
                // dev phrasing in a player-facing slot, same class as the StatusNote split.
                // "Empty inbox" is kept verbatim rather than reworded because MailShellTests
                // asserts on that exact phrase; rewording it to "No mail yet" broke that test.
                string subject = i == 0
                    ? "Empty inbox"
                    : MetagameShellProfileBinding.EmptyBackendLabel;
                Text subjectText = UISharedFoundation.CreateText(well.transform, "Subject", subject,
                    UITextRole.Caption, TextAnchor.MiddleLeft, new Color(0.9f, 0.88f, 0.75f), true, new Vector2(280f, 28f));
                SetNorm(subjectText.rectTransform, 0.05f, 0.1f, 0.95f, 0.9f);
            }
        }

        private void BuildDetail()
        {
            GameObject detail = new GameObject("MessageDetail", typeof(RectTransform), typeof(Image));
            detail.transform.SetParent(_canvasObj.transform, false);
            SetNorm(detail.GetComponent<RectTransform>(), 0.38f, 0.12f, 0.97f, 0.86f);
            Image detailImg = detail.GetComponent<Image>();
            UISharedFoundation.ApplyFramedPanel(detailImg, null,
                UIFrozenTokens.ColorPanel, UIFrozenTokens.ColorBackground);
            // NOT restored (CR-UI-SWEEP-RECON-002): BuildDetail() is called unconditionally from
            // BuildUI() with no SetActive(false) gate, so this panel (and therefore this scrim)
            // is always visible, covering Btn_Back and every MailRow. Restoring it was MEASURED
            // to fail MailInboxLayoutTests.NeverDrawsArtOnTopOfAnInteractiveControl against all
            // 6 mail rows plus Btn_Back - a real tap-target defect, likely pre-existing under the
            // panel's own always-visible structure, not something this scrim alone should paper
            // over. Lane B's own escape clause applies; stays removed. The always-visible detail
            // panel itself is a separate, larger finding - not in this card's scope.
            Text body = UISharedFoundation.CreateText(detail.transform, "Body",
                "No messages.\nClaim attachment stays OPEN until a mail backend exists.\n\n" +
                MetagameShellProfileBinding.WalletLine(),
                UITextRole.Body, TextAnchor.UpperLeft, Color.white, true, new Vector2(900f, 360f));
            body.fontSize = 22;
            body.fontStyle = FontStyle.Bold;
            SetNorm(body.rectTransform, 0.04f, 0.28f, 0.96f, 0.95f);
            GameObject claim = new GameObject("Btn_ClaimAttachment", typeof(RectTransform), typeof(Image), typeof(Button));
            claim.transform.SetParent(detail.transform, false);
            Image cImg = claim.GetComponent<Image>();
            HomeV3UiLibrary.ApplyPrimaryActionButton(claim.GetComponent<Button>(), cImg);
            claim.GetComponent<Button>().onClick.AddListener(() => SetStatus(MailInboxOpenValues.TryClaimAttachment().Message));
            SetNorm(claim.GetComponent<RectTransform>(), 0.55f, 0.04f, 0.96f, 0.18f);
            UISharedFoundation.CreateText(claim.transform, "Text", "CLAIM", UITextRole.Body,
                TextAnchor.MiddleCenter, Color.white, true, new Vector2(200f, 36f));
        }

        private void SetStatus(string message)
        {
            if (_statusText != null)
                // The OpenValues diagnostic is mapped to its short player-facing form:
                // ActionResult.Message carries the full StatusNote when values are not
                // locked, which overflows this band. Repointing only the initial
                // CreateText would leave the long string one click away from returning.
                _statusText.text = message == MailInboxOpenValues.StatusNote
                    ? MailInboxOpenValues.PlayerStatus
                    : (message ?? string.Empty);
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

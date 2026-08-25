using System;
using MyriadOfDragons.Metagame;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>VIP / Subscription V1 art shell. Subscribe/restore refuse while OpenValues stay OPEN.</summary>
    public class VipSubscriptionPresenter : MonoBehaviour
    {
        public const string CanvasName = "VipSubscriptionCanvas";

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

        public VipSubscriptionActionResult SubscribeForTests()
        {
            VipSubscriptionActionResult result = VipSubscriptionOpenValues.TrySubscribe();
            SetStatus(result.Message);
            return result;
        }

        public VipSubscriptionActionResult RestoreForTests()
        {
            VipSubscriptionActionResult result = VipSubscriptionOpenValues.TryRestore();
            SetStatus(result.Message);
            return result;
        }

        private void BuildUI()
        {
            TeardownUI();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();

            Canvas canvas = UISharedFoundation.CreateScreenCanvas(CanvasName, new Vector2(1920, 1080));
            _canvasObj = canvas.gameObject;
            canvas.sortingOrder = 12;

            // Opaque theme backing under preserveAspect shell — same letterbox fix as Bazaar/Chat.
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
            VipSubscriptionUiLibrary.ApplyFullscreenShell(bg.GetComponent<Image>(), shellFallback);

            BuildHeader();
            BuildIdentityColumn();
            BuildBenefitGrid();
            BuildMilestoneStrip();
            BuildActionBar();
        }

        private void BuildHeader()
        {
            GameObject topBar = new GameObject("VipHeader", typeof(RectTransform));
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

            Text title = UISharedFoundation.CreateText(topBar.transform, "Title", "VIP",
                UITextRole.Display, TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 0.82f), true,
                new Vector2(640f, 48f));
            title.fontSize = 30;
            SetNorm(title.rectTransform, 0.28f, 0.15f, 0.72f, 0.9f);

            _statusText = UISharedFoundation.CreateText(topBar.transform, "StatusLine", VipSubscriptionOpenValues.StatusNote,
                UITextRole.Caption, TextAnchor.MiddleRight, new Color(0.85f, 0.75f, 0.5f), true,
                new Vector2(520f, 40f));
            SetNorm(_statusText.rectTransform, 0.72f, 0.1f, 0.98f, 0.9f);
        }

        private void BuildIdentityColumn()
        {
            GameObject col = new GameObject("IdentityColumn", typeof(RectTransform));
            col.transform.SetParent(_canvasObj.transform, false);
            SetNorm(col.GetComponent<RectTransform>(), 0.02f, 0.16f, 0.26f, 0.86f);

            Text state = UISharedFoundation.CreateText(col.transform, "EntitlementState",
                $"Not subscribed · IAP {MetagameShellProfileBinding.OpenAmountLabel}",
                UITextRole.Title, TextAnchor.UpperLeft,
                new Color(0.95f, 0.9f, 0.79f), true, new Vector2(400f, 48f));
            SetNorm(state.rectTransform, 0.04f, 0.78f, 0.96f, 0.96f);

            Text desc = UISharedFoundation.CreateText(col.transform, "Description",
                "Convenience only — no combat power, exclusive progression, or economy grant.",
                UITextRole.Body, TextAnchor.UpperLeft, new Color(0.85f, 0.82f, 0.7f), true,
                new Vector2(400f, 120f));
            SetNorm(desc.rectTransform, 0.04f, 0.42f, 0.96f, 0.76f);

            for (int i = 0; i < VipSubscriptionOpenValues.StateSocketCount; i++)
            {
                float left = i / 3f;
                GameObject socket = new GameObject($"StateSocket_{i}", typeof(RectTransform), typeof(Image));
                socket.transform.SetParent(col.transform, false);
                Image img = socket.GetComponent<Image>();
                img.color = new Color(0.12f, 0.14f, 0.18f, 0.35f);
                img.raycastTarget = false;
                SetNorm(socket.GetComponent<RectTransform>(), left + 0.04f, 0.08f, left + 0.28f, 0.36f);
                // Atlas is Single-mode; equal-width runtime cells (first 3 = crown tiers).
                VipSubscriptionUiLibrary.ApplyAtlasIcon(socket.transform, "StateIcon",
                    VipSubscriptionUiLibrary.LoadStateAtlasCell(i), 0.08f, 0.08f, 0.92f, 0.92f);
            }
        }

        private void BuildBenefitGrid()
        {
            GameObject grid = new GameObject("BenefitGrid", typeof(RectTransform));
            grid.transform.SetParent(_canvasObj.transform, false);
            SetNorm(grid.GetComponent<RectTransform>(), 0.28f, 0.16f, 0.68f, 0.86f);

            for (int i = 0; i < VipSubscriptionOpenValues.BenefitWellCount; i++)
            {
                int col = i % 2;
                int row = i / 2;
                float cw = 0.5f;
                float rh = 1f / 3f;
                GameObject well = new GameObject($"BenefitWell_{i}", typeof(RectTransform), typeof(Image));
                well.transform.SetParent(grid.transform, false);
                Image img = well.GetComponent<Image>();
                img.color = new Color(0.1f, 0.12f, 0.16f, 0.28f);
                img.raycastTarget = false;
                SetNorm(well.GetComponent<RectTransform>(),
                    col * cw + 0.02f, 1f - (row + 1) * rh + 0.02f,
                    (col + 1) * cw - 0.02f, 1f - row * rh - 0.02f);
                // Decorative atlas cell in the well (cells 0–5); label stays readable below.
                VipSubscriptionUiLibrary.ApplyAtlasIcon(well.transform, "BenefitIcon",
                    VipSubscriptionUiLibrary.LoadStateAtlasCell(i), 0.18f, 0.38f, 0.82f, 0.92f);
                Text label = UISharedFoundation.CreateText(well.transform, "Label",
                    $"Benefit {i + 1} — convenience {MetagameShellProfileBinding.OpenAmountLabel}",
                    UITextRole.Caption, TextAnchor.MiddleCenter, new Color(0.9f, 0.88f, 0.75f), true,
                    new Vector2(220f, 36f));
                SetNorm(label.rectTransform, 0.06f, 0.06f, 0.94f, 0.34f);
            }
        }

        private void BuildMilestoneStrip()
        {
            GameObject strip = new GameObject("MilestoneStrip", typeof(RectTransform));
            strip.transform.SetParent(_canvasObj.transform, false);
            SetNorm(strip.GetComponent<RectTransform>(), 0.70f, 0.16f, 0.97f, 0.86f);
            Text label = UISharedFoundation.CreateText(strip.transform, "MilestoneStatus",
                $"Milestones {MetagameShellProfileBinding.OpenAmountLabel}\n\n" +
                $"{MetagameShellProfileBinding.SelfIdentityLine()}\n{MetagameShellProfileBinding.WalletLine()}\n\n" +
                "Prices/durations/store entitlements stay server-owned — no local purchase truth.",
                UITextRole.Body, TextAnchor.UpperLeft,
                new Color(0.9f, 0.88f, 0.75f), true, new Vector2(360f, 400f));
            SetNorm(label.rectTransform, 0.06f, 0.08f, 0.94f, 0.94f);
        }

        private void BuildActionBar()
        {
            GameObject bar = new GameObject("ActionBar", typeof(RectTransform));
            bar.transform.SetParent(_canvasObj.transform, false);
            SetNorm(bar.GetComponent<RectTransform>(), 0.02f, 0.04f, 0.98f, 0.14f);

            GameObject subscribe = new GameObject("Btn_Subscribe", typeof(RectTransform), typeof(Image), typeof(Button));
            subscribe.transform.SetParent(bar.transform, false);
            Image sImg = subscribe.GetComponent<Image>();
            HomeV3UiLibrary.ApplyNeutralActionButton(subscribe.GetComponent<Button>(), sImg, new Color(0.28f, 0.36f, 0.22f));
            subscribe.GetComponent<Button>().onClick.AddListener(() => SetStatus(VipSubscriptionOpenValues.TrySubscribe().Message));
            SetNorm(subscribe.GetComponent<RectTransform>(), 0.42f, 0.12f, 0.68f, 0.88f);
            UISharedFoundation.CreateText(subscribe.transform, "Text", "SUBSCRIBE", UITextRole.Body,
                TextAnchor.MiddleCenter, Color.white, true, new Vector2(240f, 36f));

            GameObject restore = new GameObject("Btn_Restore", typeof(RectTransform), typeof(Image), typeof(Button));
            restore.transform.SetParent(bar.transform, false);
            Image rImg = restore.GetComponent<Image>();
            HomeV3UiLibrary.ApplyNeutralActionButton(restore.GetComponent<Button>(), rImg, new Color(0.22f, 0.26f, 0.32f));
            restore.GetComponent<Button>().onClick.AddListener(() => SetStatus(VipSubscriptionOpenValues.TryRestore().Message));
            SetNorm(restore.GetComponent<RectTransform>(), 0.70f, 0.12f, 0.96f, 0.88f);
            UISharedFoundation.CreateText(restore.transform, "Text", "RESTORE", UITextRole.Body,
                TextAnchor.MiddleCenter, Color.white, true, new Vector2(200f, 36f));
        }

        private void SetStatus(string message)
        {
            if (_statusText != null)
                _statusText.text = message ?? string.Empty;
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

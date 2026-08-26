using System;
using MyriadOfDragons.Metagame;
using MyriadOfDragons.Save;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>VIP / Subscription V1 — real Gem entitlement + scheduled Stamina claims (LOCKED 2026-08-26).</summary>
    public class VipSubscriptionPresenter : MonoBehaviour
    {
        public const string CanvasName = "VipSubscriptionCanvas";

        private GameObject _canvasObj;
        private Action _onBack;
        private Text _statusText;
        private Text _entitlementStateText;
        private Text _milestoneText;

        public GameObject CanvasObjectForTests => _canvasObj;
        public string StatusTextForTests => _statusText != null ? _statusText.text : null;

        public void Initialize(Action onBack)
        {
            _onBack = onBack;
            BuildUI();
            // Land any due claims when opening the shell (same restore path).
            VipSubscriptionActionResult restore = VipSubscriptionOpenValues.TryRestore();
            if (restore.Status == VipSubscriptionActionStatus.Applied)
                SetStatus(restore.Message);
            RefreshEntitlementCopy();
        }

        public VipSubscriptionActionResult SubscribeForTests()
        {
            VipSubscriptionActionResult result = VipSubscriptionOpenValues.TrySubscribe();
            SetStatus(result.Message);
            RefreshEntitlementCopy();
            return result;
        }

        public VipSubscriptionActionResult SubscribePlanForTests(VipPlanKind plan)
        {
            VipSubscriptionActionResult result = VipSubscriptionOpenValues.TrySubscribe(plan);
            SetStatus(result.Message);
            RefreshEntitlementCopy();
            return result;
        }

        public VipSubscriptionActionResult RestoreForTests()
        {
            VipSubscriptionActionResult result = VipSubscriptionOpenValues.TryRestore();
            SetStatus(result.Message);
            RefreshEntitlementCopy();
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

            _entitlementStateText = UISharedFoundation.CreateText(col.transform, "EntitlementState",
                "Not subscribed",
                UITextRole.Title, TextAnchor.UpperLeft,
                new Color(0.95f, 0.9f, 0.79f), true, new Vector2(400f, 48f));
            SetNorm(_entitlementStateText.rectTransform, 0.04f, 0.78f, 0.96f, 0.96f);

            Text desc = UISharedFoundation.CreateText(col.transform, "Description",
                "Convenience only — scheduled Stamina claims. No combat power, deck power, or exclusive progression.",
                UITextRole.Body, TextAnchor.UpperLeft, new Color(0.85f, 0.82f, 0.7f), true,
                new Vector2(400f, 120f));
            SetNorm(desc.rectTransform, 0.04f, 0.42f, 0.96f, 0.76f);

            string[] planLabels =
            {
                $"W {VipSubscriptionOpenValues.WeeklyGemPrice}g",
                $"F {VipSubscriptionOpenValues.FortnightGemPrice}g",
                $"M {VipSubscriptionOpenValues.MonthlyGemPrice}g",
            };
            for (int i = 0; i < VipSubscriptionOpenValues.StateSocketCount; i++)
            {
                float left = i / 3f;
                int planIndex = i;
                GameObject socket = new GameObject($"StateSocket_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                socket.transform.SetParent(col.transform, false);
                Image img = socket.GetComponent<Image>();
                img.raycastTarget = true;
                SetNorm(socket.GetComponent<RectTransform>(), left + 0.04f, 0.08f, left + 0.28f, 0.36f);
                // Applied AFTER final positioning - see EmpirePresenter's same fix for why.
                UISharedFoundation.ApplyFramedPanel(img, null,
                    UIFrozenTokens.ColorPanel, UIFrozenTokens.ColorHeader);
                VipSubscriptionUiLibrary.ApplyAtlasIcon(socket.transform, "StateIcon",
                    VipSubscriptionUiLibrary.LoadStateAtlasCell(i), 0.08f, 0.08f, 0.92f, 0.92f);
                UISharedFoundation.CreateText(socket.transform, "PlanPrice", planLabels[i],
                    UITextRole.Caption, TextAnchor.LowerCenter, new Color(0.95f, 0.9f, 0.7f), true,
                    new Vector2(100f, 24f));
                socket.GetComponent<Button>().onClick.AddListener(() =>
                {
                    VipSubscriptionActionResult result = VipSubscriptionOpenValues.TrySubscribe((VipPlanKind)planIndex);
                    SetStatus(result.Message);
                    RefreshEntitlementCopy();
                });
            }
        }

        private void BuildBenefitGrid()
        {
            GameObject grid = new GameObject("BenefitGrid", typeof(RectTransform));
            grid.transform.SetParent(_canvasObj.transform, false);
            SetNorm(grid.GetComponent<RectTransform>(), 0.28f, 0.16f, 0.68f, 0.86f);

            string[] benefitLabels =
            {
                $"Weekly - 1x{ShopStaminaCatalog.StaminaGrantPerPotion} Stam",
                "Fortnight - 2 claims",
                "Monthly - 4 claims",
                $"Uses Shop {ShopStaminaCatalog.MaxPurchasesPerRollingDay}/24h slots",
                "Full Stam = forfeit",
                "No combat power",
            };

            for (int i = 0; i < VipSubscriptionOpenValues.BenefitWellCount; i++)
            {
                int col = i % 2;
                int row = i / 2;
                float cw = 0.5f;
                float rh = 1f / 3f;
                GameObject well = new GameObject($"BenefitWell_{i}", typeof(RectTransform), typeof(Image));
                well.transform.SetParent(grid.transform, false);
                Image img = well.GetComponent<Image>();
                img.raycastTarget = false;
                SetNorm(well.GetComponent<RectTransform>(),
                    col * cw + 0.02f, 1f - (row + 1) * rh + 0.02f,
                    (col + 1) * cw - 0.02f, 1f - row * rh - 0.02f);
                // Applied AFTER final positioning - see EmpirePresenter's same fix for why.
                UISharedFoundation.ApplyFramedPanel(img, null,
                    UIFrozenTokens.ColorPanel, UIFrozenTokens.ColorBackground);
                VipSubscriptionUiLibrary.ApplyAtlasIcon(well.transform, "BenefitIcon",
                    VipSubscriptionUiLibrary.LoadStateAtlasCell(i), 0.18f, 0.38f, 0.82f, 0.92f);
                Text label = UISharedFoundation.CreateText(well.transform, "Label",
                    benefitLabels[i],
                    UITextRole.Caption, TextAnchor.MiddleCenter, Color.white, true,
                    new Vector2(220f, 36f));
                UISharedFoundation.ApplyTextShadow(label);
                SetNorm(label.rectTransform, 0.06f, 0.06f, 0.94f, 0.34f);
                // Benefit grid is 0.40×0.70 of 1920×1080; each well is half-col / third-row.
                const float wellW = 0.40f * 1920f * 0.5f;
                const float wellH = 0.70f * 1080f / 3f;
                UISharedFoundation.AddSemiTransparentScrimPanel(
                    well.transform,
                    new Vector2(wellW * 0.5f, wellH * 0.20f),
                    new Vector2(wellW * 0.9f, wellH * 0.32f),
                    UIDesignTokens.FrameTier.Tier2Section);
            }
        }

        private void BuildMilestoneStrip()
        {
            GameObject strip = new GameObject("MilestoneStrip", typeof(RectTransform));
            strip.transform.SetParent(_canvasObj.transform, false);
            SetNorm(strip.GetComponent<RectTransform>(), 0.70f, 0.16f, 0.97f, 0.86f);
            _milestoneText = UISharedFoundation.CreateText(strip.transform, "MilestoneStatus",
                BuildMilestoneCopy(),
                UITextRole.Body, TextAnchor.UpperLeft,
                new Color(0.9f, 0.88f, 0.75f), true, new Vector2(360f, 400f));
            SetNorm(_milestoneText.rectTransform, 0.06f, 0.08f, 0.94f, 0.94f);
        }

        private void BuildActionBar()
        {
            GameObject bar = new GameObject("ActionBar", typeof(RectTransform));
            bar.transform.SetParent(_canvasObj.transform, false);
            SetNorm(bar.GetComponent<RectTransform>(), 0.02f, 0.04f, 0.98f, 0.14f);

            GameObject subscribe = new GameObject("Btn_Subscribe", typeof(RectTransform), typeof(Image), typeof(Button));
            subscribe.transform.SetParent(bar.transform, false);
            Image sImg = subscribe.GetComponent<Image>();
            HomeV3UiLibrary.ApplyPrimaryActionButton(subscribe.GetComponent<Button>(), sImg);
            subscribe.GetComponent<Button>().onClick.AddListener(() =>
            {
                VipSubscriptionActionResult result = VipSubscriptionOpenValues.TrySubscribe(VipPlanKind.Weekly);
                SetStatus(result.Message);
                RefreshEntitlementCopy();
            });
            SetNorm(subscribe.GetComponent<RectTransform>(), 0.42f, 0.12f, 0.68f, 0.88f);
            UISharedFoundation.CreateText(subscribe.transform, "Text", "SUBSCRIBE", UITextRole.Body,
                TextAnchor.MiddleCenter, Color.white, true, new Vector2(240f, 36f));

            GameObject restore = new GameObject("Btn_Restore", typeof(RectTransform), typeof(Image), typeof(Button));
            restore.transform.SetParent(bar.transform, false);
            Image rImg = restore.GetComponent<Image>();
            HomeV3UiLibrary.ApplyNeutralActionButton(restore.GetComponent<Button>(), rImg, new Color(0.22f, 0.26f, 0.32f));
            restore.GetComponent<Button>().onClick.AddListener(() =>
            {
                VipSubscriptionActionResult result = VipSubscriptionOpenValues.TryRestore();
                SetStatus(result.Message);
                RefreshEntitlementCopy();
            });
            SetNorm(restore.GetComponent<RectTransform>(), 0.70f, 0.12f, 0.96f, 0.88f);
            UISharedFoundation.CreateText(restore.transform, "Text", "RESTORE", UITextRole.Body,
                TextAnchor.MiddleCenter, Color.white, true, new Vector2(200f, 36f));
        }

        private void RefreshEntitlementCopy()
        {
            if (_entitlementStateText != null)
                _entitlementStateText.text = BuildEntitlementStateLine();
            if (_milestoneText != null)
                _milestoneText.text = BuildMilestoneCopy();
        }

        private static string BuildEntitlementStateLine()
        {
            var profile = SaveSystem.CurrentProfile;
            long now = VipSubscriptionOpenValues.NowUtcTicks();
            if (!VipSubscriptionOpenValues.IsSubscriptionActive(profile, now))
                return "Not subscribed";
            VipSubscriptionOpenValues.TryParsePlanId(profile.vipPlanId, out VipPlanKind plan);
            int max = VipSubscriptionOpenValues.PlanMaxClaims[(int)plan];
            return $"Active - {VipSubscriptionOpenValues.PlanDisplayName(plan)} - claims {profile.vipClaimsConsumed}/{max}";
        }

        private static string BuildMilestoneCopy()
        {
            return
                "VIP plans (Gem)\n" +
                $"Weekly {VipSubscriptionOpenValues.WeeklyGemPrice} / 7d - 1 claim\n" +
                $"Fortnight {VipSubscriptionOpenValues.FortnightGemPrice} / 14d - 2 claims\n" +
                $"Monthly {VipSubscriptionOpenValues.MonthlyGemPrice} / 30d - 4 claims\n\n" +
                $"{MetagameShellProfileBinding.SelfIdentityLine()}\n{MetagameShellProfileBinding.WalletLine()}\n\n" +
                BuildEntitlementStateLine();
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

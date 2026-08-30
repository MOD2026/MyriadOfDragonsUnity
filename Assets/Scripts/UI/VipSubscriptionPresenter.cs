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
            GameObject topBar = new GameObject("VipHeader", typeof(RectTransform), typeof(Image));
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

            Text title = UISharedFoundation.CreateText(topBar.transform, "Title", "VIP",
                UITextRole.Display, TextAnchor.MiddleCenter, Color.white, true,
                new Vector2(640f, 48f));
            title.fontSize = 30;
            title.fontStyle = FontStyle.Bold;
            SetNorm(title.rectTransform, 0.28f, 0.15f, 0.72f, 0.9f);

            _statusText = UISharedFoundation.CreateText(topBar.transform, "StatusLine", VipSubscriptionOpenValues.PlayerStatus,
                UITextRole.Caption, TextAnchor.MiddleRight, Color.white, true,
                new Vector2(520f, 40f));
            _statusText.fontSize = 22;
            _statusText.fontStyle = FontStyle.Bold;
            SetNorm(_statusText.rectTransform, 0.72f, 0.1f, 0.98f, 0.9f);
        }

        private void BuildIdentityColumn()
        {
            GameObject col = new GameObject("IdentityColumn", typeof(RectTransform), typeof(Image));
            col.transform.SetParent(_canvasObj.transform, false);
            SetNorm(col.GetComponent<RectTransform>(), 0.02f, 0.16f, 0.26f, 0.86f);
            Image colImg = col.GetComponent<Image>();
            UISharedFoundation.ApplyFramedPanel(colImg, null,
                UIFrozenTokens.ColorPanel, UIFrozenTokens.ColorBackground);
            // VS-UI-VIP-RESIDUAL-SCRIM-ALIGN-001: the 400x750 literal only matched the column at
            // 1920x1080 (real size 460.8x756) and overhung it wherever the canvas resolves smaller
            // (EditMode's unscaled default), the same "literal vs. rect.size" bug BuildBenefitGrid
            // already documents. Measure the real column after SetNorm instead.
            Vector2 colSize = col.GetComponent<RectTransform>().rect.size;
            UISharedFoundation.AddLocalGradientScrim(col.transform, Vector2.zero, colSize, UISharedFoundation.GradientDirection.TopToBottom, 0.95f);

            _entitlementStateText = UISharedFoundation.CreateText(col.transform, "EntitlementState",
                "Not subscribed",
                UITextRole.Title, TextAnchor.UpperLeft,
                Color.white, true, new Vector2(400f, 48f));
            _entitlementStateText.fontSize = 22;
            _entitlementStateText.fontStyle = FontStyle.Bold;
            SetNorm(_entitlementStateText.rectTransform, 0.04f, 0.78f, 0.96f, 0.96f);

            Text desc = UISharedFoundation.CreateText(col.transform, "Description",
                "Convenience only — scheduled Stamina claims. No combat power, deck power, or exclusive progression.",
                UITextRole.Body, TextAnchor.UpperLeft, Color.white, true,
                new Vector2(400f, 120f));
            desc.fontSize = 22;
            desc.fontStyle = FontStyle.Bold;
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
                    VipSubscriptionUiLibrary.LoadPlanSocketSprite(i), 0.08f, 0.28f, 0.92f, 0.92f);
                // Same fix as the identity column above: measure the socket's own resolved rect
                // instead of a literal sized for one canvas resolution.
                Vector2 socketSize = socket.GetComponent<RectTransform>().rect.size;
                UISharedFoundation.AddLocalGradientScrim(socket.transform, Vector2.zero, socketSize, UISharedFoundation.GradientDirection.BottomToTop, 0.95f);
                Text planText = UISharedFoundation.CreateText(socket.transform, "PlanPrice", planLabels[i],
                    UITextRole.Caption, TextAnchor.LowerCenter, Color.white, true,
                    new Vector2(100f, 30f));
                planText.fontSize = 22;
                planText.fontStyle = FontStyle.Bold;
                UISharedFoundation.ApplyTextShadow(planText);
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
            // Grid band kept as named fractions so the well-size math below cannot drift out of
            // sync with the anchors the wells are actually built from.
            const float gridLeft = 0.28f, gridBottom = 0.16f, gridRight = 0.68f, gridTop = 0.86f;
            SetNorm(grid.GetComponent<RectTransform>(), gridLeft, gridBottom, gridRight, gridTop);

            // The six canonical icon roles, in the locked cell order (cells 0-5). Role names only:
            // each well label is ~338x70px, so the full approved restriction sentences live in the
            // MilestoneStrip panel, where they fit without clipping. Nothing here is invented - the
            // role names are the approved set and the sentences below are verbatim ST copy.
            string[] benefitLabels =
            {
                "Claim",
                "Duration",
                "Schedule",
                "Shop slot",
                "Full Stamina",
                "Value",
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
                // One set of fractions, used for BOTH the anchors and the scrim size below. The old
                // code recomputed the size as a bare half-column/third-row and silently dropped
                // these +-0.02 margins, which is half of why the scrims were mis-sized.
                float wellLeft = col * cw + 0.02f;
                float wellBottom = 1f - (row + 1) * rh + 0.02f;
                float wellRight = (col + 1) * cw - 0.02f;
                float wellTop = 1f - row * rh - 0.02f;
                SetNorm(well.GetComponent<RectTransform>(), wellLeft, wellBottom, wellRight, wellTop);
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
                // Scrim geometry, corrected on two counts.
                //
                // SIZE: measure the well, never assume it. The old literals were
                // 0.40f * 1920f * 0.5f = 384 x 252 - a bare half-column/third-row of the grid band
                // that silently ignored the +-0.02 well margins above. Design-space pixel math is
                // wrong here for a second reason too: the canvas only resolves to 1920x1080 when a
                // real screen exists, so the same literals describe a well three times too large
                // wherever the canvas resolves smaller (measured in EditMode: wells are 117.76 x
                // 98.56 against an unscaled 640x480 canvas). rect.size tracks whatever the canvas
                // actually is, so the scrim stays proportional in both cases - the "measure real
                // geometry, do not copy a literal" pattern AddLocalGradientScrim documents.
                //
                // OFFSET: was (wellW * 0.5f, wellH * 0.20f), written as if measured from the well's
                // corner. AddLocalGradientScrim point-anchors the scrim at (0.5,0.5) inside a
                // ScrimLayer stretched to the parent, so an offset is CENTRE-relative: it pushed
                // each scrim's centre clear of its own well and hung half of it over the
                // neighbouring controls. Vector2.zero is the centred position.
                Vector2 wellSize = well.GetComponent<RectTransform>().rect.size;
                UISharedFoundation.AddLocalGradientScrim(
                    well.transform,
                    Vector2.zero,
                    new Vector2(wellSize.x * 0.9f, wellSize.y * 0.32f),
                    UISharedFoundation.GradientDirection.TopToBottom, 0.95f);
                UISharedFoundation.AddLocalGradientScrim(
                    well.transform,
                    Vector2.zero,
                    new Vector2(wellSize.x * 0.9f, wellSize.y * 0.32f),
                    UISharedFoundation.GradientDirection.BottomToTop, 0.95f);
            }
        }

        private void BuildMilestoneStrip()
        {
            GameObject strip = new GameObject("MilestoneStrip", typeof(RectTransform), typeof(Image));
            strip.transform.SetParent(_canvasObj.transform, false);
            SetNorm(strip.GetComponent<RectTransform>(), 0.70f, 0.16f, 0.97f, 0.86f);
            Image stripImg = strip.GetComponent<Image>();
            UISharedFoundation.ApplyFramedPanel(stripImg, null,
                UIFrozenTokens.ColorPanel, UIFrozenTokens.ColorBackground);
            // Measured, not literal - real strip is 518.4x756 at 1920x1080 (0.27 x 0.70 of the
            // canvas), not the 500x750 the old literal assumed.
            Vector2 stripSize = strip.GetComponent<RectTransform>().rect.size;
            UISharedFoundation.AddLocalGradientScrim(strip.transform, Vector2.zero, stripSize, UISharedFoundation.GradientDirection.TopToBottom, 0.95f);

            _milestoneText = UISharedFoundation.CreateText(strip.transform, "MilestoneStatus",
                BuildMilestoneCopy(),
                UITextRole.Body, TextAnchor.UpperLeft,
                Color.white, true, new Vector2(360f, 400f));
            _milestoneText.fontSize = 22;
            _milestoneText.fontStyle = FontStyle.Bold;
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
            // Approved CTA pair is SUBSCRIBE / RESTORE PURCHASE. Preferred width widened with the
            // longer label so it cannot clip inside the same button rect.
            UISharedFoundation.CreateText(restore.transform, "Text", "RESTORE PURCHASE", UITextRole.Body,
                TextAnchor.MiddleCenter, Color.white, true, new Vector2(260f, 36f));
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
                "\n" + ValueLine + "\n\n" +
                RestrictionsCopy + "\n\n" +
                BuildEntitlementStateLine();
        }

        /// <summary>
        /// Exact approved value wording. Convenience framing only - no discount badge and no
        /// positive savings percentage is permitted at these prices.
        /// </summary>
        internal const string ValueLine =
            "Shop-equivalent value: 30 Gems per claim. No verified saving.";

        /// <summary>Approved shared restrictions, verbatim - not paraphrased, not abbreviated.</summary>
        internal const string RestrictionsCopy =
            "No combat power, deck power, or exclusive progression.\n" +
            "Each claim uses one Shop Stamina refill slot. If the 4-per-24h cap is reached, the claim waits.\n" +
            "If Stamina is full, the 50-Stamina claim is forfeited, but the claim and Shop slot are consumed.\n" +
            "One active plan only; plans do not stack. Unused claims expire when the plan lapses.";

        private void SetStatus(string message)
        {
            if (_statusText != null)
                // The OpenValues diagnostic is mapped to its short player-facing form:
                // ActionResult.Message carries the full StatusNote when values are not
                // locked, which overflows this band. Repointing only the initial
                // CreateText would leave the long string one click away from returning.
                _statusText.text = message == VipSubscriptionOpenValues.StatusNote
                    ? VipSubscriptionOpenValues.PlayerStatus
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

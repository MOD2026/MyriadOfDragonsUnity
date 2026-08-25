using System;
using System.Collections.Generic;
using MyriadOfDragons.Data;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Metagame;
using MyriadOfDragons.Save;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Empire Expedition rotation screen — metagame wrapper only (LOCKED 2026-08-24 structure).
    /// Same procedural uGUI pattern as CampaignMapPresenter, separate stage list. Does not launch
    /// combat / Auto-Fight (Battle seat). Clear settlement goes through
    /// <see cref="EmpireExpeditionClearTransaction"/> when numbers are locked; until then the
    /// screen shows OPEN-value status and refuses live clears.
    /// </summary>
    public class EmpireExpeditionPresenter : MonoBehaviour
    {
        public const string CanvasName = "EmpireExpeditionCanvas";

        private GameObject _canvasObj;
        private Action _onBack;
        private IGuildExpeditionBonusQuery _guildBonusQuery;
        private RetentionTelemetryOutbox _telemetryOutbox;
        private Text _statusText;
        private Text _guildBonusText;
        private bool _autoFightToggle;

        public GameObject CanvasObjectForTests => _canvasObj;
        public bool AutoFightEnabledForTests => _autoFightToggle;
        public string StatusTextForTests => _statusText != null ? _statusText.text : null;
        public string GuildBonusTextForTests => _guildBonusText != null ? _guildBonusText.text : null;
        public RetentionTelemetryOutbox TelemetryOutboxForTests => _telemetryOutbox;

        public void Initialize(Action onBack, IGuildExpeditionBonusQuery guildBonusQuery = null, RetentionTelemetryOutbox telemetryOutbox = null)
        {
            _onBack = onBack;
            _guildBonusQuery = guildBonusQuery ?? UnavailableGuildExpeditionBonusQuery.Instance;
            _telemetryOutbox = telemetryOutbox ?? new RetentionTelemetryOutbox(new UnityCloudCodeRetentionTelemetryGateway());
            _autoFightToggle = false;
            BuildUI();
        }

        public void BuildUIForTests() => BuildUI();

        /// <summary>Simulates a clear settlement without combat — for EditMode / shell wiring only.</summary>
        public EmpireExpeditionClearResult SimulateClearForTests(string stageId) =>
            AttemptClearSettlement(stageId);

        /// <summary>Same shape as EmpireExpeditionClearTransaction.TryApplyClearWithConfiguredAmountsForTests
        /// - lets a test reach the real "Applied" path (and therefore the real telemetry emission
        /// in EmitClearTelemetry) through the presenter itself, without needing
        /// EmpireExpeditionOpenValues locked. Production behavior is unaffected - this is a new
        /// test-only entry point, not a change to the real button's own call path.</summary>
        public EmpireExpeditionClearResult SimulateClearWithConfiguredAmountsForTests(
            string stageId, int staminaCost, int baseGold, int baseMaterials, int dailyGoldCap,
            int? dailyAttemptCap = null, int expeditionGoldEarnedTodayUtc = 0, int expeditionAttemptsTodayUtc = 0)
        {
            PlayerProfile profile = SaveSystem.CurrentProfile ?? SaveManager.SaveData;
            EmpireExpeditionClearResult result = EmpireExpeditionClearTransaction.TryApplyClearWithConfiguredAmountsForTests(
                profile, stageId, _guildBonusQuery, staminaCost, baseGold, baseMaterials, dailyGoldCap,
                dailyAttemptCap, expeditionGoldEarnedTodayUtc, expeditionAttemptsTodayUtc, persist: false);
            EmitClearTelemetry(stageId, result);
            return result;
        }

        private void BuildUI()
        {
            TeardownUI();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();

            Canvas canvas = UISharedFoundation.CreateScreenCanvas(CanvasName, new Vector2(1920, 1080));
            _canvasObj = canvas.gameObject;

            UISharedFoundation.CreateFullscreenBackground(_canvasObj.transform,
                "UI/Backdrops/Zihan_City_NO NAMES", new Color(0.10f, 0.12f, 0.14f));

            BuildHeader();
            BuildStatusRail();
            BuildStageNodes();
        }

        private void BuildHeader()
        {
            GameObject topBar = new GameObject("ExpeditionHeader", typeof(RectTransform), typeof(Image));
            topBar.transform.SetParent(_canvasObj.transform, false);
            Image topBg = topBar.GetComponent<Image>();
            topBg.raycastTarget = false;
            topBg.color = new Color(0.06f, 0.08f, 0.10f, 0.92f);
            RectTransform topRect = topBar.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0f, 1f);
            topRect.anchorMax = Vector2.one;
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.sizeDelta = new Vector2(0f, 100f);

            GameObject backBtn = new GameObject("Btn_Back", typeof(RectTransform), typeof(Image), typeof(Button));
            backBtn.transform.SetParent(topBar.transform, false);
            Image backImg = backBtn.GetComponent<Image>();
            backImg.color = new Color(0.3f, 0.2f, 0.2f);
            Button back = backBtn.GetComponent<Button>();
            HomeV3UiLibrary.ApplyNavTileButton(back, backImg);
            back.targetGraphic = backImg;
            back.onClick.AddListener(() =>
            {
                TeardownUI();
                _onBack?.Invoke();
            });
            RectTransform backRect = backBtn.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0f, 0.5f);
            backRect.anchorMax = new Vector2(0f, 0.5f);
            backRect.pivot = new Vector2(0f, 0.5f);
            backRect.anchoredPosition = new Vector2(30f, 0f);
            backRect.sizeDelta = new Vector2(160f, 60f);
            UISharedFoundation.CreateText(backBtn.transform, "Text", "< BACK", UITextRole.Body,
                TextAnchor.MiddleCenter, Color.white, true, new Vector2(140f, 50f));

            UISharedFoundation.CreateText(topBar.transform, "Title", "EMPIRE EXPEDITION", UITextRole.Display,
                TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 0.82f), true, new Vector2(900f, 60f)).fontSize = 30;

            GameObject autoBtnObj = new GameObject("Btn_AutoFight", typeof(RectTransform), typeof(Image), typeof(Button));
            autoBtnObj.transform.SetParent(topBar.transform, false);
            Image autoImg = autoBtnObj.GetComponent<Image>();
            autoImg.color = new Color(0.18f, 0.32f, 0.28f);
            Button autoBtn = autoBtnObj.GetComponent<Button>();
            HomeV3UiLibrary.ApplyNavTileButton(autoBtn, autoImg);
            autoBtn.targetGraphic = autoImg;
            Text autoLabel = UISharedFoundation.CreateText(autoBtnObj.transform, "Text", "AUTO-FIGHT: OFF",
                UITextRole.Body, TextAnchor.MiddleCenter, Color.white, true, new Vector2(220f, 40f));
            autoBtn.onClick.AddListener(() =>
            {
                _autoFightToggle = !_autoFightToggle;
                autoLabel.text = _autoFightToggle ? "AUTO-FIGHT: ON" : "AUTO-FIGHT: OFF";
                SetStatus(_autoFightToggle
                    ? "Auto-Fight armed (Battle seat resolves combat later — same reward path as manual)."
                    : "Auto-Fight off — manual play stays stronger per lock.");
            });
            RectTransform autoRect = autoBtnObj.GetComponent<RectTransform>();
            autoRect.anchorMin = new Vector2(1f, 0.5f);
            autoRect.anchorMax = new Vector2(1f, 0.5f);
            autoRect.pivot = new Vector2(1f, 0.5f);
            autoRect.anchoredPosition = new Vector2(-30f, 0f);
            autoRect.sizeDelta = new Vector2(240f, 56f);
        }

        private void BuildStatusRail()
        {
            GameObject rail = new GameObject("StatusRail", typeof(RectTransform), typeof(Image));
            rail.transform.SetParent(_canvasObj.transform, false);
            rail.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.09f, 0.9f);
            rail.GetComponent<Image>().raycastTarget = false;
            RectTransform railRect = rail.GetComponent<RectTransform>();
            railRect.anchorMin = new Vector2(0.04f, 0.80f);
            railRect.anchorMax = new Vector2(0.96f, 0.88f);
            railRect.offsetMin = Vector2.zero;
            railRect.offsetMax = Vector2.zero;

            _guildBonusText = UISharedFoundation.CreateText(rail.transform, "GuildBonusLine",
                EmpireExpeditionClearTransaction.FormatGuildBonusDisplayLine(_guildBonusQuery),
                UITextRole.Body, TextAnchor.MiddleLeft, new Color(0.75f, 0.88f, 0.7f), true, new Vector2(1600f, 36f));
            _guildBonusText.fontSize = 20;
            RectTransform guildRect = _guildBonusText.rectTransform;
            guildRect.anchorMin = new Vector2(0.02f, 0.55f);
            guildRect.anchorMax = new Vector2(0.98f, 0.95f);
            guildRect.offsetMin = Vector2.zero;
            guildRect.offsetMax = Vector2.zero;

            _statusText = UISharedFoundation.CreateText(rail.transform, "StatusLine",
                EmpireExpeditionOpenValues.StatusNote,
                UITextRole.Body, TextAnchor.MiddleLeft, new Color(0.9f, 0.82f, 0.64f), true, new Vector2(1600f, 36f));
            _statusText.fontSize = 18;
            RectTransform statusRect = _statusText.rectTransform;
            statusRect.anchorMin = new Vector2(0.02f, 0.05f);
            // 0.50 -> 0.55: measured 2026-08-25, StatusNote wraps to two 18pt lines needing 41.0px
            // but the 0.05-0.50 band is only 38.88px (rail is 0.80-0.88 = 86.4px at 1080), so it
            // bled 2.1px. There was already a 4.32px dead gap between this band's top (0.50) and
            // GuildBonusLine's bottom (0.55), so this consumes dead space rather than taking room
            // from a neighbour - the band becomes 43.2px and GuildBonusLine is untouched.
            statusRect.anchorMax = new Vector2(0.98f, 0.55f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;
        }

        private void BuildStageNodes()
        {
            GameObject scrollRoot = new GameObject("StageScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollRoot.transform.SetParent(_canvasObj.transform, false);
            RectTransform scrollRect = scrollRoot.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.04f, 0.05f);
            scrollRect.anchorMax = new Vector2(0.96f, 0.77f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;
            scrollRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.15f);

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
            viewport.transform.SetParent(scrollRoot.transform, false);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            Image viewportImage = viewport.GetComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
            viewportImage.raycastTarget = false;

            GameObject content = new GameObject("StageNodesContent", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0.5f);
            contentRect.anchorMax = new Vector2(0f, 0.5f);
            contentRect.pivot = new Vector2(0f, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;

            HorizontalLayoutGroup hlg = content.GetComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 28f;
            hlg.padding = new RectOffset(24, 24, 16, 16);
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            ScrollRect scroll = scrollRoot.GetComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            IReadOnlyList<EmpireExpeditionStageDefinition> stages =
                EmpireExpeditionCatalog.GetActiveRotationStages(DateTime.UtcNow);
            foreach (EmpireExpeditionStageDefinition stage in stages)
                CreateStageNode(content.transform, stage);
        }

        private void CreateStageNode(Transform parent, EmpireExpeditionStageDefinition stage)
        {
            GameObject nodeObj = new GameObject($"StageNode_{stage.StageId}", typeof(RectTransform), typeof(Image), typeof(Button));
            nodeObj.transform.SetParent(parent, false);
            Image nodeImg = nodeObj.GetComponent<Image>();
            nodeImg.color = new Color(0.22f, 0.38f, 0.32f, 1f);
            Button btn = nodeObj.GetComponent<Button>();
            btn.targetGraphic = nodeImg;
            string capturedId = stage.StageId;
            btn.onClick.AddListener(() => OnStageNodeClicked(capturedId));

            RectTransform rect = nodeObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(340f, 520f);
            var layout = nodeObj.AddComponent<LayoutElement>();
            layout.preferredWidth = 340f;
            layout.minHeight = 480f;
            layout.flexibleHeight = 1f;

            Text idText = UISharedFoundation.CreateText(nodeObj.transform, "StageId", stage.StageId, UITextRole.Body,
                TextAnchor.MiddleCenter, new Color(0.9f, 0.95f, 0.85f), true, new Vector2(220f, 40f));
            idText.fontSize = 28;
            SetNormalizedRect(idText.rectTransform, 0.08f, 0.72f, 0.92f, 0.94f);

            Text titleText = UISharedFoundation.CreateText(nodeObj.transform, "Title", stage.Title, UITextRole.Body,
                TextAnchor.MiddleCenter, Color.white, true, new Vector2(220f, 40f));
            titleText.fontSize = 22;
            titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetNormalizedRect(titleText.rectTransform, 0.08f, 0.36f, 0.92f, 0.70f);

            Text hintText = UISharedFoundation.CreateText(nodeObj.transform, "Hint", "CLEAR (shell)", UITextRole.Caption,
                TextAnchor.MiddleCenter, new Color(0.85f, 0.75f, 0.45f), true, new Vector2(220f, 30f));
            SetNormalizedRect(hintText.rectTransform, 0.08f, 0.08f, 0.92f, 0.30f);
        }

        private static void SetNormalizedRect(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void OnStageNodeClicked(string stageId)
        {
            EmpireExpeditionClearResult result = AttemptClearSettlement(stageId);
            SetStatus(result.Message ?? result.Status.ToString());
        }

        private EmpireExpeditionClearResult AttemptClearSettlement(string stageId)
        {
            PlayerProfile profile = SaveSystem.CurrentProfile ?? SaveManager.SaveData;
            EmpireExpeditionClearResult result = EmpireExpeditionClearTransaction.TryApplyClear(profile, stageId, _guildBonusQuery);
            EmitClearTelemetry(stageId, result);
            return result;
        }

        /// <summary>Real retention-telemetry trigger point (register: "Retention telemetry
        /// architecture - LOCKED" / dispatch "wire the actual emit calls into real gameplay call
        /// sites") - the single real settlement path both the production button handler
        /// (OnStageNodeClicked) and the EditMode test entry (SimulateClearForTests) go through.
        /// Enqueue never blocks/throws (RetentionTelemetryOutbox's own contract), so a telemetry
        /// failure can never affect the real clear result already returned above.</summary>
        private void EmitClearTelemetry(string stageId, EmpireExpeditionClearResult result)
        {
            if (_telemetryOutbox == null || result == null) return;
            string playerId = RetentionTelemetryPlayerId.CurrentOrEmpty();

            if (result.Status == EmpireExpeditionClearStatus.Applied)
            {
                _telemetryOutbox.Enqueue(RetentionTelemetryEvents.ModeRunCompleted(playerId, "empire_expedition", stageId, "success"));
                _telemetryOutbox.Enqueue(RetentionTelemetryEvents.ModeRewardClaimed(playerId, "empire_expedition", stageId,
                    result.MaterialsGranted > 0 ? "gold_and_materials" : "gold"));
            }
            else if (result.Status == EmpireExpeditionClearStatus.DailyGoldCapWouldReject
                     || result.Status == EmpireExpeditionClearStatus.DailyAttemptCapWouldReject)
            {
                _telemetryOutbox.Enqueue(RetentionTelemetryEvents.DailyCapReached(playerId, "empire_expedition", result.Status.ToString()));
            }

            _ = _telemetryOutbox.FlushAsync(System.Threading.CancellationToken.None);
        }

        private void SetStatus(string message)
        {
            if (_statusText != null)
                _statusText.text = message;
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

using System;
using MyriadOfDragons.Data;
using MyriadOfDragons.Metagame;
using MyriadOfDragons.Save;
using MyriadOfDragons.Season;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Daily Login + Daily Quests shell. Claims route through <see cref="DailyLoginQuestsService"/>.
    /// Locked copy: streak pauses and is not reset.
    /// </summary>
    public class DailyLoginQuestsPresenter : MonoBehaviour
    {
        public const string CanvasName = "DailyLoginQuestsCanvas";

        private GameObject _canvasObj;
        private Action _onBackToHome;
        private RetentionTelemetryOutbox _telemetryOutbox;
        private Text _statusText;
        private Text _walletText;
        private Text _clockText;
        private Text[] _loginRewardTexts;
        private Text[] _questCopyTexts;
        private Text[] _questProgressTexts;

        public GameObject CanvasObjectForTests => _canvasObj;
        public string StatusTextForTests => _statusText != null ? _statusText.text : null;
        public RetentionTelemetryOutbox TelemetryOutboxForTests => _telemetryOutbox;

        public void Initialize(Action onBackToHome, RetentionTelemetryOutbox telemetryOutbox = null)
        {
            _onBackToHome = onBackToHome;
            _telemetryOutbox = telemetryOutbox ?? new RetentionTelemetryOutbox(new UnityCloudCodeRetentionTelemetryGateway());
            BuildUI();
            RefreshBound();
        }

        public DailyLoginQuestClaimResult ClaimLoginForTests(int wellIndex) =>
            Apply(DailyLoginQuestsService.ClaimLogin(SaveManager.SaveData, DateTime.UtcNow));

        public DailyLoginQuestClaimResult ClaimQuestForTests(int questIndex) =>
            Apply(DailyLoginQuestsService.ClaimQuest(SaveManager.SaveData, questIndex, DateTime.UtcNow));

        /// <summary>Test seam: claim login at an injected UTC instant (streak / double-claim).</summary>
        public DailyLoginQuestClaimResult ClaimLoginAtUtcForTests(DateTime utcNow) =>
            Apply(DailyLoginQuestsService.ClaimLogin(SaveManager.SaveData, utcNow));

        public DailyLoginQuestClaimResult ClaimQuestAtUtcForTests(int questIndex, DateTime utcNow) =>
            Apply(DailyLoginQuestsService.ClaimQuest(SaveManager.SaveData, questIndex, utcNow));

        /// <summary>Real retention-telemetry trigger point (register: "Retention telemetry
        /// architecture - LOCKED" / dispatch "wire the actual emit calls into real gameplay call
        /// sites") - the single real completion path every login/quest claim (production button
        /// or EditMode test entry) goes through.</summary>
        private DailyLoginQuestClaimResult Apply(DailyLoginQuestClaimResult result)
        {
            RefreshBound();
            if (_statusText != null && !string.IsNullOrEmpty(result?.Message))
                _statusText.text = result.Message;

            if (_telemetryOutbox != null && result != null && result.Status == DailyLoginQuestClaimStatus.Applied)
            {
                string playerId = RetentionTelemetryPlayerId.CurrentOrEmpty();
                string outcome = FormatModeRewardOutcome(result);
                _telemetryOutbox.Enqueue(RetentionTelemetryEvents.ModeRewardClaimed(
                    playerId, "daily_login_quests", $"slot_{result.SlotIndex}", outcome));
                _ = _telemetryOutbox.FlushAsync(System.Threading.CancellationToken.None);
            }
            return result;
        }

        /// <summary>Builds the ModeRewardClaimed outcome string. Omits medals entirely when
        /// zeroed by the dormant event-ledger gate — "medals=0" is its own confusing lie.</summary>
        public static string FormatModeRewardOutcome(DailyLoginQuestClaimResult result)
        {
            if (result == null) return string.Empty;
            string outcome =
                $"gold={result.GoldGranted},materials={result.MaterialsGranted},stamina={result.StaminaGranted}";
            if (result.EventMedalsGranted > 0)
                outcome += $",medals={result.EventMedalsGranted}";
            outcome += $",xp={result.PassSeasonXpGranted}";
            return outcome;
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
            Image bgImg = bg.GetComponent<Image>();
            bgImg.raycastTarget = false;
            DailyLoginQuestsUiLibrary.ApplyFullscreenShell(bgImg, new Color(0.08f, 0.09f, 0.12f));

            BuildHeader();
            BuildLoginPanel();
            BuildQuestsPanel();
        }

        private void BuildHeader()
        {
            GameObject topBar = new GameObject("DailyLoginHeader", typeof(RectTransform));
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
                _onBackToHome?.Invoke();
            });
            RectTransform backRect = backBtn.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0f, 0.5f);
            backRect.anchorMax = new Vector2(0f, 0.5f);
            backRect.pivot = new Vector2(0f, 0.5f);
            backRect.anchoredPosition = new Vector2(30f, 0f);
            backRect.sizeDelta = new Vector2(160f, 56f);
            UISharedFoundation.CreateText(backBtn.transform, "Text", "< BACK", UITextRole.Body,
                TextAnchor.MiddleCenter, Color.white, true, new Vector2(140f, 44f));

            _clockText = UISharedFoundation.CreateText(topBar.transform, "ResetClock",
                MetagameShellProfileBinding.UtcDayKeyLine(), UITextRole.Body, TextAnchor.MiddleRight,
                new Color(0.85f, 0.82f, 0.7f), true, new Vector2(280f, 32f));
            SetNorm(_clockText.rectTransform, 0.72f, 0.2f, 0.97f, 0.8f);

            _walletText = UISharedFoundation.CreateText(topBar.transform, "WalletLine",
                MetagameShellProfileBinding.WalletLine(), UITextRole.Caption, TextAnchor.MiddleLeft,
                new Color(0.75f, 0.8f, 0.7f), true, new Vector2(520f, 28f));
            SetNorm(_walletText.rectTransform, 0.22f, 0.15f, 0.70f, 0.85f);
        }

        private void BuildLoginPanel()
        {
            GameObject panel = new GameObject("DailyLoginPanel", typeof(RectTransform));
            panel.transform.SetParent(_canvasObj.transform, false);
            SetNorm(panel.GetComponent<RectTransform>(), 0.04f, 0.08f, 0.48f, 0.88f);

            Text header = UISharedFoundation.CreateText(panel.transform, "Header", "DAILY LOGIN",
                UITextRole.Display, TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 0.82f), true,
                new Vector2(420f, 40f));
            header.fontSize = 28;
            SetNorm(header.rectTransform, 0.08f, 0.88f, 0.92f, 0.98f);

            Text streak = UISharedFoundation.CreateText(panel.transform, "StreakLabel", "STREAK",
                UITextRole.Title, TextAnchor.MiddleLeft, new Color(0.75f, 0.88f, 0.7f), true,
                new Vector2(200f, 28f));
            SetNorm(streak.rectTransform, 0.06f, 0.78f, 0.4f, 0.86f);

            GameObject nodes = new GameObject("StreakNodes", typeof(RectTransform));
            nodes.transform.SetParent(panel.transform, false);
            SetNorm(nodes.GetComponent<RectTransform>(), 0.04f, 0.28f, 0.96f, 0.76f);

            _loginRewardTexts = new Text[DailyLoginQuestsOpenValues.ShellLoginWellCount];
            float well = 1f / DailyLoginQuestsOpenValues.ShellLoginWellCount;
            for (int i = 0; i < DailyLoginQuestsOpenValues.ShellLoginWellCount; i++)
            {
                int day = i;
                GameObject node = new GameObject($"LoginWell_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                node.transform.SetParent(nodes.transform, false);
                Image img = node.GetComponent<Image>();
                Button btn = node.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => Apply(DailyLoginQuestsOpenValues.TryClaimLogin(day)));
                SetNorm(node.GetComponent<RectTransform>(), i * well + 0.01f, 0.1f, (i + 1) * well - 0.01f, 0.9f);
                // Applied AFTER final positioning - see EmpirePresenter's same fix for why.
                // ListRow, not the default ContentPanel - a real pre-existing oversight, only
                // just exposed once real art started loading (register 2026-08-26, 5724836): the
                // default ContentPanel kind adds a decorative DiamondOverlay child, appropriate
                // for a real content panel but not for this interactive login-claim button - every
                // other row/well ApplyFramedPanel call site in this project already passes
                // ListRow, this was the one that got missed.
                UISharedFoundation.ApplyFramedPanel(img, null,
                    UIFrozenTokens.ColorPanel, UIFrozenTokens.ColorHeader,
                    kind: UISharedFoundation.FramedPanelKind.ListRow);

                // Login panel 0.44×0.80 of canvas; streak nodes 0.92×0.48 of panel; 6 wells.
                float nodeW = (0.44f * 1920f) * 0.92f / Mathf.Max(1, DailyLoginQuestsOpenValues.ShellLoginWellCount);
                float nodeH = (0.80f * 1080f) * 0.48f * 0.8f;
                UISharedFoundation.AddSemiTransparentScrimPanel(
                    node.transform,
                    new Vector2(nodeW * 0.5f, nodeH * 0.84f),
                    new Vector2(nodeW * 0.9f, nodeH * 0.28f),
                    UIDesignTokens.FrameTier.Tier2Section);
                Text dayLabel = UISharedFoundation.CreateText(node.transform, "DayIndex", $"Day {i + 1}",
                    UITextRole.Caption, TextAnchor.UpperCenter, Color.white, true,
                    new Vector2(60f, 22f));
                UISharedFoundation.ApplyTextShadow(dayLabel);
                SetNorm(dayLabel.rectTransform, 0.05f, 0.7f, 0.95f, 0.98f);

                int gold = DailyLoginQuestsService.LoginGoldBase + i * DailyLoginQuestsService.LoginGoldPerTier;
                _loginRewardTexts[i] = UISharedFoundation.CreateText(node.transform, "RewardAmount",
                    $"{gold}g", UITextRole.Body, TextAnchor.MiddleCenter,
                    new Color(0.95f, 0.9f, 0.79f), true, new Vector2(80f, 24f));
                SetNorm(_loginRewardTexts[i].rectTransform, 0.05f, 0.08f, 0.95f, 0.45f);
            }

            GameObject statusBar = new GameObject("StreakStatusBar", typeof(RectTransform), typeof(Image));
            statusBar.transform.SetParent(panel.transform, false);
            statusBar.GetComponent<Image>().raycastTarget = false;
            SetNorm(statusBar.GetComponent<RectTransform>(), 0.06f, 0.06f, 0.94f, 0.22f);
            // Applied AFTER final positioning - see EmpirePresenter's same fix for why.
            UISharedFoundation.ApplyFramedPanel(statusBar.GetComponent<Image>(), null,
                UIFrozenTokens.ColorHeader, UIFrozenTokens.ColorBackground,
                kind: UISharedFoundation.FramedPanelKind.ListRow);

            _statusText = UISharedFoundation.CreateText(statusBar.transform, "StreakStatus",
                DailyLoginQuestsOpenValues.StreakPausedCopy, UITextRole.Title, TextAnchor.MiddleCenter,
                new Color(0.85f, 0.9f, 0.95f), true, new Vector2(700f, 36f));
            _statusText.fontSize = 18;
            SetNorm(_statusText.rectTransform, 0.04f, 0.1f, 0.96f, 0.9f);
        }

        private void BuildQuestsPanel()
        {
            GameObject panel = new GameObject("DailyQuestsPanel", typeof(RectTransform));
            panel.transform.SetParent(_canvasObj.transform, false);
            SetNorm(panel.GetComponent<RectTransform>(), 0.52f, 0.08f, 0.96f, 0.88f);

            Text header = UISharedFoundation.CreateText(panel.transform, "Header", "DAILY QUESTS",
                UITextRole.Display, TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 0.82f), true,
                new Vector2(420f, 40f));
            header.fontSize = 28;
            SetNorm(header.rectTransform, 0.08f, 0.88f, 0.92f, 0.98f);

            _questCopyTexts = new Text[DailyLoginQuestsOpenValues.DailyQuestSlots];
            _questProgressTexts = new Text[DailyLoginQuestsOpenValues.DailyQuestSlots];
            float rowH = 0.24f;
            for (int i = 0; i < DailyLoginQuestsOpenValues.DailyQuestSlots; i++)
            {
                int quest = i;
                float top = 0.84f - i * (rowH + 0.03f);
                GameObject row = new GameObject($"QuestRow_{i}", typeof(RectTransform), typeof(Image));
                row.transform.SetParent(panel.transform, false);
                row.GetComponent<Image>().raycastTarget = false;
                SetNorm(row.GetComponent<RectTransform>(), 0.04f, top - rowH, 0.96f, top);
                // Applied AFTER final positioning - see EmpirePresenter's same fix for why.
                UISharedFoundation.ApplyFramedPanel(row.GetComponent<Image>(), null,
                    UIFrozenTokens.ColorPanel, UIFrozenTokens.ColorBackground,
                    kind: UISharedFoundation.FramedPanelKind.ListRow);

                _questCopyTexts[i] = UISharedFoundation.CreateText(row.transform, "QuestCopy",
                    $"Quest {i + 1}", UITextRole.Body, TextAnchor.MiddleLeft,
                    new Color(0.95f, 0.9f, 0.79f), true, new Vector2(360f, 28f));
                SetNorm(_questCopyTexts[i].rectTransform, 0.16f, 0.55f, 0.58f, 0.92f);

                GameObject progress = new GameObject("ProgressBar", typeof(RectTransform), typeof(Image));
                progress.transform.SetParent(row.transform, false);
                progress.GetComponent<Image>().color = new Color(0.2f, 0.55f, 0.32f, 0.8f);
                progress.GetComponent<Image>().raycastTarget = false;
                SetNorm(progress.GetComponent<RectTransform>(), 0.16f, 0.18f, 0.52f, 0.48f);

                // Quest panel 0.44×0.80 of canvas; each row ~0.24 of panel height.
                float rowW = 0.44f * 1920f;
                float rowHeightPx = 0.80f * 1080f * 0.24f;
                UISharedFoundation.AddSemiTransparentScrimPanel(
                    row.transform,
                    new Vector2(rowW * 0.61f, rowHeightPx * 0.33f),
                    new Vector2(rowW * 0.16f, rowHeightPx * 0.36f),
                    UIDesignTokens.FrameTier.Tier2Section);
                _questProgressTexts[i] = UISharedFoundation.CreateText(row.transform, "ProgressCopy",
                    "0 / 1", UITextRole.Caption, TextAnchor.MiddleLeft,
                    Color.white, true, new Vector2(120f, 22f));
                UISharedFoundation.ApplyTextShadow(_questProgressTexts[i]);
                SetNorm(_questProgressTexts[i].rectTransform, 0.54f, 0.18f, 0.68f, 0.48f);

                GameObject claim = new GameObject("Btn_Claim", typeof(RectTransform), typeof(Image), typeof(Button));
                claim.transform.SetParent(row.transform, false);
                Image claimImg = claim.GetComponent<Image>();
                HomeV3UiLibrary.ApplyPrimaryActionButton(claim.GetComponent<Button>(), claimImg);
                claim.GetComponent<Button>().onClick.AddListener(() =>
                    Apply(DailyLoginQuestsOpenValues.TryClaimQuest(quest)));
                SetNorm(claim.GetComponent<RectTransform>(), 0.72f, 0.18f, 0.96f, 0.82f);
                UISharedFoundation.CreateText(claim.transform, "Text", "CLAIM",
                    UITextRole.Body, TextAnchor.MiddleCenter, Color.white, true, new Vector2(140f, 32f));
            }
        }

        private void RefreshBound()
        {
            PlayerProfile profile = SaveManager.SaveData;
            DateTime utc = DateTime.UtcNow;
            DailyLoginQuestsService.EnsureQuestDay(profile, utc);

            if (_statusText != null)
                _statusText.text = DailyLoginQuestsService.StatusCopy(profile, utc);
            if (_walletText != null)
                _walletText.text = MetagameShellProfileBinding.WalletLine();
            if (_clockText != null)
                _clockText.text = $"UTC reset {DailyLoginQuestsService.UtcDayKey(utc)}";

            if (_loginRewardTexts != null)
            {
                int currentTier = DailyLoginQuestsService.CurrentLoginTier(profile);
                for (int i = 0; i < _loginRewardTexts.Length; i++)
                {
                    if (_loginRewardTexts[i] == null) continue;
                    int gold = DailyLoginQuestsService.LoginGoldBase + i * DailyLoginQuestsService.LoginGoldPerTier;
                    string marker = i == currentTier ? "*" : "";
                    _loginRewardTexts[i].text = $"{gold}g{marker}";
                }
            }

            if (_questCopyTexts != null)
            {
                for (int i = 0; i < _questCopyTexts.Length; i++)
                {
                    DailyLoginQuestDefinition quest = DailyLoginQuestsService.GetQuest(profile, i, utc);
                    if (_questCopyTexts[i] != null)
                    {
                        string state = quest.IsClaimed ? "CLAIMED" : (quest.IsComplete ? "READY" : "IN PROGRESS");
                        _questCopyTexts[i].text = $"{quest.Title} — {state}";
                    }
                    if (_questProgressTexts[i] != null)
                        _questProgressTexts[i].text = $"{quest.Progress} / {quest.Target}";
                }
            }
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

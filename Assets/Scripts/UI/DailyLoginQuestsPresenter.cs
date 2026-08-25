using System;
using MyriadOfDragons.Season;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Daily Login + Daily Quests shell (V1 art). Claims refuse while reward amounts stay OPEN.
    /// Locked copy: streak pauses and is not reset.
    /// </summary>
    public class DailyLoginQuestsPresenter : MonoBehaviour
    {
        public const string CanvasName = "DailyLoginQuestsCanvas";

        private GameObject _canvasObj;
        private Action _onBackToHome;
        private Text _statusText;

        public GameObject CanvasObjectForTests => _canvasObj;
        public string StatusTextForTests => _statusText != null ? _statusText.text : null;

        public void Initialize(Action onBackToHome)
        {
            _onBackToHome = onBackToHome;
            BuildUI();
        }

        public DailyLoginQuestClaimResult ClaimLoginForTests(int wellIndex) =>
            Apply(DailyLoginQuestsOpenValues.TryClaimLogin(wellIndex));

        public DailyLoginQuestClaimResult ClaimQuestForTests(int questIndex) =>
            Apply(DailyLoginQuestsOpenValues.TryClaimQuest(questIndex));

        private DailyLoginQuestClaimResult Apply(DailyLoginQuestClaimResult result)
        {
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

            Text clock = UISharedFoundation.CreateText(topBar.transform, "ResetClock",
                MetagameShellProfileBinding.UtcDayKeyLine(), UITextRole.Body, TextAnchor.MiddleRight,
                new Color(0.85f, 0.82f, 0.7f), true, new Vector2(280f, 32f));
            SetNorm(clock.rectTransform, 0.72f, 0.2f, 0.97f, 0.8f);

            Text wallet = UISharedFoundation.CreateText(topBar.transform, "WalletLine",
                MetagameShellProfileBinding.WalletLine(), UITextRole.Caption, TextAnchor.MiddleLeft,
                new Color(0.75f, 0.8f, 0.7f), true, new Vector2(520f, 28f));
            SetNorm(wallet.rectTransform, 0.22f, 0.15f, 0.70f, 0.85f);
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

            float well = 1f / DailyLoginQuestsOpenValues.ShellLoginWellCount;
            for (int i = 0; i < DailyLoginQuestsOpenValues.ShellLoginWellCount; i++)
            {
                int day = i;
                GameObject node = new GameObject($"LoginWell_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                node.transform.SetParent(nodes.transform, false);
                Image img = node.GetComponent<Image>();
                img.color = new Color(0.14f, 0.22f, 0.2f, 0.4f);
                Button btn = node.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => Apply(DailyLoginQuestsOpenValues.TryClaimLogin(day)));
                SetNorm(node.GetComponent<RectTransform>(), i * well + 0.01f, 0.1f, (i + 1) * well - 0.01f, 0.9f);

                Text dayLabel = UISharedFoundation.CreateText(node.transform, "DayIndex", $"Day {i + 1}",
                    UITextRole.Caption, TextAnchor.UpperCenter, new Color(0.85f, 0.9f, 0.8f), true,
                    new Vector2(60f, 22f));
                SetNorm(dayLabel.rectTransform, 0.05f, 0.7f, 0.95f, 0.98f);

                Text reward = UISharedFoundation.CreateText(node.transform, "RewardAmount",
                    MetagameShellProfileBinding.OpenAmountLabel, UITextRole.Body, TextAnchor.MiddleCenter,
                    new Color(0.95f, 0.9f, 0.79f), true, new Vector2(80f, 24f));
                SetNorm(reward.rectTransform, 0.05f, 0.08f, 0.95f, 0.45f);
            }

            GameObject statusBar = new GameObject("StreakStatusBar", typeof(RectTransform), typeof(Image));
            statusBar.transform.SetParent(panel.transform, false);
            statusBar.GetComponent<Image>().color = new Color(0.12f, 0.18f, 0.22f, 0.55f);
            statusBar.GetComponent<Image>().raycastTarget = false;
            SetNorm(statusBar.GetComponent<RectTransform>(), 0.06f, 0.06f, 0.94f, 0.22f);

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

            float rowH = 0.24f;
            for (int i = 0; i < DailyLoginQuestsOpenValues.DailyQuestSlots; i++)
            {
                int quest = i;
                float top = 0.84f - i * (rowH + 0.03f);
                GameObject row = new GameObject($"QuestRow_{i}", typeof(RectTransform), typeof(Image));
                row.transform.SetParent(panel.transform, false);
                row.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.12f, 0.35f);
                row.GetComponent<Image>().raycastTarget = false;
                SetNorm(row.GetComponent<RectTransform>(), 0.04f, top - rowH, 0.96f, top);

                Text name = UISharedFoundation.CreateText(row.transform, "QuestCopy",
                    $"Quest {i + 1} — rewards OPEN", UITextRole.Body, TextAnchor.MiddleLeft,
                    new Color(0.95f, 0.9f, 0.79f), true, new Vector2(360f, 28f));
                SetNorm(name.rectTransform, 0.16f, 0.55f, 0.58f, 0.92f);

                GameObject progress = new GameObject("ProgressBar", typeof(RectTransform), typeof(Image));
                progress.transform.SetParent(row.transform, false);
                progress.GetComponent<Image>().color = new Color(0.2f, 0.55f, 0.32f, 0.8f);
                progress.GetComponent<Image>().raycastTarget = false;
                SetNorm(progress.GetComponent<RectTransform>(), 0.16f, 0.18f, 0.52f, 0.48f);

                Text progressCopy = UISharedFoundation.CreateText(row.transform, "ProgressCopy",
                    $"0 / {MetagameShellProfileBinding.EmptyBackendLabel}", UITextRole.Caption, TextAnchor.MiddleLeft,
                    new Color(0.8f, 0.85f, 0.7f), true, new Vector2(120f, 22f));
                SetNorm(progressCopy.rectTransform, 0.54f, 0.18f, 0.68f, 0.48f);

                GameObject claim = new GameObject("Btn_Claim", typeof(RectTransform), typeof(Image), typeof(Button));
                claim.transform.SetParent(row.transform, false);
                Image claimImg = claim.GetComponent<Image>();
                HomeV3UiLibrary.ApplyNeutralActionButton(claim.GetComponent<Button>(), claimImg,
                    new Color(0.16f, 0.4f, 0.28f));
                claim.GetComponent<Button>().onClick.AddListener(() =>
                    Apply(DailyLoginQuestsOpenValues.TryClaimQuest(quest)));
                SetNorm(claim.GetComponent<RectTransform>(), 0.72f, 0.18f, 0.96f, 0.82f);
                UISharedFoundation.CreateText(claim.transform, "Text", "CLAIM",
                    UITextRole.Body, TextAnchor.MiddleCenter, Color.white, true, new Vector2(140f, 32f));
            }
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

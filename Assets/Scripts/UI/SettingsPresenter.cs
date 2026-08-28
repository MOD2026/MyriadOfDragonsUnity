using System;
using System.Threading;
using System.Threading.Tasks;
using MyriadOfDragons.Data;
using MyriadOfDragons.Save;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>Utility Settings/Options screen — audio, notifications, language, logout.</summary>
    public class SettingsPresenter : MonoBehaviour
    {
        /// <summary>Open/close canvas fade duration (seconds). Reduced motion / EditMode snap to zero.</summary>
        public const float TransitionDurationSeconds = 0.18f;

        private GameObject _canvasObj;
        private CanvasGroup _canvasGroup;
        private Action _onBackToHome;
        private Action<string> _onLogoutCompleted;
        private CancellationTokenSource _transitionCts;
        private Task _openTransitionTask = Task.CompletedTask;
        private bool _backInFlight;
        private bool _backFadeCompletedBeforeTeardown;

        private Text _audioValueText;
        private Text _notificationsValueText;
        private Text _languageValueText;
        private Text _statusText;
        private readonly System.Collections.Generic.List<Button> _languageOptionButtons =
            new System.Collections.Generic.List<Button>();
        private readonly System.Collections.Generic.List<Text> _languageOptionLabels =
            new System.Collections.Generic.List<Text>();
        private readonly System.Collections.Generic.List<Image> _languageOptionImages =
            new System.Collections.Generic.List<Image>();

        public void Initialize(Action onBackToHome, Action<string> onLogoutCompleted = null)
        {
            _onBackToHome = onBackToHome;
            _onLogoutCompleted = onLogoutCompleted;
            BuildUI();
            RefreshAllRows();
        }

        public GameObject CanvasObjectForTests => _canvasObj;
        public float CanvasAlphaForTests => _canvasGroup != null ? _canvasGroup.alpha : -1f;
        public bool BackFadeCompletedBeforeTeardownForTests => _backFadeCompletedBeforeTeardown;
        public string StatusTextForTests => _statusText != null ? _statusText.text : null;

        /// <summary>Reduced motion collapses requested seconds to 0; otherwise preserves non-negative duration.</summary>
        public static float ResolveTransitionDurationSeconds(float requestedSeconds, bool reduceMotion) =>
            reduceMotion ? 0f : Mathf.Max(0f, requestedSeconds);

        public Task WaitForOpenTransitionForTests() => _openTransitionTask ?? Task.CompletedTask;

        public Task PressBackForTests() => HandleBackAsync();


        private void BuildUI()
        {
            TeardownUI();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();

            PlayerProfile profile = SaveManager.SaveData;
            if (profile != null)
                PlayerSettingsService.ApplyFromProfile(profile);

            Canvas canvas = UISharedFoundation.CreateScreenCanvas("SettingsCanvas", new Vector2(1920, 1080));
            _canvasObj = canvas.gameObject;

            UISharedFoundation.CreateFullscreenBackground(_canvasObj.transform,
                "UI/Backdrops/Zihan_City_NO NAMES", new Color(0.1f, 0.11f, 0.15f));

            BuildHeader();
            BuildForm();
            BeginOpenTransition();
        }

        private void BeginOpenTransition()
        {
            if (_canvasObj == null) return;
            _canvasGroup = _canvasObj.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = _canvasObj.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
            _openTransitionTask = FadeCanvasToAsync(1f);
        }

        private async Task HandleBackAsync()
        {
            if (_backInFlight) return;
            _backInFlight = true;
            _backFadeCompletedBeforeTeardown = false;
            try
            {
                await FadeCanvasToAsync(0f).ConfigureAwait(true);
                _backFadeCompletedBeforeTeardown =
                    _canvasObj != null && _canvasGroup != null && Mathf.Approximately(_canvasGroup.alpha, 0f);
                Action onBack = _onBackToHome;
                TeardownUI();
                onBack?.Invoke();
            }
            finally
            {
                _backInFlight = false;
            }
        }

        private async Task FadeCanvasToAsync(float targetAlpha)
        {
            CancelActiveTransition();
            if (_canvasGroup == null) return;

            _transitionCts = new CancellationTokenSource();
            CancellationToken ct = _transitionCts.Token;
            float duration = ResolveTransitionDurationSeconds(
                TransitionDurationSeconds, MotionPolicy.ReduceMotion);
            float startAlpha = _canvasGroup.alpha;

            // EditMode cannot drive frame-timed fades; reduced motion also snaps immediately.
            if (duration <= 0f || !Application.isPlaying)
            {
                if (!ct.IsCancellationRequested)
                    _canvasGroup.alpha = targetAlpha;
                return;
            }

            float elapsed = 0f;
            try
            {
                while (elapsed < duration)
                {
                    ct.ThrowIfCancellationRequested();
                    elapsed += Time.unscaledDeltaTime;
                    float u = Mathf.Clamp01(elapsed / duration);
                    _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, u);
                    await Task.Yield();
                }

                if (!ct.IsCancellationRequested)
                    _canvasGroup.alpha = targetAlpha;
            }
            catch (OperationCanceledException)
            {
                // Teardown / duplicate cancel — safe no-op.
            }
        }

        private void CancelActiveTransition()
        {
            if (_transitionCts == null) return;
            try { _transitionCts.Cancel(); }
            catch (ObjectDisposedException) { /* already disposed */ }
            _transitionCts.Dispose();
            _transitionCts = null;
        }

        private void BuildHeader()
        {
            GameObject topBar = new GameObject("SettingsHeader", typeof(RectTransform), typeof(Image));
            topBar.transform.SetParent(_canvasObj.transform, false);
            Image topBg = topBar.GetComponent<Image>();
            topBg.raycastTarget = false;
            topBg.color = new Color(0.06f, 0.06f, 0.1f, 0.92f);

            RectTransform topRect = topBar.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0f, 1f);
            topRect.anchorMax = Vector2.one;
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.sizeDelta = new Vector2(0f, 108f);

            // Title first (non-raycast) so it cannot steal taps from BACK on 1920x1080.
            Text title = UISharedFoundation.CreateText(topBar.transform, "Title", "SETTINGS & OPTIONS",
                UITextRole.Display, TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 0.82f), true,
                new Vector2(720f, 60f));
            title.fontSize = 32;
            title.raycastTarget = false;
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = Vector2.zero;

            // WH-UI-SETTINGS-USABILITY-001: larger landscape hit target; drawn last for input priority.
            Button backBtn = CreateHeaderButton(topBar.transform, "Btn_Back", "< BACK", new Vector2(24f, 0f),
                () => _ = HandleBackAsync());
            backBtn.transform.SetAsLastSibling();
        }

        private void BuildForm()
        {
            GameObject panel = new GameObject("SettingsBody", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(_canvasObj.transform, false);
            Image panelBg = panel.GetComponent<Image>();
            panelBg.raycastTarget = false;

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.05f, 0.06f);
            panelRect.anchorMax = new Vector2(0.95f, 0.88f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // Applied AFTER final positioning - see EmpirePresenter's same fix for why.
            UISharedFoundation.ApplyFramedPanel(panelBg, null,
                UIFrozenTokens.ColorPanel, UIFrozenTokens.ColorBackground);

            float y = 0.96f;
            const float toggleRowHeight = 0.12f;
            const float gap = 0.018f;

            CreateToggleRow(panel.transform, "AudioRow", "AUDIO", ref y, toggleRowHeight, gap, ToggleAudio, out _audioValueText);
            CreateToggleRow(panel.transform, "NotificationsRow", "NOTIFICATIONS", ref y, toggleRowHeight, gap,
                ToggleNotifications, out _notificationsValueText);
            CreateLanguageRow(panel.transform, ref y, gap);
            CreateLogoutRow(panel.transform, ref y, toggleRowHeight, gap);

            _statusText = UISharedFoundation.CreateText(panel.transform, "SettingsStatus", "",
                UITextRole.Body, TextAnchor.MiddleCenter, new Color(0.85f, 0.82f, 0.7f), true,
                new Vector2(900f, 36f));
            _statusText.fontSize = 22;
            _statusText.raycastTarget = false;
            SetNormalizedRect(_statusText.rectTransform, 0.04f, 0.02f, 0.96f, 0.09f);
        }

        private void CreateToggleRow(Transform parent, string rowName, string label, ref float yTop,
            float rowHeight, float gap, Action onToggle, out Text valueText)
        {
            GameObject row = new GameObject(rowName, typeof(RectTransform), typeof(Image));
            row.transform.SetParent(parent, false);
            Image rowBg = row.GetComponent<Image>();
            rowBg.raycastTarget = false;
            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.04f, yTop - rowHeight);
            rowRect.anchorMax = new Vector2(0.96f, yTop);
            rowRect.offsetMin = Vector2.zero;
            rowRect.offsetMax = Vector2.zero;
            yTop -= rowHeight + gap;
            // Applied AFTER final positioning - see EmpirePresenter's same fix for why.
            UISharedFoundation.ApplyFramedPanel(rowBg, null,
                UIFrozenTokens.ColorPanel, UIFrozenTokens.ColorHeader,
                kind: UISharedFoundation.FramedPanelKind.ListRow);

            Text labelText = UISharedFoundation.CreateText(row.transform, "Label", label, UITextRole.Title,
                TextAnchor.MiddleLeft, new Color(0.95f, 0.9f, 0.79f), true, new Vector2(420f, 40f));
            labelText.raycastTarget = false;
            SetNormalizedRect(labelText.rectTransform, 0.03f, 0.15f, 0.38f, 0.85f);

            valueText = UISharedFoundation.CreateText(row.transform, "Value", "", UITextRole.Body,
                TextAnchor.MiddleLeft, new Color(0.72f, 0.66f, 0.56f), true, new Vector2(220f, 36f));
            valueText.raycastTarget = false;
            SetNormalizedRect(valueText.rectTransform, 0.40f, 0.15f, 0.68f, 0.85f);

            Button toggleBtn = CreateRowButton(row.transform, "Btn_Toggle", "TOGGLE", new Vector2(-20f, 0f), onToggle);
            toggleBtn.GetComponent<RectTransform>().anchorMin = new Vector2(1f, 0.5f);
            toggleBtn.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0.5f);
            toggleBtn.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
        }

        private void CreateLanguageRow(Transform parent, ref float yTop, float gap)
        {
            const float sectionHeight = 0.36f;
            GameObject row = new GameObject("LanguageRow", typeof(RectTransform), typeof(Image));
            row.transform.SetParent(parent, false);
            Image rowBg = row.GetComponent<Image>();
            rowBg.raycastTarget = false;
            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.04f, yTop - sectionHeight);
            rowRect.anchorMax = new Vector2(0.96f, yTop);
            rowRect.offsetMin = Vector2.zero;
            rowRect.offsetMax = Vector2.zero;
            yTop -= sectionHeight + gap;
            UISharedFoundation.ApplyFramedPanel(rowBg, null,
                UIFrozenTokens.ColorPanel, UIFrozenTokens.ColorHeader,
                kind: UISharedFoundation.FramedPanelKind.ListRow);

            Text labelText = UISharedFoundation.CreateText(row.transform, "Label", "LANGUAGE PREFERENCE",
                UITextRole.Title, TextAnchor.MiddleLeft, new Color(0.95f, 0.9f, 0.79f), true, new Vector2(420f, 40f));
            labelText.raycastTarget = false;
            SetNormalizedRect(labelText.rectTransform, 0.03f, 0.78f, 0.45f, 0.96f);

            _languageValueText = UISharedFoundation.CreateText(row.transform, "Value", "", UITextRole.Body,
                TextAnchor.MiddleLeft, new Color(0.85f, 0.8f, 0.7f), true, new Vector2(520f, 36f));
            _languageValueText.fontSize = 22;
            _languageValueText.raycastTarget = false;
            SetNormalizedRect(_languageValueText.rectTransform, 0.46f, 0.78f, 0.97f, 0.96f);

            // Selectable list from PlayerSettingsCatalog.SupportedLanguages (not an opaque cycle).
            _languageOptionButtons.Clear();
            _languageOptionLabels.Clear();
            _languageOptionImages.Clear();
            GameObject options = new GameObject("LanguageOptions", typeof(RectTransform));
            options.transform.SetParent(row.transform, false);
            SetNormalizedRect(options.GetComponent<RectTransform>(), 0.03f, 0.06f, 0.97f, 0.74f);

            PlayerSettingsCatalog.LanguageOption[] languages = PlayerSettingsCatalog.SupportedLanguages;
            const int cols = 4;
            int rows = (languages.Length + cols - 1) / cols;
            for (int i = 0; i < languages.Length; i++)
            {
                PlayerSettingsCatalog.LanguageOption option = languages[i];
                int col = i % cols;
                int rowIndex = i / cols;
                float left = col / (float)cols + 0.01f;
                float right = (col + 1) / (float)cols - 0.01f;
                float top = 1f - rowIndex / (float)rows - 0.04f;
                float bottom = 1f - (rowIndex + 1) / (float)rows + 0.04f;

                string code = option.Code;
                GameObject btnObj = new GameObject($"Btn_Language_{SanitizeLanguageButtonId(code)}",
                    typeof(RectTransform), typeof(Image), typeof(Button));
                btnObj.transform.SetParent(options.transform, false);
                Image img = btnObj.GetComponent<Image>();
                HomeV3UiLibrary.ApplyNeutralActionButton(btnObj.GetComponent<Button>(), img);
                if (img.sprite == null)
                    img.color = new Color(0.16f, 0.2f, 0.26f, 0.96f);
                Button btn = btnObj.GetComponent<Button>();
                btn.onClick.AddListener(() => SelectLanguage(code));
                SetNormalizedRect(btnObj.GetComponent<RectTransform>(), left, bottom, right, top);
                btnObj.AddComponent<InteractionStateController>().Tier = UIDesignTokens.FrameTier.Tier3Utility;

                Text chipLabel = UISharedFoundation.CreateText(btnObj.transform, "Text", option.DisplayName,
                    UITextRole.Caption, TextAnchor.MiddleCenter, Color.white, true, new Vector2(200f, 36f));
                chipLabel.fontSize = 20;
                chipLabel.fontStyle = FontStyle.Bold;
                chipLabel.raycastTarget = false;
                UISharedFoundation.StretchFull(chipLabel.rectTransform);

                _languageOptionButtons.Add(btn);
                _languageOptionLabels.Add(chipLabel);
                _languageOptionImages.Add(img);
            }
        }

        private static string SanitizeLanguageButtonId(string code)
        {
            if (string.IsNullOrEmpty(code)) return "unknown";
            return code.Replace('-', '_').Replace('.', '_');
        }

        private void SelectLanguage(string languageCode)
        {
            PlayerProfile profile = SaveManager.SaveData;
            if (profile == null) return;
            PlayerSettingsService.SetPreferredLanguageCode(profile, languageCode);
            PlayerSettingsService.Persist(profile);
            RefreshAllRows();
        }

        private void CreateLogoutRow(Transform parent, ref float yTop, float rowHeight, float gap)
        {
            GameObject row = new GameObject("LogoutRow", typeof(RectTransform), typeof(Image));
            row.transform.SetParent(parent, false);
            Image rowBg = row.GetComponent<Image>();
            rowBg.raycastTarget = false;
            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.04f, yTop - rowHeight);
            rowRect.anchorMax = new Vector2(0.96f, yTop);
            rowRect.offsetMin = Vector2.zero;
            rowRect.offsetMax = Vector2.zero;
            yTop -= rowHeight + gap;
            // Applied AFTER final positioning - see EmpirePresenter's same fix for why.
            UISharedFoundation.ApplyFramedPanel(rowBg, null,
                UIFrozenTokens.ColorPanel, UIFrozenTokens.ColorHeader,
                kind: UISharedFoundation.FramedPanelKind.ListRow);

            Text labelText = UISharedFoundation.CreateText(row.transform, "Label", "ACCOUNT", UITextRole.Title,
                TextAnchor.MiddleLeft, new Color(0.95f, 0.9f, 0.79f), true, new Vector2(420f, 40f));
            labelText.raycastTarget = false;
            SetNormalizedRect(labelText.rectTransform, 0.03f, 0.15f, 0.38f, 0.85f);

            Button logoutBtn = CreateRowButton(row.transform, "Btn_Logout", "LOG OUT", new Vector2(-20f, 0f), OnLogout);
            logoutBtn.GetComponent<RectTransform>().anchorMin = new Vector2(1f, 0.5f);
            logoutBtn.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0.5f);
            logoutBtn.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
            Image logoutImg = logoutBtn.GetComponent<Image>();
            logoutImg.color = new Color(0.45f, 0.18f, 0.18f, 0.95f);
        }

        private static Button CreateHeaderButton(Transform parent, string name, string label, Vector2 anchoredPos,
            Action onClick)
        {
            GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            Image img = btnObj.GetComponent<Image>();
            img.color = new Color(0.3f, 0.2f, 0.2f);
            Button btn = btnObj.GetComponent<Button>();
            HomeV3UiLibrary.ApplyNavTileButton(btn, img);
            btn.onClick.AddListener(() => onClick?.Invoke());

            RectTransform rect = btnObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            // Landscape 1920x1080: enlarge beyond the prior 160x60 so the tap target is reliable.
            rect.sizeDelta = new Vector2(200f, 72f);

            UISharedFoundation.AddLocalGradientScrim(btnObj.transform, Vector2.zero, new Vector2(200f, 72f), UISharedFoundation.GradientDirection.TopToBottom, 0.95f);
            Text backText = UISharedFoundation.CreateText(btnObj.transform, "Text", label, UITextRole.Body, TextAnchor.MiddleCenter,
                Color.white, true, new Vector2(180f, 56f));
            backText.fontSize = 24;
            backText.fontStyle = FontStyle.Bold;
            backText.raycastTarget = false;
            btnObj.AddComponent<InteractionStateController>().Tier = UIDesignTokens.FrameTier.Tier3Utility;
            return btn;
        }

        private static Button CreateRowButton(Transform parent, string name, string label, Vector2 anchoredPos,
            Action onClick)
        {
            GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            Image img = btnObj.GetComponent<Image>();
            HomeV3UiLibrary.ApplyNeutralActionButton(btnObj.GetComponent<Button>(), img);
            if (img.sprite == null)
                img.color = new Color(0.18f, 0.24f, 0.3f, 0.96f);

            Button btn = btnObj.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());

            RectTransform rect = btnObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(180f, 52f);
            rect.anchoredPosition = anchoredPos;

            UISharedFoundation.AddLocalGradientScrim(btnObj.transform, Vector2.zero, new Vector2(180f, 52f), UISharedFoundation.GradientDirection.TopToBottom, 0.95f);
            Text btnText = UISharedFoundation.CreateText(btnObj.transform, "Text", label, UITextRole.Body, TextAnchor.MiddleCenter,
                Color.white, true, new Vector2(160f, 44f));
            btnText.fontSize = 22;
            btnText.fontStyle = FontStyle.Bold;
            btnText.raycastTarget = false;
            btnObj.AddComponent<InteractionStateController>().Tier = UIDesignTokens.FrameTier.Tier3Utility;
            return btn;
        }

        private static void SetNormalizedRect(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void ToggleAudio()
        {
            PlayerProfile profile = SaveManager.SaveData;
            if (profile == null) return;
            PlayerSettingsService.SetAudioEnabled(profile, !PlayerSettingsService.GetAudioEnabled(profile));
            PlayerSettingsService.Persist(profile);
            RefreshAllRows();
        }

        private void ToggleNotifications()
        {
            PlayerProfile profile = SaveManager.SaveData;
            if (profile == null) return;
            PlayerSettingsService.SetNotificationsEnabled(profile,
                !PlayerSettingsService.GetNotificationsEnabled(profile));
            PlayerSettingsService.Persist(profile);
            RefreshAllRows();
        }

        private void OnLogout()
        {
            PlayerProfile profile = SaveManager.SaveData;
            AccountSessionService.TryLogout(profile, out string message);
            SetStatus(message);
            _onLogoutCompleted?.Invoke(message);
        }

        private void RefreshAllRows()
        {
            PlayerProfile profile = SaveManager.SaveData;
            if (_audioValueText != null)
                _audioValueText.text = PlayerSettingsService.GetAudioEnabled(profile) ? "ON" : "OFF";
            if (_notificationsValueText != null)
                _notificationsValueText.text = PlayerSettingsService.GetNotificationsEnabled(profile) ? "ON" : "OFF";

            string code = PlayerSettingsService.GetPreferredLanguageCode(profile);
            string display = PlayerSettingsService.GetPreferredLanguageDisplayName(profile);
            if (_languageValueText != null)
                _languageValueText.text = $"Selected: {display} ({code})";

            PlayerSettingsCatalog.LanguageOption[] languages = PlayerSettingsCatalog.SupportedLanguages;
            for (int i = 0; i < _languageOptionButtons.Count && i < languages.Length; i++)
            {
                bool selected = string.Equals(languages[i].Code, code, StringComparison.OrdinalIgnoreCase);
                if (_languageOptionImages[i] != null)
                {
                    _languageOptionImages[i].color = selected
                        ? new Color(0.28f, 0.42f, 0.32f, 0.98f)
                        : new Color(0.16f, 0.2f, 0.26f, 0.96f);
                }
                if (_languageOptionLabels[i] != null)
                    _languageOptionLabels[i].fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        private void SetStatus(string message)
        {
            if (_statusText != null)
                _statusText.text = message ?? string.Empty;
        }

        public void TeardownUI()
        {
            CancelActiveTransition();
            _openTransitionTask = Task.CompletedTask;
            _canvasGroup = null;
            _languageOptionButtons.Clear();
            _languageOptionLabels.Clear();
            _languageOptionImages.Clear();

            if (_canvasObj == null) return;
            if (Application.isPlaying) Destroy(_canvasObj);
            else DestroyImmediate(_canvasObj);
            _canvasObj = null;
        }

        private void OnDestroy() => TeardownUI();
    }
}

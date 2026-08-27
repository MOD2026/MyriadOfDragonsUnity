using System;
using MyriadOfDragons.Data;
using MyriadOfDragons.Save;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>Utility Settings/Options screen — audio, notifications, language, logout.</summary>
    public class SettingsPresenter : MonoBehaviour
    {
        private GameObject _canvasObj;
        private Action _onBackToHome;
        private Action<string> _onLogoutCompleted;

        private Text _audioValueText;
        private Text _notificationsValueText;
        private Text _languageValueText;
        private Text _statusText;

        public void Initialize(Action onBackToHome, Action<string> onLogoutCompleted = null)
        {
            _onBackToHome = onBackToHome;
            _onLogoutCompleted = onLogoutCompleted;
            BuildUI();
            RefreshAllRows();
        }

        public GameObject CanvasObjectForTests => _canvasObj;

        public string StatusTextForTests => _statusText != null ? _statusText.text : null;

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
            topRect.sizeDelta = new Vector2(0f, 100f);

            Button backBtn = CreateHeaderButton(topBar.transform, "Btn_Back", "< BACK", new Vector2(30f, 0f),
                () =>
                {
                    TeardownUI();
                    _onBackToHome?.Invoke();
                });

            UISharedFoundation.CreateText(topBar.transform, "Title", "SETTINGS & OPTIONS",
                UITextRole.Display, TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 0.82f), true,
                new Vector2(720f, 60f)).fontSize = 32;
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

            float y = 0.92f;
            const float rowHeight = 0.16f;
            const float gap = 0.025f;

            CreateToggleRow(panel.transform, "AudioRow", "AUDIO", ref y, rowHeight, gap, ToggleAudio, out _audioValueText);
            CreateToggleRow(panel.transform, "NotificationsRow", "NOTIFICATIONS", ref y, rowHeight, gap,
                ToggleNotifications, out _notificationsValueText);
            CreateLanguageRow(panel.transform, ref y, rowHeight, gap);
            CreateLogoutRow(panel.transform, ref y, rowHeight, gap);

            _statusText = UISharedFoundation.CreateText(panel.transform, "SettingsStatus", "",
                UITextRole.Body, TextAnchor.MiddleCenter, new Color(0.85f, 0.82f, 0.7f), true,
                new Vector2(900f, 36f));
            _statusText.fontSize = 22;
            SetNormalizedRect(_statusText.rectTransform, 0.04f, 0.03f, 0.96f, 0.12f);
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
            SetNormalizedRect(labelText.rectTransform, 0.03f, 0.15f, 0.38f, 0.85f);

            valueText = UISharedFoundation.CreateText(row.transform, "Value", "", UITextRole.Body,
                TextAnchor.MiddleLeft, new Color(0.72f, 0.66f, 0.56f), true, new Vector2(220f, 36f));
            SetNormalizedRect(valueText.rectTransform, 0.40f, 0.15f, 0.68f, 0.85f);

            Button toggleBtn = CreateRowButton(row.transform, "Btn_Toggle", "TOGGLE", new Vector2(-20f, 0f), onToggle);
            toggleBtn.GetComponent<RectTransform>().anchorMin = new Vector2(1f, 0.5f);
            toggleBtn.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0.5f);
            toggleBtn.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
        }

        private void CreateLanguageRow(Transform parent, ref float yTop, float rowHeight, float gap)
        {
            GameObject row = new GameObject("LanguageRow", typeof(RectTransform), typeof(Image));
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

            Text labelText = UISharedFoundation.CreateText(row.transform, "Label", "LANGUAGE PREFERENCE",
                UITextRole.Title, TextAnchor.MiddleLeft, new Color(0.95f, 0.9f, 0.79f), true, new Vector2(420f, 40f));
            SetNormalizedRect(labelText.rectTransform, 0.03f, 0.15f, 0.38f, 0.85f);

            _languageValueText = UISharedFoundation.CreateText(row.transform, "Value", "", UITextRole.Body,
                TextAnchor.MiddleLeft, new Color(0.72f, 0.66f, 0.56f), true, new Vector2(420f, 36f));
            SetNormalizedRect(_languageValueText.rectTransform, 0.40f, 0.15f, 0.72f, 0.85f);

            Button cycleBtn = CreateRowButton(row.transform, "Btn_CycleLanguage", "CHANGE", new Vector2(-20f, 0f),
                CycleLanguage);
            cycleBtn.GetComponent<RectTransform>().anchorMin = new Vector2(1f, 0.5f);
            cycleBtn.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0.5f);
            cycleBtn.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
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
            rect.sizeDelta = new Vector2(160f, 60f);

            UISharedFoundation.AddLocalGradientScrim(btnObj.transform, Vector2.zero, new Vector2(160f, 60f), UISharedFoundation.GradientDirection.TopToBottom, 0.95f);
            Text backText = UISharedFoundation.CreateText(btnObj.transform, "Text", label, UITextRole.Body, TextAnchor.MiddleCenter,
                Color.white, true, new Vector2(140f, 50f));
            backText.fontSize = 22;
            backText.fontStyle = FontStyle.Bold;
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

        private void CycleLanguage()
        {
            PlayerProfile profile = SaveManager.SaveData;
            if (profile == null) return;
            PlayerSettingsService.CyclePreferredLanguage(profile);
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
            if (_languageValueText != null)
            {
                string code = PlayerSettingsService.GetPreferredLanguageCode(profile);
                string display = PlayerSettingsService.GetPreferredLanguageDisplayName(profile);
                _languageValueText.text = $"{display} ({code})";
            }
        }

        private void SetStatus(string message)
        {
            if (_statusText != null)
                _statusText.text = message ?? string.Empty;
        }

        private void TeardownUI()
        {
            if (_canvasObj == null) return;
            if (Application.isPlaying) Destroy(_canvasObj);
            else DestroyImmediate(_canvasObj);
            _canvasObj = null;
        }

        private void OnDestroy() => TeardownUI();
    }
}

using System;
using MyriadOfDragons.Metagame;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>GUILD HALL V1 art shell. Actions refuse while OpenValues stay OPEN.</summary>
    public class GuildHallEntryPresenter : MonoBehaviour
    {
        public const string CanvasName = "GuildHallEntryCanvas";

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


        public void PressEntryForTests() => OpenGuildExpedition();


        private void BuildUI()
        {
            TeardownUI();
            // Do not CleanupStaleMetagameCanvases - Guild Hall is a popup over Empire, same
            // convention as EmpireBuildingDetailPresenter; EmpireCanvas must stay underneath.

            Canvas canvas = UISharedFoundation.CreateScreenCanvas(CanvasName, new Vector2(1920, 1080));
            _canvasObj = canvas.gameObject;
            canvas.sortingOrder = 40;

            // Real bug (owner screenshot): the art background below used preserveAspect and could
            // letterbox, leaving gaps where EmpireCanvas visually bled through and stayed
            // clickable underneath. A guaranteed-opaque dimmer first, same pattern as
            // EmpireBuildingDetailPresenter's "Dimmer", fixes that regardless of art aspect ratio.
            GameObject dim = new GameObject("Dimmer", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(_canvasObj.transform, false);
            UISharedFoundation.StretchFull(dim.GetComponent<RectTransform>());
            Image dimImg = dim.GetComponent<Image>();
            dimImg.color = new Color(0.08f, 0.09f, 0.12f, 1f);
            dimImg.raycastTarget = true;

            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(_canvasObj.transform, false);
            UISharedFoundation.StretchFull(bg.GetComponent<RectTransform>());
            GuildHallUiLibrary.ApplyFullscreenShell(bg.GetComponent<Image>(), new Color(0.08f, 0.09f, 0.12f));
            bg.GetComponent<Image>().raycastTarget = false;

            BuildHeader();
            BuildBody();
        }

        private void BuildHeader()
        {
            GameObject topBar = new GameObject("GuildHallHeader", typeof(RectTransform));
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
            backBtn.AddComponent<InteractionStateController>().Tier = UIDesignTokens.FrameTier.Tier3Utility;

            Text title = UISharedFoundation.CreateText(topBar.transform, "Title", "GUILD HALL",
                UITextRole.Display, TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 0.82f), true,
                new Vector2(640f, 48f));
            title.fontSize = 30;
            SetNorm(title.rectTransform, 0.28f, 0.15f, 0.72f, 0.9f);

            _statusText = UISharedFoundation.CreateText(topBar.transform, "StatusLine", GuildHallEntryOpenValues.StatusNote,
                UITextRole.Caption, TextAnchor.MiddleRight, new Color(0.85f, 0.75f, 0.5f), true,
                new Vector2(520f, 40f));
            SetNorm(_statusText.rectTransform, 0.72f, 0.1f, 0.98f, 0.9f);
        }

        private void BuildBody()
        {
            GameObject panel = new GameObject("EntryPanel", typeof(RectTransform));
            panel.transform.SetParent(_canvasObj.transform, false);
            SetNorm(panel.GetComponent<RectTransform>(), 0.08f, 0.12f, 0.92f, 0.86f);

            Text name = UISharedFoundation.CreateText(panel.transform, "BuildingName", "GUILD HALL",
                UITextRole.Title, TextAnchor.MiddleLeft, new Color(0.95f, 0.9f, 0.79f), true, new Vector2(700f, 40f));
            SetNorm(name.rectTransform, 0.34f, 0.82f, 0.92f, 0.95f);

            Text avail = UISharedFoundation.CreateText(panel.transform, "Availability",
                $"{MetagameShellProfileBinding.SelfIdentityLine()}\n" +
                $"{MetagameShellProfileBinding.GuildContributionLine()}\n\n" +
                "Find/Create/Enter stay OPEN until social guild eligibility locks.\n" +
                "EXPEDITION opens the live Guild Expedition gateway.",
                UITextRole.Body, TextAnchor.UpperLeft,
                new Color(0.85f, 0.82f, 0.7f), true, new Vector2(900f, 160f));
            SetNorm(avail.rectTransform, 0.34f, 0.48f, 0.95f, 0.78f);

            Text flat = UISharedFoundation.CreateText(panel.transform, "FlatStatus",
                GuildHallEntryOpenValues.FlatNonUpgradeCopy, UITextRole.Body, TextAnchor.MiddleLeft,
                new Color(0.75f, 0.88f, 0.7f), true, new Vector2(900f, 36f));
            SetNorm(flat.rectTransform, 0.34f, 0.34f, 0.95f, 0.44f);

            GameObject entry = new GameObject("Btn_EntryAction", typeof(RectTransform), typeof(Image), typeof(Button));
            entry.transform.SetParent(panel.transform, false);
            Image eImg = entry.GetComponent<Image>();
            HomeV3UiLibrary.ApplyNeutralActionButton(entry.GetComponent<Button>(), eImg, new Color(0.22f, 0.36f, 0.28f));
            entry.GetComponent<Button>().onClick.AddListener(OpenGuildExpedition);
            SetNorm(entry.GetComponent<RectTransform>(), 0.58f, 0.08f, 0.95f, 0.24f);
            UISharedFoundation.CreateText(entry.transform, "Text", "EXPEDITION", UITextRole.Body,
                TextAnchor.MiddleCenter, Color.white, true, new Vector2(280f, 40f));
            entry.AddComponent<InteractionStateController>().Tier = UIDesignTokens.FrameTier.Tier1Hero;
        }

        /// <summary>Opens the live Guild Expedition gateway shell from Guild Hall entry.</summary>
        public void OpenGuildExpeditionForTests() => OpenGuildExpedition();

        private void OpenGuildExpedition()
        {
            Action returnToCaller = _onBack;
            TeardownUI();

            GuildExpeditionPresenter expedition = gameObject.GetComponent<GuildExpeditionPresenter>();
            if (expedition == null) expedition = gameObject.AddComponent<GuildExpeditionPresenter>();

            expedition.Initialize(() =>
            {
                if (Application.isPlaying) Destroy(expedition);
                else DestroyImmediate(expedition);
                returnToCaller?.Invoke();
            });
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

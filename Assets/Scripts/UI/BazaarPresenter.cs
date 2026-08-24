using System;
using MyriadOfDragons.Metagame;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>BAZAAR V1 art shell. Actions refuse while OpenValues stay OPEN.</summary>
    public class BazaarPresenter : MonoBehaviour
    {
        public const string CanvasName = "BazaarCanvas";

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


        public BazaarActionResult ConfirmTransactionForTests() =>
            Apply(BazaarOpenValues.TryConfirmTransaction());
        private BazaarActionResult Apply(BazaarActionResult result) { SetStatus(result.Message); return result; }


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
            BazaarUiLibrary.ApplyFullscreenShell(bg.GetComponent<Image>(), new Color(0.08f, 0.09f, 0.12f));

            BuildHeader();
            
            BuildTabs();
            BuildListingGrid();
            BuildSelectedPanel();

        }

        private void BuildHeader()
        {
            GameObject topBar = new GameObject("BazaarHeader", typeof(RectTransform));
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

            Text title = UISharedFoundation.CreateText(topBar.transform, "Title", "BAZAAR",
                UITextRole.Display, TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 0.82f), true,
                new Vector2(640f, 48f));
            title.fontSize = 30;
            SetNorm(title.rectTransform, 0.28f, 0.15f, 0.72f, 0.9f);

            _statusText = UISharedFoundation.CreateText(topBar.transform, "StatusLine", BazaarOpenValues.StatusNote,
                UITextRole.Caption, TextAnchor.MiddleRight, new Color(0.85f, 0.75f, 0.5f), true,
                new Vector2(520f, 40f));
            SetNorm(_statusText.rectTransform, 0.72f, 0.1f, 0.98f, 0.9f);
        }

        private void BuildTabs()
        {
            GameObject tabs = new GameObject("TabStrip", typeof(RectTransform));
            tabs.transform.SetParent(_canvasObj.transform, false);
            SetNorm(tabs.GetComponent<RectTransform>(), 0.03f, 0.04f, 0.56f, 0.14f);
            string[] labels = { "Browse", "Sell", "My Listings", "Wallet" };
            float w = 1f / labels.Length;
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                GameObject tab = new GameObject($"Tab_{labels[i].Replace(" ", "")}", typeof(RectTransform), typeof(Image), typeof(Button));
                tab.transform.SetParent(tabs.transform, false);
                Image img = tab.GetComponent<Image>();
                HomeV3UiLibrary.ApplyNeutralActionButton(tab.GetComponent<Button>(), img, new Color(0.18f, 0.22f, 0.28f, 0.55f));
                tab.GetComponent<Button>().onClick.AddListener(() => SetStatus(BazaarOpenValues.TrySelectTab(idx).Message));
                SetNorm(tab.GetComponent<RectTransform>(), i * w + 0.01f, 0.1f, (i + 1) * w - 0.01f, 0.9f);
                UISharedFoundation.CreateText(tab.transform, "Text", labels[i].ToUpperInvariant(), UITextRole.Caption,
                    TextAnchor.MiddleCenter, Color.white, true, new Vector2(180f, 36f));
            }
        }

        private void BuildListingGrid()
        {
            GameObject grid = new GameObject("ListingGrid", typeof(RectTransform));
            grid.transform.SetParent(_canvasObj.transform, false);
            SetNorm(grid.GetComponent<RectTransform>(), 0.04f, 0.22f, 0.56f, 0.72f);
            for (int i = 0; i < BazaarOpenValues.ShellListingWellCount; i++)
            {
                int slot = i;
                int col = i % 3;
                int row = i / 3;
                float cw = 1f / 3f;
                float rh = 1f / 2f;
                GameObject well = new GameObject($"ListingWell_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                well.transform.SetParent(grid.transform, false);
                Image img = well.GetComponent<Image>();
                img.color = new Color(0.1f, 0.12f, 0.16f, 0.35f);
                Button btn = well.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => SetStatus(BazaarOpenValues.TrySelectListing(slot).Message));
                SetNorm(well.GetComponent<RectTransform>(), col * cw + 0.02f, 1f - (row + 1) * rh + 0.02f, (col + 1) * cw - 0.02f, 1f - row * rh - 0.02f);
                Text t = UISharedFoundation.CreateText(well.transform, "Placeholder", BazaarOpenValues.RuntimePlaceholder,
                    UITextRole.Caption, TextAnchor.MiddleCenter, new Color(0.9f, 0.88f, 0.75f), true, new Vector2(160f, 40f));
                SetNorm(t.rectTransform, 0.05f, 0.35f, 0.95f, 0.65f);
            }
        }

        private void BuildSelectedPanel()
        {
            GameObject panel = new GameObject("SelectedPanel", typeof(RectTransform));
            panel.transform.SetParent(_canvasObj.transform, false);
            SetNorm(panel.GetComponent<RectTransform>(), 0.60f, 0.18f, 0.97f, 0.86f);
            Text details = UISharedFoundation.CreateText(panel.transform, "Details", BazaarOpenValues.RuntimePlaceholder,
                UITextRole.Body, TextAnchor.UpperLeft, new Color(0.9f, 0.88f, 0.75f), true, new Vector2(480f, 220f));
            SetNorm(details.rectTransform, 0.05f, 0.35f, 0.95f, 0.95f);
            GameObject action = new GameObject("Btn_PrimaryAction", typeof(RectTransform), typeof(Image), typeof(Button));
            action.transform.SetParent(panel.transform, false);
            Image aImg = action.GetComponent<Image>();
            HomeV3UiLibrary.ApplyNeutralActionButton(action.GetComponent<Button>(), aImg, new Color(0.2f, 0.4f, 0.3f));
            action.GetComponent<Button>().onClick.AddListener(() => SetStatus(BazaarOpenValues.TryConfirmTransaction().Message));
            SetNorm(action.GetComponent<RectTransform>(), 0.05f, 0.05f, 0.95f, 0.22f);
            UISharedFoundation.CreateText(action.transform, "Text", "CONFIRM", UITextRole.Body,
                TextAnchor.MiddleCenter, Color.white, true, new Vector2(280f, 40f));
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

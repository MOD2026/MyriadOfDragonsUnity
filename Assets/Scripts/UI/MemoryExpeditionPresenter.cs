using System;
using MyriadOfDragons.Metagame;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>MEMORY EXPEDITION V1 art shell. Actions refuse while OpenValues stay OPEN.</summary>
    public class MemoryExpeditionPresenter : MonoBehaviour
    {
        public const string CanvasName = "MemoryExpeditionCanvas";

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


        public MemoryExpeditionActionResult SelectRouteForTests(int routeIndex)
        {
            var r = MemoryExpeditionOpenValues.TrySelectRoute(routeIndex);
            SetStatus(r.Message);
            return r;
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
            MemoryExpeditionUiLibrary.ApplyFullscreenShell(bg.GetComponent<Image>(), new Color(0.08f, 0.09f, 0.12f));

            BuildHeader();
            BuildRoutes();
        }

        private void BuildHeader()
        {
            GameObject topBar = new GameObject("MemoryExpeditionHeader", typeof(RectTransform));
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

            Text title = UISharedFoundation.CreateText(topBar.transform, "Title", "MEMORY EXPEDITION",
                UITextRole.Display, TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 0.82f), true,
                new Vector2(640f, 48f));
            title.fontSize = 30;
            SetNorm(title.rectTransform, 0.28f, 0.15f, 0.72f, 0.9f);

            _statusText = UISharedFoundation.CreateText(topBar.transform, "StatusLine", MemoryExpeditionOpenValues.StatusNote,
                UITextRole.Caption, TextAnchor.MiddleRight, new Color(0.85f, 0.75f, 0.5f), true,
                new Vector2(520f, 40f));
            SetNorm(_statusText.rectTransform, 0.72f, 0.1f, 0.98f, 0.9f);
        }

        private void BuildRoutes()
        {
            GameObject row = new GameObject("RouteRow", typeof(RectTransform));
            row.transform.SetParent(_canvasObj.transform, false);
            SetNorm(row.GetComponent<RectTransform>(), 0.14f, 0.18f, 0.96f, 0.82f);
            for (int i = 0; i < MemoryExpeditionOpenValues.RouteCount; i++)
            {
                int route = i;
                float left = i / 3f;
                GameObject well = new GameObject($"RouteWell_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                well.transform.SetParent(row.transform, false);
                Image img = well.GetComponent<Image>();
                img.color = new Color(0.1f, 0.12f, 0.16f, 0.35f);
                Button btn = well.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => SetStatus(MemoryExpeditionOpenValues.TrySelectRoute(route).Message));
                SetNorm(well.GetComponent<RectTransform>(), left + 0.02f, 0.05f, left + 0.31f, 0.95f);
                Text label = UISharedFoundation.CreateText(well.transform, "Label",
                    $"ROUTE {((char)('A' + i))}", UITextRole.Title, TextAnchor.MiddleCenter,
                    new Color(0.95f, 0.9f, 0.79f), true, new Vector2(200f, 40f));
                SetNorm(label.rectTransform, 0.1f, 0.7f, 0.9f, 0.9f);
                Text ph = UISharedFoundation.CreateText(well.transform, "Placeholder",
                    MemoryExpeditionOpenValues.RuntimePlaceholder, UITextRole.Caption, TextAnchor.MiddleCenter,
                    new Color(0.85f, 0.82f, 0.7f), true, new Vector2(200f, 36f));
                SetNorm(ph.rectTransform, 0.1f, 0.35f, 0.9f, 0.55f);
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

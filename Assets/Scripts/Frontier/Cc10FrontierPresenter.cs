using System;
using System.Collections.Generic;
using MyriadOfDragons.Save;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Frontier
{
    /// <summary>
    /// CC10 presentation shell (legacy uGUI, procedural, landscape 1920x1080). Input and display
    /// only: every button forwards to <see cref="Cc10FrontierClient.ExecuteAsync"/> and the screen
    /// re-renders from the server snapshot. No runtime art is bound - none is approved
    /// (UIUX/AD gate), so surfaces are flat panels.
    /// </summary>
    public sealed class Cc10FrontierPresenter : MonoBehaviour
    {
        public const float MinTouch = 56f;
        private const float RefW = 1920f, RefH = 1080f;
        private const float TabH = 64f, RowH = 96f;

        private Cc10FrontierClient _client;
        private Func<IEnumerable<CardProgressionRecord>> _cards;
        private Action _onBack;
        private Cc10System _active = Cc10System.Tavern;
        private RectTransform _content;
        private Text _banner;
        private Text _status;
        private Font _font;
        private bool _built;

        public Cc10System ActiveSection => _active;
        public string StatusText => _status != null ? _status.text : string.Empty;
        public string BannerText => _banner != null ? _banner.text : string.Empty;
        public RectTransform Root { get; private set; }

        public void Initialize(Action onBack, Cc10FrontierClient client,
            Func<IEnumerable<CardProgressionRecord>> cards = null)
        {
            if (_built) return;
            _built = true;
            _onBack = onBack;
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _cards = cards;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
            _client.Changed += Render;
            Render();
        }

        private void OnDestroy()
        {
            if (_client != null) _client.Changed -= Render;
        }

        public void SelectSection(Cc10System system)
        {
            _active = system;
            Render();
        }

        public async void RefreshFromServer()
        {
            await _client.RefreshAsync();
        }

        public async void RunRow(Cc10Row row)
        {
            if (row == null || !row.HasAction || !row.ActionEnabled) return;
            Cc10CommandResult result = await _client.ExecuteAsync(
                _active, row.Endpoint, row.Payload, row.EntityKey);
            if (_status == null) return; // destroyed while awaiting
            _status.text = result.Outcome == Cc10Outcome.Applied ? string.Empty : result.Message;
        }

        // ---- build ---------------------------------------------------------------------

        private void Build()
        {
            var canvasGo = new GameObject("Cc10FrontierCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 190;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            Root = (RectTransform)canvasGo.transform;
            Panel("Backdrop", Root, 0, 0, RefW, RefH, new Color(0.06f, 0.07f, 0.1f, 1f));

            Label("Title", Root, 48, 24, 700, 56, "Frontier", 40, TextAnchor.MiddleLeft);
            AddButton("Btn_Back", Root, RefW - 48 - 200, 24, 200, MinTouch, "Back", () => _onBack?.Invoke());

            float tabW = (RefW - 96f) / 12f;
            int i = 0;
            foreach (Cc10System s in (Cc10System[])Enum.GetValues(typeof(Cc10System)))
            {
                Cc10System captured = s;
                AddButton("Tab_" + s, Root, 48 + i * tabW, 96, tabW - 6, TabH,
                    Cc10ViewModels.SectionTitle(s), () => SelectSection(captured), 18);
                i++;
            }

            _banner = Label("Banner", Root, 48, 176, RefW - 96, 40, string.Empty, 24, TextAnchor.MiddleLeft);
            _banner.color = new Color(1f, 0.8f, 0.4f);

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            scrollGo.transform.SetParent(Root, false);
            var scrollRt = (RectTransform)scrollGo.transform;
            Place(scrollRt, 48, 224, RefW - 96, 720);
            scrollGo.GetComponent<Image>().color = new Color(1, 1, 1, 0.04f);

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(scrollRt, false);
            _content = (RectTransform)contentGo.transform;
            _content.anchorMin = new Vector2(0, 1);
            _content.anchorMax = new Vector2(1, 1);
            _content.pivot = new Vector2(0.5f, 1);
            _content.offsetMin = Vector2.zero;
            _content.offsetMax = Vector2.zero;
            var sr = scrollGo.GetComponent<ScrollRect>();
            sr.content = _content;
            sr.horizontal = false;
            sr.viewport = scrollRt;

            _status = Label("Status", Root, 48, 960, RefW - 96, 56, string.Empty, 26, TextAnchor.MiddleLeft);
        }

        // ---- render --------------------------------------------------------------------

        private void Render()
        {
            if (_content == null) return;
            Cc10SectionVm vm = Cc10ViewModels.Build(_client, _active, _cards?.Invoke());
            _banner.text = vm.Banner;

            for (int c = _content.childCount - 1; c >= 0; c--)
                DestroyImmediate(_content.GetChild(c).gameObject);

            float y = 0;
            foreach (Cc10Row row in vm.Rows)
            {
                Cc10Row captured = row;
                var rowGo = new GameObject("Row_" + row.EntityKey, typeof(RectTransform));
                rowGo.transform.SetParent(_content, false);
                var rt = (RectTransform)rowGo.transform;
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0, 1);
                rt.offsetMin = new Vector2(0, -(y + RowH));
                rt.offsetMax = new Vector2(0, -y);

                Label("Title", rt, 24, 8, 900, 40, row.Title, 30, TextAnchor.MiddleLeft);
                string detail = row.Detail;
                if (row.HasAction && !row.ActionEnabled && !string.IsNullOrEmpty(row.DisabledReason))
                    detail = row.DisabledReason;
                Label("Detail", rt, 24, 48, 1200, 40, detail, 22, TextAnchor.MiddleLeft).color = new Color(1, 1, 1, 0.7f);

                if (row.HasAction)
                {
                    Button b = AddButton("Action", rt, 1824 - 24 - 240, (RowH - MinTouch) / 2f, 240, MinTouch,
                        row.ActionLabel, () => RunRow(captured));
                    b.interactable = row.ActionEnabled;
                }
                y += RowH;
            }
            _content.sizeDelta = new Vector2(0, y);
        }

        // ---- widgets (top-left rects) --------------------------------------------------

        private static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static Image Panel(string name, RectTransform parent, float x, float y, float w, float h, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, x, y, w, h);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private Text Label(string name, RectTransform parent, float x, float y, float w, float h,
            string text, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, x, y, w, h);
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.raycastTarget = false;
            return t;
        }

        private Button AddButton(string name, RectTransform parent, float x, float y, float w, float h,
            string label, Action onClick, int fontSize = 26)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, x, y, Mathf.Max(w, MinTouch), Mathf.Max(h, MinTouch));
            var img = go.GetComponent<Image>();
            img.color = new Color(0.22f, 0.26f, 0.36f, 1f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());
            Text t = Label("Label", (RectTransform)go.transform, 0, 0, Mathf.Max(w, MinTouch), Mathf.Max(h, MinTouch),
                label, fontSize, TextAnchor.MiddleCenter);
            t.raycastTarget = false;
            return btn;
        }
    }
}

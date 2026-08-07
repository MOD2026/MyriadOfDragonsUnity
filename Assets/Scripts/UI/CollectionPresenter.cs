using System;
using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Data;
using MyriadOfDragons.Save;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Read-only Collection screen for P1 Task 1.
    /// Displays cards owned in PlayerProfile.cardCollection with search and UI placeholders
    /// for filter/sort that can be implemented later without presenter refactoring.
    /// </summary>
    public class CollectionPresenter : MonoBehaviour
    {
        private GameObject _canvasObj;
        private Action _onBackToHomeAction;
        private Action _onOpenDeckBuilderAction;

        private Text _ownedCountText;
        private InputField _searchInput;
        private Transform _gridRoot;
        private Text _emptyStateText;

        private GameObject _detailPanel;
        private Text _detailTitleText;
        private Text _detailBodyText;
        private Image _detailArtImage;

        private readonly List<OwnedCardViewModel> _allCards = new List<OwnedCardViewModel>();
        private readonly List<OwnedCardViewModel> _filteredCards = new List<OwnedCardViewModel>();

        private ICardFilter _filter = new NoOpCardFilter();
        private ICardSort _sort = new NoOpCardSort();

        public void Initialize(Action onBackToHome, Action onOpenDeckBuilder)
        {
            _onBackToHomeAction = onBackToHome;
            _onOpenDeckBuilderAction = onOpenDeckBuilder;

            BuildUI();
            LoadOwnedCards();
            ApplySearchFilterSortAndRender();
        }

        private void BuildUI()
        {
            _canvasObj = new GameObject("CollectionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = _canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = _canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            GameObject bgObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgObj.transform.SetParent(_canvasObj.transform, false);
            Image bgImg = bgObj.GetComponent<Image>();
            bgImg.color = new Color(0.08f, 0.09f, 0.13f, 0.98f);

            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            BuildHeader();
            BuildControlsRow();
            BuildGridPanel();
            BuildDetailPanel();
            BuildBottomButtons();
        }

        private void BuildHeader()
        {
            GameObject headerObj = new GameObject("Header", typeof(RectTransform), typeof(Image));
            headerObj.transform.SetParent(_canvasObj.transform, false);

            Image headerImg = headerObj.GetComponent<Image>();
            headerImg.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);

            RectTransform headerRect = headerObj.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = Vector2.zero;
            headerRect.sizeDelta = new Vector2(0f, 110f);

            CreateTextElement(headerObj.transform, "Title", "CARD COLLECTION", new Vector2(-420f, 0f), 34, TextAnchor.MiddleLeft, new Vector2(820f, 70f));
            _ownedCountText = CreateTextElement(headerObj.transform, "OwnedCount", "Owned: 0", new Vector2(520f, 0f), 26, TextAnchor.MiddleRight, new Vector2(460f, 70f));
        }

        private void BuildControlsRow()
        {
            GameObject controlsObj = new GameObject("ControlsRow", typeof(RectTransform), typeof(Image));
            controlsObj.transform.SetParent(_canvasObj.transform, false);

            Image controlsImg = controlsObj.GetComponent<Image>();
            controlsImg.color = new Color(0.11f, 0.12f, 0.17f, 0.92f);

            RectTransform controlsRect = controlsObj.GetComponent<RectTransform>();
            controlsRect.anchorMin = new Vector2(0.5f, 1f);
            controlsRect.anchorMax = new Vector2(0.5f, 1f);
            controlsRect.pivot = new Vector2(0.5f, 1f);
            controlsRect.anchoredPosition = new Vector2(0f, -120f);
            controlsRect.sizeDelta = new Vector2(1780f, 90f);

            _searchInput = CreateSearchField(controlsObj.transform, new Vector2(-520f, 0f), new Vector2(720f, 60f));
            _searchInput.onValueChanged.AddListener(_ => ApplySearchFilterSortAndRender());

            CreatePlaceholderControl(controlsObj.transform, "FilterPlaceholder", "Filter (Coming Soon)", new Vector2(300f, 0f), new Vector2(260f, 60f), false);
            CreatePlaceholderControl(controlsObj.transform, "SortPlaceholder", "Sort (Coming Soon)", new Vector2(580f, 0f), new Vector2(260f, 60f), false);
        }

        private void BuildGridPanel()
        {
            GameObject panelObj = new GameObject("GridPanel", typeof(RectTransform), typeof(Image));
            panelObj.transform.SetParent(_canvasObj.transform, false);
            panelObj.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.2f, 0.85f);

            RectTransform panelRect = panelObj.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(0.72f, 1f);
            panelRect.offsetMin = new Vector2(20f, 130f);
            panelRect.offsetMax = new Vector2(-10f, -220f);

            GameObject scrollObj = new GameObject("CollectionScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollObj.transform.SetParent(panelObj.transform, false);
            scrollObj.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.08f);

            RectTransform scrollRect = scrollObj.GetComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(16f, 16f);
            scrollRect.offsetMax = new Vector2(-16f, -16f);

            GameObject viewportObj = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportObj.transform.SetParent(scrollObj.transform, false);
            viewportObj.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
            viewportObj.GetComponent<Mask>().showMaskGraphic = false;

            RectTransform viewportRect = viewportObj.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;

            GameObject contentObj = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            contentObj.transform.SetParent(viewportObj.transform, false);
            _gridRoot = contentObj.transform;

            RectTransform contentRect = contentObj.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            GridLayoutGroup grid = contentObj.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(220f, 300f);
            grid.spacing = new Vector2(18f, 18f);
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

            ContentSizeFitter fitter = contentObj.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect sr = scrollObj.GetComponent<ScrollRect>();
            sr.viewport = viewportRect;
            sr.content = contentRect;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;

            _emptyStateText = CreateTextElement(panelObj.transform, "EmptyState", "No owned cards to display.", new Vector2(0f, 0f), 28, TextAnchor.MiddleCenter, new Vector2(800f, 120f));
            _emptyStateText.gameObject.SetActive(false);
        }

        private void BuildDetailPanel()
        {
            _detailPanel = new GameObject("DetailPanel", typeof(RectTransform), typeof(Image));
            _detailPanel.transform.SetParent(_canvasObj.transform, false);
            _detailPanel.GetComponent<Image>().color = new Color(0.15f, 0.17f, 0.24f, 0.94f);

            RectTransform detailRect = _detailPanel.GetComponent<RectTransform>();
            detailRect.anchorMin = new Vector2(0.73f, 0f);
            detailRect.anchorMax = new Vector2(1f, 1f);
            detailRect.offsetMin = new Vector2(10f, 130f);
            detailRect.offsetMax = new Vector2(-20f, -220f);

            _detailTitleText = CreateTextElement(_detailPanel.transform, "DetailTitle", "Select a card", new Vector2(0f, 250f), 28, TextAnchor.MiddleCenter, new Vector2(420f, 70f));

            GameObject artObj = new GameObject("DetailArt", typeof(RectTransform), typeof(Image));
            artObj.transform.SetParent(_detailPanel.transform, false);
            _detailArtImage = artObj.GetComponent<Image>();
            _detailArtImage.color = new Color(0.22f, 0.24f, 0.3f, 1f);
            _detailArtImage.preserveAspect = true;

            RectTransform artRect = artObj.GetComponent<RectTransform>();
            artRect.anchoredPosition = new Vector2(0f, 75f);
            artRect.sizeDelta = new Vector2(300f, 300f);

            _detailBodyText = CreateTextElement(_detailPanel.transform, "DetailBody", "Tap any owned card in the grid to inspect details.", new Vector2(0f, -190f), 22, TextAnchor.UpperCenter, new Vector2(420f, 240f));
        }

        private void BuildBottomButtons()
        {
            GameObject backBtnObj = CreateButton(_canvasObj.transform, "BackButton", "< BACK", new Vector2(-560f, 60f), new Vector2(240f, 70f), new Color(0.28f, 0.2f, 0.2f));
            backBtnObj.GetComponent<Button>().onClick.AddListener(() =>
            {
                if (_canvasObj != null) Destroy(_canvasObj);
                _onBackToHomeAction?.Invoke();
            });

            GameObject deckBtnObj = CreateButton(_canvasObj.transform, "OpenDeckBuilderButton", "OPEN DECK BUILDER", new Vector2(560f, 60f), new Vector2(360f, 70f), new Color(0.18f, 0.4f, 0.28f));
            deckBtnObj.GetComponent<Button>().onClick.AddListener(() =>
            {
                if (_canvasObj != null) Destroy(_canvasObj);
                _onOpenDeckBuilderAction?.Invoke();
            });
        }

        private void LoadOwnedCards()
        {
            _allCards.Clear();

            PlayerProfile profile = SaveManager.SaveData;
            if (profile == null || profile.cardCollection == null)
            {
                return;
            }

            CardDatabase db = EnsureCardDatabase();

            foreach (string cardId in profile.cardCollection)
            {
                if (string.IsNullOrEmpty(cardId)) continue;

                Card resolved = db != null ? db.GetCard(cardId) : null;
                string displayName = resolved != null ? resolved.DisplayName : cardId;
                int cost = resolved != null ? resolved.ResourceCost : 0;
                int attack = resolved != null ? resolved.Attack : 0;
                int health = resolved != null ? resolved.Health : 0;
                string archetype = resolved != null ? resolved.Class.ToString() : "Unknown";
                Sprite art = (db != null && resolved != null) ? db.GetArt(resolved) : null;

                _allCards.Add(new OwnedCardViewModel
                {
                    CardId = cardId,
                    DisplayName = displayName,
                    Archetype = archetype,
                    Cost = cost,
                    Attack = attack,
                    Health = health,
                    Art = art,
                });
            }
        }

        private CardDatabase EnsureCardDatabase()
        {
            if (CardDatabase.Instance != null)
            {
                return CardDatabase.Instance;
            }

            GameObject dbGo = new GameObject("CollectionCardDatabase");
            CardDatabase db = dbGo.AddComponent<CardDatabase>();
            db.Initialize();
            return db;
        }

        private void ApplySearchFilterSortAndRender()
        {
            _filteredCards.Clear();

            IEnumerable<OwnedCardViewModel> query = _allCards;

            string search = _searchInput != null ? _searchInput.text : string.Empty;
            if (!string.IsNullOrWhiteSpace(search))
            {
                string needle = search.Trim();
                query = query.Where(c =>
                    c.DisplayName.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0
                    || c.CardId.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0
                    || c.Archetype.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            query = _filter.Apply(query);
            query = _sort.Apply(query);

            _filteredCards.AddRange(query);

            RenderGrid();
            UpdateOwnedCount();
        }

        private void RenderGrid()
        {
            foreach (Transform child in _gridRoot)
            {
                Destroy(child.gameObject);
            }

            bool hasAny = _filteredCards.Count > 0;
            _emptyStateText.gameObject.SetActive(!hasAny);

            if (!hasAny)
            {
                if (_allCards.Count == 0)
                {
                    _emptyStateText.text = "No owned cards to display.";
                }
                else
                {
                    _emptyStateText.text = "No cards match your search.";
                }
                return;
            }

            foreach (OwnedCardViewModel card in _filteredCards)
            {
                GameObject tile = CreateCardTile(_gridRoot, card);
                tile.GetComponent<Button>().onClick.AddListener(() => ShowDetail(card));
            }
        }

        private GameObject CreateCardTile(Transform parent, OwnedCardViewModel card)
        {
            GameObject tileObj = new GameObject($"OwnedCard_{card.CardId}", typeof(RectTransform), typeof(Image), typeof(Button));
            tileObj.transform.SetParent(parent, false);
            tileObj.transform.localScale = Vector3.one;

            Image bg = tileObj.GetComponent<Image>();
            bg.color = new Color(0.19f, 0.23f, 0.32f, 1f);

            CreateTextElement(tileObj.transform, "Name", card.DisplayName, new Vector2(0f, 118f), 20, TextAnchor.MiddleCenter, new Vector2(205f, 54f));

            GameObject artObj = new GameObject("Art", typeof(RectTransform), typeof(Image));
            artObj.transform.SetParent(tileObj.transform, false);
            Image artImg = artObj.GetComponent<Image>();
            artImg.preserveAspect = true;
            artImg.color = card.Art != null ? Color.white : new Color(0.3f, 0.32f, 0.4f);
            artImg.sprite = card.Art;

            RectTransform artRect = artObj.GetComponent<RectTransform>();
            artRect.anchoredPosition = new Vector2(0f, 18f);
            artRect.sizeDelta = new Vector2(178f, 178f);

            CreateTextElement(tileObj.transform, "Type", card.Archetype, new Vector2(0f, -84f), 16, TextAnchor.MiddleCenter, new Vector2(200f, 40f));
            CreateTextElement(tileObj.transform, "Stats", $"Cost {card.Cost}   ATK {card.Attack}   HP {card.Health}", new Vector2(0f, -122f), 15, TextAnchor.MiddleCenter, new Vector2(210f, 36f));

            return tileObj;
        }

        private void ShowDetail(OwnedCardViewModel card)
        {
            _detailTitleText.text = card.DisplayName;
            _detailArtImage.sprite = card.Art;
            _detailArtImage.color = card.Art != null ? Color.white : new Color(0.22f, 0.24f, 0.3f, 1f);

            _detailBodyText.text =
                $"Card ID: {card.CardId}\n" +
                $"Class: {card.Archetype}\n" +
                $"Cost: {card.Cost}\n" +
                $"Attack: {card.Attack}\n" +
                $"Health: {card.Health}\n\n" +
                "Collection view is read-only in this phase.";
        }

        private void UpdateOwnedCount()
        {
            _ownedCountText.text = $"Owned: {_allCards.Count}";
        }

        private InputField CreateSearchField(Transform parent, Vector2 position, Vector2 size)
        {
            GameObject root = new GameObject("SearchField", typeof(RectTransform), typeof(Image), typeof(InputField));
            root.transform.SetParent(parent, false);
            root.transform.localScale = Vector3.one;

            Image bg = root.GetComponent<Image>();
            bg.color = new Color(0.2f, 0.22f, 0.28f, 1f);

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchoredPosition = position;
            rootRect.sizeDelta = size;

            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(root.transform, false);
            Text text = textObj.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 22;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 0f);
            textRect.offsetMax = new Vector2(-16f, 0f);

            GameObject placeholderObj = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            placeholderObj.transform.SetParent(root.transform, false);
            Text placeholder = placeholderObj.GetComponent<Text>();
            placeholder.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            placeholder.fontSize = 22;
            placeholder.color = new Color(1f, 1f, 1f, 0.45f);
            placeholder.text = "Search owned cards...";
            placeholder.alignment = TextAnchor.MiddleLeft;

            RectTransform placeholderRect = placeholderObj.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(16f, 0f);
            placeholderRect.offsetMax = new Vector2(-16f, 0f);

            InputField input = root.GetComponent<InputField>();
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = 80;

            return input;
        }

        private GameObject CreatePlaceholderControl(Transform parent, string name, string label, Vector2 position, Vector2 size, bool interactable)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            root.transform.localScale = Vector3.one;

            Image bg = root.GetComponent<Image>();
            bg.color = new Color(0.24f, 0.19f, 0.16f, interactable ? 1f : 0.65f);

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Button button = root.GetComponent<Button>();
            button.interactable = interactable;

            CreateTextElement(root.transform, "Label", label, Vector2.zero, 18, TextAnchor.MiddleCenter, new Vector2(size.x - 10f, size.y - 8f));
            return root;
        }

        private GameObject CreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Color color)
        {
            GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            btnObj.transform.localScale = Vector3.one;

            btnObj.GetComponent<Image>().color = color;

            RectTransform rect = btnObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            CreateTextElement(btnObj.transform, "Text", label, Vector2.zero, 24, TextAnchor.MiddleCenter, size);
            return btnObj;
        }

        private Text CreateTextElement(Transform parent, string objectName, string content, Vector2 position, int fontSize,
            TextAnchor alignment, Vector2 size)
        {
            GameObject textObj = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(parent, false);
            textObj.transform.localScale = Vector3.one;

            Text txt = textObj.GetComponent<Text>();
            txt.text = content;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = fontSize;
            txt.alignment = alignment;
            txt.color = Color.white;
            txt.supportRichText = true;

            RectTransform rect = textObj.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            return txt;
        }

        private sealed class OwnedCardViewModel
        {
            public string CardId;
            public string DisplayName;
            public string Archetype;
            public int Cost;
            public int Attack;
            public int Health;
            public Sprite Art;
        }

        // Reusable interfaces for future functional filter/sort implementations.
        private interface ICardFilter
        {
            IEnumerable<OwnedCardViewModel> Apply(IEnumerable<OwnedCardViewModel> cards);
        }

        private interface ICardSort
        {
            IEnumerable<OwnedCardViewModel> Apply(IEnumerable<OwnedCardViewModel> cards);
        }

        private sealed class NoOpCardFilter : ICardFilter
        {
            public IEnumerable<OwnedCardViewModel> Apply(IEnumerable<OwnedCardViewModel> cards) => cards;
        }

        private sealed class NoOpCardSort : ICardSort
        {
            public IEnumerable<OwnedCardViewModel> Apply(IEnumerable<OwnedCardViewModel> cards) => cards;
        }
    }
}

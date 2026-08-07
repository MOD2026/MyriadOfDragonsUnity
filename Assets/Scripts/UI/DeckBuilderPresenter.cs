using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Data;
using MyriadOfDragons.Save;

namespace MyriadOfDragons.UI
{
    public class DeckCardData
    {
        public string id;
        public string cardName;
        public string archetype;
        public int cost;
        public int attack;
        public int health;
        public string artPath;

        public DeckCardData(string id, string name, string archetype, int cost, int atk, int hp, string artPath)
        {
            this.id = id;
            this.cardName = name;
            this.archetype = archetype;
            this.cost = cost;
            this.attack = atk;
            this.health = hp;
            this.artPath = artPath;
        }
    }

    public class DeckBuilderPresenter : MonoBehaviour
    {
        private GameObject canvasObj;
        private System.Action onBackToHomeAction;

        private readonly List<DeckCardData> ownedCollectionCards = new List<DeckCardData>();
        private List<DeckCardData> activeDeck = new List<DeckCardData>();
        private Text deckCounterText;
        private Transform collectionGridTransform;
        private Transform deckListTransform;
        private Text emptyCollectionText;

        public void Initialize(System.Action onBackToHome)
        {
            this.onBackToHomeAction = onBackToHome;

            LoadOwnedCollectionCards();
            BuildUI();
        }

        private void LoadOwnedCollectionCards()
        {
            ownedCollectionCards.Clear();

            PlayerProfile profile = SaveManager.SaveData;
            if (profile == null || profile.cardCollection == null)
            {
                return;
            }

            CardDatabase database = EnsureCardDatabase();

            foreach (string cardId in profile.cardCollection)
            {
                if (string.IsNullOrEmpty(cardId))
                {
                    continue;
                }

                Card resolved = database != null ? database.GetCard(cardId) : null;
                if (resolved == null)
                {
                    continue;
                }

                ownedCollectionCards.Add(new DeckCardData(
                    resolved.Id,
                    resolved.DisplayName,
                    resolved.Class.ToString(),
                    resolved.ResourceCost,
                    resolved.Attack,
                    resolved.Health,
                    resolved.ResourcePath()));
            }
        }

        private CardDatabase EnsureCardDatabase()
        {
            if (CardDatabase.Instance != null)
            {
                return CardDatabase.Instance;
            }

            GameObject databaseObject = new GameObject("DeckBuilderCardDatabase");
            CardDatabase database = databaseObject.AddComponent<CardDatabase>();
            database.Initialize();
            return database;
        }

        private void BuildUI()
        {
            // 1. Canvas Setup
            canvasObj = new GameObject("DeckBuilderCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // 2. Backdrop
            GameObject bgObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgObj.transform.SetParent(canvasObj.transform, false);
            Image bgImg = bgObj.GetComponent<Image>();
            bgImg.color = new Color(0.08f, 0.08f, 0.12f, 0.98f);

            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            // 3. Top Header Bar
            GameObject topBar = new GameObject("HeaderBar", typeof(RectTransform), typeof(Image));
            topBar.transform.SetParent(canvasObj.transform, false);
            Image topBarBg = topBar.GetComponent<Image>();
            topBarBg.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);

            RectTransform topRect = topBar.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0, 1);
            topRect.anchorMax = Vector2.one;
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.sizeDelta = new Vector2(0, 100);

            // Back Button
            GameObject backBtnObj = new GameObject("Btn_Back", typeof(RectTransform), typeof(Image), typeof(Button));
            backBtnObj.transform.SetParent(topBar.transform, false);
            Image backImg = backBtnObj.GetComponent<Image>();
            backImg.color = new Color(0.3f, 0.2f, 0.2f);

            Button backBtn = backBtnObj.GetComponent<Button>();
            backBtn.onClick.AddListener(() =>
            {
                Destroy(canvasObj);
                onBackToHomeAction?.Invoke();
            });

            RectTransform backRect = backBtnObj.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0, 0.5f);
            backRect.anchorMax = new Vector2(0, 0.5f);
            backRect.pivot = new Vector2(0, 0.5f);
            backRect.anchoredPosition = new Vector2(30, 0);
            backRect.sizeDelta = new Vector2(160, 60);

            CreateTextElement(backBtnObj.transform, "Text", "< BACK", Vector2.zero, 24, TextAnchor.MiddleCenter);

            // Header Title
            CreateTextElement(topBar.transform, "Title", "DECK BUILDER", new Vector2(-150, 0), 32, TextAnchor.MiddleCenter);

            // Deck Counter Label
            GameObject counterObj = CreateTextElement(topBar.transform, "Counter", "", new Vector2(600, 0), 28, TextAnchor.MiddleRight);
            deckCounterText = counterObj.GetComponent<Text>();

            // 4. Split Panels
            BuildCollectionPanel();
            BuildDeckPanel();

            RefreshCollectionUI();
            RefreshDeckUI();
        }

        private void BuildCollectionPanel()
        {
            GameObject panelObj = new GameObject("CollectionPanel", typeof(RectTransform), typeof(Image));
            panelObj.transform.SetParent(canvasObj.transform, false);
            Image panelImg = panelObj.GetComponent<Image>();
            panelImg.color = new Color(0.12f, 0.12f, 0.16f, 0.9f);

            RectTransform rect = panelObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0.66f, 1f);
            rect.offsetMin = new Vector2(20, 20);
            rect.offsetMax = new Vector2(-10, -120);

            CreateTextElement(panelObj.transform, "Header", "CARD COLLECTION (Tap to Add)", new Vector2(0, 410), 26, TextAnchor.MiddleCenter);

            // Collection Grid Container with Full Stretch Anchors
            GameObject gridObj = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridObj.transform.SetParent(panelObj.transform, false);
            collectionGridTransform = gridObj.transform;

            RectTransform gridRect = gridObj.GetComponent<RectTransform>();
            gridRect.anchorMin = Vector2.zero;
            gridRect.anchorMax = Vector2.one;
            gridRect.offsetMin = new Vector2(20, 20);
            gridRect.offsetMax = new Vector2(-20, -70);

            GameObject emptyStateObj = CreateTextElement(panelObj.transform, "EmptyState", "No owned cards available.", new Vector2(0, 0), 26, TextAnchor.MiddleCenter);
            emptyCollectionText = emptyStateObj.GetComponent<Text>();
            emptyCollectionText.enabled = false;

            GridLayoutGroup grid = gridObj.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(230, 320);
            grid.spacing = new Vector2(20, 20);
            grid.childAlignment = TextAnchor.UpperLeft;
        }

        private void BuildDeckPanel()
        {
            GameObject panelObj = new GameObject("DeckPanel", typeof(RectTransform), typeof(Image));
            panelObj.transform.SetParent(canvasObj.transform, false);
            Image panelImg = panelObj.GetComponent<Image>();
            panelImg.color = new Color(0.15f, 0.13f, 0.18f, 0.9f);

            RectTransform rect = panelObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.67f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(10, 20);
            rect.offsetMax = new Vector2(-20, -120);

            CreateTextElement(panelObj.transform, "Header", "ACTIVE DECK (Tap to Remove)", new Vector2(0, 410), 24, TextAnchor.MiddleCenter);

            // Deck List Container with Full Stretch Anchors
            GameObject listObj = new GameObject("DeckList", typeof(RectTransform), typeof(VerticalLayoutGroup));
            listObj.transform.SetParent(panelObj.transform, false);
            deckListTransform = listObj.transform;

            RectTransform listRect = listObj.GetComponent<RectTransform>();
            listRect.anchorMin = Vector2.zero;
            listRect.anchorMax = Vector2.one;
            listRect.offsetMin = new Vector2(20, 20);
            listRect.offsetMax = new Vector2(-20, -70);

            VerticalLayoutGroup vlg = listObj.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 8;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;
        }

        private void RefreshCollectionUI()
        {
            foreach (Transform child in collectionGridTransform)
            {
                Destroy(child.gameObject);
            }

            bool hasCards = ownedCollectionCards.Count > 0;
            if (emptyCollectionText != null)
            {
                emptyCollectionText.enabled = !hasCards;
            }

            if (!hasCards)
            {
                return;
            }

            foreach (var card in ownedCollectionCards)
            {
                GameObject cardObj = new GameObject($"Card_{card.id}", typeof(RectTransform), typeof(Image), typeof(Button));
                cardObj.transform.SetParent(collectionGridTransform, false);
                cardObj.transform.localScale = Vector3.one;

                Image cardBg = cardObj.GetComponent<Image>();
                cardBg.color = new Color(0.18f, 0.22f, 0.32f);

                Button btn = cardObj.GetComponent<Button>();
                DeckCardData capturedCard = card;
                btn.onClick.AddListener(() => AddCardToDeck(capturedCard));

                // Card Visual Elements
                CreateTextElement(cardObj.transform, "Name", card.cardName, new Vector2(0, 100), 22, TextAnchor.MiddleCenter);
                CreateTextElement(cardObj.transform, "Type", $"<{card.archetype}>", new Vector2(0, 65), 18, TextAnchor.MiddleCenter);
                CreateTextElement(cardObj.transform, "Cost", $"Cost: <color=#00BFFF>{card.cost}</color>", new Vector2(0, 10), 22, TextAnchor.MiddleCenter);
                CreateTextElement(cardObj.transform, "Stats", $"ATK: <color=#FF8C00>{card.attack}</color>  HP: <color=#FF4500>{card.health}</color>", new Vector2(0, -90), 20, TextAnchor.MiddleCenter);
            }
        }

        private void RefreshDeckUI()
        {
            foreach (Transform child in deckListTransform)
            {
                Destroy(child.gameObject);
            }

            for (int i = 0; i < activeDeck.Count; i++)
            {
                var card = activeDeck[i];
                int index = i;

                GameObject rowObj = new GameObject($"DeckRow_{i}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                rowObj.transform.SetParent(deckListTransform, false);
                rowObj.transform.localScale = Vector3.one;

                Image rowBg = rowObj.GetComponent<Image>();
                rowBg.color = new Color(0.28f, 0.22f, 0.16f);

                LayoutElement le = rowObj.GetComponent<LayoutElement>();
                le.preferredHeight = 48;
                le.minHeight = 48;

                Button btn = rowObj.GetComponent<Button>();
                btn.onClick.AddListener(() => RemoveCardFromDeck(index));

                CreateTextElement(rowObj.transform, "Text", $"[{card.cost}]  {card.cardName}  ({card.archetype})", Vector2.zero, 20, TextAnchor.MiddleCenter);
            }

            UpdateDeckCounter();
        }

        private void AddCardToDeck(DeckCardData card)
        {
            if (activeDeck.Count >= 20)
            {
                Debug.Log("Deck is full (Maximum 20 cards)!");
                return;
            }

            activeDeck.Add(card);
            RefreshDeckUI();
        }

        private void RemoveCardFromDeck(int index)
        {
            if (index >= 0 && index < activeDeck.Count)
            {
                activeDeck.RemoveAt(index);
                RefreshDeckUI();
            }
        }

        private void UpdateDeckCounter()
        {
            if (deckCounterText != null)
            {
                deckCounterText.text = $"Deck: <color=#FFD700>{activeDeck.Count}/20</color> Cards";
            }
        }

        private GameObject CreateTextElement(Transform parent, string objectName, string content, Vector2 position, int fontSize, TextAnchor alignment)
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
            rect.sizeDelta = new Vector2(500, 60);

            return textObj;
        }
    }
}
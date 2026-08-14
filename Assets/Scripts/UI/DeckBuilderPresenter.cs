using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
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

        private PlayerProfile profile;
        private CardDatabase cardDatabase;
        private int deckSizeLimit = 10;

        private readonly List<DeckCardData> ownedCollectionCards = new List<DeckCardData>();
        private readonly Dictionary<string, DeckCardData> ownedCardsById = new Dictionary<string, DeckCardData>();
        private readonly List<DeckCardData> activeDeck = new List<DeckCardData>();
        private Text deckCounterText;
        private Transform collectionGridTransform;
        private Transform deckListTransform;
        private Text collectionEmptyText;
        private Text deckEmptyText;
        private Text deckStatsText;
        private Text deckStatusText;
        private Button confirmDeckButton;
        private Button recommendedDeckButton;

        public void Initialize(System.Action onBackToHome)
        {
            this.onBackToHomeAction = onBackToHome;

            LoadProfileState();
            LoadOwnedCollectionCards();
            LoadSavedDeck();
            BuildUI();
        }

        /// <summary>Exposed for tests: builds a deck from the given owned card ids and saves
        /// it - the same effect as tapping each card in the collection panel and then Confirm/
        /// Save, without needing to simulate UI Button clicks (which EditMode tests have no way
        /// to do). Reuses the real, private AddCardToDeck/ConfirmDeck path exactly as the UI
        /// does, including CanAddCard's own ownership/uniqueness/capacity checks - an id that
        /// isn't owned or is already in the deck is silently skipped, same as a real tap on an
        /// already-added or unowned card would be.</summary>
        public void SetAndConfirmDeckForTests(IEnumerable<string> cardIds)
        {
            activeDeck.Clear();
            foreach (string cardId in cardIds)
            {
                if (ownedCardsById.TryGetValue(cardId, out DeckCardData card))
                {
                    AddCardToDeck(card);
                }
            }

            ConfirmDeck();
        }

        private void LoadProfileState()
        {
            profile = SaveManager.SaveData;
            if (profile == null)
            {
                deckSizeLimit = 10;
                return;
            }

            profile.ApplyDataToEmpire();
            if (profile.Empire != null && profile.Empire.DeckSlotCount > 0)
            {
                deckSizeLimit = profile.Empire.DeckSlotCount;
            }
            else
            {
                deckSizeLimit = 10;
            }
        }

        private void LoadOwnedCollectionCards()
        {
            ownedCollectionCards.Clear();
            ownedCardsById.Clear();

            if (profile == null || profile.cardCollection == null)
            {
                return;
            }

            cardDatabase = EnsureCardDatabase();
            HashSet<string> seenIds = new HashSet<string>();

            foreach (string cardId in profile.cardCollection)
            {
                if (string.IsNullOrEmpty(cardId) || !seenIds.Add(cardId))
                {
                    continue;
                }

                Card resolved = cardDatabase != null ? cardDatabase.GetCard(cardId) : null;
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

                ownedCardsById[resolved.Id] = ownedCollectionCards[ownedCollectionCards.Count - 1];
            }
        }

        private void LoadSavedDeck()
        {
            activeDeck.Clear();

            if (profile == null || profile.activeDeckCardIds == null)
            {
                return;
            }

            HashSet<string> seenIds = new HashSet<string>();
            foreach (string cardId in profile.activeDeckCardIds)
            {
                if (string.IsNullOrEmpty(cardId) || !seenIds.Add(cardId))
                {
                    continue;
                }

                if (!ownedCardsById.TryGetValue(cardId, out DeckCardData card))
                {
                    continue;
                }

                activeDeck.Add(card);
                if (activeDeck.Count >= deckSizeLimit)
                {
                    break;
                }
            }
        }

        private CardDatabase EnsureCardDatabase()
        {
            if (cardDatabase != null)
            {
                return cardDatabase;
            }

            if (CardDatabase.Instance != null)
            {
                cardDatabase = CardDatabase.Instance;
                return cardDatabase;
            }

            GameObject databaseObject = new GameObject("DeckBuilderCardDatabase");
            cardDatabase = databaseObject.AddComponent<CardDatabase>();
            cardDatabase.Initialize();
            return cardDatabase;
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
                DestroyDynamicUIObject(canvasObj);
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
            deckCounterText = CreateTextElement(topBar.transform, "Counter", "", new Vector2(600, 0), 28, TextAnchor.MiddleRight);

            // 4. Split Panels
            BuildCollectionPanel();
            BuildDeckPanel();

            RefreshAllUI();
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

            GameObject scrollObj = new GameObject("CollectionScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollObj.transform.SetParent(panelObj.transform, false);
            scrollObj.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.08f);

            RectTransform scrollRect = scrollObj.GetComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(16, 16);
            scrollRect.offsetMax = new Vector2(-16, -16);

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
            collectionGridTransform = contentObj.transform;

            RectTransform contentRect = contentObj.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            GridLayoutGroup grid = contentObj.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(230, 320);
            grid.spacing = new Vector2(20, 20);
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;

            ContentSizeFitter fitter = contentObj.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect sr = scrollObj.GetComponent<ScrollRect>();
            sr.viewport = viewportRect;
            sr.content = contentRect;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;

            collectionEmptyText = CreateTextElement(panelObj.transform, "EmptyState", "No owned cards available.", new Vector2(0, 0), 26, TextAnchor.MiddleCenter, new Vector2(780, 120));
            collectionEmptyText.enabled = false;
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

            deckStatsText = CreateTextElement(panelObj.transform, "DeckStats", "", new Vector2(0, 305), 22, TextAnchor.MiddleLeft, new Vector2(560, 150));

            GameObject scrollObj = new GameObject("DeckScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollObj.transform.SetParent(panelObj.transform, false);
            scrollObj.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.08f);

            RectTransform scrollRect = scrollObj.GetComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(16, 150);
            scrollRect.offsetMax = new Vector2(-16, -170);

            GameObject viewportObj = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportObj.transform.SetParent(scrollObj.transform, false);
            viewportObj.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
            viewportObj.GetComponent<Mask>().showMaskGraphic = false;

            RectTransform viewportRect = viewportObj.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;

            GameObject contentObj = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObj.transform.SetParent(viewportObj.transform, false);
            deckListTransform = contentObj.transform;

            RectTransform contentRect = contentObj.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup vlg = contentObj.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 8;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;

            ContentSizeFitter fitter = contentObj.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect sr = scrollObj.GetComponent<ScrollRect>();
            sr.viewport = viewportRect;
            sr.content = contentRect;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;

            deckEmptyText = CreateTextElement(panelObj.transform, "EmptyDeckState", "No cards in the deck yet. Add owned cards from the collection.", new Vector2(0, -5), 24, TextAnchor.MiddleCenter, new Vector2(560, 140));
            deckEmptyText.enabled = false;

            recommendedDeckButton = CreateButton(panelObj.transform, "Btn_Recommended", "RECOMMENDED DECK", new Vector2(-170, -405), new Vector2(300, 60), new Color(0.18f, 0.4f, 0.28f)).GetComponent<Button>();
            recommendedDeckButton.onClick.AddListener(ApplyRecommendedDeck);

            confirmDeckButton = CreateButton(panelObj.transform, "Btn_Confirm", "CONFIRM / SAVE DECK", new Vector2(170, -405), new Vector2(300, 60), new Color(0.85f, 0.65f, 0.15f)).GetComponent<Button>();
            confirmDeckButton.onClick.AddListener(ConfirmDeck);

            deckStatusText = CreateTextElement(panelObj.transform, "DeckStatus", "", new Vector2(0, -475), 20, TextAnchor.MiddleCenter, new Vector2(560, 90));
        }

        private void RefreshCollectionUI()
        {
            DestroyAllChildren(collectionGridTransform);

            bool hasCards = ownedCollectionCards.Count > 0;
            if (collectionEmptyText != null)
            {
                collectionEmptyText.enabled = !hasCards;
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
                btn.interactable = CanAddCard(capturedCard);

                // Card Visual Elements
                CreateTextElement(cardObj.transform, "Name", card.cardName, new Vector2(0, 100), 22, TextAnchor.MiddleCenter);
                CreateTextElement(cardObj.transform, "Type", $"<{card.archetype}>", new Vector2(0, 65), 18, TextAnchor.MiddleCenter);
                CreateTextElement(cardObj.transform, "Cost", $"Cost: <color=#00BFFF>{card.cost}</color>", new Vector2(0, 10), 22, TextAnchor.MiddleCenter);
                CreateTextElement(cardObj.transform, "Stats", $"ATK: <color=#FF8C00>{card.attack}</color>  HP: <color=#FF4500>{card.health}</color>", new Vector2(0, -90), 20, TextAnchor.MiddleCenter);
            }
        }

        private void RefreshDeckUI()
        {
            DestroyAllChildren(deckListTransform);

            if (deckEmptyText != null)
            {
                deckEmptyText.enabled = activeDeck.Count == 0;
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

            UpdateDeckUIState();
        }

        private void AddCardToDeck(DeckCardData card)
        {
            if (!CanAddCard(card))
            {
                UpdateDeckUIState();
                return;
            }

            activeDeck.Add(card);
            RefreshAllUI();
        }

        private void RemoveCardFromDeck(int index)
        {
            if (index >= 0 && index < activeDeck.Count)
            {
                activeDeck.RemoveAt(index);
                RefreshAllUI();
            }
        }

        private void ApplyRecommendedDeck()
        {
            activeDeck.Clear();

            foreach (DeckCardData card in ownedCollectionCards
                .OrderByDescending(card => card.attack + card.health)
                .ThenBy(card => card.cost)
                .ThenBy(card => card.cardName)
                .Take(deckSizeLimit))
            {
                activeDeck.Add(card);
            }

            RefreshAllUI();
        }

        private void ConfirmDeck()
        {
            if (!CanConfirmDeck())
            {
                UpdateDeckUIState();
                return;
            }

            if (profile == null)
            {
                return;
            }

            profile.activeDeckCardIds.Clear();
            foreach (DeckCardData card in activeDeck)
            {
                profile.activeDeckCardIds.Add(card.id);
            }

            SaveManager.Save();
            UpdateDeckUIState();
        }

        private void RefreshAllUI()
        {
            RefreshCollectionUI();
            RefreshDeckUI();
        }

        private void UpdateDeckUIState()
        {
            if (deckCounterText != null)
            {
                deckCounterText.text = $"Deck: <color=#FFD700>{activeDeck.Count}/{deckSizeLimit}</color> Cards";
            }

            if (deckStatsText != null)
            {
                deckStatsText.text = GetDeckStatsText();
            }

            if (deckStatusText != null)
            {
                deckStatusText.text = GetConfirmStatusText();
            }

            if (confirmDeckButton != null)
            {
                confirmDeckButton.interactable = CanConfirmDeck();
            }

            if (recommendedDeckButton != null)
            {
                recommendedDeckButton.interactable = ownedCollectionCards.Count > 0;
            }
        }

        private bool CanConfirmDeck()
        {
            if (ownedCollectionCards.Count == 0)
            {
                return false;
            }

            if (activeDeck.Count != deckSizeLimit)
            {
                return false;
            }

            HashSet<string> seenIds = new HashSet<string>();
            foreach (DeckCardData card in activeDeck)
            {
                if (!ownedCardsById.ContainsKey(card.id) || !seenIds.Add(card.id))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CanAddCard(DeckCardData card)
        {
            if (card == null || activeDeck.Count >= deckSizeLimit)
            {
                return false;
            }

            return !IsCardInDeck(card.id);
        }

        private bool IsCardInDeck(string cardId)
        {
            foreach (DeckCardData card in activeDeck)
            {
                if (card.id == cardId)
                {
                    return true;
                }
            }

            return false;
        }

        private string GetDeckStatsText()
        {
            int totalManaCost = 0;
            int totalAttack = 0;
            int totalHealth = 0;

            foreach (DeckCardData card in activeDeck)
            {
                totalManaCost += card.cost;
                totalAttack += card.attack;
                totalHealth += card.health;
            }

            float averageManaCost = activeDeck.Count > 0 ? (float)totalManaCost / activeDeck.Count : 0f;

            return
                $"Cards selected: {activeDeck.Count}/{deckSizeLimit}\n" +
                $"Total mana cost: {totalManaCost}\n" +
                $"Average mana cost: {averageManaCost:0.0}\n" +
                $"Total ATK: {totalAttack}\n" +
                $"Total HP: {totalHealth}";
        }

        private string GetConfirmStatusText()
        {
            if (ownedCollectionCards.Count == 0)
            {
                return "No owned cards are available to build a deck.";
            }

            if (activeDeck.Count < deckSizeLimit)
            {
                int missingCards = deckSizeLimit - activeDeck.Count;
                return $"Confirm disabled: add {missingCards} more card{(missingCards == 1 ? string.Empty : "s")} to reach {deckSizeLimit}.";
            }

            if (!CanConfirmDeck())
            {
                return "Confirm disabled: the deck must contain unique owned cards only.";
            }

            return "Deck ready to confirm and save.";
        }

        /// <summary>Destroys every dynamically-created UI GameObject directly parented under
        /// root - only ever called on collectionGridTransform/deckListTransform, whose entire
        /// contents this class builds itself each refresh, never a project asset or
        /// scene-authored object. Snapshots children before destroying rather than iterating
        /// root's own child enumerator while mutating it - required for correctness under
        /// DestroyDynamicUIObject's DestroyImmediate path (which removes a child from its
        /// parent immediately, unlike Destroy's end-of-frame removal), not just a style
        /// preference.</summary>
        private void DestroyAllChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                DestroyDynamicUIObject(root.GetChild(i).gameObject);
            }
        }

        /// <summary>Destroys a dynamically-created Deck Builder UI GameObject safely in both
        /// Play Mode and EditMode. EditMode tests (DeckPersistenceTests) drive this presenter's
        /// real refresh/confirm/back-button paths directly - Destroy() only schedules removal
        /// for the end of the current frame, which never arrives outside Play Mode, and Unity
        /// logs an error ("Destroy may not be called from edit mode!") if it's used there
        /// anyway. Every call site this feeds is a card entry, a deck row, or the whole
        /// dynamically-built DeckBuilderCanvas - never a project asset or scene-authored
        /// object, so DestroyImmediate is safe here specifically.</summary>
        private void DestroyDynamicUIObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private Text CreateTextElement(Transform parent, string objectName, string content, Vector2 position, int fontSize, TextAnchor alignment)
        {
            return CreateTextElement(parent, objectName, content, position, fontSize, alignment, new Vector2(500, 60));
        }

        private Text CreateTextElement(Transform parent, string objectName, string content, Vector2 position, int fontSize, TextAnchor alignment, Vector2 size)
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
    }
}
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
        private string entryStatusOverride;

        /// <summary>Soft first-open / incomplete-deck guidance — Campaign and To Battle need a confirmed deck.</summary>
        public const string ConfirmedDeckRequiredGuidance =
            "Confirm and save a 10-card deck before Campaign or To Battle.";

        public Text DeckStatusTextForTests => deckStatusText;

        /// <summary>entryStatusMessage: an optional one-time message shown on the existing status
        /// surface (deckStatusText) instead of the normal GetConfirmStatusText() readout, for a
        /// caller that redirected the player here for a specific reason - currently Home's "To
        /// Battle" gate, which needs to say why Deck Builder opened rather than Battle. Not a new
        /// status surface: BuildUI/UpdateDeckUIState still own deckStatusText, and the very next
        /// deck edit (add/remove a card) replaces this override with the normal readout, same as
        /// it always has.</summary>
        public void Initialize(System.Action onBackToHome, string entryStatusMessage = null)
        {
            this.onBackToHomeAction = onBackToHome;
            this.entryStatusOverride = entryStatusMessage;

            LoadProfileState();
            LoadOwnedCollectionCards();
            LoadSavedDeck();
            BuildUI();

            if (!string.IsNullOrEmpty(entryStatusOverride) && deckStatusText != null)
            {
                deckStatusText.text = entryStatusOverride;
            }
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

            if (profile == null)
            {
                return;
            }

            cardDatabase = EnsureCardDatabase();
            HashSet<string> seenIds = new HashSet<string>();

            if (profile.UsesCollectionV1 && profile.cardProgression != null)
            {
                foreach (CardProgressionRecord record in profile.cardProgression)
                {
                    if (record == null || string.IsNullOrEmpty(record.cardId) || record.copyCount < 1)
                    {
                        continue;
                    }

                    TryAddOwnedCardById(record.cardId, seenIds);
                }

                return;
            }

            if (profile.cardCollection == null)
            {
                return;
            }

            foreach (string cardId in profile.cardCollection)
            {
                TryAddOwnedCardById(cardId, seenIds);
            }
        }

        private void TryAddOwnedCardById(string cardId, HashSet<string> seenIds)
        {
            if (string.IsNullOrEmpty(cardId) || !seenIds.Add(cardId))
            {
                return;
            }

            Card resolved = cardDatabase != null ? cardDatabase.GetCard(cardId) : null;
            if (resolved == null)
            {
                return;
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
            TeardownUI();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();

            canvasObj = new GameObject("DeckBuilderCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObj.transform.SetParent(transform, false);
            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            GameObject bgObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgObj.transform.SetParent(canvasObj.transform, false);
            Image bgImg = bgObj.GetComponent<Image>();
            bgImg.color = new Color(0.035f, 0.055f, 0.075f, 1f);
            SetNormalizedRect(bgObj.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f);

            GameObject topBar = new GameObject("HeaderBar", typeof(RectTransform), typeof(Image));
            topBar.transform.SetParent(canvasObj.transform, false);
            Image topBarBg = topBar.GetComponent<Image>();
            if (!HomeV3UiLibrary.TryApplyHeaderFrame(topBarBg))
                topBarBg.color = new Color(0.045f, 0.085f, 0.11f, 1f);
            SetScreenRectFromTopLeftPixels(topBar.GetComponent<RectTransform>(), 0f, 0f, 1920f, 100f);

            Text title = CreateTextElement(topBar.transform, "Title", "DECK BUILDER", Vector2.zero, 34, TextAnchor.MiddleCenter, new Vector2(640, 64));
            title.color = new Color(0.91f, 0.95f, 0.86f);
            SetLocalNormalisedRect(title.rectTransform, 0.3f, 0.18f, 0.7f, 0.82f);

            deckCounterText = CreateTextElement(topBar.transform, "Counter", "", new Vector2(0, 0), 27, TextAnchor.MiddleRight, new Vector2(290, 58));
            deckCounterText.color = new Color(0.58f, 0.94f, 0.88f);
            SetLocalNormalisedRect(deckCounterText.rectTransform, 0.78f, 0.18f, 0.98f, 0.82f);

            BuildCollectionPanel();
            BuildDeckPanel();
            BuildActionRail();

            RefreshAllUI();
        }

        private void BuildCollectionPanel()
        {
            GameObject panelObj = new GameObject("CollectionPanel", typeof(RectTransform), typeof(Image));
            panelObj.transform.SetParent(canvasObj.transform, false);
            Image panelImg = panelObj.GetComponent<Image>();
            panelImg.color = new Color(0.055f, 0.09f, 0.115f, 1f);
            SetScreenRectFromTopLeftPixels(panelObj.GetComponent<RectTransform>(), 24f, 150f, 900f, 960f);

            Text header = CreateTextElement(panelObj.transform, "Header", "OWNED CARDS", Vector2.zero, 26, TextAnchor.MiddleLeft, new Vector2(400, 54));
            header.color = new Color(0.57f, 0.91f, 0.9f);
            SetLocalNormalisedRect(header.rectTransform, 0.03f, 0.92f, 0.5f, 0.995f);

            GameObject scrollObj = new GameObject("CollectionScroll", typeof(RectTransform), typeof(ScrollRect));
            scrollObj.transform.SetParent(panelObj.transform, false);
            SetLocalNormalisedRect(scrollObj.GetComponent<RectTransform>(), 0.02f, 0.03f, 0.98f, 0.90f);

            GameObject viewportObj = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportObj.transform.SetParent(scrollObj.transform, false);
            SetNormalizedRect(viewportObj.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f);

            GameObject contentObj = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            contentObj.transform.SetParent(viewportObj.transform, false);
            collectionGridTransform = contentObj.transform;

            RectTransform contentRect = contentObj.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            GridLayoutGroup grid = contentObj.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(190, 260);
            grid.spacing = new Vector2(16, 16);
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

            ContentSizeFitter fitter = contentObj.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect sr = scrollObj.GetComponent<ScrollRect>();
            sr.viewport = viewportObj.GetComponent<RectTransform>();
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
            panelImg.color = new Color(0.07f, 0.075f, 0.095f, 1f);
            SetScreenRectFromTopLeftPixels(panelObj.GetComponent<RectTransform>(), 918f, 150f, 1896f, 960f);

            Text header = CreateTextElement(panelObj.transform, "Header", "ACTIVE DECK", Vector2.zero, 26, TextAnchor.MiddleLeft, new Vector2(360, 54));
            header.color = new Color(0.94f, 0.75f, 0.4f);
            SetLocalNormalisedRect(header.rectTransform, 0.03f, 0.92f, 0.5f, 0.995f);

            deckStatsText = CreateTextElement(panelObj.transform, "DeckStats", "", Vector2.zero, 18, TextAnchor.MiddleCenter, new Vector2(720, 72));
            deckStatsText.color = new Color(0.86f, 0.9f, 0.82f);
            SetLocalNormalisedRect(deckStatsText.rectTransform, 0.03f, 0.06f, 0.97f, 0.17f);

            GameObject scrollObj = new GameObject("DeckScroll", typeof(RectTransform), typeof(ScrollRect));
            scrollObj.transform.SetParent(panelObj.transform, false);
            SetLocalNormalisedRect(scrollObj.GetComponent<RectTransform>(), 0.03f, 0.18f, 0.97f, 0.90f);

            GameObject viewportObj = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportObj.transform.SetParent(scrollObj.transform, false);
            SetNormalizedRect(viewportObj.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f);

            GameObject contentObj = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            contentObj.transform.SetParent(viewportObj.transform, false);
            deckListTransform = contentObj.transform;

            RectTransform contentRect = contentObj.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            GridLayoutGroup grid = contentObj.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(150, 205);
            grid.spacing = new Vector2(12, 12);
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;

            ContentSizeFitter fitter = contentObj.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect sr = scrollObj.GetComponent<ScrollRect>();
            sr.viewport = viewportObj.GetComponent<RectTransform>();
            sr.content = contentRect;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;

            deckEmptyText = CreateTextElement(panelObj.transform, "EmptyDeckState", "No cards in the deck yet. Tap an owned card to add it.", new Vector2(0, 36), 22, TextAnchor.MiddleCenter, new Vector2(600, 70));
            deckEmptyText.enabled = false;

            deckStatusText = CreateTextElement(panelObj.transform, "DeckStatus", "", Vector2.zero, 19, TextAnchor.MiddleCenter, new Vector2(720, 46));
            deckStatusText.color = new Color(0.9f, 0.82f, 0.64f);
            SetLocalNormalisedRect(deckStatusText.rectTransform, 0.03f, 0.0f, 0.97f, 0.05f);

        }

        private void BuildActionRail()
        {
            GameObject railObj = new GameObject("ActionRail", typeof(RectTransform), typeof(Image));
            railObj.transform.SetParent(canvasObj.transform, false);
            railObj.GetComponent<Image>().color = new Color(0.045f, 0.085f, 0.105f, 1f);
            SetScreenRectFromTopLeftPixels(railObj.GetComponent<RectTransform>(), 24f, 16f, 1896f, 120f);

            GameObject backBtnObj = CreateButton(railObj.transform, "Btn_Back_Rail", "< BACK", new Vector2(0, 0), new Vector2(210, 62), new Color(0.22f, 0.18f, 0.14f));
            SetNormalizedRect(backBtnObj.GetComponent<RectTransform>(), 0.02f, 0.18f, 0.18f, 0.82f);
            HomeV3UiLibrary.ApplyNavTileButton(backBtnObj.GetComponent<Button>(), backBtnObj.GetComponent<Image>());
            backBtnObj.GetComponent<Button>().onClick.AddListener(() =>
            {
                DestroyDynamicUIObject(canvasObj);
                onBackToHomeAction?.Invoke();
            });

            recommendedDeckButton = CreateButton(railObj.transform, "Btn_Recommended", "RECOMMENDED DECK", new Vector2(0, 0), new Vector2(320, 62), new Color(0.08f, 0.34f, 0.3f)).GetComponent<Button>();
            SetNormalizedRect(recommendedDeckButton.GetComponent<RectTransform>(), 0.39f, 0.18f, 0.61f, 0.82f);
            HomeV3UiLibrary.ApplyNavTileButton(recommendedDeckButton, recommendedDeckButton.GetComponent<Image>());
            recommendedDeckButton.onClick.AddListener(ApplyRecommendedDeck);

            confirmDeckButton = CreateButton(railObj.transform, "Btn_Confirm", "CONFIRM / SAVE DECK", new Vector2(0, 0), new Vector2(320, 62), new Color(0.66f, 0.43f, 0.14f)).GetComponent<Button>();
            SetNormalizedRect(confirmDeckButton.GetComponent<RectTransform>(), 0.80f, 0.18f, 0.98f, 0.82f);
            HomeV3UiLibrary.ApplyNavTileButton(confirmDeckButton, confirmDeckButton.GetComponent<Image>());
            confirmDeckButton.onClick.AddListener(ConfirmDeck);
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
                GameObject cardObj = CreateCardVisual($"Card_{card.id}", card, false);
                cardObj.transform.SetParent(collectionGridTransform, false);
                cardObj.transform.localScale = Vector3.one;

                Button btn = cardObj.GetComponent<Button>();
                DeckCardData capturedCard = card;
                btn.onClick.AddListener(() => AddCardToDeck(capturedCard));
                btn.interactable = CanAddCard(capturedCard);
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

                GameObject rowObj = CreateCardVisual($"DeckRow_{i}", card, true);
                rowObj.transform.SetParent(deckListTransform, false);
                rowObj.transform.localScale = Vector3.one;

                Button btn = rowObj.GetComponent<Button>();
                btn.onClick.AddListener(() => RemoveCardFromDeck(index));
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
                $"Deck {activeDeck.Count}/{deckSizeLimit}    Total Mana {totalManaCost}\n" +
                $"Average Mana {averageManaCost:0.0}\n" +
                $"Total ATK {totalAttack}    Total HP {totalHealth}";
        }

        private string GetConfirmStatusText()
        {
            if (ownedCollectionCards.Count == 0)
            {
                return $"{ConfirmedDeckRequiredGuidance} No owned cards are available yet.";
            }

            if (activeDeck.Count < deckSizeLimit)
            {
                int missingCards = deckSizeLimit - activeDeck.Count;
                return $"{ConfirmedDeckRequiredGuidance} Add {missingCards} more card{(missingCards == 1 ? string.Empty : "s")} ({activeDeck.Count}/{deckSizeLimit}).";
            }

            if (!CanConfirmDeck())
            {
                return $"{ConfirmedDeckRequiredGuidance} Deck must contain unique owned cards only.";
            }

            if (IsSavedDeckConfirmedAndMatchingActive())
            {
                return "Deck confirmed and ready for Campaign / To Battle.";
            }

            return "Deck complete — tap Confirm / Save Deck before Campaign or To Battle.";
        }

        /// <summary>True when the profile already has a legal confirmed deck that matches the
        /// current builder selection (post-Confirm Soft status).</summary>
        private bool IsSavedDeckConfirmedAndMatchingActive()
        {
            if (profile?.activeDeckCardIds == null || profile.activeDeckCardIds.Count != deckSizeLimit)
                return false;
            if (activeDeck.Count != deckSizeLimit)
                return false;

            var saved = new HashSet<string>(profile.activeDeckCardIds);
            if (saved.Count != deckSizeLimit)
                return false;

            foreach (DeckCardData card in activeDeck)
            {
                if (card == null || !saved.Contains(card.id))
                    return false;
            }

            return true;
        }

        private GameObject CreateCardVisual(string objectName, DeckCardData card, bool compact)
        {
            Vector2 cardSize = compact ? new Vector2(150f, 205f) : new Vector2(190f, 260f);
            GameObject cardObj = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            Image cardBackground = cardObj.GetComponent<Image>();
            cardBackground.color = new Color(0.08f, 0.10f, 0.14f, 0.75f);
            Button cardButton = cardObj.GetComponent<Button>();
            cardButton.transition = Selectable.Transition.ColorTint;
            cardButton.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(0.8f, 1f, 1f, 1f),
                pressedColor = new Color(0.7f, 0.9f, 0.95f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.45f, 0.48f, 0.5f, 1f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };

            GameObject borderObj = new GameObject("CardBorder", typeof(RectTransform), typeof(Image));
            borderObj.transform.SetParent(cardObj.transform, false);
            Image borderImage = borderObj.GetComponent<Image>();
            borderImage.color = new Color(0.28f, 0.35f, 0.40f, 0.90f);
            borderImage.raycastTarget = false;
            SetLocalNormalisedRect(borderImage.rectTransform, 0.02f, 0.02f, 0.98f, 0.98f);

            GameObject artViewport = new GameObject("ArtViewport", typeof(RectTransform), typeof(RectMask2D));
            artViewport.transform.SetParent(cardObj.transform, false);
            SetLocalNormalisedRect(artViewport.GetComponent<RectTransform>(), 0.08f, 0.28f, 0.92f, 0.84f);

            GameObject artObj = new GameObject("Art", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            artObj.transform.SetParent(artViewport.transform, false);

            Image artImage = artObj.GetComponent<Image>();
            Card resolved = cardDatabase != null ? cardDatabase.GetCard(card.id) : null;
            Sprite art = resolved != null && cardDatabase != null ? cardDatabase.GetArt(resolved) : null;
            artImage.sprite = art;
            artImage.color = art != null ? Color.white : Color.clear;
            artImage.preserveAspect = true;
            artImage.raycastTarget = false;

            RectTransform artRect = artObj.GetComponent<RectTransform>();
            artRect.anchorMin = Vector2.zero;
            artRect.anchorMax = Vector2.one;
            artRect.offsetMin = Vector2.zero;
            artRect.offsetMax = Vector2.zero;

            AspectRatioFitter artFitter = artObj.GetComponent<AspectRatioFitter>();
            artFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            artFitter.aspectRatio = art != null ? art.rect.width / art.rect.height : 1f;

            Text name = CreateTextElement(cardObj.transform, "Name", card.cardName, Vector2.zero, compact ? 14 : 16, TextAnchor.MiddleCenter, new Vector2(160f, 30f));
            name.color = new Color(0.95f, 0.94f, 0.84f);
            SetLocalNormalisedRect(name.rectTransform, 0.08f, 0.17f, 0.92f, 0.29f);

            Text type = CreateTextElement(cardObj.transform, "Type", card.archetype, Vector2.zero, compact ? 11 : 12, TextAnchor.MiddleCenter, new Vector2(160f, 24f));
            type.color = new Color(0.55f, 0.85f, 0.84f);
            SetLocalNormalisedRect(type.rectTransform, 0.08f, 0.08f, 0.92f, 0.17f);

            Text stats = CreateTextElement(cardObj.transform, "Stats", $"COST {card.cost}   ATK {card.attack}   HP {card.health}", Vector2.zero, compact ? 10 : 12, TextAnchor.MiddleCenter, new Vector2(180f, 24f));
            stats.color = new Color(0.9f, 0.95f, 0.9f);
            SetLocalNormalisedRect(stats.rectTransform, 0.04f, 0.01f, 0.96f, 0.09f);

            if (!compact && IsCardInDeck(card.id))
            {
                var cardBorderOutline = cardObj.AddComponent<Outline>();
                cardBorderOutline.effectColor = new Color(0.88f, 0.76f, 0.28f, 1f);
                cardBorderOutline.effectDistance = new Vector2(2f, -2f);
            }

            return cardObj;
        }

        private static void SetLocalNormalisedRect(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetScreenRectFromTopLeftPixels(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = new Vector2(left / 1920f, 1f - bottom / 1080f);
            rect.anchorMax = new Vector2(right / 1920f, 1f - top / 1080f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetNormalizedRect(RectTransform rect, float left, float bottom, float right, float top)
        {
            SetLocalNormalisedRect(rect, left, bottom, right, top);
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

        public void TeardownUI()
        {
            if (canvasObj == null) return;
            canvasObj.SetActive(false);
            DestroyDynamicUIObject(canvasObj);
            canvasObj = null;
        }

        private void OnDestroy()
        {
            TeardownUI();
        }
    }
}
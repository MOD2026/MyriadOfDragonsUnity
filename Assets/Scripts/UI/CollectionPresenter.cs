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
    /// <summary>Collection grid sort modes (Block X). Default preserves load order.</summary>
    public enum CollectionSortMode
    {
        Default = 0,
        NameAscending = 1,
        RarityDescending = 2,
    }

    /// <summary>
    /// Read-only Collection screen for P1 Task 1.
    /// Displays owned cards from <see cref="PlayerProfile.cardProgression"/> (V1) or legacy list.
    /// V1 duplicates can be burned or evolved via atomic save services.
    /// </summary>
    public class CollectionPresenter : MonoBehaviour
    {
        private GameObject _canvasObj;
        private Action _onBackToHomeAction;
        private Action _onOpenDeckBuilderAction;

        private Text _ownedCountText;
        private Text _goldPillText;
        private Text _permitPillText;
        private InputField _searchInput;
        private Text _classFilterLabel;
        private Transform _gridRoot;
        private Text _emptyStateText;

        /// <summary>Empty grid when the player owns nothing.</summary>
        public const string EmptyNoOwnedCopy = "No owned cards to display.";
        /// <summary>Empty grid when search and/or class filter hide every owned card.</summary>
        public const string EmptyNoMatchCopy = "No cards match your search or filter.";

        /// <summary>Null = All classes. Block V class filter.</summary>
        private CardClass? _classFilter;
        private CollectionSortMode _sortMode = CollectionSortMode.Default;
        private Text _sortLabel;

        private GameObject _detailPanel;
        private Transform _detailActionsRoot;
        private Text _detailTitleText;
        private Text _detailBodyText;
        private Text _detailStatusText;
        private Image _detailArtImage;
        private OwnedCardViewModel _selectedCard;

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
            TeardownUI();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();

            _canvasObj = new GameObject("CollectionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = _canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

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
            headerImg.sprite = null;
            headerImg.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);

            RectTransform headerRect = headerObj.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = Vector2.zero;
            headerRect.sizeDelta = new Vector2(0f, 110f);

            CreateTextElement(headerObj.transform, "Title", "CARD COLLECTION", new Vector2(-420f, 0f), 34, TextAnchor.MiddleLeft, new Vector2(820f, 70f));

            // Gold + Permit pills — Evolve spends both; keep balances visible (MVP clarity).
            GameObject currencyGroup = new GameObject("CurrencyPillGroup", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            currencyGroup.transform.SetParent(headerObj.transform, false);
            RectTransform currencyGroupRect = currencyGroup.GetComponent<RectTransform>();
            currencyGroupRect.anchorMin = new Vector2(1f, 0.5f);
            currencyGroupRect.anchorMax = new Vector2(1f, 0.5f);
            currencyGroupRect.pivot = new Vector2(1f, 0.5f);
            currencyGroupRect.anchoredPosition = new Vector2(-400f, 0f);
            currencyGroupRect.sizeDelta = new Vector2(400f, 56f);
            HorizontalLayoutGroup currencyHlg = currencyGroup.GetComponent<HorizontalLayoutGroup>();
            currencyHlg.childAlignment = TextAnchor.MiddleRight;
            currencyHlg.spacing = 10f;
            currencyHlg.childControlWidth = false;

            PlayerProfile headerProfile = SaveManager.SaveData;
            int gold = headerProfile != null ? headerProfile.gold : 0;
            int permits = headerProfile != null ? headerProfile.ascensionPermitBalance : 0;
            _goldPillText = HomeV3UiLibrary.CreateResourcePill(currencyGroup.transform, "home_resource_gold_pill_v3",
                "Gold", $"{gold}", 180f);
            // Reuse gems pill frame — no dedicated Permit art yet; label makes the wallet clear.
            _permitPillText = HomeV3UiLibrary.CreateResourcePill(currencyGroup.transform, "home_resource_gems_pill_v3",
                "Permits", $"{permits}/{CollectionSchemaRules.AscensionPermitHoardCap}", 200f);

            _ownedCountText = CreateTextElement(headerObj.transform, "OwnedCount", "Owned: 0", new Vector2(520f, 0f), 26, TextAnchor.MiddleRight, new Vector2(360f, 70f));
        }

        private void RefreshGoldPill()
        {
            if (_goldPillText == null) return;
            PlayerProfile profile = SaveManager.SaveData;
            _goldPillText.text = profile != null ? $"{profile.gold}" : "0";
        }

        private void RefreshPermitPill()
        {
            if (_permitPillText == null) return;
            PlayerProfile profile = SaveManager.SaveData;
            int balance = profile != null ? profile.ascensionPermitBalance : 0;
            _permitPillText.text = $"{balance}/{CollectionSchemaRules.AscensionPermitHoardCap}";
        }

        private void RefreshCurrencyPills()
        {
            RefreshGoldPill();
            RefreshPermitPill();
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

            _classFilterLabel = CreateClassFilterControl(controlsObj.transform, new Vector2(300f, 0f), new Vector2(260f, 60f));
            _sortLabel = CreateSortControl(controlsObj.transform, new Vector2(580f, 0f), new Vector2(260f, 60f));
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

            _detailBodyText = CreateTextElement(_detailPanel.transform, "DetailBody", "Tap any owned card in the grid to inspect details.", new Vector2(0f, -120f), 20, TextAnchor.UpperCenter, new Vector2(420f, 200f));

            GameObject actionsObj = new GameObject("DetailActions", typeof(RectTransform));
            actionsObj.transform.SetParent(_detailPanel.transform, false);
            _detailActionsRoot = actionsObj.transform;
            RectTransform actionsRect = actionsObj.GetComponent<RectTransform>();
            actionsRect.anchorMin = new Vector2(0.5f, 0f);
            actionsRect.anchorMax = new Vector2(0.5f, 0f);
            actionsRect.pivot = new Vector2(0.5f, 0f);
            actionsRect.anchoredPosition = new Vector2(0f, 24f);
            actionsRect.sizeDelta = new Vector2(400f, 180f);

            _detailStatusText = CreateTextElement(_detailPanel.transform, "DetailStatus", string.Empty, new Vector2(0f, -300f), 18, TextAnchor.UpperCenter, new Vector2(420f, 60f));
            _detailStatusText.color = new Color(0.75f, 0.9f, 0.75f);
        }

        private void BuildBottomButtons()
        {
            GameObject backBtnObj = CreateButton(_canvasObj.transform, "BackButton", "< BACK", new Vector2(-560f, 60f), new Vector2(240f, 70f), new Color(0.28f, 0.2f, 0.2f));
            HomeV3UiLibrary.ApplyNavTileButton(backBtnObj.GetComponent<Button>(), backBtnObj.GetComponent<Image>());
            backBtnObj.GetComponent<Button>().onClick.AddListener(() =>
            {
                if (_canvasObj != null) Destroy(_canvasObj);
                _onBackToHomeAction?.Invoke();
            });

            GameObject deckBtnObj = CreateButton(_canvasObj.transform, "OpenDeckBuilderButton", "OPEN DECK BUILDER", new Vector2(560f, 60f), new Vector2(360f, 70f), new Color(0.18f, 0.4f, 0.28f));
            HomeV3UiLibrary.ApplyNavTileButton(deckBtnObj.GetComponent<Button>(), deckBtnObj.GetComponent<Image>());
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
            if (profile == null) return;

            CardDatabase db = EnsureCardDatabase();

            if (profile.UsesCollectionV1 && profile.cardProgression != null)
            {
                foreach (CardProgressionRecord record in profile.cardProgression)
                {
                    if (record == null || string.IsNullOrEmpty(record.cardId) || record.copyCount < 1) continue;
                    TryAddOwnedCard(db, record.cardId, record.copyCount);
                }

                return;
            }

            if (profile.cardCollection == null) return;

            foreach (string cardId in profile.cardCollection)
            {
                if (string.IsNullOrEmpty(cardId)) continue;
                TryAddOwnedCard(db, cardId, 1);
            }
        }

        private void TryAddOwnedCard(CardDatabase db, string cardId, int copyCount)
        {
            Card resolved = db != null ? db.GetCard(cardId) : null;
            string displayName = resolved != null ? resolved.DisplayName : cardId;
            if (copyCount > 1) displayName += $" ×{copyCount}";

            _allCards.Add(new OwnedCardViewModel
            {
                CardId = cardId,
                DisplayName = displayName,
                CopyCount = copyCount,
                Class = resolved != null ? resolved.Class : (CardClass?)null,
                Archetype = resolved != null ? resolved.Class.ToString() : "Unknown",
                Rarity = resolved != null ? resolved.Rarity : 0,
                Cost = resolved != null ? resolved.ResourceCost : 0,
                Attack = resolved != null ? resolved.Attack : 0,
                Health = resolved != null ? resolved.Health : 0,
                Art = (db != null && resolved != null) ? db.GetArt(resolved) : null,
            });
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

            _filter = _classFilter.HasValue
                ? (ICardFilter)new ClassCardFilter(_classFilter.Value)
                : new NoOpCardFilter();
            _sort = CreateSortForMode(_sortMode);
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
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }

            bool hasAny = _filteredCards.Count > 0;
            _emptyStateText.gameObject.SetActive(!hasAny);

            if (!hasAny)
            {
                _emptyStateText.text = _allCards.Count == 0 ? EmptyNoOwnedCopy : EmptyNoMatchCopy;
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
            tileObj.GetComponent<RectTransform>().sizeDelta = new Vector2(210f, 280f);

            Image hit = tileObj.GetComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0.01f);
            Button tileBtn = tileObj.GetComponent<Button>();
            tileBtn.targetGraphic = hit;

            GameObject baseObj = new GameObject("OpaqueCardBase", typeof(RectTransform), typeof(Image));
            baseObj.transform.SetParent(tileObj.transform, false);
            Image baseImg = baseObj.GetComponent<Image>();
            int rarity = ResolveRarity(card.CardId);
            Sprite frame = HomeV3UiLibrary.LoadCardFrameForRarity(rarity);
            if (frame != null)
            {
                baseImg.sprite = frame;
                baseImg.preserveAspect = true;
                baseImg.color = Color.white;
            }
            else
            {
                baseImg.color = new Color(0.19f, 0.23f, 0.32f, 1f);
            }
            baseImg.raycastTarget = false;
            RectTransform baseRect = baseObj.GetComponent<RectTransform>();
            baseRect.anchorMin = Vector2.zero;
            baseRect.anchorMax = Vector2.one;
            baseRect.offsetMin = Vector2.zero;
            baseRect.offsetMax = Vector2.zero;

            GameObject artMask = new GameObject("CardArtMask", typeof(RectTransform), typeof(RectMask2D));
            artMask.transform.SetParent(tileObj.transform, false);
            RectTransform maskRect = artMask.GetComponent<RectTransform>();
            maskRect.anchorMin = new Vector2(0.08f, 0.33f);
            maskRect.anchorMax = new Vector2(0.92f, 0.87f);
            maskRect.offsetMin = Vector2.zero;
            maskRect.offsetMax = Vector2.zero;

            GameObject artObj = new GameObject("CardArt", typeof(RectTransform), typeof(Image));
            artObj.transform.SetParent(artMask.transform, false);
            Image artImg = artObj.GetComponent<Image>();
            artImg.preserveAspect = true;
            artImg.color = card.Art != null ? Color.white : new Color(0.3f, 0.32f, 0.4f);
            artImg.sprite = card.Art;
            artImg.raycastTarget = false;
            RectTransform artRect = artObj.GetComponent<RectTransform>();
            artRect.anchorMin = Vector2.zero;
            artRect.anchorMax = Vector2.one;
            artRect.offsetMin = Vector2.zero;
            artRect.offsetMax = Vector2.zero;

            CreateTextElement(tileObj.transform, "Cost", $"{card.Cost}", new Vector2(-70f, 110f), 18, TextAnchor.MiddleCenter, new Vector2(48f, 36f));
            CreateTextElement(tileObj.transform, "Name", card.DisplayName, new Vector2(0f, -50f), 16, TextAnchor.MiddleCenter, new Vector2(190f, 40f));
            CreateTextElement(tileObj.transform, "AtkStat", $"ATK {card.Attack}", new Vector2(-48f, -100f), 16, TextAnchor.MiddleCenter, new Vector2(100f, 32f));
            CreateTextElement(tileObj.transform, "HpStat", $"HP {card.Health}", new Vector2(48f, -100f), 16, TextAnchor.MiddleCenter, new Vector2(100f, 32f));

            return tileObj;
        }

        private void ShowDetail(OwnedCardViewModel card)
        {
            _selectedCard = card;
            _detailTitleText.text = card.DisplayName;
            _detailArtImage.sprite = card.Art;
            _detailArtImage.color = card.Art != null ? Color.white : new Color(0.22f, 0.24f, 0.3f, 1f);

            PlayerProfile profile = SaveManager.SaveData;
            CardProgressionRecord record = profile != null && profile.UsesCollectionV1
                ? CollectionProgression.FindRecordForTests(profile, card.CardId)
                : null;

            var body = new System.Text.StringBuilder();
            body.AppendLine($"Card ID: {card.CardId}");
            body.AppendLine($"Copies: {card.CopyCount}");
            body.AppendLine($"Class: {card.Archetype}");
            body.AppendLine($"Cost: {card.Cost}   ATK: {card.Attack}   HP: {card.Health}");
            if (record != null)
            {
                body.AppendLine($"Level: {record.cardLevel}   Evo step: {record.evolutionStep}");
                body.AppendLine($"Training XP: {record.trainingXp}");
                if (profile.collectionWallet != null)
                {
                    int rarity = ResolveRarity(card.CardId);
                    int dust = CollectionEvolutionRules.GetDustBalance(profile.collectionWallet, rarity);
                    body.AppendLine(
                        $"Forge: {profile.collectionWallet.forgeCredits}   Dust ({rarity}★): {dust}   Sacrifice: {profile.collectionWallet.genericSacrificeCredits}");
                    body.AppendLine(CollectionPlayerCopy.ForgeDustWalletCaption);
                }
            }

            _detailBodyText.text = body.ToString().TrimEnd();
            _detailStatusText.text = string.Empty;
            RebuildDetailActions(card, record, profile);
        }

        private void RebuildDetailActions(OwnedCardViewModel card, CardProgressionRecord record, PlayerProfile profile)
        {
            foreach (Transform child in _detailActionsRoot)
            {
                Destroy(child.gameObject);
            }

            if (profile == null || !profile.UsesCollectionV1 || record == null)
            {
                return;
            }

            if (card.CopyCount < 2)
            {
                _detailStatusText.text = CollectionPlayerCopy.BurnEmptyState;
                return;
            }

            float y = 130f;
            CreateDetailActionButton("Burn → XP", new Vector2(0f, y), new Color(0.22f, 0.38f, 0.28f),
                () => AttemptBurn(CollectionBurnPath.TrainingXp));
            CreateDetailActionButton("Burn → Sacrifice", new Vector2(0f, y - 44f), new Color(0.28f, 0.32f, 0.42f),
                () => AttemptBurn(CollectionBurnPath.GenericSacrifice));
            CreateDetailActionButton("Burn → Forge", new Vector2(0f, y - 88f), new Color(0.38f, 0.28f, 0.2f),
                () => AttemptBurn(CollectionBurnPath.ForgeCredit));
            CreateDetailActionButton("Burn → Dust", new Vector2(0f, y - 132f), new Color(0.32f, 0.28f, 0.38f),
                () => AttemptBurn(CollectionBurnPath.Dust));

            if (record.evolutionStep < CollectionEvolutionRules.MaxEvolutionStep)
            {
                int rarity = ResolveRarity(card.CardId);
                string evolveLabel = FormatEvolveButtonLabel(profile, rarity, record.evolutionStep);
                CreateDetailActionButton(evolveLabel, new Vector2(0f, y - 176f), new Color(0.45f, 0.22f, 0.38f), AttemptEvolve);
            }
        }

        /// <summary>Evolve button cost label using live Gold due after Forge/Dust offsets.</summary>
        public static string FormatEvolveButtonLabel(PlayerProfile profile, int rarity, int evolutionStep)
        {
            int baseGold = CollectionEvolutionRules.GoldCostForNextStep(rarity, evolutionStep);
            int goldDue = CollectionEvolutionRules.ComputeGoldDue(
                profile, rarity, baseGold, out _, out _);
            int sacrificeRequired = CollectionEvolutionRules.GenericSacrificeCreditsRequired(evolutionStep);
            bool needsPermit = CollectionEvolutionRules.RequiresAscensionPermit(evolutionStep);

            var costParts = new System.Collections.Generic.List<string>();
            if (goldDue < baseGold)
                costParts.Add($"{goldDue}g (was {baseGold}g)");
            else
                costParts.Add($"{goldDue}g");

            if (sacrificeRequired > 0)
                costParts.Add($"{sacrificeRequired} Sacrifice");
            if (needsPermit)
                costParts.Add("Permit");

            return $"Evolve ({string.Join(" + ", costParts)})";
        }

        private void CreateDetailActionButton(string label, Vector2 pos, Color tint, UnityEngine.Events.UnityAction action)
        {
            GameObject btnObj = CreateButton(_detailActionsRoot, label.Replace(" ", ""), label, pos, new Vector2(360f, 38f), tint);
            btnObj.GetComponent<Button>().onClick.AddListener(action);
        }

        private void AttemptBurn(CollectionBurnPath path)
        {
            if (_selectedCard == null) return;
            PlayerProfile profile = SaveManager.SaveData;
            string receiptId = System.Guid.NewGuid().ToString("N");
            if (!CollectionBurnService.TryBurnCopy(profile, _selectedCard.CardId, path, receiptId, out CollectionBurnReceiptResult result))
            {
                _detailStatusText.text = result.Error == CollectionBurnError.InsufficientCopies
                    ? CollectionPlayerCopy.BurnEmptyState
                    : $"Burn failed: {result.Error}";
                return;
            }

            SaveManager.Save();
            _detailStatusText.text = $"Burned 1 copy → +{result.YieldAmount} ({path}).";
            RefreshCurrencyPills();
            LoadOwnedCards();
            ApplySearchFilterSortAndRender();
            OwnedCardViewModel refreshed = _allCards.Find(c => c.CardId == _selectedCard.CardId);
            if (refreshed != null) ShowDetail(refreshed);
        }

        private void AttemptEvolve()
        {
            if (_selectedCard == null) return;
            PlayerProfile profile = SaveManager.SaveData;
            string receiptId = System.Guid.NewGuid().ToString("N");
            if (!CollectionEvolutionService.TryEvolve(profile, _selectedCard.CardId, receiptId, out CollectionEvolutionReceiptResult result))
            {
                _detailStatusText.text = IsEvolutionShortage(result.Error)
                    ? CollectionPlayerCopy.EvolutionShortage
                    : $"Evolve failed: {result.Error}";
                return;
            }

            SaveManager.Save();
            _detailStatusText.text = $"Evolved to step {result.EvolutionStepAfter} (−{result.GoldSpent} Gold).";
            RefreshCurrencyPills();
            LoadOwnedCards();
            ApplySearchFilterSortAndRender();
            OwnedCardViewModel refreshed = _allCards.Find(c => c.CardId == _selectedCard.CardId);
            if (refreshed != null) ShowDetail(refreshed);
        }

        private static bool IsEvolutionShortage(CollectionEvolutionError error) =>
            error == CollectionEvolutionError.InsufficientGold
            || error == CollectionEvolutionError.InsufficientCopies
            || error == CollectionEvolutionError.InsufficientSacrificeCredits
            || error == CollectionEvolutionError.PermitRequired;

        /// <summary>Exposed for EditMode: detail body after ShowDetail.</summary>
        public string DetailBodyTextForTests => _detailBodyText != null ? _detailBodyText.text : null;

        /// <summary>Exposed for EditMode: empty-state copy currently shown (null if grid has tiles).</summary>
        public string EmptyStateTextForTests =>
            _emptyStateText != null && _emptyStateText.gameObject.activeSelf ? _emptyStateText.text : null;

        /// <summary>Exposed for EditMode: visible grid card ids after search/filter/sort.</summary>
        public IReadOnlyList<string> VisibleCardIdsForTests =>
            _filteredCards.Select(c => c.CardId).ToList();

        /// <summary>Exposed for EditMode: class filter label (e.g. "Filter: All").</summary>
        public string ClassFilterLabelForTests => _classFilterLabel != null ? _classFilterLabel.text : null;

        /// <summary>Exposed for EditMode: sort label (e.g. "Sort: Default").</summary>
        public string SortLabelForTests => _sortLabel != null ? _sortLabel.text : null;

        /// <summary>Exposed for EditMode: set class filter (null = All) and re-render.</summary>
        public void SetClassFilterForTests(CardClass? classFilter)
        {
            _classFilter = classFilter;
            RefreshClassFilterLabel();
            ApplySearchFilterSortAndRender();
        }

        /// <summary>Exposed for EditMode: set sort mode and re-render.</summary>
        public void SetSortModeForTests(CollectionSortMode mode)
        {
            _sortMode = mode;
            RefreshSortLabel();
            ApplySearchFilterSortAndRender();
        }

        /// <summary>Exposed for EditMode: set search box text and re-render.</summary>
        public void SetSearchTextForTests(string search)
        {
            if (_searchInput != null) _searchInput.text = search ?? string.Empty;
            ApplySearchFilterSortAndRender();
        }

        /// <summary>Exposed for EditMode: select owned card by id and refresh detail panel.</summary>
        public void ShowDetailForTests(string cardId)
        {
            OwnedCardViewModel card = _allCards.Find(c => c.CardId == cardId);
            if (card != null) ShowDetail(card);
        }

        /// <summary>Exposed for EditMode: evolve button label currently shown in detail actions.</summary>
        public string EvolveButtonLabelForTests()
        {
            if (_detailActionsRoot == null) return null;
            foreach (Transform child in _detailActionsRoot)
            {
                Text label = child.GetComponentInChildren<Text>();
                if (label != null && label.text != null && label.text.StartsWith("Evolve ("))
                    return label.text;
            }

            return null;
        }

        private static int ResolveRarity(string cardId)
        {
            CardDatabase db = CardDatabase.Instance;
            Card card = db != null ? db.GetCard(cardId) : null;
            return card?.Rarity ?? 1;
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

        /// <summary>Class filter control (Block V). Cycles All → each <see cref="CardClass"/> on tap.</summary>
        private Text CreateClassFilterControl(Transform parent, Vector2 position, Vector2 size)
        {
            GameObject root = new GameObject("ClassFilter", typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            root.transform.localScale = Vector3.one;

            Image bg = root.GetComponent<Image>();
            bg.color = new Color(0.22f, 0.28f, 0.36f, 1f);

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Text label = CreateTextElement(root.transform, "Label", FormatClassFilterLabel(), Vector2.zero, 18, TextAnchor.MiddleCenter, new Vector2(size.x - 10f, size.y - 8f));
            Button button = root.GetComponent<Button>();
            HomeV3UiLibrary.ApplyNavTileButton(button, root.GetComponent<Image>());
            button.onClick.AddListener(CycleClassFilter);
            return label;
        }

        private void CycleClassFilter()
        {
            if (!_classFilter.HasValue)
            {
                _classFilter = CardClass.Warrior;
            }
            else
            {
                int next = (int)_classFilter.Value + 1;
                CardClass[] all = (CardClass[])Enum.GetValues(typeof(CardClass));
                _classFilter = next < all.Length ? all[next] : (CardClass?)null;
            }

            RefreshClassFilterLabel();
            ApplySearchFilterSortAndRender();
        }

        private void RefreshClassFilterLabel()
        {
            if (_classFilterLabel != null)
                _classFilterLabel.text = FormatClassFilterLabel();
        }

        private string FormatClassFilterLabel() =>
            _classFilter.HasValue ? $"Filter: {_classFilter.Value}" : "Filter: All";

        /// <summary>Sort control (Block X). Cycles Default → Name A–Z → Rarity High→Low.</summary>
        private Text CreateSortControl(Transform parent, Vector2 position, Vector2 size)
        {
            GameObject root = new GameObject("CollectionSort", typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            root.transform.localScale = Vector3.one;

            Image bg = root.GetComponent<Image>();
            bg.color = new Color(0.22f, 0.28f, 0.36f, 1f);

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Text label = CreateTextElement(root.transform, "Label", FormatSortLabel(), Vector2.zero, 18, TextAnchor.MiddleCenter, new Vector2(size.x - 10f, size.y - 8f));
            Button button = root.GetComponent<Button>();
            HomeV3UiLibrary.ApplyNavTileButton(button, root.GetComponent<Image>());
            button.onClick.AddListener(CycleSortMode);
            return label;
        }

        private void CycleSortMode()
        {
            int next = ((int)_sortMode + 1) % 3;
            _sortMode = (CollectionSortMode)next;
            RefreshSortLabel();
            ApplySearchFilterSortAndRender();
        }

        private void RefreshSortLabel()
        {
            if (_sortLabel != null)
                _sortLabel.text = FormatSortLabel();
        }

        private string FormatSortLabel()
        {
            switch (_sortMode)
            {
                case CollectionSortMode.NameAscending: return "Sort: Name A-Z";
                case CollectionSortMode.RarityDescending: return "Sort: Rarity";
                default: return "Sort: Default";
            }
        }

        private static ICardSort CreateSortForMode(CollectionSortMode mode)
        {
            switch (mode)
            {
                case CollectionSortMode.NameAscending: return new NameAscendingSort();
                case CollectionSortMode.RarityDescending: return new RarityDescendingSort();
                default: return new NoOpCardSort();
            }
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
            public int CopyCount = 1;
            public string DisplayName;
            public CardClass? Class;
            public string Archetype;
            public int Rarity;
            public int Cost;
            public int Attack;
            public int Health;
            public Sprite Art;
        }

        // Reusable interfaces for filter/sort (Blocks V/X).
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

        private sealed class ClassCardFilter : ICardFilter
        {
            private readonly CardClass _required;

            public ClassCardFilter(CardClass required) => _required = required;

            public IEnumerable<OwnedCardViewModel> Apply(IEnumerable<OwnedCardViewModel> cards) =>
                cards.Where(c => c.Class.HasValue && c.Class.Value == _required);
        }

        private sealed class NoOpCardSort : ICardSort
        {
            public IEnumerable<OwnedCardViewModel> Apply(IEnumerable<OwnedCardViewModel> cards) => cards;
        }

        private sealed class NameAscendingSort : ICardSort
        {
            public IEnumerable<OwnedCardViewModel> Apply(IEnumerable<OwnedCardViewModel> cards) =>
                cards.OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(c => c.CardId, StringComparer.OrdinalIgnoreCase);
        }

        private sealed class RarityDescendingSort : ICardSort
        {
            public IEnumerable<OwnedCardViewModel> Apply(IEnumerable<OwnedCardViewModel> cards) =>
                cards.OrderByDescending(c => c.Rarity)
                    .ThenBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(c => c.CardId, StringComparer.OrdinalIgnoreCase);
        }

        public void TeardownUI()
        {
            if (_canvasObj == null) return;
            _canvasObj.SetActive(false);
            if (Application.isPlaying) Destroy(_canvasObj);
            else DestroyImmediate(_canvasObj);
            _canvasObj = null;
            _detailPanel = null;
        }

        private void OnDestroy()
        {
            TeardownUI();
        }
    }
}

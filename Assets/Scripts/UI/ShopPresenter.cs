using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Economy;
using MyriadOfDragons.Save;

namespace MyriadOfDragons.UI
{
    public class ShopItemData
    {
        public string id;
        public string title;
        public string description;
        public int goldCost;
        public int gemCost;

        /// <summary>Grants the item's reward and returns whether it could actually be fulfilled.
        /// Returning false (the card reward sequence is exhausted - see
        /// ShopPresenter.TryGrantNextUnownedCard) must leave the profile completely untouched;
        /// AttemptPurchase relies on that to decide whether currency is spent at all.</summary>
        public System.Func<PlayerProfile, bool> onPurchase;

        /// <summary>When true, <see cref="onPurchase"/> already spent currency and saved (e.g. pack receipt).</summary>
        public bool walletCommittedByCallback;

        /// <summary>
        /// When true, omitted from the live Shop grid but still reachable via
        /// <see cref="ShopPresenter.PurchaseForTests"/> (V1 EditMode contracts).
        /// Used to withhold SKUs that violate SHOP_V2_NUMBERS_PACKET without deleting test harnesses.
        /// </summary>
        public bool hideFromShopGrid;

        public ShopItemData(string id, string title, string desc, int goldCost, int gemCost,
            System.Func<PlayerProfile, bool> onPurchase, bool walletCommittedByCallback = false,
            bool hideFromShopGrid = false)
        {
            this.id = id;
            this.title = title;
            this.description = desc;
            this.goldCost = goldCost;
            this.gemCost = gemCost;
            this.onPurchase = onPurchase;
            this.walletCommittedByCallback = walletCommittedByCallback;
            this.hideFromShopGrid = hideFromShopGrid;
        }
    }

    public class ShopPresenter : MonoBehaviour
    {
        private GameObject canvasObj;
        private PlayerProfile player;
        private System.Action onBackToHomeAction;
        private System.Action onOpenCollectionAction;

        private Text goldText;
        private Text gemsText;
        private Text energyText;
        private Text statusText;
        private Text pityBannerText;
        private readonly List<Text> packPityLineTexts = new List<Text>();
        private readonly Dictionary<int, Image> staminaTierImages = new Dictionary<int, Image>();

        private List<ShopItemData> shopItems;
        private PackReceiptResult _pendingPackReceipt;

        public void Initialize(PlayerProfile profile, System.Action onBackToHome, System.Action onOpenCollection = null)
        {
            this.player = profile;
            this.onBackToHomeAction = onBackToHome;
            this.onOpenCollectionAction = onOpenCollection;

            if (player != null)
                ShopStaminaCatalog.NormalizeLadderFields(player);

            SetupShopItems();
            BuildUI();
            RefreshStaminaBuyButtons();
        }

        /// <summary>Exposed for tests: the real "BUY" button calls the private AttemptPurchase()
        /// directly - EditMode tests have no way to click a UI Button, so this is the only way to
        /// exercise the real purchase handler (affordability gate, reward fulfillment, and the
        /// currency-spend-only-on-fulfillment ordering) rather than reimplementing it in a test.
        /// Returns whether the item existed by id - not whether the purchase itself succeeded, so
        /// a test asserts the real, resulting profile state rather than a return value standing
        /// in for it.</summary>
        public bool PurchaseForTests(string itemId)
        {
            ShopItemData item = shopItems?.Find(i => i.id == itemId);
            if (item == null) return false;
            AttemptPurchase(item);
            return true;
        }

        /// <summary>
        /// The one card a pack purchase actually placed into cardCollection used to be a single
        /// hardcoded literal per item - "warrior" for the Novice pack (no ownership check, so a
        /// second purchase silently duplicated it), and "dragon" for the Dragon Booster, an id
        /// that reads as a real database entry but is exactly the kind of placeholder grant this
        /// release feature exists to remove (rarity 7, no verified art/balance pass behind it -
        /// excluded by literal id below, not by any new "is this card legitimate" heuristic).
        ///
        /// PlaceholderCardId is the sole, explicit exclusion - not a general rarity/quality
        /// filter (requirement: "no new economy balancing, randomness, rarity system"). The rest
        /// of the sequence is simply CardDatabase.AllCards in its own existing file order, which
        /// is already the deterministic ordering authority other code in this project relies on
        /// (e.g. deck-persistence tests already slice AllCards the same way) - not a second,
        /// hand-maintained id list that could drift out of sync with the real database.
        /// </summary>
        private const string PlaceholderCardId = "dragon";

        /// <summary>
        /// Grants the next real card id from CardDatabase.AllCards (in the database's own fixed
        /// order) that isn't already in profile.cardCollection and isn't the placeholder id -
        /// the one fixed, deterministic reward sequence every card-granting Shop item draws from.
        /// Returns false, touching nothing, if every real card is already owned (sequence
        /// exhausted) - AttemptPurchase relies on this to withhold currency in that case.
        /// </summary>
        private static bool TryGrantNextUnownedCard(PlayerProfile profile)
        {
            CardDatabase database = CardDatabase.Instance;
            if (database == null) return false;

            foreach (Card card in database.AllCards)
            {
                if (card.Id == PlaceholderCardId) continue;
                if (CollectionProgression.OwnsAnyCopy(profile, card.Id)) continue;

                if (CollectionProgression.TryGrantFirstCopy(profile, card.Id, id => database.GetCard(id) != null))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Exposed for tests: overlay draw row after a gem pack purchase.</summary>
        public Transform PackDrawContentForTests =>
            canvasObj != null ? PackOpenOverlayPresenter.DrawContentForTests(canvasObj.transform) : null;

        /// <summary>Exposed for tests: sequential reveal controller on the pack overlay.</summary>
        public PackOpenRevealRunner PackRevealRunnerForTests =>
            canvasObj != null ? PackOpenOverlayPresenter.RevealRunnerForTests(canvasObj.transform) : null;

        /// <summary>Exposed for tests: Shop status line after ladder blocks or successful purchase.</summary>
        public string ShopStatusTextForTests => statusText != null ? statusText.text : null;

        /// <summary>Exposed for tests: whether a live grid tile exists for the SKU (hidden V1 stubs excluded).</summary>
        public bool ShopGridContainsSkuForTests(string skuId) =>
            canvasObj != null && canvasObj.transform.Find($"ShopGrid/ShopCard_{skuId}") != null;

        /// <summary>Exposed for tests: Stamina ladder BUY interactable state after RefreshStaminaBuyButtons.</summary>
        public bool StaminaBuyButtonInteractableForTests(int gemCost)
        {
            if (canvasObj == null) return false;
            string id = ShopStaminaCatalog.SkuIdForGemCost(gemCost);
            Button buyBtn = canvasObj.transform.Find($"ShopGrid/ShopCard_{id}/Btn_Buy")?.GetComponent<Button>();
            return buyBtn != null && buyBtn.interactable;
        }

        private static bool TryOpenGemPack(PlayerProfile profile, string skuId, out PackReceiptResult result)
        {
            result = null;
            if (profile == null) return false;

            var rng = new System.Random();
            string receiptId = System.Guid.NewGuid().ToString("N");
            result = new PackReceiptResult();
            if (!CollectionPackReceiptService.TryOpenPack(profile, skuId, rng, receiptId, out result))
            {
                Debug.Log($"Gem pack '{skuId}' could not open: {result.Error}");
                return false;
            }

            return true;
        }

        private void SetupShopItems()
        {
            shopItems = new List<ShopItemData>();

            // --- SHOP_V2_NUMBERS_PACKET locked Packs (prices + draw counts from CollectionPackCatalog) ---
            shopItems.Add(CreateLockedGemPackItem(
                CollectionPackCatalog.SingleSigilSkuId, "Single Sigil",
                sku => $"{sku.NormalDrawCount} Normal draw — best gem/card value ({sku.GemsPerCard:0.#} Gems/card)."));
            shopItems.Add(CreateLockedGemPackItem(
                CollectionPackCatalog.ScoutCacheSkuId, "Scout Cache",
                sku => $"{sku.NormalDrawCount} Normal + {sku.HighDrawCount} High — at least one {sku.FloorMinRarity}★+ ({sku.GemsPerCard:0.#} Gems/card)."));
            shopItems.Add(CreateLockedGemPackItem(
                CollectionPackCatalog.WarbandCacheSkuId, "Warband Cache",
                sku => $"{sku.NormalDrawCount} Normal + {sku.HighDrawCount} High — at least one {sku.FloorMinRarity}★+ ({sku.GemsPerCard:0.#} Gems/card)."));
            shopItems.Add(CreateLockedGemPackItem(
                CollectionPackCatalog.LegionCacheSkuId, "Legion Cache",
                sku => $"{sku.NormalDrawCount} Normal + {sku.HighDrawCount} High — at least one {sku.FloorMinRarity}★+ ({sku.GemsPerCard:0.#} Gems/card)."));

            if (!CollectionPackCatalog.HasInverseGemPerCardOrdering())
                Debug.LogError("[Shop] CollectionPackCatalog lost inverse gem/card order (SHOP_V2 lock).");

            // --- Resources (SHOP_V2 Stamina ladder — prices/grant/cap from ShopStaminaCatalog) ---
            foreach (int gemCost in ShopStaminaCatalog.GemCosts)
                shopItems.Add(CreateStaminaPotionItem(ShopStaminaCatalog.SkuIdForGemCost(gemCost), gemCost));

            // --- V1 leftovers withheld from live grid (still PurchaseForTests) ---
            shopItems.Add(new ShopItemData(
                ShopV1StubCatalog.NovicePackId, "Novice Card Pack", "V1 stub — withheld (bypasses pack pity).",
                ShopV1StubCatalog.NoviceGoldCost, 0, (p) =>
                {
                    bool granted = TryGrantNextUnownedCard(p);
                    if (granted) Debug.Log("Purchased Novice Card Pack! Added a new card to your collection.");
                    else Debug.Log("Novice Card Pack: your collection already contains every available card.");
                    return granted;
                }, hideFromShopGrid: true));

            shopItems.Add(new ShopItemData(
                ShopV1StubCatalog.DragonBoosterId, "Dragon Booster", "V1 stub — withheld (beats Singles gem/card).",
                0, ShopV1StubCatalog.DragonBoosterGemCost, (p) =>
                {
                    bool granted = TryGrantNextUnownedCard(p);
                    if (granted) Debug.Log("Purchased Dragon Booster! Added a new card to your collection.");
                    else Debug.Log("Dragon Booster: your collection already contains every available card.");
                    return granted;
                }, hideFromShopGrid: true));

            shopItems.Add(new ShopItemData(
                ShopV1StubCatalog.GoldVaultId, "Gold Vault", "V1 stub — withheld (no Phase-1 Gem→Gold).",
                0, ShopV1StubCatalog.GoldVaultGemCost, (p) =>
                {
                    bool granted = CurrencyManager.AddCurrency(p, CurrencyType.Gold, ShopV1StubCatalog.GoldVaultGoldGrant, persist: false);
                    if (granted) Debug.Log($"Purchased {ShopV1StubCatalog.GoldVaultGoldGrant:N0} Gold!");
                    return granted;
                }, hideFromShopGrid: true));
        }

        /// <summary>Gem pack tile priced from <see cref="CollectionPackCatalog"/> — sole lock authority.</summary>
        private ShopItemData CreateLockedGemPackItem(string skuId, string title,
            System.Func<CollectionPackSku, string> descriptionFactory)
        {
            if (!CollectionPackCatalog.TryGetSku(skuId, out CollectionPackSku sku))
            {
                Debug.LogError($"[Shop] Missing locked SKU '{skuId}' in CollectionPackCatalog.");
                return new ShopItemData(skuId, title, "Catalog miss", 0, 0, _ => false);
            }

            string description = descriptionFactory(sku);
            return new ShopItemData(skuId, title, description, 0, sku.GemCost, p =>
            {
                if (!TryOpenGemPack(p, skuId, out PackReceiptResult r)) return false;
                _pendingPackReceipt = r;
                return true;
            }, walletCommittedByCallback: true);
        }

        /// <summary>SHOP_V2 Stamina ladder row — escalate-in-order + 4/24h cap via <see cref="ShopStaminaCatalog"/>.</summary>
        private ShopItemData CreateStaminaPotionItem(string id, int gemCost)
        {
            int grant = ShopStaminaCatalog.StaminaGrantPerPotion;
            return new ShopItemData(
                id,
                $"Stamina Potion ({gemCost})",
                $"+{grant} Stamina ({gemCost} Gems). Ladder: next tier only · max {ShopStaminaCatalog.MaxPurchasesPerRollingDay}/24h.",
                0,
                gemCost,
                p =>
                {
                    long now = ShopStaminaCatalog.NowUtcTicks();
                    if (!ShopStaminaCatalog.IsGemCostAllowedNow(p, gemCost, now, out string ladderError))
                    {
                        Debug.Log($"Stamina ladder blocked: {ladderError}");
                        return false;
                    }

                    bool granted = CurrencyManager.RestoreStamina(p, grant, persist: false);
                    if (!granted) return false;

                    ShopStaminaCatalog.RecordSuccessfulPurchase(p, now);
                    Debug.Log($"Restored +{grant} Stamina ({gemCost} Gems)!");
                    return true;
                });
        }

        private void BuildUI()
        {
            TeardownUI();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
            staminaTierImages.Clear();

            // 1. Canvas Setup
            canvasObj = new GameObject("ShopCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // 2. Shop V1 catalog shell (1920×1080 RGBA) — empty wells; runtime owns all text/values.
            GameObject bgObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgObj.transform.SetParent(canvasObj.transform, false);
            Image bgImg = bgObj.GetComponent<Image>();
            ShopV1UiLibrary.ApplyFullscreenShell(
                bgImg, ShopV1UiLibrary.CatalogShellName, new Color(0.08f, 0.08f, 0.12f, 0.98f));
            bgImg.raycastTarget = false;
            UISharedFoundation.StretchFull(bgObj.GetComponent<RectTransform>());

            // 3. Top Header Bar (transparent over shell header wells)
            GameObject topBar = new GameObject("HeaderBar", typeof(RectTransform), typeof(Image));
            topBar.transform.SetParent(canvasObj.transform, false);
            Image topBarBg = topBar.GetComponent<Image>();
            topBarBg.color = new Color(0f, 0f, 0f, 0f);
            topBarBg.raycastTarget = false;

            RectTransform topRect = topBar.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0, 1);
            topRect.anchorMax = Vector2.one;
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.sizeDelta = new Vector2(0, 110);

            // Back Button
            GameObject backBtnObj = new GameObject("Btn_Back", typeof(RectTransform), typeof(Image), typeof(Button));
            backBtnObj.transform.SetParent(topBar.transform, false);
            Image backImg = backBtnObj.GetComponent<Image>();
            backImg.color = new Color(0.3f, 0.2f, 0.2f, 0.85f);

            Button backBtn = backBtnObj.GetComponent<Button>();
            HomeV3UiLibrary.ApplyNavTileButton(backBtn, backImg);
            backBtn.onClick.AddListener(() =>
            {
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
            CreateTextElement(topBar.transform, "Title", "SHOP & SUPPLIES", new Vector2(-250, 0), 32, TextAnchor.MiddleCenter);

            // Resource Displays Group
            GameObject resourceGroup = new GameObject("ResourceGroup", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            resourceGroup.transform.SetParent(topBar.transform, false);
            RectTransform resRect = resourceGroup.GetComponent<RectTransform>();
            resRect.anchorMin = new Vector2(1, 0.5f);
            resRect.anchorMax = new Vector2(1, 0.5f);
            resRect.pivot = new Vector2(1, 0.5f);
            resRect.anchoredPosition = new Vector2(-30, 0);
            resRect.sizeDelta = new Vector2(600, 60);

            HorizontalLayoutGroup hlg = resourceGroup.GetComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.spacing = 15;
            hlg.childControlWidth = false;

            goldText = HomeV3UiLibrary.CreateResourcePill(resourceGroup.transform, "home_resource_gold_pill_v3",
                "Gold", $"{player.gold}", 185f);
            gemsText = HomeV3UiLibrary.CreateResourcePill(resourceGroup.transform, "home_resource_gems_pill_v3",
                "Gems", $"{player.gems}", 185f);
            energyText = HomeV3UiLibrary.CreateResourcePill(resourceGroup.transform, "home_resource_energy_pill_v3",
                "Stamina", $"{player.stamina}/{player.maxStamina}", 200f);

            GameObject statusObj = CreateTextElement(canvasObj.transform, "ShopStatus", "Tap BUY on a supply to purchase.",
                new Vector2(0, -480), 20, TextAnchor.MiddleCenter);
            statusText = statusObj.GetComponent<Text>();
            statusText.color = new Color(0.9f, 0.82f, 0.64f);
            statusObj.GetComponent<RectTransform>().sizeDelta = new Vector2(1200f, 40f);

            // High-draw pity (10/60) — same counters that drive PITY SAVE on pack-open tiles.
            GameObject pityObj = CreateTextElement(
                canvasObj.transform,
                "PityProgress",
                CollectionPackPityCopy.FormatShopBanner(player),
                new Vector2(0, 360),
                17,
                TextAnchor.MiddleCenter);
            pityBannerText = pityObj.GetComponent<Text>();
            pityBannerText.color = new Color(1f, 0.75f, 0.35f);
            pityObj.GetComponent<RectTransform>().sizeDelta = new Vector2(1400f, 36f);

            // 4. Shop Items — packs over left shell wells; stamina ladder over right sidebar rows.
            BuildShopGrid();
        }

        private void BuildShopGrid()
        {
            packPityLineTexts.Clear();
            staminaTierImages.Clear();

            GameObject gridObj = new GameObject("ShopGrid", typeof(RectTransform));
            gridObj.transform.SetParent(canvasObj.transform, false);
            RectTransform gridRect = gridObj.GetComponent<RectTransform>();
            UISharedFoundation.StretchFull(gridRect);

            // Catalog shell product wells (top-left pixel space, measured from RGBA source).
            float[,] packBounds =
            {
                { 40f, 210f, 275f, 790f },
                { 328f, 210f, 564f, 790f },
                { 618f, 210f, 855f, 790f },
                { 908f, 180f, 1126f, 790f },
            };

            // Right sidebar ladder rows (shell chrome is opaque; atlas tiles overlay).
            float[,] staminaBounds =
            {
                { 1220f, 290f, 1860f, 440f },
                { 1220f, 460f, 1860f, 610f },
                { 1220f, 625f, 1860f, 775f },
                { 1220f, 795f, 1860f, 945f },
            };

            int packIndex = 0;
            int staminaIndex = 0;
            foreach (var item in shopItems)
            {
                if (item == null || item.hideFromShopGrid) continue;

                if (IsStaminaLadderSku(item.id))
                {
                    if (staminaIndex >= 4) continue;
                    float left = staminaBounds[staminaIndex, 0];
                    float top = staminaBounds[staminaIndex, 1];
                    float right = staminaBounds[staminaIndex, 2];
                    float bottom = staminaBounds[staminaIndex, 3];
                    CreateShopCardTile(gridObj.transform, item, left, top, right, bottom, staminaTierIndex1Based: staminaIndex + 1);
                    staminaIndex++;
                }
                else
                {
                    if (packIndex >= 4) continue;
                    float left = packBounds[packIndex, 0];
                    float top = packBounds[packIndex, 1];
                    float right = packBounds[packIndex, 2];
                    float bottom = packBounds[packIndex, 3];
                    CreateShopCardTile(gridObj.transform, item, left, top, right, bottom, staminaTierIndex1Based: 0);
                    packIndex++;
                }
            }
        }

        private void CreateShopCardTile(Transform parent, ShopItemData item,
            float leftPx, float topPx, float rightPx, float bottomPx, int staminaTierIndex1Based)
        {
            GameObject cardObj = new GameObject($"ShopCard_{item.id}", typeof(RectTransform), typeof(Image));
            cardObj.transform.SetParent(parent, false);
            cardObj.transform.localScale = Vector3.one;

            Image cardBg = cardObj.GetComponent<Image>();
            bool isStamina = staminaTierIndex1Based > 0;
            if (isStamina)
            {
                ShopV1UiLibrary.ApplyStaminaTierSprite(cardBg, staminaTierIndex1Based, unlocked: false);
                staminaTierImages[staminaTierIndex1Based] = cardBg;
            }
            else
            {
                // Transparent over catalog product well — shell provides the frame.
                cardBg.sprite = null;
                cardBg.color = new Color(0.08f, 0.1f, 0.14f, 0.35f);
            }

            SetScreenRectFromTopLeftPixels(cardObj.GetComponent<RectTransform>(), leftPx, topPx, rightPx, bottomPx);

            // Item Title
            CreateTextElement(cardObj.transform, "Title", item.title, new Vector2(0, isStamina ? 28f : 110f), 20, TextAnchor.MiddleCenter);

            // Description
            CreateTextElement(cardObj.transform, "Desc", item.description, new Vector2(0, isStamina ? -8f : 30f), 14, TextAnchor.MiddleCenter);

            // High-draw packs: live pity toward PITY SAVE (bundle FLOOR LIFT stays in pack description).
            if (CollectionPackCatalog.TryGetSku(item.id, out CollectionPackSku sku) && sku.HighDrawCount > 0)
            {
                GameObject pityLineObj = CreateTextElement(
                    cardObj.transform,
                    "PityLine",
                    CollectionPackPityCopy.FormatPackTileLine(player),
                    new Vector2(0, -40),
                    14,
                    TextAnchor.MiddleCenter);
                Text pityLine = pityLineObj.GetComponent<Text>();
                pityLine.color = new Color(1f, 0.75f, 0.35f);
                pityLineObj.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 40f);
                packPityLineTexts.Add(pityLine);
            }

            // Buy Button
            GameObject buyBtnObj = new GameObject("Btn_Buy", typeof(RectTransform), typeof(Image), typeof(Button));
            buyBtnObj.transform.SetParent(cardObj.transform, false);
            buyBtnObj.transform.localScale = Vector3.one;

            Image buyImg = buyBtnObj.GetComponent<Image>();
            buyImg.color = item.goldCost > 0 ? new Color(0.85f, 0.65f, 0.15f, 0.92f) : new Color(0.55f, 0.25f, 0.85f, 0.92f);

            RectTransform buyRect = buyBtnObj.GetComponent<RectTransform>();
            buyRect.anchorMin = new Vector2(0.5f, 0f);
            buyRect.anchorMax = new Vector2(0.5f, 0f);
            buyRect.pivot = new Vector2(0.5f, 0f);
            buyRect.anchoredPosition = new Vector2(0, isStamina ? 10f : 16f);
            buyRect.sizeDelta = new Vector2(isStamina ? 200f : 200f, 48f);

            Button buyBtn = buyBtnObj.GetComponent<Button>();
            HomeV3UiLibrary.ApplyNavTileButton(buyBtn, buyImg);
            buyBtn.onClick.AddListener(() => AttemptPurchase(item));

            string priceLabel = item.goldCost > 0 ? $"{item.goldCost} Gold" : $"{item.gemCost} Gems";
            CreateTextElement(buyBtnObj.transform, "PriceText", $"BUY ({priceLabel})", Vector2.zero, 18, TextAnchor.MiddleCenter);
        }

        private static void SetScreenRectFromTopLeftPixels(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = new Vector2(left / 1920f, 1f - bottom / 1080f);
            rect.anchorMax = new Vector2(right / 1920f, 1f - top / 1080f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// CurrencyManager is the sole wallet authority for this transaction: affordability reads
        /// (GetBalance), the reward's own currency/stamina grants (AddCurrency/RestoreStamina,
        /// called from inside each ShopItemData.onPurchase with persist:false), and the final cost
        /// deduction (SpendCurrency, also persist:false) all go through it - ShopPresenter itself
        /// never writes player.gold/gems/stamina directly. Every CurrencyManager call in this
        /// transaction defers its own save so the whole purchase (reward + cost) commits in the
        /// single SaveSystem.Save below, not several partial writes.
        /// </summary>
        private void AttemptPurchase(ShopItemData item)
        {
            // Insufficient currency changes nothing - checked, and returned on, before any
            // profile mutation or reward attempt.
            if (item.goldCost > 0 && CurrencyManager.GetBalance(player, CurrencyType.Gold) < item.goldCost)
            {
                SetShopStatus($"Not enough Gold for {item.title}.");
                return;
            }

            if (item.gemCost > 0 && CurrencyManager.GetBalance(player, CurrencyType.Gems) < item.gemCost)
            {
                SetShopStatus($"Not enough Gems for {item.title}.");
                return;
            }

            if (IsStaminaLadderSku(item.id))
            {
                long now = ShopStaminaCatalog.NowUtcTicks();
                if (!ShopStaminaCatalog.IsGemCostAllowedNow(player, item.gemCost, now, out string ladderError))
                {
                    SetShopStatus(ladderError ?? "Stamina refill not available.");
                    RefreshStaminaBuyButtons();
                    return;
                }
            }

            // The reward is fulfilled BEFORE any currency is spent, and only currency is spent
            // if it actually was fulfilled - a card-granting item whose reward sequence is
            // exhausted (TryGrantNextUnownedCard returns false, having touched nothing) must not
            // spend the player's Gold/Gems for nothing. Existing non-card items (Gold Vault,
            // Energy Potion) always return true and are unaffected by this ordering change.
            bool fulfilled = item.onPurchase != null && item.onPurchase.Invoke(player);
            if (!fulfilled)
            {
                SetShopStatus($"{item.title}: could not be fulfilled — no currency spent.");
                RefreshStaminaBuyButtons();
                return;
            }

            if (!item.walletCommittedByCallback)
            {
                if (item.goldCost > 0) CurrencyManager.SpendCurrency(player, CurrencyType.Gold, item.goldCost, persist: false);
                if (item.gemCost > 0) CurrencyManager.SpendCurrency(player, CurrencyType.Gems, item.gemCost, persist: false);
                MyriadOfDragons.Save.SaveSystem.Save(player);
            }

            if (_pendingPackReceipt != null && _pendingPackReceipt.Success && canvasObj != null)
            {
                PackReceiptResult receipt = _pendingPackReceipt;
                _pendingPackReceipt = null;
                RefreshPityDisplay();
                PackOpenOverlayPresenter.Show(
                    canvasObj.transform,
                    receipt,
                    onDismiss: RefreshResourceDisplay,
                    onOpenCollection: onOpenCollectionAction == null
                        ? null
                        : () =>
                        {
                            TeardownUI();
                            onOpenCollectionAction.Invoke();
                        },
                    ownershipProfile: player);
                SetShopStatus($"Opened {item.title}.");
            }
            else
            {
                _pendingPackReceipt = null;
                SetShopStatus($"Purchased {item.title}.");
                RefreshResourceDisplay();
                RefreshStaminaBuyButtons();
            }
        }

        private static bool IsStaminaLadderSku(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            foreach (int gemCost in ShopStaminaCatalog.GemCosts)
            {
                if (string.Equals(itemId, ShopStaminaCatalog.SkuIdForGemCost(gemCost), System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private void RefreshStaminaBuyButtons()
        {
            if (canvasObj == null || player == null) return;

            long now = ShopStaminaCatalog.NowUtcTicks();
            bool hasNext = ShopStaminaCatalog.TryGetNextGemCost(player, now, out int nextCost, out _);

            for (int tier = 0; tier < ShopStaminaCatalog.GemCosts.Length; tier++)
            {
                int gemCost = ShopStaminaCatalog.GemCosts[tier];
                string id = ShopStaminaCatalog.SkuIdForGemCost(gemCost);
                Button buyBtn = canvasObj.transform.Find($"ShopGrid/ShopCard_{id}/Btn_Buy")?.GetComponent<Button>();
                bool unlocked = hasNext && gemCost == nextCost;
                if (buyBtn != null)
                    buyBtn.interactable = unlocked;

                int tierIndex1Based = tier + 1;
                if (staminaTierImages.TryGetValue(tierIndex1Based, out Image tierImage) && tierImage != null)
                    ShopV1UiLibrary.ApplyStaminaTierSprite(tierImage, tierIndex1Based, unlocked);
            }
        }

        private void SetShopStatus(string message)
        {
            if (statusText != null) statusText.text = message ?? string.Empty;
            Debug.Log(message);
        }

        private void RefreshResourceDisplay()
        {
            if (goldText != null) goldText.text = $"{player.gold}";
            if (gemsText != null) gemsText.text = $"{player.gems}";
            if (energyText != null) energyText.text = $"{player.stamina}/{player.maxStamina}";
            RefreshPityDisplay();
        }

        private void RefreshPityDisplay()
        {
            if (pityBannerText != null)
                pityBannerText.text = CollectionPackPityCopy.FormatShopBanner(player);

            string tileLine = CollectionPackPityCopy.FormatPackTileLine(player);
            for (int i = 0; i < packPityLineTexts.Count; i++)
            {
                if (packPityLineTexts[i] != null)
                    packPityLineTexts[i].text = tileLine;
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
            rect.sizeDelta = new Vector2(280, 80);

            return textObj;
        }

        public void TeardownUI()
        {
            packPityLineTexts.Clear();
            pityBannerText = null;
            staminaTierImages.Clear();
            if (canvasObj == null) return;
            canvasObj.SetActive(false);
            if (Application.isPlaying) Destroy(canvasObj);
            else DestroyImmediate(canvasObj);
            canvasObj = null;
        }

        private void OnDestroy()
        {
            TeardownUI();
        }
    }
}
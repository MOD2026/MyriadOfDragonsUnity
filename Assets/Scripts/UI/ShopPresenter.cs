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
            // Novice: deterministic unowned grant bypasses pack pity — not on Shop V2 SKU list.
            shopItems.Add(new ShopItemData("pack_novice", "Novice Card Pack", "V1 stub — withheld (bypasses pack pity).", 500, 0, (p) =>
            {
                bool granted = TryGrantNextUnownedCard(p);
                if (granted) Debug.Log("Purchased Novice Card Pack! Added a new card to your collection.");
                else Debug.Log("Novice Card Pack: your collection already contains every available card.");
                return granted;
            }, hideFromShopGrid: true));

            // Dragon Booster @ 100 Gems/card beats Single Sigil @ 150 — violates inverse bulk. Hidden.
            shopItems.Add(new ShopItemData("pack_dragon", "Dragon Booster", "V1 stub — withheld (beats Singles gem/card).", 0, 100, (p) =>
            {
                bool granted = TryGrantNextUnownedCard(p);
                if (granted) Debug.Log("Purchased Dragon Booster! Added a new card to your collection.");
                else Debug.Log("Dragon Booster: your collection already contains every available card.");
                return granted;
            }, hideFromShopGrid: true));

            // Gold Vault: Phase-1 gem→gold banned by SHOP_V2_NUMBERS_PACKET. Hidden.
            shopItems.Add(new ShopItemData("res_gold", "Gold Vault", "V1 stub — withheld (no Phase-1 Gem→Gold).", 0, 50, (p) =>
            {
                bool granted = CurrencyManager.AddCurrency(p, CurrencyType.Gold, 1500, persist: false);
                if (granted) Debug.Log("Purchased 1,500 Gold!");
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

            // 1. Canvas Setup
            canvasObj = new GameObject("ShopCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

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
            if (!HomeV3UiLibrary.TryApplyHeaderFrame(topBarBg))
                topBarBg.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);

            RectTransform topRect = topBar.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0, 1);
            topRect.anchorMax = Vector2.one;
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.sizeDelta = new Vector2(0, 110);

            // Back Button
            GameObject backBtnObj = new GameObject("Btn_Back", typeof(RectTransform), typeof(Image), typeof(Button));
            backBtnObj.transform.SetParent(topBar.transform, false);
            Image backImg = backBtnObj.GetComponent<Image>();
            backImg.color = new Color(0.3f, 0.2f, 0.2f);

            Button backBtn = backBtnObj.GetComponent<Button>();
            HomeV3UiLibrary.ApplyNavTileButton(backBtn, backImg);
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

            // 4. Shop Items Grid Container
            BuildShopGrid();
        }

        private void BuildShopGrid()
        {
            GameObject gridObj = new GameObject("ShopGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridObj.transform.SetParent(canvasObj.transform, false);

            RectTransform gridRect = gridObj.GetComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0.5f, 0.5f);
            gridRect.anchorMax = new Vector2(0.5f, 0.5f);
            gridRect.pivot = new Vector2(0.5f, 0.5f);
            gridRect.anchoredPosition = new Vector2(0, -40);
            gridRect.sizeDelta = new Vector2(1400, 750);

            GridLayoutGroup grid = gridObj.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(320, 340);
            grid.spacing = new Vector2(30, 30);
            grid.childAlignment = TextAnchor.MiddleCenter;

            foreach (var item in shopItems)
            {
                if (item == null || item.hideFromShopGrid) continue;
                CreateShopCardTile(gridObj.transform, item);
            }
        }

        private void CreateShopCardTile(Transform parent, ShopItemData item)
        {
            GameObject cardObj = new GameObject($"ShopCard_{item.id}", typeof(RectTransform), typeof(Image));
            cardObj.transform.SetParent(parent, false);
            cardObj.transform.localScale = Vector3.one;

            Image cardBg = cardObj.GetComponent<Image>();
            cardBg.color = new Color(0.14f, 0.16f, 0.22f);

            // Item Title
            CreateTextElement(cardObj.transform, "Title", item.title, new Vector2(0, 110), 22, TextAnchor.MiddleCenter);

            // Description
            CreateTextElement(cardObj.transform, "Desc", item.description, new Vector2(0, 20), 18, TextAnchor.MiddleCenter);

            // Buy Button
            GameObject buyBtnObj = new GameObject("Btn_Buy", typeof(RectTransform), typeof(Image), typeof(Button));
            buyBtnObj.transform.SetParent(cardObj.transform, false);
            buyBtnObj.transform.localScale = Vector3.one;

            Image buyImg = buyBtnObj.GetComponent<Image>();
            buyImg.color = item.goldCost > 0 ? new Color(0.85f, 0.65f, 0.15f) : new Color(0.55f, 0.25f, 0.85f);

            RectTransform buyRect = buyBtnObj.GetComponent<RectTransform>();
            buyRect.anchoredPosition = new Vector2(0, -100);
            buyRect.sizeDelta = new Vector2(240, 55);

            Button buyBtn = buyBtnObj.GetComponent<Button>();
            HomeV3UiLibrary.ApplyNavTileButton(buyBtn, buyImg);
            buyBtn.onClick.AddListener(() => AttemptPurchase(item));

            string priceLabel = item.goldCost > 0 ? $"{item.goldCost} Gold" : $"{item.gemCost} Gems";
            CreateTextElement(buyBtnObj.transform, "PriceText", $"BUY ({priceLabel})", Vector2.zero, 20, TextAnchor.MiddleCenter);
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

            foreach (int gemCost in ShopStaminaCatalog.GemCosts)
            {
                string id = ShopStaminaCatalog.SkuIdForGemCost(gemCost);
                Button buyBtn = canvasObj.transform.Find($"ShopGrid/ShopCard_{id}/Btn_Buy")?.GetComponent<Button>();
                if (buyBtn == null) continue;
                buyBtn.interactable = hasNext && gemCost == nextCost;
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
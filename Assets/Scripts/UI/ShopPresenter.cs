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

        public ShopItemData(string id, string title, string desc, int goldCost, int gemCost, System.Func<PlayerProfile, bool> onPurchase)
        {
            this.id = id;
            this.title = title;
            this.description = desc;
            this.goldCost = goldCost;
            this.gemCost = gemCost;
            this.onPurchase = onPurchase;
        }
    }

    public class ShopPresenter : MonoBehaviour
    {
        private GameObject canvasObj;
        private PlayerProfile player;
        private System.Action onBackToHomeAction;

        private Text goldText;
        private Text gemsText;
        private Text energyText;

        private List<ShopItemData> shopItems;

        public void Initialize(PlayerProfile profile, System.Action onBackToHome)
        {
            this.player = profile;
            this.onBackToHomeAction = onBackToHome;

            SetupShopItems();
            BuildUI();
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
                if (profile.cardCollection.Contains(card.Id)) continue;

                profile.cardCollection.Add(card.Id);
                return true;
            }

            return false;
        }

        private void SetupShopItems()
        {
            shopItems = new List<ShopItemData>()
            {
                new ShopItemData("pack_novice", "Novice Card Pack", "Grants one new card for your collection.", 500, 0, (p) =>
                {
                    bool granted = TryGrantNextUnownedCard(p);
                    if (granted) Debug.Log("Purchased Novice Card Pack! Added a new card to your collection.");
                    else Debug.Log("Novice Card Pack: your collection already contains every available card.");
                    return granted;
                }),
                new ShopItemData("pack_dragon", "Dragon Booster", "Grants one new card for your collection.", 0, 100, (p) =>
                {
                    bool granted = TryGrantNextUnownedCard(p);
                    if (granted) Debug.Log("Purchased Dragon Booster! Added a new card to your collection.");
                    else Debug.Log("Dragon Booster: your collection already contains every available card.");
                    return granted;
                }),
                new ShopItemData("res_gold", "Gold Vault", "Instantly adds 1,500 Gold to your wallet.", 0, 50, (p) =>
                {
                    bool granted = CurrencyManager.AddCurrency(p, CurrencyType.Gold, 1500, persist: false);
                    if (granted) Debug.Log("Purchased 1,500 Gold!");
                    return granted;
                }),
                new ShopItemData("res_energy", "Energy Potion", "Restores +50 Stamina for campaign battles.", 0, 30, (p) =>
                {
                    bool granted = CurrencyManager.RestoreStamina(p, 50, persist: false);
                    if (granted) Debug.Log("Restored +50 Energy!");
                    return granted;
                })
            };
        }

        private void BuildUI()
        {
            // 1. Canvas Setup
            canvasObj = new GameObject("ShopCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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
            topRect.sizeDelta = new Vector2(0, 110);

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

            goldText = CreateResourcePill(resourceGroup.transform, $"Gold: {player.gold}", new Color(0.85f, 0.68f, 0.15f, 0.95f));
            gemsText = CreateResourcePill(resourceGroup.transform, $"Gems: {player.gems}", new Color(0.55f, 0.25f, 0.85f, 0.95f));
            energyText = CreateResourcePill(resourceGroup.transform, $"Energy: {player.stamina}/{player.maxStamina}", new Color(0.2f, 0.65f, 0.35f, 0.95f));

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
                Debug.Log("Not enough Gold!");
                return;
            }

            if (item.gemCost > 0 && CurrencyManager.GetBalance(player, CurrencyType.Gems) < item.gemCost)
            {
                Debug.Log("Not enough Gems!");
                return;
            }

            // The reward is fulfilled BEFORE any currency is spent, and only currency is spent
            // if it actually was fulfilled - a card-granting item whose reward sequence is
            // exhausted (TryGrantNextUnownedCard returns false, having touched nothing) must not
            // spend the player's Gold/Gems for nothing. Existing non-card items (Gold Vault,
            // Energy Potion) always return true and are unaffected by this ordering change.
            bool fulfilled = item.onPurchase != null && item.onPurchase.Invoke(player);
            if (!fulfilled)
            {
                Debug.Log($"{item.title}: purchase could not be fulfilled - no currency spent.");
                return;
            }

            if (item.goldCost > 0) CurrencyManager.SpendCurrency(player, CurrencyType.Gold, item.goldCost, persist: false);
            if (item.gemCost > 0) CurrencyManager.SpendCurrency(player, CurrencyType.Gems, item.gemCost, persist: false);

            MyriadOfDragons.Save.SaveSystem.Save(player);
            RefreshResourceDisplay();
        }

        private void RefreshResourceDisplay()
        {
            if (goldText != null) goldText.text = $"Gold: {player.gold}";
            if (gemsText != null) gemsText.text = $"Gems: {player.gems}";
            if (energyText != null) energyText.text = $"Energy: {player.stamina}/{player.maxStamina}";
        }

        private Text CreateResourcePill(Transform parent, string text, Color bgColor)
        {
            GameObject pill = new GameObject("Pill", typeof(RectTransform), typeof(Image));
            pill.transform.SetParent(parent, false);
            pill.transform.localScale = Vector3.one;

            Image img = pill.GetComponent<Image>();
            img.color = bgColor;

            RectTransform rect = pill.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(170, 50);

            GameObject textObj = CreateTextElement(pill.transform, "Text", text, Vector2.zero, 20, TextAnchor.MiddleCenter);
            return textObj.GetComponent<Text>();
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
    }
}
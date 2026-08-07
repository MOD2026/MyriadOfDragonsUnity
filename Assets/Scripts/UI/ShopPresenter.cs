using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
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
        public System.Action<PlayerProfile> onPurchase;

        public ShopItemData(string id, string title, string desc, int goldCost, int gemCost, System.Action<PlayerProfile> onPurchase)
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

        private void SetupShopItems()
        {
            shopItems = new List<ShopItemData>()
            {
                new ShopItemData("pack_novice", "Novice Card Pack", "Contains 3 basic warrior & strategist cards.", 500, 0, (p) => {
                    p.gold -= 500;
                    p.cardCollection.Add("warrior");
                    Debug.Log("Purchased Novice Card Pack! Added 3 cards to collection.");
                }),
                new ShopItemData("pack_dragon", "Dragon Booster", "Guaranteed 1 Epic Dragon card & 2 Rare spells.", 0, 100, (p) => {
                    p.gems -= 100;
                    p.cardCollection.Add("dragon");
                    Debug.Log("Purchased Dragon Booster! Added Dragon card to collection.");
                }),
                new ShopItemData("res_gold", "Gold Vault", "Instantly adds 1,500 Gold to your wallet.", 0, 50, (p) => {
                    p.gems -= 50;
                    p.gold += 1500;
                    Debug.Log("Purchased 1,500 Gold!");
                }),
                new ShopItemData("res_energy", "Energy Potion", "Restores +50 Stamina for campaign battles.", 0, 30, (p) => {
                    p.gems -= 30;
                    p.stamina = Mathf.Min(p.stamina + 50, p.maxStamina);
                    Debug.Log("Restored +50 Energy!");
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

        private void AttemptPurchase(ShopItemData item)
        {
            if (item.goldCost > 0 && player.gold < item.goldCost)
            {
                Debug.Log("Not enough Gold!");
                return;
            }

            if (item.gemCost > 0 && player.gems < item.gemCost)
            {
                Debug.Log("Not enough Gems!");
                return;
            }

            item.onPurchase?.Invoke(player);
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
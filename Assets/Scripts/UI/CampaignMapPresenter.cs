using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace MyriadOfDragons.UI
{
    public class CampaignStageData
    {
        public string stageId;
        public string title;
        public string enemyName;
        public string enemyPortraitPath;
        public string description;
        public int goldReward;
        public int gemReward;
        public bool isUnlocked;

        public CampaignStageData(string id, string title, string enemy, string portrait, string desc, int gold, int gems, bool unlocked)
        {
            this.stageId = id;
            this.title = title;
            this.enemyName = enemy;
            this.enemyPortraitPath = portrait;
            this.description = desc;
            this.goldReward = gold;
            this.gemReward = gems;
            this.isUnlocked = unlocked;
        }
    }

    public class CampaignMapPresenter : MonoBehaviour
    {
        private GameObject mapCanvasObj;
        private GameObject detailModalObj;
        private System.Action onBackToHomeAction;
        private System.Action<CampaignStageData> onLaunchBattleAction;

        private List<CampaignStageData> chapterStages = new List<CampaignStageData>()
        {
            new CampaignStageData("1-1", "Outer Border Guard", "Orc Scout Patrol", "UI/Portraits/Paladin", "A small scouting party blocks the mountain path. Defeat them to open the route.", 200, 20, true),
            new CampaignStageData("1-2", "Volcanic Ridge", "Wyvern Tamer Kaelen", "UI/Portraits/Paladin", "Kaelen commands the high ground with his trained drakes. Break his vanguard!", 350, 50, true),
            new CampaignStageData("1-3", "Stronghold Citadel", "High Warlord Gorn", "UI/Portraits/Paladin", "The citadel commander awaits inside the obsidian gates. Defeat him to liberate Chapter 1.", 500, 100, false)
        };

        public void Initialize(System.Action onBackToHome, System.Action<CampaignStageData> onLaunchBattle)
        {
            this.onBackToHomeAction = onBackToHome;
            this.onLaunchBattleAction = onLaunchBattle;
            BuildCampaignMapUI();
        }

        private void BuildCampaignMapUI()
        {
            // 1. Canvas Setup
            mapCanvasObj = new GameObject("CampaignMapCanvas");
            Canvas canvas = mapCanvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = mapCanvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            mapCanvasObj.AddComponent<GraphicRaycaster>();

            // 2. Backdrop Image
            GameObject bgObj = new GameObject("MapBackdrop");
            bgObj.transform.SetParent(mapCanvasObj.transform, false);
            Image bgImg = bgObj.AddComponent<Image>();

            Sprite mapSprite = Resources.Load<Sprite>("UI/Backdrops/Dark_Forest");
            if (mapSprite == null) mapSprite = Resources.Load<Sprite>("UI/Backdrops/Desert_Ruins");

            if (mapSprite != null)
                bgImg.sprite = mapSprite;
            else
                bgImg.color = new Color(0.1f, 0.08f, 0.12f);

            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            // 3. Top Header Bar
            GameObject topBar = new GameObject("CampaignHeader");
            topBar.transform.SetParent(mapCanvasObj.transform, false);
            Image topBarBg = topBar.AddComponent<Image>();
            topBarBg.color = new Color(0.06f, 0.06f, 0.1f, 0.9f);

            RectTransform topRect = topBar.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0, 1);
            topRect.anchorMax = Vector2.one;
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.sizeDelta = new Vector2(0, 100);

            // Back Button
            GameObject backBtnObj = new GameObject("Btn_Back");
            backBtnObj.transform.SetParent(topBar.transform, false);
            Image backImg = backBtnObj.AddComponent<Image>();
            backImg.color = new Color(0.3f, 0.2f, 0.2f);

            Button backBtn = backBtnObj.AddComponent<Button>();
            backBtn.onClick.AddListener(() =>
            {
                Destroy(mapCanvasObj);
                onBackToHomeAction?.Invoke();
            });

            RectTransform backRect = backBtnObj.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0, 0.5f);
            backRect.anchorMax = new Vector2(0, 0.5f);
            backRect.pivot = new Vector2(0, 0.5f);
            backRect.anchoredPosition = new Vector2(30, 0);
            backRect.sizeDelta = new Vector2(160, 60);

            CreateTextElement(backBtnObj.transform, "Text", "< BACK", Vector2.zero, 24, TextAnchor.MiddleCenter);

            // Chapter Title
            CreateTextElement(topBar.transform, "TitleText", "CHAPTER 1: THE ORC INVASION", new Vector2(0, 0), 32, TextAnchor.MiddleCenter);

            // 4. Stage Nodes Container
            GameObject nodeContainer = new GameObject("StageNodesContainer");
            nodeContainer.transform.SetParent(mapCanvasObj.transform, false);

            RectTransform nodeContRect = nodeContainer.AddComponent<RectTransform>();
            nodeContRect.anchoredPosition = new Vector2(0, -30);
            nodeContRect.sizeDelta = new Vector2(1200, 300);

            HorizontalLayoutGroup hlg = nodeContainer.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 150;
            hlg.childControlWidth = false;

            // Create Stage Nodes
            foreach (var stage in chapterStages)
            {
                CreateStageNode(nodeContainer.transform, stage);
            }
        }

        private void CreateStageNode(Transform parent, CampaignStageData stage)
        {
            GameObject nodeObj = new GameObject($"StageNode_{stage.stageId}");
            nodeObj.transform.SetParent(parent, false);

            Image nodeImg = nodeObj.AddComponent<Image>();
            nodeImg.color = stage.isUnlocked ? new Color(0.85f, 0.65f, 0.2f) : new Color(0.3f, 0.3f, 0.35f, 0.7f);

            Button btn = nodeObj.AddComponent<Button>();
            btn.interactable = stage.isUnlocked;
            btn.onClick.AddListener(() => OpenStageDetails(stage));

            RectTransform rect = nodeObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(180, 180);

            // Stage Number Badge
            CreateTextElement(nodeObj.transform, "StageNum", stage.stageId, new Vector2(0, 20), 36, TextAnchor.MiddleCenter);

            // Stage Status Label
            string statusText = stage.isUnlocked ? stage.title : "LOCKED";
            CreateTextElement(nodeObj.transform, "Status", statusText, new Vector2(0, -45), 20, TextAnchor.MiddleCenter);
        }

        private void OpenStageDetails(CampaignStageData stage)
        {
            if (detailModalObj != null) Destroy(detailModalObj);

            detailModalObj = new GameObject("StageDetailModal");
            detailModalObj.transform.SetParent(mapCanvasObj.transform, false);

            // Dark Backdrop Dimmer
            Image dimImg = detailModalObj.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.75f);

            RectTransform dimRect = detailModalObj.GetComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.sizeDelta = Vector2.zero;

            // Panel Window
            GameObject panelObj = new GameObject("DetailPanel");
            panelObj.transform.SetParent(detailModalObj.transform, false);
            Image panelImg = panelObj.AddComponent<Image>();
            panelImg.color = new Color(0.12f, 0.14f, 0.2f);

            RectTransform panelRect = panelObj.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(800, 500);

            // Enemy Title & Description
            CreateTextElement(panelObj.transform, "Title", $"STAGE {stage.stageId}: {stage.title}", new Vector2(0, 190), 30, TextAnchor.MiddleCenter);
            CreateTextElement(panelObj.transform, "Enemy", $"Target: <color=#FFD700>{stage.enemyName}</color>", new Vector2(0, 130), 26, TextAnchor.MiddleCenter);
            CreateTextElement(panelObj.transform, "Desc", stage.description, new Vector2(0, 40), 22, TextAnchor.MiddleCenter);

            // Reward Info
            CreateTextElement(panelObj.transform, "Rewards", $"First Clear Rewards: <color=#FFD700>{stage.goldReward} Gold</color> | <color=#A020F0>{stage.gemReward} Gems</color>", new Vector2(0, -60), 24, TextAnchor.MiddleCenter);

            // Launch Button
            GameObject launchBtnObj = new GameObject("Btn_Launch");
            launchBtnObj.transform.SetParent(panelObj.transform, false);
            Image launchImg = launchBtnObj.AddComponent<Image>();
            launchImg.color = new Color(0.8f, 0.25f, 0.2f);

            Button launchBtn = launchBtnObj.AddComponent<Button>();
            launchBtn.onClick.AddListener(() =>
            {
                Destroy(mapCanvasObj);
                onLaunchBattleAction?.Invoke(stage);
            });

            RectTransform launchRect = launchBtnObj.GetComponent<RectTransform>();
            launchRect.anchoredPosition = new Vector2(-120, -170);
            launchRect.sizeDelta = new Vector2(220, 65);

            CreateTextElement(launchBtnObj.transform, "Text", "LAUNCH BATTLE", Vector2.zero, 24, TextAnchor.MiddleCenter);

            // Close Button
            GameObject closeBtnObj = new GameObject("Btn_Close");
            closeBtnObj.transform.SetParent(panelObj.transform, false);
            Image closeImg = closeBtnObj.AddComponent<Image>();
            closeImg.color = new Color(0.3f, 0.3f, 0.35f);

            Button closeBtn = closeBtnObj.AddComponent<Button>();
            closeBtn.onClick.AddListener(() => Destroy(detailModalObj));

            RectTransform closeRect = closeBtnObj.GetComponent<RectTransform>();
            closeRect.anchoredPosition = new Vector2(150, -170);
            closeRect.sizeDelta = new Vector2(180, 65);

            CreateTextElement(closeBtnObj.transform, "Text", "CLOSE", Vector2.zero, 24, TextAnchor.MiddleCenter);
        }

        private void CreateTextElement(Transform parent, string objectName, string content, Vector2 position, int fontSize, TextAnchor alignment)
        {
            GameObject textObj = new GameObject(objectName);
            textObj.transform.SetParent(parent, false);

            Text txt = textObj.AddComponent<Text>();
            txt.text = content;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = fontSize;
            txt.alignment = alignment;
            txt.color = Color.white;
            txt.supportRichText = true;

            RectTransform rect = textObj.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(700, 100);
        }
    }
}
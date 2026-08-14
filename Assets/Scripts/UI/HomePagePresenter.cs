using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MyriadOfDragons.UI;
using MyriadOfDragons.Data;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Save;

public class HomePagePresenter : MonoBehaviour
{
    private GameObject homeCanvasObj;
    private GameObject dialogueOverlayObj;
    private BattleController _battleController;

    // HUD Text References
    private Text goldHudText;
    private Text gemsHudText;
    private Text energyHudText;

    // Tutorial Dialogue State
    private int currentDialogueIndex = 0;
    private Text speakerNameText;
    private Text dialogueBodyText;
    private Image portraitImage;

    // Current Active Stage Track
    private CampaignStageData currentActiveStage;

    private struct DialogueLine
    {
        public string speaker;
        public string text;
        public string portraitPath;

        public DialogueLine(string speaker, string text, string portraitPath)
        {
            this.speaker = speaker;
            this.text = text;
            this.portraitPath = portraitPath;
        }
    }

    private List<DialogueLine> tutorialDialogue = new List<DialogueLine>()
    {
        new DialogueLine("High Priestess", "Greetings, Sovereign. Darkness encroaches upon our borders once more.", "UI/Portraits/Paladin"),
        new DialogueLine("High Priestess", "The Wyvern Tamer Kaelen has mobilized his vanguard near the volcanic ridge.", "UI/Portraits/Paladin"),
        new DialogueLine("High Priestess", "Prepare your deck and lead our forces into battle when you are ready!", "UI/Portraits/Paladin")
    };

    void Start()
    {
        SaveManager.Load();
        BuildHomePageUI();
        ShowTutorialDialogue();

        // Subscribe to Claude's Battle Outcome Event
        BindBattleControllerForTests(FindAnyObjectByType<BattleController>());
    }

    /// <summary>Exposed for tests: Start() never fires in EditMode (no Play Mode lifecycle),
    /// so this is the only way a test can subscribe HandleMatchCompleted to a real
    /// BattleController and exercise the real reward-handler path - the same two lines
    /// Start() itself runs, and nothing else (no save load, no UI build, no dialogue).
    /// HandleMatchCompleted itself stays private; only this subscription step is exposed.</summary>
    public void BindBattleControllerForTests(BattleController controller)
    {
        _battleController = controller;
        if (_battleController != null)
        {
            _battleController.OnMatchCompleted += HandleMatchCompleted;
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        if (_battleController != null)
        {
            _battleController.OnMatchCompleted -= HandleMatchCompleted;
            _battleController = null;
        }
    }

    private void HandleMatchCompleted(MatchResult result)
    {
        Debug.Log($"[Metagame] Match Ended. Victory: {result.IsVictory} | Ticks: {result.TicksTaken}");

        // The offline tutorial battle (GameBootstrap.StartApprovedTutorialBattle) must never
        // grant rewards, unlock stages, or write a save - it makes no server call and confirms
        // no victory. This is the only guard; normal-match reward behavior below is unchanged.
        if (GameBootstrap.Instance != null && GameBootstrap.Instance.IsTutorialMatch)
        {
            return;
        }

        if (result.IsVictory)
        {
            int goldEarned = currentActiveStage != null ? currentActiveStage.goldReward : 250;
            int gemsEarned = currentActiveStage != null ? currentActiveStage.gemReward : 25;

            PlayerProfile profile = SaveManager.SaveData;
            if (profile != null)
            {
                profile.gold += goldEarned;
                profile.gems += gemsEarned;

                if (currentActiveStage != null && !profile.unlockedStageIds.Contains(currentActiveStage.stageId))
                {
                    profile.unlockedStageIds.Add(currentActiveStage.stageId);
                }

                SaveManager.Save();
            }

            RefreshTopHUD();
            Debug.Log($"[Metagame] Awarded {goldEarned} Gold & {gemsEarned} Gems for Victory!");
        }
    }

    private void BuildHomePageUI()
    {
        // 1. Canvas Setup
        homeCanvasObj = new GameObject("HomePageCanvas");
        Canvas canvas = homeCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = homeCanvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        homeCanvasObj.AddComponent<GraphicRaycaster>();

        if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // 2. Background
        GameObject bgObj = new GameObject("CityBackground");
        bgObj.transform.SetParent(homeCanvasObj.transform, false);
        Image bgImage = bgObj.AddComponent<Image>();

        Sprite citySprite = Resources.Load<Sprite>("UI/Backdrops/Zihan_City_NO NAMES");
        if (citySprite != null)
            bgImage.sprite = citySprite;
        else
            bgImage.color = new Color(0.12f, 0.1f, 0.15f);

        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // 3. Top Header Bar
        GameObject topBar = new GameObject("TopHUD");
        topBar.transform.SetParent(homeCanvasObj.transform, false);
        Image topBarBg = topBar.AddComponent<Image>();

        Sprite barSprite = Resources.Load<Sprite>("UI/Panels/ui_hud_backing");
        if (barSprite != null)
        {
            topBarBg.sprite = barSprite;
            topBarBg.type = Image.Type.Sliced;
        }
        else
        {
            topBarBg.color = new Color(0.05f, 0.05f, 0.08f, 0.9f);
        }

        RectTransform topRect = topBar.GetComponent<RectTransform>();
        topRect.anchorMin = new Vector2(0, 1);
        topRect.anchorMax = Vector2.one;
        topRect.pivot = new Vector2(0.5f, 1f);
        topRect.anchoredPosition = Vector2.zero;
        topRect.sizeDelta = new Vector2(0, 110);

        // Player Profile Info
        GameObject profileGroup = new GameObject("PlayerProfileGroup");
        profileGroup.transform.SetParent(topBar.transform, false);
        RectTransform profRect = profileGroup.AddComponent<RectTransform>();
        profRect.anchorMin = new Vector2(0, 0.5f);
        profRect.anchorMax = new Vector2(0, 0.5f);
        profRect.pivot = new Vector2(0, 0.5f);
        profRect.anchoredPosition = new Vector2(30, 0);
        profRect.sizeDelta = new Vector2(400, 80);

        string pName = SaveManager.SaveData != null ? SaveManager.SaveData.playerName : "Sovereign";
        int pLevel = SaveManager.SaveData != null ? SaveManager.SaveData.level : 1;

        CreateTextElement(profileGroup.transform, "PlayerNameText", $"{pName}\n<size=22><color=#FFD700>Lv. {pLevel} Leader</color></size>", Vector2.zero, 28, TextAnchor.MiddleLeft);

        // Currency Badges Group
        GameObject resourceGroup = new GameObject("ResourceGroup");
        resourceGroup.transform.SetParent(topBar.transform, false);
        RectTransform resRect = resourceGroup.AddComponent<RectTransform>();
        resRect.anchorMin = new Vector2(1, 0.5f);
        resRect.anchorMax = new Vector2(1, 0.5f);
        resRect.pivot = new Vector2(1, 0.5f);
        resRect.anchoredPosition = new Vector2(-30, 0);
        resRect.sizeDelta = new Vector2(600, 80);

        HorizontalLayoutGroup hlg = resourceGroup.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleRight;
        hlg.spacing = 15;
        hlg.childControlWidth = false;

        int goldVal = SaveManager.SaveData != null ? SaveManager.SaveData.gold : 1000;
        int gemsVal = SaveManager.SaveData != null ? SaveManager.SaveData.gems : 150;
        int stamVal = SaveManager.SaveData != null ? SaveManager.SaveData.stamina : 100;
        int maxStamVal = SaveManager.SaveData != null ? SaveManager.SaveData.maxStamina : 100;

        goldHudText = CreateResourcePill(resourceGroup.transform, $"Gold: {goldVal}", new Color(0.85f, 0.68f, 0.15f, 0.95f));
        gemsHudText = CreateResourcePill(resourceGroup.transform, $"Gems: {gemsVal}", new Color(0.55f, 0.25f, 0.85f, 0.95f));
        energyHudText = CreateResourcePill(resourceGroup.transform, $"Energy: {stamVal}/{maxStamVal}", new Color(0.2f, 0.65f, 0.35f, 0.95f));

        // 4. Bottom Navigation Dock
        BuildBottomDock();
    }

    private void BuildBottomDock()
    {
        GameObject dockObj = new GameObject("BottomNavDock");
        dockObj.transform.SetParent(homeCanvasObj.transform, false);

        Image dockBg = dockObj.AddComponent<Image>();
        dockBg.color = new Color(0.08f, 0.08f, 0.12f, 0.85f);

        RectTransform dockRect = dockObj.GetComponent<RectTransform>();
        dockRect.anchorMin = new Vector2(0f, 0f);
        dockRect.anchorMax = new Vector2(1f, 0f);
        dockRect.pivot = new Vector2(0.5f, 0f);
        dockRect.sizeDelta = new Vector2(0, 130);

        HorizontalLayoutGroup hlg = dockObj.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 30;
        hlg.childControlWidth = false;

        CreateNavButton(dockObj.transform, "STORY", OpenStoryCampaign, new Vector2(220, 85), new Color(0.2f, 0.35f, 0.6f));
        CreateNavButton(dockObj.transform, "CARDS", OpenCollection, new Vector2(220, 85), new Color(0.2f, 0.5f, 0.35f));
        CreateNavButton(dockObj.transform, "SHOP", OpenShop, new Vector2(220, 85), new Color(0.6f, 0.45f, 0.2f));
        CreateNavButton(dockObj.transform, "TO BATTLE", OnToBattleClicked, new Vector2(280, 95), new Color(0.8f, 0.25f, 0.2f));

        // Secondary, clearly smaller/muted than the four primary nav buttons above - same
        // underlying button style/asset (CreateNavButton), deliberately not equal-weight with
        // them. Offline prototype only: see GameBootstrap.StartApprovedTutorialBattle.
        CreateNavButton(dockObj.transform, "START TUTORIAL", OnStartTutorialClicked, new Vector2(160, 55), new Color(0.3f, 0.45f, 0.45f));
    }

    private void CreateNavButton(Transform parent, string label, UnityEngine.Events.UnityAction action, Vector2 size, Color btnColor)
    {
        GameObject btnObj = new GameObject($"Btn_{label}");
        btnObj.transform.SetParent(parent, false);

        Image img = btnObj.AddComponent<Image>();

        Sprite btnSprite = Resources.Load<Sprite>("UI/Buttons/btn_play_massive");
        if (btnSprite != null)
        {
            img.sprite = btnSprite;
            img.color = btnColor;
        }
        else
        {
            img.color = btnColor;
        }

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(action);

        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.sizeDelta = size;

        CreateTextElement(btnObj.transform, "Text", label, Vector2.zero, 28, TextAnchor.MiddleCenter);
    }

    private void OpenStoryCampaign()
    {
        if (homeCanvasObj != null) homeCanvasObj.SetActive(false);

        CampaignMapPresenter campaign = gameObject.GetComponent<CampaignMapPresenter>();
        if (campaign == null) campaign = gameObject.AddComponent<CampaignMapPresenter>();

        campaign.Initialize(
            onBackToHome: () =>
            {
                if (homeCanvasObj != null) homeCanvasObj.SetActive(true);
                RefreshTopHUD();
                if (campaign != null) Destroy(campaign);
            },
            onLaunchBattle: (stageData) =>
            {
                currentActiveStage = stageData;
                Debug.Log($"Launching Battle for Stage {stageData.stageId}: {stageData.title}");
                if (campaign != null) Destroy(campaign);
                OnToBattleClicked();
            }
        );
    }

    private void OpenDeckBuilder()
    {
        if (homeCanvasObj != null) homeCanvasObj.SetActive(false);

        DeckBuilderPresenter deckBuilder = gameObject.GetComponent<DeckBuilderPresenter>();
        if (deckBuilder == null) deckBuilder = gameObject.AddComponent<DeckBuilderPresenter>();

        deckBuilder.Initialize(
            onBackToHome: () =>
            {
                if (homeCanvasObj != null) homeCanvasObj.SetActive(true);
                SaveManager.Save();
                if (deckBuilder != null) Destroy(deckBuilder);
            }
        );
    }

    private void OpenCollection()
    {
        if (homeCanvasObj != null) homeCanvasObj.SetActive(false);

        CollectionPresenter collection = gameObject.GetComponent<CollectionPresenter>();
        if (collection == null) collection = gameObject.AddComponent<CollectionPresenter>();

        collection.Initialize(
            onBackToHome: () =>
            {
                if (homeCanvasObj != null) homeCanvasObj.SetActive(true);
                SaveManager.Save();
                if (collection != null) Destroy(collection);
            },
            onOpenDeckBuilder: () =>
            {
                if (collection != null) Destroy(collection);
                OpenDeckBuilder();
            }
        );
    }

    private void OpenShop()
    {
        if (homeCanvasObj != null) homeCanvasObj.SetActive(false);

        ShopPresenter shop = gameObject.GetComponent<ShopPresenter>();
        if (shop == null) shop = gameObject.AddComponent<ShopPresenter>();

        shop.Initialize(
            profile: SaveManager.SaveData,
            onBackToHome: () =>
            {
                SaveManager.Save();
                if (homeCanvasObj != null) homeCanvasObj.SetActive(true);
                RefreshTopHUD();
                if (shop != null) Destroy(shop);
            }
        );
    }

    private void RefreshTopHUD()
    {
        if (SaveManager.SaveData == null) return;
        if (goldHudText != null) goldHudText.text = $"Gold: {SaveManager.SaveData.gold}";
        if (gemsHudText != null) gemsHudText.text = $"Gems: {SaveManager.SaveData.gems}";
        if (energyHudText != null) energyHudText.text = $"Energy: {SaveManager.SaveData.stamina}/{SaveManager.SaveData.maxStamina}";
    }

    private void ShowTutorialDialogue()
    {
        dialogueOverlayObj = new GameObject("StoryDialogueOverlay");
        dialogueOverlayObj.transform.SetParent(homeCanvasObj.transform, false);

        Image dimBg = dialogueOverlayObj.AddComponent<Image>();
        dimBg.color = new Color(0f, 0f, 0f, 0.65f);

        RectTransform dimRect = dialogueOverlayObj.GetComponent<RectTransform>();
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.sizeDelta = Vector2.zero;

        GameObject boxObj = new GameObject("DialogueBox");
        boxObj.transform.SetParent(dialogueOverlayObj.transform, false);

        Image boxBg = boxObj.AddComponent<Image>();
        boxBg.color = new Color(0.1f, 0.12f, 0.18f, 0.95f);

        RectTransform boxRect = boxObj.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0f);
        boxRect.anchorMax = new Vector2(0.5f, 0f);
        boxRect.pivot = new Vector2(0.5f, 0f);
        boxRect.anchoredPosition = new Vector2(0, 30);
        boxRect.sizeDelta = new Vector2(1400, 240);

        GameObject portraitObj = new GameObject("SpeakerPortrait");
        portraitObj.transform.SetParent(boxObj.transform, false);
        portraitImage = portraitObj.AddComponent<Image>();

        RectTransform portRect = portraitObj.GetComponent<RectTransform>();
        portRect.anchorMin = new Vector2(0, 0.5f);
        portRect.anchorMax = new Vector2(0, 0.5f);
        portRect.pivot = new Vector2(0, 0.5f);
        portRect.anchoredPosition = new Vector2(30, 0);
        portRect.sizeDelta = new Vector2(180, 180);

        GameObject nameObj = new GameObject("SpeakerName");
        nameObj.transform.SetParent(boxObj.transform, false);
        speakerNameText = nameObj.AddComponent<Text>();
        speakerNameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        speakerNameText.fontSize = 32;
        speakerNameText.fontStyle = FontStyle.Bold;
        speakerNameText.color = new Color(1f, 0.85f, 0.3f);

        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0.5f);
        nameRect.anchorMax = new Vector2(0, 0.5f);
        nameRect.pivot = new Vector2(0, 0.5f);
        nameRect.anchoredPosition = new Vector2(240, 55);
        nameRect.sizeDelta = new Vector2(1100, 45);

        GameObject bodyObj = new GameObject("DialogueBody");
        bodyObj.transform.SetParent(boxObj.transform, false);
        dialogueBodyText = bodyObj.AddComponent<Text>();
        dialogueBodyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        dialogueBodyText.fontSize = 26;
        dialogueBodyText.color = Color.white;

        RectTransform bodyRect = bodyObj.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0, 0.5f);
        bodyRect.anchorMax = new Vector2(0, 0.5f);
        bodyRect.pivot = new Vector2(0, 0.5f);
        bodyRect.anchoredPosition = new Vector2(240, -15);
        bodyRect.sizeDelta = new Vector2(1100, 95);

        Button tapBtn = dialogueOverlayObj.AddComponent<Button>();
        tapBtn.onClick.AddListener(AdvanceDialogue);

        RenderCurrentLine();
    }

    private void RenderCurrentLine()
    {
        if (currentDialogueIndex < tutorialDialogue.Count)
        {
            DialogueLine line = tutorialDialogue[currentDialogueIndex];
            speakerNameText.text = line.speaker;
            dialogueBodyText.text = line.text;

            Sprite pSprite = Resources.Load<Sprite>(line.portraitPath);
            if (pSprite != null)
                portraitImage.sprite = pSprite;
            else
                portraitImage.color = new Color(0.4f, 0.3f, 0.5f);
        }
        else
        {
            if (dialogueOverlayObj != null)
                Destroy(dialogueOverlayObj);
        }
    }

    private void AdvanceDialogue()
    {
        currentDialogueIndex++;
        RenderCurrentLine();
    }

    private Text CreateResourcePill(Transform parent, string text, Color bgColor)
    {
        GameObject pill = new GameObject("ResourcePill");
        pill.transform.SetParent(parent, false);

        Image img = pill.AddComponent<Image>();
        img.color = bgColor;

        RectTransform rect = pill.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(170, 50);

        GameObject textObj = CreateTextElement(pill.transform, "PillText", text, Vector2.zero, 22, TextAnchor.MiddleCenter);
        return textObj.GetComponent<Text>();
    }

    private void OnToBattleClicked()
    {
        Debug.Log("Transitioning to Battle...");
        if (homeCanvasObj != null)
        {
            homeCanvasObj.SetActive(false);
        }

        GameBootstrap.Instance?.SetBattleCanvasVisible(true);
    }

    /// <summary>Secondary entry point - approved offline tutorial battle only. Does not
    /// change OnToBattleClicked()'s own behavior; the two are independent.</summary>
    private void OnStartTutorialClicked()
    {
        Debug.Log("Transitioning to the approved tutorial battle...");
        if (homeCanvasObj != null)
        {
            homeCanvasObj.SetActive(false);
        }

        GameBootstrap.Instance?.StartApprovedTutorialBattle();
        GameBootstrap.Instance?.SetBattleCanvasVisible(true);
    }

    private GameObject CreateTextElement(Transform parent, string objectName, string content, Vector2 position, int fontSize, TextAnchor alignment)
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
        rect.sizeDelta = new Vector2(380, 80);

        return textObj;
    }
}
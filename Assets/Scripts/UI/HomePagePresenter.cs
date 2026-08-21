using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MyriadOfDragons.UI;
using MyriadOfDragons.Data;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Save;
using MyriadOfDragons.Story;

public class HomePagePresenter : MonoBehaviour
{
    private GameObject homeCanvasObj;
    private BattleController _battleController;
    private GameBootstrap _boundGameBootstrap;
    private CampaignMapPresenter _storyCampaign;

    // HUD Text References
    private Text goldHudText;
    private Text gemsHudText;
    private Text energyHudText;

    // Current Active Stage Track
    private CampaignStageData currentActiveStage;

    void Start()
    {
        SaveManager.Load();

        BuildHomePageUI();

        // Home is the phase-1 proof surface and should own first render. Keep battle canvas
        // hidden until the explicit TO BATTLE action is invoked.
        GameBootstrap.Instance?.SetBattleCanvasVisible(false);

        // Narrative onboarding (ShowTutorialDialogue) is deferred pending Narrative-owned
        // approved content and a working dialogue panel - it must not block Home startup.

        // Subscribe to Claude's Battle Outcome Event
        BindBattleControllerForTests(FindAnyObjectByType<BattleController>());

        // Battle/metagame return handoff: restores Home once GameBootstrap's "Return to City"
        // has hidden the battle canvas - see BindGameBootstrapForTests' own comment.
        BindGameBootstrapForTests(GameBootstrap.Instance);
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

    /// <summary>Exposed for tests: BuildHomePageUI() is private and only ever called from
    /// Start(), which never fires in EditMode - this is the only way a test can build Home's
    /// UI and inspect the result without Play Mode.</summary>
    public void BuildHomePageUIForTests() => BuildHomePageUI();

    /// <summary>Exposed for tests: invokes the exact private handler the real "To Battle" tile's
    /// Button.onClick calls (see CreateHeroTile's own wiring) - the saved-deck gate, the
    /// starter-entitlement call, and the Deck-Builder-vs-Battle branch all live in
    /// OnToBattleClicked itself and are not reimplemented here.</summary>
    public void OnToBattleClickedForTests() => OnToBattleClicked();

    /// <summary>Exposed for tests: read-only access to the canvas BuildHomePageUIForTests()
    /// just built, so a test can inspect it without a broader production accessor.</summary>
    public GameObject HomeCanvasObjectForTests => homeCanvasObj;

    /// <summary>Exposed for tests: sets currentActiveStage exactly as OpenStoryCampaign's own
    /// onLaunchBattle callback does when a real Story stage tile is tapped - the only way an
    /// EditMode test can put HandleMatchCompleted into its stage-scoped reward/unlock path
    /// without building and clicking through the full CampaignMapPresenter UI (which does not
    /// run in EditMode). currentActiveStage itself stays private.</summary>
    public void SetActiveStageForTests(CampaignStageData stage) => currentActiveStage = stage;

    /// <summary>Exposed for tests: the currently-active campaign stage, or null - so a test can
    /// confirm HandleReturnToCityRequested's own reset without a broader accessor.</summary>
    public CampaignStageData ActiveStageForTests => currentActiveStage;

    /// <summary>Exposed for tests: Start() never fires in EditMode, so this is the only way a
    /// test can subscribe HandleReturnToCityRequested to a real GameBootstrap and exercise the
    /// real return-to-city handoff (GameBootstrap.ReturnToCityForTests() -&gt;
    /// OnReturnToCityRequested -&gt; this). HandleReturnToCityRequested itself stays private;
    /// only this subscription step is exposed - same pattern as BindBattleControllerForTests.
    /// Stores the bound instance (rather than always reading the static GameBootstrap.Instance
    /// in OnDestroy) so unsubscription always targets the exact instance subscribed to, even if
    /// a later test run left a different GameBootstrap as the current Instance.</summary>
    public void BindGameBootstrapForTests(GameBootstrap bootstrap)
    {
        _boundGameBootstrap = bootstrap;
        if (_boundGameBootstrap != null)
        {
            _boundGameBootstrap.OnReturnToCityRequested += HandleReturnToCityRequested;
        }
    }

    /// <summary>The other half of the battle/metagame handoff: restores Home once
    /// GameBootstrap's "Return to City" has hidden the battle canvas. Visibility/HUD refresh
    /// only - reward and progression are already fully resolved by the time this ever fires
    /// (both guards run at match resolution, well before the player reaches this button), so
    /// this must never call ShowTutorialDialogue, touch profile data, or start a match.
    ///
    /// currentActiveStage is cleared here too - a local UI field, not profile data, so clearing
    /// it does not conflict with the rule above. Safe specifically because HandleMatchCompleted
    /// has already fully resolved and saved the just-finished match's reward/unlock by the time
    /// this fires; clearing it now only prevents a LATER, different battle entered without going
    /// back through Story from being mis-attributed to whichever stage was played previously.</summary>
    private void HandleReturnToCityRequested()
    {
        currentActiveStage = null;
        if (homeCanvasObj != null)
        {
            homeCanvasObj.SetActive(true);
            RefreshTopHUD();
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

        if (_boundGameBootstrap != null)
        {
            _boundGameBootstrap.OnReturnToCityRequested -= HandleReturnToCityRequested;
            _boundGameBootstrap = null;
        }
    }

    /// <summary>
    /// Chapter 1 progression contract (release feature): a stage victory grants that stage's
    /// configured reward exactly once, unlocks the immediately next stage (per
    /// CampaignMapPresenter.GetNextStageId - the sole order authority), and persists
    /// immediately. Replaying an already-cleared stage (Play Again, Reset Lineup, or simply
    /// re-launching it from Story) must never grant a second reward - enforced by
    /// claimedStageRewardIds, a per-stage-id set that grants that stage's gold/gems exactly once.
    /// The next-stage unlock is a separate, idempotent check that runs on every victory of this
    /// stage regardless of claim status (progression-repair fix, 2026-08-21): a save whose claim
    /// already happened through some earlier path without also recording the next unlock must
    /// still self-heal the next time the player wins this same stage, rather than staying stuck
    /// forever because the reward-grant branch below it never runs again.
    ///
    /// currentActiveStage == null (a battle entered without going through Story at all) keeps
    /// its pre-existing flat 250 Gold / 25 Gems reward, granted on every such victory with no
    /// dedup - unrelated to Chapter 1 stage progression and explicitly out of this feature's
    /// scope (no economy/reward-value change). NormalVictory_ThroughTheRealRewardHandler_
    /// StillGrantsGoldAndGems (HomePageTutorialRewardGuardTests) locks this in.
    /// </summary>
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

        // Stage defeat grants and unlocks nothing (Chapter 1 progression contract #3) - already
        // true by construction below (only the IsVictory branch ever mutates the profile), made
        // explicit here so a reader does not have to infer it from an absent else-branch.
        if (!result.IsVictory) return;

        PlayerProfile profile = SaveManager.SaveData;
        if (profile == null) return;

        if (currentActiveStage == null)
        {
            // No active campaign stage - pre-existing flat reward, unrelated to Chapter 1
            // progression, deliberately left exactly as it was (see this method's own comment).
            profile.gold += 250;
            profile.gems += 25;
            SaveManager.Save();
            RefreshTopHUD();
            Debug.Log("[Metagame] Awarded 250 Gold & 25 Gems for Victory (no active campaign stage)!");
            return;
        }

        string stageId = currentActiveStage.stageId;

        // Campaign match-context lifecycle contract, requirement 3: a Campaign victory clears
        // the active battle-CONFIGURATION context (GameBootstrap._pendingCampaignStage) as soon
        // as it resolves - fresh clear or already-claimed replay alike, since either way this
        // campaign-match session is now finished. A later "Play Again" from this same result
        // screen therefore falls through to the ordinary random-enemy path instead of refighting
        // this stage's boss again - retrying the identical configured deck (requirement 2) is a
        // DEFEAT-only behavior; StartNewMatch never reaches this method on a loss (see the
        // !result.IsVictory guard above), so this line never fires there. Deliberately does NOT
        // touch currentActiveStage itself - that field is reward-attribution state, still owned
        // exclusively by HandleReturnToCityRequested's own clear (see its comment), and clearing
        // it here would break the existing claimedStageRewardIds replay-dedup check just below.
        GameBootstrap.Instance?.SetPendingCampaignStageForNextMatch(null);

        // Progression-repair fix, 2026-08-21 (owner report: won 1-2, returned to City, Story
        // still showed 1-3 locked): the next-stage unlock must run on EVERY campaign victory of
        // this stage, not only a first clear. It used to live only below the claimedStageRewardIds
        // check, inside the never-replayed first-clear branch - so a save whose claim already
        // happened (an earlier session, before unlockedStageIds' "1-1"-only default landed, or any
        // other path that set claimedStageRewardIds without also recording the next unlock) could
        // win this same stage forever afterward and never repair the missing next-stage unlock, since
        // every later victory took the early-return branch below and skipped it entirely. Computing
        // and applying it here, before that branch, makes it idempotent and unconditional: it runs
        // whether this is a first clear or a replay, and is a no-op once the next stage is already
        // present - never a second reward, never a duplicate list entry.
        string nextStageId = CampaignMapPresenter.GetNextStageId(stageId);
        bool nextStageNewlyUnlocked = nextStageId != null && !profile.unlockedStageIds.Contains(nextStageId);
        if (nextStageNewlyUnlocked)
        {
            profile.unlockedStageIds.Add(nextStageId);
        }

        if (profile.claimedStageRewardIds.Contains(stageId))
        {
            // Already claimed on an earlier clear - no duplicate reward or currency, but the
            // next-stage repair above must still be saved if it just changed anything.
            if (nextStageNewlyUnlocked)
            {
                SaveManager.Save();
                Debug.Log($"[Metagame] Stage {stageId} already cleared - repaired missing unlock of Stage {nextStageId}.");
            }
            else
            {
                Debug.Log($"[Metagame] Stage {stageId} already cleared - no duplicate reward.");
            }
            RefreshTopHUD();
            return;
        }

        profile.gold += currentActiveStage.goldReward;
        profile.gems += currentActiveStage.gemReward;
        profile.claimedStageRewardIds.Add(stageId);

        // Idempotent: the cleared stage must already have been unlocked to have been launchable,
        // but this keeps unlockedStageIds authoritative even for a hand-edited or pre-migration
        // save that reached this point without it.
        if (!profile.unlockedStageIds.Contains(stageId))
        {
            profile.unlockedStageIds.Add(stageId);
        }

        // Saved immediately after the first-clear reward/unlock mutation (Chapter 1 progression
        // contract #7) - not deferred to OnDestroy, OpenDeckBuilder's own save-on-close, or any
        // later action, so the result survives even if the player quits before doing anything
        // else.
        SaveManager.Save();

        RefreshTopHUD();
        Debug.Log($"[Metagame] Awarded {currentActiveStage.goldReward} Gold & {currentActiveStage.gemReward} Gems " +
                  $"for first clear of Stage {stageId}!" +
                  (nextStageId != null ? $" Unlocked Stage {nextStageId}." : " Chapter 1 complete."));

        // Chapter 1 post-victory story bridge: first clear only (replays already returned above).
        // Rewards/unlocks are already persisted - story is presentation on top of that contract,
        // never a gate. Missing sequence is a silent no-op so stages without copy still clear.
        TryPlayCampaignPostVictoryStory(stageId);
    }

    /// <summary>Resolves "&lt;stageId&gt;_post" from StoryDatabase the same way Campaign launch
    /// resolves "&lt;stageId&gt;_pre". Exposed for EditMode contract tests.</summary>
    public static string GetCampaignPostVictoryStoryKey(string stageId) => $"{stageId}_post";

    private static void TryPlayCampaignPostVictoryStory(string stageId)
    {
        if (string.IsNullOrEmpty(stageId)) return;
        StorySequence seq = StoryDatabase.GetSequence(GetCampaignPostVictoryStoryKey(stageId));
        if (seq == null) return;
        StoryOverlayPresenter.PlaySequence(seq, onCompleted: null);
    }

    private void BuildHomePageUI()
    {
        // HomeV3 Release Fallback Spec Implementation
        // Reference: HomeV3_Release_Fallback_Build_Spec.md
        // Canvas setup: 1920×1080 landscape with normalised anchor coordinates
        
        Canvas canvas = UISharedFoundation.CreateScreenCanvas("HomePageCanvas", new Vector2(1920, 1080));
        homeCanvasObj = canvas.gameObject;
        homeCanvasObj.transform.SetParent(transform, false);

        // Background field - preserve existing backdrop if present; otherwise neutral charcoal
        UISharedFoundation.CreateFullscreenBackground(homeCanvasObj.transform, "UI/Backdrops/Zihan_City_NO NAMES", new Color(0.15f, 0.14f, 0.18f));
        Image backgroundImage = homeCanvasObj.transform.Find("Background")?.GetComponent<Image>();
        if (backgroundImage != null)
        {
            backgroundImage.raycastTarget = false;
        }

        // === IDENTITY SURFACE (text-only fallback, no opaque crest) ===
        // Region (pixels): (24,18)–(704,100) from spec top-origin
        GameObject identityRoot = new GameObject("IdentityRoot", typeof(RectTransform));
        identityRoot.transform.SetParent(homeCanvasObj.transform, false);
        SetScreenRectFromTopLeftPixels(identityRoot.GetComponent<RectTransform>(), 24, 18, 704, 100);

        // Neutral identity surface (no frame art needed; text-only layout)
        string pName = SaveManager.SaveData != null ? SaveManager.SaveData.playerName : "Sovereign";
        int pLevel = SaveManager.SaveData != null ? SaveManager.SaveData.level : 1;

        // Player name text (bold, large)
        Text playerNameText = UISharedFoundation.CreateText(
            identityRoot.transform, "PlayerName", pName.ToUpperInvariant(),
            MyriadOfDragons.UI.UITextRole.Display, TextAnchor.MiddleLeft, HexColor("#F2E5C9"), true, new Vector2(300f, 40f));
        playerNameText.fontSize = 30;
        playerNameText.fontStyle = FontStyle.Bold;
        playerNameText.raycastTarget = false;
        SetLocalNormalisedRect(playerNameText.rectTransform, 0.05f, 0.55f, 0.95f, 1.0f);

        // Player level/role line
        Text playerLevelText = UISharedFoundation.CreateText(
            identityRoot.transform, "PlayerLevelRole", $"Level {pLevel} · Sovereign",
            MyriadOfDragons.UI.UITextRole.Body, TextAnchor.MiddleLeft, HexColor("#B8A68F"), true, new Vector2(300f, 20f));
        playerLevelText.fontSize = 18;
        playerLevelText.raycastTarget = false;
        SetLocalNormalisedRect(playerLevelText.rectTransform, 0.05f, 0.0f, 0.95f, 0.45f);

        // === RESOURCE ROW (three approved pill backings) ===
        // Region (pixels): (900,18)–(1896,90) from spec top-origin
        GameObject resourceRow = new GameObject("ResourceRow", typeof(RectTransform));
        resourceRow.transform.SetParent(homeCanvasObj.transform, false);
        SetScreenRectFromTopLeftPixels(resourceRow.GetComponent<RectTransform>(), 900, 18, 1896, 90);

        int goldVal = SaveManager.SaveData != null ? SaveManager.SaveData.gold : 1000;
        int gemsVal = SaveManager.SaveData != null ? SaveManager.SaveData.gems : 150;
        int stamVal = SaveManager.SaveData != null ? SaveManager.SaveData.stamina : 100;
        int maxStamVal = SaveManager.SaveData != null ? SaveManager.SaveData.maxStamina : 100;

        // Gold pill: left 0-33% of row
        goldHudText = CreateResourcePill(resourceRow.transform, "home_resource_gold_pill_v3", 
            "Gold", $"{goldVal}", 0.0f, 0.33f);

        // Gems pill: middle 33-67% of row
        gemsHudText = CreateResourcePill(resourceRow.transform, "home_resource_gems_pill_v3",
            "Gems", $"{gemsVal}", 0.34f, 0.67f);

        // Energy pill: right 67-100% of row
        energyHudText = CreateResourcePill(resourceRow.transform, "home_resource_energy_pill_v3",
            "Energy", $"{stamVal}/{maxStamVal}", 0.68f, 1.0f);

        // === TUTORIAL STRIP (neutral fallback, no banner art) ===
        // Region: (0.063, 0.107)–(0.938, 0.169)
        BuildNeutralTutorialStrip();

        // === NAVIGATION STAGE (four independent hero tiles, no decorative dock) ===
        // Region: (0.013, 0.670)–(0.988, 0.974)
        BuildNavigationStage();
    }

    private void BuildNeutralTutorialStrip()
    {
        // Tutorial strip using intentional neutral fallback (no banner image art - it needs slice metadata)
        // Region (pixels): (120,116)–(1800,182) from spec top-origin
        
        GameObject tutorialRoot = new GameObject("TutorialRoot", typeof(RectTransform), typeof(Image));
        tutorialRoot.transform.SetParent(homeCanvasObj.transform, false);
        
        // Neutral charcoal background
        Image tutorialBg = tutorialRoot.GetComponent<Image>();
        tutorialBg.color = HexColor("#2C2C2C");
        tutorialBg.raycastTarget = false;
        
        SetScreenRectFromTopLeftPixels(tutorialRoot.GetComponent<RectTransform>(), 120, 116, 1800, 182);

        // Tutorial copy (text-only, no emblem)
        Text tutorialCopy = UISharedFoundation.CreateText(
            tutorialRoot.transform, "TutorialCopy",
            "Ready to lead your forces into battle? The Empire awaits your command.",
            MyriadOfDragons.UI.UITextRole.Body, TextAnchor.MiddleLeft, HexColor("#F2E5C9"), true, new Vector2(1000f, 50f));
        tutorialCopy.fontSize = 22;
        tutorialCopy.raycastTarget = false;
        SetLocalNormalisedRect(tutorialCopy.rectTransform, 0.08f, 0.15f, 0.70f, 0.85f);

        // Start Tutorial button (intentional neutral action surface, no button art)
        GameObject startTutorialBtn = new GameObject("StartTutorialButtonRoot", typeof(RectTransform), typeof(Image), typeof(Button));
        startTutorialBtn.transform.SetParent(tutorialRoot.transform, false);
        
        Image btnBg = startTutorialBtn.GetComponent<Image>();
        btnBg.color = HexColor("#1A3A4A"); // Neutral dark teal
        Button btn = startTutorialBtn.GetComponent<Button>();
        btn.onClick.AddListener(OnStartTutorialClicked);
        btn.transition = Selectable.Transition.ColorTint;

        RectTransform btnRect = startTutorialBtn.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.75f, 0.5f);
        btnRect.anchorMax = new Vector2(0.75f, 0.5f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.sizeDelta = new Vector2(180f, 45f);

        Text btnLabel = UISharedFoundation.CreateText(
            startTutorialBtn.transform, "ActionLabel", "START TUTORIAL",
            MyriadOfDragons.UI.UITextRole.Display, TextAnchor.MiddleCenter, HexColor("#F2E5C9"), true, new Vector2(160f, 40f));
        btnLabel.fontSize = 18;
        btnLabel.fontStyle = FontStyle.Bold;
        btnLabel.raycastTarget = false;
    }

    private void BuildNavigationStage()
    {
        // Four independent hero navigation tiles: Story, Cards, Shop, To Battle
        // Each tile is 440×306, with consistent spacing
        // Overall region (pixels): (24,724)–(1896,1052) from spec top-origin

        GameObject navStage = new GameObject("NavigationStage", typeof(RectTransform));
        navStage.transform.SetParent(homeCanvasObj.transform, false);
        SetScreenRectFromTopLeftPixels(navStage.GetComponent<RectTransform>(), 24, 724, 1896, 1052);

        // Story tile (pixels): (38,724)–(478,1030)
        CreateHeroTile("Story", "home_icon_story_v3", 38, 724, 478, 1030, OpenStoryCampaign, navStage.transform);

        // Cards tile (pixels): (498,724)–(938,1030)
        CreateHeroTile("Cards", "home_icon_cards_v3", 498, 724, 938, 1030, OpenCollection, navStage.transform);

        // Shop tile (pixels): (958,724)–(1398,1030)
        CreateHeroTile("Shop", "home_icon_shop_v3", 958, 724, 1398, 1030, OpenShop, navStage.transform);

        // To Battle tile (pixels): (1418,724)–(1858,1030)
        CreateHeroTile("To Battle", "home_icon_battle_v3", 1418, 724, 1858, 1030, () => OnToBattleClicked(), navStage.transform);
    }

    private void CreateHeroTile(string label, string heroSprite, float left, float top, float right, float bottom,
        UnityEngine.Events.UnityAction action, Transform parent)
    {
        // Hero tile blueprint: HeroTileButtonRoot (440×306) with hero art and label
        // Positioned using pixel coordinates from screen top-origin
        
        GameObject tileRoot = new GameObject($"Btn_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
        tileRoot.transform.SetParent(parent, false);

        Image tileBackground = tileRoot.GetComponent<Image>();
        tileBackground.color = new Color(0f, 0f, 0f, 0f);
        tileBackground.raycastTarget = true;

        Button tileBtn = tileRoot.GetComponent<Button>();
        tileBtn.targetGraphic = tileBackground;
        tileBtn.onClick.AddListener(action);
        tileBtn.transition = Selectable.Transition.ColorTint;
        tileBtn.colors = new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f),
            pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f),
            selectedColor = Color.white,
            disabledColor = Color.gray,
            colorMultiplier = 1f,
            fadeDuration = 0.1f
        };
        
        // Tile bounds are relative to the NavStage parent, not the screen.
        // Use the actual parent rect bounds so each tile is exactly 440×306 inside the stage.
        RectTransform tileParent = parent as RectTransform;
        if (tileParent != null)
        {
            SetChildRectFromParentTopOrigin(tileRoot.GetComponent<RectTransform>(), tileParent, 24f, 724f, 1896f, 1052f, left, top, right, bottom);
        }

        // Hero art layer (approved transparent foreground or intentional fallback)
        GameObject heroArtObj = new GameObject("HeroArt", typeof(RectTransform), typeof(Image));
        heroArtObj.transform.SetParent(tileRoot.transform, false);
        
        Image heroArt = heroArtObj.GetComponent<Image>();
        heroArt.sprite = LoadHomeSprite(heroSprite);
        heroArt.preserveAspect = true;
        heroArt.raycastTarget = false;
        heroArt.color = heroArt.sprite != null ? Color.white : HexColor("#2C3E50"); // Neutral fallback
        
        // Hero art safe box: left 7%, top 7%, right 93%, bottom 69% (top-origin within tile)
        SetLocalTopOriginRect(heroArt.rectTransform, 0.07f, 0.07f, 0.93f, 0.69f);

        // Label layer (over art at bottom, inside safe box)
        Text tileLabel = UISharedFoundation.CreateText(
            tileRoot.transform, "TileLabel", label,
            MyriadOfDragons.UI.UITextRole.Display, TextAnchor.MiddleCenter, HexColor("#F2E5C9"), true, new Vector2(400f, 60f));
        tileLabel.fontSize = 28;
        tileLabel.fontStyle = FontStyle.Bold;
        tileLabel.raycastTarget = false;
        
        // Label safe box: left 9%, top 73%, right 91%, bottom 94% (top-origin within tile)
        SetLocalTopOriginRect(tileLabel.rectTransform, 0.09f, 0.73f, 0.91f, 0.94f);
    }

    private static void SetLocalTopOriginRect(RectTransform rect, float leftPercent, float topPercent, float rightPercent, float bottomPercent)
    {
        rect.anchorMin = new Vector2(leftPercent, 1f - bottomPercent);
        rect.anchorMax = new Vector2(rightPercent, 1f - topPercent);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetChildRectFromParentTopOrigin(RectTransform rect, RectTransform parent, float parentLeftPx, float parentTopPx, float parentRightPx, float parentBottomPx, float leftPx, float topPx, float rightPx, float bottomPx)
    {
        float parentWidth = Mathf.Max(1f, parentRightPx - parentLeftPx);
        float parentHeight = Mathf.Max(1f, parentBottomPx - parentTopPx);

        float localLeft = leftPx - parentLeftPx;
        float localRight = rightPx - parentLeftPx;
        float localTop = topPx - parentTopPx;
        float localBottom = bottomPx - parentTopPx;

        rect.anchorMin = new Vector2(localLeft / parentWidth, 1f - localBottom / parentHeight);
        rect.anchorMax = new Vector2(localRight / parentWidth, 1f - localTop / parentHeight);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetLocalNormalisedRect(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = new Vector2(left, bottom);
        rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// Sets a screen-level RectTransform using HomeV3 spec pixels from the top-left origin.
    /// This conversion is only for root-level screen elements, never for local child layout.
    /// </summary>
    private static void SetScreenRectFromTopLeftPixels(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = new Vector2(left / 1920f, 1f - bottom / 1080f);
        rect.anchorMax = new Vector2(right / 1920f, 1f - top / 1080f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Sprite LoadHomeSprite(string fileName)
    {
        return Resources.Load<Sprite>($"UI/HomeV3/{fileName}");
    }

    private static Color HexColor(string hex, float alpha = 1f)
    {
        if (ColorUtility.TryParseHtmlString(hex, out Color parsed))
        {
            parsed.a = alpha;
            return parsed;
        }
        return Color.white;
    }

    /// <summary>Exposed for tests: the real "Story" tile's Button.onClick calls the private
    /// OpenStoryCampaign() directly - needed to build a real CampaignMapPresenter (with its own
    /// persistent status text and Launch Battle button) for the Campaign launch feedback
    /// contract's tests, which EditMode cannot reach by clicking through Home's UI.</summary>
    public void OpenStoryCampaignForTests() => OpenStoryCampaign();

    /// <summary>Destroy is not legal outside Play Mode (this project's own non-negotiable rule -
    /// DestroyImmediate(), not Destroy(), for anything reachable from Initialize(); EditMode
    /// tests that click through the real Campaign Launch Battle button reach this directly).
    /// Production (Play Mode) behavior and timing are unchanged - Destroy still runs there.</summary>
    private static void SafeDestroy(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

    private void OpenStoryCampaign()
    {
        if (homeCanvasObj != null) homeCanvasObj.SetActive(false);

        CampaignMapPresenter campaign = gameObject.GetComponent<CampaignMapPresenter>();
        if (campaign == null)
        {
            campaign = gameObject.AddComponent<CampaignMapPresenter>();
        }

        campaign.Initialize(
            onBackToHome: () =>
            {
                if (homeCanvasObj != null) homeCanvasObj.SetActive(true);
                RefreshTopHUD();
                if (campaign != null) SafeDestroy(campaign);
            },
            onLaunchBattle: (stageData) => TryLaunchCampaignStage(stageData, campaign)
        );
    }

    /// <summary>The exact required player-facing text for each way a Campaign launch can block
    /// (Campaign launch feedback contract, requirement 2) - one source of truth so the Campaign
    /// map's status surface and the Deck Builder redirect never drift apart from each other or
    /// from what LaunchCampaignStage/TryLaunchCampaignStage actually checks.</summary>
    public const string DeckBlockedMessage = "Build and confirm a 10-card deck before launching a Campaign stage.";
    public const string StaminaBlockedMessage = "Need 1 Stamina to launch this stage.";
    public const string LockedBlockedMessage = "This stage is locked. Complete the previous stage first.";
    public const string InvalidConfigBlockedMessage = "This stage cannot launch because its battle setup is invalid.";

    /// <summary>
    /// The real Campaign-stage launch gate, in the exact required order (Campaign stamina-entry
    /// contract, requirement 2): stage battle-configuration validity, stage unlock, a confirmed
    /// player deck, then the Stamina cost - each one must already be true before the next check
    /// even runs, and Stamina is spent only once every earlier prerequisite has passed. Any
    /// failure blocks the launch completely safely (requirement 3): Battle is never revealed
    /// (OnToBattleClicked/SetBattleCanvasVisible is never reached), no campaign context is
    /// created (currentActiveStage/SetPendingCampaignStageForNextMatch untouched), and nothing is
    /// spent.
    ///
    /// Campaign launch feedback contract: the caller (CampaignMapPresenter's Launch button) needs
    /// to know WHAT happened to render the right player-facing text, so this now returns a
    /// CampaignLaunchOutcome instead of relying on Debug.Log (requirement 6) - Debug.Log here is
    /// diagnostic only, never the player's actual feedback. The one exception with its own
    /// explicit routing is a missing/invalid deck (requirement 3): this method itself closes the
    /// Campaign map and opens the existing Deck Builder with the exact required message, rather
    /// than leaving that to the caller - there is nothing useful left to show on the Campaign map
    /// in that case.
    ///
    /// Stage unlock is read directly from PlayerProfile.unlockedStageIds - the same sole
    /// authority CampaignMapPresenter.RefreshStageUnlockStatus itself reads from - rather than
    /// requiring a live CampaignMapPresenter instance, so this gate has exactly one source of
    /// truth and no second unlock-tracking system.
    ///
    /// Extracted from OpenStoryCampaign's own onLaunchBattle callback into a named method so a
    /// test can drive the real gate directly - EditMode cannot click through
    /// CampaignMapPresenter's UI (its Launch Battle button) to reach it otherwise.
    /// </summary>
    private CampaignLaunchOutcome TryLaunchCampaignStage(CampaignStageData stageData, CampaignMapPresenter campaign)
    {
        GameBootstrap bootstrap = GameBootstrap.Instance;

        // Campaign-stage battle-configuration contract, requirement 9: an invalid/missing stage
        // battle config must block launch safely, before Battle is ever revealed and before the
        // Campaign map itself is torn down - never a silent fallback deck.
        if (bootstrap != null && !bootstrap.IsCampaignStageBattleConfigValid(stageData))
        {
            return CampaignLaunchOutcome.BlockedInvalidConfig;
        }

        PlayerProfile profile = SaveManager.SaveData;
        if (profile?.unlockedStageIds == null || !profile.unlockedStageIds.Contains(stageData.stageId))
        {
            return CampaignLaunchOutcome.BlockedLocked;
        }

        if (bootstrap != null && !bootstrap.HasValidConfirmedDeckForNormalBattle())
        {
            // Requirement 3: routed directly to Deck Builder with its own existing status
            // surface - the player is never stranded on a now-meaningless Campaign map.
            bootstrap.EnsureApprovedStarterCollectionGranted();
            if (campaign != null) SafeDestroy(campaign);
            OpenDeckBuilder(entryStatusMessage: DeckBlockedMessage);
            return CampaignLaunchOutcome.BlockedNoDeck;
        }

        // Campaign stamina-entry contract, requirement 1/2/3: exactly 1 Stamina, spent only now
        // that every earlier prerequisite is confirmed valid - CurrencyManager (via GameBootstrap.
        // TrySpendCampaignStaminaForAttempt) is the sole stamina authority, requirement 9.
        if (bootstrap != null && !bootstrap.TrySpendCampaignStaminaForAttempt())
        {
            return CampaignLaunchOutcome.BlockedInsufficientStamina;
        }

        currentActiveStage = stageData;
        Debug.Log($"Launching Battle for Stage {stageData.stageId}: {stageData.title}");
        if (campaign != null) SafeDestroy(campaign);
        OnToBattleClicked(stageData);
        return CampaignLaunchOutcome.Launched;
    }

    /// <summary>Exposed for tests: EditMode cannot click through CampaignMapPresenter's Launch
    /// Battle button, so this is the only way to exercise the real launch gate (stage config,
    /// stage unlock, confirmed deck, Stamina spend, in that exact order) without reimplementing
    /// it. campaignPresenterOrNull mirrors the real call site's own CampaignMapPresenter
    /// reference, purely so it can be torn down on a successful launch exactly as production
    /// does - passing null (no live map to destroy) does not skip or weaken any of the checks
    /// above, all of which read PlayerProfile/GameBootstrap state directly.</summary>
    public CampaignLaunchOutcome LaunchCampaignStageForTests(CampaignStageData stageData, CampaignMapPresenter campaignPresenterOrNull = null) =>
        TryLaunchCampaignStage(stageData, campaignPresenterOrNull);

    private void OpenDeckBuilder(string entryStatusMessage = null)
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
            },
            entryStatusMessage: entryStatusMessage
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
        if (goldHudText != null) goldHudText.text = $"{SaveManager.SaveData.gold}";
        if (gemsHudText != null) gemsHudText.text = $"{SaveManager.SaveData.gems}";
        if (energyHudText != null) energyHudText.text = $"{SaveManager.SaveData.stamina}/{SaveManager.SaveData.maxStamina}";
    }

    private Text CreateResourcePill(Transform parent, string spriteName, string label, string value, float left, float right)
    {
        // Resource pill: approved backing + shared label/value text layout
        // No icon image, no placeholder letter, no extra object inside pill
        
        GameObject pillRoot = new GameObject("ResourcePillButtonRoot", typeof(RectTransform), typeof(Button));
        pillRoot.transform.SetParent(parent, false);
        pillRoot.GetComponent<Button>().transition = Selectable.Transition.None;
        pillRoot.GetComponent<Button>().interactable = false; // Informational only for now
        
        SetLocalNormalisedRect(pillRoot.GetComponent<RectTransform>(), left, 0.0f, right, 1.0f);

        // Pill backing (PreserveAspect, approved horizontal resource-pill backing)
        GameObject pillBacking = new GameObject("PillBacking", typeof(RectTransform), typeof(Image));
        pillBacking.transform.SetParent(pillRoot.transform, false);
        
        Image backingImage = pillBacking.GetComponent<Image>();
        backingImage.sprite = LoadHomeSprite(spriteName);
        backingImage.preserveAspect = true;
        backingImage.raycastTarget = false;
        
        // Backing fills the pill root
        SetLocalNormalisedRect(backingImage.rectTransform, 0.0f, 0.0f, 1.0f, 1.0f);

        // Shared text-only layout for all three resource pills
        Text pillLabel = UISharedFoundation.CreateText(
            pillRoot.transform, "ResourceLabel", label,
            MyriadOfDragons.UI.UITextRole.Body, TextAnchor.MiddleLeft, HexColor("#F2E5C9"), true, new Vector2(100f, 30f));
        pillLabel.fontSize = 16;
        pillLabel.raycastTarget = false;
        SetLocalNormalisedRect(pillLabel.rectTransform, 0.18f, 0.2f, 0.52f, 0.8f);

        // Value (right side, larger, never truncate)
        Text pillValue = UISharedFoundation.CreateText(
            pillRoot.transform, "ResourceValue", value,
            MyriadOfDragons.UI.UITextRole.Display, TextAnchor.MiddleRight, HexColor("#F2E5C9"), true, new Vector2(100f, 30f));
        pillValue.fontSize = 24;
        pillValue.fontStyle = FontStyle.Bold;
        pillValue.raycastTarget = false;
        SetLocalNormalisedRect(pillValue.rectTransform, 0.52f, 0.2f, 0.91f, 0.8f);

        return pillValue; // Return the value text for runtime updates
    }

    /// <summary>
    /// First-time normal-battle entry contract: gated on the same saved-deck validation
    /// GameBootstrap.StartNewMatch itself relies on (HasValidConfirmedDeckForNormalBattle wraps
    /// its own TryBuildSavedPlayerDeck - no second validator here). A missing/invalid/incomplete
    /// deck must never reveal an empty/blocked normal Battle; it opens the existing Deck Builder
    /// instead, ensuring a fresh player already owns the approved starter cards first (via the
    /// existing tutorial entitlement grant, exposed read-only for this call site - not a new
    /// grant path) so they have something to build a deck from. A valid confirmed deck falls
    /// through to the unchanged normal Battle entry route below.
    ///
    /// campaignStage: null for the ordinary Home "To Battle" tile (its own Button.onClick still
    /// calls this with no arguments - requirement 4, completely unchanged). Non-null only when
    /// OpenStoryCampaign's onLaunchBattle callback invokes this after already confirming the
    /// stage's battle configuration resolves (IsCampaignStageBattleConfigValid) - attaching it to
    /// GameBootstrap via SetPendingCampaignStageForNextMatch is the one handoff point into the
    /// existing Battle startup path (requirement 6: no second, parallel battle system).
    /// </summary>
    private void OnToBattleClicked(CampaignStageData campaignStage = null)
    {
        GameBootstrap bootstrap = GameBootstrap.Instance;
        if (bootstrap != null && !bootstrap.HasValidConfirmedDeckForNormalBattle())
        {
            bootstrap.EnsureApprovedStarterCollectionGranted();
            OpenDeckBuilder(entryStatusMessage: "Build and confirm a 10-card deck before normal Battle.");
            return;
        }

        Debug.Log("Transitioning to Battle...");
        if (homeCanvasObj != null)
        {
            homeCanvasObj.SetActive(false);
        }

        bootstrap?.SetPendingCampaignStageForNextMatch(campaignStage);
        bootstrap?.SetBattleCanvasVisible(true);
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
}
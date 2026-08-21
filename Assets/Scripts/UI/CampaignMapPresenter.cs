using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using MyriadOfDragons.Economy;
using MyriadOfDragons.Save;
using MyriadOfDragons.Story;

namespace MyriadOfDragons.UI
{
    /// <summary>Campaign launch feedback contract: what a Launch Battle attempt actually did, so
    /// the caller (CampaignMapPresenter's own Launch button) can show the right player-facing
    /// text instead of relying on Debug.Log. BlockedNoDeck is special: by the time it's returned,
    /// the Campaign map has already been closed and Deck Builder already opened with its own
    /// status message (HomePagePresenter.TryLaunchCampaignStage) - there is nothing further for
    /// the Campaign map to render for that case.</summary>
    public enum CampaignLaunchOutcome
    {
        Launched,
        BlockedInvalidConfig,
        BlockedLocked,
        BlockedNoDeck,
        BlockedInsufficientStamina,
    }

    public class CampaignMapPresenter : MonoBehaviour
    {
        private GameObject mapCanvasObj;
        private GameObject detailModalObj;
        private System.Action onBackToHomeAction;
        private System.Func<CampaignStageData, CampaignLaunchOutcome> onLaunchBattleAction;
        private Text statusText;

        // The sole order authority for Chapter 1 stage progression (Chapter 1 progression
        // contract) - static so it survives independently of any one CampaignMapPresenter
        // instance's lifecycle (the presenter is created fresh each time Story opens and
        // destroyed on Back/Launch - see OpenStoryCampaign), which matters because
        // GetNextStageId below is called from HomePagePresenter.HandleMatchCompleted, long after
        // the instance that was open when the stage launched has already been destroyed. No
        // second list/ordering exists anywhere else; do not introduce one.
        /// <summary>Campaign-stage battle-configuration contract: each stage's data-defined enemy
        /// deck, real CardDatabase ids only. Chapter 1 VERTICAL-SLICE curve (2026-08-21 review):
        /// every stage must be clearable by a fresh starter deck under Auto Formation (the taught
        /// path) - not only under a full manual dump. Stages stay distinct by composition; count
        /// and power escalate 1-1 → 1-2 → 1-3 without jumping to a full 10-card elite wall that
        /// Auto Formation cannot beat. No invented card ids.</summary>
        private static readonly string[] Stage1EnemyDeck =
        {
            // Beginner: three rarity-1 grunts (Auto Formation clear path).
            "giant_worms", "mountain_harpy", "snake_archer",
        };

        private static readonly string[] Stage2EnemyDeck =
        {
            // Intermediate - three low/mid cards, strictly stronger than 1-1's rarity-1 grunts
            // but still Auto-Formation-clearable with the starter squad (owner could not clear
            // the prior ten-card mid/high wall under the taught AF path).
            "fire_worm", "butcher", "cursed_soldier",
        };

        private static readonly string[] Stage3EnemyDeck =
        {
            // Citadel closer - three mid cards (harder than 1-2's ATK3 pack, still AF-clearable).
            "ogre", "werewolf", "wood_wizard",
        };

        /// <summary>Chapter 2 "Ashes of Boiotia" (2026-08-22) - same vertical-slice curve as
        /// Chapter 1: three real CardDatabase ids per stage (never the "dragon" placeholder, never
        /// an invented id), Auto-Formation-clearable, count/power escalating 2-1 -> 2-2 -> 2-3
        /// without jumping to a full ten-card elite wall. Disjoint from Chapter 1's own three
        /// rosters and from the approved starter collection - distinct encounters, not a reused
        /// wall under a new name.</summary>
        private static readonly string[] Stage2_1EnemyDeck =
        {
            // Ashfall Outpost - weakest of the three, roughly Stage 1-3's own power level.
            "zombified_captain", "eastern_sorcerer", "corrupted_warrior",
        };

        private static readonly string[] Stage2_2EnemyDeck =
        {
            // Titan-Vein Camp - strictly stronger than 2-1, still AF-clearable with the starter squad.
            "undead_pirate", "goblin_shaman", "elf_wanderer",
        };

        private static readonly string[] Stage2_3EnemyDeck =
        {
            // Legion of Ash - Act II closer, harder than 2-2's mix, still AF-clearable. An
            // earlier all-4/4-rarity3 roster (owl_keeper/goblin_witch/succubus) measured as a
            // real DEFEAT under the deterministic AF policy (Chapter2CampaignContentTests) -
            // swapped one 4/4 for two 3/3s to bring total enemy power back down while staying at
            // or above 2-2's own total (measured, not guessed).
            "persian_princess", "conquistador", "owl_keeper",
        };

        private static readonly List<CampaignStageData> chapterStages = new List<CampaignStageData>()
        {
            new CampaignStageData("1-1", "Outer Border Guard", "Orc Scout Patrol", "UI/Portraits/Paladin", "A small scouting party blocks the mountain path. Defeat them to open the route.", 200, 20, enemyDeckCardIds: Stage1EnemyDeck),
            new CampaignStageData("1-2", "Volcanic Ridge", "Wyvern Tamer Kaelen", "UI/Portraits/Paladin", "Kaelen commands the high ground with his trained drakes. Break his vanguard!", 350, 50, enemyDeckCardIds: Stage2EnemyDeck),
            new CampaignStageData("1-3", "Stronghold Citadel", "High Warlord Gorn", "UI/Portraits/Paladin", "The citadel commander awaits inside the obsidian gates. Defeat him to liberate Chapter 1.", 500, 100, enemyDeckCardIds: Stage3EnemyDeck),
            new CampaignStageData("2-1", "Ashfall Outpost", "Ash Road Overseer", "UI/Portraits/Paladin", "The Titan-vein miners' outer camp burns day and night. Break through the ash-choked sentries.", 650, 130, enemyDeckCardIds: Stage2_1EnemyDeck),
            new CampaignStageData("2-2", "Titan-Vein Camp", "Vein-Warden Thessos", "UI/Portraits/Paladin", "Forced labor gangs mine the Titan-vein under the divine legion's watch. Free the camp and seize the vein.", 800, 160, enemyDeckCardIds: Stage2_2EnemyDeck),
            new CampaignStageData("2-3", "Legion of Ash", "Legion Commander Ares", "UI/Portraits/Paladin", "Olympus has sent its Ashfall Legion to bury Boiotia's rebellion for good. End their march here.", 1000, 200, enemyDeckCardIds: Stage2_3EnemyDeck),
        };

        /// <summary>The stage immediately after <paramref name="currentStageId"/> in the existing
        /// ordered campaign list - the sole order authority (see chapterStages' own comment).
        /// Returns null if the id is unknown or is already the last stage (nothing further to
        /// unlock).</summary>
        public static string GetNextStageId(string currentStageId)
        {
            int index = chapterStages.FindIndex(stage => stage.stageId == currentStageId);
            if (index < 0 || index + 1 >= chapterStages.Count) return null;
            return chapterStages[index + 1].stageId;
        }

        /// <summary>Exposed for tests: the real, configured CampaignStageData for a given id from
        /// the sole order authority (chapterStages) - so a test can drive HandleMatchCompleted
        /// with the actual reward values production uses, instead of a hand-typed stand-in that
        /// could silently drift out of sync with them.</summary>
        public static CampaignStageData GetStageForTests(string stageId) =>
            chapterStages.Find(stage => stage.stageId == stageId);

        public void Initialize(System.Action onBackToHome, System.Func<CampaignStageData, CampaignLaunchOutcome> onLaunchBattle)
        {
            this.onBackToHomeAction = onBackToHome;
            this.onLaunchBattleAction = onLaunchBattle;
            RefreshStageUnlockStatus();
            BuildCampaignMapUI();
        }

        /// <summary>Exposed for tests: whether the given stage shows as unlocked right now -
        /// re-derives fresh from the current profile via RefreshStageUnlockStatus, the exact
        /// same real check Initialize() runs every time Story opens (Chapter 1 progression
        /// contract #9: reopening Story must visibly retain the resulting stage state), without
        /// requiring the full visual UI tree BuildCampaignMapUI() would construct.</summary>
        public bool IsStageUnlockedForTests(string stageId)
        {
            RefreshStageUnlockStatus();
            return chapterStages.Find(stage => stage.stageId == stageId)?.isUnlocked ?? false;
        }

        /// <summary>Exposed for tests: EditMode cannot click a real stage-node Button to open the
        /// detail modal - calls the private OpenStageDetails() directly, the same handler a real
        /// node tap uses.</summary>
        public void OpenStageDetailsForTests(CampaignStageData stage) => OpenStageDetails(stage);

        /// <summary>Exposed for tests: the persistent status surface's current text (Campaign
        /// launch feedback contract) - null if the map hasn't been built yet. statusText itself
        /// stays private.</summary>
        public string StatusTextForTests => statusText != null ? statusText.text : null;

        /// <summary>Exposed for tests: whether a stage detail modal is currently open -
        /// detailModalObj itself stays private.</summary>
        public bool IsDetailModalOpenForTests => detailModalObj != null;

        /// <summary>Destroy is not legal outside Play Mode (this project's own non-negotiable
        /// rule - DestroyImmediate(), not Destroy(), for anything reachable from Initialize();
        /// EditMode tests that click through the real Launch Battle button reach this directly).
        /// Production (Play Mode) behavior and timing are unchanged - Destroy still runs there.</summary>
        private static void SafeDestroy(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        /// <summary>Exposed for tests: EditMode has no way to click a real UI Button, so this
        /// invokes the real stage-node Button's own onClick (requirement 1: selecting a Campaign
        /// stage opens its existing detail modal) - the same handler (OpenStageDetails) a real
        /// tap on the node runs. Returns false only if the map hasn't been built yet or the node
        /// doesn't exist for the given stage id.</summary>
        public bool ClickStageNodeForTests(string stageId)
        {
            if (mapCanvasObj == null) return false;
            Transform nodeTransform = mapCanvasObj.transform.Find($"StageNodesContainer/StageNode_{stageId}");
            Button nodeBtn = nodeTransform != null ? nodeTransform.GetComponent<Button>() : null;
            if (nodeBtn == null) return false;
            nodeBtn.onClick.Invoke();
            return true;
        }

        /// <summary>Exposed for tests: EditMode has no way to click a real UI Button, so this
        /// invokes the real Launch Battle button's own onClick - the same handler
        /// (AttemptLaunch, via the story-sequence branch a stage with no configured "_pre"
        /// sequence skips) a real tap runs. Returns false only if no detail modal is currently
        /// open (nothing to click).</summary>
        public bool ClickLaunchButtonForTests()
        {
            if (detailModalObj == null) return false;
            Transform launchBtnTransform = detailModalObj.transform.Find("DetailPanel/Btn_Launch");
            Button launchBtn = launchBtnTransform != null ? launchBtnTransform.GetComponent<Button>() : null;
            if (launchBtn == null) return false;
            launchBtn.onClick.Invoke();
            return true;
        }

        private void RefreshStageUnlockStatus()
        {
            PlayerProfile profile = SaveSystem.CurrentProfile;
            List<string> unlockedIds = profile?.unlockedStageIds;

            foreach (var stage in chapterStages)
            {
                stage.isUnlocked = unlockedIds != null && unlockedIds.Contains(stage.stageId);
            }
        }

        /// <summary>Campaign launch feedback contract, requirement 1: restores the persistent
        /// "Stamina: current/max - Stage entry: 1" line - CurrencyManager.GetStamina is the sole
        /// stamina authority (Campaign stamina-entry contract, requirement 9), read-only here.
        /// Also the reset point after a blocked-launch message has been shown (see
        /// OpenStageDetails), so opening a fresh stage's details always starts from live state,
        /// not a stale block message from a previous attempt.</summary>
        private void RefreshPersistentStatusText()
        {
            if (statusText == null) return;
            PlayerProfile profile = SaveSystem.CurrentProfile;
            int current = CurrencyManager.GetStamina(profile);
            int max = profile?.maxStamina ?? 0;
            statusText.text = $"Stamina: {current}/{max} • Stage entry: 1";
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
            bgImg.raycastTarget = false; // Campaign input contract, requirement 5: decorative backdrop must never intercept clicks.

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
            topBarBg.raycastTarget = false; // decorative header background - the Back button below owns its own click target.

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
            backImg.raycastTarget = true;

            Button backBtn = backBtnObj.AddComponent<Button>();
            backBtn.targetGraphic = backImg; // Campaign input contract, requirement 4: root Image + Button + assigned targetGraphic.
            backBtn.onClick.AddListener(() =>
            {
                SafeDestroy(mapCanvasObj);
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

            // Campaign launch feedback contract, requirement 1: a persistent status surface
            // (Stamina: current/max + the per-attempt entry cost), reused for requirement 2's
            // blocked-launch messages so there is exactly one status surface on this screen, not
            // a new one per concern. Placed below the title inside the existing header bar - no
            // new panel, no layout change to the Back button/title/stage nodes below.
            GameObject statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(topBar.transform, false);
            statusText = statusObj.AddComponent<Text>();
            statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusText.fontSize = 20;
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.color = new Color(0.9f, 0.82f, 0.64f);
            statusText.supportRichText = true;
            statusText.raycastTarget = false; // informational only - must never intercept clicks.
            RectTransform statusRect = statusObj.GetComponent<RectTransform>();
            statusRect.anchoredPosition = new Vector2(0, -35);
            statusRect.sizeDelta = new Vector2(900, 40);
            RefreshPersistentStatusText();

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
            nodeImg.raycastTarget = true;

            Button btn = nodeObj.AddComponent<Button>();
            btn.targetGraphic = nodeImg; // Campaign input contract, requirement 4: root Image + Button + assigned targetGraphic.
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

        /// <summary>Campaign launch feedback contract: invokes the real launch gate
        /// (HomePagePresenter.TryLaunchCampaignStage, via onLaunchBattleAction) and renders
        /// whatever it actually did. Launched/BlockedNoDeck both mean the caller has already torn
        /// this Campaign map down itself (a successful launch, or the deck-invalid redirect to
        /// Deck Builder) - nothing left to render here. Every other outcome means the launch
        /// never happened and this map is still the live screen: close just the detail modal and
        /// show the one exact required reason on the persistent status surface (requirement 2) -
        /// never Debug.Log (requirement 6), never leaving the player without an explanation
        /// (requirement 3).</summary>
        private void AttemptLaunch(CampaignStageData stage)
        {
            CampaignLaunchOutcome outcome = onLaunchBattleAction != null
                ? onLaunchBattleAction.Invoke(stage)
                : CampaignLaunchOutcome.BlockedInvalidConfig;

            if (outcome == CampaignLaunchOutcome.Launched || outcome == CampaignLaunchOutcome.BlockedNoDeck)
            {
                return;
            }

            if (detailModalObj != null)
            {
                SafeDestroy(detailModalObj);
                detailModalObj = null;
            }

            if (statusText == null) return;
            statusText.text = outcome switch
            {
                CampaignLaunchOutcome.BlockedLocked => HomePagePresenter.LockedBlockedMessage,
                CampaignLaunchOutcome.BlockedInsufficientStamina => HomePagePresenter.StaminaBlockedMessage,
                CampaignLaunchOutcome.BlockedInvalidConfig => HomePagePresenter.InvalidConfigBlockedMessage,
                _ => statusText.text,
            };
        }

        private void OpenStageDetails(CampaignStageData stage)
        {
            RefreshPersistentStatusText();
            if (detailModalObj != null) SafeDestroy(detailModalObj);

            detailModalObj = new GameObject("StageDetailModal");
            detailModalObj.transform.SetParent(mapCanvasObj.transform, false);

            // Dark Backdrop Dimmer
            Image dimImg = detailModalObj.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.75f);
            dimImg.raycastTarget = false; // Campaign input contract, requirement 5: modal backdrop must never intercept clicks.

            RectTransform dimRect = detailModalObj.GetComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.sizeDelta = Vector2.zero;

            // Panel Window
            GameObject panelObj = new GameObject("DetailPanel");
            panelObj.transform.SetParent(detailModalObj.transform, false);
            Image panelImg = panelObj.AddComponent<Image>();
            panelImg.color = new Color(0.12f, 0.14f, 0.2f);
            panelImg.raycastTarget = false; // Campaign input contract, requirement 5: decorative panel background must never intercept clicks - Launch/Close own their own click targets.

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
            launchImg.raycastTarget = true;

            Button launchBtn = launchBtnObj.AddComponent<Button>();
            launchBtn.targetGraphic = launchImg; // Campaign input contract, requirement 4: root Image + Button + assigned targetGraphic.
            launchBtn.onClick.AddListener(() =>
            {
                // Campaign launch feedback contract, requirement 3: the map is no longer
                // destroyed unconditionally up front - a blocked attempt must leave the player on
                // a working Campaign map with a visible reason, not a torn-down screen. Whether
                // (and how) this screen gets torn down now depends entirely on AttemptLaunch's
                // real outcome.
                string storyKey = $"{stage.stageId}_pre";
                StorySequence seq = StoryDatabase.GetSequence(storyKey);

                if (seq != null)
                {
                    StoryOverlayPresenter.PlaySequence(seq, () => AttemptLaunch(stage));
                }
                else
                {
                    AttemptLaunch(stage);
                }
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
            closeImg.raycastTarget = true;

            Button closeBtn = closeBtnObj.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg; // Campaign input contract, requirement 4: root Image + Button + assigned targetGraphic.
            closeBtn.onClick.AddListener(() => SafeDestroy(detailModalObj));

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
            // Campaign input contract, requirement 4/5: every call site of this helper is a
            // decorative label (button caption, title, description) living inside an actionable
            // root that already owns its own raycastable Image - a raycastable child Text would
            // needlessly widen the hit-test surface and is never itself a click target.
            txt.raycastTarget = false;

            RectTransform rect = textObj.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(700, 100);
        }
    }
}
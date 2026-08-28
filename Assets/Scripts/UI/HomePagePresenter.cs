using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MyriadOfDragons.UI;
using MyriadOfDragons.Data;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Metagame;
using MyriadOfDragons.Save;
using MyriadOfDragons.Story;
using MyriadOfDragons.Empire;

public class HomePagePresenter : MonoBehaviour
{
    /// <summary>HomeV3 feature-panel Soft invite — must point new players at Start Tutorial.</summary>
    public const string HomeFeatureTutorialInviteCopy =
        "New to the Empire? Tap Start Tutorial to learn formation and face your first threat.";

    private GameObject homeCanvasObj;
    private BattleController _battleController;
    private GameBootstrap _boundGameBootstrap;
    private CampaignMapPresenter _storyCampaign;

    // HUD Text References
    private Text goldHudText;
    private Text gemsHudText;
    private Text energyHudText;
    private Text avatarIdentityText;

    // Current Active Stage Track
    private CampaignStageData currentActiveStage;

    private RetentionTelemetryOutbox _telemetryOutbox;

    /// <summary>Exposed for tests: inject a fake-gateway outbox so Home feature_entry / Campaign
    /// win-loss emits are observable without Unity Cloud Code.</summary>
    public RetentionTelemetryOutbox TelemetryOutboxForTests => _telemetryOutbox;

    public void BindTelemetryOutboxForTests(RetentionTelemetryOutbox outbox) =>
        _telemetryOutbox = outbox;

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

    /// <summary>Exposed for tests: drives the real HandleMatchCompleted reward/telemetry path
    /// without a full BattleController resolution (same pattern as SetActiveStageForTests).</summary>
    public void NotifyMatchCompletedForTests(MatchResult result) => HandleMatchCompleted(result);

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
        CampaignMapPresenter.CleanupStaleMetagameCanvases();
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

    /// <summary>Chapter finale stage ids that grant one Ascension Permit on first clear (milestone, not weekly).</summary>
    public static readonly string[] ChapterFinalePermitStageIds =
    {
        "1-3", "2-21", "3-30", "4-30", "5-30", "6-30", "7-30", "8-30", "9-30", "10-30", "11-30", "12-30", "13-30", "14-30", "15-30", "16-30", "17-30", "18-30",
    };

    /// <summary>Chapter 1 finale — kept for existing call sites / tests.</summary>
    public const string FirstCampaignChapterClearStageId = "1-3";

    public static bool IsChapterFinalePermitStage(string stageId)
    {
        if (string.IsNullOrEmpty(stageId)) return false;
        for (int i = 0; i < ChapterFinalePermitStageIds.Length; i++)
        {
            if (ChapterFinalePermitStageIds[i] == stageId) return true;
        }

        return false;
    }

    /// <summary>
    /// Milestone permit on first clear of a chapter finale. Hoard-capped; never touches weekly ledger.
    /// When hoard is full, returns 0 with no queue (CC-locked silent loss — player still clears the stage).
    /// </summary>
    public static int TryGrantChapterFinalePermit(PlayerProfile profile, string stageId)
    {
        if (profile == null || !IsChapterFinalePermitStage(stageId)) return 0;
        return CollectionAscensionPermits.TryGrantMilestone(profile, 1);
    }

    /// <summary>Alias for <see cref="TryGrantChapterFinalePermit"/> (Ch1 1-3 and later finales).</summary>
    public static int TryGrantFirstChapterClearPermit(PlayerProfile profile, string stageId) =>
        TryGrantChapterFinalePermit(profile, stageId);

    /// <summary>
    /// Option C stopgap: claim this CC week’s Ascension Permits via ManualTrustedWeekKey.
    /// Idempotent for the same week key. Does not use device clock or DevTrustedWeekKeyPlaceholder.
    /// </summary>
    /// <returns>Permits granted (0 if already claimed / hoard full).</returns>
    public static int TryClaimManualWeeklyPermits(PlayerProfile profile, out string statusMessage)
    {
        statusMessage = "No profile.";
        if (profile == null) return 0;

        CollectionSchemaMigration.NormalizeCollectionFields(profile);

        int granted = CollectionAscensionPermits.TryGrantWeekly(
            profile,
            CollectionSchemaRules.AscensionPermitsPerTrustedWeek,
            CollectionAscensionPermits.ManualTrustedWeekKey);

        if (granted > 0)
        {
            statusMessage =
                $"Granted {granted} Ascension Permit(s) this week " +
                $"(weekly rate {CollectionSchemaRules.AscensionPermitsPerTrustedWeek}). " +
                $"Balance {profile.ascensionPermitBalance}/{CollectionSchemaRules.AscensionPermitHoardCap}.";
            return granted;
        }

        if (profile.ascensionPermitBalance >= CollectionSchemaRules.AscensionPermitHoardCap)
        {
            statusMessage =
                $"Hoard full ({profile.ascensionPermitBalance}/{CollectionSchemaRules.AscensionPermitHoardCap}).";
            return 0;
        }

        statusMessage =
            $"Already claimed this week. Balance {profile.ascensionPermitBalance}/{CollectionSchemaRules.AscensionPermitHoardCap} " +
            $"(weekly rate {CollectionSchemaRules.AscensionPermitsPerTrustedWeek}).";
        return 0;
    }

    /// <summary>Exposed for EditMode: runs the Home weekly-claim helper and optional save.</summary>
    public static int ClaimManualWeeklyPermitsForTests(PlayerProfile profile, bool save, out string statusMessage)
    {
        int granted = TryClaimManualWeeklyPermits(profile, out statusMessage);
        if (granted > 0 && save)
            SaveManager.Save();
        return granted;
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
    /// currentActiveStage == null (a battle entered without going through Story at all) grants
    /// no economy reward (0 Gold / 0 Gems). This keeps Campaign as the sole current Gold/Gems
    /// faucet (first clear only) while preserving the existing non-economy progression flow for
    /// ordinary normal matches (e.g. totalMatches/totalWins via GameBootstrap).
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

        PlayerProfile profile = SaveManager.SaveData;

        // Retention telemetry: Campaign win/loss (and first-clear reward claim) — emit before the
        // defeat early-return so losses are visible; normal (non-campaign) matches skip.
        if (currentActiveStage != null)
        {
            bool firstClear = result.IsVictory
                && profile != null
                && profile.claimedStageRewardIds != null
                && !profile.claimedStageRewardIds.Contains(currentActiveStage.stageId);
            EnsureTelemetryOutbox();
            CampaignMapPresenter.EmitCampaignMatchTelemetry(
                _telemetryOutbox, currentActiveStage.stageId, result.IsVictory, firstClear);
        }

        // Stage defeat grants and unlocks nothing (Chapter 1 progression contract #3) - already
        // true by construction below (only the IsVictory branch ever mutates the profile), made
        // explicit here so a reader does not have to infer it from an absent else-branch.
        if (!result.IsVictory) return;

        if (profile == null) return;

        if (currentActiveStage == null)
        {
            // No active campaign stage - this path intentionally mints no economy rewards.
            SaveManager.Save();
            RefreshTopHUD();
            Debug.Log("[Metagame] No economy reward for Victory (no active campaign stage).");
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
                // Stage-gated spells key off unlockedStageIds - a repair that just added the
                // next stage must grant newly-eligible ownership the same way a first-clear does.
                SpellOwnershipSync.SynchronizeEligibleSpellOwnership(profile);
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

        int permitsGranted = ApplyFirstClearRewards(profile, currentActiveStage);

        RefreshTopHUD();
        Debug.Log($"[Metagame] Awarded {currentActiveStage.goldReward} Gold & {currentActiveStage.gemReward} Gems " +
                  $"for first clear of Stage {stageId}!" +
                  (permitsGranted > 0 ? $" Granted {permitsGranted} Ascension Permit." : string.Empty) +
                  (nextStageId != null ? $" Unlocked Stage {nextStageId}." : " Chapter 1 complete."));

        // Chapter 1 post-victory story bridge: first clear only (replays already returned above).
        // Rewards/unlocks are already persisted - story is presentation on top of that contract,
        // never a gate. Missing sequence is a silent no-op so stages without copy still clear.
        TryPlayCampaignPostVictoryStory(stageId);
    }

    /// <summary>The real first-clear reward mutation sequence - extracted from
    /// HandleMatchCompleted (CC 2026-08-27, after SpellBookProductionReachabilityTests.cs caught
    /// a hand-mirrored test copy silently drifting from production and staying green while the
    /// real call site was dead) so this is the ONE place either production or a test can drive it
    /// from. HandleMatchCompleted cannot be called directly from EditMode (private, needs live
    /// UI - see its own MonoBehaviour-only concerns), but this method has none of that: plain
    /// PlayerProfile mutation, no scene/canvas dependency, callable and assertable directly. A
    /// test asserting against this method is asserting against what production actually runs, not
    /// a copy of it - CLAUDE.md's "real logic in plain testable methods" rule applied to reward
    /// grants, not just battle math. Returns permits granted, for the caller's own log line.</summary>
    public static int ApplyFirstClearRewards(PlayerProfile profile, CampaignStageData stage)
    {
        string stageId = stage.stageId;
        profile.gold += stage.goldReward;
        profile.gems += stage.gemReward;
        profile.claimedStageRewardIds.Add(stageId);
        int permitsGranted = TryGrantChapterFinalePermit(profile, stageId);

        // Idempotent: the cleared stage must already have been unlocked to have been launchable,
        // but this keeps unlockedStageIds authoritative even for a hand-edited or pre-migration
        // save that reached this point without it.
        if (!profile.unlockedStageIds.Contains(stageId))
        {
            profile.unlockedStageIds.Add(stageId);
        }

        // Spell-Book Acquisition + Ownership Sync: stage first-clear is a required call site
        // (same service as Avatar level-up). Must run after unlockedStageIds mutations above
        // (this stage + next) so stage-gated spells (e.g. Cinder Lash at 1-2) land in
        // ownedSpellIds immediately - RecordMatchResult's level-up sync may already have
        // fired with the pre-unlock stage list.
        SpellOwnershipSync.SynchronizeEligibleSpellOwnership(profile);

        // Chapter-finale spell book. Must run AFTER the unlockedStageIds mutations above and
        // BEFORE SaveManager.Save() below, so the grant lands in the same persisted write as the
        // rest of the first-clear reward. persist:false because the Save() below covers it -
        // passing true would write the profile twice on every finale clear.
        SpellBookGrant.TryGrant(profile, stageId, persist: false);

        // Saved immediately after the first-clear reward/unlock mutation (Chapter 1 progression
        // contract #7) - not deferred to OnDestroy, OpenDeckBuilder's own save-on-close, or any
        // later action, so the result survives even if the player quits before doing anything
        // else.
        SaveManager.Save();
        return permitsGranted;
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

        // Semantic regions — authoritative parents for NEW Home geometry (LOCKED 2026-08-25).
        HomeSemanticRegions.EnsureAll(homeCanvasObj.transform);
        Transform topHud = HomeRegion(HomeSemanticRegions.TopHud);
        Transform contentPanel = HomeRegion(HomeSemanticRegions.ContentPanel);

        // === IDENTITY SURFACE (text-only — identity frame needs slice metadata; crest RGB excluded) ===
        GameObject identityRoot = new GameObject("IdentityRoot", typeof(RectTransform), typeof(Image));
        identityRoot.transform.SetParent(topHud, false);
        SetScreenRectFromTopLeftPixels(identityRoot.GetComponent<RectTransform>(), 24, 18, 704, 100);
        Image identityBg = identityRoot.GetComponent<Image>();
        // Under 128px — token fill only (no ApplyFramedPanel), same hold as MemoryExpedition.
        Color panel = UIFrozenTokens.ColorPanel;
        identityBg.sprite = null;
        identityBg.color = new Color(panel.r, panel.g, panel.b, 0.72f);
        identityBg.raycastTarget = true;

        Button identityButton = identityRoot.AddComponent<Button>();
        identityButton.targetGraphic = identityBg;
        // None, not ColorTint (CR, 2026-08-27, interaction-states pass 1/pressed-only): Button's
        // own ColorTint transition silently overwrites any color InteractionStateController paints
        // (documented conflict, see CreateButton's own identical note) - the controller is now the
        // single source of truth for this button's visual feedback.
        identityButton.transition = Selectable.Transition.None;
        identityButton.onClick.AddListener(() => OpenAvatar());
        identityRoot.AddComponent<InteractionStateController>().Tier = UIDesignTokens.FrameTier.Tier3Utility;

        // HUD placeholders before Save is ready — must mirror PlayerProfile field defaults (not re-typed).
        PlayerProfile hudDefaults = new PlayerProfile();
        string pName = SaveManager.SaveData != null ? SaveManager.SaveData.playerName : hudDefaults.playerName;
        // Avatar combat level (Empire track) — not the legacy `level` field.
        int avatarLevel = 1;
        int liveCap = 0;
        int liveStartHp = 0;
        if (SaveManager.SaveData != null)
        {
            PlayerProfile profile = SaveManager.SaveData;
            profile.ApplyDataToEmpire();
            avatarLevel = Mathf.Max(1, profile.avatarLevel);
            liveCap = profile.Empire.ResourceCap;
            liveStartHp = profile.Empire.StartingAvatarHealth;
        }

        // Player name text (bold, large)
        Text playerNameText = UISharedFoundation.CreateText(
            identityRoot.transform, "PlayerName", pName.ToUpperInvariant(),
            MyriadOfDragons.UI.UITextRole.Display, TextAnchor.MiddleLeft, HexColor("#F2E5C9"), true, new Vector2(300f, 40f));
        playerNameText.fontSize = 30;
        playerNameText.fontStyle = FontStyle.Bold;
        playerNameText.raycastTarget = false;
        SetLocalNormalisedRect(playerNameText.rectTransform, 0.06f, 0.55f, 0.95f, 1.0f);

        // Avatar identity line — level + live battle economy from Empire readers.
        avatarIdentityText = UISharedFoundation.CreateText(
            identityRoot.transform, "PlayerLevelRole",
            $"Avatar L{avatarLevel} · Cap {liveCap} · Start HP {liveStartHp}",
            MyriadOfDragons.UI.UITextRole.Body, TextAnchor.MiddleLeft, HexColor("#B8A68F"), true, new Vector2(420f, 20f));
        // Type-scale hard floor (register: "22px absolute minimum for any player-facing text").
        UIDesignTokens.Apply(avatarIdentityText, UIDesignTokens.TypeTier.T1Micro);
        avatarIdentityText.raycastTarget = false;
        SetLocalNormalisedRect(avatarIdentityText.rectTransform, 0.06f, 0.0f, 0.95f, 0.45f);

        GameObject resourceRow = new GameObject("ResourceRow", typeof(RectTransform));
        resourceRow.transform.SetParent(topHud, false);
        SetScreenRectFromTopLeftPixels(resourceRow.GetComponent<RectTransform>(), 900, 18, 1328, 90);

        int goldVal = SaveManager.SaveData != null ? SaveManager.SaveData.gold : hudDefaults.gold;
        int gemsVal = SaveManager.SaveData != null ? SaveManager.SaveData.gems : hudDefaults.gems;
        int stamVal = SaveManager.SaveData != null ? SaveManager.SaveData.stamina : hudDefaults.stamina;
        int maxStamVal = SaveManager.SaveData != null ? SaveManager.SaveData.maxStamina : hudDefaults.maxStamina;

        goldHudText = CreateResourcePill(resourceRow.transform, "home_resource_gold_pill_v3",
            "Gold", $"{goldVal}", 0.0f, 0.33f);
        gemsHudText = CreateResourcePill(resourceRow.transform, "home_resource_gems_pill_v3",
            "Gems", $"{gemsVal}", 0.34f, 0.67f);
        energyHudText = CreateResourcePill(resourceRow.transform, "home_resource_energy_pill_v3",
            "Stamina", $"{stamVal}/{maxStamVal}", 0.68f, 1.0f);

        BuildSettingsEntryButton(topHud);
        BuildSocialDrawerEntryButton(topHud);

        if (SaveManager.SaveData != null)
            PlayerSettingsService.ApplyFromProfile(SaveManager.SaveData);

        // LOCKED 2026-08-26 (register: "LOCKED: Home IA rebuild - five-destination shell +
        // rotating feed") - replaces the old static 6-tile grid + scattered SPELLS/PASS/LOGIN/
        // BAZAAR/CHAT/MAIL/FRIENDS/MEMORY/VIP header row with: a swipeable feed (3-5 cards, one
        // dominant primary action per page) and a persistent 5-destination bottom bar. Avatar
        // tile is CUT per the locked verdict (identity header above already owns that need).
        BuildFeed(contentPanel);
        BuildDestinationBar(contentPanel);

        // Must run AFTER BuildFeed - the WEEKLY PERMIT card's status text field is created there.
        TryAutoClaimWeeklyPermitsOnHomeOpen();
    }

    private Transform HomeRegion(string regionName) =>
        HomeSemanticRegions.Ensure(homeCanvasObj.transform, regionName);

    /// <summary>Silent auto-claim on Home open - the merged Permit entry (OpenPermitClaimEntry,
    /// inside the WEEKLY PERMIT feed card - moved off the old top-header WeeklyPermitStrip, real
    /// functionality preserved, not resurrected UI.</summary>
    private void TryAutoClaimWeeklyPermitsOnHomeOpen()
    {
        PlayerProfile profile = SaveManager.SaveData;
        if (profile == null) return;

        int granted = TryClaimManualWeeklyPermits(profile, out string status);
        if (granted > 0)
            SaveManager.Save();
        if (_weeklyPermitStatusText != null)
            _weeklyPermitStatusText.text = status;
    }

    private void OnClaimWeeklyPermitsClicked()
    {
        PlayerProfile profile = SaveManager.SaveData;
        if (profile == null) return;

        int granted = TryClaimManualWeeklyPermits(profile, out string status);
        if (granted > 0)
            SaveManager.Save();
        if (_weeklyPermitStatusText != null)
            _weeklyPermitStatusText.text = status;
    }

    /// <summary>One feed card's content - real state read at BuildFeed time, not a template.</summary>
    private readonly struct FeedCard
    {
        public readonly string Title;
        public readonly string Body;
        public readonly string ActionLabel;
        public readonly UnityEngine.Events.UnityAction Action;

        public FeedCard(string title, string body, string actionLabel, UnityEngine.Events.UnityAction action)
        {
            Title = title;
            Body = body;
            ActionLabel = actionLabel;
            Action = action;
        }
    }

    /// <summary>LOCKED Home IA: "Main feed: one swipeable/paginated area, 3-5 cards (Campaign
    /// objective, Circuit/Memory Expedition, Battle Pass/event promo, Empire construction status,
    /// limited-time notice), one dominant primary action per page." A real horizontal ScrollRect,
    /// one full-viewport card per page - each card names its own single dominant action, matching
    /// the locked rule rather than a grid of equal-weight buttons.
    ///
    /// Tutorial banner: no real "days since install" field exists on PlayerProfile (frozen file,
    /// not adding one for this) - profile.totalMatches == 0 is the closest already-existing real
    /// signal for "hasn't played yet" and is used as the gate. This is a genuine proxy, not the
    /// literal "first few days" language - flagged rather than silently treated as equivalent.
    /// Once totalMatches > 0 the tutorial card is gone for good, matching "converts to the Events
    /// feed card" (a returning player who somehow has 0 matches would see the tutorial card again,
    /// which is the correct behavior for that edge case, not a bug).</summary>
    private void BuildFeed(Transform contentParent)
    {
        PlayerProfile profile = SaveManager.SaveData;
        bool isNewPlayer = profile == null || profile.totalMatches == 0;

        // HUD/Content canvas split (CC 4e836a5, 2026-08-27, Home as the reference
        // implementation): the feed is scrollable, masked, centre-weighted CONTENT, not edge-
        // anchored chrome - it gets its own nested Canvas at match=0.5 rather than inheriting the
        // outer canvas's match=1 (which exists to protect TopHud/DestinationBar from vertical
        // clipping, not to govern content). The nested canvas's OWN RectTransform is still
        // positioned in the OUTER (match=1) canvas's coordinate space - only content INSIDE it
        // uses the nested CanvasScaler. scrollObj now stretch-fills the nested canvas instead of
        // repeating the same pixel placement a second time.
        GameObject feedCanvasObj = new GameObject("HomeFeedCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        feedCanvasObj.transform.SetParent(contentParent, false);
        SetScreenRectFromTopLeftPixels(feedCanvasObj.GetComponent<RectTransform>(), 24, 176, 1896, 962);
        Canvas feedCanvas = feedCanvasObj.GetComponent<Canvas>();
        feedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler feedScaler = feedCanvasObj.GetComponent<CanvasScaler>();
        feedScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        feedScaler.referenceResolution = new Vector2(1920, 1080);
        feedScaler.matchWidthOrHeight = 0.5f;

        GameObject scrollObj = new GameObject("HomeFeed", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollObj.transform.SetParent(feedCanvasObj.transform, false);
        UISharedFoundation.StretchFull(scrollObj.GetComponent<RectTransform>());
        Image scrollBg = scrollObj.GetComponent<Image>();
        scrollBg.sprite = null;
        scrollBg.color = Color.clear;
        scrollBg.raycastTarget = true;

        GameObject viewportObj = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
        viewportObj.transform.SetParent(scrollObj.transform, false);
        SetLocalNormalisedRect(viewportObj.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f);
        Image viewportImg = viewportObj.GetComponent<Image>();
        viewportImg.color = Color.clear;
        viewportImg.raycastTarget = false;

        GameObject contentObj = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        contentObj.transform.SetParent(viewportObj.transform, false);
        RectTransform contentRect = contentObj.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 0f);
        contentRect.anchorMax = new Vector2(0f, 1f);
        contentRect.pivot = new Vector2(0f, 0.5f);
        contentRect.anchoredPosition = Vector2.zero;

        var layout = contentObj.GetComponent<HorizontalLayoutGroup>();
        // childControlWidth MUST be true - a card's LayoutElement.preferredWidth/preferredHeight
        // is only ever read/applied by HorizontalLayoutGroup when childControl* is true. With it
        // false (the mistake caught here via a real overlap test), every card silently stayed at
        // Unity's own default 100x100 RectTransform size instead of a real full-viewport page -
        // real bug, not just a stale test: cards were reported at ~33px wide, small enough to
        // overlap the identity header instead of filling the feed.
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.spacing = 0f;

        ScrollRect sr = scrollObj.GetComponent<ScrollRect>();
        sr.viewport = viewportObj.GetComponent<RectTransform>();
        sr.content = contentRect;
        sr.horizontal = true;
        sr.vertical = false;
        sr.movementType = ScrollRect.MovementType.Elastic;

        var cards = new List<FeedCard>();

        if (isNewPlayer)
        {
            cards.Add(new FeedCard("WELCOME", HomeFeatureTutorialInviteCopy, "START TUTORIAL", OnStartTutorialClicked));
        }

        // Must route to the real map (OpenStoryCampaign), not straight into a match
        // (OnToBattleClicked skips CampaignMapPresenter entirely) - register: "'To Battle' folds
        // under Battle, no second world map." A direct-to-match shortcut here would be a second,
        // divergent navigation target alongside the DestinationBar's own BATTLE root.
        cards.Add(new FeedCard("CAMPAIGN",
            "Continue your Campaign push - the next stage is waiting.",
            "TO BATTLE", OpenStoryCampaign));

        cards.Add(new FeedCard("QUESTS & EVENTS",
            "Daily Login, Battle Pass, Memory Expedition, Solo Circuit, and Guild content live here.",
            "OPEN", OpenQuestsEventsHub));

        cards.Add(new FeedCard("EMPIRE",
            profile != null
                ? $"Castle L{profile.castleLevel} · Barracks L{profile.barracksLevel} · Gate L{profile.gateLevel}"
                : "Manage your Empire's construction.",
            "OPEN EMPIRE", OpenEmpire));

        float viewportWidth = 1896f - 24f;
        float viewportHeight = 962f - 176f;
        for (int i = 0; i < cards.Count; i++)
        {
            BuildFeedCard(contentObj.transform, cards[i], viewportWidth, viewportHeight);
        }

        // The merged Permit entry (locked design: "SERVER-KEY and WEEKLY-permit-claim merge into
        // ONE Quests/Events entry with two labeled sub-states... no two permanent Home buttons
        // for what's really one logical feature") gets its own feed card rather than a bare tab,
        // because it's the one entry that needs real, visible status feedback (was
        // WeeklyPermitStrip's WeeklyPermitStatus text on the old Home header row - real
        // functionality, not resurrected UI, just relocated into the feed).
        BuildWeeklyPermitFeedCard(contentObj.transform, viewportWidth, viewportHeight);

        // Content is vertically STRETCHED (anchorMin.y=0, anchorMax.y=1), so sizeDelta.y is an
        // ADDITIVE delta on top of the fully-stretched parent height, not an absolute height -
        // a real bug caught here via direct geometry measurement: setting it to viewportHeight
        // made the row 786px taller than its own viewport (1048 + 786 = 1834, measured), pushing
        // every feed card up into the TopHud row. Only the X axis (unstretched) needs an
        // absolute total scroll width; Y must stay 0 so content matches the viewport's height.
        contentRect.sizeDelta = new Vector2(viewportWidth * (cards.Count + 1), 0f);
    }

    private Text _weeklyPermitStatusText;

    private void BuildWeeklyPermitFeedCard(Transform parent, float width, float height)
    {
        GameObject cardObj = new GameObject("FeedCard_WEEKLY PERMIT", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        cardObj.transform.SetParent(parent, false);
        LayoutElement le = cardObj.GetComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = height;
        // The HorizontalLayoutGroup only resolves this rect on its next layout pass, so at
        // creation time it's still Unity's 100x100 default - set it explicitly to the known
        // target size first so ApplyFramedPanel's 9-slice border fits against the real size,
        // not the stale default (apply-before-position, same pattern documented on
        // UISharedFoundation.ApplyFramedPanel). The layout group overwrites this again once it
        // runs; setting it early is free and harmless.
        cardObj.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);

        Image cardBg = cardObj.GetComponent<Image>();
        UISharedFoundation.ApplyFramedPanel(cardBg, null, UIFrozenTokens.ColorPanel, UIFrozenTokens.ColorBackground,
            tier: UIDesignTokens.FrameTier.Tier1Hero);
        cardBg.raycastTarget = false;

        Text title = UISharedFoundation.CreateText(cardObj.transform, "Title", "WEEKLY PERMIT", UITextRole.Display,
            TextAnchor.UpperLeft, HexColor("#F2E5C9"), true, new Vector2(900f, 50f));
        title.fontSize = 30;
        title.fontStyle = FontStyle.Bold;
        title.raycastTarget = false;
        SetLocalNormalisedRect(title.rectTransform, 0.06f, 0.72f, 0.7f, 0.9f);

        _weeklyPermitStatusText = UISharedFoundation.CreateText(cardObj.transform, "WeeklyPermitStatus", string.Empty,
            UITextRole.Body, TextAnchor.UpperLeft, HexColor("#B8A68F"), true, new Vector2(1400f, 200f));
        // Type-scale hard floor: body/status copy needs >=28px (register: "28px minimum for
        // body copy and interactive labels").
        UIDesignTokens.Apply(_weeklyPermitStatusText, UIDesignTokens.TypeTier.T2Utility);
        _weeklyPermitStatusText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _weeklyPermitStatusText.raycastTarget = false;
        SetLocalNormalisedRect(_weeklyPermitStatusText.rectTransform, 0.06f, 0.28f, 0.94f, 0.7f);
        RefreshWeeklyPermitStatusText();

        GameObject claimBtnObj = new GameObject("ClaimWeeklyPermitsButton", typeof(RectTransform), typeof(Image), typeof(Button));
        claimBtnObj.transform.SetParent(cardObj.transform, false);
        Image claimImg = claimBtnObj.GetComponent<Image>();
        Button claimBtn = claimBtnObj.GetComponent<Button>();
        HomeV3UiLibrary.ApplyPrimaryActionButton(claimBtn, claimImg);
        claimBtn.onClick.AddListener(OnClaimWeeklyPermitsClicked);
        // Additive to the existing SpriteSwap press art (not a replacement) - SpriteSwap only
        // touches .sprite, this controller only touches .color/localScale, so they compose rather
        // than conflict (unlike ColorTint, which fights over the same .color property).
        claimBtnObj.AddComponent<InteractionStateController>().Tier = UIDesignTokens.FrameTier.Tier2Section;
        SetLocalNormalisedRect(claimBtnObj.GetComponent<RectTransform>(), 0.06f, 0.08f, 0.4f, 0.22f);
        Text claimLabel = UISharedFoundation.CreateText(claimBtnObj.transform, "Label", "CLAIM", UITextRole.Body,
            TextAnchor.MiddleCenter, Color.white, true, new Vector2(260f, 40f));
        // Interactive-label hard floor: >=28px (register: same line as body copy).
        UIDesignTokens.Apply(claimLabel, UIDesignTokens.TypeTier.T2Utility);
        claimLabel.fontStyle = FontStyle.Bold;
        claimLabel.raycastTarget = false;

        // Second sub-state: the server-authoritative PermitWeekKey screen (SERVER KEY, demoted
        // per the locked design - no player-facing implementation terminology in the label).
        GameObject serverBtnObj = new GameObject("Btn_PermitWeekKey", typeof(RectTransform), typeof(Image), typeof(Button));
        serverBtnObj.transform.SetParent(cardObj.transform, false);
        Image serverImg = serverBtnObj.GetComponent<Image>();
        Button serverBtn = serverBtnObj.GetComponent<Button>();
        HomeV3UiLibrary.ApplyNeutralActionButton(serverBtn, serverImg, new Color(0.16f, 0.22f, 0.2f, 0.92f));
        serverBtn.onClick.AddListener(OpenPermitWeekKey);
        serverBtnObj.AddComponent<InteractionStateController>().Tier = UIDesignTokens.FrameTier.Tier3Utility;
        SetLocalNormalisedRect(serverBtnObj.GetComponent<RectTransform>(), 0.44f, 0.08f, 0.7f, 0.22f);
        Text serverLabel = UISharedFoundation.CreateText(serverBtnObj.transform, "Label", "OTHER BONUS", UITextRole.Body,
            TextAnchor.MiddleCenter, Color.white, true, new Vector2(200f, 40f));
        UIDesignTokens.Apply(serverLabel, UIDesignTokens.TypeTier.T2Utility);
        serverLabel.fontStyle = FontStyle.Bold;
        serverLabel.raycastTarget = false;
    }

    private void RefreshWeeklyPermitStatusText()
    {
        if (_weeklyPermitStatusText == null) return;
        PlayerProfile profile = SaveManager.SaveData;
        _weeklyPermitStatusText.text = profile != null
            ? $"Balance {profile.ascensionPermitBalance}/{CollectionSchemaRules.AscensionPermitHoardCap}."
            : string.Empty;
    }

    private void BuildFeedCard(Transform parent, FeedCard card, float width, float height)
    {
        GameObject cardObj = new GameObject($"FeedCard_{card.Title}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        cardObj.transform.SetParent(parent, false);
        LayoutElement le = cardObj.GetComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = height;
        // See BuildWeeklyPermitFeedCard's identical comment - sets the pre-layout rect to the
        // known target size so ApplyFramedPanel's border-fit isn't computed against Unity's
        // 100x100 default.
        cardObj.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);

        Image cardBg = cardObj.GetComponent<Image>();
        UISharedFoundation.ApplyFramedPanel(cardBg, null, UIFrozenTokens.ColorPanel, UIFrozenTokens.ColorBackground,
            tier: UIDesignTokens.FrameTier.Tier1Hero);
        cardBg.raycastTarget = false;

        Text title = UISharedFoundation.CreateText(cardObj.transform, "Title", card.Title, UITextRole.Display,
            TextAnchor.UpperLeft, HexColor("#F2E5C9"), true, new Vector2(900f, 50f));
        title.fontSize = 30;
        title.fontStyle = FontStyle.Bold;
        title.raycastTarget = false;
        SetLocalNormalisedRect(title.rectTransform, 0.06f, 0.72f, 0.7f, 0.9f);

        // Local scrim under body copy only (card already framed; muted taupe on art read <2:1).
        // AddSemiTransparentScrimPanel (flat Image.color, no sprite) moved zero measured contrast
        // ratios wherever it was used and was removed 2026-08-27 (CC) - swapped to the proven
        // pattern from CampaignMapPresenter's StatusTextPlate: a solid CreateRoundedPanelSprite
        // fill plus two opposite-direction AddLocalGradientScrim calls stacked on the same rect,
        // whose overlapping alpha reads as a near-flat high-opacity fill.
        {
            Vector2 scrimCenter = new Vector2(width * 0.5f, height * 0.49f);
            Vector2 scrimSize = new Vector2(width * 0.88f, height * 0.40f);
            GameObject scrimPlate = new GameObject("BodyScrimPlate", typeof(RectTransform));
            scrimPlate.transform.SetParent(cardObj.transform, false);
            RectTransform scrimPlateRect = scrimPlate.GetComponent<RectTransform>();
            scrimPlateRect.anchorMin = new Vector2(0f, 0f);
            scrimPlateRect.anchorMax = new Vector2(0f, 0f);
            scrimPlateRect.pivot = new Vector2(0.5f, 0.5f);
            scrimPlateRect.sizeDelta = scrimSize;
            scrimPlateRect.anchoredPosition = scrimCenter;
            Image scrimFill = scrimPlate.AddComponent<Image>();
            scrimFill.sprite = UISharedFoundation.CreateRoundedPanelSprite(
                new Color(0.03f, 0.035f, 0.05f, 0.60f),
                new Color(0.03f, 0.035f, 0.05f, 0.60f), cornerRadius: 1);
            scrimFill.type = Image.Type.Simple;
            scrimFill.color = Color.white;
            scrimFill.raycastTarget = false;
            UISharedFoundation.AddLocalGradientScrim(
                scrimPlate.transform, scrimSize * 0.5f, scrimSize,
                UISharedFoundation.GradientDirection.TopToBottom, 0.60f);
            UISharedFoundation.AddLocalGradientScrim(
                scrimPlate.transform, scrimSize * 0.5f, scrimSize,
                UISharedFoundation.GradientDirection.BottomToTop, 0.60f);
            // Behind title too, matching the removed helper's GetOrCreateScrimContainer
            // first-sibling guarantee - title is created earlier in this method and would
            // otherwise end up behind this plate purely by insertion order.
            scrimPlate.transform.SetAsFirstSibling();
        }

        Text body = UISharedFoundation.CreateText(cardObj.transform, "Body", card.Body, UITextRole.Body,
            TextAnchor.UpperLeft, HexColor("#F2E5C9"), true, new Vector2(1400f, 200f));
        // Type-scale hard floor: body copy >=28px. T3Body (35px) matches its exact documented
        // use ("descriptions, instructional copy") in the locked type table.
        UIDesignTokens.Apply(body, UIDesignTokens.TypeTier.T3Body);
        body.horizontalOverflow = HorizontalWrapMode.Wrap;
        body.raycastTarget = false;
        UISharedFoundation.ApplyTextShadow(body);
        SetLocalNormalisedRect(body.rectTransform, 0.06f, 0.28f, 0.94f, 0.7f);

        // One dominant primary action per page (locked rule) - primary chrome, not neutral.
        GameObject actionBtn = new GameObject("PrimaryAction", typeof(RectTransform), typeof(Image), typeof(Button));
        actionBtn.transform.SetParent(cardObj.transform, false);
        Image actionImg = actionBtn.GetComponent<Image>();
        Button actionButton = actionBtn.GetComponent<Button>();
        HomeV3UiLibrary.ApplyPrimaryActionButton(actionButton, actionImg);
        actionButton.onClick.AddListener(card.Action);
        // Tier1Hero: the comment above and the locked rule both say this is the screen's one
        // primary CTA - gets the strong end of the press feedback (0.96 scale, 80ms).
        actionBtn.AddComponent<InteractionStateController>().Tier = UIDesignTokens.FrameTier.Tier1Hero;
        SetLocalNormalisedRect(actionBtn.GetComponent<RectTransform>(), 0.06f, 0.08f, 0.4f, 0.22f);

        Text actionLabel = UISharedFoundation.CreateText(actionBtn.transform, "Label", card.ActionLabel, UITextRole.Body,
            TextAnchor.MiddleCenter, Color.white, true, new Vector2(260f, 40f));
        // Interactive-label hard floor: >=28px - this is the screen's one primary CTA.
        UIDesignTokens.Apply(actionLabel, UIDesignTokens.TypeTier.T2Utility);
        actionLabel.fontStyle = FontStyle.Bold;
        actionLabel.raycastTarget = false;
    }

    /// <summary>LOCKED Home IA: persistent five-destination bottom bar - Home/My Page, Battle,
    /// Quests/Events, Collection, Empire. Home itself is this screen (a tap just ensures the
    /// feed is showing); the other four route through the real existing entry points
    /// (OpenStoryCampaign/OnToBattleClicked, the new Quests/Events + Collection hub launchers,
    /// OpenEmpire) rather than inventing new navigation.</summary>
    private void BuildDestinationBar(Transform parent)
    {
        GameObject bar = new GameObject("DestinationBar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(parent, false);
        SetScreenRectFromTopLeftPixels(bar.GetComponent<RectTransform>(), 0, 970, 1920, 1080);
        Image barBg = bar.GetComponent<Image>();
        barBg.sprite = null;
        barBg.color = UIFrozenTokens.ColorHeader;
        barBg.raycastTarget = false;

        const int destCount = 5;
        const float gap = 8f;
        const float barLeft = 16f;
        const float barRight = 1904f;
        float destWidth = ((barRight - barLeft) - gap * (destCount - 1)) / destCount;

        void PlaceDestination(int index, string label, UnityEngine.Events.UnityAction action)
        {
            float left = barLeft + index * (destWidth + gap);
            float right = left + destWidth;
            // DestinationBar-local placement (WH-UI-HOME-DEST-LOCAL-LAYOUT-001): prior call used
            // root screen Y 970..1080 under a nested 110px bar → ~11px buttons + clipped labels.
            CreateDestinationButtonInBar(bar.transform, label, left, right, action);
        }

        PlaceDestination(0, "HOME", RefreshHomeFeed);
        PlaceDestination(1, "BATTLE", OpenStoryCampaign);
        PlaceDestination(2, "QUESTS", OpenQuestsEventsHub);
        PlaceDestination(3, "COLLECTION", OpenCollectionHub);
        PlaceDestination(4, "EMPIRE", OpenEmpire);
    }

    /// <summary>DestinationBar-only builder: parent-local top-left pixels in the bar's 1920×110
    /// design space. Shared drawer/hub callers keep <see cref="CreateDestinationButton"/>.</summary>
    private void CreateDestinationButtonInBar(Transform parent, string label, float leftPx, float rightPx,
        UnityEngine.Events.UnityAction action)
    {
        const float barDesignWidth = 1920f;
        const float barDesignHeight = 110f;
        GameObject btnObj = new GameObject($"Dest_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);
        SetParentLocalRectFromTopLeftPixels(
            btnObj.GetComponent<RectTransform>(), leftPx, 0f, rightPx, barDesignHeight,
            barDesignWidth, barDesignHeight);
        Image img = btnObj.GetComponent<Image>();
        Button btn = btnObj.GetComponent<Button>();
        HomeV3UiLibrary.ApplyNeutralActionButton(btn, img, new Color(0.11f, 0.14f, 0.19f, 0.9f));
        btn.onClick.AddListener(action);
        btnObj.AddComponent<InteractionStateController>().Tier = UIDesignTokens.FrameTier.Tier2Section;

        float labelWidth = Mathf.Max(100f, (rightPx - leftPx) - 24f);
        Text text = UISharedFoundation.CreateText(btnObj.transform, "Label", label, UITextRole.Body,
            TextAnchor.MiddleCenter, HexColor("#F2E5C9"), true, new Vector2(labelWidth, 36f));
        UIDesignTokens.Apply(text, UIDesignTokens.TypeTier.T2Utility);
        text.fontStyle = FontStyle.Bold;
        text.raycastTarget = false;
    }

    private void CreateDestinationButton(Transform parent, string label, float leftPx, float topPx, float rightPx, float bottomPx,
        UnityEngine.Events.UnityAction action)
    {
        GameObject btnObj = new GameObject($"Dest_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);
        SetScreenRectFromTopLeftPixels(btnObj.GetComponent<RectTransform>(), leftPx, topPx, rightPx, bottomPx);
        Image img = btnObj.GetComponent<Image>();
        Button btn = btnObj.GetComponent<Button>();
        HomeV3UiLibrary.ApplyNeutralActionButton(btn, img, new Color(0.11f, 0.14f, 0.19f, 0.9f));
        btn.onClick.AddListener(action);
        // Shared by DestinationBar, both tab hubs, and the Social drawer's tab strip (see the
        // label-box comment below) - one wiring point covers all of them.
        btnObj.AddComponent<InteractionStateController>().Tier = UIDesignTokens.FrameTier.Tier2Section;

        // Label box scales with the REAL button width (was a hardcoded 160px regardless of the
        // caller's actual button size) - at the T2Utility (28px) type floor, a fixed 160px box
        // wrapped longer labels like "COLLECTION" mid-word inside buttons that were actually
        // ~370px wide. 24px side padding, floor of 100px for the narrowest callers.
        float labelWidth = Mathf.Max(100f, (rightPx - leftPx) - 24f);
        Text text = UISharedFoundation.CreateText(btnObj.transform, "Label", label, UITextRole.Body,
            TextAnchor.MiddleCenter, HexColor("#F2E5C9"), true, new Vector2(labelWidth, 36f));
        // Interactive-label hard floor (>=28px, not just the 22px absolute floor - these are
        // Button labels): shared by DestinationBar, both tab hubs, and the Social drawer's tab
        // strip - all of them build their buttons through this one method.
        UIDesignTokens.Apply(text, UIDesignTokens.TypeTier.T2Utility);
        text.fontStyle = FontStyle.Bold;
        text.raycastTarget = false;
    }

    /// <summary>The "HOME" destination is this same screen - refreshes the live feed/HUD rather
    /// than opening anything, matching HomePage's own "no Back button, it is the root" rule
    /// (register: navigation dead-end audit).</summary>
    private void RefreshHomeFeed()
    {
        RefreshTopHUD();
        if (homeCanvasObj != null) homeCanvasObj.SetActive(true);
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

    /// <summary>Parent-local top-left pixels → anchors on that parent (not 1920×1080 root).
    /// Used by DestinationBar children only (WH-UI-HOME-DEST-LOCAL-LAYOUT-001).</summary>
    private static void SetParentLocalRectFromTopLeftPixels(
        RectTransform rect, float left, float top, float right, float bottom,
        float parentWidth, float parentHeight)
    {
        rect.anchorMin = new Vector2(left / parentWidth, 1f - bottom / parentHeight);
        rect.anchorMax = new Vector2(right / parentWidth, 1f - top / parentHeight);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Sprite LoadHomeSprite(string fileName)
    {
        Sprite sprite = HomeV3UiLibrary.Load(fileName);
        if (sprite == null && !string.IsNullOrEmpty(fileName))
        {
            Debug.LogWarning(
                $"[Home] Failed to load HomeV3 sprite '{fileName}' " +
                $"(Resources/{HomeV3UiLibrary.ResourceRoot}{fileName}).");
        }

        return sprite;
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

    public void OpenEmpireForTests() => OpenEmpire();

    /// <summary>Exposed for tests: same hook the Home Avatar tile uses (direct entry, Back → Home).</summary>
    public void OpenAvatarForTests() => OpenAvatar(returnToEmpireOnBack: false);

    /// <summary>Exposed for tests: same hook the Home Settings gear uses.</summary>
    public void OpenSettingsForTests() => OpenSettings();

    public void OpenBattlePassForTests() => OpenBattlePass();

    public void OpenDailyLoginQuestsForTests() => OpenDailyLoginQuests();

    /// <summary>Destroy is not legal outside Play Mode (this project's own non-negotiable rule -
    /// DestroyImmediate(), not Destroy(), for anything reachable from Initialize(); EditMode
    /// tests that click through Empire/Avatar/Campaign nav reach this directly).
    /// Production (Play Mode) behavior and timing are unchanged - Destroy still runs there.</summary>
    private static void SafeDestroy(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

    private void EnsureTelemetryOutbox()
    {
        if (_telemetryOutbox == null)
            _telemetryOutbox = new RetentionTelemetryOutbox(new UnityCloudCodeRetentionTelemetryGateway());
    }

    /// <summary>Home dock feature_entry — real retention-telemetry emit (register: remaining
    /// Metagame-owned call sites). Enqueue never blocks/throws.</summary>
    private void EmitFeatureEntry(string mode)
    {
        if (string.IsNullOrEmpty(mode)) return;
        EnsureTelemetryOutbox();
        string playerId = RetentionTelemetryPlayerId.CurrentOrEmpty();
        _telemetryOutbox.Enqueue(RetentionTelemetryEvents.FeatureEntry(playerId, mode));
        _ = _telemetryOutbox.FlushAsync(System.Threading.CancellationToken.None);
    }

    /// <summary>Exposed for tests: same FeatureEntry emit Open* paths use.</summary>
    public void EmitFeatureEntryForTests(string mode) => EmitFeatureEntry(mode);

    private void OpenEmpire()
    {
        EmitFeatureEntry("empire");
        if (homeCanvasObj != null) homeCanvasObj.SetActive(false);

        CampaignMapPresenter.CleanupStaleMetagameCanvases();

        EmpirePresenter empire = gameObject.GetComponent<EmpirePresenter>();
        if (empire == null) empire = gameObject.AddComponent<EmpirePresenter>();

        empire.Initialize(
            onBackToHome: () =>
            {
                if (homeCanvasObj != null) homeCanvasObj.SetActive(true);
                SaveManager.Save();
                RefreshTopHUD();
                SafeDestroy(empire);
            },
            onOpenAvatar: () =>
            {
                SafeDestroy(empire);
                OpenAvatar(returnToEmpireOnBack: true);
            });
    }

    private void OpenAvatar(bool returnToEmpireOnBack = false)
    {
        EmitFeatureEntry("avatar");
        if (homeCanvasObj != null) homeCanvasObj.SetActive(false);
        CampaignMapPresenter.CleanupStaleMetagameCanvases();

        AvatarPresenter avatar = gameObject.GetComponent<AvatarPresenter>();
        if (avatar == null) avatar = gameObject.AddComponent<AvatarPresenter>();

        avatar.Initialize(
            onBackToHome: () =>
            {
                if (returnToEmpireOnBack)
                {
                    SafeDestroy(avatar);
                    OpenEmpire();
                    return;
                }

                if (homeCanvasObj != null) homeCanvasObj.SetActive(true);
                RefreshTopHUD();
                SafeDestroy(avatar);
            },
            onOpenEmpire: () =>
            {
                SafeDestroy(avatar);
                OpenEmpire();
            },
            onOpenSpellLoadout: () =>
            {
                SafeDestroy(avatar);
                OpenSpellLoadoutPicker();
            });
    }

    private void BuildSettingsEntryButton(Transform parent)
    {
        GameObject btnObj = new GameObject("Btn_Settings", typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);
        SetScreenRectFromTopLeftPixels(btnObj.GetComponent<RectTransform>(), 1780, 18, 1896, 90);

        Image img = btnObj.GetComponent<Image>();
        Button btn = btnObj.GetComponent<Button>();
        Sprite gear = Resources.Load<Sprite>("UI/Icons/icon_settings_gear");
        if (gear != null)
        {
            img.sprite = gear;
            img.preserveAspect = true;
        }
        else
        {
            Debug.LogWarning("[Home] Failed to load settings gear sprite 'UI/Icons/icon_settings_gear'.");
        }

        HomeV3UiLibrary.ApplyNeutralActionButton(btn, img, new Color(1f, 1f, 1f, gear != null ? 1f : 0.85f));
        if (gear == null)
            img.color = new Color(0.2f, 0.24f, 0.3f, 0.9f);

        btn.onClick.AddListener(OpenSettings);
        // Small icon button - Tier4Surface (no scale change, tint only per the locked tokens).
        btnObj.AddComponent<InteractionStateController>().Tier = UIDesignTokens.FrameTier.Tier4Surface;

        if (gear == null)
        {
            Text label = UISharedFoundation.CreateText(btnObj.transform, "Label", "⚙", UITextRole.Display,
                TextAnchor.MiddleCenter, HexColor("#F2E5C9"), true, new Vector2(80f, 60f));
            label.fontSize = 28;
            label.raycastTarget = false;
        }
    }

    /// <summary>LOCKED Home IA: Chat/Mail/Friends collapse into ONE global Social drawer,
    /// explicitly NOT a sixth destination and explicitly not three separate Home buttons
    /// (register: "ChatSocial / MailInbox / Friends -> ONE global Social drawer, accessible from
    /// any destination, three tabs inside it, unread badges. Explicitly NOT a sixth bottom-nav
    /// destination"). This button is the drawer's one entry point.</summary>
    private void BuildSocialDrawerEntryButton(Transform parent)
    {
        GameObject btnObj = new GameObject("Btn_SocialDrawer", typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);
        // Widened left edge (was 1656, 116px wide) - the T2Utility (28px) type floor no longer
        // fits "SOCIAL" on one line in the old width, wrapping it mid-word ("SOCIA/L"). ResourceRow
        // ends at 1328, so this has 328px of real clearance to grow into before touching it.
        SetScreenRectFromTopLeftPixels(btnObj.GetComponent<RectTransform>(), 1580, 18, 1772, 90);
        Image img = btnObj.GetComponent<Image>();
        Button btn = btnObj.GetComponent<Button>();
        HomeV3UiLibrary.ApplyNeutralActionButton(btn, img, new Color(0.16f, 0.22f, 0.2f, 0.92f));
        btn.onClick.AddListener(OpenSocialDrawer);
        btnObj.AddComponent<InteractionStateController>().Tier = UIDesignTokens.FrameTier.Tier3Utility;

        Text label = UISharedFoundation.CreateText(btnObj.transform, "Label", "SOCIAL", UITextRole.Caption,
            TextAnchor.MiddleCenter, HexColor("#F2E5C9"), true, new Vector2(180f, 36f));
        UIDesignTokens.Apply(label, UIDesignTokens.TypeTier.T2Utility);
        label.fontStyle = FontStyle.Bold;
        label.raycastTarget = false;
    }

    private enum SocialDrawerTab { Chat, Mail, Friends }

    private GameObject _socialDrawerObj;
    private SocialDrawerTab _socialDrawerActiveTab = SocialDrawerTab.Chat;

    /// <summary>Opens the global Social drawer overlay - three tabs (Chat/Mail/Friends), each
    /// swapping in the real existing presenter's own canvas rather than reimplementing chat/mail/
    /// friends inside the drawer. Accessible from any destination per the locked design; built
    /// here on Home since every destination routes back through Home's own gameObject/lifecycle.</summary>
    private void OpenSocialDrawer()
    {
        EmitFeatureEntry("social_drawer");
        if (homeCanvasObj != null) homeCanvasObj.SetActive(false);
        if (_socialDrawerObj != null) SafeDestroy(_socialDrawerObj);

        _socialDrawerObj = new GameObject("SocialDrawer", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _socialDrawerObj.transform.SetParent(transform, false);
        Canvas canvas = _socialDrawerObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        CanvasScaler scaler = _socialDrawerObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = UISharedFoundation.MatchWidthOrHeight;

        GameObject dim = new GameObject("Dimmer", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(_socialDrawerObj.transform, false);
        UISharedFoundation.StretchFull(dim.GetComponent<RectTransform>());
        Image dimImg = dim.GetComponent<Image>();
        dimImg.color = UIFrozenTokens.ColorBackground;
        dimImg.raycastTarget = true;

        GameObject tabBar = new GameObject("TabBar", typeof(RectTransform), typeof(Image));
        tabBar.transform.SetParent(_socialDrawerObj.transform, false);
        SetScreenRectFromTopLeftPixels(tabBar.GetComponent<RectTransform>(), 0, 0, 1920, 100);
        Image tabBarBg = tabBar.GetComponent<Image>();
        tabBarBg.sprite = null;
        tabBarBg.color = UIFrozenTokens.ColorHeader;
        tabBarBg.raycastTarget = false;

        CreateDestinationButton(tabBar.transform, "CHAT", 24, 0, 644, 100, () => SwitchSocialDrawerTab(SocialDrawerTab.Chat));
        CreateDestinationButton(tabBar.transform, "MAIL", 660, 0, 1280, 100, () => SwitchSocialDrawerTab(SocialDrawerTab.Mail));
        CreateDestinationButton(tabBar.transform, "FRIENDS", 1296, 0, 1896, 100, () => SwitchSocialDrawerTab(SocialDrawerTab.Friends));

        GameObject closeBtnObj = new GameObject("Btn_CloseDrawer", typeof(RectTransform), typeof(Image), typeof(Button));
        closeBtnObj.transform.SetParent(_socialDrawerObj.transform, false);
        SetScreenRectFromTopLeftPixels(closeBtnObj.GetComponent<RectTransform>(), 1780, 970, 1896, 1080);
        Image closeImg = closeBtnObj.GetComponent<Image>();
        Button closeBtn = closeBtnObj.GetComponent<Button>();
        HomeV3UiLibrary.ApplyNeutralActionButton(closeBtn, closeImg, new Color(0.2f, 0.14f, 0.14f, 0.92f));
        closeBtn.onClick.AddListener(CloseSocialDrawer);
        closeBtnObj.AddComponent<InteractionStateController>().Tier = UIDesignTokens.FrameTier.Tier4Surface;
        Text closeLabel = UISharedFoundation.CreateText(closeBtnObj.transform, "Label", "CLOSE", UITextRole.Body,
            TextAnchor.MiddleCenter, HexColor("#F2E5C9"), true, new Vector2(100f, 32f));
        UIDesignTokens.Apply(closeLabel, UIDesignTokens.TypeTier.T2Utility);
        closeLabel.fontStyle = FontStyle.Bold;
        closeLabel.raycastTarget = false;

        SwitchSocialDrawerTab(SocialDrawerTab.Chat);
    }

    private void SwitchSocialDrawerTab(SocialDrawerTab tab)
    {
        _socialDrawerActiveTab = tab;

        // Tear down whichever of the three tab presenters is currently live before opening the
        // newly-selected one - only one tab's content is ever on screen at a time.
        SafeDestroy(gameObject.GetComponent<ChatSocialPresenter>());
        SafeDestroy(gameObject.GetComponent<MailInboxPresenter>());
        SafeDestroy(gameObject.GetComponent<FriendsPresenter>());

        // onBack wired to CloseSocialDrawer, not a no-op - each sub-presenter's own real Back
        // button must close the whole drawer and restore Home, not silently do nothing (a real
        // bug caught before shipping: an empty callback would tear down the sub-presenter's
        // canvas but leave the drawer's tab bar/dimmer stranded on screen with nothing under it).
        switch (tab)
        {
            case SocialDrawerTab.Chat:
                EmitFeatureEntry("chat_social");
                gameObject.AddComponent<ChatSocialPresenter>().Initialize(CloseSocialDrawer);
                break;
            case SocialDrawerTab.Mail:
                EmitFeatureEntry("mail_inbox");
                gameObject.AddComponent<MailInboxPresenter>().Initialize(CloseSocialDrawer);
                break;
            case SocialDrawerTab.Friends:
                EmitFeatureEntry("friends");
                gameObject.AddComponent<FriendsPresenter>().Initialize(CloseSocialDrawer);
                break;
        }
    }

    private void CloseSocialDrawer()
    {
        SafeDestroy(gameObject.GetComponent<ChatSocialPresenter>());
        SafeDestroy(gameObject.GetComponent<MailInboxPresenter>());
        SafeDestroy(gameObject.GetComponent<FriendsPresenter>());
        if (_socialDrawerObj != null) SafeDestroy(_socialDrawerObj);
        _socialDrawerObj = null;
        if (homeCanvasObj != null) homeCanvasObj.SetActive(true);
        RefreshTopHUD();
    }

    /// <summary>Exposed for tests: the Social drawer button's own onClick target.</summary>
    public void OpenSocialDrawerForTests() => OpenSocialDrawer();
    public GameObject SocialDrawerObjectForTests => _socialDrawerObj;

    /// <summary>LOCKED Home IA: DailyLoginQuests, BattlePass, MemoryExpedition, SoloCircuit, the
    /// merged Permit entry, and GuildHallEntry (as a Guild tab) all consolidate under ONE
    /// Quests/Events destination (register: "SoloCircuit -> Quests/Events", "PermitWeekKey ->
    /// Quests/Events, no separate destination", "GuildHallEntry -> Quests/Events as a GUILD TAB").
    /// A tab-strip launcher that opens the real existing presenter per tab - not a deep visual
    /// merge of six screens, a single shared entry point so none of them are a permanent Home
    /// button of their own.</summary>
    private void OpenQuestsEventsHub()
    {
        EmitFeatureEntry("quests_events_hub");
        BuildTabHub("QuestsEventsHub", new (string, UnityEngine.Events.UnityAction)[]
        {
            ("DAILY LOGIN", OpenDailyLoginQuests),
            ("BATTLE PASS", OpenBattlePass),
            ("MEMORY", OpenMemoryExpedition),
            ("CIRCUIT", OpenSoloCircuit),
            ("GUILD", OpenGuildHallEntry),
            ("PERMIT", OpenPermitClaimEntry),
        });
    }

    /// <summary>LOCKED Home IA: Cards/Shop/Bazaar/VIP consolidate under ONE Collection
    /// destination; DeckBuilder is a Collection sub-screen (register: "DeckBuilder -> Collection
    /// (sub-screen, not a root). Owns persistent card/deck state.") reachable via the Cards tab's
    /// own existing "Open Deck Builder" button, unchanged.</summary>
    private void OpenCollectionHub()
    {
        EmitFeatureEntry("collection_hub");
        BuildTabHub("CollectionHub", new (string, UnityEngine.Events.UnityAction)[]
        {
            ("CARDS", OpenCollection),
            ("SHOP", OpenShop),
            ("BAZAAR", OpenBazaar),
            ("VIP", OpenVipSubscription),
        });
    }

    private GameObject _tabHubObj;

    /// <summary>Shared minimal hub shell for Quests/Events and Collection - a tab strip over a
    /// dimmed backdrop; tapping a tab tears the hub down and opens the real existing presenter
    /// for that tab (unchanged onBack, returns straight to Home, same as every other destination
    /// today). Kept deliberately thin rather than a deep per-hub reimplementation.</summary>
    private void BuildTabHub(string hubName, (string label, UnityEngine.Events.UnityAction action)[] tabs)
    {
        if (homeCanvasObj != null) homeCanvasObj.SetActive(false);
        if (_tabHubObj != null) SafeDestroy(_tabHubObj);

        _tabHubObj = new GameObject(hubName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _tabHubObj.transform.SetParent(transform, false);
        Canvas canvas = _tabHubObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = _tabHubObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = UISharedFoundation.MatchWidthOrHeight;

        UISharedFoundation.CreateFullscreenBackground(_tabHubObj.transform, null, UIFrozenTokens.ColorBackground);

        GameObject header = new GameObject("Header", typeof(RectTransform), typeof(Image));
        header.transform.SetParent(_tabHubObj.transform, false);
        SetScreenRectFromTopLeftPixels(header.GetComponent<RectTransform>(), 0, 0, 1920, 100);
        Image headerBg = header.GetComponent<Image>();
        headerBg.sprite = null;
        headerBg.color = UIFrozenTokens.ColorHeader;
        headerBg.raycastTarget = false;

        GameObject backBtnObj = new GameObject("Btn_Back", typeof(RectTransform), typeof(Image), typeof(Button));
        backBtnObj.transform.SetParent(header.transform, false);
        backBtnObj.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 1f);
        backBtnObj.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 1f);
        backBtnObj.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
        backBtnObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(30f, -5f);
        backBtnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(160f, 40f);
        Image backImg = backBtnObj.GetComponent<Image>();
        Button backBtn = backBtnObj.GetComponent<Button>();
        HomeV3UiLibrary.ApplyNeutralActionButton(backBtn, backImg, new Color(0.22f, 0.18f, 0.14f, 0.92f));
        backBtn.onClick.AddListener(CloseTabHub);
        backBtnObj.AddComponent<InteractionStateController>().Tier = UIDesignTokens.FrameTier.Tier4Surface;
        UISharedFoundation.CreateText(backBtnObj.transform, "Text", "< BACK", UITextRole.Body,
            TextAnchor.MiddleCenter, Color.white, true, new Vector2(140f, 34f));

        GameObject tabRow = new GameObject("TabRow", typeof(RectTransform));
        tabRow.transform.SetParent(_tabHubObj.transform, false);
        SetScreenRectFromTopLeftPixels(tabRow.GetComponent<RectTransform>(), 220, 24, 1896, 76);

        float tabGap = 8f;
        float tabWidth = ((1896f - 220f) - tabGap * (tabs.Length - 1)) / tabs.Length;
        for (int i = 0; i < tabs.Length; i++)
        {
            (string label, UnityEngine.Events.UnityAction action) tab = tabs[i];
            float left = 220f + i * (tabWidth + tabGap);
            float right = left + tabWidth;
            UnityEngine.Events.UnityAction wrapped = () =>
            {
                SafeDestroy(_tabHubObj);
                _tabHubObj = null;
                tab.action();
            };
            CreateDestinationButton(_tabHubObj.transform, tab.label, left, 24, right, 76, wrapped);
        }
    }

    private void CloseTabHub()
    {
        if (_tabHubObj != null) SafeDestroy(_tabHubObj);
        _tabHubObj = null;
        if (homeCanvasObj != null) homeCanvasObj.SetActive(true);
        RefreshTopHUD();
    }

    /// <summary>Exposed for tests.</summary>
    public void OpenQuestsEventsHubForTests() => OpenQuestsEventsHub();
    public void OpenCollectionHubForTests() => OpenCollectionHub();
    public GameObject TabHubObjectForTests => _tabHubObj;

    private void OpenSoloCircuit()
    {
        EmitFeatureEntry("solo_circuit");
        OpenMetagameShellPresenter<SoloCircuitPresenter>(pass => pass.Initialize(SaveManager.SaveData, System.DateTime.UtcNow, () =>
        {
            if (homeCanvasObj != null) homeCanvasObj.SetActive(true);
            RefreshTopHUD();
            SafeDestroy(pass);
        }));
    }

    private void OpenGuildHallEntry()
    {
        EmitFeatureEntry("guild_hall");
        OpenMetagameShellPresenter<GuildHallEntryPresenter>(pass => pass.Initialize(() =>
        {
            if (homeCanvasObj != null) homeCanvasObj.SetActive(true);
            RefreshTopHUD();
            SafeDestroy(pass);
        }));
    }

    public void OpenSoloCircuitForTests() => OpenSoloCircuit();
    public void OpenGuildHallEntryForTests() => OpenGuildHallEntry();

    /// <summary>The merged Permit entry (locked design: "SERVER-KEY and WEEKLY-permit-claim merge
    /// into ONE Quests/Events entry with two labeled sub-states... no player-facing implementation
    /// terminology, no two permanent Home buttons for what's really one logical feature"). Both
    /// sub-states run from this one tab tap: the local scheduled weekly claim, then the
    /// server-authoritative PermitWeekKey screen.</summary>
    private void OpenPermitClaimEntry()
    {
        // Reuses the existing local-claim logic exactly as-is (TryClaimManualWeeklyPermits) - no
        // weeklyPermitStatusText surface exists on this hub tab, so the result is only logged;
        // the second sub-state (server-authoritative) still opens its own real status screen.
        PlayerProfile profile = SaveManager.SaveData;
        if (profile != null)
        {
            int granted = TryClaimManualWeeklyPermits(profile, out string status);
            if (granted > 0) SaveManager.Save();
            Debug.Log($"[Home] Weekly permit claim: {status}");
        }

        OpenPermitWeekKey();
    }

    public void OpenBazaarForTests() => OpenBazaar();
    public void OpenChatSocialForTests() => OpenChatSocial();
    public void OpenMailInboxForTests() => OpenMailInbox();
    public void OpenFriendsForTests() => OpenFriends();
    public void OpenMemoryExpeditionForTests() => OpenMemoryExpedition();
    public void OpenVipSubscriptionForTests() => OpenVipSubscription();
    public void OpenPermitWeekKeyForTests() => OpenPermitWeekKey();
    public void OpenSpellLoadoutPickerForTests() => OpenSpellLoadoutPicker();

    private void OpenMetagameShellPresenter<T>(System.Action<T> initialize) where T : MonoBehaviour
    {
        if (homeCanvasObj != null) homeCanvasObj.SetActive(false);
        CampaignMapPresenter.CleanupStaleMetagameCanvases();

        T presenter = gameObject.GetComponent<T>();
        if (presenter == null) presenter = gameObject.AddComponent<T>();
        initialize(presenter);
    }

    private void OpenBazaar()
    {
        EmitFeatureEntry("bazaar");
        OpenMetagameShellPresenter<BazaarPresenter>(pass => pass.Initialize(() =>
        {
            if (homeCanvasObj != null) homeCanvasObj.SetActive(true);
            RefreshTopHUD();
            SafeDestroy(pass);
        }));
    }

    private void OpenChatSocial()
    {
        EmitFeatureEntry("chat_social");
        OpenMetagameShellPresenter<ChatSocialPresenter>(pass => pass.Initialize(() =>
        {
            if (homeCanvasObj != null) homeCanvasObj.SetActive(true);
            RefreshTopHUD();
            SafeDestroy(pass);
        }));
    }

    private void OpenMailInbox()
    {
        EmitFeatureEntry("mail_inbox");
        OpenMetagameShellPresenter<MailInboxPresenter>(pass => pass.Initialize(() =>
        {
            if (homeCanvasObj != null) homeCanvasObj.SetActive(true);
            RefreshTopHUD();
            SafeDestroy(pass);
        }));
    }

    private void OpenFriends()
    {
        EmitFeatureEntry("friends");
        OpenMetagameShellPresenter<FriendsPresenter>(pass => pass.Initialize(() =>
        {
            if (homeCanvasObj != null) homeCanvasObj.SetActive(true);
            RefreshTopHUD();
            SafeDestroy(pass);
        }));
    }

    private void OpenMemoryExpedition()
    {
        EmitFeatureEntry("memory_expedition");
        OpenMetagameShellPresenter<MemoryExpeditionPresenter>(pass => pass.Initialize(() =>
        {
            if (homeCanvasObj != null) homeCanvasObj.SetActive(true);
            RefreshTopHUD();
            SafeDestroy(pass);
        }));
    }

    private void OpenVipSubscription()
    {
        EmitFeatureEntry("vip_subscription");
        OpenMetagameShellPresenter<VipSubscriptionPresenter>(pass => pass.Initialize(() =>
        {
            if (homeCanvasObj != null) homeCanvasObj.SetActive(true);
            RefreshTopHUD();
            SafeDestroy(pass);
        }));
    }

    private void OpenPermitWeekKey()
    {
        EmitFeatureEntry("permit_week_key");
        OpenMetagameShellPresenter<PermitWeekKeyPresenter>(pass => pass.Initialize(() =>
        {
            if (homeCanvasObj != null) homeCanvasObj.SetActive(true);
            RefreshTopHUD();
            SafeDestroy(pass);
        }));
    }

    private void OpenSpellLoadoutPicker()
    {
        EmitFeatureEntry("spell_loadout");
        OpenMetagameShellPresenter<SpellLoadoutPickerPresenter>(pass => pass.Initialize(() =>
        {
            if (homeCanvasObj != null) homeCanvasObj.SetActive(true);
            RefreshTopHUD();
            SafeDestroy(pass);
        }));
    }

    private void OpenSettings()
    {
        EmitFeatureEntry("settings");
        if (homeCanvasObj != null) homeCanvasObj.SetActive(false);
        CampaignMapPresenter.CleanupStaleMetagameCanvases();

        SettingsPresenter settings = gameObject.GetComponent<SettingsPresenter>();
        if (settings == null) settings = gameObject.AddComponent<SettingsPresenter>();

        settings.Initialize(
            onBackToHome: () =>
            {
                if (homeCanvasObj != null) homeCanvasObj.SetActive(true);
                RefreshTopHUD();
                SafeDestroy(settings);
            },
            onLogoutCompleted: _ =>
            {
                if (homeCanvasObj != null) homeCanvasObj.SetActive(true);
                RefreshTopHUD();
                SafeDestroy(settings);
            });
    }

    private void OpenBattlePass()
    {
        EmitFeatureEntry("battle_pass");
        if (homeCanvasObj != null) homeCanvasObj.SetActive(false);
        CampaignMapPresenter.CleanupStaleMetagameCanvases();

        BattlePassPresenter pass = gameObject.GetComponent<BattlePassPresenter>();
        if (pass == null) pass = gameObject.AddComponent<BattlePassPresenter>();

        pass.Initialize(() =>
        {
            if (homeCanvasObj != null) homeCanvasObj.SetActive(true);
            RefreshTopHUD();
            SafeDestroy(pass);
        });
    }

    private void OpenDailyLoginQuests()
    {
        EmitFeatureEntry("daily_login");
        if (homeCanvasObj != null) homeCanvasObj.SetActive(false);
        CampaignMapPresenter.CleanupStaleMetagameCanvases();

        DailyLoginQuestsPresenter daily = gameObject.GetComponent<DailyLoginQuestsPresenter>();
        if (daily == null) daily = gameObject.AddComponent<DailyLoginQuestsPresenter>();

        daily.Initialize(() =>
        {
            if (homeCanvasObj != null) homeCanvasObj.SetActive(true);
            RefreshTopHUD();
            SafeDestroy(daily);
        });
    }

    private void OpenStoryCampaign()
    {
        EmitFeatureEntry("campaign");
        if (homeCanvasObj != null) homeCanvasObj.SetActive(false);
        CampaignMapPresenter.CleanupStaleMetagameCanvases();

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
    /// <summary>Exact Campaign→Deck redirect copy when Barracks still grants the L1 default (10).
    /// EditMode contracts assert this literal; live redirects use <see cref="FormatDeckBlockedMessage"/>.</summary>
    public const string DeckBlockedMessage = "Build and confirm a 10-card deck before launching a Campaign stage.";

    public static string FormatDeckBlockedMessage(int deckSlots) =>
        $"Build and confirm a {deckSlots}-card deck before launching a Campaign stage.";

    public static string FormatNormalBattleDeckBlockedMessage(int deckSlots) =>
        $"Build and confirm a {deckSlots}-card deck before normal Battle.";

    /// <summary>Live Barracks Deck Slots for player-facing copy (falls back to L1 Barracks slots).</summary>
    public static int ResolveDeckSlotCountForCopy()
    {
        PlayerProfile profile = SaveManager.SaveData;
        if (profile == null)
            return PlayerEmpireData.DeckSlotsForBarracksLevel(1);

        profile.ApplyDataToEmpire();
        int slots = profile.Empire != null ? profile.Empire.DeckSlotCount : 0;
        return slots > 0 ? slots : PlayerEmpireData.DeckSlotsForBarracksLevel(1);
    }
    /// <summary>Campaign launch Stamina gate — copy tracks <see cref="GameBootstrap.CampaignStaminaCostPerAttempt"/>.</summary>
    public static string StaminaBlockedMessage =>
        $"Need {GameBootstrap.CampaignStaminaCostPerAttempt} Stamina to launch this stage.";
    public const string LockedBlockedMessage = "This stage is locked. Complete the previous stage first.";
    public const string InvalidConfigBlockedMessage = "This stage cannot launch because its battle setup is invalid.";
    public const string GateBlockedMessage = "This chapter requires a higher Gate level. Upgrade Gate on the Empire screen to proceed.";

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

        // Block W: Gate is necessary but not sufficient (EMPIRE_SCHEMA_LOCK) - stage unlock alone
        // no longer means Campaign will actually launch. Checked after stage-unlock (a locked
        // stage must always block first, regardless of Gate) and before the deck/Stamina checks.
        if (CampaignMapPresenter.TryParseStageChapter(stageData.stageId, out int chapter)
            && !PlayerEmpireData.IsCampaignChapterAllowedByGate(profile.gateLevel, chapter))
        {
            return CampaignLaunchOutcome.BlockedByGate;
        }

        if (bootstrap != null && !bootstrap.HasValidConfirmedDeckForNormalBattle())
        {
            // Requirement 3: routed directly to Deck Builder with its own existing status
            // surface - the player is never stranded on a now-meaningless Campaign map.
            bootstrap.EnsureApprovedStarterCollectionGranted();
            if (campaign != null)
            {
                campaign.TeardownMapForBattle();
                SafeDestroy(campaign);
            }
            OpenDeckBuilder(entryStatusMessage: FormatDeckBlockedMessage(ResolveDeckSlotCountForCopy()));
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
        if (campaign != null)
        {
            campaign.TeardownMapForBattle();
            SafeDestroy(campaign);
        }
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

    private void OpenDeckBuilder(string entryStatusMessage = null, bool returnToCollectionOnBack = false)
    {
        EmitFeatureEntry("deck_builder");
        if (homeCanvasObj != null) homeCanvasObj.SetActive(false);
        CampaignMapPresenter.CleanupStaleMetagameCanvases();

        DeckBuilderPresenter deckBuilder = gameObject.GetComponent<DeckBuilderPresenter>();
        if (deckBuilder == null) deckBuilder = gameObject.AddComponent<DeckBuilderPresenter>();

        deckBuilder.Initialize(
            onBackToHome: () =>
            {
                if (returnToCollectionOnBack)
                {
                    deckBuilder.TeardownUI();
                    SafeDestroy(deckBuilder);
                    OpenCollection();
                    return;
                }

                if (homeCanvasObj != null) homeCanvasObj.SetActive(true);
                SaveManager.Save();
                RefreshTopHUD();
                deckBuilder.TeardownUI();
                SafeDestroy(deckBuilder);
            },
            entryStatusMessage: entryStatusMessage,
            onOpenCollection: () =>
            {
                deckBuilder.TeardownUI();
                SafeDestroy(deckBuilder);
                OpenCollection();
            }
        );
    }

    private void OpenCollection()
    {
        EmitFeatureEntry("collection");
        if (homeCanvasObj != null) homeCanvasObj.SetActive(false);
        CampaignMapPresenter.CleanupStaleMetagameCanvases();

        CollectionPresenter collection = gameObject.GetComponent<CollectionPresenter>();
        if (collection == null) collection = gameObject.AddComponent<CollectionPresenter>();

        collection.Initialize(
            onBackToHome: () =>
            {
                if (homeCanvasObj != null) homeCanvasObj.SetActive(true);
                SaveManager.Save();
                RefreshTopHUD();
                collection.TeardownUI();
                SafeDestroy(collection);
            },
            onOpenDeckBuilder: () =>
            {
                collection.TeardownUI();
                SafeDestroy(collection);
                OpenDeckBuilder(returnToCollectionOnBack: true);
            }
        );
    }

    private void OpenShop()
    {
        EmitFeatureEntry("shop");
        if (homeCanvasObj != null) homeCanvasObj.SetActive(false);
        CampaignMapPresenter.CleanupStaleMetagameCanvases();

        ShopPresenter shop = gameObject.GetComponent<ShopPresenter>();
        if (shop == null) shop = gameObject.AddComponent<ShopPresenter>();

        shop.Initialize(
            profile: SaveManager.SaveData,
            onBackToHome: () =>
            {
                SaveManager.Save();
                if (homeCanvasObj != null) homeCanvasObj.SetActive(true);
                RefreshTopHUD();
                shop.TeardownUI();
                SafeDestroy(shop);
            },
            onOpenCollection: () =>
            {
                shop.TeardownUI();
                SafeDestroy(shop);
                OpenCollection();
            }
        );
    }

    private void RefreshTopHUD()
    {
        if (SaveManager.SaveData == null) return;
        if (goldHudText != null) goldHudText.text = $"{SaveManager.SaveData.gold}";
        if (gemsHudText != null) gemsHudText.text = $"{SaveManager.SaveData.gems}";
        if (energyHudText != null) energyHudText.text = $"{SaveManager.SaveData.stamina}/{SaveManager.SaveData.maxStamina}";

        if (avatarIdentityText != null)
        {
            PlayerProfile profile = SaveManager.SaveData;
            profile.ApplyDataToEmpire();
            avatarIdentityText.text =
                $"Avatar L{Mathf.Max(1, profile.avatarLevel)} · Cap {profile.Empire.ResourceCap} · Start HP {profile.Empire.StartingAvatarHealth}";
        }
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

        // Text plate with dark scrim backing to guarantee WCAG AA contrast over bright pill art
        GameObject textPlate = new GameObject("ResourceTextPlate", typeof(RectTransform), typeof(Image));
        textPlate.transform.SetParent(pillRoot.transform, false);
        SetLocalNormalisedRect(textPlate.GetComponent<RectTransform>(), 0.05f, 0.05f, 0.95f, 0.95f);
        Image plateFill = textPlate.GetComponent<Image>();
        plateFill.sprite = UISharedFoundation.CreateRoundedPanelSprite(
            new Color(0.03f, 0.035f, 0.05f, 0.95f),
            new Color(0.03f, 0.035f, 0.05f, 0.95f), cornerRadius: 1);
        plateFill.type = Image.Type.Simple;
        plateFill.color = Color.white;
        plateFill.raycastTarget = false;
        UISharedFoundation.AddLocalGradientScrim(
            textPlate.transform,
            Vector2.zero,
            new Vector2(130f, 40f),
            UISharedFoundation.GradientDirection.TopToBottom, 0.98f);

        // Shared text-only layout for all three resource pills
        Text pillLabel = UISharedFoundation.CreateText(
            textPlate.transform, "ResourceLabel", label,
            MyriadOfDragons.UI.UITextRole.Body, TextAnchor.MiddleLeft, Color.white, true, new Vector2(50f, 30f));
        pillLabel.fontSize = 22;
        pillLabel.fontStyle = FontStyle.Bold;
        pillLabel.raycastTarget = false;
        SetLocalNormalisedRect(pillLabel.rectTransform, 0.08f, 0.1f, 0.45f, 0.9f);

        // Value (right side, larger, never truncate)
        Text pillValue = UISharedFoundation.CreateText(
            textPlate.transform, "ResourceValue", value,
            MyriadOfDragons.UI.UITextRole.Display, TextAnchor.MiddleRight, Color.white, true, new Vector2(80f, 30f));
        pillValue.fontSize = 24;
        pillValue.fontStyle = FontStyle.Bold;
        pillValue.raycastTarget = false;
        SetLocalNormalisedRect(pillValue.rectTransform, 0.45f, 0.1f, 0.95f, 0.9f);

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
            OpenDeckBuilder(entryStatusMessage: FormatNormalBattleDeckBlockedMessage(ResolveDeckSlotCountForCopy()));
            return;
        }

        Debug.Log("Transitioning to Battle...");
        CampaignMapPresenter.CleanupStaleMetagameCanvases();
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
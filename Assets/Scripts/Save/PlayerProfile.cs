using System;
using System.Collections.Generic;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Economy;

namespace MyriadOfDragons.Save
{
    [Serializable]
    public class PlayerProfile
    {
        // Basic Profile Info
        public string playerName = "Sovereign";
        public int level = 1;

        // --- 5 CORE CURRENCIES (Blueprint v1.0) ---
        public int gold = 1000;              // 1. Soft Currency (Progression)
        public int gems = 150;               // 2. Premium Currency (Developer Economy)
        public int eventMedals = 0;          // 3. Seasonal Event Tokens
        public int guildContribution = 0;    // 4. Guild Reputation/Bound Currency
        public int dragonRelics = 0;         // 5. Player Trading Currency (Player Economy)

        public int stamina = 100;
        public int maxStamina = 100;

        /// <summary>
        /// SHOP_V2 Stamina ladder (CC 2026-08-23): UTC ticks when the current rolling 24h window
        /// started (0 = never). Additive — old saves deserialize as 0.
        /// </summary>
        public long staminaShopWindowStartUtcTicks = 0;

        /// <summary>
        /// Purchases completed in the current rolling window (0..4). Escalates GemCosts in order.
        /// </summary>
        public int staminaShopPurchasesInWindow = 0;

        /// <summary>
        /// VIP / Subscription entitlement (LOCKED 2026-08-26, additive). Empty = no active plan.
        /// Plan ids: weekly | fortnight | monthly. Unused claims expire when vipExpiresUtcTicks elapses
        /// (see VipSubscriptionOpenValues.ExpireIfLapsed). No combat/deck/progression power.
        /// </summary>
        public string vipPlanId = string.Empty;

        /// <summary>UTC ticks when the current VIP plan started (claim period origin).</summary>
        public long vipStartedUtcTicks = 0;

        /// <summary>UTC ticks when the current VIP plan ends. 0 = none.</summary>
        public long vipExpiresUtcTicks = 0;

        /// <summary>Stamina claims already consumed under the current VIP plan (0..plan max).</summary>
        public int vipClaimsConsumed = 0;

        // --- PROPERTY WRAPPERS FOR UI PRESENTERS ---
        public string PlayerName { get => playerName; set => playerName = value; }
        public int Level { get => level; set => level = value; }
        public int Gold { get => gold; set => gold = value; }
        public int Gems { get => gems; set => gems = value; }
        public int Stamina { get => stamina; set => stamina = value; }
        public int MaxStamina { get => maxStamina; set => maxStamina = value; }
        public int EventMedals { get => eventMedals; set => eventMedals = value; }
        public int GuildContribution { get => guildContribution; set => guildContribution = value; }
        public int DragonRelics { get => dragonRelics; set => dragonRelics = value; }

        // --- PROGRESSION MATERIALS ---
        public int dragonEssence = 0;
        public int heroSouls = 0;
        public int evolutionStones = 0;
        public int limitCores = 0;
        public int skillTomes = 0;

        /// <summary>Empire construction v2's Materials balance (EMPIRE_SCHEMA_LOCK_2026-08-22.md
        /// §4) - a currency separate from Gold, additive field, owner-authorized 2026-08-24.
        /// EmpireExpeditionClearTransaction computes a Materials grant per clear already; this is
        /// the field it had nowhere to persist to (see that class's own "frozen save shape"
        /// comment).</summary>
        public int constructionMaterials = 0;

        // Onboarding & Story Progress
        public bool hasSeenIntro = false;
        public bool seenIntro
        {
            get => hasSeenIntro;
            set => hasSeenIntro = value;
        }
        public List<string> seenChapters = new List<string>();

        // Campaign, Decks & Tradeable Inventory
        //
        // Fresh-profile Chapter 1 stage-access contract: a brand-new profile starts with ONLY
        // Stage 1-1 unlocked - the sequential campaign flow (win 1-1 -> unlock 1-2 -> win 1-2 ->
        // unlock 1-3, via HomePagePresenter.HandleMatchCompleted/CampaignMapPresenter.GetNextStageId,
        // the existing ordered campaign list) is the only way 1-2/1-3 are ever supposed to become
        // reachable. Previously defaulted to {"1-1","1-2"}, which silently unlocked 1-2 for every
        // new player with no win required, contradicting that flow. This default applies ONLY to a
        // genuinely new `PlayerProfile()` construction (no save file exists yet - SaveSystem.Load's
        // NewGame branch never runs JsonUtility deserialization at all); an existing save's real
        // JSON `unlockedStageIds` array always overwrites this field initializer during
        // deserialization regardless of what it is set to here, so no already-progressed existing
        // player loses anything they already had unlocked.
        public List<string> unlockedStageIds = new List<string>() { "1-1" };

        /// <summary>Chapter 1 progression contract: distinguishes "unlocked" (playable) from
        /// "first-clear reward already claimed" - unlockedStageIds alone cannot express this,
        /// since a stage is added to it once (to make it playable) but a player can then replay
        /// a cleared stage any number of times. A stage id lands here exactly once, the first
        /// time HomePagePresenter.HandleMatchCompleted sees a victory for it; every later
        /// replay's reward grant is skipped once its id is already present. Backward compatible:
        /// an old save with no such key deserializes this as null, and SaveMigration.Normalize
        /// (like every other List field here) replaces null with an empty list on load.</summary>
        public List<string> claimedStageRewardIds = new List<string>();
        public List<string> activeDeckCardIds = new List<string>() { "c1", "c3", "c4", "c6" };

        /// <summary>
        /// Spell-Book Acquisition + Ownership Sync (LOCKED 2026-08-24, owner-authorized frozen
        /// field). Every spell this player has ever earned/unlocked - permanent, never removed.
        /// Defaults to the current starter four's real catalog ids (see AvatarSpell.
        /// CreateDefaultSpellbook) so a fresh profile's real ownership already matches what it
        /// could always cast, unchanged from before this field existed. Populated going forward by
        /// MyriadOfDragons.Battle.SpellOwnershipSync.SynchronizeEligibleSpellOwnership (Avatar
        /// level-up, stage first-clear, Spell Book grant, new-profile creation, and once as a
        /// migration repair pass) - not mutated directly anywhere else.
        /// </summary>
        public List<string> ownedSpellIds = new List<string>() { "firestorm", "mend", "war_cry", "divine_bolt" };

        /// <summary>The subset of ownedSpellIds currently equipped for battle (max 4 - see
        /// SPELL_CATALOG_v1.md §4, "player eventually owns 36 and equips 4 per battle"). Defaults
        /// to the same starter four, in the same order, as ownedSpellIds - a fresh profile's real
        /// battle spellbook is unchanged from before this field existed. Real player-choice
        /// loadout UI is separate, future work (not this Wave); until it exists, SpellLoadoutAutoEquip
        /// keeps this in sync with the strongest currently-owned spell per effect type.</summary>
        public List<string> equippedSpellIds = new List<string>() { "firestorm", "mend", "war_cry", "divine_bolt" };

        public List<string> cardCollection = new List<string>();
        public List<TradeableAssetInstance> inventoryAssets = new List<TradeableAssetInstance>();

        // --- Collection schema V1 (additive — COLLECTION_SCHEMA_PROPOSAL_v1, owner opened Save 2026-08-22) ---
        public int collectionSchemaVersion = 0;
        public List<CardProgressionRecord> cardProgression = new List<CardProgressionRecord>();
        public List<string> collectionMigrationUnknownIds = new List<string>();
        public int normalPityMisses = 0;
        public int highPityMissesSince5Star = 0;
        public int highPityMissesSince7Star = 0;
        public int ascensionPermitBalance = 0;
        public string ascensionPermitWeekKey = string.Empty;
        public int ascensionPermitsEarnedThisWeek = 0;
        public CollectionMaterialWallet collectionWallet = new CollectionMaterialWallet();

        /// <summary>True when legacy flat list has been migrated into <see cref="cardProgression"/>.</summary>
        public bool UsesCollectionV1 => collectionSchemaVersion >= CollectionSchemaRules.CurrentCollectionSchemaVersion;

        // Base & Structure Levels
        public int avatarLevel = 1;
        public int castleLevel = 1;
        public int barracksLevel = 1;
        public int gateLevel = 1;

        // --- The remaining 5 upgradeable buildings. Additive, owner-locked 2026-08-25
        // ("Empire building save-schema defaults - LOCKED", BS, vetted) BEFORE this frozen file was
        // touched - same discipline as Memory Expedition and Tactical Puzzle.
        //
        // DEFAULT IS 1, NOT 0, AND THAT IS THE LOCKED DECISION, not a stylistic choice. These are
        // minimum-valid structures rather than absent inventory: Level 1 grants no shortcut,
        // preserves existing progression, and stops a MIGRATED player - especially an existing
        // Evolution user - from being blocked by a field that did not exist when they last played.
        // Old saves deserialize straight to 1, so there is no migration step and no backfill.
        public int storageLevel = 1;
        public int trainingGroundsLevel = 1;
        public int quarryLevel = 1;
        public int academyLevel = 1;
        public int treeOfKnowledgeLevel = 1;

        // --- The last 3 buildings, completing the 11-building set. Additive, OWNER-SIGNED-OFF
        // 2026-08-26 ("OWNER SIGN-OFF: 3 new PlayerProfile fields for Empire interlock") to unblock
        // BS's Day-1/paired-milestone interlock. Proposed, signed off, THEN written - same order as
        // Memory Expedition, Tactical Puzzle and the five above.
        //
        // DEFAULT 1, following the interlock's own Day-1 rule: "all visible at Level 1, upgrade-
        // gated not access-gated". Same reasoning as the other five - a minimum-valid structure
        // rather than an absent one, so a migrated player is never blocked by a field that did not
        // exist when they last played.
        //
        // These three were DELIBERATELY unbacked until now (EmpireBuildingLevels documented
        // "0 means no level field, never level zero"). That contract changes here by owner
        // decision, not by drift - see the test that used to assert it.
        public int embassyLevel = 1;
        public int prisonLevel = 1;
        public int guildHallLevel = 1;

        /// <summary>
        /// Additive Empire construction project (EMPIRE_SCHEMA_LOCK). Null on old saves —
        /// SaveMigration.Normalize replaces with idle.
        /// </summary>
        public EmpireConstructionState empireConstruction = new EmpireConstructionState();

        /// <summary>
        /// Empire Expedition daily scope (EMPIRE_SCHEMA_LOCK_2026-08-22.md §4, owner-authorized
        /// 2026-08-24) - additive only, matches the existing week-key reset-on-mismatch pattern
        /// CollectionAscensionPermits.TryGrantWeekly already uses for ascensionPermitWeekKey,
        /// generalized from week to day. expeditionDayKeyUtc is the stored "yyyy-MM-dd" (UTC) the
        /// two counters below were last reset for - see EmpireExpeditionDailyReset.EnsureCurrentDay,
        /// which resets both counters the moment the real UTC day no longer matches this key.
        /// Deliberately no rotation-key/unlock-flag fields yet - those depend on cadence/unlock
        /// numbers that are still open (not speculative additions).
        /// </summary>
        public string expeditionDayKeyUtc = string.Empty;

        /// <summary>Gold earned via Empire Expedition clears so far on expeditionDayKeyUtc - feeds
        /// the structure-locked, numbers-open daily Expedition Gold cap.</summary>
        public int expeditionGoldEarnedTodayUtc = 0;

        /// <summary>Empire Expedition clear attempts so far on expeditionDayKeyUtc.</summary>
        public int expeditionAttemptsTodayUtc = 0;

        /// <summary>Player Settings (2026-08-24, additive) — audio/notifications/language for utility screen + day-1 translation.</summary>
        public bool settingsAudioEnabled = true;

        /// <summary>Push/notification opt-in stored on profile for cross-device sync.</summary>
        public bool settingsNotificationsEnabled = true;

        /// <summary>BCP-47 language code consumed by realtime translation (Settings screen).</summary>
        public string preferredLanguageCode = "en";

        // --- Daily Login + Daily Quests (LOCKED 2026-08-24; additive Save, owner sign-off) ---

        /// <summary>UTC calendar day (yyyy-MM-dd) of the last successful Daily Login claim.
        /// Empty = never claimed. Additive — old saves deserialize as empty.</summary>
        public string lastLoginClaimUtcDate = string.Empty;

        /// <summary>Login streak index (increments on each successful claim; never resets on a
        /// missed day — streak pauses). Display tier = index % 6. Additive default 0.</summary>
        public int loginStreakIndex = 0;

        /// <summary>UTC day key the current daily quest set / completion mask applies to.</summary>
        public string dailyQuestUtcDate = string.Empty;

        /// <summary>Bitmask of claimed daily quest slots for dailyQuestUtcDate (bits 0..2).</summary>
        public int dailyQuestCompletionMask = 0;

        /// <summary>Optional generation snapshot: packs totalMatches/totalWins at day roll so
        /// play/win quests measure same-day progress without extra Save fields.</summary>
        public int dailyQuestGenerationId = 0;

        /// <summary>Pass Season XP sink for login/quest grants (Battle Pass track reads later).
        /// Additive beyond the register's named field list — flagged for owner sign-off.</summary>
        public int passSeasonXp = 0;

        /// <summary>Shop Loyalty track progress. Additive, owner-signed-off 2026-08-26 ("OWNER
        /// SIGN-OFF: PlayerProfile.shopMilestoneProgress field"), per
        /// docs/Shop_V1_Release_Contract.md:50.
        ///
        /// ACCRUAL ONLY. That spec line requires "a milestone table" alongside this counter and no
        /// such table exists - no thresholds, no rewards. So points accumulate and nothing redeems
        /// them yet; see ShopLoyaltyService, which says so in code rather than implying a reward
        /// path that was never designed.
        ///
        /// Defaults to 0, which is already the correct starting value for an old save, so no
        /// migration step is required. SaveMigration applies AtLeastZero to every other int as a
        /// corrupted-save guard; that file is frozen and was NOT part of this sign-off, so
        /// ShopLoyaltyService floors the value on read and write instead.</summary>
        public int shopMilestoneProgress = 0;

        /// <summary>Highest loyalty milestone POINTS value the player has already claimed.
        /// Additive, owner-signed-off 2026-08-26 ("OWNER SIGN-OFF: highestClaimedLoyaltyMilestone
        /// field") after the milestone ladder was locked.
        ///
        /// Stores the threshold itself, not an index, so a milestone X is claimed iff
        /// X &lt;= this value. One int is sufficient - not a bitmask and not seven flags - because
        /// the ladder is strictly ascending and shopMilestoneProgress never decays.
        ///
        /// Defaults to 0, which is below the lowest rung (100), so an old save correctly reads as
        /// "nothing claimed yet" and no migration step is required. Consequence worth knowing: this
        /// shape forces claims to happen in ascending order, since claiming a high rung would mark
        /// every lower one claimed. ShopLoyaltyService enforces that explicitly rather than letting
        /// a caller silently burn the lower rewards.</summary>
        public int highestClaimedLoyaltyMilestone = 0;

        /// <summary>VIP vouchers earned from Loyalty milestones but not yet activated, in FIFO
        /// order. Additive, auto-approved 2026-08-26 under the extended sign-off protocol.
        ///
        /// EXISTS BECAUSE OF A MEASURED LOCKOUT: vouchers cannot stack with an active subscription,
        /// claims are strictly ascending, and a whale is effectively always subscribed - so the
        /// 250-point rung refused forever and blocked every rung behind it, including the whole
        /// Gold tier. The six-month simulation measured LoyaltyGoldClaimed == 0 for a whale.
        ///
        /// Stores earned ENTITLEMENT RECORDS (which voucher), never active duration. The activation
        /// clock starts only when a voucher actually goes live, so this does not bank active time
        /// and does not conflict with the "no banking past duration" rule.
        ///
        /// Bounded by construction: at most one voucher per voucher-granting milestone, and each
        /// milestone is one-time. Defaults to an empty list, which is the correct reading for every
        /// existing save, so no migration step is needed.</summary>
        public List<string> pendingLoyaltyVipVoucherIds = new List<string>();

        /// <summary>Loyalty Stamina claims earned but not yet delivered, because the real 4-per-24h
        /// Shop refill cap was already spent. Additive, owner-signed-off 2026-08-26 (BS option (c),
        /// benchmarked against Genshin's mailbox delivery model).
        ///
        /// EXISTS BECAUSE THE ENTITLEMENT WAS BEING DESTROYED: the top rung grants 8 Stamina claims
        /// against a cap of 4 per 24h, so at least half were silently discarded the moment they
        /// were earned - the headline whale reward was worth roughly zero to exactly the players
        /// who reached it. The claims were reported as "deferred" in the result and then dropped.
        ///
        /// NOT A CURRENCY: cannot be purchased, traded or converted. Only ever increased by a
        /// successfully claimed Loyalty milestone, and only ever decreased by normal cap-gated
        /// delivery - so it can never be used to exceed the 4/24h ceiling, only to stop losing what
        /// the ceiling defers.
        ///
        /// Defaults to 0, the correct reading for every existing save, so no migration is needed.</summary>
        public int pendingLoyaltyStaminaClaims = 0;

        /// <summary>Solo Collection Circuit daily/weekly state. Additive, owner-signed-off
        /// 2026-08-26 - approved as ONE nested object rather than seven loose fields, mirroring
        /// collectionWallet's shape, so future Circuit state lands inside it without touching this
        /// frozen file again.
        ///
        /// Every field inside defaults to "no circuit run yet" (empty day keys, zero counters), so
        /// a save that predates the feature deserializes correctly and no migration step is needed.
        /// Logic lives in Empire/SoloCollectionCircuit.cs as a plain testable class; this is only
        /// its persistence.</summary>
        public MyriadOfDragons.Empire.SoloCircuitProgress soloCircuitProgress =
            new MyriadOfDragons.Empire.SoloCircuitProgress();

        /// <summary>Count of Battle Pass free-track tiers already claimed, from tier 0 upward.
        /// Additive, owner-signed-off 2026-08-26 ("OWNER SIGN-OFF x2 + protocol extension
        /// locked", exact shape: int, default 0) - ascending-guard mirror of
        /// <see cref="highestClaimedLoyaltyMilestone"/>, adapted from a threshold VALUE to a
        /// 0-based tier INDEX: tier N is claimed iff N &lt; this value (not &lt;=), so this reads
        /// as "how many tiers claimed so far" rather than "highest index claimed" - the same
        /// count either way, but this framing keeps default 0 unambiguously meaning "nothing
        /// claimed yet" without needing a -1 sentinel or an extra field, which the sign-off didn't
        /// include. Tiers unlock in strictly ascending order, so one int is sufficient.</summary>
        public int battlePassClaimedFreeTier = 0;

        /// <summary>Count of Battle Pass paid-track tiers already claimed. Same shape and
        /// reasoning as <see cref="battlePassClaimedFreeTier"/>, tracked separately because the
        /// two tracks claim independently (a player can be ahead on one track and behind on the
        /// other). Paid-track claims additionally require premium to be unlocked - enforced in
        /// BattlePassOpenValues, not by this field.</summary>
        public int battlePassClaimedPaidTier = 0;

        // --- MEMORY EXPEDITION (Tree of Knowledge daily minigame) ---
        // Additive, owner-approved 2026-08-25 (all 12 fields listed and approved verbatim before
        // this frozen file was touched). Core logic lives in Empire/MemoryExpedition.cs as a plain
        // testable class; these are only its persistence. Old saves deserialize to the initializers
        // below, which read as "no run today" and start a fresh run - no migration step needed.

        /// <summary>UTC day (yyyy-MM-dd) the stored run belongs to. Empty = no run yet.</summary>
        public string memoryExpeditionDayKey = string.Empty;

        /// <summary>Layout seed, stored so a run's grid can never re-roll across app launches.</summary>
        public int memoryExpeditionSeed = 0;

        /// <summary>Rules version the stored run was generated under; a bump starts a fresh run.</summary>
        public int memoryExpeditionRulesVersion = 0;

        /// <summary>1-based current round (0 = not started).</summary>
        public int memoryExpeditionCurrentRound = 0;

        /// <summary>Bit per tile index, set once a pair is permanently face-up.</summary>
        public long memoryExpeditionRevealedPairMask = 0L;

        /// <summary>Tile currently flipped awaiting its pair. -1 = none. NOTE the -1 initializer:
        /// defaulting to 0 would make an old save look like "tile 0 is already selected".</summary>
        public int memoryExpeditionFirstSelectedTile = -1;

        /// <summary>Mistakes left in the current round.</summary>
        public int memoryExpeditionMistakesRemaining = 0;

        /// <summary>Highest round cleared today (0..3) - the reward band is keyed off this.</summary>
        public int memoryExpeditionHighestRoundCleared = 0;

        /// <summary>Whether today's single atomic reward claim has been taken. Blocks a second
        /// claim, so a practice replay after claiming grants nothing.</summary>
        public bool memoryExpeditionRewardClaimed = false;

        /// <summary>Whether the run ended by running out of mistakes. Cleared rounds stay credited.</summary>
        public bool memoryExpeditionRunFailed = false;

        /// <summary>Research points held from a Memory Expedition claim.</summary>
        public int temporaryResearchPoints = 0;

        /// <summary>UTC day key on/after which temporaryResearchPoints expire. Null/empty = none
        /// held. Points never silently carry past a UTC reset.</summary>
        public string temporaryResearchExpiryDayKey = string.Empty;

        /// <summary>Reads the persisted fields into the plain logic type. Kept as an explicit
        /// mapping rather than serializing the state class directly, so the save shape and the game
        /// logic can evolve independently.</summary>
        public MyriadOfDragons.Empire.MemoryExpeditionState ToMemoryExpeditionState()
        {
            if (string.IsNullOrEmpty(memoryExpeditionDayKey)) return null;
            return new MyriadOfDragons.Empire.MemoryExpeditionState
            {
                DayKey = memoryExpeditionDayKey,
                Seed = memoryExpeditionSeed,
                RulesVersion = memoryExpeditionRulesVersion,
                CurrentRound = memoryExpeditionCurrentRound,
                RevealedPairMask = memoryExpeditionRevealedPairMask,
                FirstSelectedTile = memoryExpeditionFirstSelectedTile,
                MistakesRemaining = memoryExpeditionMistakesRemaining,
                HighestRoundCleared = memoryExpeditionHighestRoundCleared,
                RewardClaimed = memoryExpeditionRewardClaimed,
                RunFailed = memoryExpeditionRunFailed,
                TemporaryResearchPoints = temporaryResearchPoints,
                TemporaryResearchExpiryDayKey = string.IsNullOrEmpty(temporaryResearchExpiryDayKey)
                    ? null : temporaryResearchExpiryDayKey,
            };
        }

        /// <summary>Writes the logic type back onto the persisted fields. Null clears the run.</summary>
        public void ApplyMemoryExpeditionState(MyriadOfDragons.Empire.MemoryExpeditionState state)
        {
            if (state == null)
            {
                memoryExpeditionDayKey = string.Empty;
                memoryExpeditionFirstSelectedTile = -1;
                return;
            }
            memoryExpeditionDayKey = state.DayKey;
            memoryExpeditionSeed = state.Seed;
            memoryExpeditionRulesVersion = state.RulesVersion;
            memoryExpeditionCurrentRound = state.CurrentRound;
            memoryExpeditionRevealedPairMask = state.RevealedPairMask;
            memoryExpeditionFirstSelectedTile = state.FirstSelectedTile;
            memoryExpeditionMistakesRemaining = state.MistakesRemaining;
            memoryExpeditionHighestRoundCleared = state.HighestRoundCleared;
            memoryExpeditionRewardClaimed = state.RewardClaimed;
            memoryExpeditionRunFailed = state.RunFailed;
            temporaryResearchPoints = state.TemporaryResearchPoints;
            temporaryResearchExpiryDayKey = state.TemporaryResearchExpiryDayKey ?? string.Empty;
        }

        // --- TACTICAL PUZZLE (War-Room Reconstructions) ---
        // Additive, owner-cleared 2026-08-25 after CC vetted and locked the exact field list below
        // (register 5fbc2f1) - proposed, vetted, THEN written, same discipline as Memory Expedition.
        // Core logic lives in Battle/TacticalPuzzleSlate.cs as plain testable classes; these are
        // only its persistence. Old saves deserialize to the initializers below, which read as
        // "no puzzles solved" - no migration step needed.
        //
        // DELIBERATELY SMALL. Unlock/locked state is NOT stored because it is DERIVED from
        // completions by the slate's sequential rule; persisting it would let a later rule change
        // leave old saves disagreeing with the code. In-progress attempts are not stored either -
        // the mode resets immediately on failure and a session rebuilds from the definition.

        /// <summary>One entry per SOLVED puzzle. An absent entry IS "not solved", so no separate
        /// completion flag exists. Keyed by puzzleId only - never by slot index, because slate
        /// ORDER can change between releases and a positional key would silently re-point a
        /// player's completions at different puzzles.</summary>
        public List<TacticalPuzzleRecord> tacticalPuzzleRecords = new List<TacticalPuzzleRecord>();

        /// <summary>Bumped when verifier or reposition rules change, so a migration can invalidate
        /// stored BEST scores without wiping completions - a best earned under different rules may
        /// no longer be reachable.</summary>
        public int tacticalPuzzleRulesVersion = 0;

        /// <summary>Finds a puzzle's record, or null when it has never been solved.</summary>
        public TacticalPuzzleRecord FindTacticalPuzzleRecord(string puzzleId)
        {
            if (string.IsNullOrEmpty(puzzleId) || tacticalPuzzleRecords == null) return null;
            for (int i = 0; i < tacticalPuzzleRecords.Count; i++)
            {
                if (tacticalPuzzleRecords[i] != null && tacticalPuzzleRecords[i].puzzleId == puzzleId)
                    return tacticalPuzzleRecords[i];
            }
            return null;
        }

        public bool HasSolvedTacticalPuzzle(string puzzleId) => FindTacticalPuzzleRecord(puzzleId) != null;

        // Battle History & Stats
        public int winStreak = 0;
        public int totalMatches = 0;
        public int totalWins = 0;

        // Properties & Members Required by GameBootstrap and SaveSystem
        public PlayerProfile Data => this;
        public PlayerEmpireData Empire { get; set; } = new PlayerEmpireData();
        public SaveLoadStatus LoadStatus { get; set; } = SaveLoadStatus.Success;

        public bool SeenIntro
        {
            get => hasSeenIntro;
            set => hasSeenIntro = value;
        }

        public static PlayerProfile LoadOrCreate()
        {
            return SaveSystem.LoadOrCreate();
        }

        // Methods Required by UI Presenters
        public void Save()
        {
            SaveSystem.Save(this);
        }

        public void AddGold(int amount)
        {
            gold += amount;
        }

        public void AddGems(int amount)
        {
            gems += amount;
        }

        public bool TrySpendGold(int amount)
        {
            if (gold >= amount)
            {
                gold -= amount;
                return true;
            }
            return false;
        }

        public bool TrySpendGems(int amount)
        {
            if (gems >= amount)
            {
                gems -= amount;
                return true;
            }
            return false;
        }

        public void UnlockStage(string stageId)
        {
            if (!unlockedStageIds.Contains(stageId))
            {
                unlockedStageIds.Add(stageId);
            }
        }

        public void RestoreStamina(int amount)
        {
            stamina = Math.Min(stamina + amount, maxStamina);
        }

        /// <summary>
        /// Pushes this profile's persisted levels into Empire and derives everything the battle
        /// layer actually reads from them (DeckSlotCount, ResourceCap, StartingAvatarHealth - see
        /// PlayerEmpireData.InitializeTCGModifiers). Without this call Empire keeps its own
        /// from-scratch defaults forever, which for DeckSlotCount is 0 - GameBootstrap.StartNewMatch
        /// sizes both decks off Empire.DeckSlotCount, so a profile whose Empire was never
        /// initialized deals a 0-card hand to every match, not just a mistuned one.
        /// </summary>
        public void ApplyDataToEmpire()
        {
            Empire.SetLevels(avatarLevel, castleLevel, barracksLevel, gateLevel);
            Empire.InitializeTCGModifiers();
        }

        public void ApplyDataToEmpire(object target) { }

        public void ResetOnboarding()
        {
            hasSeenIntro = false;
            seenChapters.Clear();
        }

        public void MarkIntroSeen()
        {
            hasSeenIntro = true;
        }

        public void MarkChapterSeen(string chapterId = "")
        {
            if (!string.IsNullOrEmpty(chapterId) && !seenChapters.Contains(chapterId))
            {
                seenChapters.Add(chapterId);
            }
        }

        public bool HasSeenChapter(string chapterId)
        {
            return seenChapters.Contains(chapterId);
        }

        public void RecordMatchResult(bool isVictory)
        {
            // Raises (or lightly bumps, on a loss) Avatar level and re-derives DeckSlotCount/
            // ResourceCap/StartingAvatarHealth from it - without this a win updates the match
            // history fields below but the battle layer's actual difficulty/deck size never
            // changes, so "winning makes the next match easier" would silently stop being true.
            Empire.ApplyMatchResult(isVictory);
            avatarLevel = Empire.AvatarLevel;
            Empire.InitializeTCGModifiers();

            // Spell-Book Acquisition + Ownership Sync (LOCKED 2026-08-24): Avatar level-up is one
            // of the sync service's required call sites - a level crossing an Avatar-L threshold
            // (e.g. Ember Wave at L5) must grant real ownership the moment it happens, not wait
            // for the player to next open a screen that happens to re-derive it.
            MyriadOfDragons.Battle.SpellOwnershipSync.SynchronizeEligibleSpellOwnership(this);

            totalMatches++;
            if (isVictory)
            {
                totalWins++;
                winStreak++;
            }
            else
            {
                winStreak = 0;
            }
        }

        public void RecordMatchResult(bool isVictory, int ticks)
        {
            RecordMatchResult(isVictory);
        }

        public void RecordMatchResult(object result)
        {
            totalMatches++;
        }
    }
}
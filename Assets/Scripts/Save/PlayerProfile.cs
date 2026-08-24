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
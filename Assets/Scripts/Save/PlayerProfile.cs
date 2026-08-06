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

        // Onboarding & Story Progress
        public bool hasSeenIntro = false;
        public bool seenIntro
        {
            get => hasSeenIntro;
            set => hasSeenIntro = value;
        }
        public List<string> seenChapters = new List<string>();

        // Campaign, Decks & Tradeable Inventory
        public List<string> unlockedStageIds = new List<string>() { "1-1", "1-2" };
        public List<string> activeDeckCardIds = new List<string>() { "c1", "c3", "c4", "c6" };
        public List<string> cardCollection = new List<string>();
        public List<TradeableAssetInstance> inventoryAssets = new List<TradeableAssetInstance>();

        // Base & Structure Levels
        public int avatarLevel = 1;
        public int castleLevel = 1;
        public int barracksLevel = 1;
        public int gateLevel = 1;

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
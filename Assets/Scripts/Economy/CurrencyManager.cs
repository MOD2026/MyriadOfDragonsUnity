using UnityEngine;
using MyriadOfDragons.Save;

namespace MyriadOfDragons.Economy
{
    public static class CurrencyManager
    {
        public static int GetBalance(CurrencyType type)
        {
            PlayerProfile profile = SaveSystem.Profile;
            if (profile == null) return 0;

            switch (type)
            {
                case CurrencyType.Gold: return profile.gold;
                case CurrencyType.Gems: return profile.gems;
                case CurrencyType.EventMedal: return profile.eventMedals;
                case CurrencyType.GuildContribution: return profile.guildContribution;
                case CurrencyType.DragonRelic: return profile.dragonRelics;
                default: return 0;
            }
        }

        public static bool AddCurrency(CurrencyType type, int amount)
        {
            if (amount <= 0) return false;
            PlayerProfile profile = SaveSystem.Profile;
            if (profile == null) return false;

            switch (type)
            {
                case CurrencyType.Gold: profile.gold += amount; break;
                case CurrencyType.Gems: profile.gems += amount; break;
                case CurrencyType.EventMedal: profile.eventMedals += amount; break;
                case CurrencyType.GuildContribution: profile.guildContribution += amount; break;
                case CurrencyType.DragonRelic: profile.dragonRelics += amount; break;
            }

            SaveSystem.Save(profile);
            Debug.Log($"[CurrencyManager] Added {amount} {type}. New Balance: {GetBalance(type)}");
            return true;
        }

        public static bool SpendCurrency(CurrencyType type, int amount)
        {
            if (amount <= 0) return false;
            PlayerProfile profile = SaveSystem.Profile;
            if (profile == null) return false;

            if (GetBalance(type) < amount)
            {
                Debug.LogWarning($"[CurrencyManager] Insufficient {type}! Required: {amount}, Current: {GetBalance(type)}");
                return false;
            }

            switch (type)
            {
                case CurrencyType.Gold: profile.gold -= amount; break;
                case CurrencyType.Gems: profile.gems -= amount; break;
                case CurrencyType.EventMedal: profile.eventMedals -= amount; break;
                case CurrencyType.GuildContribution: profile.guildContribution -= amount; break;
                case CurrencyType.DragonRelic: profile.dragonRelics -= amount; break;
            }

            SaveSystem.Save(profile);
            Debug.Log($"[CurrencyManager] Spent {amount} {type}. Remaining Balance: {GetBalance(type)}");
            return true;
        }

        public static bool ExecutePlayerTrade(PlayerProfile seller, PlayerProfile buyer, TradeableAssetInstance asset, int relicPrice)
        {
            if (!asset.isTradeable || asset.isSoulbound)
            {
                Debug.LogError("[CurrencyManager] Trade Rejected: Asset is soulbound or non-tradeable.");
                return false;
            }

            if (buyer.dragonRelics < relicPrice)
            {
                Debug.LogError("[CurrencyManager] Trade Rejected: Buyer has insufficient Dragon Relics.");
                return false;
            }

            // Execute Transaction
            buyer.dragonRelics -= relicPrice;
            seller.dragonRelics += relicPrice;

            // Re-bind Asset
            seller.inventoryAssets.Remove(asset);
            buyer.inventoryAssets.Add(asset);

            SaveSystem.Save(seller);
            SaveSystem.Save(buyer);
            Debug.Log($"[CurrencyManager] Successfully traded Asset {asset.instanceId} for {relicPrice} Dragon Relics!");
            return true;
        }
    }
}
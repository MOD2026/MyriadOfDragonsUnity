using UnityEngine;
using MyriadOfDragons.Save;

namespace MyriadOfDragons.Economy
{
    public static class CurrencyManager
    {
        public static int GetBalance(CurrencyType type) => GetBalance(SaveSystem.Profile, type);

        /// <summary>Explicit-profile overload - the session-global <see cref="SaveSystem.Profile"/>
        /// is not always the same instance a caller is working against (e.g. a presenter holding
        /// its own PlayerProfile reference), so every wallet operation has a form that takes the
        /// profile to act on directly rather than always reaching for the global singleton.</summary>
        public static int GetBalance(PlayerProfile profile, CurrencyType type)
        {
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

        public static bool AddCurrency(CurrencyType type, int amount) => AddCurrency(SaveSystem.Profile, type, amount);

        /// <summary>Grants currency on the given profile. <paramref name="persist"/> lets a caller
        /// that is about to perform further mutations on the same profile (e.g. a purchase that
        /// also spends currency) defer the disk write until its own transaction is complete,
        /// instead of this call saving a half-finished state.</summary>
        public static bool AddCurrency(PlayerProfile profile, CurrencyType type, int amount, bool persist = true)
        {
            if (amount <= 0 || profile == null) return false;

            switch (type)
            {
                case CurrencyType.Gold: profile.gold += amount; break;
                case CurrencyType.Gems: profile.gems += amount; break;
                case CurrencyType.EventMedal: profile.eventMedals += amount; break;
                case CurrencyType.GuildContribution: profile.guildContribution += amount; break;
                case CurrencyType.DragonRelic: profile.dragonRelics += amount; break;
            }

            if (persist) SaveSystem.Save(profile);
            Debug.Log($"[CurrencyManager] Added {amount} {type}. New Balance: {GetBalance(profile, type)}");
            return true;
        }

        public static bool SpendCurrency(CurrencyType type, int amount) => SpendCurrency(SaveSystem.Profile, type, amount);

        /// <summary>Spends currency on the given profile, refusing (and touching nothing) if the
        /// balance is insufficient. See <see cref="AddCurrency(PlayerProfile,CurrencyType,int,bool)"/>
        /// for why <paramref name="persist"/> exists.</summary>
        public static bool SpendCurrency(PlayerProfile profile, CurrencyType type, int amount, bool persist = true)
        {
            if (amount <= 0 || profile == null) return false;

            if (GetBalance(profile, type) < amount)
            {
                Debug.LogWarning($"[CurrencyManager] Insufficient {type}! Required: {amount}, Current: {GetBalance(profile, type)}");
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

            if (persist) SaveSystem.Save(profile);
            Debug.Log($"[CurrencyManager] Spent {amount} {type}. Remaining Balance: {GetBalance(profile, type)}");
            return true;
        }

        /// <summary>Stamina is a resource, not a tradeable/giftable ICurrency (see
        /// CurrencyDefinitions.cs), so it is not a CurrencyType case above - but it is still
        /// wallet-owned player state, so its grant lives here rather than as a direct field write
        /// at any call site. Delegates to PlayerProfile's own RestoreStamina for the clamp-to-max
        /// rule so that rule has exactly one home.</summary>
        public static bool RestoreStamina(PlayerProfile profile, int amount, bool persist = true)
        {
            if (amount <= 0 || profile == null) return false;

            profile.RestoreStamina(amount);

            if (persist) SaveSystem.Save(profile);
            Debug.Log($"[CurrencyManager] Restored {amount} Stamina. New Balance: {profile.stamina}/{profile.maxStamina}");
            return true;
        }

        /// <summary>Read-only Stamina balance query, mirroring GetBalance's own null-safe
        /// contract for the currency cases above.</summary>
        public static int GetStamina(PlayerProfile profile) => profile?.stamina ?? 0;

        /// <summary>Deducts Stamina, refusing (and touching nothing) if the balance is
        /// insufficient - the same refuse-cleanly contract SpendCurrency already has. Stamina
        /// entry-cost consumer: Campaign stage attempts (see GameBootstrap.
        /// TrySpendCampaignStaminaForAttempt) - this method itself has no opinion on WHO is
        /// spending or why.</summary>
        public static bool SpendStamina(PlayerProfile profile, int amount, bool persist = true)
        {
            if (amount <= 0 || profile == null) return false;
            if (profile.stamina < amount) return false;

            profile.stamina -= amount;

            if (persist) SaveSystem.Save(profile);
            Debug.Log($"[CurrencyManager] Spent {amount} Stamina. Remaining Balance: {profile.stamina}/{profile.maxStamina}");
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
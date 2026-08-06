using System;

namespace MyriadOfDragons.Economy
{
    public enum CurrencyType
    {
        Gold,
        Gems,
        EventMedal,
        GuildContribution,
        DragonRelic
    }

    public interface ICurrency
    {
        CurrencyType Type { get; }
        string DisplayName { get; }
        bool Tradable { get; }
        bool Giftable { get; }
        bool Purchasable { get; }
        bool Expires { get; }
        bool Premium { get; }
        bool IsPlayerEconomy { get; }
        bool IsDeveloperEconomy { get; }
    }

    public struct GoldCurrency : ICurrency
    {
        public CurrencyType Type => CurrencyType.Gold;
        public string DisplayName => "Gold";
        public bool Tradable => false;
        public bool Giftable => false;
        public bool Purchasable => false;
        public bool Expires => false;
        public bool Premium => false;
        public bool IsPlayerEconomy => false;
        public bool IsDeveloperEconomy => false;
    }

    public struct GemsCurrency : ICurrency
    {
        public CurrencyType Type => CurrencyType.Gems;
        public string DisplayName => "Gems";
        public bool Tradable => false;
        public bool Giftable => false;
        public bool Purchasable => true;
        public bool Expires => false;
        public bool Premium => true;
        public bool IsPlayerEconomy => false;
        public bool IsDeveloperEconomy => true;
    }

    public struct EventMedalCurrency : ICurrency
    {
        public CurrencyType Type => CurrencyType.EventMedal;
        public string DisplayName => "Event Medal";
        public bool Tradable => false;
        public bool Giftable => false;
        public bool Purchasable => false;
        public bool Expires => true;
        public bool Premium => false;
        public bool IsPlayerEconomy => false;
        public bool IsDeveloperEconomy => false;
    }

    public struct GuildContributionCurrency : ICurrency
    {
        public CurrencyType Type => CurrencyType.GuildContribution;
        public string DisplayName => "Guild Contribution";
        public bool Tradable => false;
        public bool Giftable => false;
        public bool Purchasable => false;
        public bool Expires => false;
        public bool Premium => false;
        public bool IsPlayerEconomy => false;
        public bool IsDeveloperEconomy => false;
    }

    public struct DragonRelicCurrency : ICurrency
    {
        public CurrencyType Type => CurrencyType.DragonRelic;
        public string DisplayName => "Dragon Relic";
        public bool Tradable => true;
        public bool Giftable => true;
        public bool Purchasable => false;
        public bool Expires => false;
        public bool Premium => false;
        public bool IsPlayerEconomy => true;
        public bool IsDeveloperEconomy => false;
    }

    [Serializable]
    public class TradeableAssetInstance
    {
        public string instanceId;
        public string assetId;
        public string assetType; // "Hero", "Equipment", "Cosmetic"
        public bool isSoulbound;
        public bool isTradeable;
        public bool isGiftable;
        public bool isMarketplaceEligible;
        public string acquiredDate;
        public string transferLockUntil;
    }
}
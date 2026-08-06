using System.Collections.Generic;

namespace MyriadOfDragons.Items
{
    /// <summary>
    /// Static definitions for every currency/item, grounded in the original design doc's
    /// descriptions (Part I §7, Part III §3, Part VI) and renamed only where the doc's own
    /// generic name didn't fit a Titan-rebellion war game - "High Security" became
    /// "Peace Treaty" (matching the actual icon file's own shorthand, "ppeace.png"), since a
    /// modern tech-security term reads oddly next to Dragon Coins and Titan lore.
    /// </summary>
    public static class ItemDatabase
    {
        public static readonly Dictionary<ItemId, ItemDefinition> Items = new Dictionary<ItemId, ItemDefinition>
        {
            [ItemId.Gold] = new ItemDefinition(
                ItemId.Gold, "Gold",
                "Basic resource. Funds recruiting, building upgrades, and trading.",
                "coin_stack"),

            [ItemId.Food] = new ItemDefinition(
                ItemId.Food, "Food",
                "Basic resource, produced by the Barn. Also funds recruiting soldiers.",
                "food_ui"),

            [ItemId.DragonCoin] = new ItemDefinition(
                ItemId.DragonCoin, "Dragon Coin",
                "Premium in-world currency. Never tradable, unlike Gold and Food.",
                "dragon_coin"),

            [ItemId.Gem] = new ItemDefinition(
                ItemId.Gem, "Gem",
                "IAP currency. Buys card draws, potions, and build-time skips.",
                "gem"),

            [ItemId.Flag] = new ItemDefinition(
                ItemId.Flag, "Flag",
                "Spent to attack a rival - cost scales with the level gap (Part II §5).",
                "flag"),

            [ItemId.HpPotion] = new ItemDefinition(
                ItemId.HpPotion, "HP Potion",
                "Restores Avatar or card Health outside of a match.",
                "hp_potion"),

            [ItemId.ManaPotion] = new ItemDefinition(
                ItemId.ManaPotion, "Mana Potion",
                "Restores Grip of Titans (match resource) between turns - a consumable tied "
                + "directly to the v2 combat resource, not just flavor.",
                "mana_potion"),

            [ItemId.SpellBook] = new ItemDefinition(
                ItemId.SpellBook, "Spell Book",
                "Teaches the Avatar a new spell (Part I, later-phase item).",
                "book"),

            [ItemId.DragonEgg] = new ItemDefinition(
                ItemId.DragonEgg, "Dragon Egg",
                "Feeds a raised dragon during the Pet Raising event (Part V §10) - a concrete "
                + "collectible for that event instead of a generic \"event food\" placeholder.",
                "dragon_eggs"),

            [ItemId.PeaceTreaty] = new ItemDefinition(
                ItemId.PeaceTreaty, "Peace Treaty",
                "Grants temporary immunity from attack. Renamed from the original doc's "
                + "\"High Security\" to fit the setting.",
                "ppeace"),
        };

        public static ItemDefinition Get(ItemId id) => Items[id];
    }
}

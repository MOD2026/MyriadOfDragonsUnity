namespace MyriadOfDragons.Items
{
    /// <summary>
    /// Every currency and consumable item in the game, named and iconed to match the actual
    /// illustrated asset set in Drawing/Jackie/UI/Icons - not generic placeholder names.
    /// </summary>
    public enum ItemId
    {
        Gold,
        Food,
        DragonCoin,
        Gem,
        Flag,
        HpPotion,
        ManaPotion,
        SpellBook,
        DragonEgg,
        PeaceTreaty,
    }

    [System.Serializable]
    public class ItemDefinition
    {
        public ItemId Id;
        public string DisplayName;
        public string Description;
        public string IconResourcePath; // relative to Resources/, no extension

        public ItemDefinition(ItemId id, string displayName, string description, string iconFile)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            IconResourcePath = $"UI/Icons/{iconFile}";
        }
    }
}

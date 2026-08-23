namespace MyriadOfDragons.Save
{
    /// <summary>
    /// SHOP_V2_NUMBERS_PACKET Stamina resource lane — sole authority for Shop potion Gem prices
    /// and +Stamina grant (presenter must not hardcode these).
    /// </summary>
    public static class ShopStaminaCatalog
    {
        public const int StaminaGrantPerPotion = 50;

        /// <summary>Locked Gem costs: 30 / 60 / 120 / 240.</summary>
        public static readonly int[] GemCosts = { 30, 60, 120, 240 };

        public static string SkuIdForGemCost(int gemCost) =>
            gemCost == 30 ? "res_energy" : $"res_stamina_{gemCost}";
    }
}

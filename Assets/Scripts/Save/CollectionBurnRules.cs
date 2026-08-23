namespace MyriadOfDragons.Save
{
    /// <summary>
    /// Exact per-rarity yields from CARD_BURN_AND_INFLATION_PACKET.md (Owner locked).
    /// Index 0 unused; rarities 1–7. Do not replace with formulas.
    /// </summary>
    public static class CollectionBurnRules
    {
        private static readonly int[] TrainingXpByRarity = { 0, 10, 25, 60, 150, 350, 800, 1800 };
        private static readonly int[] SacrificeByRarity = { 0, 1, 2, 4, 8, 16, 32, 64 };
        private static readonly int[] ForgeByRarity = { 0, 10, 25, 60, 150, 350, 800, 1800 };
        private static readonly int[] DustByRarity = { 0, 1, 2, 5, 12, 30, 75, 180 };

        public static int GetTrainingXpYield(int rarity) => Lookup(TrainingXpByRarity, rarity);
        public static int GetGenericSacrificeYield(int rarity) => Lookup(SacrificeByRarity, rarity);
        public static int GetForgeCreditYield(int rarity) => Lookup(ForgeByRarity, rarity);
        public static int GetDustYield(int rarity) => Lookup(DustByRarity, rarity);

        public static int GetYield(CollectionBurnPath path, int rarity)
        {
            switch (path)
            {
                case CollectionBurnPath.TrainingXp: return GetTrainingXpYield(rarity);
                case CollectionBurnPath.GenericSacrifice: return GetGenericSacrificeYield(rarity);
                case CollectionBurnPath.ForgeCredit: return GetForgeCreditYield(rarity);
                case CollectionBurnPath.Dust: return GetDustYield(rarity);
                default: return 0;
            }
        }

        private static int Lookup(int[] table, int rarity)
        {
            rarity = UnityEngine.Mathf.Clamp(rarity, 1, 7);
            return table[rarity];
        }
    }
}

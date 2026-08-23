namespace MyriadOfDragons.Save
{
    public static class CollectionEvolutionRules
    {
        public const int MaxEvolutionStep = 3;

        public static int GoldCostForNextStep(int rarity, int currentStep)
        {
            rarity = UnityEngine.Mathf.Clamp(rarity, 1, 7);
            return 1000 * rarity * (currentStep + 1);
        }

        /// <summary>Owner locked 2026-08-23: step 0→1 is permit-free (onboarding taper); step 2+ requires Permit.</summary>
        public static bool RequiresAscensionPermit(int currentStep) => currentStep >= 1;

        /// <summary>Burn packet: sacrifice credits apply only on the first evolution step; never replace duplicate or Permit.</summary>
        public static int GenericSacrificeCreditsRequired(int currentStep) =>
            currentStep == 0 ? CollectionSchemaRules.GenericSacrificeCreditsFirstStep : 0;

        public static int MaxForgeCreditOffset(int goldCost) =>
            UnityEngine.Mathf.FloorToInt(goldCost * CollectionSchemaRules.ForgeCreditMaxRecipeFraction);

        public static int MaxDustOffset(int goldCost) =>
            UnityEngine.Mathf.FloorToInt(goldCost * CollectionSchemaRules.DustMaxRecipeFraction);

        /// <summary>Applies 20% forge + 10% dust caps against base gold recipe cost.</summary>
        public static int ComputeGoldDue(
            PlayerProfile profile,
            int cardRarity,
            int baseGoldCost,
            out int forgeCreditsApplied,
            out int dustApplied)
        {
            forgeCreditsApplied = 0;
            dustApplied = 0;
            if (profile?.collectionWallet == null || baseGoldCost <= 0)
                return baseGoldCost;

            int maxForge = MaxForgeCreditOffset(baseGoldCost);
            forgeCreditsApplied = UnityEngine.Mathf.Min(profile.collectionWallet.forgeCredits, maxForge);

            int maxDust = MaxDustOffset(baseGoldCost);
            int availableDust = GetDustBalance(profile.collectionWallet, cardRarity);
            dustApplied = UnityEngine.Mathf.Min(availableDust, maxDust);

            return UnityEngine.Mathf.Max(0, baseGoldCost - forgeCreditsApplied - dustApplied);
        }

        public static void ApplyMaterialSpend(
            PlayerProfile profile,
            int cardRarity,
            int forgeCreditsApplied,
            int dustApplied,
            int sacrificeCreditsApplied = 0)
        {
            if (profile?.collectionWallet == null) return;
            profile.collectionWallet.forgeCredits -= forgeCreditsApplied;
            profile.collectionWallet.genericSacrificeCredits -= sacrificeCreditsApplied;
            if (dustApplied > 0)
                SpendDust(profile.collectionWallet, cardRarity, dustApplied);
        }

        /// <summary>Read-only Dust balance for a rarity (Collection UI honesty).</summary>
        public static int GetDustBalance(CollectionMaterialWallet wallet, int rarity)
        {
            if (wallet?.dustByRarity == null) return 0;
            rarity = UnityEngine.Mathf.Clamp(rarity, 1, 7);
            foreach (RarityMaterialBalance entry in wallet.dustByRarity)
            {
                if (entry.rarity == rarity) return entry.dust;
            }

            return 0;
        }

        private static void SpendDust(CollectionMaterialWallet wallet, int rarity, int amount)
        {
            foreach (RarityMaterialBalance entry in wallet.dustByRarity)
            {
                if (entry.rarity != rarity) continue;
                entry.dust -= amount;
                return;
            }
        }
    }
}

using MyriadOfDragons.Save;

namespace MyriadOfDragons.UI
{
    /// <summary>Player-facing Collection copy (docs/FORGE_DUST_PLAYER_COPY_v1.md).
    /// Fractions come from <see cref="CollectionSchemaRules"/> so UI cannot drift from the recipe caps.</summary>
    public static class CollectionPlayerCopy
    {
        public static string ForgeCreditTooltip =>
            $"Forge Credit lowers up to {PercentLabel(CollectionSchemaRules.ForgeCreditMaxRecipeFraction)} of one Evolution fee. It cannot replace the required card or Permit.";

        public static string DustTooltip =>
            $"Dust supports a matching Evolution recipe up to {PercentLabel(CollectionSchemaRules.DustMaxRecipeFraction)}. The right duplicate still matters.";

        public const string BurnEmptyState =
            "No surplus cards to burn. Keep collecting duplicates to strengthen the cards you choose.";

        public const string BurnConfirmation =
            "Burn this surplus card for materials? This card becomes Forge Credit, Dust, training, or sacrifice support — never Gold.";

        public const string EvolutionShortage =
            "Power is earned in pieces: a matching duplicate, scarce materials, and Gold. Choose your next Evolution carefully.";

        /// <summary>Short caption under the detail wallet row.</summary>
        public static string ForgeDustWalletCaption =>
            $"Forge and Dust trim Evolution's Gold cost — up to {PercentLabel(CollectionSchemaRules.ForgeCreditMaxRecipeFraction)} Forge / {PercentLabel(CollectionSchemaRules.DustMaxRecipeFraction)} Dust, not a shortcut around it.";

        private static string PercentLabel(float fraction) =>
            $"{UnityEngine.Mathf.RoundToInt(fraction * 100f)}%";
    }
}

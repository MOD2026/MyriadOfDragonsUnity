using System;

namespace MyriadOfDragons.Save
{
    /// <summary>
    /// SHOP_V2_NUMBERS_PACKET Stamina resource lane — sole authority for Shop potion Gem prices,
    /// +Stamina grant, and the rolling 24h / escalate-in-order purchase ladder.
    /// </summary>
    public static class ShopStaminaCatalog
    {
        public const int StaminaGrantPerPotion = 50;

        /// <summary>Locked Gem costs: 30 / 60 / 120 / 240 — purchase index 0..3 within a window.</summary>
        public static readonly int[] GemCosts = { 30, 60, 120, 240 };

        public const int MaxPurchasesPerRollingDay = 4;

        public static readonly long RollingWindowTicks = TimeSpan.FromHours(24).Ticks;

        public static string SkuIdForGemCost(int gemCost) =>
            gemCost == 30 ? "res_energy" : $"res_stamina_{gemCost}";

        public static void NormalizeLadderFields(PlayerProfile profile)
        {
            if (profile == null) return;
            if (profile.staminaShopWindowStartUtcTicks < 0)
                profile.staminaShopWindowStartUtcTicks = 0;
            if (profile.staminaShopPurchasesInWindow < 0)
                profile.staminaShopPurchasesInWindow = 0;
            if (profile.staminaShopPurchasesInWindow > MaxPurchasesPerRollingDay)
                profile.staminaShopPurchasesInWindow = MaxPurchasesPerRollingDay;
        }

        /// <summary>Resets the rolling window when expired (or never started).</summary>
        public static void RefreshRollingWindow(PlayerProfile profile, long nowUtcTicks)
        {
            if (profile == null) return;
            NormalizeLadderFields(profile);

            if (profile.staminaShopWindowStartUtcTicks <= 0
                || nowUtcTicks - profile.staminaShopWindowStartUtcTicks >= RollingWindowTicks)
            {
                profile.staminaShopWindowStartUtcTicks = nowUtcTicks;
                profile.staminaShopPurchasesInWindow = 0;
            }
        }

        /// <summary>Next Gem cost in the escalate-in-order ladder, or false if daily cap hit.</summary>
        public static bool TryGetNextGemCost(PlayerProfile profile, long nowUtcTicks, out int gemCost, out string error)
        {
            gemCost = 0;
            error = null;
            if (profile == null)
            {
                error = "No profile.";
                return false;
            }

            RefreshRollingWindow(profile, nowUtcTicks);
            int index = profile.staminaShopPurchasesInWindow;
            if (index >= MaxPurchasesPerRollingDay || index >= GemCosts.Length)
            {
                error = $"Daily Stamina refill limit reached ({MaxPurchasesPerRollingDay} per 24h).";
                return false;
            }

            gemCost = GemCosts[index];
            return true;
        }

        /// <summary>True when this SKU's Gem price is the next required ladder step.</summary>
        public static bool IsGemCostAllowedNow(PlayerProfile profile, int gemCost, long nowUtcTicks, out string error)
        {
            if (!TryGetNextGemCost(profile, nowUtcTicks, out int nextCost, out error))
                return false;

            if (gemCost != nextCost)
            {
                error = gemCost < nextCost
                    ? $"Already purchased this tier — next refill is {nextCost} Gems."
                    : $"Buy the {nextCost}-Gem Stamina refill next (ladder escalates in order).";
                return false;
            }

            error = null;
            return true;
        }

        public static void RecordSuccessfulPurchase(PlayerProfile profile, long nowUtcTicks)
        {
            if (profile == null) return;
            RefreshRollingWindow(profile, nowUtcTicks);
            if (profile.staminaShopPurchasesInWindow < MaxPurchasesPerRollingDay)
                profile.staminaShopPurchasesInWindow++;
        }

        public static long NowUtcTicks() => DateTime.UtcNow.Ticks;
    }
}

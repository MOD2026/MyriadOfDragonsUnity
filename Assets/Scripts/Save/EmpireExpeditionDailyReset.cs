using System;

namespace MyriadOfDragons.Save
{
    /// <summary>
    /// Resets Empire Expedition's two daily counters (expeditionGoldEarnedTodayUtc/
    /// expeditionAttemptsTodayUtc) the moment the real UTC calendar day no longer matches
    /// expeditionDayKeyUtc - the same reset-on-key-mismatch shape CollectionAscensionPermits.
    /// TryGrantWeekly already uses for ascensionPermitWeekKey (compare current period key to a
    /// stored one, reset if different), generalized from ISO week to UTC calendar day. Not the
    /// rolling-24h-window shape ShopStaminaCatalog uses for staminaShopWindowStartUtcTicks - a
    /// "daily cap" is meant to reset at a predictable UTC midnight, not 24h after whenever the
    /// player happened to last spend, which the register's own "daily Expedition Gold cap" wording
    /// implies.
    /// </summary>
    public static class EmpireExpeditionDailyReset
    {
        public static string CurrentUtcDayKey() => DateTime.UtcNow.ToString("yyyy-MM-dd");

        /// <summary>Call before reading either daily counter - resets both to 0 and stamps
        /// expeditionDayKeyUtc to today the moment the stored key no longer matches the real UTC
        /// day. A no-op on every call within the same UTC day.</summary>
        public static void EnsureCurrentDay(PlayerProfile profile)
        {
            if (profile == null) return;

            string currentDayKey = CurrentUtcDayKey();
            if (profile.expeditionDayKeyUtc == currentDayKey) return;

            profile.expeditionDayKeyUtc = currentDayKey;
            profile.expeditionGoldEarnedTodayUtc = 0;
            profile.expeditionAttemptsTodayUtc = 0;
        }
    }
}

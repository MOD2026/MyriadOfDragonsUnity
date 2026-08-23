namespace MyriadOfDragons.Save
{
    /// <summary>
    /// Ascension Permit ledger helpers.
    /// Weekly earn uses a trusted week key (server/event — empty key refuses).
    /// Milestone grants (e.g. Ch1 1-3 clear) are hoard-capped only and never touch the weekly ledger.
    /// </summary>
    public static class CollectionAscensionPermits
    {
        /// <summary>Placeholder trusted week key until the server ledger supplies ISO week ids.
        /// Must NOT be reused for new weekly earn call sites (Claude Task 2 / Block E).</summary>
        public const string DevTrustedWeekKeyPlaceholder = "dev-permit-week-v1";

        /// <summary>
        /// CC-rotated Option C stopgap week key for ManualTrustedWeekKey weekly claims.
        /// NEVER reuse <see cref="DevTrustedWeekKeyPlaceholder"/>. Replace with server ISO week later.
        /// </summary>
        public const string ManualTrustedWeekKey = "cc-permit-week-2026-08-23";

        /// <summary>Weekly faucet path: trusted week key + 8/wk + 16 hoard (current constants).</summary>
        public static int TryGrantWeekly(PlayerProfile profile, int requested, string trustedWeekKey)
        {
            if (profile == null || requested <= 0) return 0;
            if (string.IsNullOrEmpty(trustedWeekKey)) return 0;

            CollectionSchemaMigration.NormalizeCollectionFields(profile);

            if (profile.ascensionPermitWeekKey != trustedWeekKey)
            {
                profile.ascensionPermitWeekKey = trustedWeekKey;
                profile.ascensionPermitsEarnedThisWeek = 0;
            }

            int weeklyRemaining = CollectionSchemaRules.AscensionPermitsPerTrustedWeek
                - profile.ascensionPermitsEarnedThisWeek;
            if (weeklyRemaining <= 0) return 0;

            int hoardRemaining = CollectionSchemaRules.AscensionPermitHoardCap
                - profile.ascensionPermitBalance;
            if (hoardRemaining <= 0) return 0;

            int granted = requested;
            if (granted > weeklyRemaining) granted = weeklyRemaining;
            if (granted > hoardRemaining) granted = hoardRemaining;

            profile.ascensionPermitBalance += granted;
            profile.ascensionPermitsEarnedThisWeek += granted;
            return granted;
        }

        /// <summary>
        /// One-shot / milestone grants (campaign chapter clear, etc.). Hoard-capped only —
        /// never mutates week key or weekly earned counter.
        /// </summary>
        public static int TryGrantMilestone(PlayerProfile profile, int requested)
        {
            if (profile == null || requested <= 0) return 0;

            CollectionSchemaMigration.NormalizeCollectionFields(profile);

            int hoardRemaining = CollectionSchemaRules.AscensionPermitHoardCap
                - profile.ascensionPermitBalance;
            if (hoardRemaining <= 0) return 0;

            int granted = requested;
            if (granted > hoardRemaining) granted = hoardRemaining;

            profile.ascensionPermitBalance += granted;
            return granted;
        }

        /// <summary>Alias for <see cref="TryGrantWeekly"/> (existing call sites / tests).</summary>
        public static int TryGrant(PlayerProfile profile, int requested, string trustedWeekKey) =>
            TryGrantWeekly(profile, requested, trustedWeekKey);

        public static bool TrySpendOne(PlayerProfile profile)
        {
            if (profile == null || profile.ascensionPermitBalance < 1) return false;
            profile.ascensionPermitBalance -= 1;
            return true;
        }
    }
}

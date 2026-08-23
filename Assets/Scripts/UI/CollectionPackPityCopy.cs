using MyriadOfDragons.Save;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Shop-facing High-draw pity progress. Counters and caps come from profile +
    /// <see cref="CollectionSchemaRules"/> (same 10 / 60 that force PITY SAVE on High pulls).
    /// </summary>
    public static class CollectionPackPityCopy
    {
        /// <summary>Shop banner — account High-pity progress toward PITY SAVE thresholds.</summary>
        public static string FormatShopBanner(PlayerProfile profile)
        {
            int toward5 = ProgressToward(
                profile?.highPityMissesSince5Star ?? 0,
                CollectionSchemaRules.HighPityMissesFor5StarPlus);
            int toward7 = ProgressToward(
                profile?.highPityMissesSince7Star ?? 0,
                CollectionSchemaRules.HighPityMissesFor7Star);

            return
                $"High pity · {toward5}/{CollectionSchemaRules.HighPityMissesFor5StarPlus} pulls to 5★+ PITY SAVE · " +
                $"{toward7}/{CollectionSchemaRules.HighPityMissesFor7Star} pulls to 7★ PITY SAVE";
        }

        /// <summary>Per-tile line for packs that include High draws.</summary>
        public static string FormatPackTileLine(PlayerProfile profile)
        {
            int toward5 = ProgressToward(
                profile?.highPityMissesSince5Star ?? 0,
                CollectionSchemaRules.HighPityMissesFor5StarPlus);
            int toward7 = ProgressToward(
                profile?.highPityMissesSince7Star ?? 0,
                CollectionSchemaRules.HighPityMissesFor7Star);

            return
                $"{toward5}/{CollectionSchemaRules.HighPityMissesFor5StarPlus} to 5★+ · " +
                $"{toward7}/{CollectionSchemaRules.HighPityMissesFor7Star} to 7★";
        }

        private static int ProgressToward(int misses, int threshold)
        {
            if (threshold <= 0) return 0;
            if (misses < 0) return 0;
            return misses > threshold ? threshold : misses;
        }
    }
}

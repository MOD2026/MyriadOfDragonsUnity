using System;
using System.Collections.Generic;

namespace MyriadOfDragons.Save
{
    public readonly struct CollectionPackSku
    {
        public string SkuId { get; }
        public int GemCost { get; }
        public int NormalDrawCount { get; }
        public int HighDrawCount { get; }
        /// <summary>0 = no bundle floor; otherwise whole receipt must include at least one card at this rarity.</summary>
        public int FloorMinRarity { get; }

        public int TotalDrawCount => NormalDrawCount + HighDrawCount;

        public float GemsPerCard => GemCost / (float)TotalDrawCount;

        public CollectionPackSku(
            string skuId,
            int gemCost,
            int normalDrawCount,
            int highDrawCount,
            int floorMinRarity)
        {
            SkuId = skuId;
            GemCost = gemCost;
            NormalDrawCount = normalDrawCount;
            HighDrawCount = highDrawCount;
            FloorMinRarity = floorMinRarity;
        }
    }

    /// <summary>Locked Shop V2 gem pack rows (SHOP_V2_NUMBERS_PACKET / COLLECTION_PACK_RECEIPT_v1).</summary>
    public static class CollectionPackCatalog
    {
        public const string SingleSigilSkuId = "pack_single_sigil";
        public const string ScoutCacheSkuId = "pack_scout_cache";
        public const string WarbandCacheSkuId = "pack_warband_cache";
        public const string LegionCacheSkuId = "pack_legion_cache";

        private static readonly CollectionPackSku[] Skus =
        {
            new CollectionPackSku(SingleSigilSkuId, 150, 1, 0, 0),
            new CollectionPackSku(ScoutCacheSkuId, 800, 4, 1, 2),
            new CollectionPackSku(WarbandCacheSkuId, 1650, 7, 2, 3),
            new CollectionPackSku(LegionCacheSkuId, 4000, 16, 4, 5),
        };

        public static IReadOnlyList<CollectionPackSku> AllSkus => Skus;

        public static bool TryGetSku(string skuId, out CollectionPackSku sku)
        {
            if (!string.IsNullOrEmpty(skuId))
            {
                foreach (CollectionPackSku row in Skus)
                {
                    if (string.Equals(row.SkuId, skuId, StringComparison.Ordinal))
                    {
                        sku = row;
                        return true;
                    }
                }
            }

            sku = default;
            return false;
        }

        /// <summary>Inverse gem/card ordering: 150 &lt; 160 &lt; 183.3 &lt; 200.</summary>
        public static bool HasInverseGemPerCardOrdering()
        {
            float previous = float.MinValue;
            foreach (CollectionPackSku row in Skus)
            {
                float rate = row.GemsPerCard;
                if (rate <= previous) return false;
                previous = rate;
            }

            return true;
        }
    }
}

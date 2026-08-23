using System.Collections.Generic;

namespace MyriadOfDragons.Save
{
    public enum PackReceiptError
    {
        None,
        RequiresMigration,
        InvalidSku,
        InsufficientGems,
        EmptyPool,
        SaveFailed,
    }

    public enum PackDrawKind
    {
        Normal,
        High,
    }

    public struct ResolvedPackDraw
    {
        public string CardId;
        public int Rarity;
        public PackDrawKind DrawKind;
        public bool WasPityForced;
        public bool WasFloorReroll;
    }

    public sealed class PackReceiptResult
    {
        public bool Success;
        public PackReceiptError Error;
        public string ReceiptId;
        public string SkuId;
        public int GemsSpent;
        public int GemsBefore;
        public int GemsAfter;
        public int NormalPityBefore;
        public int NormalPityAfter;
        public int HighPityBefore5Star;
        public int HighPityAfter5Star;
        public int HighPityBefore7Star;
        public int HighPityAfter7Star;
        /// <summary>L9 telemetry: floor rarity applied when bundle floor re-roll fired; 0 if none.</summary>
        public int FloorTriggeredRarity;
        public List<ResolvedPackDraw> Draws = new List<ResolvedPackDraw>();
    }
}

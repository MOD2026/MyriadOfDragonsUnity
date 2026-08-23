namespace MyriadOfDragons.Save
{
    public enum CollectionBurnPath
    {
        TrainingXp,
        GenericSacrifice,
        ForgeCredit,
        Dust,
    }

    public enum CollectionBurnError
    {
        None,
        RequiresMigration,
        InvalidCard,
        InsufficientCopies,
        SaveFailed,
    }

    public sealed class CollectionBurnReceiptResult
    {
        public bool Success;
        public CollectionBurnError Error;
        public string ReceiptId;
        public string CardId;
        public CollectionBurnPath Path;
        public int Rarity;
        public int CopiesBefore;
        public int CopiesAfter;
        public int YieldAmount;
        public int GoldBefore;
        public int GoldAfter;
    }

    public enum CollectionEvolutionError
    {
        None,
        RequiresMigration,
        InvalidCard,
        InsufficientCopies,
        InsufficientGold,
        InsufficientSacrificeCredits,
        PermitRequired,
        MaxStepReached,
        SaveFailed,
    }

    public sealed class CollectionEvolutionReceiptResult
    {
        public bool Success;
        public CollectionEvolutionError Error;
        public string ReceiptId;
        public string CardId;
        public int Rarity;
        public int GoldSpent;
        public int ForgeCreditsSpent;
        public int DustSpent;
        public int SacrificeCreditsSpent;
        public bool PermitSpent;
        public int EvolutionStepBefore;
        public int EvolutionStepAfter;
        public int CopiesBefore;
        public int CopiesAfter;
    }

}

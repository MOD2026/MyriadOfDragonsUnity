namespace MyriadOfDragons.Save
{
    /// <summary>Locked Collection V1 constants (COLLECTION_SCHEMA_PROPOSAL_v1 / OWNER_REVIEW_LOG).</summary>
    public static class CollectionSchemaRules
    {
        public const int CurrentCollectionSchemaVersion = 1;

        public const int AscensionPermitHoardCap = 8;
        /// <summary>Owner/CC lock 2026-08-23: weekly earn is 4/week (was 8). Tests assert via this constant.</summary>
        public const int AscensionPermitsPerTrustedWeek = 4;

        /// <summary>Bible lock: collection card level hard cap (release blocker).</summary>
        public const int MaxCardLevel = 100;

        public const int HighPityMissesFor5StarPlus = 10;
        public const int HighPityMissesFor7Star = 60;

        public const float ForgeCreditMaxRecipeFraction = 0.20f;
        public const float DustMaxRecipeFraction = 0.10f;

        /// <summary>Burn packet: generic sacrifice satisfies the early evolution recipe portion only (step 0→1).</summary>
        public const int GenericSacrificeCreditsFirstStep = 1;
    }
}

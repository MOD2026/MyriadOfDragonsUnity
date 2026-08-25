namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Locked Campaign Gem grants (OWNER_REVIEW_LOG "Campaign Gem recompute", 2026-08-23; Ch11
    /// extension 2026-08-25). Gold stays formula-driven in <see cref="CampaignMapPresenter"/>;
    /// Gems are milestone-weighted only. Finales share Ascension Permit chapter milestone ids.
    /// </summary>
    public static class CampaignGemRewardRules
    {
        public const int RegularStageGems = 8;
        public const int ChapterFinaleGems = 440;

        /// <summary>263 regular × 8 + 10 finales × 440 (Ch1–10 spine only).</summary>
        public const int LockedTotalCh1Through10 = 6504;

        /// <summary>292 regular × 8 + 11 finales × 440 (Ch1–11 after Chapter 11 depth fill).</summary>
        public const int LockedTotalCh1Through11 = 7176;

        public static int ForStage(string stageId) =>
            HomePagePresenter.IsChapterFinalePermitStage(stageId)
                ? ChapterFinaleGems
                : RegularStageGems;
    }
}

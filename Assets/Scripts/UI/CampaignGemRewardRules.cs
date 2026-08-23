namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Locked Campaign Ch1–10 Gem grants (OWNER_REVIEW_LOG "Campaign Gem recompute", 2026-08-23).
    /// Gold stays formula-driven in <see cref="CampaignMapPresenter"/>; Gems are milestone-weighted only.
    /// Finales share the same stage ids as Ascension Permit chapter milestones.
    /// </summary>
    public static class CampaignGemRewardRules
    {
        public const int RegularStageGems = 8;
        public const int ChapterFinaleGems = 440;

        /// <summary>263 regular × 8 + 10 finales × 440.</summary>
        public const int LockedTotalCh1Through10 = 6504;

        public static int ForStage(string stageId) =>
            HomePagePresenter.IsChapterFinalePermitStage(stageId)
                ? ChapterFinaleGems
                : RegularStageGems;
    }
}

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

        /// <summary>321 regular × 8 + 12 finales × 440 (Ch1–12 after Chapter 12 depth fill).</summary>
        public const int LockedTotalCh1Through12 = 7848;

        /// <summary>350 regular × 8 + 13 finales × 440 (Ch1–13 after Chapter 13 depth fill).</summary>
        public const int LockedTotalCh1Through13 = 8520;

        /// <summary>379 regular × 8 + 14 finales × 440 (Ch1–14 after Chapter 14 depth fill).</summary>
        public const int LockedTotalCh1Through14 = 9192;

        /// <summary>408 regular × 8 + 15 finales × 440 (Ch1–15 after Chapter 15 depth fill).</summary>
        public const int LockedTotalCh1Through15 = 9864;

        /// <summary>437 regular × 8 + 16 finales × 440 (Ch1–16 after Chapter 16 depth fill).</summary>
        public const int LockedTotalCh1Through16 = 10536;

        /// <summary>466 regular × 8 + 17 finales × 440 (Ch1–17 after Chapter 17 depth fill).</summary>
        public const int LockedTotalCh1Through17 = 11208;

        /// <summary>495 regular × 8 + 18 finales × 440 (Ch1–18 after Chapter 18 depth fill).</summary>
        public const int LockedTotalCh1Through18 = 11880;

        public static int ForStage(string stageId) =>
            HomePagePresenter.IsChapterFinalePermitStage(stageId)
                ? ChapterFinaleGems
                : RegularStageGems;
    }
}

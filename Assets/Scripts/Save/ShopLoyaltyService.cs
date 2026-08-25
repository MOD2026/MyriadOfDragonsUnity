using System;

namespace MyriadOfDragons.Save
{
    /// <summary>Outcome of a loyalty accrual, so a caller can report it rather than guess.</summary>
    public struct ShopLoyaltyResult
    {
        public bool Accrued;
        public int PointsAdded;
        public int TotalAfter;
        public string Message;
    }

    /// <summary>
    /// Shop Loyalty accrual - the EARN/TRACK half only.
    ///
    /// Spec: docs/Shop_V1_Release_Contract.md:50 - "Loyalty/reward track - needs a
    /// `int shopMilestoneProgress` (or similar) field and a milestone table."
    ///
    /// REDEMPTION IS NOT BUILT, AND CANNOT BE. That line names a milestone table as a requirement
    /// and no such table exists anywhere in the contract or the repo - no thresholds, no rewards,
    /// no claim rules. Building a redemption path would mean inventing the reward economy, which is
    /// a design pass, not an implementation detail. The counter is real and persists now; what a
    /// player gets for reaching 50 points is still unwritten.
    ///
    /// POINTS-PER-PURCHASE vs POINTS-PER-SPEND IS DELIBERATELY NOT DECIDED HERE. The contract lists
    /// "no streak counter, no cumulative-spend counter, no milestone list" as all absent, so it does
    /// not say which shape loyalty takes - and the difference is an economy decision, not a
    /// rounding one: fifty small purchases and one large one are worlds apart under the two models.
    /// So the CALLER supplies the points, and this service only accrues them. Whichever model the
    /// design picks, nothing here changes.
    ///
    /// Real logic in a plain testable class per CLAUDE.md non-negotiable #6 - no MonoBehaviour, and
    /// it never saves. The caller decides when to persist, exactly like
    /// TacticalPuzzleSlate.WriteProgress, so a test can assert the accrual without touching disk.
    /// </summary>
    public static class ShopLoyaltyService
    {
        /// <summary>
        /// Adds loyalty points for a completed purchase. Does NOT save - the caller persists.
        ///
        /// Refuses non-positive amounts rather than silently accepting them: a zero-point purchase
        /// is a caller bug, and a negative one would quietly drain a balance that is meant to be
        /// monotonic.
        /// </summary>
        public static ShopLoyaltyResult Accrue(PlayerProfile profile, int points)
        {
            var result = new ShopLoyaltyResult();
            if (profile == null)
            {
                result.Message = "No profile.";
                return result;
            }

            if (points <= 0)
            {
                result.TotalAfter = Math.Max(0, profile.shopMilestoneProgress);
                result.Message = "Loyalty points must be positive.";
                return result;
            }

            // Floor the existing value before adding: a corrupted or hand-edited save could hold a
            // negative, and this field is never allowed to go backwards. Matches the AtLeastZero
            // treatment every other int gets in SaveMigration - applied here because this service
            // cannot edit that frozen file itself.
            int before = Math.Max(0, profile.shopMilestoneProgress);
            profile.shopMilestoneProgress = before + points;

            result.Accrued = true;
            result.PointsAdded = points;
            result.TotalAfter = profile.shopMilestoneProgress;
            result.Message = $"Earned {points} loyalty point(s).";
            return result;
        }

        /// <summary>Current progress, floored. Reads never trust a stored negative either.</summary>
        public static int ProgressOf(PlayerProfile profile) =>
            profile == null ? 0 : Math.Max(0, profile.shopMilestoneProgress);

        /// <summary>
        /// True once a milestone table exists to redeem against. Hardcoded false, deliberately:
        /// the contract requires a milestone table and does not contain one, so any redemption UI
        /// asking this question gets an honest "not yet" instead of a fabricated reward.
        /// </summary>
        public static bool RedemptionAvailable => false;
    }
}

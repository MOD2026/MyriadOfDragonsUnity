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
    /// UPDATED 2026-08-26: both questions this file originally left open are now answered by the
    /// register entry "Loyalty Points, redemption side now specified", and this header was rewritten
    /// rather than left describing a state that no longer holds.
    ///   - The earn model is PER SPEND: 1 point per 10 Gems, rounded down per transaction.
    ///   - The milestone table is locked and lives here as data (see Milestones).
    ///
    /// REDEMPTION IS STILL NOT BUILT, but for a narrower reason than before. The blocker is no
    /// longer a missing table - it is that these milestones are ONE-TIME and nothing on the profile
    /// records which have been claimed, plus two reward types the save cannot represent at all (no
    /// cosmetic ownership model exists, and "3-day VIP" is not expressible in the weekly|fortnight|
    /// monthly plan vocabulary). See RedemptionAvailable.
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
        /// Points earned for a Gem spend: 1 per 10 Gems, ROUNDED DOWN PER TRANSACTION.
        ///
        /// Locked 2026-08-26 (register: "Loyalty Points, redemption side now specified"). This also
        /// settled the points-per-purchase vs points-per-spend question I had left open - it is
        /// per SPEND, and per transaction, which matters: ten 9-Gem purchases earn 0 points, while
        /// one 90-Gem purchase earns 9. Rounding per transaction rather than on a running total is
        /// the locked rule, not an implementation shortcut.
        ///
        /// Gems only. The spec says "per 10 Gems spent" - Gold spending earns nothing, and free or
        /// refunded Gems must not be passed here at all (the caller owns that filter, since only it
        /// knows whether a transaction was a real spend).
        /// </summary>
        public const int GemsPerLoyaltyPoint = 10;

        public static int PointsForGemsSpent(int gemsSpent) =>
            gemsSpent <= 0 ? 0 : gemsSpent / GemsPerLoyaltyPoint;

        /// <summary>One locked milestone. Rewards are deliberately a pure recognition/convenience
        /// sink - no cards, packs, Forge Dust, Permits, Evolution materials, Market Credits, spell
        /// ownership, combat stats or timer skips anywhere in the list, so loyalty can never become
        /// a second acquisition path (the exact mistake VIP's original wording made).</summary>
        public readonly struct LoyaltyMilestone
        {
            public readonly int Points;
            public readonly string Reward;

            public LoyaltyMilestone(int points, string reward)
            {
                Points = points;
                Reward = reward;
            }
        }

        /// <summary>The locked one-time milestone ladder, ascending. Data only - see
        /// RedemptionAvailable for why nothing claims against it yet.</summary>
        public static readonly LoyaltyMilestone[] Milestones =
        {
            new LoyaltyMilestone(100, "1 Stamina claim (counts against the existing 4/24h cap)"),
            new LoyaltyMilestone(250, "3-day VIP voucher"),
            new LoyaltyMilestone(500, "Cosmetic badge/frame (existing catalog only)"),
            new LoyaltyMilestone(1000, "7-day VIP voucher"),
            new LoyaltyMilestone(2000, "Cosmetic badge/frame (existing catalog only)"),
            new LoyaltyMilestone(4000, "30-day VIP voucher"),
            new LoyaltyMilestone(8000, "Premium cosmetic frame"),
        };

        /// <summary>Highest milestone the player's progress has REACHED, or -1. Reaching is not
        /// claiming - see RedemptionAvailable.</summary>
        public static int HighestMilestoneReachedIndex(PlayerProfile profile)
        {
            int progress = ProgressOf(profile);
            int index = -1;
            for (int i = 0; i < Milestones.Length; i++)
            {
                if (progress >= Milestones[i].Points) index = i;
            }

            return index;
        }

        /// <summary>
        /// Still false, and now for a DIFFERENT and more specific reason than before.
        ///
        /// The milestone table exists as of 2026-08-26, so the earlier blocker is gone. What is
        /// missing is the ability to CLAIM: these are ONE-TIME milestones and PlayerProfile has no
        /// field recording which have been taken, so a claim could be repeated indefinitely.
        ///
        /// Two of the seven reward types also have nowhere to land. There is no cosmetic ownership
        /// model on the profile at all (3 of 7 milestones award cosmetics), and the VIP plan
        /// vocabulary is weekly|fortnight|monthly - a "3-day voucher" is not expressible in it.
        /// Granting a reward the save cannot represent is worse than not granting it.
        /// </summary>
        public static bool RedemptionAvailable => false;
    }
}

using System;
using MyriadOfDragons.Economy;

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

    /// <summary>Outcome of a milestone claim. Carries the partial cases explicitly - a caller that
    /// only checks Claimed would otherwise report "you got 8 Stamina" when the 24h cap let 2
    /// through.</summary>
    public struct ShopLoyaltyClaimResult
    {
        public bool Claimed;
        public int MilestonePoints;
        public int GoldGranted;
        public int StaminaClaimsApplied;
        public int StaminaClaimsForfeited;
        public int StaminaClaimsDeferred;
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
    /// UPDATED AGAIN 2026-08-26: REDEMPTION IS NOW BUILT, in the two halves that are actually
    /// decided. The claim guard exists (PlayerProfile.highestClaimedLoyaltyMilestone, owner-signed
    /// off), and the Gold and Stamina-claim rewards grant for real. Two things are still gated and
    /// say so in code rather than being silently absent:
    ///   - VIP VOUCHER DURATIONS ARE HELD. The two locks disagree: 250 was remapped to weekly (7d)
    ///     and 1,000 to fortnight (14d) to retire the unexpressible "3-day", while the revised
    ///     whale-tier lock puts a 7-day voucher at 2,000 - below the 1,000 rung, so the ladder
    ///     would stop ascending. Repairing it means raising 2,000 to 30-day, a real increase in
    ///     what paid spend returns, which is an owner call. See VoucherGrantsHeld.
    ///   - MILESTONE 500 IS A COSMETIC and no cosmetic ownership model exists on the profile.
    /// Granting a reward the save cannot represent is still worse than not granting it, so both
    /// stay refused with a reason string instead of a silent no-op.
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
            new LoyaltyMilestone(250, "Weekly (7-day) VIP voucher"),
            new LoyaltyMilestone(500, "Cosmetic badge/frame (existing catalog only)"),
            new LoyaltyMilestone(1000, "Fortnight (14-day) VIP voucher"),
            new LoyaltyMilestone(2000, "25,000 Gold + 2 Stamina claims + VIP voucher"),
            new LoyaltyMilestone(4000, "50,000 Gold + 4 Stamina claims + VIP voucher"),
            new LoyaltyMilestone(8000, "100,000 Gold + 8 Stamina claims + VIP voucher"),
        };

        /// <summary>Gold granted by a milestone, 0 for the rungs that grant none. Kept beside the
        /// table rather than parsed out of the display string, so a copy edit can never change what
        /// a player is actually paid.</summary>
        public static int GoldRewardFor(int milestonePoints)
        {
            switch (milestonePoints)
            {
                case 2000: return 25000;
                case 4000: return 50000;
                case 8000: return 100000;
                default: return 0;
            }
        }

        /// <summary>Stamina claims granted by a milestone. Every one is still subject to the real
        /// 4-per-24h Shop refill cap - a milestone may never bypass it, so a grant can come back
        /// partially applied.</summary>
        public static int StaminaClaimsFor(int milestonePoints)
        {
            switch (milestonePoints)
            {
                case 100: return 1;
                case 2000: return 2;
                case 4000: return 4;
                case 8000: return 8;
                default: return 0;
            }
        }

        /// <summary>True for rungs whose reward includes a VIP voucher whose duration is not yet
        /// locked. See the header - this is the ascending-ladder conflict, not an oversight.</summary>
        public static bool VoucherGrantsHeld(int milestonePoints) =>
            milestonePoints == 250 || milestonePoints == 1000 ||
            milestonePoints == 2000 || milestonePoints == 4000 || milestonePoints == 8000;

        /// <summary>True for rungs awarding a cosmetic. No cosmetic ownership model exists on
        /// PlayerProfile, so these cannot be claimed yet.</summary>
        public static bool CosmeticGrantsUnsupported(int milestonePoints) => milestonePoints == 500;

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

        /// <summary>Redemption exists now that the claim guard does. Individual rewards can
        /// still refuse - see VoucherGrantsHeld and CosmeticGrantsUnsupported.</summary>
        public static bool RedemptionAvailable => true;

        /// <summary>Highest milestone POINTS value already claimed. Floored, for the same
        /// corrupted-save reason ProgressOf floors.</summary>
        public static int HighestClaimedOf(PlayerProfile profile) =>
            profile == null ? 0 : Math.Max(0, profile.highestClaimedLoyaltyMilestone);

        /// <summary>
        /// The next milestone the player may claim, or -1 if there is none.
        ///
        /// Deliberately the LOWEST unclaimed rung that has been reached, never the highest. The
        /// guard stores a threshold rather than a set, so claiming a high rung marks every lower
        /// rung claimed - letting a caller pick would let a player silently destroy the rewards
        /// underneath the one they picked.
        /// </summary>
        public static int NextClaimableMilestone(PlayerProfile profile)
        {
            int progress = ProgressOf(profile);
            int claimed = HighestClaimedOf(profile);
            for (int i = 0; i < Milestones.Length; i++)
            {
                int points = Milestones[i].Points;
                if (points > claimed && progress >= points) return points;
            }

            return -1;
        }

        /// <summary>
        /// Claims the next eligible milestone. Does NOT save - the caller persists, same contract
        /// as Accrue.
        ///
        /// Refuses rather than partially advancing when a reward cannot be represented: a held
        /// voucher or an unsupported cosmetic leaves highestClaimedLoyaltyMilestone untouched, so
        /// the reward is still owed once the gate lifts instead of being consumed for nothing.
        /// Stamina is the one reward that CAN come back partial, because the 4-per-24h cap is real
        /// and outranks the milestone - that case still consumes the claim, and says so.
        /// </summary>
        public static ShopLoyaltyClaimResult ClaimNext(PlayerProfile profile, long nowUtcTicks)
        {
            var result = new ShopLoyaltyClaimResult { MilestonePoints = -1 };
            if (profile == null)
            {
                result.Message = "No profile.";
                return result;
            }

            int points = NextClaimableMilestone(profile);
            if (points < 0)
            {
                result.Message = "No milestone is currently claimable.";
                return result;
            }

            result.MilestonePoints = points;

            if (CosmeticGrantsUnsupported(points))
            {
                result.Message =
                    "Milestone " + points + " awards a cosmetic, and no cosmetic ownership model " +
                    "exists on the profile yet. Claim refused so the reward stays owed.";
                return result;
            }

            if (VoucherGrantsHeld(points))
            {
                result.Message =
                    "Milestone " + points + " includes a VIP voucher whose duration is not locked " +
                    "(the 1,000 vs 2,000 ascending-ladder conflict). Claim refused so the reward " +
                    "stays owed.";
                return result;
            }

            int gold = GoldRewardFor(points);
            if (gold > 0)
            {
                CurrencyManager.AddCurrency(profile, CurrencyType.Gold, gold, persist: false);
                result.GoldGranted = gold;
            }

            int claims = StaminaClaimsFor(points);
            for (int i = 0; i < claims; i++)
            {
                ShopStaminaCatalog.RefreshRollingWindow(profile, nowUtcTicks);
                if (profile.staminaShopPurchasesInWindow >= ShopStaminaCatalog.MaxPurchasesPerRollingDay)
                {
                    result.StaminaClaimsDeferred = claims - i;
                    break;
                }

                bool atFull = profile.stamina >= profile.maxStamina && profile.maxStamina > 0;
                if (atFull) result.StaminaClaimsForfeited++;
                else
                {
                    CurrencyManager.RestoreStamina(profile, ShopStaminaCatalog.StaminaGrantPerPotion, persist: false);
                    result.StaminaClaimsApplied++;
                }

                ShopStaminaCatalog.RecordSuccessfulPurchase(profile, nowUtcTicks);
            }

            profile.highestClaimedLoyaltyMilestone = points;
            result.Claimed = true;
            result.Message = "Claimed milestone " + points + ".";
            return result;
        }
    }
}

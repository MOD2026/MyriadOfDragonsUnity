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

    /// <summary>Result of delivering queued Loyalty Stamina claims. Applied/Forfeited/StillPending
    /// are reported separately because "you got 8" would be false in every partial case.</summary>
    public struct ShopLoyaltyStaminaDelivery
    {
        public int Applied;
        public int Forfeited;
        public int StillPending;
    }

    /// <summary>Outcome of a milestone claim. Carries the partial cases explicitly - a caller that
    /// only checks Claimed would otherwise report "you got 8 Stamina" when the 24h cap let 2
    /// through.</summary>
    public struct ShopLoyaltyClaimResult
    {
        public bool Claimed;
        public int MilestonePoints;
        public int GoldGranted;

        /// <summary>Construction Materials granted by this claim.</summary>
        public int MaterialsGranted;
        public int StaminaClaimsApplied;
        public int StaminaClaimsForfeited;
        public int StaminaClaimsDeferred;

        /// <summary>Plan id of a VIP voucher granted LIVE by this claim, or empty.</summary>
        public string VoucherPlanGranted;

        /// <summary>Plan id of a voucher QUEUED because a subscription was already active. The
        /// milestone still claimed fully.</summary>
        public string VoucherPlanQueued;

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
    ///   - VIP VOUCHERS NOW GRANT FOR REAL (locked 2026-08-26): 250=7d, 1,000=14d,
    ///     2,000/4,000/8,000=30d. The earlier hold - which refused every voucher rung while the
    ///     2,000-point duration was undecided - is resolved, and its gate has been REMOVED rather
    ///     than left permanently false, because a dead gate reads as a live constraint.
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
            new LoyaltyMilestone(250, "Weekly (7-day) VIP voucher + 50 Materials"),
            new LoyaltyMilestone(500, "5,000 Gold + 100 Materials + 1 Stamina claim"),
            new LoyaltyMilestone(1000, "Fortnight (14-day) VIP voucher"),
            new LoyaltyMilestone(2000, "25,000 Gold + 250 Materials + 2 Stamina claims + VIP voucher"),
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
                case 500: return 5000;      // BS-locked 2026-08-26, replacing the cosmetic
                case 2000: return 25000;
                case 4000: return 50000;
                case 8000: return 100000;
                default: return 0;
            }
        }

        /// <summary>
        /// Construction Materials granted by a milestone.
        ///
        /// REPLACES the Avatar XP stopgap (BS option (b), 2026-08-26). XP was reported as "owed"
        /// because PlayerProfile has no XP field and nothing consumed it - granting would have
        /// written to a void. Materials has a real field (constructionMaterials) and a real sink,
        /// so this grants for real instead of reporting an IOU.
        /// </summary>
        public static int MaterialsRewardFor(int milestonePoints)
        {
            switch (milestonePoints)
            {
                case 250: return 50;
                case 500: return 100;
                case 2000: return 250;
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
                case 500: return 1;         // BS-locked 2026-08-26
                case 2000: return 2;
                case 4000: return 4;
                case 8000: return 8;
                default: return 0;
            }
        }

        /// <summary>
        /// VIP voucher plan id for a milestone, or empty when that rung grants no voucher.
        ///
        /// LOCKED 2026-08-26 (BS, benchmarked against Genshin's Welkin Moon as a real fixed 30-day
        /// unit): 250=weekly(7d), 1,000=fortnight(14d), 2,000/4,000/8,000=monthly(30d). Monotone
        /// non-decreasing, then deliberately plateaus at 30 - shipped games use 30 days as a
        /// ceiling unit rather than an unbounded linear duration curve.
        ///
        /// REPLACES VoucherGrantsHeld, which refused every voucher rung while the 2,000-point
        /// duration was undecided. That hold is resolved, so the gate is REMOVED rather than left
        /// permanently false - a dead gate reads as a live constraint to the next person.
        ///
        /// Ids come from VipSubscriptionOpenValues.PlanIds, never string literals, so this cannot
        /// drift from the real plan vocabulary.
        /// </summary>
        public static string VoucherPlanIdFor(int milestonePoints)
        {
            switch (milestonePoints)
            {
                case 250: return MyriadOfDragons.Metagame.VipSubscriptionOpenValues.PlanIds[0];
                case 1000: return MyriadOfDragons.Metagame.VipSubscriptionOpenValues.PlanIds[1];
                case 2000:
                case 4000:
                case 8000: return MyriadOfDragons.Metagame.VipSubscriptionOpenValues.PlanIds[2];
                default: return string.Empty;
            }
        }

        public static bool GrantsVoucher(int milestonePoints) =>
            !string.IsNullOrEmpty(VoucherPlanIdFor(milestonePoints));

        /// <summary>
        /// Puts a voucher live. The activation clock starts NOW, never when it was earned - that is
        /// what makes a queued voucher a deferred entitlement rather than banked active time, and
        /// is why this does not conflict with the "no banking past duration" rule.
        /// </summary>
        private static void ActivateVoucher(PlayerProfile profile, string planId, long nowTicks)
        {
            int planIndex = PlanIndexOf(planId);
            profile.vipPlanId = planId;
            profile.vipStartedUtcTicks = nowTicks;
            profile.vipExpiresUtcTicks = nowTicks + System.TimeSpan.FromDays(
                MyriadOfDragons.Metagame.VipSubscriptionOpenValues.PlanDurationDays[planIndex]).Ticks;
            profile.vipClaimsConsumed = 0;
        }

        /// <summary>
        /// Activates the next queued voucher if - and only if - no subscription is currently live.
        ///
        /// ONE AT A TIME, FIFO, never while something is active: activating two at once, or
        /// extending a running subscription, is exactly the stacking the locked rules forbid.
        /// Cheap no-op when nothing is due, so it is safe to call wherever a lapse could have
        /// happened. Does NOT save - the caller persists, same contract as Accrue and ClaimNext.
        /// </summary>
        public static string ActivateNextPendingVoucher(PlayerProfile profile, long nowTicks)
        {
            if (profile?.pendingLoyaltyVipVoucherIds == null) return string.Empty;
            if (profile.pendingLoyaltyVipVoucherIds.Count == 0) return string.Empty;
            if (MyriadOfDragons.Metagame.VipSubscriptionOpenValues.IsSubscriptionActive(profile, nowTicks))
                return string.Empty;

            string next = profile.pendingLoyaltyVipVoucherIds[0];
            profile.pendingLoyaltyVipVoucherIds.RemoveAt(0);
            ActivateVoucher(profile, next, nowTicks);
            return next;
        }

        /// <summary>
        /// Delivers queued Loyalty Stamina claims, up to whatever the real 4-per-24h window still
        /// allows.
        ///
        /// Routes through the SAME cap path a Shop purchase uses, so a queued claim can never be a
        /// way around the ceiling - it only stops the entitlement being destroyed by it. A claim
        /// arriving while Stamina is already full is CONSUMED, matching how VIP claims behave
        /// (ProcessDueClaims forfeits at full) - queuing forever would let a player hoard
        /// entitlement indefinitely and dump it all at once.
        ///
        /// Does NOT save - the caller persists, same contract as the rest of this service.
        /// </summary>
        public static ShopLoyaltyStaminaDelivery DeliverPendingStaminaClaims(
            PlayerProfile profile, long nowUtcTicks)
        {
            var delivery = new ShopLoyaltyStaminaDelivery();
            if (profile == null) return delivery;

            // Floor on READ AND WRITE, not just read. The first version computed the floor into a
            // local and returned early without persisting it, so a corrupted negative survived
            // every delivery call untouched - my own test caught it. This matches the AtLeastZero
            // discipline every other int here already follows.
            int pending = Math.Max(0, profile.pendingLoyaltyStaminaClaims);
            profile.pendingLoyaltyStaminaClaims = pending;
            if (pending == 0) return delivery;

            while (pending > 0)
            {
                ShopStaminaCatalog.RefreshRollingWindow(profile, nowUtcTicks);
                if (profile.staminaShopPurchasesInWindow >= ShopStaminaCatalog.MaxPurchasesPerRollingDay)
                    break;

                bool atFull = profile.stamina >= profile.maxStamina && profile.maxStamina > 0;
                if (atFull) delivery.Forfeited++;
                else
                {
                    CurrencyManager.RestoreStamina(profile, ShopStaminaCatalog.StaminaGrantPerPotion, persist: false);
                    delivery.Applied++;
                }

                ShopStaminaCatalog.RecordSuccessfulPurchase(profile, nowUtcTicks);
                pending--;
            }

            profile.pendingLoyaltyStaminaClaims = pending;
            delivery.StillPending = pending;
            return delivery;
        }

        /// <summary>Vouchers earned and still waiting. Floored against a null list from an old save.</summary>
        public static int PendingVoucherCount(PlayerProfile profile) =>
            profile?.pendingLoyaltyVipVoucherIds?.Count ?? 0;

        private static int PlanIndexOf(string planId)
        {
            string[] ids = MyriadOfDragons.Metagame.VipSubscriptionOpenValues.PlanIds;
            for (int i = 0; i < ids.Length; i++)
                if (string.Equals(ids[i], planId, System.StringComparison.Ordinal)) return i;
            return 0;
        }

        /// <summary>
        /// No rung awards a cosmetic any more.
        ///
        /// Milestone 500 was the only one, and it was unclaimable because PlayerProfile has no
        /// cosmetic ownership model - which blocked the whole ascending ladder behind it. BS
        /// replaced the cosmetic with concrete rewards on 2026-08-26 rather than building an
        /// ownership model for a single rung, so the blocker is gone by removal, not by workaround.
        ///
        /// Kept as a function returning false rather than deleted: callers and tests still ask the
        /// question, and a permanently-false predicate documents that the answer is now "none"
        /// instead of leaving readers to infer it from an absence.
        /// </summary>
        public static bool CosmeticGrantsUnsupported(int milestonePoints) => false;

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
        /// still refuse - see CosmeticGrantsUnsupported, and the no-stacking rule in ClaimNext.</summary>
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

            string voucherPlan = VoucherPlanIdFor(points);
            if (!string.IsNullOrEmpty(voucherPlan))
            {
                long nowTicks = MyriadOfDragons.Metagame.VipSubscriptionOpenValues.NowUtcTicks();

                // DEFERRED ENTITLEMENT (locked 2026-08-26, BS). The milestone claims FULLY either
                // way - the Gold and Stamina grants below run regardless. Only the VOUCHER portion
                // queues when a subscription is already active.
                //
                // This replaces an all-or-nothing refusal that measured as a total lockout: claims
                // are strictly ascending and a whale is effectively always subscribed, so the
                // 250-point rung refused forever and blocked every rung behind it - the six-month
                // sim measured LoyaltyGoldClaimed == 0 for a whale. The refusal was individually
                // correct and collectively catastrophic.
                if (MyriadOfDragons.Metagame.VipSubscriptionOpenValues.IsSubscriptionActive(profile, nowTicks))
                {
                    if (profile.pendingLoyaltyVipVoucherIds == null)
                        profile.pendingLoyaltyVipVoucherIds = new System.Collections.Generic.List<string>();

                    profile.pendingLoyaltyVipVoucherIds.Add(voucherPlan);
                    result.VoucherPlanQueued = voucherPlan;
                }
                else
                {
                    ActivateVoucher(profile, voucherPlan, nowTicks);
                    result.VoucherPlanGranted = voucherPlan;
                }
            }

            int gold = GoldRewardFor(points);
            if (gold > 0)
            {
                CurrencyManager.AddCurrency(profile, CurrencyType.Gold, gold, persist: false);
                result.GoldGranted = gold;
            }

            // Written directly with a floor rather than through CurrencyManager: there is no
            // CurrencyType.Materials (CurrencyDefinitions is Gold/Gems/EventMedal/GuildContribution/
            // DragonRelic), and constructionMaterials is not a tradeable currency. This is the same
            // grant path DailyLoginQuestsService and EmpireExpeditionClearTransaction already use.
            int materials = MaterialsRewardFor(points);
            if (materials > 0)
            {
                profile.constructionMaterials = Math.Max(0, profile.constructionMaterials) + materials;
                result.MaterialsGranted = materials;
            }

            int claims = StaminaClaimsFor(points);
            for (int i = 0; i < claims; i++)
            {
                ShopStaminaCatalog.RefreshRollingWindow(profile, nowUtcTicks);
                if (profile.staminaShopPurchasesInWindow >= ShopStaminaCatalog.MaxPurchasesPerRollingDay)
                {
                    // QUEUE the remainder instead of dropping it. This used to report a "deferred"
                    // count in the result and then discard it - so the 8-claim top rung lost at
                    // least half its value against a 4/24h cap, silently, for exactly the players
                    // who reached it. Same deferred-entitlement shape as the voucher queue.
                    int deferred = claims - i;
                    result.StaminaClaimsDeferred = deferred;
                    profile.pendingLoyaltyStaminaClaims =
                        Math.Max(0, profile.pendingLoyaltyStaminaClaims) + deferred;
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

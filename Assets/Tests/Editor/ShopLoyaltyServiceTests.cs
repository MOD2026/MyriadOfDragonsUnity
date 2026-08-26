using MyriadOfDragons.Save;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Shop Loyalty accrual - the EARN/TRACK half.
    ///
    /// UPDATED 2026-08-26, same correction as the service's own header: the milestone table now
    /// EXISTS (register: "Loyalty Points, redemption side now specified"), so the original reason
    /// given here - "no such table exists" - is no longer true and was rewritten rather than left
    /// standing.
    ///
    /// UPDATED AGAIN 2026-08-26: redemption now EXISTS. The claim guard landed
    /// (PlayerProfile.highestClaimedLoyaltyMilestone, owner-signed-off), so the two tests that used
    /// to assert RedemptionAvailable == false have been rewritten rather than flipped - the old one
    /// said in its own body that rewriting was the correct response once redemption was built.
    ///
    /// What is still gated, and pinned below so neither reads as forgotten work: VIP voucher
    /// DURATIONS are held on an unresolved ascending-ladder conflict, and milestone 500 awards a
    /// cosmetic the save has no ownership model for. Both refuse WITHOUT consuming the claim.
    ///
    /// No save isolation needed: the service never touches disk. The caller persists, exactly like
    /// TacticalPuzzleSlate.WriteProgress, which is what lets these run as plain object tests.
    /// </summary>
    public class ShopLoyaltyServiceTests
    {
        [Test]
        public void AFreshProfile_StartsAtZero()
        {
            Assert.AreEqual(0, ShopLoyaltyService.ProgressOf(new PlayerProfile()),
                "A new player has earned nothing yet.");
        }

        [Test]
        public void APurchase_AccruesItsPoints_AndAccumulatesAcrossPurchases()
        {
            var profile = new PlayerProfile();

            ShopLoyaltyResult first = ShopLoyaltyService.Accrue(profile, 5);
            Assert.IsTrue(first.Accrued);
            Assert.AreEqual(5, first.TotalAfter);

            ShopLoyaltyResult second = ShopLoyaltyService.Accrue(profile, 3);
            Assert.AreEqual(3, second.PointsAdded, "The result reports THIS purchase, not the total.");
            Assert.AreEqual(8, second.TotalAfter, "Progress must accumulate, not overwrite.");
            Assert.AreEqual(8, profile.shopMilestoneProgress, "The profile itself must carry it.");
        }

        [Test]
        public void TheServiceDoesNotSave_SoTheCallerDecidesWhenToPersist()
        {
            // Mirrors TacticalPuzzleSlate.WriteProgress. Keeping persistence out of the accrual is
            // what lets a purchase spend currency and earn loyalty in ONE save rather than two, and
            // it is why these tests need no scratch save directory.
            var profile = new PlayerProfile();
            ShopLoyaltyService.Accrue(profile, 4);

            Assert.AreEqual(4, profile.shopMilestoneProgress,
                "The in-memory profile is updated; writing it out is the caller's call.");
        }

        [Test]
        public void NonPositivePoints_AreRefused_RatherThanSilentlyAccepted()
        {
            // A zero-point purchase is a caller bug, and a negative one would drain a balance that
            // is meant to be monotonic. Both are refused loudly instead of quietly applied.
            var profile = new PlayerProfile();
            ShopLoyaltyService.Accrue(profile, 7);

            foreach (int bad in new[] { 0, -1, -100 })
            {
                ShopLoyaltyResult result = ShopLoyaltyService.Accrue(profile, bad);
                Assert.IsFalse(result.Accrued, bad + " must not accrue.");
                Assert.AreEqual(7, profile.shopMilestoneProgress,
                    "A refused accrual must leave the balance untouched.");
                Assert.IsNotEmpty(result.Message, "A refusal must say why.");
            }
        }

        [Test]
        public void ACorruptedNegativeBalance_IsFlooredOnBothReadAndWrite()
        {
            // SaveMigration applies AtLeastZero to every other int as a corrupted-save guard. That
            // file is FROZEN and was not part of this field's sign-off, so the service does the
            // flooring itself rather than leaving a hand-edited save to poison the total.
            var profile = new PlayerProfile { shopMilestoneProgress = -50 };

            Assert.AreEqual(0, ShopLoyaltyService.ProgressOf(profile), "Reads must floor at zero.");

            ShopLoyaltyService.Accrue(profile, 10);
            Assert.AreEqual(10, profile.shopMilestoneProgress,
                "Accruing onto a negative must start from zero, not from -50 (which would need 60 " +
                "points of purchases before the player saw any progress at all).");
        }

        [Test]
        public void ANullProfile_IsRefused_NotThrown()
        {
            ShopLoyaltyResult result = ShopLoyaltyService.Accrue(null, 5);

            Assert.IsFalse(result.Accrued);
            Assert.AreEqual(0, ShopLoyaltyService.ProgressOf(null),
                "A missing profile reads as zero rather than throwing mid-purchase.");
        }

        [Test]
        public void PointsAreEarnedPerTenGems_RoundedDownPerTransaction()
        {
            // The locked rule, and the rounding boundary is the whole point of it: rounding PER
            // TRANSACTION means ten 9-Gem purchases earn nothing while one 90-Gem purchase earns 9.
            // That asymmetry is deliberate, so it is pinned rather than left to look like a bug.
            Assert.AreEqual(0, ShopLoyaltyService.PointsForGemsSpent(9), "Under one point rounds to zero.");
            Assert.AreEqual(1, ShopLoyaltyService.PointsForGemsSpent(10));
            Assert.AreEqual(1, ShopLoyaltyService.PointsForGemsSpent(19), "Rounds DOWN, never up.");
            Assert.AreEqual(9, ShopLoyaltyService.PointsForGemsSpent(90));

            int tenSmallPurchases = 0;
            for (int i = 0; i < 10; i++) tenSmallPurchases += ShopLoyaltyService.PointsForGemsSpent(9);
            Assert.AreEqual(0, tenSmallPurchases,
                "90 Gems across ten transactions earns 0, while 90 in one earns 9 - per-transaction " +
                "rounding is the locked rule, not an accident.");

            Assert.AreEqual(0, ShopLoyaltyService.PointsForGemsSpent(0));
            Assert.AreEqual(0, ShopLoyaltyService.PointsForGemsSpent(-50), "A refund must never earn.");
        }

        [Test]
        public void TheMilestoneLadder_IsAscending_AndAwardsNoAcquisitionRewards()
        {
            // The ladder must ascend or "highest reached" is meaningless. The second half matters
            // more: loyalty is a recognition/convenience sink by design, and a card or pack slipping
            // into this list would quietly make it a second acquisition path - the exact mistake
            // VIP's original wording made.
            var forbidden = new[] { "card", "pack", "dust", "permit", "evolution", "credit", "spell" };
            int previous = 0;
            foreach (ShopLoyaltyService.LoyaltyMilestone m in ShopLoyaltyService.Milestones)
            {
                Assert.Greater(m.Points, previous, "Milestones must strictly ascend.");
                previous = m.Points;
                foreach (string word in forbidden)
                    StringAssert.DoesNotContain(word, m.Reward.ToLowerInvariant(),
                        "Milestone " + m.Points + " awards an acquisition reward: " + m.Reward);
            }
        }

        [Test]
        public void ReachingAMilestone_IsTrackedSeparatelyFromClaimingIt()
        {
            var profile = new PlayerProfile();
            Assert.AreEqual(-1, ShopLoyaltyService.HighestMilestoneReachedIndex(profile),
                "Nothing reached at zero progress.");

            ShopLoyaltyService.Accrue(profile, 100);
            Assert.AreEqual(0, ShopLoyaltyService.HighestMilestoneReachedIndex(profile),
                "100 points reaches the first milestone exactly.");

            ShopLoyaltyService.Accrue(profile, 900);   // 1000 total
            Assert.AreEqual(3, ShopLoyaltyService.HighestMilestoneReachedIndex(profile));

            // Reaching is still not claiming: progress moved, the claim guard did not.
            Assert.AreEqual(0, ShopLoyaltyService.HighestClaimedOf(profile),
                "Accruing points must never claim anything on its own.");
        }

        [Test]
        public void RedemptionIsNowBuilt_AndTheGuardStartsClosed()
        {
            // Replaces the old "redemption is not available" test, which said in its own body that
            // it should be rewritten rather than flipped once redemption existed. It does now.
            Assert.IsTrue(ShopLoyaltyService.RedemptionAvailable,
                "The claim guard exists, so redemption is available as a system.");

            var profile = new PlayerProfile();
            Assert.AreEqual(0, profile.highestClaimedLoyaltyMilestone,
                "A fresh save must read as nothing-claimed.");
            Assert.AreEqual(-1, ShopLoyaltyService.NextClaimableMilestone(profile),
                "Zero progress reaches no rung, so there is nothing to claim.");
        }

        [Test]
        public void AnOldSaveDefaultsBelowTheLowestRung_SoNoMigrationIsNeeded()
        {
            // This is WHY the field stores the milestone points value rather than an index. Old
            // saves deserialize the int to 0; 0 is below the lowest rung (100), so it reads as
            // "nothing claimed". An index-based field would have made index 0 - the 100 rung -
            // look already claimed on every save that existed before this field did.
            var oldSave = new PlayerProfile();
            ShopLoyaltyService.Accrue(oldSave, 100);

            Assert.AreEqual(100, ShopLoyaltyService.NextClaimableMilestone(oldSave),
                "An existing player who has reached 100 must still be owed the 100 rung.");
        }

        [Test]
        public void ClaimingIsStrictlyAscending_SoAHighRungCannotBurnTheLowerOnes()
        {
            // The single-int guard means "claimed iff X <= stored". If a caller could claim 1,000
            // directly, 100/250/500 would all become unclaimable in the same stroke - a player
            // would silently destroy three rewards by picking the shiny one. So there is no
            // claim-by-value API at all, only ClaimNext, and this pins that.
            var profile = new PlayerProfile();
            ShopLoyaltyService.Accrue(profile, 1000);

            Assert.AreEqual(100, ShopLoyaltyService.NextClaimableMilestone(profile),
                "Even at 1,000 points, the next claimable rung is the LOWEST unclaimed one.");
        }

        [Test]
        public void TheFirstRung_GrantsRealStamina_AndCountsAgainstTheShopCap()
        {
            var profile = new PlayerProfile { stamina = 0, maxStamina = 100 };
            ShopLoyaltyService.Accrue(profile, 100);
            long now = ShopStaminaCatalog.NowUtcTicks();

            ShopLoyaltyClaimResult result = ShopLoyaltyService.ClaimNext(profile, now);

            Assert.IsTrue(result.Claimed, result.Message);
            Assert.AreEqual(100, result.MilestonePoints);
            Assert.AreEqual(1, result.StaminaClaimsApplied, "The 100 rung grants one real claim.");
            Assert.Greater(profile.stamina, 0, "Stamina must actually be restored, not just counted.");

            // The whole point of the reward wording: a milestone may NOT bypass the 4-per-24h cap.
            Assert.AreEqual(1, profile.staminaShopPurchasesInWindow,
                "A loyalty Stamina claim must consume a slot in the real Shop refill window.");

            Assert.AreEqual(100, profile.highestClaimedLoyaltyMilestone,
                "The guard must advance so the rung cannot be claimed twice.");
        }

        [Test]
        public void TheSameRung_CannotBeClaimedTwice()
        {
            var profile = new PlayerProfile { stamina = 0, maxStamina = 100 };
            ShopLoyaltyService.Accrue(profile, 100);
            long now = ShopStaminaCatalog.NowUtcTicks();

            Assert.IsTrue(ShopLoyaltyService.ClaimNext(profile, now).Claimed);

            ShopLoyaltyClaimResult again = ShopLoyaltyService.ClaimNext(profile, now);
            Assert.IsFalse(again.Claimed,
                "One-time means one-time - this is the entire reason the guard field was added.");
            Assert.AreEqual(1, profile.staminaShopPurchasesInWindow,
                "A refused re-claim must not grant a second Stamina claim either.");
        }

        [Test]
        public void AHeldVoucherRung_RefusesWithoutConsumingTheClaim()
        {
            // The 250 rung's voucher duration is not locked (250 was remapped to weekly while the
            // revised whale-tier lock puts a 7-day voucher at 2,000, which would sit BELOW the
            // 1,000 rung's fortnight and stop the ladder ascending). Refusing is correct - but it
            // must NOT advance the guard, or the player pays for the indecision by losing the
            // reward permanently.
            var profile = new PlayerProfile { stamina = 0, maxStamina = 100 };
            ShopLoyaltyService.Accrue(profile, 250);
            long now = ShopStaminaCatalog.NowUtcTicks();

            ShopLoyaltyService.ClaimNext(profile, now);            // consumes the 100 rung
            ShopLoyaltyClaimResult held = ShopLoyaltyService.ClaimNext(profile, now);

            Assert.IsFalse(held.Claimed, "A voucher with no locked duration must not be granted.");
            Assert.AreEqual(250, held.MilestonePoints);
            Assert.AreEqual(100, profile.highestClaimedLoyaltyMilestone,
                "The guard must stay where it was, so the 250 reward is still owed later.");
        }

        [Test]
        public void TheCosmeticRung_RefusesBecauseTheSaveCannotRepresentIt()
        {
            // Milestone 500 awards a cosmetic and PlayerProfile has no cosmetic ownership model at
            // all. Granting a reward the save cannot represent is worse than not granting it - the
            // player would see a success message and own nothing.
            Assert.IsTrue(ShopLoyaltyService.CosmeticGrantsUnsupported(500));
            Assert.IsFalse(ShopLoyaltyService.CosmeticGrantsUnsupported(100),
                "Only the cosmetic rung is blocked for this reason.");
        }

        [Test]
        public void TheGoldRungs_AreCurrentlyUNREACHABLE_BecauseHeldRungsBlockTheQueue()
        {
            // Worth pinning loudly rather than discovering in QA: claims are strictly ascending and
            // 250 refuses, so a player sitting on 2,000 points cannot reach the 25,000 Gold reward
            // at all. The code is correct; the ladder is order-blocked. This test should START
            // FAILING the moment the voucher durations are locked - that is the signal to unhold
            // them, not a regression.
            var profile = new PlayerProfile { stamina = 0, maxStamina = 100 };
            ShopLoyaltyService.Accrue(profile, 2000);
            long now = ShopStaminaCatalog.NowUtcTicks();

            ShopLoyaltyService.ClaimNext(profile, now);            // 100, succeeds
            ShopLoyaltyClaimResult blocked = ShopLoyaltyService.ClaimNext(profile, now);

            Assert.IsFalse(blocked.Claimed);
            Assert.AreEqual(250, blocked.MilestonePoints,
                "The queue stops at the first held rung, well short of any Gold reward.");
            Assert.AreEqual(0, profile.gold - 1000,
                "No Gold may be granted while the queue is blocked (1000 is the profile default).");
        }

        [Test]
        public void TheRewardTables_MatchTheLockedLadder()
        {
            // Relationships, not magnitudes where it matters - but these three ARE the locked
            // numbers (verified: 100,000 Gold = 80 days of max Solo Circuit = 5.62% of the Empire
            // sink), so a silent change to them is a real economy change and should break a test.
            Assert.AreEqual(0, ShopLoyaltyService.GoldRewardFor(100));
            Assert.AreEqual(25000, ShopLoyaltyService.GoldRewardFor(2000));
            Assert.AreEqual(50000, ShopLoyaltyService.GoldRewardFor(4000));
            Assert.AreEqual(100000, ShopLoyaltyService.GoldRewardFor(8000));

            Assert.AreEqual(1, ShopLoyaltyService.StaminaClaimsFor(100));
            Assert.AreEqual(8, ShopLoyaltyService.StaminaClaimsFor(8000));

            // Gold must ascend with the rung, or a higher tier pays less than a lower one.
            Assert.Greater(ShopLoyaltyService.GoldRewardFor(8000), ShopLoyaltyService.GoldRewardFor(4000));
            Assert.Greater(ShopLoyaltyService.GoldRewardFor(4000), ShopLoyaltyService.GoldRewardFor(2000));
        }

    }
}

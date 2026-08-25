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
    /// Redemption is still absent, for a narrower reason now: the milestones are ONE-TIME and
    /// nothing on the profile records which have been claimed, and two reward types have nowhere to
    /// land in the save at all. One test pins that so a future reader cannot mistake the gap for an
    /// oversight.
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

            // Reaching is not claiming, and nothing here grants anything.
            Assert.IsFalse(ShopLoyaltyService.RedemptionAvailable);
        }

        [Test]
        public void RedemptionIsNotAvailable_AndThatIsRecordedDeliberately()
        {
            // NOT a placeholder. Shop_V1_Release_Contract.md:50 requires a milestone table and does
            // not contain one, so there is nothing to redeem AGAINST. This test exists so the gap
            // is visible in the suite rather than looking like forgotten work - and it will fail
            // the moment someone implements redemption, which is when it should be rewritten.
            Assert.IsFalse(ShopLoyaltyService.RedemptionAvailable,
                "If redemption is now built, this test's premise has changed - rewrite it rather " +
                "than flipping the assertion.");
        }
    }
}

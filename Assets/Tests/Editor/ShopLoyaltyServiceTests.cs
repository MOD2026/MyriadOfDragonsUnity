using MyriadOfDragons.Save;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Shop Loyalty accrual - the EARN/TRACK half.
    ///
    /// Redemption is deliberately absent: docs/Shop_V1_Release_Contract.md:50 requires "a milestone
    /// table" alongside the counter, and no such table exists - no thresholds, no rewards, no claim
    /// rules. These tests cover what was specifiable and one of them pins the fact that redemption
    /// is NOT available, so a future reader cannot mistake silence for an oversight.
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

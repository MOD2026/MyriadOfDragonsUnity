using MyriadOfDragons.Empire;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Locks the owner-signed deck curve (L1=7 ... L30=15) and that construction targets
    /// (the paid milestone ladder) are unchanged and still skip interstitial levels.
    /// </summary>
    public class BarracksMilestoneTests
    {
        [Test]
        public void CurveMilestones_GrantTheOwnerSignedSlotTable()
        {
            // BATTLE-REMAINING-OWNER-DECISIONS-0.9-SIGNED.md: all eight milestones.
            Assert.AreEqual(7, PlayerEmpireData.DeckSlotsForBarracksLevel(1));
            Assert.AreEqual(8, PlayerEmpireData.DeckSlotsForBarracksLevel(3));
            Assert.AreEqual(9, PlayerEmpireData.DeckSlotsForBarracksLevel(5));
            Assert.AreEqual(10, PlayerEmpireData.DeckSlotsForBarracksLevel(8));
            Assert.AreEqual(11, PlayerEmpireData.DeckSlotsForBarracksLevel(10));
            Assert.AreEqual(12, PlayerEmpireData.DeckSlotsForBarracksLevel(15));
            Assert.AreEqual(13, PlayerEmpireData.DeckSlotsForBarracksLevel(20));
            Assert.AreEqual(15, PlayerEmpireData.DeckSlotsForBarracksLevel(30));
        }

        [Test]
        public void InterstitialLevels_DoNotGrantExtraSlots()
        {
            Assert.AreEqual(7, PlayerEmpireData.DeckSlotsForBarracksLevel(2));
            Assert.AreEqual(8, PlayerEmpireData.DeckSlotsForBarracksLevel(4));
            Assert.AreEqual(9, PlayerEmpireData.DeckSlotsForBarracksLevel(7));
            Assert.AreEqual(10, PlayerEmpireData.DeckSlotsForBarracksLevel(9));
            Assert.AreEqual(11, PlayerEmpireData.DeckSlotsForBarracksLevel(14));
            Assert.AreEqual(13, PlayerEmpireData.DeckSlotsForBarracksLevel(29));
        }

        [Test]
        public void PastCap_StaysAtFifteenSlots()
        {
            Assert.AreEqual(15, PlayerEmpireData.DeckSlotsForBarracksLevel(31));
            Assert.AreEqual(15, PlayerEmpireData.DeckSlotsForBarracksLevel(50));
        }

        [Test]
        public void NextPaidMilestone_SkipsEmptyLevels()
        {
            Assert.AreEqual(5, PlayerEmpireData.NextPaidBarracksMilestone(1));
            Assert.AreEqual(5, PlayerEmpireData.NextPaidBarracksMilestone(4));
            Assert.AreEqual(10, PlayerEmpireData.NextPaidBarracksMilestone(5));
            Assert.AreEqual(30, PlayerEmpireData.NextPaidBarracksMilestone(25));
            Assert.AreEqual(0, PlayerEmpireData.NextPaidBarracksMilestone(30));
            Assert.AreEqual(0, PlayerEmpireData.NextPaidBarracksMilestone(50));
        }

        [Test]
        public void InitializeTCGModifiers_UsesTheLookupNotDividedByFive()
        {
            var empire = new PlayerEmpireData();
            empire.SetLevelsForTesting(avatarLevel: 1, castleLevel: 1, barracksLevel: 30);
            empire.InitializeTCGModifiers();
            Assert.AreEqual(15, empire.DeckSlotCount,
                "L30 must be the 15-slot ceiling. The old ÷5 formula only reached 16 here.");

            empire.SetLevelsForTesting(avatarLevel: 1, castleLevel: 1, barracksLevel: 4);
            empire.InitializeTCGModifiers();
            Assert.AreEqual(8, empire.DeckSlotCount,
                "Empty Barracks levels must not sell a deck slot.");
        }

        [Test]
        public void UnusedRegenAndReplenish_DoNotScaleWithBarracks()
        {
            var low = new PlayerEmpireData();
            low.SetLevelsForTesting(1, 1, 1);
            low.InitializeTCGModifiers();

            var high = new PlayerEmpireData();
            high.SetLevelsForTesting(1, 1, 30);
            high.InitializeTCGModifiers();

            Assert.AreEqual(low.ResourceRegenRate, high.ResourceRegenRate);
            Assert.AreEqual(low.PostMatchReplenishRate, high.PostMatchReplenishRate);
            Assert.Greater(high.DeckSlotCount, low.DeckSlotCount);
        }

        [Test]
        public void GoldCosts_StrictlyIncreaseAcrossPaidTiers()
        {
            int[] targets = { 5, 10, 15, 20, 25, 30 };
            int[] from = { 1, 5, 10, 15, 20, 25 };
            int previous = 0;
            for (int i = 0; i < targets.Length; i++)
            {
                int cost = PlayerEmpireData.GoldCostForBarracksUpgrade(from[i], targets[i]);
                Assert.Greater(cost, previous,
                    $"Barracks →L{targets[i]} must cost more Gold than the prior paid tier.");
                previous = cost;
            }
        }

        [Test]
        public void CastlePrereqLadder_MatchesTheLockedCurve()
        {
            // Real curve locked 2026-08-24 (GPT round, closes the "Open, real" Castle-interlock
            // gap in docs/LOCKED_DECISIONS_REGISTER.md) - lighter/lower than Gate's own curve,
            // using only Barracks' existing purchasable milestones.
            Assert.AreEqual(1, PlayerEmpireData.MinimumCastleForBarracksLevel(1));
            Assert.AreEqual(3, PlayerEmpireData.MinimumCastleForBarracksLevel(5));
            Assert.AreEqual(7, PlayerEmpireData.MinimumCastleForBarracksLevel(10));
            Assert.AreEqual(12, PlayerEmpireData.MinimumCastleForBarracksLevel(15));
            Assert.AreEqual(18, PlayerEmpireData.MinimumCastleForBarracksLevel(20));
            Assert.AreEqual(24, PlayerEmpireData.MinimumCastleForBarracksLevel(25));
            Assert.AreEqual(30, PlayerEmpireData.MinimumCastleForBarracksLevel(30));
            Assert.AreEqual(0, PlayerEmpireData.MinimumCastleForBarracksLevel(4),
                "Empty Barracks levels are not purchasable targets - same sparse shape as Gate's own ladder.");
        }

        [Test]
        public void GoldCosts_EarlyTierAreAffordable_LateCapstoneIsNotTrivial()
        {
            // Relationships vs Campaign first-clear Gold bands (EMPIRE_FEASIBILITY_AUDIT),
            // not hard-coded stage reward magnitudes — so retuning a chapter step does not
            // falsely fail this gate.
            const int ApproxChapter1Gold = 6_000;
            const int ApproxThroughChapter3Gold = 155_000;
            const int ApproxThroughChapter5Gold = 650_000;

            int toL5 = PlayerEmpireData.GoldCostForBarracksUpgrade(1, 5);
            Assert.Less(toL5, ApproxChapter1Gold,
                "First Barracks buy must be reachable from a Chapter 1 clear (keep playing).");

            int toL30 = PlayerEmpireData.GoldCostForBarracksUpgrade(25, 30);
            Assert.Greater(toL30, ApproxThroughChapter3Gold,
                "L30 alone must cost more than Chapters 1–3 combined — not a mid-game impulse buy.");

            int fullPath = PlayerEmpireData.RemainingGoldToMaxBarracks(1);
            Assert.Greater(fullPath, ApproxThroughChapter5Gold / 2,
                "Full Barracks path must consume a large share of early-mid Campaign Gold so L30 is a late goal.");
            Assert.Less(fullPath, ApproxThroughChapter5Gold,
                "Full Barracks path must still be achievable from Campaign Gold alone before Ch10 (not softlocked).");

            Assert.AreEqual(0, PlayerEmpireData.GoldCostForBarracksUpgrade(1, 10),
                "Skipping paid tiers is forbidden — cost is only for the next milestone.");
            Assert.AreEqual(0, PlayerEmpireData.RemainingGoldToMaxBarracks(30));
        }
    }
}

using MyriadOfDragons.Empire;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Gate meta route map (EMPIRE_SCHEMA_LOCK): necessary ≠ sufficient for Campaign entry.
    /// </summary>
    public class GateRouteTests
    {
        [Test]
        public void GateMilestones_UnlockChaptersOneThroughTen()
        {
            Assert.AreEqual(1, PlayerEmpireData.GetHighestCampaignChapterAllowed(1));
            Assert.AreEqual(1, PlayerEmpireData.GetHighestCampaignChapterAllowed(2));
            Assert.AreEqual(2, PlayerEmpireData.GetHighestCampaignChapterAllowed(3));
            Assert.AreEqual(3, PlayerEmpireData.GetHighestCampaignChapterAllowed(6));
            Assert.AreEqual(4, PlayerEmpireData.GetHighestCampaignChapterAllowed(9));
            Assert.AreEqual(5, PlayerEmpireData.GetHighestCampaignChapterAllowed(12));
            Assert.AreEqual(6, PlayerEmpireData.GetHighestCampaignChapterAllowed(15));
            Assert.AreEqual(7, PlayerEmpireData.GetHighestCampaignChapterAllowed(18));
            Assert.AreEqual(8, PlayerEmpireData.GetHighestCampaignChapterAllowed(21));
            Assert.AreEqual(9, PlayerEmpireData.GetHighestCampaignChapterAllowed(24));
            Assert.AreEqual(10, PlayerEmpireData.GetHighestCampaignChapterAllowed(27));
            Assert.AreEqual(11, PlayerEmpireData.GetHighestCampaignChapterAllowed(30),
                "Gate L30 unlocks Chapter 11 (The Storm's Price).");
        }

        [Test]
        public void ChapterAllowed_IsNecessaryNotSufficient()
        {
            Assert.IsFalse(PlayerEmpireData.IsCampaignChapterAllowedByGate(1, 2));
            Assert.IsTrue(PlayerEmpireData.IsCampaignChapterAllowedByGate(3, 2));
            Assert.IsFalse(PlayerEmpireData.IsCampaignChapterAllowedByGate(21, 9));
            Assert.IsTrue(PlayerEmpireData.IsCampaignChapterAllowedByGate(24, 9));
            Assert.IsTrue(PlayerEmpireData.IsCampaignChapterAllowedByGate(30, 11));
            Assert.IsFalse(PlayerEmpireData.IsCampaignChapterAllowedByGate(27, 11));
            Assert.IsFalse(PlayerEmpireData.IsCampaignChapterAllowedByGate(30, 0));
        }

        [Test]
        public void CastlePrereqLadder_MatchesFeasibility()
        {
            Assert.AreEqual(1, PlayerEmpireData.MinimumCastleForGateLevel(1));
            Assert.AreEqual(5, PlayerEmpireData.MinimumCastleForGateLevel(3));
            Assert.AreEqual(10, PlayerEmpireData.MinimumCastleForGateLevel(6));
            Assert.AreEqual(15, PlayerEmpireData.MinimumCastleForGateLevel(9));
            Assert.AreEqual(20, PlayerEmpireData.MinimumCastleForGateLevel(12));
            Assert.AreEqual(22, PlayerEmpireData.MinimumCastleForGateLevel(15));
            Assert.AreEqual(24, PlayerEmpireData.MinimumCastleForGateLevel(18));
            Assert.AreEqual(26, PlayerEmpireData.MinimumCastleForGateLevel(21));
            Assert.AreEqual(28, PlayerEmpireData.MinimumCastleForGateLevel(24));
            Assert.AreEqual(30, PlayerEmpireData.MinimumCastleForGateLevel(27));
            Assert.AreEqual(0, PlayerEmpireData.MinimumCastleForGateLevel(4),
                "Empty Gate levels are not purchasable targets.");
        }

        [Test]
        public void GateGold_OnlyNextMilestone_AndCh1SoftlockBand()
        {
            Assert.AreEqual(1_900, PlayerEmpireData.GoldCostForGateUpgrade(1, 3));
            Assert.AreEqual(0, PlayerEmpireData.GoldCostForGateUpgrade(1, 6),
                "Cannot skip Gate milestones.");
            Assert.AreEqual(7_000, PlayerEmpireData.GoldCostForGateUpgrade(3, 6));
            Assert.AreEqual(180_000, PlayerEmpireData.GoldCostForGateUpgrade(27, 30));
            Assert.AreEqual(0, PlayerEmpireData.NextPaidGateMilestone(30));

            // Castle L2–5 draft sum 1,950 + Gate L3 1,900 = 3,850 < ~6k Ch1 (packet guardrail).
            const int CastleL2ThroughL5Draft = 1_950;
            Assert.Less(CastleL2ThroughL5Draft + PlayerEmpireData.GoldCostForGateUpgrade(1, 3), 6_000);
        }
    }
}

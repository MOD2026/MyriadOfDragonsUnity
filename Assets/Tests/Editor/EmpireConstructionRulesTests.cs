using MyriadOfDragons.Empire;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    public class EmpireConstructionRulesTests
    {
        [Test]
        public void CastleStart_ChargesAndLandsReadyToCollect()
        {
            var idle = new EmpireConstructionState();
            var result = EmpireConstructionRules.TryStart(idle, new EmpireConstructionStartRequest
            {
                BuildingId = EmpireBuildingId.Castle,
                CurrentBuildingLevel = 1,
                CastleLevel = 1,
                AvailableGold = 10_000,
                ProjectId = "p1",
            });

            Assert.IsTrue(result.Ok);
            Assert.AreEqual(250, result.GoldCharged);
            Assert.AreEqual(EmpireConstructionStatus.ReadyToCollect, result.State.status);
            Assert.AreEqual(2, result.State.targetLevel);
            Assert.IsTrue(result.State.costCharged);
        }

        [Test]
        public void BarracksStart_UsesNextPaidMilestoneNotPlusOne()
        {
            var result = EmpireConstructionRules.TryStart(new EmpireConstructionState(),
                new EmpireConstructionStartRequest
                {
                    BuildingId = EmpireBuildingId.Barracks,
                    CurrentBuildingLevel = 1,
                    CastleLevel = 1,
                    AvailableGold = 50_000,
                    ProjectId = "b1",
                });

            Assert.IsTrue(result.Ok);
            Assert.AreEqual(5, result.State.targetLevel);
            Assert.AreEqual(1_200, result.GoldCharged);
        }

        [Test]
        public void GateStart_RequiresCastlePrereq_AndBlocksSkip()
        {
            var blocked = EmpireConstructionRules.TryStart(new EmpireConstructionState(),
                new EmpireConstructionStartRequest
                {
                    BuildingId = EmpireBuildingId.Gate,
                    CurrentBuildingLevel = 1,
                    CastleLevel = 1,
                    AvailableGold = 50_000,
                    ProjectId = "g1",
                });
            Assert.IsFalse(blocked.Ok);

            var ok = EmpireConstructionRules.TryStart(new EmpireConstructionState(),
                new EmpireConstructionStartRequest
                {
                    BuildingId = EmpireBuildingId.Gate,
                    CurrentBuildingLevel = 1,
                    CastleLevel = 5,
                    AvailableGold = 50_000,
                    ProjectId = "g2",
                });
            Assert.IsTrue(ok.Ok);
            Assert.AreEqual(3, ok.State.targetLevel);
            Assert.AreEqual(1_900, ok.GoldCharged);
        }

        [Test]
        public void Claim_IsIdempotent()
        {
            var started = EmpireConstructionRules.TryStart(new EmpireConstructionState(),
                new EmpireConstructionStartRequest
                {
                    BuildingId = EmpireBuildingId.Castle,
                    CurrentBuildingLevel = 4,
                    CastleLevel = 4,
                    AvailableGold = 50_000,
                    ProjectId = "c1",
                });
            Assert.IsTrue(started.Ok);

            bool first = EmpireConstructionRules.TryClaimComplete(
                started.State, "c1", 4, out int levelAfter, out var afterFirst);
            Assert.IsTrue(first);
            Assert.AreEqual(5, levelAfter);

            bool second = EmpireConstructionRules.TryClaimComplete(
                started.State, "c1", 5, out int levelDup, out _);
            Assert.IsFalse(second);
            Assert.AreEqual(5, levelDup);
            Assert.AreEqual(EmpireConstructionStatus.CompleteClaimed, afterFirst.status);
        }

        [Test]
        public void CastleFullPath_MatchesPublishedCumulative()
        {
            Assert.AreEqual(727_450, PlayerEmpireData.RemainingGoldToMaxCastle(1));
            Assert.AreEqual(28_000, PlayerEmpireData.GoldCostForCastleUpgrade(20));
            Assert.Greater(PlayerEmpireData.GoldCostForCastleUpgrade(20),
                PlayerEmpireData.GoldCostForCastleUpgrade(19));
        }

        [Test]
        public void MaxCastleLevel_IsSharedConstant_UsedByConstructionRules()
        {
            Assert.AreEqual(30, PlayerEmpireData.MaxCastleLevel);

            var atCap = EmpireConstructionRules.TryStart(new EmpireConstructionState(),
                new EmpireConstructionStartRequest
                {
                    BuildingId = EmpireBuildingId.Castle,
                    CurrentBuildingLevel = PlayerEmpireData.MaxCastleLevel,
                    CastleLevel = PlayerEmpireData.MaxCastleLevel,
                    AvailableGold = 1_000_000,
                    ProjectId = "cap",
                });
            Assert.IsFalse(atCap.Ok, "Castle at MaxCastleLevel must refuse further starts.");
            Assert.AreEqual("Castle is at cap.", atCap.Error);

            Assert.AreEqual(0, PlayerEmpireData.GoldCostForCastleUpgrade(PlayerEmpireData.MaxCastleLevel));
            Assert.AreEqual(0, PlayerEmpireData.RemainingGoldToMaxCastle(PlayerEmpireData.MaxCastleLevel));
        }

        [Test]
        public void BarracksAndGate_AtCap_RefuseStart_WhenNextPaidReturnsZero()
        {
            Assert.AreEqual(0, PlayerEmpireData.NextPaidBarracksMilestone(30));
            Assert.AreEqual(0, PlayerEmpireData.NextPaidGateMilestone(30));

            var barracksAtCap = EmpireConstructionRules.TryStart(new EmpireConstructionState(),
                new EmpireConstructionStartRequest
                {
                    BuildingId = EmpireBuildingId.Barracks,
                    CurrentBuildingLevel = 30,
                    CastleLevel = PlayerEmpireData.MaxCastleLevel,
                    AvailableGold = 1_000_000,
                    ProjectId = "b-cap",
                });
            Assert.IsFalse(barracksAtCap.Ok);
            Assert.AreEqual("Barracks is at cap.", barracksAtCap.Error);

            var gateAtCap = EmpireConstructionRules.TryStart(new EmpireConstructionState(),
                new EmpireConstructionStartRequest
                {
                    BuildingId = EmpireBuildingId.Gate,
                    CurrentBuildingLevel = 30,
                    CastleLevel = PlayerEmpireData.MaxCastleLevel,
                    AvailableGold = 1_000_000,
                    ProjectId = "g-cap",
                });
            Assert.IsFalse(gateAtCap.Ok);
            Assert.AreEqual("Gate is at cap.", gateAtCap.Error);
        }
    }
}

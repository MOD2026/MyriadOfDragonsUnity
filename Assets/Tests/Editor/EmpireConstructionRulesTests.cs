using MyriadOfDragons.Empire;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    public class EmpireConstructionRulesTests
    {
        [TearDown]
        public void TearDown() => EmpireConstructionTimer.ClearTestClock();

        [Test]
        public void CastleStart_ChargesAndLandsBuilding_NotInstantReady()
        {
            const long now = 1_700_000_000_000L;
            var idle = new EmpireConstructionState();
            var result = EmpireConstructionRules.TryStart(idle, new EmpireConstructionStartRequest
            {
                BuildingId = EmpireBuildingId.Castle,
                CurrentBuildingLevel = 1,
                CastleLevel = 1,
                AvailableGold = 10_000,
                AvailableMaterials = 10_000,
                ProjectId = "p1",
                NowUtcMs = now,
            });

            Assert.IsTrue(result.Ok);
            Assert.AreEqual(250, result.GoldCharged);
            Assert.AreEqual(1_100, result.MaterialsCharged);
            Assert.AreEqual(EmpireConstructionStatus.Building, result.State.status);
            Assert.AreEqual(2, result.State.targetLevel);
            Assert.IsTrue(result.State.costCharged);
            Assert.AreEqual(now, result.State.startedUtcMs);
            Assert.Greater(result.State.endsAtUtcMs, now);
            Assert.AreEqual(
                EmpireConstructionTimer.DurationSecondsForTargetLevel(2) * 1000L,
                result.State.endsAtUtcMs - now);
        }

        [Test]
        public void Advance_CompletesWhenTimerElapsed_RollbackBlocksEarly()
        {
            var started = EmpireConstructionRules.TryStart(new EmpireConstructionState(),
                new EmpireConstructionStartRequest
                {
                    BuildingId = EmpireBuildingId.Castle,
                    CurrentBuildingLevel = 1,
                    CastleLevel = 1,
                    AvailableGold = 10_000,
                    AvailableMaterials = 10_000,
                    ProjectId = "p-adv",
                    NowUtcMs = 1_000_000,
                });
            Assert.IsTrue(started.Ok);

            Assert.IsFalse(EmpireConstructionRules.TryAdvanceToReady(started.State, 999_000),
                "Clock rollback must not complete.");
            Assert.IsFalse(EmpireConstructionRules.TryAdvanceToReady(started.State, 1_000_000 + 1_000),
                "Before endsAt must stay Building.");
            Assert.IsTrue(EmpireConstructionRules.TryAdvanceToReady(started.State, started.State.endsAtUtcMs));
            Assert.AreEqual(EmpireConstructionStatus.ReadyToCollect, started.State.status);
        }

        [Test]
        public void BarracksStart_UsesNextPaidMilestoneNotPlusOne()
        {
            var result = EmpireConstructionRules.TryStart(new EmpireConstructionState(),
                new EmpireConstructionStartRequest
                {
                    BuildingId = EmpireBuildingId.Barracks,
                    CurrentBuildingLevel = 1,
                    CastleLevel = 3,
                    AvailableGold = 50_000,
                    AvailableMaterials = 50_000,
                    ProjectId = "b1",
                    NowUtcMs = 1,
                });

            Assert.IsTrue(result.Ok);
            Assert.AreEqual(5, result.State.targetLevel);
            Assert.AreEqual(1_200, result.GoldCharged);
            Assert.AreEqual(EmpireMaterialsLadder.CostBetween(1, 5), result.MaterialsCharged);
            Assert.AreEqual(EmpireConstructionStatus.Building, result.State.status);
        }

        [Test]
        public void BarracksStart_RequiresCastlePrereq_AndBlocksSkip()
        {
            var blocked = EmpireConstructionRules.TryStart(new EmpireConstructionState(),
                new EmpireConstructionStartRequest
                {
                    BuildingId = EmpireBuildingId.Barracks,
                    CurrentBuildingLevel = 1,
                    CastleLevel = 2,
                    AvailableGold = 50_000,
                    AvailableMaterials = 50_000,
                    ProjectId = "b2",
                    NowUtcMs = 1,
                });
            Assert.IsFalse(blocked.Ok);

            var ok = EmpireConstructionRules.TryStart(new EmpireConstructionState(),
                new EmpireConstructionStartRequest
                {
                    BuildingId = EmpireBuildingId.Barracks,
                    CurrentBuildingLevel = 1,
                    CastleLevel = 3,
                    AvailableGold = 50_000,
                    AvailableMaterials = 50_000,
                    ProjectId = "b3",
                    NowUtcMs = 1,
                });
            Assert.IsTrue(ok.Ok);
            Assert.AreEqual(5, ok.State.targetLevel);
            Assert.AreEqual(1_200, ok.GoldCharged);
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
                    AvailableMaterials = 50_000,
                    ProjectId = "g1",
                    NowUtcMs = 1,
                });
            Assert.IsFalse(blocked.Ok);

            var ok = EmpireConstructionRules.TryStart(new EmpireConstructionState(),
                new EmpireConstructionStartRequest
                {
                    BuildingId = EmpireBuildingId.Gate,
                    CurrentBuildingLevel = 1,
                    CastleLevel = 5,
                    AvailableGold = 50_000,
                    AvailableMaterials = 50_000,
                    ProjectId = "g2",
                    NowUtcMs = 1,
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
                    AvailableMaterials = 50_000,
                    ProjectId = "c1",
                    NowUtcMs = 1,
                });
            Assert.IsTrue(started.Ok);
            Assert.IsTrue(EmpireConstructionRules.TryAdvanceToReady(started.State, started.State.endsAtUtcMs));

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
                    AvailableMaterials = 1_000_000,
                    ProjectId = "cap",
                    NowUtcMs = 1,
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
                    AvailableMaterials = 1_000_000,
                    ProjectId = "b-cap",
                    NowUtcMs = 1,
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
                    AvailableMaterials = 1_000_000,
                    ProjectId = "g-cap",
                    NowUtcMs = 1,
                });
            Assert.IsFalse(gateAtCap.Ok);
            Assert.AreEqual("Gate is at cap.", gateAtCap.Error);
        }

        [Test]
        public void TimerBands_MatchLockedRegisterBoundaries()
        {
            Assert.AreEqual(30 * 60, EmpireConstructionTimer.DurationSecondsForTargetLevel(1));
            Assert.AreEqual(60 * 60, EmpireConstructionTimer.DurationSecondsForTargetLevel(5));
            Assert.AreEqual(4 * 3600, EmpireConstructionTimer.DurationSecondsForTargetLevel(6));
            Assert.AreEqual(8 * 3600, EmpireConstructionTimer.DurationSecondsForTargetLevel(10));
            Assert.AreEqual(86400, EmpireConstructionTimer.DurationSecondsForTargetLevel(11));
            Assert.AreEqual(3 * 86400, EmpireConstructionTimer.DurationSecondsForTargetLevel(15));
            Assert.AreEqual(12 * 86400, EmpireConstructionTimer.DurationSecondsForTargetLevel(25));
            // T26-30 band starts lower than T21-25 max by locked design (7-14d vs 8-12d).
            Assert.AreEqual(7 * 86400, EmpireConstructionTimer.DurationSecondsForTargetLevel(26));
            Assert.AreEqual(14 * 86400, EmpireConstructionTimer.DurationSecondsForTargetLevel(30));
            Assert.Greater(
                EmpireConstructionTimer.DurationSecondsForTargetLevel(30),
                EmpireConstructionTimer.DurationSecondsForTargetLevel(26));
        }
    }
}

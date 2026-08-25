using MyriadOfDragons.Empire;
using MyriadOfDragons.Save;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    public class EmpireConstructionServiceTests
    {
        [TearDown]
        public void TearDown() => EmpireConstructionTimer.ClearTestClock();

        [Test]
        public void StartAndClaim_RaisesCastle_DeductsGoldAndMaterials_AppliesEmpireOnce()
        {
            var profile = new PlayerProfile
            {
                gold = 5_000,
                constructionMaterials = 5_000,
                castleLevel = 1,
            };
            profile.ApplyDataToEmpire();
            int slotsBefore = profile.Empire.DeckSlotCount;

            Assert.IsTrue(EmpireConstructionService.TryStart(
                profile, EmpireBuildingId.Castle, "proj-a", out _));
            Assert.AreEqual(5_000 - 250, profile.gold);
            Assert.AreEqual(5_000 - 1_100, profile.constructionMaterials);
            Assert.AreEqual(1, profile.castleLevel);
            Assert.AreEqual(EmpireConstructionStatus.Building, profile.empireConstruction.status);

            EmpireConstructionTimer.UtcNowMsOverrideForTests = profile.empireConstruction.endsAtUtcMs;
            Assert.IsTrue(EmpireConstructionService.AdvanceIfDue(profile));
            Assert.AreEqual(EmpireConstructionStatus.ReadyToCollect, profile.empireConstruction.status);

            Assert.IsTrue(EmpireConstructionService.TryClaimComplete(profile, "proj-a"));
            Assert.AreEqual(2, profile.castleLevel);
            Assert.IsFalse(EmpireConstructionService.TryClaimComplete(profile, "proj-a"));

            profile.ApplyDataToEmpire();
            Assert.AreEqual(slotsBefore, profile.Empire.DeckSlotCount,
                "Castle claim must not change Barracks deck slots.");
        }

        [Test]
        public void Start_FailsWithoutMaterials_DoesNotChargeGold()
        {
            var profile = new PlayerProfile { gold = 5_000, constructionMaterials = 0, castleLevel = 1 };
            Assert.IsFalse(EmpireConstructionService.TryStart(
                profile, EmpireBuildingId.Castle, "proj-poor", out string error));
            Assert.AreEqual("Insufficient Materials.", error);
            Assert.AreEqual(5_000, profile.gold);
            Assert.AreEqual(EmpireConstructionStatus.Idle, profile.empireConstruction.status);
        }

        [Test]
        public void Normalize_NullConstruction_BecomesIdle()
        {
            var profile = new PlayerProfile { empireConstruction = null };
            SaveMigration.Normalize(profile);
            Assert.IsNotNull(profile.empireConstruction);
            Assert.AreEqual(EmpireConstructionStatus.Idle, profile.empireConstruction.status);
        }
    }
}

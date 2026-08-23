using MyriadOfDragons.Empire;
using MyriadOfDragons.Save;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    public class EmpireConstructionServiceTests
    {
        [Test]
        public void StartAndClaim_RaisesCastle_DeductsGold_AppliesEmpireOnce()
        {
            var profile = new PlayerProfile { gold = 5_000, castleLevel = 1 };
            profile.ApplyDataToEmpire();
            int slotsBefore = profile.Empire.DeckSlotCount;

            Assert.IsTrue(EmpireConstructionService.TryStart(
                profile, EmpireBuildingId.Castle, "proj-a", out _));
            Assert.AreEqual(5_000 - 250, profile.gold);
            Assert.AreEqual(1, profile.castleLevel);
            Assert.AreEqual(EmpireConstructionStatus.ReadyToCollect, profile.empireConstruction.status);

            Assert.IsTrue(EmpireConstructionService.TryClaimComplete(profile, "proj-a"));
            Assert.AreEqual(2, profile.castleLevel);
            Assert.IsFalse(EmpireConstructionService.TryClaimComplete(profile, "proj-a"));

            profile.ApplyDataToEmpire();
            Assert.AreEqual(slotsBefore, profile.Empire.DeckSlotCount,
                "Castle claim must not change Barracks deck slots.");
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

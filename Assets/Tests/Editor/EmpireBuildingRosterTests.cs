using System;
using System.Linq;
using MyriadOfDragons.Empire;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Empire construction v2's pure data/logic layer (docs/LOCKED_DECISIONS_REGISTER.md, "Empire
    /// construction — STRUCTURE LOCKED 2026-08-23"). Deliberately no BattleController/GameBootstrap/
    /// UI wiring and no Save-shape change - see EmpireBuildingRoster.cs's own class docs for why.
    /// </summary>
    public class EmpireBuildingRosterTests
    {
        [Test]
        public void Roster_ContainsExactlyTheElevenLockedBuildings()
        {
            CollectionAssert.AreEquivalent(
                new[]
                {
                    EmpireBuildingKind.Castle, EmpireBuildingKind.Barracks, EmpireBuildingKind.Storage,
                    EmpireBuildingKind.TrainingGrounds, EmpireBuildingKind.Quarry, EmpireBuildingKind.Gate,
                    EmpireBuildingKind.Academy, EmpireBuildingKind.Embassy, EmpireBuildingKind.TreeOfKnowledge,
                    EmpireBuildingKind.Prison, EmpireBuildingKind.GuildHall,
                },
                EmpireBuildingRoster.Buildings.Select(b => b.Kind).ToList());
        }

        [Test]
        public void Roster_EveryBuildingAppearsExactlyOnce()
        {
            CollectionAssert.AllItemsAreUnique(EmpireBuildingRoster.Buildings.Select(b => b.Kind).ToList());
        }

        [Test]
        public void GuildHall_IsTheOnlyBuildingWithoutAnUpgradeLadder()
        {
            var flat = EmpireBuildingRoster.Buildings.Where(b => !b.HasUpgradeLadder).Select(b => b.Kind).ToList();
            CollectionAssert.AreEqual(new[] { EmpireBuildingKind.GuildHall }, flat,
                "Guild Hall is the roster's only 'flat, single-level, no upgrade ladder' row.");
        }

        [Test]
        public void LaddedBuildings_AreExactlyTenBuildings()
        {
            Assert.AreEqual(10, EmpireBuildingRoster.LaddedBuildings.Count(),
                "Ten laddered buildings x 181,500 Materials each = the locked 1,815,000 faucet total.");
        }

        [Test]
        public void Get_ReturnsTheMatchingRow()
        {
            EmpireBuildingDefinition castle = EmpireBuildingRoster.Get(EmpireBuildingKind.Castle);
            Assert.AreEqual("Castle", castle.DisplayName);
            Assert.IsTrue(castle.HasUpgradeLadder);
        }

        [Test]
        public void CoreSpine_IsExactlyTheFiveNamedUngatedBuildings()
        {
            CollectionAssert.AreEquivalent(
                new[]
                {
                    EmpireBuildingKind.Castle, EmpireBuildingKind.Barracks, EmpireBuildingKind.Gate,
                    EmpireBuildingKind.Academy, EmpireBuildingKind.Embassy,
                },
                EmpireBuildingRoster.CoreSpineUngatedAtPhase1Start);
        }

        // ---------- Materials ladder ----------

        [TestCase(1, 1_100)]
        [TestCase(5, 1_100)]
        [TestCase(6, 2_200)]
        [TestCase(10, 2_200)]
        [TestCase(11, 4_400)]
        [TestCase(15, 4_400)]
        [TestCase(16, 7_700)]
        [TestCase(20, 7_700)]
        [TestCase(21, 11_000)]
        [TestCase(25, 11_000)]
        [TestCase(26, 12_375)]
        [TestCase(29, 12_375)]
        public void CostToUpgrade_MatchesTheLockedBandForEveryLevel(int currentLevel, int expectedCost)
        {
            Assert.AreEqual(expectedCost, EmpireMaterialsLadder.CostToUpgrade(currentLevel));
        }

        [Test]
        public void CostToUpgrade_AtMaxLevel_IsZero()
        {
            Assert.AreEqual(0, EmpireMaterialsLadder.CostToUpgrade(EmpireMaterialsLadder.MaxLevel));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(31)]
        public void CostToUpgrade_OutOfRangeLevel_IsZero(int invalidLevel)
        {
            Assert.AreEqual(0, EmpireMaterialsLadder.CostToUpgrade(invalidLevel));
        }

        [Test]
        public void TotalMaterialsToMax_FromLevelOne_IsExactlyTheLockedTotal()
        {
            Assert.AreEqual(181_500, EmpireMaterialsLadder.TotalMaterialsToMax(),
                "Locked exact total per building - 5x1,100 + 5x2,200 + 5x4,400 + 5x7,700 + 5x11,000 + 4x12,375.");
        }

        [Test]
        public void TotalMaterialsToMax_AcrossAllTenLaddedBuildings_MatchesTheLockedCampaignFaucet()
        {
            int total = EmpireMaterialsLadder.TotalMaterialsToMax() * EmpireBuildingRoster.LaddedBuildings.Count();
            Assert.AreEqual(1_815_000, total, "Locked: matches the existing campaign Materials faucet exactly.");
        }

        [Test]
        public void TotalMaterialsToMax_PartwayThrough_IsLessThanFromLevelOne()
        {
            int fromMidway = EmpireMaterialsLadder.TotalMaterialsToMax(fromLevel: 16);
            Assert.Less(fromMidway, EmpireMaterialsLadder.TotalMaterialsToMax());
            Assert.Greater(fromMidway, 0);
        }

        // ---------- Embassy help ----------

        [Test]
        public void ChargesPerDay_AtL1To5Band_MatchesTheLockedEndpoint()
        {
            Assert.AreEqual(1, EmpireEmbassyHelp.ChargesPerDayForBand(1));
            Assert.AreEqual(1, EmpireEmbassyHelp.ChargesPerDayForBand(5));
        }

        [Test]
        public void ChargesPerDay_AtL26To30Band_MatchesTheLockedEndpoint()
        {
            Assert.AreEqual(6, EmpireEmbassyHelp.ChargesPerDayForBand(26));
            Assert.AreEqual(6, EmpireEmbassyHelp.ChargesPerDayForBand(30));
        }

        [Test]
        public void ReductionMinutes_AtL1To5Band_MatchesTheLockedEndpoint()
        {
            Assert.AreEqual(10, EmpireEmbassyHelp.ReductionMinutesPerChargeForBand(1));
        }

        [Test]
        public void ReductionMinutes_AtL26To30Band_MatchesTheLockedEndpoint()
        {
            Assert.AreEqual(90, EmpireEmbassyHelp.ReductionMinutesPerChargeForBand(30));
        }

        [TestCase(6)]
        [TestCase(10)]
        [TestCase(11)]
        [TestCase(15)]
        [TestCase(16)]
        [TestCase(20)]
        [TestCase(21)]
        [TestCase(25)]
        public void ChargesPerDay_ForAnUnlockedMiddleBand_ThrowsRatherThanGuessing(int unlockedBandLevel)
        {
            Assert.Throws<NotSupportedException>(() => EmpireEmbassyHelp.ChargesPerDayForBand(unlockedBandLevel),
                "The four middle Embassy bands are not locked anywhere - must fail loudly, not silently interpolate.");
        }

        [TestCase(6)]
        [TestCase(21)]
        public void ReductionMinutes_ForAnUnlockedMiddleBand_ThrowsRatherThanGuessing(int unlockedBandLevel)
        {
            Assert.Throws<NotSupportedException>(() => EmpireEmbassyHelp.ReductionMinutesPerChargeForBand(unlockedBandLevel));
        }

        [Test]
        public void LifetimeCapPerProject_UsesThirtyPercentOfTimer_WhenBelowTheSixHourCeiling()
        {
            TimeSpan timer = TimeSpan.FromHours(10); // 30% = 3h, below the 6h ceiling.
            TimeSpan cap = EmpireEmbassyHelp.LifetimeCapPerProject(timer);
            Assert.AreEqual(TimeSpan.FromHours(3), cap);
        }

        [Test]
        public void LifetimeCapPerProject_ClampsToSixHours_ForALongTimer()
        {
            TimeSpan timer = TimeSpan.FromDays(7); // 30% = 50.4h, well above the 6h ceiling.
            TimeSpan cap = EmpireEmbassyHelp.LifetimeCapPerProject(timer);
            Assert.AreEqual(TimeSpan.FromHours(6), cap);
        }

        [Test]
        public void LifetimeCapPerProject_AtExactlySixHourEquivalent_IsSixHours()
        {
            TimeSpan timer = TimeSpan.FromHours(20); // 30% = exactly 6h.
            TimeSpan cap = EmpireEmbassyHelp.LifetimeCapPerProject(timer);
            Assert.AreEqual(TimeSpan.FromHours(6), cap);
        }
    }
}

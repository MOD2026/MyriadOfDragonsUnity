using MyriadOfDragons.Save;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>Exact 7×4 burn yield table + clamp + evolution spend-cap regression (BURN_YIELD_CODE_AUDIT_2026-08-23).</summary>
    public class CollectionBurnRulesTests
    {
        private static readonly int[] ExpectedTrainingXp = { 0, 10, 25, 60, 150, 350, 800, 1800 };
        private static readonly int[] ExpectedSacrifice = { 0, 1, 2, 4, 8, 16, 32, 64 };
        private static readonly int[] ExpectedForge = { 0, 10, 25, 60, 150, 350, 800, 1800 };
        private static readonly int[] ExpectedDust = { 0, 1, 2, 5, 12, 30, 75, 180 };

        [Test]
        public void LockedTable_EveryRarityAndPath_MatchesPacket()
        {
            for (int rarity = 1; rarity <= 7; rarity++)
            {
                Assert.AreEqual(ExpectedTrainingXp[rarity], CollectionBurnRules.GetTrainingXpYield(rarity),
                    $"Training XP rarity {rarity}");
                Assert.AreEqual(ExpectedSacrifice[rarity], CollectionBurnRules.GetGenericSacrificeYield(rarity),
                    $"Sacrifice rarity {rarity}");
                Assert.AreEqual(ExpectedForge[rarity], CollectionBurnRules.GetForgeCreditYield(rarity),
                    $"Forge rarity {rarity}");
                Assert.AreEqual(ExpectedDust[rarity], CollectionBurnRules.GetDustYield(rarity),
                    $"Dust rarity {rarity}");

                Assert.AreEqual(ExpectedTrainingXp[rarity],
                    CollectionBurnRules.GetYield(CollectionBurnPath.TrainingXp, rarity));
                Assert.AreEqual(ExpectedSacrifice[rarity],
                    CollectionBurnRules.GetYield(CollectionBurnPath.GenericSacrifice, rarity));
                Assert.AreEqual(ExpectedForge[rarity],
                    CollectionBurnRules.GetYield(CollectionBurnPath.ForgeCredit, rarity));
                Assert.AreEqual(ExpectedDust[rarity],
                    CollectionBurnRules.GetYield(CollectionBurnPath.Dust, rarity));
            }
        }

        [Test]
        public void RarityClamp_BelowOne_UsesOneStarYields()
        {
            Assert.AreEqual(ExpectedTrainingXp[1], CollectionBurnRules.GetTrainingXpYield(0));
            Assert.AreEqual(ExpectedTrainingXp[1], CollectionBurnRules.GetTrainingXpYield(-3));
            Assert.AreEqual(ExpectedDust[1], CollectionBurnRules.GetDustYield(0));
        }

        [Test]
        public void RarityClamp_AboveSeven_UsesSevenStarYields()
        {
            Assert.AreEqual(ExpectedForge[7], CollectionBurnRules.GetForgeCreditYield(8));
            Assert.AreEqual(ExpectedForge[7], CollectionBurnRules.GetForgeCreditYield(99));
            Assert.AreEqual(ExpectedSacrifice[7], CollectionBurnRules.GetGenericSacrificeYield(12));
        }

        [Test]
        public void UnsupportedPath_ReturnsZero()
        {
            Assert.AreEqual(0, CollectionBurnRules.GetYield((CollectionBurnPath)999, 4));
        }

        [Test]
        public void EvolutionSpendCaps_RemainLockedAt20And10Percent()
        {
            Assert.AreEqual(0.20f, CollectionSchemaRules.ForgeCreditMaxRecipeFraction, 0.0001f);
            Assert.AreEqual(0.10f, CollectionSchemaRules.DustMaxRecipeFraction, 0.0001f);

            const int sampleGoldFee = 1000;
            Assert.AreEqual(200, CollectionEvolutionRules.MaxForgeCreditOffset(sampleGoldFee));
            Assert.AreEqual(100, CollectionEvolutionRules.MaxDustOffset(sampleGoldFee));
        }
    }
}

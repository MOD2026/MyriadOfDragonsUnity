using System;
using System.Collections.Generic;
using MyriadOfDragons.Empire;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Collection Trial - rarity-band rules only, per the 2026-08-26 ruling. No school/faction
    /// taxonomy is invented here to make the original spec wording fit.
    ///
    /// Pure functions over an injected rarity lookup, so none of this depends on the shipped
    /// catalog's contents and none of it breaks when card content changes.
    /// </summary>
    public class SoloCircuitCollectionRuleTests
    {
        /// <summary>The REAL rarity distribution of the shipped catalog (85 cards), verified
        /// against Assets/Resources/Data/card_data.json. Used to check the bands are actually
        /// clearable, without coupling any test to a specific card id.</summary>
        private static readonly int[] CatalogCountsByRarity = { 0, 8, 9, 15, 21, 16, 9, 7 };

        private static Func<string, int> RarityMap(Dictionary<string, int> map) =>
            id => map.TryGetValue(id, out int r) ? r : 0;

        [Test]
        public void TheSameUtcDay_AlwaysSelectsTheSameBand()
        {
            Assert.AreEqual(
                SoloCircuitCollectionRule.BandFor("2026-08-26").Describe(),
                SoloCircuitCollectionRule.BandFor("2026-08-26").Describe());
        }

        [Test]
        public void BandsVaryAcrossDays_SoTheTrialIsNotTheSameEveryDay()
        {
            var seen = new HashSet<string>();
            DateTime day = new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc);
            for (int i = 0; i < 30; i++)
            {
                seen.Add(SoloCircuitCollectionRule.BandFor(SoloCollectionCircuit.UtcDayKey(day)).Describe());
                day = day.AddDays(1);
            }

            Assert.Greater(seen.Count, 1, "A single repeating band would make the trial static.");
        }

        [Test]
        public void EveryBand_IsClearableFromTheRealCatalog()
        {
            // The failure this prevents: a band like "5+ at exactly 7 stars" reads fine but only 7
            // such cards exist in the entire game, so most rosters could never clear it - and the
            // player would silently lose that day's circuit through no fault of their own. Asserts
            // against the real distribution rather than a specific card list, so it stays valid as
            // content changes.
            foreach (SoloCircuitRarityBand band in SoloCircuitCollectionRule.Bands)
            {
                int available = 0;
                for (int rarity = 1; rarity < CatalogCountsByRarity.Length; rarity++)
                    if (band.Includes(rarity)) available += CatalogCountsByRarity[rarity];

                Assert.GreaterOrEqual(available, band.RequiredCards,
                    "Band is unclearable even owning every card in the game: " + band.Describe());

                // Stronger: it must be clearable by a REALISTIC roster, not only a complete one.
                Assert.GreaterOrEqual(available, band.RequiredCards * 3,
                    "Band is too tight to be a daily: " + band.Describe() + " - only " + available +
                    " cards in the whole catalog qualify.");
            }
        }

        [Test]
        public void OwningEnoughInBand_SatisfiesTheRule()
        {
            var band = new SoloCircuitRarityBand(3, 5, 5);
            var map = new Dictionary<string, int>
            {
                { "a", 3 }, { "b", 4 }, { "c", 5 }, { "d", 3 }, { "e", 4 },
            };

            Assert.IsTrue(SoloCircuitCollectionRule.IsSatisfied(map.Keys, RarityMap(map), band));
        }

        [Test]
        public void CardsOutsideTheBand_DoNotCount()
        {
            var band = new SoloCircuitRarityBand(3, 5, 3);
            var map = new Dictionary<string, int>
            {
                { "low1", 1 }, { "low2", 2 }, { "high1", 6 }, { "high2", 7 }, { "in", 4 },
            };

            Assert.AreEqual(1, SoloCircuitCollectionRule.CountMatching(map.Keys, RarityMap(map), band));
            Assert.IsFalse(SoloCircuitCollectionRule.IsSatisfied(map.Keys, RarityMap(map), band));
        }

        [Test]
        public void DuplicateCopies_DoNotCountTwice()
        {
            // The trial is about BREADTH. Counting copies would let one lucky duplicate streak
            // clear a rule that exists to ask whether the player has a spread of cards.
            var band = new SoloCircuitRarityBand(3, 5, 3);
            var map = new Dictionary<string, int> { { "same", 4 } };
            var ownedWithDupes = new List<string> { "same", "same", "same", "same" };

            Assert.AreEqual(1,
                SoloCircuitCollectionRule.CountMatching(ownedWithDupes, RarityMap(map), band));
            Assert.IsFalse(SoloCircuitCollectionRule.IsSatisfied(ownedWithDupes, RarityMap(map), band));
        }

        [Test]
        public void UnknownCardIds_NeverCountTowardAReward()
        {
            // A migrated save can hold ids the catalog no longer knows
            // (collectionMigrationUnknownIds exists for exactly this). An unknown id must not
            // quietly satisfy a reward condition.
            var band = new SoloCircuitRarityBand(1, 7, 2);
            var map = new Dictionary<string, int> { { "real", 4 } };
            var owned = new List<string> { "real", "ghost", "" , null };

            Assert.AreEqual(1, SoloCircuitCollectionRule.CountMatching(owned, RarityMap(map), band));
        }

        [Test]
        public void AnEmptyRoster_IsRefused_NotThrown()
        {
            var band = SoloCircuitCollectionRule.BandFor("2026-08-26");
            Assert.AreEqual(0, SoloCircuitCollectionRule.CountMatching(null, id => 1, band));
            Assert.IsFalse(
                SoloCircuitCollectionRule.IsSatisfied(new List<string>(), id => 1, band));
        }
    }
}

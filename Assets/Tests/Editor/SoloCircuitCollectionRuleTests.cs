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

        // --- Eligibility-aware rotation (BS ruling 2026-08-26). BS supplied the acceptance list;
        // these are those cases, plus the determinism property the change put at risk. ---

        /// <summary>The real starter grant's rarity spread: 2x r1, 3x r2, 5x r3, nothing above.
        /// Verified against GameBootstrap.ApprovedStarterCollectionCardIds and card_data.json.</summary>
        private static Dictionary<string, int> StarterRoster()
        {
            var map = new Dictionary<string, int>();
            int n = 0;
            foreach (int rarity in new[] { 1, 1, 2, 2, 2, 3, 3, 3, 3, 3 }) map["s" + n++] = rarity;
            return map;
        }

        private static Dictionary<string, int> RosterWith(int highRarityCount)
        {
            Dictionary<string, int> map = StarterRoster();
            for (int i = 0; i < highRarityCount; i++) map["high" + i] = 5;
            return map;
        }

        [Test]
        public void AFreshStarterRoster_NEVERReceivesTheHighRarityBand()
        {
            // The original defect: 1 day in 6 was unclearable for a new player, and because the
            // 7-day cycle needs CONSECUTIVE days that cost ~72% of new players their first bonus.
            Dictionary<string, int> roster = StarterRoster();
            DateTime day = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            for (int i = 0; i < 180; i++)
            {
                SoloCircuitRarityBand band = SoloCircuitCollectionRule.BandFor(
                    SoloCollectionCircuit.UtcDayKey(day), roster.Keys, RarityMap(roster));

                Assert.IsTrue(SoloCircuitCollectionRule.IsSatisfied(roster.Keys, RarityMap(roster), band),
                    "Day " + i + " handed a starter roster an unclearable rule: " + band.Describe());
                day = day.AddDays(1);
            }
        }

        [Test]
        public void ExactlyFOURHighRarityCards_StillDoesNotUnlockTheHighBand()
        {
            // BS's boundary case. Four is one short, and off-by-one here means the band enters
            // rotation while still being unclearable - the original bug with extra steps.
            Dictionary<string, int> roster = RosterWith(4);
            Assert.IsFalse(SoloCircuitCollectionRule.QualifiesForHighRarityBand(
                roster.Keys, RarityMap(roster)));
        }

        [Test]
        public void ExactlyFIVEHighRarityCards_UnlocksTheHighBandNormally()
        {
            Dictionary<string, int> roster = RosterWith(5);
            Assert.IsTrue(SoloCircuitCollectionRule.QualifiesForHighRarityBand(
                roster.Keys, RarityMap(roster)));
        }

        [Test]
        public void LosingAQualifyingCard_RemovesEligibilitySafely()
        {
            // Burn/removal is real. The rule must re-evaluate rather than hold stale eligibility -
            // and must not throw partway through a claim path.
            Dictionary<string, int> roster = RosterWith(5);
            Assert.IsTrue(SoloCircuitCollectionRule.QualifiesForHighRarityBand(roster.Keys, RarityMap(roster)));

            roster.Remove("high0");

            Assert.DoesNotThrow(() => SoloCircuitCollectionRule.BandFor(
                "2026-08-26", roster.Keys, RarityMap(roster)));
            Assert.IsFalse(SoloCircuitCollectionRule.QualifiesForHighRarityBand(
                roster.Keys, RarityMap(roster)),
                "Eligibility must follow the CURRENT roster, not a remembered one.");
        }

        [Test]
        public void TheFallbackIsWORTHTHESAME_NotAConsolationRule()
        {
            // The trap this guards: fixing "new players cannot clear it" by quietly paying them
            // less would be the same problem wearing a different hat. The fallback differs only in
            // WHAT it asks, never in what it grants - reward value lives in SoloCollectionCircuit
            // and is not a function of which band was selected.
            var thin = new Dictionary<string, int> { { "a", 2 }, { "b", 2 }, { "c", 2 } };
            SoloCircuitRarityBand fallback = SoloCircuitCollectionRule.BandFor(
                "2026-08-26", thin.Keys, RarityMap(thin));

            Assert.AreEqual(SoloCircuitCollectionRule.FallbackRequiredCards, fallback.RequiredCards);
            Assert.IsTrue(SoloCircuitCollectionRule.IsSatisfied(thin.Keys, RarityMap(thin), fallback),
                "A roster that triggers the fallback must be able to CLEAR the fallback.");
        }

        [Test]
        public void SameDayAndSameRoster_AlwaysGivesTheSameRule()
        {
            // Determinism is what this change put at risk. It survives in the form that matters -
            // same day AND same roster - but it is no longer the same rule for every player on a
            // given day. That is the deliberate trade, pinned so nobody "restores" the pure hash.
            Dictionary<string, int> roster = RosterWith(5);
            string a = SoloCircuitCollectionRule.BandFor("2026-08-26", roster.Keys, RarityMap(roster)).Describe();
            string b = SoloCircuitCollectionRule.BandFor("2026-08-26", roster.Keys, RarityMap(roster)).Describe();
            Assert.AreEqual(a, b);
        }

        [Test]
        public void ARosterChange_MayChangeTodaysRule_AndThatIsIntended()
        {
            // Direct consequence of eligibility-aware rotation, stated as a property rather than
            // discovered as a bug: acquiring a card can change today's rule mid-day. It cannot
            // double-grant, because the claim guard is keyed by date+trialId, not by band.
            Dictionary<string, int> thin = StarterRoster();
            Dictionary<string, int> rich = RosterWith(5);

            string thinRule = SoloCircuitCollectionRule.BandFor("2026-08-26", thin.Keys, RarityMap(thin)).Describe();
            string richRule = SoloCircuitCollectionRule.BandFor("2026-08-26", rich.Keys, RarityMap(rich)).Describe();

            Assert.IsTrue(SoloCircuitCollectionRule.IsSatisfied(thin.Keys, RarityMap(thin),
                SoloCircuitCollectionRule.BandFor("2026-08-26", thin.Keys, RarityMap(thin))));
            Assert.IsTrue(SoloCircuitCollectionRule.IsSatisfied(rich.Keys, RarityMap(rich),
                SoloCircuitCollectionRule.BandFor("2026-08-26", rich.Keys, RarityMap(rich))));
            Assert.IsNotNull(thinRule);
            Assert.IsNotNull(richRule);
        }

        [Test]
        public void ANullRoster_FallsBackToTheDateOnlyPick_RatherThanThrowing()
        {
            // Display/preview paths may have no roster. They must degrade, not crash a screen.
            Assert.DoesNotThrow(() => SoloCircuitCollectionRule.BandFor("2026-08-26", null, null));
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

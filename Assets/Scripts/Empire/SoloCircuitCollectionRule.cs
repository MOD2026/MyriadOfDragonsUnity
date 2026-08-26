using System;
using System.Collections.Generic;

namespace MyriadOfDragons.Empire
{
    /// <summary>
    /// One day's Collection Trial rule: own at least N cards inside a rarity band.
    ///
    /// RARITY ONLY, by ruling (2026-08-26): school/faction rules are not Phase-1 and no card
    /// taxonomy is being invented to make the original spec wording fit. Worth recording for
    /// whoever revisits this - `Card.Class` (Warrior/Knight/Strategist/Perfect) DOES exist and IS
    /// fully populated across all 85 cards, so a class-based rule day would be buildable today if
    /// the design ever wants one. It is left unbuilt because it was not approved, not because the
    /// data is missing.
    /// </summary>
    public readonly struct SoloCircuitRarityBand
    {
        public readonly int MinRarity;
        public readonly int MaxRarity;
        public readonly int RequiredCards;

        public SoloCircuitRarityBand(int minRarity, int maxRarity, int requiredCards)
        {
            MinRarity = minRarity;
            MaxRarity = maxRarity;
            RequiredCards = requiredCards;
        }

        public bool Includes(int rarity) => rarity >= MinRarity && rarity <= MaxRarity;

        public string Describe() =>
            MinRarity == MaxRarity
                ? "Own " + RequiredCards + " or more cards at exactly " + MinRarity + " stars."
                : "Own " + RequiredCards + " or more cards between " + MinRarity + " and " +
                  MaxRarity + " stars.";
    }

    /// <summary>
    /// The Collection Trial's daily rule selection and satisfaction check.
    ///
    /// A pure function over (owned card ids -> rarity). It takes a rarity lookup rather than
    /// reaching into CardDatabase, for the same reason SoloCircuitDailySeed takes the puzzle id
    /// list: it keeps the class testable without the shipped catalog, so these tests do not start
    /// failing when card content changes.
    /// </summary>
    public static class SoloCircuitCollectionRule
    {
        /// <summary>
        /// The day-rule pool.
        ///
        /// Every band is deliberately WIDE and its threshold LOW relative to the real catalog
        /// (85 cards: 8/9/15/21/16/9/7 at rarity 1-7). A narrow band like "5+ at exactly 7 stars"
        /// would be unclearable for most rosters and would silently cost a player their daily
        /// circuit - the Collection Trial is meant to ask "do you have a spread of cards", not to
        /// gate the day behind a specific pull. The satisfiability test pins that intent.
        /// </summary>
        public static readonly IReadOnlyList<SoloCircuitRarityBand> Bands = new[]
        {
            new SoloCircuitRarityBand(1, 3, 5),
            new SoloCircuitRarityBand(2, 4, 5),
            new SoloCircuitRarityBand(3, 5, 5),
            new SoloCircuitRarityBand(4, 7, 5),
            new SoloCircuitRarityBand(1, 7, 8),
            new SoloCircuitRarityBand(3, 7, 5),
        };

        /// <summary>Today's rule. Same UTC day, same band, every device - the same determinism
        /// contract the other two trials use.</summary>
        public static SoloCircuitRarityBand BandFor(string dayKeyUtc)
        {
            int index = SoloCircuitDailySeed.IndexFor(dayKeyUtc, SoloCircuitTrial.Collection, Bands.Count);
            return index < 0 ? Bands[0] : Bands[index];
        }

        /// <summary>
        /// How many owned cards fall inside the band.
        ///
        /// Counts DISTINCT owned card ids, not copies: owning five copies of one card is not a
        /// collection, and counting copyCount would let a single lucky duplicate streak clear the
        /// trial that is specifically about breadth.
        /// </summary>
        public static int CountMatching(
            IEnumerable<string> ownedCardIds,
            Func<string, int> rarityOf,
            SoloCircuitRarityBand band)
        {
            if (ownedCardIds == null || rarityOf == null) return 0;

            var counted = new HashSet<string>(StringComparer.Ordinal);
            int matches = 0;
            foreach (string id in ownedCardIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                if (!counted.Add(id)) continue;

                int rarity = rarityOf(id);
                if (rarity <= 0) continue;          // unknown id - never counts toward a reward
                if (band.Includes(rarity)) matches++;
            }

            return matches;
        }

        /// <summary>True when the roster satisfies the day's rule.</summary>
        public static bool IsSatisfied(
            IEnumerable<string> ownedCardIds,
            Func<string, int> rarityOf,
            SoloCircuitRarityBand band) =>
            CountMatching(ownedCardIds, rarityOf, band) >= band.RequiredCards;
    }
}

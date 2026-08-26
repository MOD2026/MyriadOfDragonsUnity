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

        /// <summary>
        /// Minimum cards a fallback rule asks for. Lower than the standard 5 because the fallback
        /// only fires for a roster that could not satisfy ANY normal band - asking 5 of a player
        /// who owns almost nothing would reproduce the exact lockout this rotation exists to fix.
        /// </summary>
        public const int FallbackRequiredCards = 3;

        /// <summary>
        /// Today's rule for a player who owns nothing in particular - the pure date pick.
        ///
        /// KEPT for display/preview paths that have no roster to hand, but it is NOT what scores a
        /// trial. Use the roster-aware overload wherever the answer must match what the player is
        /// actually judged against.
        /// </summary>
        public static SoloCircuitRarityBand BandFor(string dayKeyUtc)
        {
            int index = SoloCircuitDailySeed.IndexFor(dayKeyUtc, SoloCircuitTrial.Collection, Bands.Count);
            return index < 0 ? Bands[0] : Bands[index];
        }

        /// <summary>
        /// Today's rule for THIS player's roster - eligibility-aware rotation (LOCKED 2026-08-26, BS).
        ///
        /// WHY THIS IS NOT A PURE DATE HASH ANY MORE: measured against the real starter grant, the
        /// (4,7,5) band is unclearable for a new player - the starter roster tops out at rarity 3,
        /// so 1 day in 6 was impossible through no fault of theirs. Because the 7-day cycle needs
        /// CONSECUTIVE complete days, that pushed ~72% of new players out of their first weekly
        /// bonus. A band nobody can clear is not a challenge, it is a silent tax on new accounts.
        ///
        /// So a band only enters rotation when the roster can actually satisfy it, and the day's
        /// hash picks from the ELIGIBLE set. Determinism is preserved in the sense that matters:
        /// same day AND same roster always gives the same rule. It is no longer the same rule for
        /// every player on a given day, which is the deliberate trade.
        ///
        /// If nothing at all is eligible - rare with an 85-card catalog, but real for a brand-new
        /// or heavily-burned roster - a deterministic fallback asks for 3 cards from the player's
        /// LOWEST owned rarity. Same reward, still counts toward the cycle: the fix for "new players
        /// cannot clear it" must not quietly become "new players get paid less".
        /// </summary>
        public static SoloCircuitRarityBand BandFor(
            string dayKeyUtc, IEnumerable<string> ownedCardIds, Func<string, int> rarityOf)
        {
            if (ownedCardIds == null || rarityOf == null) return BandFor(dayKeyUtc);

            var eligible = new List<SoloCircuitRarityBand>();
            foreach (SoloCircuitRarityBand band in Bands)
            {
                if (IsSatisfied(ownedCardIds, rarityOf, band)) eligible.Add(band);
            }

            if (eligible.Count == 0) return FallbackBandFor(ownedCardIds, rarityOf);

            int index = SoloCircuitDailySeed.IndexFor(
                dayKeyUtc, SoloCircuitTrial.Collection, eligible.Count);
            return index < 0 ? eligible[0] : eligible[index];
        }

        /// <summary>
        /// Deterministic last-resort rule: 3 cards at the player's lowest owned rarity.
        ///
        /// Uses the LOWEST owned rarity rather than the highest, because the highest is exactly what
        /// a thin roster has least of - picking it would produce another unclearable day. Returns a
        /// 1-1/3 band for an empty roster, which is honestly unclearable, but a player owning zero
        /// cards cannot play the game at all and that is not this rule's problem to paper over.
        /// </summary>
        public static SoloCircuitRarityBand FallbackBandFor(
            IEnumerable<string> ownedCardIds, Func<string, int> rarityOf)
        {
            int lowest = int.MaxValue;
            if (ownedCardIds != null && rarityOf != null)
            {
                var counted = new HashSet<string>(StringComparer.Ordinal);
                foreach (string id in ownedCardIds)
                {
                    if (string.IsNullOrEmpty(id) || !counted.Add(id)) continue;
                    int rarity = rarityOf(id);
                    if (rarity > 0 && rarity < lowest) lowest = rarity;
                }
            }

            if (lowest == int.MaxValue) lowest = 1;
            return new SoloCircuitRarityBand(lowest, lowest, FallbackRequiredCards);
        }

        /// <summary>True when the roster can satisfy the high (4-7) band - the specific gate BS's
        /// ruling names. Exposed so a caller can explain WHY a rule is or is not showing.</summary>
        public static bool QualifiesForHighRarityBand(
            IEnumerable<string> ownedCardIds, Func<string, int> rarityOf) =>
            IsSatisfied(ownedCardIds, rarityOf, new SoloCircuitRarityBand(4, 7, 5));

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

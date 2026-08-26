using System;
using System.Collections.Generic;

namespace MyriadOfDragons.Empire
{
    /// <summary>
    /// Deterministic UTC-seeded selection for the Circuit's daily trials.
    ///
    /// "Deterministic" here means: the same UTC day always produces the same trial content, on
    /// every device, with no server call and nothing stored. That is what lets the Circuit be a
    /// shared daily without any backend - two players on the same UTC day get the same Formation
    /// restriction and the same Tactical Brief.
    ///
    /// Uses an explicit FNV-1a hash of the day key rather than System.Random seeded with a derived
    /// int. Random's sequence is an implementation detail that is not contractually stable across
    /// runtimes, and "yesterday's puzzle changed after a Unity upgrade" is a silent, untraceable
    /// bug. This hash is fixed arithmetic and will produce the same value forever.
    /// </summary>
    public static class SoloCircuitDailySeed
    {
        /// <summary>FNV-1a over the UTF-16 code units of the day key. Stable by construction.</summary>
        public static uint SeedFor(string dayKeyUtc)
        {
            unchecked
            {
                const uint offsetBasis = 2166136261;
                const uint prime = 16777619;
                uint hash = offsetBasis;
                if (string.IsNullOrEmpty(dayKeyUtc)) return hash;
                for (int i = 0; i < dayKeyUtc.Length; i++)
                {
                    hash ^= dayKeyUtc[i];
                    hash *= prime;
                }

                return hash;
            }
        }

        /// <summary>
        /// Picks one index in [0, count) for the given day and trial.
        ///
        /// The trial is mixed into the seed so the three trials do not move in lockstep - without
        /// it, the day that selects the first Formation rule would also always select the first
        /// puzzle, and the Circuit would feel far more repetitive than the content warrants.
        /// </summary>
        public static int IndexFor(string dayKeyUtc, SoloCircuitTrial trial, int count)
        {
            if (count <= 0) return -1;
            unchecked
            {
                uint hash = SeedFor(dayKeyUtc);
                hash ^= (uint)((int)trial + 1) * 2654435761u;
                return (int)(hash % (uint)count);
            }
        }

        /// <summary>
        /// The Formation Trial's daily restriction pool.
        ///
        /// Deliberately phrased as constraints on how the player deploys, never on WHICH cards they
        /// own - owning the right cards is the Collection Trial's axis, and duplicating it here
        /// would make both trials fail together for a thin roster, which is the opposite of the
        /// engagement the Circuit is for.
        /// </summary>
        public static readonly IReadOnlyList<string> FormationRestrictions = new[]
        {
            "Front lane only - no units may be deployed to the Middle or Back lane.",
            "Back lane only - no units may be deployed to the Front or Middle lane.",
            "No more than one unit per lane.",
            // Replaced a duplicate 2026-08-26: this slot previously read "Every deployed unit must
            // sit in a different lane", which is the SAME CONSTRAINT as "No more than one unit per
            // lane" above - so the pool advertised six rules while only offering five, and that one
            // came up twice as often as any other. A Resource ceiling is genuinely distinct from
            // the positional rules and fits the trial's "command of the available ranks" framing.
            "Clear using no more than one full bar of Resource.",
            "Win without deploying to the Middle lane.",
            "Deploy at most three units for the whole battle.",
        };

        /// <summary>Today's Formation restriction. Same day, same rule, every device.</summary>
        public static string FormationRestrictionFor(string dayKeyUtc)
        {
            int index = IndexFor(dayKeyUtc, SoloCircuitTrial.Formation, FormationRestrictions.Count);
            return index < 0 ? string.Empty : FormationRestrictions[index];
        }

        /// <summary>
        /// Today's Tactical Brief, chosen from whatever puzzle ids the caller passes in.
        ///
        /// Takes the id list rather than reaching into TacticalPuzzleLibrary itself, so this stays
        /// a pure function and a test can pin "the same day always selects the same puzzle" without
        /// depending on the shipped library's contents. That matters: the assertion must not start
        /// failing the day someone adds puzzle #7.
        /// </summary>
        public static string TacticalBriefFor(string dayKeyUtc, IReadOnlyList<string> puzzleIds)
        {
            if (puzzleIds == null || puzzleIds.Count == 0) return string.Empty;
            int index = IndexFor(dayKeyUtc, SoloCircuitTrial.TacticalBrief, puzzleIds.Count);
            return index < 0 ? string.Empty : puzzleIds[index];
        }
    }
}

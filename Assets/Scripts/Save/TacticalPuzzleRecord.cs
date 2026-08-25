using System;

namespace MyriadOfDragons.Save
{
    /// <summary>
    /// One solved Tactical Puzzle, as stored in <see cref="PlayerProfile.tacticalPuzzleRecords"/>.
    /// Owner-cleared 2026-08-25 after CC vetted and locked this exact shape (register 5fbc2f1).
    ///
    /// A record EXISTS only for a solved puzzle - that is the completion flag, so there is no
    /// separate boolean to keep in sync with it.
    ///
    /// EVERY BEST FIELD DEFAULTS TO -1, NOT 0, AND THIS MATTERS MORE THAN IT LOOKS. Today a record
    /// is only ever written by a solve, which sets all of them. But the moment a new field is added
    /// to this type, every record already on disk deserializes that field as 0 - and 0 orders used
    /// reads as a PERFECT score, making every stored best permanently unbeatable. -1 means
    /// "unknown"; ranking code must treat it as no information, never as a good result.
    /// This is the same trap that <see cref="PlayerProfile.memoryExpeditionFirstSelectedTile"/>
    /// documents, one level deeper - inside a list element rather than at the top level.
    ///
    /// Kept in its own file rather than nested in the frozen PlayerProfile.cs so the record can
    /// gain a field later without another edit to a frozen file.
    /// </summary>
    [Serializable]
    public class TacticalPuzzleRecord
    {
        /// <summary>Stable id from the puzzle definition. The ONLY key - never a slot index.</summary>
        public string puzzleId;

        /// <summary>Fewest orders used in a solving attempt - the primary decision-quality signal
        /// the locked design names. -1 = unknown.</summary>
        public int bestActionsUsed = -1;

        /// <summary>Resource left over on that attempt; the tiebreak. -1 = unknown.</summary>
        public int bestResourceRemaining = -1;

        /// <summary>Friendly units still alive on that attempt. -1 = unknown.</summary>
        public int bestUnitsPreserved = -1;

        /// <summary>Lanes still held on that attempt. -1 = unknown.</summary>
        public int bestLanesHeld = -1;

        /// <summary>UTC date (yyyy-MM-dd) of the first solve. Empty = unknown. Lets a "best this
        /// cycle" view be derived later in app logic with no schema change, which is why no
        /// cycle-key field was added - completions accumulate.</summary>
        public string firstSolvedUtcDate = string.Empty;

        /// <summary>True when a stored best carries no ranking information and must not be
        /// presented as a score.</summary>
        public bool HasRankingInfo => bestActionsUsed >= 0;
    }
}

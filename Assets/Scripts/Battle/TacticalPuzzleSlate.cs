using System.Collections.Generic;
using System.Linq;

namespace MyriadOfDragons.Battle
{
    public enum TacticalPuzzleSlotState
    {
        Locked,
        Available,
        Completed,
    }

    /// <summary>
    /// The locked narrative framing, in one place so no screen invents its own wording.
    /// Register: "Tactical Puzzle narrative framing - FULLY LOCKED".
    ///
    /// The per-puzzle labels are PROVISIONAL by decision, not by oversight - they stand in until
    /// real story-bible terminology is confirmed. Kept here rather than inlined in the presenter
    /// so that swap is one edit in one file.
    /// </summary>
    public static class TacticalPuzzleCopy
    {
        public const string ScreenTitle = "WAR-ROOM RECONSTRUCTIONS";

        public const string Intro =
            "The outcome is known. The better command is not. Study the position, test your " +
            "orders, and find the line that preserves the Empire's strength.";

        /// <summary>Provisional slot labels. Cycled per index; NOT story-bible terminology yet.</summary>
        private static readonly string[] ProvisionalNames =
        {
            "Recon Record",
            "Battle Reconstruction",
            "Tactical Brief",
        };

        public static string ProvisionalNameFor(int index) =>
            ProvisionalNames[((index % ProvisionalNames.Length) + ProvisionalNames.Length) % ProvisionalNames.Length];

        public static string SlotLabel(int index) =>
            ProvisionalNameFor(index) + " " + (index + 1);

        public const string SolvedLine = "POSITION SOLVED";
        public const string UnsolvedLine = "THE LINE HOLDS ELSEWHERE";
        public const string LockedLine = "SEALED UNTIL THE PRECEDING RECORD IS STUDIED";
    }

    public sealed class TacticalPuzzleSlot
    {
        public TacticalPuzzleDefinition Definition;
        public TacticalPuzzleSlotState State = TacticalPuzzleSlotState.Locked;
        public string Label;

        /// <summary>Best score inputs from the completing attempt, for the entry list. Null until
        /// solved.</summary>
        public TacticalPuzzleResult BestResult;
    }

    /// <summary>
    /// The set of puzzle slots a player can see, and which of them are open.
    ///
    /// PROGRESS IS IN-MEMORY ONLY, DELIBERATELY. Persisting "which puzzles are solved" needs a
    /// PlayerProfile field, and PlayerProfile is a FROZEN file - a shape change there needs the
    /// human to coordinate both seats. So completion survives the screen but not the app, and this
    /// is flagged rather than worked around: writing a save field unilaterally is the one thing
    /// the seat rules forbid outright, and quietly faking persistence would be worse than not
    /// having it.
    ///
    /// Unlock rule is sequential - slot N opens when slot N-1 is completed - which is a STRUCTURAL
    /// choice, not a content one: it needs no numbers and no schedule. If the design later wants
    /// all slots open at once, or a date gate, that replaces this method and nothing else.
    /// </summary>
    public sealed class TacticalPuzzleSlate
    {
        private readonly List<TacticalPuzzleSlot> _slots = new List<TacticalPuzzleSlot>();

        public IReadOnlyList<TacticalPuzzleSlot> Slots => _slots;

        public int CompletedCount => _slots.Count(s => s.State == TacticalPuzzleSlotState.Completed);

        public TacticalPuzzleSlate(IEnumerable<TacticalPuzzleDefinition> definitions)
        {
            int i = 0;
            foreach (TacticalPuzzleDefinition def in definitions ?? Enumerable.Empty<TacticalPuzzleDefinition>())
            {
                if (def == null) continue;
                _slots.Add(new TacticalPuzzleSlot
                {
                    Definition = def,
                    Label = string.IsNullOrWhiteSpace(def.DisplayName)
                        ? TacticalPuzzleCopy.SlotLabel(i)
                        : def.DisplayName,
                    State = i == 0 ? TacticalPuzzleSlotState.Available : TacticalPuzzleSlotState.Locked,
                });
                i++;
            }
        }

        public TacticalPuzzleSlot SlotAt(int index) =>
            index >= 0 && index < _slots.Count ? _slots[index] : null;

        public bool CanOpen(int index) =>
            SlotAt(index)?.State != TacticalPuzzleSlotState.Locked && SlotAt(index) != null;

        /// <summary>
        /// Records a finished attempt. Only a SOLVED attempt completes a slot and unlocks the
        /// next; an unsolved one changes nothing, so a player can walk away and come back without
        /// being penalised for having tried.
        /// </summary>
        public void RecordAttempt(int index, TacticalPuzzleResult result)
        {
            TacticalPuzzleSlot slot = SlotAt(index);
            if (slot == null || result == null || !result.Solved) return;

            slot.State = TacticalPuzzleSlotState.Completed;

            // Keep the better attempt: fewer orders is the primary decision-quality signal the
            // locked design names, with Resource left over as the tiebreak. This ORDERS attempts;
            // it does not score them - turning these into points is a design decision.
            if (slot.BestResult == null ||
                result.ActionsUsed < slot.BestResult.ActionsUsed ||
                (result.ActionsUsed == slot.BestResult.ActionsUsed &&
                 result.ResourceRemaining > slot.BestResult.ResourceRemaining))
            {
                slot.BestResult = result;
            }

            TacticalPuzzleSlot next = SlotAt(index + 1);
            if (next != null && next.State == TacticalPuzzleSlotState.Locked)
                next.State = TacticalPuzzleSlotState.Available;
        }
    }
}

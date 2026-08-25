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

        /// <summary>Best orders-used loaded from a previous session. -1 = unknown, which must be
        /// rendered as "no record" rather than as a perfect score.</summary>
        public int SavedBestActions = -1;
    }

    /// <summary>
    /// The set of puzzle slots a player can see, and which of them are open.
    ///
    /// PROGRESS PERSISTS as of 2026-08-25: PlayerProfile.tacticalPuzzleRecords, added after the
    /// field list was proposed, vetted and locked (register 5fbc2f1) rather than written
    /// unilaterally into a frozen file.
    ///
    /// ONLY COMPLETIONS AND BEST SCORES ARE STORED. Locked/available state is recomputed from them
    /// on load by the same rule that produces it during play, so a later change to the unlock rule
    /// applies to existing saves instead of leaving them inconsistent with the code.
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

        /// <summary>
        /// Marks slots solved from saved records and re-derives which are open. Safe to call on a
        /// profile that has never seen this mode - an absent record simply means "not solved".
        /// </summary>
        public void ApplySavedProgress(MyriadOfDragons.Save.PlayerProfile profile)
        {
            if (profile == null) return;

            foreach (TacticalPuzzleSlot slot in _slots)
            {
                MyriadOfDragons.Save.TacticalPuzzleRecord record =
                    profile.FindTacticalPuzzleRecord(slot.Definition?.PuzzleId);
                if (record == null) continue;

                slot.State = TacticalPuzzleSlotState.Completed;
                slot.SavedBestActions = record.HasRankingInfo ? record.bestActionsUsed : -1;
            }

            RederiveAvailability();
        }

        /// <summary>
        /// Recomputes locked/available from completions. Availability is DERIVED, never stored -
        /// that is why a rule change here reaches old saves too.
        /// </summary>
        private void RederiveAvailability()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].State == TacticalPuzzleSlotState.Completed) continue;

                bool open = i == 0 || _slots[i - 1].State == TacticalPuzzleSlotState.Completed;
                _slots[i].State = open ? TacticalPuzzleSlotState.Available : TacticalPuzzleSlotState.Locked;
            }
        }

        /// <summary>
        /// Writes a solved attempt into the profile, keeping the better record. Does nothing for an
        /// unsolved attempt, so walking away never writes anything.
        ///
        /// The caller saves; this only shapes the data. Keeping the write and the save separate is
        /// what lets a test assert the record without touching the disk.
        /// </summary>
        public void WriteProgress(int index, MyriadOfDragons.Save.PlayerProfile profile,
            TacticalPuzzleResult result, string utcDate)
        {
            TacticalPuzzleSlot slot = SlotAt(index);
            if (profile == null || slot == null || result == null || !result.Solved) return;
            if (profile.tacticalPuzzleRecords == null)
                profile.tacticalPuzzleRecords = new List<MyriadOfDragons.Save.TacticalPuzzleRecord>();

            string id = slot.Definition?.PuzzleId;
            if (string.IsNullOrEmpty(id)) return;

            MyriadOfDragons.Save.TacticalPuzzleRecord record = profile.FindTacticalPuzzleRecord(id);
            if (record == null)
            {
                record = new MyriadOfDragons.Save.TacticalPuzzleRecord
                {
                    puzzleId = id,
                    firstSolvedUtcDate = utcDate ?? string.Empty,
                };
                profile.tacticalPuzzleRecords.Add(record);
            }

            // A stored best with no ranking info (-1) must lose to any real result rather than
            // beating it - -1 is "unknown", not "zero orders used".
            bool better = !record.HasRankingInfo ||
                          result.ActionsUsed < record.bestActionsUsed ||
                          (result.ActionsUsed == record.bestActionsUsed &&
                           result.ResourceRemaining > record.bestResourceRemaining);

            if (better)
            {
                record.bestActionsUsed = result.ActionsUsed;
                record.bestResourceRemaining = result.ResourceRemaining;
                record.bestUnitsPreserved = result.FriendlyUnitsAlive;
                record.bestLanesHeld = result.LanesHeld;
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

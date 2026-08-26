using MyriadOfDragons.Save;

namespace MyriadOfDragons.Empire
{
    /// <summary>
    /// Bridges a REAL completion signal into a Circuit trial clear.
    ///
    /// Exists as a plain function taking an explicit signal, rather than scanning the save for
    /// evidence a trial was completed, because THE SAVE CANNOT ANSWER THAT QUESTION:
    /// <see cref="TacticalPuzzleRecord.firstSolvedUtcDate"/> records only the FIRST solve. A player
    /// who solved today's selected puzzle last week has a record whose date is last week's, and a
    /// player re-solving it today writes nothing new. So "does a record exist" over-counts (credits
    /// an old solve) and "is the record dated today" under-counts (a re-solve never clears the
    /// trial). Neither is acceptable for a daily reward, so completion must be reported by whoever
    /// actually observed the solve.
    ///
    /// That is a real limitation of the existing record shape, not a shortcoming of this class -
    /// and it is deliberately NOT fixed by adding a field to the record, because completions
    /// accumulating without a per-day key is a documented design choice there.
    /// </summary>
    public static class SoloCircuitCompletion
    {
        /// <summary>
        /// Clears the Tactical Brief if the puzzle just solved is the one today's Circuit selected.
        ///
        /// Takes the day's puzzle pool so selection is computed from the same deterministic seed the
        /// screen displays - the trial must be scored against the rule the player was actually
        /// shown, never a separately-derived one.
        /// </summary>
        public static SoloCircuitClearResult ReportPuzzleSolved(
            SoloCircuitProgress progress,
            System.Collections.Generic.IReadOnlyList<string> todaysPuzzleIds,
            string solvedPuzzleId,
            System.DateTime nowUtc)
        {
            var refused = new SoloCircuitClearResult { Trial = SoloCircuitTrial.TacticalBrief };

            if (progress == null || string.IsNullOrEmpty(solvedPuzzleId))
            {
                refused.Message = "No solve to report.";
                return refused;
            }

            string dayKey = SoloCollectionCircuit.UtcDayKey(nowUtc);
            string briefId = SoloCircuitDailySeed.TacticalBriefFor(dayKey, todaysPuzzleIds);

            if (string.IsNullOrEmpty(briefId))
            {
                refused.Message = "No Tactical Brief is selected today - the puzzle library is empty.";
                return refused;
            }

            if (briefId != solvedPuzzleId)
            {
                // Solving SOME puzzle is not solving TODAY'S puzzle. Crediting any solve would let a
                // player clear the brief from the free-play War Room entry without ever engaging
                // with the day's selection.
                refused.Message =
                    "Solved " + solvedPuzzleId + ", but today's Tactical Brief is " + briefId + ".";
                return refused;
            }

            return SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.TacticalBrief, nowUtc);
        }

        /// <summary>
        /// Clears the Formation Trial if a battle was WON under today's restriction.
        ///
        /// Takes the deployment log rather than the final board, for the reason
        /// SoloCircuitFormationRule documents: a unit deployed and then lost is invisible at match
        /// end, so a cumulative rule judged from the end state would silently pass someone who
        /// broke it.
        ///
        /// Derives the restriction from the same deterministic seed the SCREEN displays. The rule
        /// the player was shown and the rule they are scored against must come from one source;
        /// deriving it separately here is exactly how the two drift apart.
        /// </summary>
        public static SoloCircuitClearResult ReportBattleFinished(
            SoloCircuitProgress progress,
            System.Collections.Generic.IReadOnlyList<SoloCircuitDeployment> deployments,
            bool isVictory,
            System.DateTime nowUtc)
        {
            var refused = new SoloCircuitClearResult { Trial = SoloCircuitTrial.Formation };
            if (progress == null)
            {
                refused.Message = "No circuit progress.";
                return refused;
            }

            string restriction =
                SoloCircuitDailySeed.FormationRestrictionFor(SoloCollectionCircuit.UtcDayKey(nowUtc));

            if (!SoloCircuitFormationRule.IsCleared(restriction, deployments, isVictory))
            {
                refused.Message = isVictory
                    ? "Won, but not under today's restriction: " + restriction
                    : "The Formation Trial needs a win.";
                return refused;
            }

            return SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Formation, nowUtc);
        }

        /// <summary>
        /// Clears the Collection Trial if the roster satisfies today's rule.
        ///
        /// Unlike the Brief, this one CAN be evaluated from the save alone - ownership is a
        /// standing fact, not an event - so it takes the roster directly rather than a signal.
        /// </summary>
        public static SoloCircuitClearResult ReportCollectionChecked(
            SoloCircuitProgress progress,
            System.Collections.Generic.IEnumerable<string> ownedCardIds,
            System.Func<string, int> rarityOf,
            System.DateTime nowUtc)
        {
            var refused = new SoloCircuitClearResult { Trial = SoloCircuitTrial.Collection };
            if (progress == null)
            {
                refused.Message = "No circuit progress.";
                return refused;
            }

            SoloCircuitRarityBand band =
                SoloCircuitCollectionRule.BandFor(SoloCollectionCircuit.UtcDayKey(nowUtc));

            if (!SoloCircuitCollectionRule.IsSatisfied(ownedCardIds, rarityOf, band))
            {
                int have = SoloCircuitCollectionRule.CountMatching(ownedCardIds, rarityOf, band);
                refused.Message =
                    "Roster does not meet today's rule (" + have + " of " + band.RequiredCards + "). " +
                    band.Describe();
                return refused;
            }

            return SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Collection, nowUtc);
        }
    }
}

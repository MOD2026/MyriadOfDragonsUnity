using System.Collections.Generic;

namespace MyriadOfDragons.Battle
{
    /// <summary>
    /// Where the game asks for the puzzles a player can currently see.
    ///
    /// IT RETURNS NOTHING TODAY, AND THAT IS THE HONEST STATE. Puzzle content is a separate,
    /// BS-directed design pass that has not landed, and inventing a puzzle here to make the screen
    /// look populated would quietly become the content nobody agreed to - the exact failure mode
    /// the whole authoring layer was built to avoid.
    ///
    /// So this is a real seam, not a stub with fake data: the entry point, the screen, the session
    /// and the verifier are all wired end to end and exercised by tests. The only missing piece is
    /// authored definitions, and when they arrive they are supplied HERE - one method, no UI change.
    ///
    /// Whatever eventually feeds this (a shipped asset, a seeded daily generator, a downloaded
    /// weekly set) must return definitions that pass TacticalPuzzleAuthoring.Validate and whose
    /// envelopes pass CheckEnvelope. Nothing downstream re-checks that for you.
    /// </summary>
    public static class TacticalPuzzleLibrary
    {
        private static IReadOnlyList<TacticalPuzzleDefinition> _overrideForTests;

        /// <summary>The puzzles to show on the entry screen, in slot order.</summary>
        public static IReadOnlyList<TacticalPuzzleDefinition> AvailablePuzzles() =>
            _overrideForTests ?? System.Array.Empty<TacticalPuzzleDefinition>();

        /// <summary>Test-only: supply a puzzle set so the screen can be exercised end to end
        /// without shipping content to do it.</summary>
        public static void SetPuzzlesForTests(IReadOnlyList<TacticalPuzzleDefinition> puzzles) =>
            _overrideForTests = puzzles;

        public static void ClearPuzzlesForTests() => _overrideForTests = null;

        /// <summary>True when there is nothing to play, so a screen can say so plainly instead of
        /// rendering an empty row that reads as a bug.</summary>
        public static bool IsEmpty => AvailablePuzzles().Count == 0;
    }
}

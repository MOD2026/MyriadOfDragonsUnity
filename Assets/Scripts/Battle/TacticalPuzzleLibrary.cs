using System.Collections.Generic;
using UnityEngine;

namespace MyriadOfDragons.Battle
{
    /// <summary>JsonUtility cannot parse a top-level array - this wraps one, same as
    /// <see cref="Cards.CardDataList"/> does for the card data.</summary>
    [System.Serializable]
    public class TacticalPuzzleDefinitionList
    {
        public List<TacticalPuzzleDefinition> puzzles = new List<TacticalPuzzleDefinition>();
    }

    /// <summary>
    /// Where the game asks for the puzzles a player can currently see, and the only place that
    /// reads authored puzzle content off disk.
    ///
    /// THERE IS STILL NO CONTENT, AND NONE IS INVENTED HERE. The resource file this reads does not
    /// exist yet - the design pass that writes it is separate. What changed is that content is now
    /// a DATA drop rather than a code change: when authored puzzles arrive, they land as
    /// Resources/Data/tactical_puzzles.json and this picks them up with nothing else edited.
    ///
    /// A DEFINITION THAT FAILS VALIDATION IS SKIPPED, NOT SHOWN. An incoherent puzzle can be
    /// unsolvable - a lane over capacity, an objective pointing at no unit, a card id that does not
    /// resolve - and showing one would hand a player a position they cannot win and no way to know
    /// why. Skipping is loud (one error per problem, naming the puzzle) so a broken puzzle is
    /// impossible to miss in the log, rather than silently absent.
    ///
    /// Structural validation only. CheckEnvelope - replaying an author's claimed lines through the
    /// real verifier - is a CI/editor concern, not something to run on every screen open: it
    /// replays every line of every puzzle. <see cref="ValidateAllEnvelopes"/> exists for a test to
    /// call over shipped content.
    /// </summary>
    public static class TacticalPuzzleLibrary
    {
        /// <summary>Resources path (no extension) of the authored puzzle set.</summary>
        public const string ResourcePath = "Data/tactical_puzzles";

        private static IReadOnlyList<TacticalPuzzleDefinition> _overrideForTests;
        private static List<TacticalPuzzleDefinition> _loaded;
        private static bool _loadAttempted;

        /// <summary>The puzzles to show on the entry screen, in authored order.</summary>
        public static IReadOnlyList<TacticalPuzzleDefinition> AvailablePuzzles()
        {
            if (_overrideForTests != null) return _overrideForTests;

            if (!_loadAttempted) Load();
            return (IReadOnlyList<TacticalPuzzleDefinition>)_loaded ??
                   System.Array.Empty<TacticalPuzzleDefinition>();
        }

        /// <summary>
        /// Reads and validates the authored set. A missing file is the EXPECTED state until the
        /// content pass lands, so it is not an error - it just means no puzzles, which the entry
        /// screen already renders honestly.
        /// </summary>
        private static void Load()
        {
            _loadAttempted = true;
            _loaded = new List<TacticalPuzzleDefinition>();

            TextAsset json = Resources.Load<TextAsset>(ResourcePath);
            if (json == null) return;   // no content authored yet - not a failure

            TacticalPuzzleDefinitionList parsed;
            try
            {
                parsed = JsonUtility.FromJson<TacticalPuzzleDefinitionList>(json.text);
            }
            catch (System.Exception e)
            {
                Debug.LogError("TacticalPuzzleLibrary: " + ResourcePath + " is not valid JSON - " +
                               "no puzzles loaded. " + e.Message);
                return;
            }

            if (parsed?.puzzles == null)
            {
                Debug.LogError("TacticalPuzzleLibrary: " + ResourcePath +
                               " parsed but contained no 'puzzles' array.");
                return;
            }

            var seenIds = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (TacticalPuzzleDefinition def in parsed.puzzles)
            {
                if (def == null) continue;

                List<string> problems = TacticalPuzzleAuthoring.Validate(def);
                if (problems.Count > 0)
                {
                    Debug.LogError("TacticalPuzzleLibrary: skipping puzzle '" +
                                   (def.PuzzleId ?? "(no id)") + "' - " +
                                   string.Join("  |  ", problems));
                    continue;
                }

                // A duplicate id would make two puzzles share one save record, so the second would
                // read as already solved the moment the first was.
                if (!seenIds.Add(def.PuzzleId))
                {
                    Debug.LogError("TacticalPuzzleLibrary: skipping puzzle '" + def.PuzzleId +
                                   "' - duplicate PuzzleId. Ids key save records and must be unique.");
                    continue;
                }

                _loaded.Add(def);
            }
        }

        /// <summary>
        /// Replays every authored envelope through the real verifier and returns the mismatches,
        /// keyed by puzzle id. For a CI/editor check over shipped content - NOT for runtime.
        /// Empty result = every puzzle behaves as its author claimed.
        /// </summary>
        public static Dictionary<string, List<TacticalPuzzleEnvelopeMismatch>> ValidateAllEnvelopes()
        {
            var byPuzzle = new Dictionary<string, List<TacticalPuzzleEnvelopeMismatch>>();
            foreach (TacticalPuzzleDefinition def in AvailablePuzzles())
            {
                List<TacticalPuzzleEnvelopeMismatch> mismatches =
                    TacticalPuzzleAuthoring.CheckEnvelope(def);
                if (mismatches.Count > 0) byPuzzle[def.PuzzleId] = mismatches;
            }

            return byPuzzle;
        }

        /// <summary>Test-only: supply a puzzle set so the screen can be exercised end to end
        /// without shipping content to do it.</summary>
        public static void SetPuzzlesForTests(IReadOnlyList<TacticalPuzzleDefinition> puzzles) =>
            _overrideForTests = puzzles;

        public static void ClearPuzzlesForTests() => _overrideForTests = null;

        /// <summary>Test-only: drop the cached load so the next call re-reads from Resources.
        /// The whole EditMode suite shares one process, so without this a test that changes what
        /// is on disk would keep seeing the first load.</summary>
        public static void ResetCacheForTests()
        {
            _loaded = null;
            _loadAttempted = false;
        }

        /// <summary>True when there is nothing to play, so a screen can say so plainly instead of
        /// rendering an empty row that reads as a bug.</summary>
        public static bool IsEmpty => AvailablePuzzles().Count == 0;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Cards;

namespace MyriadOfDragons.Battle
{
    /// <summary>A materialised puzzle: two live battle states plus the coordinate lookup that
    /// turns authored unit references back into the instances the verifier needs.</summary>
    public sealed class MaterializedPuzzle
    {
        public PlayerBattleState PlayerSide;
        public PlayerBattleState EnemySide;

        /// <summary>Authored order, per side and lane, captured at materialisation. Held
        /// separately from the LaneState lists because actions mutate those - a Windstep moves a
        /// unit to another lane, which would otherwise renumber every later reference mid-run.
        /// Coordinates always mean "where this unit STARTED".</summary>
        private readonly Dictionary<string, BattleCardInstance> _byCoordinate =
            new Dictionary<string, BattleCardInstance>();

        internal void Register(TacticalPuzzleSide side, Lane lane, int index, BattleCardInstance unit) =>
            _byCoordinate[Key(side, lane, index)] = unit;

        private static string Key(TacticalPuzzleSide side, Lane lane, int index) =>
            (int)side + ":" + (int)lane + ":" + index;

        /// <summary>Returns null when the reference points at nothing - callers report that as an
        /// authoring problem rather than throwing, so a validation pass can list every bad
        /// reference at once instead of dying on the first.</summary>
        public BattleCardInstance Resolve(TacticalPuzzleUnitRef unitRef)
        {
            if (unitRef == null) return null;
            return _byCoordinate.TryGetValue(Key(unitRef.Side, unitRef.Lane, unitRef.IndexInLane), out var unit)
                ? unit
                : null;
        }
    }

    /// <summary>One mismatch between what an author claimed and what the verifier actually
    /// did.</summary>
    public sealed class TacticalPuzzleEnvelopeMismatch
    {
        public int ExpectationIndex;
        public string Description;
        public TacticalPuzzleStatus Expected;
        public TacticalPuzzleStatus Actual;
        public string Detail;

        public override string ToString() =>
            "[" + ExpectationIndex + "] " + (string.IsNullOrEmpty(Description) ? "(unnamed)" : Description) +
            ": expected " + Expected + ", got " + Actual +
            (string.IsNullOrEmpty(Detail) ? "" : " - " + Detail);
    }

    /// <summary>
    /// Turns a <see cref="TacticalPuzzleDefinition"/> into something
    /// <see cref="TacticalPuzzleVerifier"/> can run, and checks that the definition says what its
    /// author thinks it says.
    ///
    /// This is the authoring half of the Tactical Puzzle gate; the verifier was the other half.
    /// Three responsibilities, kept separate on purpose:
    ///
    ///   Validate    - is this definition COHERENT? (unknown card id, lane over capacity,
    ///                 objective missing its required field, hand index nobody could ever play).
    ///                 Reports every problem at once; an authoring tool that surfaces one error
    ///                 per save is a bad authoring tool.
    ///   Materialize - build the fixed state. Deterministic, no RNG, no clock.
    ///   CheckEnvelope - replay the author's claimed lines through the REAL verifier and report
    ///                 mismatches.
    ///
    /// A definition failing Validate is an authoring bug. A play failing verification is a puzzle
    /// working correctly. Conflating those two would make an unsolvable puzzle look like a code
    /// defect and a typo'd card id look like a hard puzzle.
    ///
    /// STRUCTURE ONLY: nothing here invents a card, a Resource amount, a clash count or a
    /// difficulty curve. Every value comes from the definition. Validation asserts internal
    /// consistency, never balance.
    /// </summary>
    public static class TacticalPuzzleAuthoring
    {
        /// <summary>
        /// Card lookup, injectable so puzzles do not hard-depend on the CardDatabase singleton.
        /// Tests pass a fixed set; production passes the database. Defaults to the database when
        /// one exists, and to a null-returning source when it does not - which surfaces as an
        /// "unknown card id" validation problem rather than a NullReferenceException at
        /// materialisation time.
        /// </summary>
        public static Func<string, Card> DefaultCardSource =>
            id => CardDatabase.Instance != null ? CardDatabase.Instance.GetCard(id) : null;

        /// <summary>
        /// Every reason a definition is not coherent, in author-readable form. Empty list = the
        /// definition can be materialised. This does NOT mean the puzzle is solvable - that is
        /// what the envelope is for.
        /// </summary>
        public static List<string> Validate(TacticalPuzzleDefinition def, Func<string, Card> cardSource = null)
        {
            var problems = new List<string>();
            if (def == null)
            {
                problems.Add("Definition is null.");
                return problems;
            }

            cardSource = cardSource ?? DefaultCardSource;

            if (string.IsNullOrWhiteSpace(def.PuzzleId))
                problems.Add("PuzzleId is required - a seeded daily needs a stable identity.");

            // No default is invented for these; an unset cap is an authoring omission, not a
            // number this layer gets to choose.
            if (def.ResourceCap <= 0)
                problems.Add("ResourceCap must be set to a positive value by the author.");
            else if (def.StartingResource > def.ResourceCap)
                problems.Add("StartingResource " + def.StartingResource + " exceeds ResourceCap " + def.ResourceCap + ".");
            if (def.StartingResource < 0)
                problems.Add("StartingResource cannot be negative.");
            if (def.AvatarHealth <= 0)
                problems.Add("AvatarHealth must be set to a positive value by the author.");

            ValidateBoard(def.PlayerBoard, "PlayerBoard", cardSource, problems);
            ValidateBoard(def.EnemyBoard, "EnemyBoard", cardSource, problems);

            for (int i = 0; i < (def.Hand?.Count ?? 0); i++)
            {
                string id = def.Hand[i];
                if (string.IsNullOrWhiteSpace(id)) { problems.Add("Hand[" + i + "] has no card id."); continue; }
                if (cardSource(id) == null) problems.Add("Hand[" + i + "] references unknown card id '" + id + "'.");
            }

            ValidateObjective(def, problems);
            ValidateEnvelopeShape(def, problems);

            return problems;
        }

        private static void ValidateBoard(
            List<TacticalPuzzleUnitSpec> board, string label, Func<string, Card> cardSource, List<string> problems)
        {
            if (board == null) return;

            // Capacity is checked with the REAL rule (SlotWeight against LaneState.MaxSlots)
            // rather than a card count, because a lane holding two rarity-5 units is already
            // over-full at two cards while three 1-stars fit exactly.
            var slotsUsed = new Dictionary<Lane, int>();
            for (int i = 0; i < board.Count; i++)
            {
                TacticalPuzzleUnitSpec spec = board[i];
                if (spec == null) { problems.Add(label + "[" + i + "] is null."); continue; }
                if (string.IsNullOrWhiteSpace(spec.CardId))
                {
                    problems.Add(label + "[" + i + "] has no card id.");
                    continue;
                }

                Card card = cardSource(spec.CardId);
                if (card == null)
                {
                    problems.Add(label + "[" + i + "] references unknown card id '" + spec.CardId + "'.");
                    continue;
                }

                if (spec.PreDamage < 0)
                    problems.Add(label + "[" + i + "] has negative PreDamage.");
                else if (spec.PreDamage >= card.Health)
                    problems.Add(label + "[" + i + "] ('" + spec.CardId + "') starts with PreDamage " +
                                 spec.PreDamage + " against Health " + card.Health +
                                 " - it would begin the puzzle already dead.");

                slotsUsed.TryGetValue(spec.Lane, out int used);
                slotsUsed[spec.Lane] = used + card.SlotWeight;
            }

            foreach (var pair in slotsUsed)
            {
                if (pair.Value > LaneState.MaxSlots)
                    problems.Add(label + " " + pair.Key + " needs " + pair.Value + " slots, lane holds " +
                                 LaneState.MaxSlots + ".");
            }
        }

        private static void ValidateObjective(TacticalPuzzleDefinition def, List<string> problems)
        {
            TacticalPuzzleObjectiveSpec obj = def.Objective;
            if (obj == null)
            {
                problems.Add("Objective is required.");
                return;
            }

            // Each objective shape has exactly one field it cannot work without. The verifier
            // already treats an unset one as "not solved" rather than a free win, so this is a
            // second line of defence at authoring time, where the author can still fix it.
            switch (obj.Kind)
            {
                case TacticalPuzzleObjectiveKind.SurviveClashes:
                    if (obj.ClashCount <= 0)
                        problems.Add("SurviveClashes needs a positive ClashCount.");
                    break;

                case TacticalPuzzleObjectiveKind.DefeatMarkedTarget:
                    if (obj.MarkedTarget == null)
                        problems.Add("DefeatMarkedTarget needs a MarkedTarget reference.");
                    else if (!ReferenceExists(def, obj.MarkedTarget))
                        problems.Add("DefeatMarkedTarget points at " + obj.MarkedTarget + ", which no authored unit occupies.");
                    break;

                case TacticalPuzzleObjectiveKind.ProtectLane:
                    // Not an error to protect an empty lane at the start - a puzzle can require
                    // deploying into it - so only the reference itself is checkable here.
                    break;

                case TacticalPuzzleObjectiveKind.MinimalResourceSolve:
                    if (obj.ResourceBudget <= 0)
                        problems.Add("MinimalResourceSolve needs a positive ResourceBudget.");
                    else if (obj.ResourceBudget > def.StartingResource)
                        problems.Add("MinimalResourceSolve budget " + obj.ResourceBudget +
                                     " exceeds StartingResource " + def.StartingResource +
                                     " - the constraint could never bind.");
                    break;
            }
        }

        private static bool ReferenceExists(TacticalPuzzleDefinition def, TacticalPuzzleUnitRef unitRef)
        {
            List<TacticalPuzzleUnitSpec> board =
                unitRef.Side == TacticalPuzzleSide.Player ? def.PlayerBoard : def.EnemyBoard;
            if (board == null) return false;
            int seen = board.Count(s => s != null && s.Lane == unitRef.Lane);
            return unitRef.IndexInLane >= 0 && unitRef.IndexInLane < seen;
        }

        private static void ValidateEnvelopeShape(TacticalPuzzleDefinition def, List<string> problems)
        {
            if (def.Envelope == null) return;
            for (int e = 0; e < def.Envelope.Count; e++)
            {
                TacticalPuzzleExpectation exp = def.Envelope[e];
                if (exp == null) { problems.Add("Envelope[" + e + "] is null."); continue; }
                if (exp.Actions == null) continue;

                for (int a = 0; a < exp.Actions.Count; a++)
                {
                    TacticalPuzzleActionSpec spec = exp.Actions[a];
                    string where = "Envelope[" + e + "].Actions[" + a + "] ";
                    if (spec == null) { problems.Add(where + "is null."); continue; }

                    switch (spec.Kind)
                    {
                        case TacticalPuzzleActionKind.Deploy:
                            // An out-of-range index is only an authoring error when the author did
                            // not intend it - an expectation of IllegalAction may name one on
                            // purpose, which is a legitimate negative case to pin.
                            if ((spec.HandIndex < 0 || spec.HandIndex >= (def.Hand?.Count ?? 0)) &&
                                exp.ExpectedStatus != TacticalPuzzleStatus.IllegalAction)
                                problems.Add(where + "deploys hand index " + spec.HandIndex +
                                             " with a hand of " + (def.Hand?.Count ?? 0) + ".");
                            break;

                        case TacticalPuzzleActionKind.Windstep:
                            if (spec.UnitA == null) problems.Add(where + "Windstep needs UnitA.");
                            break;

                        case TacticalPuzzleActionKind.SeismicSwap:
                            if (spec.UnitA == null || spec.UnitB == null)
                                problems.Add(where + "SeismicSwap needs both UnitA and UnitB.");
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Builds the fixed starting state. Deterministic: same definition in, identical state
        /// out, every time.
        ///
        /// Both sides are constructed with an EMPTY deck and the hand is placed afterwards. That
        /// is not incidental - PlayerBattleState's constructor SHUFFLES the deck it is given
        /// (unseeded, in production) and auto-draws StartingHandSize. Passing the authored hand as
        /// a deck would randomise its order, silently breaking every Deploy index in the envelope
        /// and destroying the determinism the whole mode is gated on.
        /// </summary>
        public static MaterializedPuzzle Materialize(TacticalPuzzleDefinition def, Func<string, Card> cardSource = null)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            cardSource = cardSource ?? DefaultCardSource;

            var empty = new List<Card>();
            var result = new MaterializedPuzzle
            {
                PlayerSide = new PlayerBattleState(empty, def.ResourceCap, 0, def.AvatarHealth),
                EnemySide = new PlayerBattleState(empty, def.ResourceCap, 0, def.AvatarHealth),
            };

            result.PlayerSide.Resource = def.StartingResource;
            // The enemy never acts in a puzzle, so it is given no Resource - it is fixed scenery.
            result.EnemySide.Resource = 0;

            if (def.Hand != null)
            {
                foreach (string id in def.Hand)
                {
                    Card card = cardSource(id);
                    if (card != null) result.PlayerSide.Hand.Add(card);
                }
            }

            PlaceBoard(result, def.PlayerBoard, TacticalPuzzleSide.Player, result.PlayerSide, cardSource);
            PlaceBoard(result, def.EnemyBoard, TacticalPuzzleSide.Enemy, result.EnemySide, cardSource);

            return result;
        }

        private static void PlaceBoard(
            MaterializedPuzzle target, List<TacticalPuzzleUnitSpec> board, TacticalPuzzleSide side,
            PlayerBattleState state, Func<string, Card> cardSource)
        {
            if (board == null) return;

            var indexInLane = new Dictionary<Lane, int>();
            foreach (TacticalPuzzleUnitSpec spec in board)
            {
                if (spec == null) continue;
                Card card = cardSource(spec.CardId);
                if (card == null) continue;

                // Lane bonuses go through the same helpers live deployment uses, so a puzzle unit
                // in the Front lane has exactly the stats it would have in a real match.
                var unit = new BattleCardInstance(
                    card,
                    isPlayerOwned: side == TacticalPuzzleSide.Player,
                    laneAttackBonus: RepositionRules.LaneAttackBonusFor(spec.Lane),
                    laneHealthBonus: RepositionRules.LaneHealthBonusFor(spec.Lane));

                if (spec.PreDamage > 0) unit.ApplyDamage(spec.PreDamage);

                state.Lanes[spec.Lane].Cards.Add(unit);

                indexInLane.TryGetValue(spec.Lane, out int idx);
                target.Register(side, spec.Lane, idx, unit);
                indexInLane[spec.Lane] = idx + 1;
            }
        }

        /// <summary>Converts an authored action spec into the runtime action the verifier takes,
        /// against an already-materialised state.</summary>
        public static TacticalPuzzleAction Resolve(TacticalPuzzleActionSpec spec, MaterializedPuzzle puzzle)
        {
            if (spec == null || puzzle == null) return null;
            return new TacticalPuzzleAction
            {
                Kind = spec.Kind,
                HandIndex = spec.HandIndex,
                Lane = spec.Lane,
                UnitA = puzzle.Resolve(spec.UnitA),
                UnitB = puzzle.Resolve(spec.UnitB),
            };
        }

        /// <summary>Converts an authored objective into the runtime objective, binding
        /// MarkedTarget to a live instance.</summary>
        public static TacticalPuzzleObjective ResolveObjective(
            TacticalPuzzleObjectiveSpec spec, MaterializedPuzzle puzzle)
        {
            if (spec == null || puzzle == null) return null;
            return new TacticalPuzzleObjective
            {
                Kind = spec.Kind,
                ClashCount = spec.ClashCount,
                MarkedTarget = puzzle.Resolve(spec.MarkedTarget),
                ProtectedLane = spec.ProtectedLane,
                ResourceBudget = spec.ResourceBudget,
            };
        }

        /// <summary>
        /// Runs one authored line through the real verifier on a FRESH materialisation. Fresh is
        /// mandatory: the verifier mutates the state it is given (that is what "immediate reset on
        /// failure" means for this mode), so replaying a second line against a used state would
        /// measure the wrong board.
        /// </summary>
        public static TacticalPuzzleResult Run(
            TacticalPuzzleDefinition def, IEnumerable<TacticalPuzzleActionSpec> actions,
            Func<string, Card> cardSource = null)
        {
            MaterializedPuzzle puzzle = Materialize(def, cardSource);
            var resolved = (actions ?? Enumerable.Empty<TacticalPuzzleActionSpec>())
                .Select(a => Resolve(a, puzzle))
                .ToList();

            return TacticalPuzzleVerifier.Verify(
                puzzle.PlayerSide, puzzle.EnemySide, ResolveObjective(def.Objective, puzzle), resolved);
        }

        /// <summary>
        /// Replays every expectation in the envelope and returns the mismatches. Empty list = the
        /// puzzle behaves exactly as its author claimed.
        ///
        /// This is the check worth running in CI over authored puzzle content: it catches an
        /// intended solution that does not actually solve, a "this should be illegal" line that
        /// the rules happily allow, and a puzzle that silently became trivial after a card or a
        /// reposition rule changed underneath it.
        /// </summary>
        public static List<TacticalPuzzleEnvelopeMismatch> CheckEnvelope(
            TacticalPuzzleDefinition def, Func<string, Card> cardSource = null)
        {
            var mismatches = new List<TacticalPuzzleEnvelopeMismatch>();
            if (def?.Envelope == null) return mismatches;

            for (int i = 0; i < def.Envelope.Count; i++)
            {
                TacticalPuzzleExpectation exp = def.Envelope[i];
                if (exp == null) continue;

                TacticalPuzzleResult actual = Run(def, exp.Actions, cardSource);

                if (actual.Status != exp.ExpectedStatus)
                {
                    mismatches.Add(new TacticalPuzzleEnvelopeMismatch
                    {
                        ExpectationIndex = i,
                        Description = exp.Description,
                        Expected = exp.ExpectedStatus,
                        Actual = actual.Status,
                        Detail = actual.Message,
                    });
                    continue;
                }

                if (exp.ExpectedFailedActionIndex >= 0 &&
                    actual.FailedActionIndex != exp.ExpectedFailedActionIndex)
                {
                    mismatches.Add(new TacticalPuzzleEnvelopeMismatch
                    {
                        ExpectationIndex = i,
                        Description = exp.Description,
                        Expected = exp.ExpectedStatus,
                        Actual = actual.Status,
                        Detail = "right outcome, wrong action blamed: expected action " +
                                 exp.ExpectedFailedActionIndex + ", got " + actual.FailedActionIndex,
                    });
                }
            }

            return mismatches;
        }
    }
}

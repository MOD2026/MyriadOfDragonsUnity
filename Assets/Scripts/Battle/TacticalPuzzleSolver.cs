using System.Collections.Generic;
using MyriadOfDragons.Cards;

namespace MyriadOfDragons.Battle
{
    /// <summary>One solving line found by the solver, with the cost inputs a designer ranks by.</summary>
    public sealed class TacticalPuzzleSolution
    {
        public List<TacticalPuzzleActionSpec> Actions = new List<TacticalPuzzleActionSpec>();
        public int ActionsUsed;
        public int ResourceSpent;

        /// <summary>
        /// Canonical identity of this ANSWER: the position it leaves behind.
        ///
        /// Two lines that end in the same place are the SAME answer to a player, however they were
        /// ordered. Deploying warrior-then-archer and archer-then-warrior into the same lane is one
        /// idea with the steps swapped, not two competing solutions - and counting them as two
        /// wrongly condemns a puzzle as ambiguous.
        /// </summary>
        public string ResultingPosition;

        public override string ToString()
        {
            var parts = new List<string>();
            foreach (TacticalPuzzleActionSpec a in Actions)
            {
                parts.Add(a.Kind switch
                {
                    TacticalPuzzleActionKind.Deploy => "Deploy(hand " + a.HandIndex + " -> " + a.Lane + ")",
                    TacticalPuzzleActionKind.Windstep => "Windstep(" + a.UnitA + " -> " + a.Lane + ")",
                    TacticalPuzzleActionKind.SeismicSwap => "Swap(" + a.UnitA + " <-> " + a.UnitB + ")",
                    _ => a.Kind.ToString(),
                });
            }

            return string.Join(", ", parts) + "  [" + ActionsUsed + " orders, " + ResourceSpent + " Resource]";
        }
    }

    /// <summary>
    /// Exhaustive search over a puzzle's legal action sequences.
    ///
    /// EXISTS FOR THE CONTENT-VALIDATION STEPS THE AUTHORING LAYER COULD NOT ANSWER. Validate proves
    /// a puzzle is COHERENT and CheckEnvelope proves the author's claimed lines behave as claimed -
    /// neither can answer "is this solvable at all", "is the stated solution actually the cheapest",
    /// or "do several unrelated lines solve it equally cheaply". Those are questions about the whole
    /// space of play, so they need the whole space.
    ///
    /// IT ASKS THE VERIFIER FOR EVERY ANSWER. Legality comes from TacticalPuzzleSession, which goes
    /// to the real verifier - the solver never re-derives a rule. A solver that disagreed with the
    /// game would certify content that does not work.
    ///
    /// COST IS BOUNDED BY THE ACTION BUDGET, and it is a real combinatorial search: branching is
    /// (hand size x 3 lanes) + windsteps + swap pairs, so an unbounded puzzle is refused rather than
    /// explored forever. A puzzle whose budget makes this expensive is also a puzzle no player can
    /// hold in their head, so the limit is a design signal, not just a performance guard.
    /// </summary>
    public static class TacticalPuzzleSolver
    {
        /// <summary>Refuse to search past this many orders even if a puzzle asks for more - the
        /// space grows exponentially and the locked design calls for a SMALL legal-action set.</summary>
        public const int MaxSearchDepth = 4;

        /// <summary>
        /// Every distinct solving line, shortest first. Empty = the puzzle is unsolvable within its
        /// budget, which is an automatic reject.
        /// </summary>
        public static List<TacticalPuzzleSolution> FindAllSolutions(
            TacticalPuzzleDefinition definition, System.Func<string, Card> cardSource = null,
            int? depthOverride = null)
        {
            var found = new List<TacticalPuzzleSolution>();
            if (definition == null) return found;

            int budget = depthOverride ?? (definition.ActionBudget > 0
                ? definition.ActionBudget
                : MaxSearchDepth);
            budget = System.Math.Min(budget, MaxSearchDepth);

            Explore(definition, cardSource, new List<TacticalPuzzleActionSpec>(), budget, found);

            found.Sort((a, b) =>
            {
                int byActions = a.ActionsUsed.CompareTo(b.ActionsUsed);
                return byActions != 0 ? byActions : a.ResourceSpent.CompareTo(b.ResourceSpent);
            });
            return found;
        }

        private static void Explore(
            TacticalPuzzleDefinition definition, System.Func<string, Card> cardSource,
            List<TacticalPuzzleActionSpec> prefix, int budget, List<TacticalPuzzleSolution> found)
        {
            // Replay from scratch each node. Wasteful in theory; at a 4-order ceiling over three
            // lanes it is nothing, and it guarantees the state a candidate is judged against is the
            // verifier's own - the same reason the session re-derives rather than mutates.
            var session = new TacticalPuzzleSession(definition, cardSource);
            foreach (TacticalPuzzleActionSpec step in prefix)
            {
                if (!session.TryIssue(step).Accepted) return;   // prefix became illegal: dead branch
            }

            if (session.IsSolved)
            {
                found.Add(new TacticalPuzzleSolution
                {
                    Actions = new List<TacticalPuzzleActionSpec>(prefix),
                    ActionsUsed = session.Current.ActionsUsed,
                    ResourceSpent = definition.StartingResource - session.Current.ResourceRemaining,
                    ResultingPosition = DescribePosition(session),
                });
                return;   // shortest line only - a solved position does not need padding
            }

            if (prefix.Count >= budget) return;

            foreach (TacticalPuzzleActionSpec candidate in AllLegalMoves(session))
            {
                prefix.Add(candidate);
                Explore(definition, cardSource, prefix, budget, found);
                prefix.RemoveAt(prefix.Count - 1);
            }
        }

        /// <summary>The player's board as a stable string, for answer identity. Cards are sorted
        /// within a lane so that arrival ORDER cannot make one position look like two.</summary>
        private static string DescribePosition(TacticalPuzzleSession session)
        {
            var sb = new System.Text.StringBuilder();
            foreach (Lane lane in new[] { Lane.Front, Lane.Middle, Lane.Back })
            {
                var ids = new List<string>();
                foreach (BattleCardInstance u in session.Board.PlayerSide.Lanes[lane].Cards)
                    ids.Add(u.Definition.Id + ":" + u.CurrentHealth);
                ids.Sort(System.StringComparer.Ordinal);
                sb.Append(lane).Append('[').Append(string.Join(",", ids)).Append(']');
            }

            return sb.ToString();
        }

        private static List<TacticalPuzzleActionSpec> AllLegalMoves(TacticalPuzzleSession session)
        {
            var moves = new List<TacticalPuzzleActionSpec>();
            moves.AddRange(session.LegalDeploysNow());
            moves.AddRange(session.LegalWindstepsNow());
            moves.AddRange(session.LegalSeismicSwapsNow());
            return moves;
        }

        /// <summary>
        /// The content verdict for one puzzle: solvable, and solvable in exactly one cheapest way.
        ///
        /// "Multiple unrelated lines solve it equally cheaply" is a real reject condition, not
        /// fussiness - a puzzle with two equal answers has no intended answer, so its hint text, its
        /// score and its lesson all point at something the player need not have found.
        /// </summary>
        public static string DescribeSolutionSpace(TacticalPuzzleDefinition definition,
            System.Func<string, Card> cardSource = null)
        {
            List<TacticalPuzzleSolution> all = FindAllSolutions(definition, cardSource);
            if (all.Count == 0) return "UNSOLVABLE within its action budget - automatic reject.";

            TacticalPuzzleSolution best = all[0];

            // Count DISTINCT ANSWERS at the cheapest cost, not distinct action orders. An earlier
            // version counted sequences and condemned tac_w1_h02 as ambiguous when its two "lines"
            // were one answer with the two deploys swapped.
            var cheapestPositions = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (TacticalPuzzleSolution s in all)
            {
                if (s.ActionsUsed == best.ActionsUsed && s.ResourceSpent == best.ResourceSpent)
                    cheapestPositions.Add(s.ResultingPosition ?? "");
            }

            return string.Format(
                "{0} solving line(s); cheapest is {1} order(s)/{2} Resource, reached by {3} distinct position(s).{4}",
                all.Count, best.ActionsUsed, best.ResourceSpent, cheapestPositions.Count,
                cheapestPositions.Count > 1 ? "  AMBIGUOUS - no single intended answer." : "");
        }
    }
}

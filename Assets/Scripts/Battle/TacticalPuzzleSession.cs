using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Cards;

namespace MyriadOfDragons.Battle
{
    /// <summary>Why an attempted action was refused, for UI that has to say something useful
    /// rather than just ignore a tap.</summary>
    public enum TacticalPuzzleIssueOutcome
    {
        Accepted,
        RejectedIllegal,
        RejectedUnaffordable,
        RejectedBudgetExhausted,
        RejectedAlreadyFinished,
    }

    public sealed class TacticalPuzzleIssueReport
    {
        public TacticalPuzzleIssueOutcome Outcome;
        public string Message;
        public TacticalPuzzleResult Result;

        public bool Accepted => Outcome == TacticalPuzzleIssueOutcome.Accepted;
    }

    /// <summary>
    /// One player's live attempt at one puzzle. All of the interactive logic lives here, in a
    /// plain class, because EditMode cannot run Update() or coroutines - anything put in the
    /// MonoBehaviour is permanently untestable. The presenter owns timing and pixels; this owns
    /// what an action DOES.
    ///
    /// HOW IT WORKS, AND WHY: the verifier is all-or-nothing over a whole action list, which is
    /// right for checking a solution but not for playing one a tap at a time. So the session keeps
    /// the accepted action list and RE-RUNS it from a fresh materialisation on every issue. That
    /// sounds wasteful and is not - a puzzle is a handful of actions over three lanes - and it buys
    /// the property that matters: the board can never drift out of agreement with the verifier,
    /// because it IS the verifier's board. A separately-mutated "live" board that only got checked
    /// at the end is exactly how a puzzle mode ends up accepting a move it should have refused.
    ///
    /// A REFUSED ACTION IS NOT COMMITTED. Tapping an illegal move leaves the attempt untouched
    /// rather than failing it, which is what "the UI should refuse the tap" means in practice.
    /// Failure and reset are a player choice (Reset) or an authored budget running out, not a
    /// consequence of touching the wrong tile.
    /// </summary>
    public sealed class TacticalPuzzleSession
    {
        private readonly TacticalPuzzleDefinition _definition;
        private readonly System.Func<string, Card> _cardSource;
        private readonly List<TacticalPuzzleActionSpec> _accepted = new List<TacticalPuzzleActionSpec>();

        public TacticalPuzzleDefinition Definition => _definition;

        /// <summary>Actions accepted so far, in order. Read-only to callers - the only way in is
        /// TryIssue, so nothing can bypass legality.</summary>
        public IReadOnlyList<TacticalPuzzleActionSpec> AcceptedActions => _accepted;

        /// <summary>The board as it stands after the accepted actions. Rebuilt on every change;
        /// callers should not hold on to it across an issue.</summary>
        public MaterializedPuzzle Board { get; private set; }

        /// <summary>The verifier's verdict on the CURRENT action list.</summary>
        public TacticalPuzzleResult Current { get; private set; }

        public bool IsSolved => Current != null && Current.Solved;

        /// <summary>True once no further action can change anything - solved, or out of budget.
        /// An unsolved attempt with budget left is still live, which is the normal state.</summary>
        public bool IsFinished => IsSolved || BudgetExhausted;

        public bool BudgetExhausted =>
            _definition.ActionBudget > 0 && _accepted.Count >= _definition.ActionBudget;

        /// <summary>Actions left, or -1 when the author set no budget. -1 rather than int.MaxValue
        /// so a UI can tell "unbounded" from "a very large budget" and render nothing at all.</summary>
        public int ActionsRemaining =>
            _definition.ActionBudget > 0 ? _definition.ActionBudget - _accepted.Count : -1;

        public TacticalPuzzleSession(TacticalPuzzleDefinition definition, System.Func<string, Card> cardSource = null)
        {
            _definition = definition ?? throw new System.ArgumentNullException(nameof(definition));
            _cardSource = cardSource;
            Reset();
        }

        /// <summary>Back to the authored starting position. This is the mode's "immediate reset on
        /// failure" - cheap, because the definition is the single source of truth and nothing
        /// about an attempt is stored anywhere else.</summary>
        public void Reset()
        {
            _accepted.Clear();
            Recompute();
        }

        /// <summary>Takes back the last accepted action. Distinct from Reset: a puzzle is a
        /// thinking exercise, and undo is the difference between experimenting and being punished
        /// for it.</summary>
        public bool Undo()
        {
            if (_accepted.Count == 0) return false;
            _accepted.RemoveAt(_accepted.Count - 1);
            Recompute();
            return true;
        }

        /// <summary>
        /// Attempts one action. Accepted actions are committed; refused ones leave the attempt
        /// exactly as it was.
        /// </summary>
        public TacticalPuzzleIssueReport TryIssue(TacticalPuzzleActionSpec action)
        {
            if (action == null)
            {
                return new TacticalPuzzleIssueReport
                {
                    Outcome = TacticalPuzzleIssueOutcome.RejectedIllegal,
                    Message = "No action.",
                    Result = Current,
                };
            }

            if (IsSolved)
            {
                return new TacticalPuzzleIssueReport
                {
                    Outcome = TacticalPuzzleIssueOutcome.RejectedAlreadyFinished,
                    Message = "This position is already solved.",
                    Result = Current,
                };
            }

            if (BudgetExhausted)
            {
                return new TacticalPuzzleIssueReport
                {
                    Outcome = TacticalPuzzleIssueOutcome.RejectedBudgetExhausted,
                    Message = "No orders remaining.",
                    Result = Current,
                };
            }

            var candidate = new List<TacticalPuzzleActionSpec>(_accepted) { action };
            TacticalPuzzleResult result = TacticalPuzzleAuthoring.Play(
                _definition, candidate, out _, _cardSource);

            // Only a rejection OF THE NEW ACTION refuses the tap. A rejection blamed on an earlier
            // action would mean the already-accepted list had become illegal, which cannot happen -
            // but if it ever did, silently blaming the player's newest tap would hide a real bug.
            bool blamesNewAction = result.FailedActionIndex == candidate.Count - 1;

            if (result.Status == TacticalPuzzleStatus.IllegalAction && blamesNewAction)
            {
                return new TacticalPuzzleIssueReport
                {
                    Outcome = TacticalPuzzleIssueOutcome.RejectedIllegal,
                    Message = result.Message,
                    Result = Current,
                };
            }

            if (result.Status == TacticalPuzzleStatus.InsufficientResource && blamesNewAction)
            {
                return new TacticalPuzzleIssueReport
                {
                    Outcome = TacticalPuzzleIssueOutcome.RejectedUnaffordable,
                    Message = result.Message,
                    Result = Current,
                };
            }

            _accepted.Add(action);
            Current = result;
            // Same reason as Recompute: `board` here came back from Play() with the objective
            // already evaluated against it, so it is post-clash. Rebuild the display position.
            Board = TacticalPuzzleAuthoring.BoardAfterActions(_definition, _accepted, _cardSource);

            return new TacticalPuzzleIssueReport
            {
                Outcome = TacticalPuzzleIssueOutcome.Accepted,
                Message = result.Message,
                Result = result,
            };
        }

        private void Recompute()
        {
            // TWO passes, deliberately. Play() evaluates the objective, which RESOLVES LANE
            // CLASHES and mutates the board it returns - correct for a verdict, wrong for display.
            // Board must be the position the player's orders actually left, or the screen shows a
            // board where the fight already happened while they are still choosing orders.
            Current = TacticalPuzzleAuthoring.Play(_definition, _accepted, out _, _cardSource);
            Board = TacticalPuzzleAuthoring.BoardAfterActions(_definition, _accepted, _cardSource);
        }

        /// <summary>
        /// The decision-based score inputs the locked design names, read off the current verdict.
        /// WEIGHTS ARE NOT APPLIED HERE - how much a spare Resource is worth against a preserved
        /// card is a design decision, not a verification one, and inventing a formula would quietly
        /// become the balance nobody agreed to.
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, int>> ScoreInputs()
        {
            var list = new List<KeyValuePair<string, int>>();
            if (Current == null) return list;
            list.Add(new KeyValuePair<string, int>("Orders used", Current.ActionsUsed));
            list.Add(new KeyValuePair<string, int>("Resource remaining", Current.ResourceRemaining));
            list.Add(new KeyValuePair<string, int>("Units preserved", Current.FriendlyUnitsAlive));
            list.Add(new KeyValuePair<string, int>("Lanes held", Current.LanesHeld));
            return list;
        }

        /// <summary>
        /// True when the verifier would refuse this action as the NEXT one. One shared probe, so
        /// every "is this legal" question the UI asks resolves exactly the way the real attempt
        /// will - a highlight that disagreed with legality would be worse than no highlight.
        /// </summary>
        public bool WouldRefuse(TacticalPuzzleActionSpec candidate)
        {
            if (candidate == null) return true;
            var trial = new List<TacticalPuzzleActionSpec>(_accepted) { candidate };
            TacticalPuzzleResult result = TacticalPuzzleAuthoring.Play(
                _definition, trial, out _, _cardSource);

            return (result.Status == TacticalPuzzleStatus.IllegalAction ||
                    result.Status == TacticalPuzzleStatus.InsufficientResource) &&
                   result.FailedActionIndex == trial.Count - 1;
        }

        /// <summary>Every legal Deploy right now, for a UI that shows options instead of making the
        /// player guess.</summary>
        public List<TacticalPuzzleActionSpec> LegalDeploysNow()
        {
            var legal = new List<TacticalPuzzleActionSpec>();
            if (IsFinished || Board == null) return legal;

            int handSize = Board.PlayerSide.Hand.Count;
            foreach (Lane lane in new[] { Lane.Front, Lane.Middle, Lane.Back })
            {
                for (int i = 0; i < handSize; i++)
                {
                    var candidate = new TacticalPuzzleActionSpec
                    {
                        Kind = TacticalPuzzleActionKind.Deploy, HandIndex = i, Lane = lane,
                    };
                    if (!WouldRefuse(candidate)) legal.Add(candidate);
                }
            }

            return legal;
        }

        /// <summary>Every legal Windstep right now. Units are named by their STARTING coordinate,
        /// which is what an action spec has to carry - after an earlier Windstep the unit is no
        /// longer in the lane its coordinate names.</summary>
        public List<TacticalPuzzleActionSpec> LegalWindstepsNow()
        {
            var legal = new List<TacticalPuzzleActionSpec>();
            if (IsFinished || Board == null) return legal;

            foreach (BattleCardInstance unit in FriendlyUnits())
            {
                TacticalPuzzleUnitRef unitRef = Board.CoordinateOf(unit);
                if (unitRef == null) continue;

                foreach (Lane destination in new[] { Lane.Front, Lane.Middle, Lane.Back })
                {
                    var candidate = new TacticalPuzzleActionSpec
                    {
                        Kind = TacticalPuzzleActionKind.Windstep, UnitA = unitRef, Lane = destination,
                    };
                    if (!WouldRefuse(candidate)) legal.Add(candidate);
                }
            }

            return legal;
        }

        /// <summary>Every legal Seismic Swap right now. Each unordered pair appears ONCE - offering
        /// both orderings would show the player two buttons that do the same thing.</summary>
        public List<TacticalPuzzleActionSpec> LegalSeismicSwapsNow()
        {
            var legal = new List<TacticalPuzzleActionSpec>();
            if (IsFinished || Board == null) return legal;

            List<BattleCardInstance> units = FriendlyUnits();
            for (int i = 0; i < units.Count; i++)
            {
                for (int j = i + 1; j < units.Count; j++)
                {
                    TacticalPuzzleUnitRef a = Board.CoordinateOf(units[i]);
                    TacticalPuzzleUnitRef b = Board.CoordinateOf(units[j]);
                    if (a == null || b == null) continue;

                    var candidate = new TacticalPuzzleActionSpec
                    {
                        Kind = TacticalPuzzleActionKind.SeismicSwap, UnitA = a, UnitB = b,
                    };
                    if (!WouldRefuse(candidate)) legal.Add(candidate);
                }
            }

            return legal;
        }

        /// <summary>Living friendly units, in lane order.</summary>
        public List<BattleCardInstance> FriendlyUnits()
        {
            var units = new List<BattleCardInstance>();
            if (Board == null) return units;
            foreach (Lane lane in new[] { Lane.Front, Lane.Middle, Lane.Back })
            {
                foreach (BattleCardInstance unit in Board.PlayerSide.Lanes[lane].Cards)
                    if (unit.IsAlive) units.Add(unit);
            }
            return units;
        }

        public override string ToString() =>
            _definition.PuzzleId + " [" + _accepted.Count + " orders, " +
            (Current != null ? Current.Status.ToString() : "unstarted") + "]";
    }
}

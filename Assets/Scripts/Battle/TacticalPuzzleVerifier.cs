using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Cards;

namespace MyriadOfDragons.Battle
{
    /// <summary>The four locked objective shapes. No numbers are baked in - a puzzle supplies
    /// them, because puzzle CONTENT is explicitly a separate design pass.</summary>
    public enum TacticalPuzzleObjectiveKind
    {
        SurviveClashes,
        DefeatMarkedTarget,
        ProtectLane,
        MinimalResourceSolve,
    }

    /// <summary>The small legal action set. Deliberately small: the locked design calls for a
    /// "small legal-action set", and every action here maps to an EXISTING rule rather than a new
    /// puzzle-only mechanic.</summary>
    public enum TacticalPuzzleActionKind
    {
        Deploy,
        Windstep,
        SeismicSwap,
    }

    public sealed class TacticalPuzzleAction
    {
        public TacticalPuzzleActionKind Kind;

        /// <summary>Deploy: index into the fixed hand. There is no draw, so this is stable.</summary>
        public int HandIndex;

        /// <summary>Deploy/Windstep destination lane.</summary>
        public Lane Lane;

        /// <summary>Windstep: the unit to move. SeismicSwap: the first unit.</summary>
        public BattleCardInstance UnitA;

        /// <summary>SeismicSwap: the partner unit.</summary>
        public BattleCardInstance UnitB;
    }

    public sealed class TacticalPuzzleObjective
    {
        public TacticalPuzzleObjectiveKind Kind;

        /// <summary>SurviveClashes: how many clashes the player's board must survive.</summary>
        public int ClashCount;

        /// <summary>DefeatMarkedTarget: the enemy unit that must be dead.</summary>
        public BattleCardInstance MarkedTarget;

        /// <summary>ProtectLane: the lane that must still hold a living friendly unit.</summary>
        public Lane ProtectedLane;

        /// <summary>MinimalResourceSolve: max Resource the solution may spend. 0 = unset.</summary>
        public int ResourceBudget;
    }

    public enum TacticalPuzzleStatus
    {
        ObjectiveMet,
        ObjectiveNotMet,
        IllegalAction,
        InsufficientResource,
        MalformedPuzzle,
    }

    /// <summary>
    /// Outcome plus the decision-based score inputs the locked design names: Resource remaining,
    /// cards preserved, lanes protected, actions used. Scoring WEIGHTS are not invented here -
    /// those are content/design, not verification.
    /// </summary>
    public sealed class TacticalPuzzleResult
    {
        public TacticalPuzzleStatus Status;
        public int ActionsUsed;
        public int ResourceRemaining;
        public int FriendlyUnitsAlive;
        public int LanesHeld;
        public int FailedActionIndex = -1;
        public string Message;

        public bool Solved => Status == TacticalPuzzleStatus.ObjectiveMet;
    }

    /// <summary>
    /// Deterministic single-state outcome verifier for Tactical Puzzle mode (register: "Minigame
    /// count/second-mode design - LOCKED FINAL"). This is the REAL PREREQUISITE the design gates
    /// the mode on: "If implementation does not support this deterministic verifier, the mode
    /// should not be built yet."
    ///
    /// Given a fixed board/hand/Resource/objective state and a sequence of player actions, it
    /// resolves whether the objective is met - with NO card draw, NO AI opponent acting, and NO
    /// tick-by-tick combat. That is the structural difference from Campaign, which draws, lets the
    /// opponent act, and resolves over time.
    ///
    /// IT REUSES EXISTING RULES RATHER THAN REIMPLEMENTING COMBAT:
    ///   - deployment capacity  -> LaneState.HasRoomFor / FreeSlots (SlotWeight-aware)
    ///   - reposition legality  -> RepositionRules.IsLegalWindstep / IsLegalSeismicSwap
    ///   - clash outcome        -> LaneBattleResolver.ResolveLaneClash
    /// ResolveLaneClash is already a pure static function over two LaneStates, so clash resolution
    /// is separable from the tick loop. Nothing here re-derives damage maths - a puzzle that
    /// disagreed with real combat would be worse than no puzzle at all.
    ///
    /// STRICTLY DETERMINISTIC: no RNG, no clock, no ambient state. The same state plus the same
    /// actions must always produce the same result, which is what makes a seeded daily puzzle
    /// verifiable and a solution checkable.
    /// </summary>
    public static class TacticalPuzzleVerifier
    {
        /// <summary>
        /// Applies each action in order against a COPY-FREE view of the supplied states, then
        /// evaluates the objective. Callers pass states they are willing to have mutated; puzzle
        /// hosts are expected to rebuild the fixed state per attempt, which is what "immediate
        /// reset on failure" means.
        /// </summary>
        public static TacticalPuzzleResult Verify(
            PlayerBattleState playerSide,
            PlayerBattleState enemySide,
            TacticalPuzzleObjective objective,
            IReadOnlyList<TacticalPuzzleAction> actions)
        {
            var result = new TacticalPuzzleResult();

            if (playerSide == null || enemySide == null || objective == null)
            {
                result.Status = TacticalPuzzleStatus.MalformedPuzzle;
                result.Message = "Puzzle state is incomplete.";
                return result;
            }

            actions = actions ?? new List<TacticalPuzzleAction>();
            int startingResource = playerSide.Resource;

            TacticalPuzzleStatus applyStatus = ApplyActions(
                playerSide, actions, out int failedIndex, out string failureReason);
            if (applyStatus != TacticalPuzzleStatus.ObjectiveMet)
            {
                result.Status = applyStatus;
                result.FailedActionIndex = failedIndex;
                result.Message = failureReason;
                return FinishWithMetrics(result, playerSide, failedIndex);
            }

            result.ActionsUsed = actions.Count;
            bool met = EvaluateObjective(playerSide, enemySide, objective, startingResource);
            result.Status = met ? TacticalPuzzleStatus.ObjectiveMet : TacticalPuzzleStatus.ObjectiveNotMet;
            return FinishWithMetrics(result, playerSide, actions.Count);
        }

        /// <summary>
        /// Applies actions to a side and stops at the first refusal, WITHOUT evaluating the
        /// objective. Returns ObjectiveMet to mean "all actions applied cleanly".
        ///
        /// Split out from <see cref="Verify"/> because evaluating an objective RESOLVES LANE
        /// CLASHES, which mutates the board. Anything that wants the position as the player's
        /// orders left it - a UI rendering the board, or a legality probe for the next action -
        /// must apply actions only. Using the post-objective state for that shows a board where
        /// the fight has already happened.
        /// </summary>
        public static TacticalPuzzleStatus ApplyActions(
            PlayerBattleState playerSide, IReadOnlyList<TacticalPuzzleAction> actions,
            out int failedActionIndex, out string failureReason)
        {
            failedActionIndex = -1;
            failureReason = null;
            if (playerSide == null) return TacticalPuzzleStatus.MalformedPuzzle;

            actions = actions ?? new List<TacticalPuzzleAction>();
            for (int i = 0; i < actions.Count; i++)
            {
                TacticalPuzzleAction action = actions[i];
                if (action == null)
                {
                    failedActionIndex = i;
                    failureReason = "Null action.";
                    return TacticalPuzzleStatus.IllegalAction;
                }

                TacticalPuzzleStatus applied = Apply(playerSide, action, out string why);
                if (applied != TacticalPuzzleStatus.ObjectiveMet)
                {
                    failedActionIndex = i;
                    failureReason = why;
                    return applied;
                }
            }

            return TacticalPuzzleStatus.ObjectiveMet;
        }

        private static TacticalPuzzleResult FinishWithMetrics(
            TacticalPuzzleResult result, PlayerBattleState side, int actionsUsed)
        {
            result.ActionsUsed = actionsUsed;
            result.ResourceRemaining = side.Resource;
            result.FriendlyUnitsAlive = side.Lanes.Values.Sum(l => l.AliveCards.Count());
            result.LanesHeld = side.Lanes.Values.Count(l => l.AliveCards.Any());
            return result;
        }

        /// <summary>Returns ObjectiveMet to mean "action applied cleanly" - any other value is a
        /// rejection reason.</summary>
        private static TacticalPuzzleStatus Apply(
            PlayerBattleState side, TacticalPuzzleAction action, out string why)
        {
            why = null;
            switch (action.Kind)
            {
                case TacticalPuzzleActionKind.Deploy:
                {
                    if (action.HandIndex < 0 || action.HandIndex >= side.Hand.Count)
                    {
                        why = "Hand index " + action.HandIndex + " is outside the fixed hand.";
                        return TacticalPuzzleStatus.IllegalAction;
                    }
                    Card card = side.Hand[action.HandIndex];
                    if (card == null)
                    {
                        why = "That hand slot is empty.";
                        return TacticalPuzzleStatus.IllegalAction;
                    }
                    if (card.ResourceCost > side.Resource)
                    {
                        why = card.DisplayName + " costs " + card.ResourceCost + " with " + side.Resource + " available.";
                        return TacticalPuzzleStatus.InsufficientResource;
                    }
                    LaneState lane = side.Lanes[action.Lane];
                    if (!lane.HasRoomFor(card))
                    {
                        why = action.Lane + " has " + lane.FreeSlots + " free slot(s), needs " + card.SlotWeight + ".";
                        return TacticalPuzzleStatus.IllegalAction;
                    }

                    side.Resource -= card.ResourceCost;
                    side.Hand.RemoveAt(action.HandIndex);
                    lane.Cards.Add(new BattleCardInstance(
                        card, isPlayerOwned: true,
                        laneAttackBonus: action.Lane == Lane.Front ? BattleController.FrontLaneAttackBonus : 0,
                        laneHealthBonus: action.Lane == Lane.Middle ? BattleController.MiddleLaneHealthBonus : 0));
                    return TacticalPuzzleStatus.ObjectiveMet;
                }

                case TacticalPuzzleActionKind.Windstep:
                {
                    if (!RepositionRules.IsLegalWindstep(side, action.UnitA, action.Lane))
                    {
                        why = "Illegal Windstep to " + action.Lane + ".";
                        return TacticalPuzzleStatus.IllegalAction;
                    }
                    RepositionRules.ExecuteWindstep(side, action.UnitA, action.Lane);
                    return TacticalPuzzleStatus.ObjectiveMet;
                }

                case TacticalPuzzleActionKind.SeismicSwap:
                {
                    if (!RepositionRules.IsLegalSeismicSwap(side, action.UnitA, action.UnitB))
                    {
                        why = "Illegal Seismic Swap.";
                        return TacticalPuzzleStatus.IllegalAction;
                    }
                    RepositionRules.ExecuteSeismicSwap(side, action.UnitA, action.UnitB);
                    return TacticalPuzzleStatus.ObjectiveMet;
                }
            }

            why = "Unknown action kind.";
            return TacticalPuzzleStatus.IllegalAction;
        }

        private static bool EvaluateObjective(
            PlayerBattleState playerSide, PlayerBattleState enemySide,
            TacticalPuzzleObjective objective, int startingResource)
        {
            switch (objective.Kind)
            {
                case TacticalPuzzleObjectiveKind.SurviveClashes:
                {
                    if (objective.ClashCount <= 0) return false;
                    for (int clash = 0; clash < objective.ClashCount; clash++)
                    {
                        foreach (Lane lane in new[] { Lane.Front, Lane.Middle, Lane.Back })
                            LaneBattleResolver.ResolveLaneClash(playerSide.Lanes[lane], enemySide.Lanes[lane]);

                        if (!playerSide.Lanes.Values.Any(l => l.AliveCards.Any()))
                            return false;
                    }
                    return true;
                }

                case TacticalPuzzleObjectiveKind.DefeatMarkedTarget:
                {
                    if (objective.MarkedTarget == null) return false;
                    foreach (Lane lane in new[] { Lane.Front, Lane.Middle, Lane.Back })
                        LaneBattleResolver.ResolveLaneClash(playerSide.Lanes[lane], enemySide.Lanes[lane]);
                    return !objective.MarkedTarget.IsAlive;
                }

                case TacticalPuzzleObjectiveKind.ProtectLane:
                {
                    foreach (Lane lane in new[] { Lane.Front, Lane.Middle, Lane.Back })
                        LaneBattleResolver.ResolveLaneClash(playerSide.Lanes[lane], enemySide.Lanes[lane]);
                    return playerSide.Lanes[objective.ProtectedLane].AliveCards.Any();
                }

                case TacticalPuzzleObjectiveKind.MinimalResourceSolve:
                {
                    if (objective.ResourceBudget <= 0) return false;
                    int spent = startingResource - playerSide.Resource;
                    if (spent > objective.ResourceBudget) return false;

                    // RESOLVE THE CLASH BEFORE JUDGING (fixed 2026-08-26, owner-approved via BS).
                    //
                    // The comment below this line always said a solve that spends nothing and loses
                    // everything is not a solve - but nothing ever resolved a clash, so "loses
                    // everything" could not be detected and DOING NOTHING ALWAYS PASSED. The real
                    // validator caught tac_w1_m01 being solved in zero actions because of it; the
                    // intent was right and the implementation never matched it.
                    //
                    // One deterministic full clash, exactly like ProtectLane and DefeatMarkedTarget
                    // above. No RNG and no new combat rule - the same LaneBattleResolver every other
                    // objective already uses.
                    foreach (Lane lane in new[] { Lane.Front, Lane.Middle, Lane.Back })
                        LaneBattleResolver.ResolveLaneClash(playerSide.Lanes[lane], enemySide.Lanes[lane]);

                    // Still has to hold the board AFTER the clash - now actually checkable.
                    return playerSide.Lanes.Values.Any(l => l.AliveCards.Any());
                }
            }
            return false;
        }
    }
}

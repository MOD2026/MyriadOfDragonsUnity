using System.Collections.Generic;
using MyriadOfDragons.Battle;

namespace MyriadOfDragons.Empire
{
    /// <summary>One deployment the player actually made during a battle: which lane, and when.</summary>
    public readonly struct SoloCircuitDeployment
    {
        public readonly Lane Lane;
        public readonly int TickDeployed;

        /// <summary>Resource paid for this deployment. Needed by the Resource-total restriction,
        /// which is a SPEND rule, not a board rule.</summary>
        public readonly int ResourceSpent;

        public SoloCircuitDeployment(Lane lane, int tickDeployed, int resourceSpent = 0)
        {
            Lane = lane;
            TickDeployed = tickDeployed;
            ResourceSpent = resourceSpent;
        }
    }

    /// <summary>
    /// Evaluates the Formation Trial's daily restriction against what the player actually did.
    ///
    /// WHY THIS TAKES A DEPLOYMENT LOG RATHER THAN THE FINAL BOARD - the finding that shaped it:
    ///
    /// `MatchResult` (frozen) carries victory, ticks and health, and NO deployment information at
    /// all, so the restriction cannot be judged from the match result. The live board IS readable
    /// (`BattleController.PlayerState.Lanes`), but a snapshot is still not enough: restrictions
    /// split into two kinds.
    ///   - SNAPSHOT-checkable: "Front lane only" - a unit in the Middle lane is visible whenever
    ///     you look, as long as you look while it is alive.
    ///   - CUMULATIVE: "Deploy at most three units for the whole battle" - a fourth unit that was
    ///     deployed and then died, or was recalled, is INVISIBLE on the final board. Judging that
    ///     from the end state would silently pass a player who broke the rule.
    /// So compliance has to be observed as it happens. That is a real constraint of the existing
    /// contract, not a preference - and it needs no frozen-file change, which is why this shape was
    /// chosen over asking for one.
    ///
    /// Pure and plain per CLAUDE.md non-negotiable #6: no MonoBehaviour, no clock, no save. The
    /// caller collects the log; this only judges it.
    /// </summary>
    public static class SoloCircuitFormationRule
    {
        /// <summary>
        /// Resource ceiling for the thrift restriction: one full starting Resource bar.
        ///
        /// Tied to the real base cap (PlayerEmpireData.BaseResourceCap is 20) rather than being a
        /// magic number, so the rule keeps MEANING "a whole bar's worth" if that cap is ever
        /// retuned - the relationship is the design intent, the digit is not. Resource ramps each
        /// turn, so a full battle offers far more than this; spending only one bar's worth is a
        /// real constraint rather than a formality.
        ///
        /// If this needs balancing it is a tuning value, not a wording change - flagged as such
        /// rather than buried.
        /// </summary>
        public const int MaxResourceForThriftRestriction = 20;

        /// <summary>
        /// True when the deployments satisfy the given restriction text.
        ///
        /// Matches on the restriction STRINGS in SoloCircuitDailySeed.FormationRestrictions, which
        /// is why those live in one place: the rule the player is shown and the rule that is scored
        /// must be the same object, not two parallel lists that can drift apart.
        /// </summary>
        public static bool IsSatisfied(string restriction, IReadOnlyList<SoloCircuitDeployment> deployments)
        {
            if (string.IsNullOrEmpty(restriction)) return false;
            if (deployments == null) deployments = new List<SoloCircuitDeployment>();

            int front = CountIn(deployments, Lane.Front);
            int middle = CountIn(deployments, Lane.Middle);
            int back = CountIn(deployments, Lane.Back);

            if (restriction.StartsWith("Front lane only"))
                return middle == 0 && back == 0;

            if (restriction.StartsWith("Back lane only"))
                return front == 0 && middle == 0;

            if (restriction.StartsWith("No more than one unit per lane"))
                return front <= 1 && middle <= 1 && back <= 1;

            if (restriction.StartsWith("Win without deploying to the Middle lane"))
                return middle == 0;

            if (restriction.StartsWith("Deploy at most three units"))
                return deployments.Count <= 3;

            if (restriction.StartsWith("Clear using no more than"))
            {
                int spent = 0;
                for (int i = 0; i < deployments.Count; i++) spent += deployments[i].ResourceSpent;
                return spent <= MaxResourceForThriftRestriction;
            }

            // An unrecognised restriction FAILS rather than passes. A typo or a newly-added rule
            // must never hand out a free clear - the safe default for a reward gate is refusal.
            return false;
        }

        /// <summary>
        /// Judges a completed battle: the restriction must hold AND the player must have won.
        ///
        /// Both halves matter - the locked wording is "win under the day's restriction", so
        /// obeying the formation and losing is not a clear.
        /// </summary>
        public static bool IsCleared(
            string restriction, IReadOnlyList<SoloCircuitDeployment> deployments, bool isVictory) =>
            isVictory && IsSatisfied(restriction, deployments);

        private static int CountIn(IReadOnlyList<SoloCircuitDeployment> deployments, Lane lane)
        {
            int n = 0;
            for (int i = 0; i < deployments.Count; i++)
                if (deployments[i].Lane == lane) n++;
            return n;
        }
    }
}

using System.Collections.Generic;
using System.Linq;

namespace MyriadOfDragons.Battle
{
    /// <summary>
    /// The player-facing tap-to-target flow for Windstep/Seismic Swap (GPT spec, LOCKED
    /// 2026-08-25), as plain state - no MonoBehaviour, no rendering, so it's fully covered by
    /// EditMode tests (CLAUDE.md's own "real logic in plain testable methods; MonoBehaviours
    /// supply only timing" rule). GameBootstrap wires taps into this and reads
    /// LegalUnitCandidates/LegalDestinations to decide what to highlight; the actual highlight
    /// rendering and raycast wiring live there and are NOT covered by this class or by EditMode
    /// (no Play Mode access in this environment - see the class using it for that flag).
    ///
    /// Every method here uses <see cref="RepositionRules"/> for legality - "same legality
    /// evaluator must be shared by player and AI, no divergence" (the spec's own requirement)
    /// means this class must never invent its own notion of "legal."
    ///
    /// Energy is deducted only on a real BattleController.TryCastSpell call, which only ever
    /// happens once <see cref="TryBuildWindstepTarget"/>/<see cref="TryBuildSeismicSwapTarget"/>
    /// returns a real target - this class never touches Energy itself, so "Energy deducted only
    /// on confirm" falls out of the existing cast path for free, not something reimplemented here.
    /// </summary>
    public sealed class RepositionSelectionState
    {
        public enum Step
        {
            /// <summary>Not targeting anything.</summary>
            Idle,

            /// <summary>Windstep armed, waiting for the unit to move.</summary>
            WindstepSelectingUnit,

            /// <summary>Windstep's unit is chosen, waiting for the destination lane.</summary>
            WindstepSelectingDestination,

            /// <summary>Seismic Swap armed, waiting for the first unit.</summary>
            SeismicSwapSelectingFirstUnit,

            /// <summary>Seismic Swap's first unit is chosen, waiting for the partner.</summary>
            SeismicSwapSelectingSecondUnit,
        }

        public Step Current { get; private set; } = Step.Idle;
        public int SpellIndex { get; private set; } = -1;
        public BattleCardInstance FirstUnit { get; private set; }

        public bool IsActive => Current != Step.Idle;

        public void BeginWindstep(int spellIndex)
        {
            Current = Step.WindstepSelectingUnit;
            SpellIndex = spellIndex;
            FirstUnit = null;
        }

        public void BeginSeismicSwap(int spellIndex)
        {
            Current = Step.SeismicSwapSelectingFirstUnit;
            SpellIndex = spellIndex;
            FirstUnit = null;
        }

        /// <summary>Leaves targeting mode without casting - the same "tap anywhere else cancels"
        /// contract ArmSpellTargeting/CancelSpellTargeting already give every other spell.</summary>
        public void Cancel()
        {
            Current = Step.Idle;
            SpellIndex = -1;
            FirstUnit = null;
        }

        /// <summary>What a tap should be able to select right now - empty when nothing is armed,
        /// or when the current step is Windstep's destination-lane pick (that step's legal set is
        /// <see cref="LegalDestinationLanes"/> instead, since it picks a Lane, not a unit).</summary>
        public List<BattleCardInstance> LegalUnitCandidates(PlayerBattleState side)
        {
            switch (Current)
            {
                case Step.WindstepSelectingUnit:
                    return RepositionRules.LegalWindstepUnits(side);
                case Step.SeismicSwapSelectingFirstUnit:
                    return RepositionRules.LegalSeismicSwapFirstUnits(side);
                case Step.SeismicSwapSelectingSecondUnit:
                    return FirstUnit != null ? RepositionRules.LegalSeismicSwapPartners(side, FirstUnit) : new List<BattleCardInstance>();
                default:
                    return new List<BattleCardInstance>();
            }
        }

        /// <summary>Legal destination lanes for Windstep's second step - empty at every other step.</summary>
        public List<Lane> LegalDestinationLanes(PlayerBattleState side) =>
            Current == Step.WindstepSelectingDestination && FirstUnit != null
                ? RepositionRules.LegalWindstepDestinations(side, FirstUnit)
                : new List<Lane>();

        /// <summary>
        /// A tap on `unit`. Returns true when the tap was accepted (advanced the flow) - false
        /// for an illegal or out-of-step tap, which the caller should treat as a no-op (the same
        /// "an illegal tap changes nothing" contract every other targeting flow in this project
        /// already has), not an error.
        /// </summary>
        public bool TrySelectUnit(PlayerBattleState side, BattleCardInstance unit)
        {
            if (unit == null) return false;

            switch (Current)
            {
                case Step.WindstepSelectingUnit:
                    if (!LegalUnitCandidates(side).Contains(unit)) return false;
                    FirstUnit = unit;
                    Current = Step.WindstepSelectingDestination;
                    return true;

                case Step.SeismicSwapSelectingFirstUnit:
                    if (!LegalUnitCandidates(side).Contains(unit)) return false;
                    FirstUnit = unit;
                    Current = Step.SeismicSwapSelectingSecondUnit;
                    return true;

                case Step.SeismicSwapSelectingSecondUnit:
                    if (!LegalUnitCandidates(side).Contains(unit)) return false;
                    // Caller reads BuildSeismicSwapTarget next - this class does not auto-confirm,
                    // so a UI can still show a preview (the spec's own requirement) before the
                    // player's separate confirm tap actually casts.
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>Windstep's destination tap. Same accepted/no-op contract as TrySelectUnit.</summary>
        public bool TrySelectDestination(PlayerBattleState side, Lane destination)
        {
            if (Current != Step.WindstepSelectingDestination) return false;
            return LegalDestinationLanes(side).Contains(destination);
        }

        /// <summary>Builds the real cast target once Windstep's unit+destination are both chosen -
        /// null if called at the wrong step, or if `destination` is not actually legal for
        /// FirstUnit right now (re-checked here, not trusted from an earlier TrySelectDestination
        /// call - a caller that skips straight to this method, or whose game state changed between
        /// the two taps, must never get back a target RepositionRules would itself reject).</summary>
        public RepositionTarget TryBuildWindstepTarget(PlayerBattleState side, Lane destination)
        {
            if (Current != Step.WindstepSelectingDestination || FirstUnit == null) return null;
            if (!RepositionRules.IsLegalWindstep(side, FirstUnit, destination)) return null;
            return RepositionTarget.ForWindstep(FirstUnit, destination);
        }

        /// <summary>Builds the real cast target once both Seismic Swap units are chosen - same
        /// re-check reasoning as TryBuildWindstepTarget.</summary>
        public RepositionTarget TryBuildSeismicSwapTarget(PlayerBattleState side, BattleCardInstance secondUnit)
        {
            if (Current != Step.SeismicSwapSelectingSecondUnit || FirstUnit == null || secondUnit == null) return null;
            if (!RepositionRules.IsLegalSeismicSwap(side, FirstUnit, secondUnit)) return null;
            return RepositionTarget.ForSeismicSwap(FirstUnit, secondUnit);
        }
    }
}

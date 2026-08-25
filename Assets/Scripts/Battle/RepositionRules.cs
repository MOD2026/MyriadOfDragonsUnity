using System.Collections.Generic;
using System.Linq;

namespace MyriadOfDragons.Battle
{
    /// <summary>
    /// Windstep/Seismic Swap targeting legality (GPT spec, LOCKED 2026-08-25, closing the
    /// Reposition targeting-model gap flagged in the Full 36-Spell Catalogue Diagnosis, Wave 4).
    ///
    /// "Same legality evaluator must be shared by player and AI - anything shown as selectable to
    /// the player must always be executable, no divergence" is the whole reason this exists as one
    /// static class both GameBootstrap's UI flow and AISpellCaster's targeting call into, rather
    /// than each side re-deriving its own notion of "legal."
    ///
    /// Both spells act only on the caster's own living, deployed units - neither ever targets an
    /// enemy unit, an empty slot, or an Avatar (there is no Avatar BattleCardInstance in this
    /// codebase's model to exclude; Avatar Health lives on PlayerBattleState directly, so "not an
    /// Avatar" from the spec is automatically true for every BattleCardInstance).
    /// </summary>
    public static class RepositionRules
    {
        /// <summary>Front-Middle-Back, no wrap-around: Front/Back each have exactly one neighbor,
        /// Middle has both. Same adjacency AvatarSpell.CrossLaneDamage uses.</summary>
        public static IEnumerable<Lane> AdjacentLanes(Lane lane)
        {
            switch (lane)
            {
                case Lane.Front:
                    yield return Lane.Middle;
                    break;
                case Lane.Middle:
                    yield return Lane.Front;
                    yield return Lane.Back;
                    break;
                case Lane.Back:
                    yield return Lane.Middle;
                    break;
            }
        }

        /// <summary>The lane `unit` currently sits in on `side`, or null if it isn't a living,
        /// deployed unit on that side at all (already defeated, never played, or belongs to the
        /// other side).</summary>
        public static Lane? FindLane(PlayerBattleState side, BattleCardInstance unit)
        {
            if (side == null || unit == null || !unit.IsAlive) return null;
            foreach (KeyValuePair<Lane, LaneState> entry in side.Lanes)
            {
                if (entry.Value.Cards.Contains(unit)) return entry.Key;
            }
            return null;
        }

        // ---------- Windstep: move one unit to an adjacent lane ----------

        /// <summary>Every adjacent lane `unit` could legally move into right now (alive, deployed,
        /// has room for this unit's own SlotWeight). Empty when the unit is invalid or has no
        /// adjacent lane with capacity - exactly "no legal adjacent lane with capacity" from the
        /// spec's invalid-unit clause, expressed as an empty result rather than a separate check.</summary>
        public static List<Lane> LegalWindstepDestinations(PlayerBattleState side, BattleCardInstance unit)
        {
            Lane? current = FindLane(side, unit);
            if (current == null) return new List<Lane>();

            return AdjacentLanes(current.Value)
                .Where(candidate => side.Lanes[candidate].FreeSlots >= unit.Definition.SlotWeight)
                .ToList();
        }

        /// <summary>Every unit on `side` with at least one legal Windstep destination - the pool a
        /// player taps from, or an AI enumerates.</summary>
        public static List<BattleCardInstance> LegalWindstepUnits(PlayerBattleState side)
        {
            if (side == null) return new List<BattleCardInstance>();
            return side.Lanes.Values.SelectMany(l => l.AliveCards)
                .Where(unit => LegalWindstepDestinations(side, unit).Count > 0)
                .ToList();
        }

        public static bool IsLegalWindstep(PlayerBattleState side, BattleCardInstance unit, Lane destination) =>
            LegalWindstepDestinations(side, unit).Contains(destination);

        /// <summary>Moves `unit` to `destination`. Caller must have already checked
        /// <see cref="IsLegalWindstep"/> - this does not re-validate, matching AvatarSpell.Cast's
        /// own pattern of trusting its caller for legality (BattleController owns affordability;
        /// this class owns targeting legality; neither re-checks the other).</summary>
        public static void ExecuteWindstep(PlayerBattleState side, BattleCardInstance unit, Lane destination)
        {
            Lane current = FindLane(side, unit) ?? destination;
            side.Lanes[current].Cards.Remove(unit);
            side.Lanes[destination].Cards.Add(unit);
            unit.ReapplyLaneBonuses(LaneAttackBonusFor(destination), LaneHealthBonusFor(destination));
        }

        // ---------- Seismic Swap: exchange two units, any lanes ----------

        /// <summary>Phase 1: every living, deployed unit on `side` is a legal first pick - the
        /// second-unit pool (<see cref="LegalSeismicSwapPartners"/>) is where capacity is actually
        /// evaluated, since legality depends on the PAIR, not either unit alone.</summary>
        public static List<BattleCardInstance> LegalSeismicSwapFirstUnits(PlayerBattleState side)
        {
            if (side == null) return new List<BattleCardInstance>();
            return side.Lanes.Values.SelectMany(l => l.AliveCards).ToList();
        }

        /// <summary>Every unit that could legally swap with `first` right now - same-lane pairs are
        /// excluded (not a real reposition), and each destination is checked against its real
        /// post-swap SlotWeight total, not assumed capacity-neutral (a 2-slot unit swapping with a
        /// 1-slot unit changes both lanes' totals, and can push either over MaxSlots).</summary>
        public static List<BattleCardInstance> LegalSeismicSwapPartners(PlayerBattleState side, BattleCardInstance first)
        {
            Lane? laneA = FindLane(side, first);
            if (laneA == null) return new List<BattleCardInstance>();

            return LegalSeismicSwapFirstUnits(side)
                .Where(candidate => !ReferenceEquals(candidate, first))
                .Where(candidate => IsLegalSwapPair(side, first, laneA.Value, candidate, FindLane(side, candidate)))
                .ToList();
        }

        public static bool IsLegalSeismicSwap(PlayerBattleState side, BattleCardInstance unitA, BattleCardInstance unitB)
        {
            if (ReferenceEquals(unitA, unitB)) return false;
            Lane? laneA = FindLane(side, unitA);
            return IsLegalSwapPair(side, unitA, laneA, unitB, FindLane(side, unitB));
        }

        private static bool IsLegalSwapPair(PlayerBattleState side, BattleCardInstance unitA, Lane? laneA, BattleCardInstance unitB, Lane? laneB)
        {
            if (laneA == null || laneB == null) return false;
            if (laneA.Value == laneB.Value) return false; // same lane - not a real reposition

            // Each destination's free capacity AFTER the outgoing unit leaves must fit the
            // incoming unit's SlotWeight - the two units' weights can differ, so a swap is not
            // automatically capacity-neutral (e.g. a 2-slot unit swapping into a lane a 1-slot
            // unit just vacated can still overflow it).
            int laneAFreeAfterALeaves = side.Lanes[laneA.Value].FreeSlots + unitA.Definition.SlotWeight;
            int laneBFreeAfterBLeaves = side.Lanes[laneB.Value].FreeSlots + unitB.Definition.SlotWeight;
            return laneAFreeAfterALeaves >= unitB.Definition.SlotWeight
                && laneBFreeAfterBLeaves >= unitA.Definition.SlotWeight;
        }

        /// <summary>Exchanges `unitA` and `unitB`'s lanes. Caller must have already checked
        /// <see cref="IsLegalSeismicSwap"/> - see ExecuteWindstep's own note on why this doesn't
        /// re-validate.</summary>
        public static void ExecuteSeismicSwap(PlayerBattleState side, BattleCardInstance unitA, BattleCardInstance unitB)
        {
            Lane laneA = FindLane(side, unitA)!.Value;
            Lane laneB = FindLane(side, unitB)!.Value;
            side.Lanes[laneA].Cards.Remove(unitA);
            side.Lanes[laneB].Cards.Remove(unitB);
            side.Lanes[laneA].Cards.Add(unitB);
            side.Lanes[laneB].Cards.Add(unitA);
            unitB.ReapplyLaneBonuses(LaneAttackBonusFor(laneA), LaneHealthBonusFor(laneA));
            unitA.ReapplyLaneBonuses(LaneAttackBonusFor(laneB), LaneHealthBonusFor(laneB));
        }

        /// <summary>Same two numbers BattleController.TryPlayCard applies to a freshly-deployed
        /// unit (Part II §2.3) - the only other place a lane bonus is computed from a Lane value,
        /// now shared rather than duplicated so the two can never drift.</summary>
        public static int LaneAttackBonusFor(Lane lane) => lane == Lane.Front ? BattleController.FrontLaneAttackBonus : 0;
        public static int LaneHealthBonusFor(Lane lane) => lane == Lane.Middle ? BattleController.MiddleLaneHealthBonus : 0;
    }
}

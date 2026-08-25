using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;

namespace MyriadOfDragons.AI
{
    /// <summary>
    /// AI selection for Windstep/Seismic Swap (GPT spec, LOCKED 2026-08-25). Enumerates every
    /// legal target RepositionRules allows - "same legality evaluator must be shared by player and
    /// AI" - and picks by the spec's own priority order, falling through to the next tier only
    /// when the current one leaves a tie.
    ///
    /// Two of the spec's four priority tiers per spell are exact, game-state facts (Front/Middle
    /// bonus gained, lane currently undefended). The other two ("prevents imminent lane loss",
    /// "improves projected damage/health after next clash") are outcome PROJECTIONS with no exact
    /// numeric definition in the spec, and this class deliberately does not duplicate
    /// LaneBattleResolver's real per-unit sequential damage distribution to build one (the
    /// project's standing rule against an external/duplicate combat-math replica silently
    /// drifting from the real rules - see AISpellCaster's own class doc for the established
    /// pattern this follows). Both are operationalized here from the same aggregate
    /// living-Attack-vs-Health figures AISpellCaster already uses elsewhere (TryPickHealLane's
    /// "real threat" check, TryPickDamageLane's kill check) - documented per-method below as this
    /// implementation's own interpretation, not a separately re-confirmed locked numeric rule.
    /// </summary>
    public static class AIRepositionSelector
    {
        private static readonly Lane[] AllLanes = { Lane.Front, Lane.Middle, Lane.Back };

        // ---------- Windstep ----------

        public static bool TryPickWindstep(PlayerBattleState aiSide, PlayerBattleState opposingSide, out RepositionTarget target)
        {
            target = null;
            var candidates = new List<(BattleCardInstance unit, Lane destination, WindstepScore score)>();

            foreach (BattleCardInstance unit in RepositionRules.LegalWindstepUnits(aiSide))
            {
                foreach (Lane destination in RepositionRules.LegalWindstepDestinations(aiSide, unit))
                {
                    candidates.Add((unit, destination, ScoreWindstep(aiSide, opposingSide, unit, destination)));
                }
            }

            if (candidates.Count == 0) return false;

            (BattleCardInstance unit, Lane destination, WindstepScore score) best = candidates[0];
            for (int i = 1; i < candidates.Count; i++)
            {
                if (CompareWindstep(candidates[i], best) > 0) best = candidates[i];
            }

            target = RepositionTarget.ForWindstep(best.unit, best.destination);
            return true;
        }

        private readonly struct WindstepScore
        {
            public readonly bool PreventsImminentLaneLoss;
            public readonly bool CreatesValidBonus;
            public readonly bool RemovesOverflowRisk;
            public readonly int ProjectedMarginDelta;
            public readonly int CurrentHealth;
            public readonly int Attack;

            public WindstepScore(bool preventsLoss, bool createsBonus, bool removesOverflow, int marginDelta, int currentHealth, int attack)
            {
                PreventsImminentLaneLoss = preventsLoss;
                CreatesValidBonus = createsBonus;
                RemovesOverflowRisk = removesOverflow;
                ProjectedMarginDelta = marginDelta;
                CurrentHealth = currentHealth;
                Attack = attack;
            }
        }

        private static WindstepScore ScoreWindstep(PlayerBattleState aiSide, PlayerBattleState opposingSide, BattleCardInstance unit, Lane destination)
        {
            Lane source = RepositionRules.FindLane(aiSide, unit)!.Value;

            // Tier 1, "prevents imminent lane loss": this specific unit is in mortal danger where
            // it stands (its own CurrentHealth would not survive its lane's incoming total living
            // Attack this clash) and the destination lane would not kill it outright either -
            // moving it genuinely saves it, not just relocates it into the same danger.
            int sourceIncoming = TotalLivingAttack(opposingSide, source);
            int destIncoming = TotalLivingAttack(opposingSide, destination);
            bool preventsLoss = sourceIncoming > 0 && unit.CurrentHealth <= sourceIncoming
                                 && unit.CurrentHealth > destIncoming;

            // Tier 2, "creates a valid Front/Middle bonus": exact - the real bonus this unit would
            // carry at the destination exceeds what it carries at its current lane.
            int bonusAtSource = RepositionRules.LaneAttackBonusFor(source) + RepositionRules.LaneHealthBonusFor(source);
            int bonusAtDest = RepositionRules.LaneAttackBonusFor(destination) + RepositionRules.LaneHealthBonusFor(destination);
            bool createsBonus = bonusAtDest > bonusAtSource;

            // Tier 3, "removes overflow/improves lane capacity": exact, by LaneBattleResolver's own
            // overflow rule (ApplyDamageToLane) - a lane with zero living defenders lets damage
            // through to the Avatar. Moving a unit into a currently-undefended lane closes that gap.
            bool removesOverflow = !aiSide.Lanes[destination].Cards.Any(c => c.IsAlive);

            // Tier 4, "improves projected damage/health after next clash": ally-Attack-minus-
            // opposing-Attack margin, per lane, compared as the WORSE of the two affected lanes'
            // margins (the lane actually at risk next clash) before vs after - not the two lanes'
            // combined margin, which is conserved by construction (moving one unit's Attack from
            // one lane to the other cannot change the total across both, only redistribute it).
            int marginBeforeSource = TotalLivingAttack(aiSide, source) - sourceIncoming;
            int marginBeforeDest = TotalLivingAttack(aiSide, destination) - destIncoming;
            int marginAfterSource = (TotalLivingAttack(aiSide, source) - unit.Attack) - sourceIncoming;
            int marginAfterDest = (TotalLivingAttack(aiSide, destination) + unit.Attack) - destIncoming;
            int marginDelta = System.Math.Min(marginAfterSource, marginAfterDest) - System.Math.Min(marginBeforeSource, marginBeforeDest);

            return new WindstepScore(preventsLoss, createsBonus, removesOverflow, marginDelta, unit.CurrentHealth, unit.Attack);
        }

        private static int CompareWindstep((BattleCardInstance unit, Lane destination, WindstepScore score) a, (BattleCardInstance unit, Lane destination, WindstepScore score) b)
        {
            int cmp = a.score.PreventsImminentLaneLoss.CompareTo(b.score.PreventsImminentLaneLoss);
            if (cmp != 0) return cmp;
            cmp = a.score.CreatesValidBonus.CompareTo(b.score.CreatesValidBonus);
            if (cmp != 0) return cmp;
            cmp = a.score.RemovesOverflowRisk.CompareTo(b.score.RemovesOverflowRisk);
            if (cmp != 0) return cmp;
            cmp = a.score.ProjectedMarginDelta.CompareTo(b.score.ProjectedMarginDelta);
            if (cmp != 0) return cmp;

            // Tie-breakers (spec, exact): lowest current HP first, then highest Attack, then
            // stable board order (first-seen candidate wins - CompareWindstep only replaces `best`
            // on a strictly-greater result, so equal-on-every-tiebreak candidates keep whichever
            // was enumerated first).
            cmp = b.score.CurrentHealth.CompareTo(a.score.CurrentHealth); // lower HP wins -> reversed
            if (cmp != 0) return cmp;
            return a.score.Attack.CompareTo(b.score.Attack);
        }

        // ---------- Seismic Swap ----------

        public static bool TryPickSeismicSwap(PlayerBattleState aiSide, PlayerBattleState opposingSide, out RepositionTarget target)
        {
            target = null;
            var candidates = new List<(BattleCardInstance a, BattleCardInstance b, SwapScore score)>();
            var seen = new HashSet<(BattleCardInstance, BattleCardInstance)>();

            foreach (BattleCardInstance first in RepositionRules.LegalSeismicSwapFirstUnits(aiSide))
            {
                foreach (BattleCardInstance second in RepositionRules.LegalSeismicSwapPartners(aiSide, first))
                {
                    // Unordered pair - (X,Y) and (Y,X) are the same swap, only score it once.
                    if (!seen.Add(OrderedPair(first, second))) continue;
                    candidates.Add((first, second, ScoreSwap(aiSide, opposingSide, first, second)));
                }
            }

            if (candidates.Count == 0) return false;

            (BattleCardInstance a, BattleCardInstance b, SwapScore score) best = candidates[0];
            for (int i = 1; i < candidates.Count; i++)
            {
                if (CompareSwap(candidates[i], best) > 0) best = candidates[i];
            }

            target = RepositionTarget.ForSeismicSwap(best.a, best.b);
            return true;
        }

        private static (BattleCardInstance, BattleCardInstance) OrderedPair(BattleCardInstance a, BattleCardInstance b) =>
            a.GetHashCode() <= b.GetHashCode() ? (a, b) : (b, a);

        private readonly struct SwapScore
        {
            public readonly bool PreventsLaneDefeatOrOverflow;
            public readonly bool ImprovesMatchupOrSurvival;
            public readonly bool PreservesOrCreatesBonus;
            public readonly int ProjectedDamageDelta;
            public readonly int HpPreservationDelta;
            public readonly int AttackDelta;

            public SwapScore(bool preventsDefeat, bool improvesSurvival, bool preservesBonus, int damageDelta, int hpDelta, int attackDelta)
            {
                PreventsLaneDefeatOrOverflow = preventsDefeat;
                ImprovesMatchupOrSurvival = improvesSurvival;
                PreservesOrCreatesBonus = preservesBonus;
                ProjectedDamageDelta = damageDelta;
                HpPreservationDelta = hpDelta;
                AttackDelta = attackDelta;
            }
        }

        private static SwapScore ScoreSwap(PlayerBattleState aiSide, PlayerBattleState opposingSide, BattleCardInstance a, BattleCardInstance b)
        {
            Lane laneA = RepositionRules.FindLane(aiSide, a)!.Value;
            Lane laneB = RepositionRules.FindLane(aiSide, b)!.Value;
            int incomingA = TotalLivingAttack(opposingSide, laneA);
            int incomingB = TotalLivingAttack(opposingSide, laneB);

            // Tier 1, "prevents lane defeat/Avatar overflow": after the swap, B stands where A did
            // (facing incomingA) and A stands where B did (facing incomingB) - true when at least
            // one of the two would have died in its OLD lane but survives in its NEW one, same
            // "genuinely saves a unit" reading as Windstep's own tier 1.
            bool aWasDoomed = incomingA > 0 && a.CurrentHealth <= incomingA;
            bool bWasDoomed = incomingB > 0 && b.CurrentHealth <= incomingB;
            bool aSavedBySwap = aWasDoomed && a.CurrentHealth > incomingB;
            bool bSavedBySwap = bWasDoomed && b.CurrentHealth > incomingA;
            bool preventsDefeat = aSavedBySwap || bSavedBySwap;

            // Tier 2, "improves lane matchup/survival": the swap reduces total incoming-damage
            // exposure to the more fragile of the two units (lower CurrentHealth) - i.e. the
            // fragile unit ends up facing the smaller of the two lanes' incoming Attack.
            BattleCardInstance fragile = a.CurrentHealth <= b.CurrentHealth ? a : b;
            int fragileIncomingBefore = ReferenceEquals(fragile, a) ? incomingA : incomingB;
            int fragileIncomingAfter = ReferenceEquals(fragile, a) ? incomingB : incomingA;
            bool improvesSurvival = fragileIncomingAfter < fragileIncomingBefore;

            // Tier 3, "preserves/creates Front/Middle bonuses": under this codebase's real bonus
            // model (a flat +1 granted by the LANE, identical regardless of which unit occupies
            // it - BattleController.FrontLaneAttackBonus/MiddleLaneHealthBonus), the combined
            // Front+Middle bonus across exactly these two lanes is the same total no matter which
            // of the two units sits in which - a swap can only ever tie this tier, never win or
            // lose it. Computed honestly rather than hard-coded to true, so a future non-flat
            // bonus model (e.g. per-rarity) would make this comparison meaningful again for free.
            int bonusBefore = RepositionRules.LaneAttackBonusFor(laneA) + RepositionRules.LaneHealthBonusFor(laneA)
                             + RepositionRules.LaneAttackBonusFor(laneB) + RepositionRules.LaneHealthBonusFor(laneB);
            int bonusAfter = RepositionRules.LaneAttackBonusFor(laneB) + RepositionRules.LaneHealthBonusFor(laneB)
                            + RepositionRules.LaneAttackBonusFor(laneA) + RepositionRules.LaneHealthBonusFor(laneA);
            bool preservesBonus = bonusAfter >= bonusBefore;

            // Tier 4, "improves projected total damage": same "worse of the two lanes" reading as
            // Windstep's own tier 4, and for the same reason - the two lanes' COMBINED margin is
            // conserved by a pure two-unit swap (each lane loses one unit's Attack and gains the
            // other's), so only the weaker lane's margin can actually move.
            int marginBeforeA = TotalLivingAttack(aiSide, laneA) - incomingA;
            int marginBeforeB = TotalLivingAttack(aiSide, laneB) - incomingB;
            int marginAfterA = (TotalLivingAttack(aiSide, laneA) - a.Attack + b.Attack) - incomingA;
            int marginAfterB = (TotalLivingAttack(aiSide, laneB) - b.Attack + a.Attack) - incomingB;
            int damageDelta = System.Math.Min(marginAfterA, marginAfterB) - System.Math.Min(marginBeforeA, marginBeforeB);

            int hpPreservationDelta = aSavedBySwap || bSavedBySwap ? System.Math.Max(
                aSavedBySwap ? incomingA - incomingB : 0,
                bSavedBySwap ? incomingB - incomingA : 0) : 0;
            int attackDelta = System.Math.Abs(a.Attack - b.Attack);

            return new SwapScore(preventsDefeat, improvesSurvival, preservesBonus, damageDelta, hpPreservationDelta, attackDelta);
        }

        private static int CompareSwap((BattleCardInstance a, BattleCardInstance b, SwapScore score) x, (BattleCardInstance a, BattleCardInstance b, SwapScore score) y)
        {
            int cmp = x.score.PreventsLaneDefeatOrOverflow.CompareTo(y.score.PreventsLaneDefeatOrOverflow);
            if (cmp != 0) return cmp;
            cmp = x.score.ImprovesMatchupOrSurvival.CompareTo(y.score.ImprovesMatchupOrSurvival);
            if (cmp != 0) return cmp;
            cmp = x.score.PreservesOrCreatesBonus.CompareTo(y.score.PreservesOrCreatesBonus);
            if (cmp != 0) return cmp;
            cmp = x.score.ProjectedDamageDelta.CompareTo(y.score.ProjectedDamageDelta);
            if (cmp != 0) return cmp;

            // Tie-breakers (spec, exact): larger HP-preservation delta, then larger Attack delta,
            // then stable board order (see CompareWindstep's own note on how that falls out).
            cmp = x.score.HpPreservationDelta.CompareTo(y.score.HpPreservationDelta);
            if (cmp != 0) return cmp;
            return x.score.AttackDelta.CompareTo(y.score.AttackDelta);
        }

        private static int TotalLivingAttack(PlayerBattleState side, Lane lane) =>
            side.Lanes[lane].Cards.Where(c => c.IsAlive).Sum(c => c.Attack);
    }
}

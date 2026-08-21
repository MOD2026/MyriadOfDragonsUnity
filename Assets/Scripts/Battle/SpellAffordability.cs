using System.Collections.Generic;

namespace MyriadOfDragons.Battle
{
    /// <summary>
    /// Spell affordability hint (2026-08-22, owner: "help casting, not watch numbers and guess") -
    /// the plain, testable "can this be cast right now" predicate the Combat spell-rail cue is
    /// built from. Deliberately just the same three facts BattleController.TryCastSpell itself
    /// gates on (off cooldown, Energy covers cost) - no Update()/coroutine dependency, no UI
    /// state, and no invented rule: a spell this reports "castable" is exactly one TryCastSpell
    /// would accept (phase is the caller's concern, not this - Formation/Resolved never call in
    /// with a live castable spell anyway since Energy is always 0 outside Combat).
    /// </summary>
    public static class SpellAffordability
    {
        public static bool IsCastable(AvatarSpell spell, int energy) =>
            spell != null && spell.IsOffCooldown && spell.EnergyCost <= energy;

        /// <summary>True as soon as any one spell in the list is castable - the aggregate the
        /// hint cue itself shows.</summary>
        public static bool AnyCastable(IEnumerable<AvatarSpell> spells, int energy)
        {
            if (spells == null) return false;
            foreach (AvatarSpell spell in spells)
            {
                if (IsCastable(spell, energy)) return true;
            }
            return false;
        }
    }
}

using System.Collections.Generic;
using System.Linq;

namespace MyriadOfDragons.Battle
{
    /// <summary>
    /// Phase-1 stopgap auto-equip: no player-choice loadout UI exists yet (Metagame/future work),
    /// so BattleController needs *some* real algorithm to turn "currently unlocked spells" into
    /// the 4-spell loadout a match actually uses, instead of the previous permanently-hardcoded
    /// starter four.
    ///
    /// Heuristic: exactly one spell per <see cref="SpellEffect"/> type (LaneDamage, LaneHeal,
    /// LaneAttackBuff, AvatarStrike). There are exactly four live effect types and exactly four
    /// equip slots, so "one of each" both gives real type variety - SPELL_CATALOG_v1.md §1's own
    /// stated goal ("real loadout choices across three schools") - and automatically satisfies
    /// MOS_v1.1.md's "max 1 AvatarStrike equipped" lock as a structural consequence of the shape,
    /// not a bolted-on check layered on top. Within a type, picks the currently-unlocked spell
    /// with the highest Magnitude (the strongest available version of that effect) -
    /// deterministic, no randomness, ties break on catalog order.
    ///
    /// A freshly-started player (Avatar L1, only Stage 1-1 unlocked) always resolves to exactly
    /// the starter four (Firestorm/Mend/War Cry/Divine Bolt), since those are the only unlocked
    /// spell in each of the four types - matches the old CreateDefaultSpellbook()-only behaviour
    /// exactly, including spell order/index, for a brand-new match.
    /// </summary>
    public static class SpellLoadoutAutoEquip
    {
        private static readonly SpellEffect[] EquipSlotOrder =
        {
            SpellEffect.LaneDamage, SpellEffect.LaneHeal, SpellEffect.LaneAttackBuff, SpellEffect.AvatarStrike,
        };

        public static List<AvatarSpell> AutoEquip(int avatarLevel, IReadOnlyCollection<string> unlockedStageIds)
        {
            List<AvatarSpell> unlocked = SpellUnlockResolver.ResolveUnlockedSpells(avatarLevel, unlockedStageIds);

            var loadout = new List<AvatarSpell>();
            foreach (SpellEffect effect in EquipSlotOrder)
            {
                AvatarSpell best = unlocked
                    .Where(s => s.Effect == effect)
                    .OrderByDescending(s => s.Magnitude)
                    .FirstOrDefault();
                if (best != null) loadout.Add(best);
            }
            return loadout;
        }
    }
}

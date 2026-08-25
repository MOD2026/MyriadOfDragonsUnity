using System.Collections.Generic;
using System.Linq;

namespace MyriadOfDragons.Battle
{
        /// <summary>
        /// Auto-equip stopgap used when equippedSpellIds is empty (migration / fresh profile).
        /// Manual choice goes through SpellLoadoutPickerPresenter -> SpellLoadoutSelection; this
        /// heuristic remains the StartMatch/OwnershipSync fallback.
        ///
        /// Loadout expansion (LOCKED 2026-08-25, GPT): slot count is now tier-unlocked by Avatar
        /// level (4/5/6 - see RequiredSlotCount), not a fixed 4. At most one spell per
        /// SpellEffect type, still - with ~14 live effect types and a 6-slot cap, filling every
        /// slot is a real omission choice now, not "one of each of everything."
        /// Heuristic: "prioritize effect diversity first, then existing per-effect ranking" (the
        /// locked wording) - walks FullEffectPriorityOrder and takes the highest-Magnitude
        /// unlocked spell for the first N distinct effect types that have one, N = the slot count.
        /// Deterministic, no randomness; ties within a type break on catalog order.
        ///
        /// A freshly-started player (Avatar L1, only Stage 1-1 unlocked, 4 slots) still resolves
        /// to exactly the starter four (Firestorm/Mend/War Cry/Divine Bolt) - those are the only
        /// unlocked spell in the first four priority types, unchanged from the original behaviour.
        /// </summary>
    public static class SpellLoadoutAutoEquip
    {
        /// <summary>The original four (Damage -> Heal -> Buff -> AvatarStrike), kept as its own
        /// array since it's still what a level-1 player's whole selectable pool actually spans,
        /// and what the current manual picker UI (SpellLoadoutPickerPresenter) still renders one
        /// column per. See FullEffectPriorityOrder for the complete list the tier-unlocked
        /// loadout now uses.</summary>
        public static readonly SpellEffect[] EquipSlotOrder =
        {
            SpellEffect.LaneDamage, SpellEffect.LaneHeal, SpellEffect.LaneAttackBuff, SpellEffect.AvatarStrike,
        };

        /// <summary>Every live SpellEffect type, in auto-equip priority order - the original four
        /// unchanged, then every Wave 3+ effect type in the order it was added to the catalog.
        /// The exact ordering of the ten added types is this implementation's own reasonable
        /// default (the locked spec says "existing per-effect ranking" for the original four but
        /// does not prescribe an order for the rest) - not a separately re-confirmed locked list.</summary>
        public static readonly SpellEffect[] FullEffectPriorityOrder = EquipSlotOrder.Concat(new[]
        {
            SpellEffect.LaneShield, SpellEffect.Cleanse, SpellEffect.Dispel, SpellEffect.Vulnerability,
            SpellEffect.AllLaneAttackBuff, SpellEffect.CrossLaneDamage, SpellEffect.AllLaneDamage,
            SpellEffect.DrawCards, SpellEffect.Reposition, SpellEffect.Silence,
        }).ToArray();

        /// <summary>Slots by Avatar level (LOCKED 2026-08-25, GPT): new player 4, L10 = 5, L20 = 6.
        /// Earned via Avatar progression ONLY - never Gems/subscription/VIP/Shop, so this method
        /// deliberately takes only avatarLevel, nothing that could smuggle a purchased slot in.</summary>
        public const int StartingSlotCount = 4;
        public const int AvatarLevelForFiveSlots = 10;
        public const int AvatarLevelForSixSlots = 20;
        public const int MaxSlotCount = 6;

        public static int RequiredSlotCount(int avatarLevel)
        {
            if (avatarLevel >= AvatarLevelForSixSlots) return MaxSlotCount;
            if (avatarLevel >= AvatarLevelForFiveSlots) return 5;
            return StartingSlotCount;
        }

        public static List<AvatarSpell> AutoEquip(int avatarLevel, IReadOnlyCollection<string> unlockedStageIds)
        {
            List<AvatarSpell> unlocked = SpellUnlockResolver.ResolveUnlockedSpells(avatarLevel, unlockedStageIds);
            return SelectHighestMagnitudePerEffect(unlocked, RequiredSlotCount(avatarLevel));
        }

        /// <summary>"Effect diversity first, then existing per-effect ranking, NOT top-N-by-
        /// magnitude" (the locked wording) - walks FullEffectPriorityOrder and takes the highest-
        /// Magnitude unlocked spell for the first `slotCount` distinct effect types that have one.
        /// Exposed for callers (AIEnemySpellbookResolver) that build their own unlocked-spell pool
        /// by a different rule than avatarLevel/unlockedStageIds.</summary>
        public static List<AvatarSpell> SelectHighestMagnitudePerEffect(IEnumerable<AvatarSpell> unlocked, int slotCount)
        {
            List<AvatarSpell> unlockedList = unlocked as List<AvatarSpell> ?? unlocked.ToList();
            var loadout = new List<AvatarSpell>();
            foreach (SpellEffect effect in FullEffectPriorityOrder)
            {
                if (loadout.Count >= slotCount) break;
                AvatarSpell best = unlockedList
                    .Where(s => s.Effect == effect)
                    .OrderByDescending(s => s.Magnitude)
                    .FirstOrDefault();
                if (best != null) loadout.Add(best);
            }
            return loadout;
        }
    }
}

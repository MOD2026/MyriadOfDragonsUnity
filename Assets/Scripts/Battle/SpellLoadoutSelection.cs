using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Save;

namespace MyriadOfDragons.Battle
{
    public enum SpellLoadoutApplyStatus
    {
        Applied,
        NullProfile,
        WrongCount,
        MissingEffect,
        DuplicateEffect,
        NotSelectable,
    }

    public sealed class SpellLoadoutApplyResult
    {
        public SpellLoadoutApplyStatus Status;
        public string Message;
    }

    /// <summary>
    /// Player-driven 4-spell loadout selection. Pool comes from
    /// <see cref="SpellUnlockResolver"/> (Avatar level + chapter progress), plus any catalog
    /// spells already on <see cref="PlayerProfile.ownedSpellIds"/> so SpellBookGrant acquisitions
    /// remain equippable. Writes <see cref="PlayerProfile.equippedSpellIds"/> in the same
    /// one-per-<see cref="SpellEffect"/> shape <see cref="SpellLoadoutAutoEquip"/> guarantees.
    /// </summary>
    public static class SpellLoadoutSelection
    {
        public const int RequiredSlotCount = 4;

        public static string EffectSlotLabel(SpellEffect effect)
        {
            switch (effect)
            {
                case SpellEffect.LaneDamage: return "Damage";
                case SpellEffect.LaneHeal: return "Heal";
                case SpellEffect.LaneAttackBuff: return "Buff";
                case SpellEffect.AvatarStrike: return "Avatar Strike";
                default: return effect.ToString();
            }
        }

        /// <summary>Unlocked progression pool, union owned catalog spells (SpellBookGrant path).</summary>
        public static List<AvatarSpell> ResolveSelectablePool(PlayerProfile profile)
        {
            var byId = new Dictionary<string, AvatarSpell>();
            if (profile == null) return new List<AvatarSpell>();

            int avatarLevel = profile.avatarLevel > 0 ? profile.avatarLevel : 1;
            foreach (AvatarSpell spell in SpellUnlockResolver.ResolveUnlockedSpells(avatarLevel, profile.unlockedStageIds))
            {
                if (!string.IsNullOrEmpty(spell.Id))
                    byId[spell.Id] = spell;
            }

            if (profile.ownedSpellIds != null)
            {
                Dictionary<string, AvatarSpell> catalog = AvatarSpell.CreateCatalog()
                    .Where(s => !string.IsNullOrEmpty(s.Id))
                    .ToDictionary(s => s.Id);
                foreach (string id in profile.ownedSpellIds)
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    if (catalog.TryGetValue(id, out AvatarSpell owned))
                        byId[id] = owned;
                }
            }

            return byId.Values
                .OrderBy(s => System.Array.IndexOf(SpellLoadoutAutoEquip.EquipSlotOrder, s.Effect))
                .ThenByDescending(s => s.Magnitude)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal)
                .ToList();
        }

        public static List<AvatarSpell> SpellsForEffect(IEnumerable<AvatarSpell> pool, SpellEffect effect) =>
            pool.Where(s => s.Effect == effect).ToList();

        /// <summary>True when the pool has at least one spell for every equip slot.</summary>
        public static bool PoolCoversAllSlots(IEnumerable<AvatarSpell> pool)
        {
            var set = new HashSet<SpellEffect>(pool.Select(s => s.Effect));
            foreach (SpellEffect effect in SpellLoadoutAutoEquip.EquipSlotOrder)
            {
                if (!set.Contains(effect)) return false;
            }
            return true;
        }

        /// <summary>Validates and writes equippedSpellIds in EquipSlotOrder. Does not save.</summary>
        public static SpellLoadoutApplyResult TryApply(PlayerProfile profile, IReadOnlyList<string> selectedIds)
        {
            if (profile == null)
                return Fail(SpellLoadoutApplyStatus.NullProfile, "No profile.");

            if (selectedIds == null || selectedIds.Count != RequiredSlotCount)
                return Fail(SpellLoadoutApplyStatus.WrongCount,
                    $"Pick exactly {RequiredSlotCount} spells (one Damage, one Heal, one Buff, one Avatar Strike).");

            List<AvatarSpell> pool = ResolveSelectablePool(profile);
            var poolById = pool.Where(s => !string.IsNullOrEmpty(s.Id)).ToDictionary(s => s.Id);
            var chosen = new List<AvatarSpell>(RequiredSlotCount);

            foreach (string id in selectedIds)
            {
                if (string.IsNullOrEmpty(id) || !poolById.TryGetValue(id, out AvatarSpell spell))
                    return Fail(SpellLoadoutApplyStatus.NotSelectable,
                        $"Spell '{id}' is not in the unlocked/owned pool.");
                chosen.Add(spell);
            }

            if (chosen.Select(s => s.Effect).Distinct().Count() != RequiredSlotCount)
                return Fail(SpellLoadoutApplyStatus.DuplicateEffect,
                    "Loadout must be one spell per effect type (no two Heals, etc.).");

            foreach (SpellEffect effect in SpellLoadoutAutoEquip.EquipSlotOrder)
            {
                if (chosen.All(s => s.Effect != effect))
                    return Fail(SpellLoadoutApplyStatus.MissingEffect,
                        $"Missing {EffectSlotLabel(effect)} slot.");
            }

            // Stable write order matches AutoEquip / StartMatch expectations.
            profile.equippedSpellIds = SpellLoadoutAutoEquip.EquipSlotOrder
                .Select(effect => chosen.First(s => s.Effect == effect).Id)
                .ToList();

            return new SpellLoadoutApplyResult
            {
                Status = SpellLoadoutApplyStatus.Applied,
                Message = "Loadout saved.",
            };
        }

        private static SpellLoadoutApplyResult Fail(SpellLoadoutApplyStatus status, string message) =>
            new SpellLoadoutApplyResult { Status = status, Message = message };
    }
}

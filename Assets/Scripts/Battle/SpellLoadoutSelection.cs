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

        /// <summary>Loadout expansion (LOCKED 2026-08-25): "no duplicate spell ids" as an explicit
        /// rule distinct from DuplicateEffect, since a genuinely broken pool (two different
        /// AvatarSpell entries sharing one id - a data bug, never possible through the real
        /// catalog) could otherwise slip past the effect-uniqueness check.</summary>
        DuplicateSpellId,

        /// <summary>Loadout expansion (LOCKED 2026-08-25): "max 1 AvatarStrike stays an EXPLICIT
        /// validation rule even though effect-uniqueness already implies it - don't remove it as
        /// redundant." Kept as its own status so this specific rule's rejection message is never
        /// silently folded into the generic DuplicateEffect one.</summary>
        TooManyAvatarStrikes,
    }

    public sealed class SpellLoadoutApplyResult
    {
        public SpellLoadoutApplyStatus Status;
        public string Message;
    }

    /// <summary>
    /// Player-driven loadout selection. Pool comes from <see cref="SpellUnlockResolver"/> (Avatar
    /// level + chapter progress), plus any catalog spells already on
    /// <see cref="PlayerProfile.ownedSpellIds"/> so SpellBookGrant acquisitions remain equippable.
    ///
    /// Loadout expansion (LOCKED 2026-08-25, GPT): slot count is now tier-unlocked by Avatar level
    /// (see <see cref="SpellLoadoutAutoEquip.RequiredSlotCount"/>), not a fixed 4 - a player no
    /// longer needs one spell of EVERY effect type (there are ~14 live types and at most 6 slots,
    /// a real omission choice), just at most one PER type, up to their current slot count. Slots
    /// are earned via Avatar progression only; nothing here reads Gems/VIP/Shop state, and an
    /// existing profile's already-equipped starters are never auto-filled into newly unlocked
    /// slots - TryApply only ever writes what the caller explicitly selects.
    /// </summary>
    public static class SpellLoadoutSelection
    {
        /// <summary>Slots by the profile's own Avatar level - see SpellLoadoutAutoEquip.RequiredSlotCount.</summary>
        public static int RequiredSlotCount(int avatarLevel) => SpellLoadoutAutoEquip.RequiredSlotCount(avatarLevel);

        public static string EffectSlotLabel(SpellEffect effect) => effect switch
        {
            SpellEffect.LaneDamage => "Damage",
            SpellEffect.LaneHeal => "Heal",
            SpellEffect.LaneAttackBuff => "Buff",
            SpellEffect.AvatarStrike => "Avatar Strike",
            SpellEffect.LaneShield => "Shield",
            SpellEffect.Cleanse => "Cleanse",
            SpellEffect.Dispel => "Dispel",
            SpellEffect.Vulnerability => "Vulnerability",
            SpellEffect.AllLaneAttackBuff => "All-Lane Buff",
            SpellEffect.CrossLaneDamage => "Cross-Lane Damage",
            SpellEffect.AllLaneDamage => "All-Lane Damage",
            SpellEffect.DrawCards => "Draw",
            SpellEffect.Reposition => "Reposition",
            SpellEffect.Silence => "Silence",
            _ => effect.ToString(),
        };

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
                .OrderBy(s => System.Array.IndexOf(SpellLoadoutAutoEquip.FullEffectPriorityOrder, s.Effect))
                .ThenByDescending(s => s.Magnitude)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal)
                .ToList();
        }

        public static List<AvatarSpell> SpellsForEffect(IEnumerable<AvatarSpell> pool, SpellEffect effect) =>
            pool.Where(s => s.Effect == effect).ToList();

        /// <summary>True when the pool has at least one spell for every one of the ORIGINAL four
        /// slots (Damage/Heal/Buff/AvatarStrike) - what the current manual picker UI still renders
        /// and requires, unchanged. Not a claim about the full ~14-effect pool a 5/6-slot player
        /// could in principle choose from - see PoolHasEnoughDistinctEffects for that.</summary>
        public static bool PoolCoversAllSlots(IEnumerable<AvatarSpell> pool)
        {
            var set = new HashSet<SpellEffect>(pool.Select(s => s.Effect));
            foreach (SpellEffect effect in SpellLoadoutAutoEquip.EquipSlotOrder)
            {
                if (!set.Contains(effect)) return false;
            }
            return true;
        }

        /// <summary>Loadout expansion (LOCKED 2026-08-25): whether the pool has enough distinct
        /// effect types to fill `requiredSlotCount` slots at all (each slot must be a different
        /// effect type) - a real, useful check now that not every type is mandatory, just capped.</summary>
        public static bool PoolHasEnoughDistinctEffects(IEnumerable<AvatarSpell> pool, int requiredSlotCount) =>
            pool.Select(s => s.Effect).Distinct().Count() >= requiredSlotCount;

        /// <summary>Validates and writes equippedSpellIds. Does not save. Required count comes
        /// from the profile's own Avatar level (loadout expansion, LOCKED 2026-08-25) - the
        /// caller does not pick a count, the player's progression does.</summary>
        public static SpellLoadoutApplyResult TryApply(PlayerProfile profile, IReadOnlyList<string> selectedIds)
        {
            if (profile == null)
                return Fail(SpellLoadoutApplyStatus.NullProfile, "No profile.");

            int avatarLevel = profile.avatarLevel > 0 ? profile.avatarLevel : 1;
            int requiredCount = RequiredSlotCount(avatarLevel);

            if (selectedIds == null || selectedIds.Count != requiredCount)
                return Fail(SpellLoadoutApplyStatus.WrongCount,
                    $"Pick exactly {requiredCount} spells (at most one per effect type, at most one Avatar Strike).");

            List<AvatarSpell> pool = ResolveSelectablePool(profile);
            var poolById = pool.Where(s => !string.IsNullOrEmpty(s.Id)).ToDictionary(s => s.Id);
            var chosen = new List<AvatarSpell>(requiredCount);

            foreach (string id in selectedIds)
            {
                if (string.IsNullOrEmpty(id) || !poolById.TryGetValue(id, out AvatarSpell spell))
                    return Fail(SpellLoadoutApplyStatus.NotSelectable,
                        $"Spell '{id}' is not in the unlocked/owned pool.");
                chosen.Add(spell);
            }

            // No duplicate spell ids (explicit, LOCKED 2026-08-25) - checked before effect
            // uniqueness so a selection like [firestorm, firestorm, ...] reports the more precise
            // "duplicate spell" reason rather than the generic "duplicate effect" one.
            if (chosen.Select(s => s.Id).Distinct().Count() != chosen.Count)
                return Fail(SpellLoadoutApplyStatus.DuplicateSpellId,
                    "Loadout cannot include the same spell twice.");

            // At most one per effect type - still a hard rule, just no longer paired with "every
            // type is mandatory" (see the class doc comment).
            if (chosen.Select(s => s.Effect).Distinct().Count() != chosen.Count)
                return Fail(SpellLoadoutApplyStatus.DuplicateEffect,
                    "Loadout must have at most one spell per effect type.");

            // Max 1 AvatarStrike - EXPLICIT rule (LOCKED 2026-08-25: "don't remove it as
            // redundant" even though effect-uniqueness above already implies it).
            if (chosen.Count(s => s.Effect == SpellEffect.AvatarStrike) > 1)
                return Fail(SpellLoadoutApplyStatus.TooManyAvatarStrikes,
                    "At most one Avatar Strike spell may be equipped.");

            // Stable write order (FullEffectPriorityOrder, not selection order) matches AutoEquip
            // / StartMatch expectations - only effect types actually present in `chosen` appear.
            profile.equippedSpellIds = SpellLoadoutAutoEquip.FullEffectPriorityOrder
                .Where(effect => chosen.Any(s => s.Effect == effect))
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

using System;
using System.Collections.Generic;
using System.Linq;

namespace MyriadOfDragons.Battle
{
    /// <summary>
    /// What a spell does when it lands. Every value here is actually implemented in
    /// <see cref="AvatarSpell.Cast"/> - deliberately a short list, because a declared-but-unhandled
    /// effect type silently does nothing at runtime and looks like a bug rather than a gap.
    /// </summary>
    public enum SpellEffect
    {
        /// <summary>Damage every living enemy unit in the target lane.</summary>
        LaneDamage,

        /// <summary>Restore Health to every living friendly unit in the target lane.</summary>
        LaneHeal,

        /// <summary>Permanently raise the Attack of every living friendly unit in the target lane.</summary>
        LaneAttackBuff,

        /// <summary>Damage the enemy Avatar directly, bypassing lanes entirely.</summary>
        AvatarStrike,
    }

    /// <summary>
    /// SPELL_CATALOG_v1.md's three schools (§1): Andras - Assault (lane damage/controlled Avatar
    /// pressure), Ktini - Sustenance (lane healing/permanent Attack buffs), Pnevmas - Divine
    /// (precise Avatar pressure/recovery/rally). Cosmetic/loadout-flavor metadata only - School
    /// does not gate what a spell can do; SpellEffect already owns that. Not a 1:1 map with
    /// SpellEffect either - the catalog's own lock explicitly keeps Fault Line (Ktini/LaneDamage)
    /// as written rather than "corrected" toward its school's usual pattern (see
    /// CreatePhase1Catalog's own comment on that exact spell).
    /// </summary>
    public enum SpellSchool
    {
        Andras,
        Ktini,
        Pnevmas,
    }

    /// <summary>
    /// An Avatar spell - the player's active input during the automated combat phase.
    ///
    /// This is the half of the "pre-battle formation + auto-combat" model that keeps the player
    /// involved once the board is locked in. Formation decides the shape of the fight; spells
    /// decide how you respond to it as it plays out.
    ///
    /// Cooldowns are counted in *combat ticks*, not seconds. Ticks are the unit the battle
    /// actually advances in (see BattleController.AdvanceCombatTick), so a tick-based cooldown is
    /// deterministic, survives the game being paused or the tick rate being retuned, and - unlike
    /// a per-frame timer - can be tested without running Play Mode.
    /// </summary>
    [Serializable]
    public class AvatarSpell
    {
        /// <summary>SPELL_CATALOG_v1.md's own snake_case catalog id (e.g. "divine_bolt") - the
        /// stable join key for PlayerProfile.ownedSpellIds/equippedSpellIds. Display Name is not
        /// safe to use for this: it's presentation text, not an identifier.</summary>
        public readonly string Id;
        public readonly string Name;
        public readonly string Description;
        public readonly int EnergyCost;
        public readonly int CooldownTicks;
        public readonly SpellEffect Effect;
        public readonly int Magnitude;
        public readonly SpellSchool School;

        /// <summary>Ticks remaining before this can be cast again. 0 means ready.</summary>
        public int CooldownRemaining { get; private set; }

        /// <summary>id/school are trailing optional params, not inserted into the existing
        /// positional order - every pre-existing call site (production catalog and several test
        /// files that construct synthetic spells directly) keeps compiling unchanged. A spell
        /// built without an explicit id gets "" (never a real catalog member, never matches a
        /// real ownedSpellIds/equippedSpellIds entry - correct for a synthetic test spell that
        /// isn't part of the real catalog anyway).</summary>
        public AvatarSpell(string name, string description, int energyCost, int cooldownTicks,
            SpellEffect effect, int magnitude, string id = "", SpellSchool school = SpellSchool.Andras)
        {
            Id = id;
            Name = name;
            Description = description;
            EnergyCost = energyCost;
            CooldownTicks = cooldownTicks;
            Effect = effect;
            Magnitude = magnitude;
            School = school;
        }

        /// <summary>
        /// Converts a unit-scale LaneDamage magnitude (sized against card Health, 1-12) into
        /// Avatar-scale damage when it passes through an undefended lane. Mirrors
        /// LaneBattleResolver.AvatarDamageMultiplier, which does the same job for lane overflow -
        /// without it, a spell that "gets through" would chip 4 off a 1300 HP pool and look broken.
        /// </summary>
        private const int AvatarDamageScale = LaneBattleResolver.AvatarDamageMultiplier;
        // Deliberately the base multiplier, not the escalating overtime one: a spell's printed
        // magnitude should mean the same thing on tick 11 as on tick 2, or its cost stops being
        // comparable to its effect.

        public bool IsOffCooldown => CooldownRemaining <= 0;

        public void TickCooldown()
        {
            if (CooldownRemaining > 0) CooldownRemaining--;
        }

        public void PutOnCooldown() => CooldownRemaining = CooldownTicks;

        /// <summary>
        /// Applies this spell's effect. Does not check energy or cooldown - BattleController.
        /// TryCastSpell owns those rules, so this stays a pure "what the effect does" method.
        /// Returns the amount of Avatar damage dealt (0 for anything that isn't AvatarStrike),
        /// so the caller can drive damage numbers off the same value the state changed by.
        /// </summary>
        public int Cast(PlayerBattleState caster, PlayerBattleState opponent, Lane targetLane)
        {
            switch (Effect)
            {
                case SpellEffect.LaneDamage:
                    List<BattleCardInstance> defenders = LivingUnits(opponent, targetLane);
                    if (defenders.Count == 0)
                    {
                        // An undefended lane lets damage through to the Avatar - the same rule
                        // lane combat already follows (see LaneBattleResolver.ApplyDamageToLane).
                        // Without this, casting into an empty lane silently did nothing at all,
                        // which by the late game - when both boards have wiped out - meant every
                        // damage spell was a dead button ("the health of the enemy does not move
                        // after casting spell", 2026-08-06).
                        int through = Math.Min(Magnitude * AvatarDamageScale, opponent.AvatarHealth);
                        opponent.AvatarHealth -= through;
                        return through;
                    }

                    foreach (BattleCardInstance unit in defenders)
                    {
                        unit.ApplyDamage(Magnitude);
                    }
                    // Dead units are pruned during lane resolution (LaneBattleResolver), not here -
                    // keeping removal in one place avoids two different rules about when a corpse
                    // stops occupying its slot.
                    return 0;

                case SpellEffect.LaneHeal:
                    foreach (BattleCardInstance unit in LivingUnits(caster, targetLane))
                    {
                        unit.Heal(Magnitude);
                    }
                    return 0;

                case SpellEffect.LaneAttackBuff:
                    foreach (BattleCardInstance unit in LivingUnits(caster, targetLane))
                    {
                        unit.BuffAttack(Magnitude);
                    }
                    return 0;

                case SpellEffect.AvatarStrike:
                    int dealt = Math.Min(Magnitude, opponent.AvatarHealth);
                    opponent.AvatarHealth -= dealt;
                    return dealt;

                default:
                    return 0;
            }
        }

        private static List<BattleCardInstance> LivingUnits(PlayerBattleState side, Lane lane)
        {
            return side.Lanes[lane].Cards.Where(c => c.IsAlive).ToList();
        }

        /// <summary>
        /// The starting spellbook. Magnitudes are sized against the Integer Model (card
        /// Attack/Health are 1-12), not against Avatar HP - LaneDamage of 4 is a real threat to a
        /// mid-rarity unit, whereas AvatarStrike is quoted in Avatar-scale numbers because it
        /// bypasses units entirely and lands on a pool measured in the hundreds or thousands.
        /// </summary>
        public static List<AvatarSpell> CreateDefaultSpellbook()
        {
            return new List<AvatarSpell>
            {
                new AvatarSpell("Firestorm", "Deal 4 damage to every enemy unit in a lane.",
                    energyCost: 30, cooldownTicks: 3, SpellEffect.LaneDamage, magnitude: 4,
                    id: "firestorm", school: SpellSchool.Andras),

                new AvatarSpell("Mend", "Restore 4 Health to every friendly unit in a lane.",
                    energyCost: 25, cooldownTicks: 3, SpellEffect.LaneHeal, magnitude: 4,
                    id: "mend", school: SpellSchool.Ktini),

                // Catalog lock: War Cry is Pnevmas, not Andras - verified directly against
                // SPELL_CATALOG_v1.md's real table (an earlier draft had this wrong).
                new AvatarSpell("War Cry", "Permanently grant +2 Attack to a friendly lane.",
                    energyCost: 40, cooldownTicks: 4, SpellEffect.LaneAttackBuff, magnitude: 2,
                    id: "war_cry", school: SpellSchool.Pnevmas),

                new AvatarSpell("Divine Bolt", "Strike the enemy Avatar directly for 100.",
                    energyCost: 60, cooldownTicks: 5, SpellEffect.AvatarStrike, magnitude: 100,
                    id: "divine_bolt", school: SpellSchool.Pnevmas),
            };
        }

        /// <summary>
        /// Every Phase-1 spell from SPELL_CATALOG_v1.md's "Phase-1 ship slice" table (the starter
        /// four plus the ten Ch1-3/Avatar-L1-12 unlocks) - all fourteen use only the four live
        /// <see cref="SpellEffect"/> values per that doc's own "Phase-1 rule", so no new Cast()
        /// case is needed. This is deliberately NOT what a new player starts with -
        /// CreateDefaultSpellbook() above stays exactly as it was, still just the starter four, so
        /// existing callers (BattleController's player/AI spellbook construction) are unaffected.
        /// This method exists purely so the other ten spells exist and are castable through the
        /// same TryCastSpell path once something equips them; where/when each one unlocks is not
        /// implemented here - see the catalog's own "Unlock" column (Ch1-2, Avatar L5, Ch2-4, etc.)
        /// for what a future equip/spell-book system still needs to gate on.
        /// </summary>
        public static List<AvatarSpell> CreatePhase1Catalog()
        {
            var catalog = CreateDefaultSpellbook();
            catalog.AddRange(new[]
            {
                new AvatarSpell("Cinder Lash", "Deal 2 damage to every enemy unit in a lane.",
                    energyCost: 18, cooldownTicks: 2, SpellEffect.LaneDamage, magnitude: 2,
                    id: "cinder_lash", school: SpellSchool.Andras),

                new AvatarSpell("Ember Wave", "Deal 3 damage to every enemy unit in a lane.",
                    energyCost: 26, cooldownTicks: 3, SpellEffect.LaneDamage, magnitude: 3,
                    id: "ember_wave", school: SpellSchool.Andras),

                // Catalog lock: Fault Line is Ktini school but a LaneDamage effect, unlike every
                // other Ktini spell here (Sustenance: healing and Attack buffs per the catalog's
                // own school descriptions) - implemented exactly as the locked table specifies,
                // not "corrected" toward the school's usual pattern.
                new AvatarSpell("Fault Line", "Deal 5 damage to every enemy unit in a lane.",
                    energyCost: 44, cooldownTicks: 5, SpellEffect.LaneDamage, magnitude: 5,
                    id: "fault_line", school: SpellSchool.Ktini),

                new AvatarSpell("Vital Spark", "Restore 2 Health to every friendly unit in a lane.",
                    energyCost: 18, cooldownTicks: 2, SpellEffect.LaneHeal, magnitude: 2,
                    id: "vital_spark", school: SpellSchool.Ktini),

                new AvatarSpell("Renewal", "Restore 6 Health to every friendly unit in a lane.",
                    energyCost: 45, cooldownTicks: 5, SpellEffect.LaneHeal, magnitude: 6,
                    id: "renewal", school: SpellSchool.Ktini),

                new AvatarSpell("Rallying Gale", "Permanently grant +1 Attack to a friendly lane.",
                    energyCost: 24, cooldownTicks: 3, SpellEffect.LaneAttackBuff, magnitude: 1,
                    id: "rallying_gale", school: SpellSchool.Pnevmas),

                new AvatarSpell("Banner of Ashes", "Permanently grant +3 Attack to a friendly lane.",
                    energyCost: 55, cooldownTicks: 5, SpellEffect.LaneAttackBuff, magnitude: 3,
                    id: "banner_of_ashes", school: SpellSchool.Andras),

                new AvatarSpell("Sun Lance", "Strike the enemy Avatar directly for 75.",
                    energyCost: 35, cooldownTicks: 3, SpellEffect.AvatarStrike, magnitude: 75,
                    id: "sun_lance", school: SpellSchool.Pnevmas),

                new AvatarSpell("Stone Judgment", "Strike the enemy Avatar directly for 120.",
                    energyCost: 80, cooldownTicks: 7, SpellEffect.AvatarStrike, magnitude: 120,
                    id: "stone_judgment", school: SpellSchool.Ktini),

                new AvatarSpell("Tempest Brand", "Deal 3 damage to every enemy unit in a lane.",
                    energyCost: 36, cooldownTicks: 4, SpellEffect.LaneDamage, magnitude: 3,
                    id: "tempest_brand", school: SpellSchool.Pnevmas),
            });
            return catalog;
        }
    }
}

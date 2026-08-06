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
        public readonly string Name;
        public readonly string Description;
        public readonly int EnergyCost;
        public readonly int CooldownTicks;
        public readonly SpellEffect Effect;
        public readonly int Magnitude;

        /// <summary>Ticks remaining before this can be cast again. 0 means ready.</summary>
        public int CooldownRemaining { get; private set; }

        public AvatarSpell(string name, string description, int energyCost, int cooldownTicks,
            SpellEffect effect, int magnitude)
        {
            Name = name;
            Description = description;
            EnergyCost = energyCost;
            CooldownTicks = cooldownTicks;
            Effect = effect;
            Magnitude = magnitude;
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
                    energyCost: 30, cooldownTicks: 3, SpellEffect.LaneDamage, magnitude: 4),

                new AvatarSpell("Mend", "Restore 4 Health to every friendly unit in a lane.",
                    energyCost: 25, cooldownTicks: 3, SpellEffect.LaneHeal, magnitude: 4),

                new AvatarSpell("War Cry", "Permanently grant +2 Attack to a friendly lane.",
                    energyCost: 40, cooldownTicks: 4, SpellEffect.LaneAttackBuff, magnitude: 2),

                new AvatarSpell("Divine Bolt", "Strike the enemy Avatar directly for 150.",
                    energyCost: 60, cooldownTicks: 5, SpellEffect.AvatarStrike, magnitude: 150),
            };
        }
    }
}

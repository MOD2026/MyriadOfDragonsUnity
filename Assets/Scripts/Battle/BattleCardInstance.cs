using MyriadOfDragons.Cards;

namespace MyriadOfDragons.Battle
{
    /// <summary>
    /// A card once it's on the battlefield - wraps the static <see cref="Card"/> definition
    /// with the mutable state a live match needs (current Health, whether it's already acted).
    /// </summary>
    public class BattleCardInstance
    {
        public readonly Card Definition;
        public readonly bool IsPlayerOwned;

        /// <summary>Live Attack. No longer readonly: Avatar spells (see AvatarSpell) can buff a
        /// unit mid-combat, which is the whole point of the active-spell layer.</summary>
        public int Attack { get; private set; }

        /// <summary>The Health ceiling a Heal spell may restore up to, so healing can't inflate
        /// a unit past what it was deployed at. Not readonly: formation synergy raises it (see
        /// BuffMaxHealth) before combat starts.</summary>
        public int MaxHealth { get; private set; }

        public int CurrentHealth { get; private set; }
        public bool HasTaunt { get; }

        /// <summary>Absorbs damage before Health. Wave 3 lock: replace-not-stack - a second Shield
        /// replaces a weaker one rather than adding; expires naturally at match end since nothing
        /// resets it mid-match and a BattleCardInstance never outlives a match.</summary>
        public int Shield { get; private set; }

        /// <summary>Wave 3 lock: a single mark, consumed (+1 damage) the next time this unit takes
        /// damage, or cleared unused at the end of the clash it was cast into - see
        /// LaneBattleResolver.ResolveLaneClash's end-of-clash sweep.</summary>
        private bool _vulnerabilityMarked;

        /// <summary>Wave 3 lock: spell-granted Attack buffs (War Cry/Rallying Gale/Banner of
        /// Ashes/Thunder Decree) cap at +3/unit cumulative, regardless of how many different
        /// spells contribute.</summary>
        private int _spellAttackBuffApplied;
        private const int SpellAttackBuffCap = 3;

        public BattleCardInstance(Card definition, bool isPlayerOwned, int laneAttackBonus, int laneHealthBonus)
        {
            Definition = definition;
            IsPlayerOwned = isPlayerOwned;
            Attack = definition.Attack + laneAttackBonus;
            MaxHealth = definition.Health + laneHealthBonus;
            CurrentHealth = MaxHealth;
            HasTaunt = definition.Class == CardClass.Knight;
        }

        public bool IsAlive => CurrentHealth > 0;

        public void ApplyDamage(int amount)
        {
            if (amount <= 0) return;
            if (_vulnerabilityMarked)
            {
                amount += 1;
                _vulnerabilityMarked = false;
            }
            if (Shield > 0)
            {
                int absorbed = System.Math.Min(Shield, amount);
                Shield -= absorbed;
                amount -= absorbed;
            }
            CurrentHealth = System.Math.Max(0, CurrentHealth - amount);
        }

        /// <summary>Restores Health up to MaxHealth. A dead unit stays dead - healing is not a
        /// resurrection, and letting it revive corpses would quietly undo the lane-clearing rules
        /// that overflow damage depends on.</summary>
        public void Heal(int amount)
        {
            if (!IsAlive || amount <= 0) return;
            CurrentHealth = System.Math.Min(MaxHealth, CurrentHealth + amount);
        }

        /// <summary>Permanent (for this match) Attack increase from a buff spell. Wave 3 lock:
        /// capped at +3/unit cumulative across every spell-sourced buff this unit receives.</summary>
        public void BuffAttack(int amount)
        {
            if (amount <= 0) return;
            int room = System.Math.Max(0, SpellAttackBuffCap - _spellAttackBuffApplied);
            int applied = System.Math.Min(amount, room);
            if (applied <= 0) return;
            Attack += applied;
            _spellAttackBuffApplied += applied;
        }

        /// <summary>Wave 3 lock: replace-not-stack.</summary>
        public void ApplyShield(int amount)
        {
            if (amount > Shield) Shield = amount;
        }

        public void ClearShield()
        {
            Shield = 0;
        }

        public void MarkVulnerable()
        {
            _vulnerabilityMarked = true;
        }

        public void ClearVulnerabilityMark()
        {
            _vulnerabilityMarked = false;
        }

        /// <summary>Called once per clash for every living unit on both sides (see
        /// LaneBattleResolver.ResolveLaneClash) - implements "expires next clash if unused" as a
        /// no-op for a mark that already triggered via ApplyDamage this same clash.</summary>
        public void ExpireVulnerabilityMarkAtClashEnd()
        {
            _vulnerabilityMarked = false;
        }

        /// <summary>
        /// Raises both the Health ceiling and current Health - used by formation synergy, which
        /// applies before a single blow is struck. Raising only the ceiling would hand the unit
        /// a bonus it starts already missing, which reads as a bug rather than a buff.
        /// </summary>
        public void BuffMaxHealth(int amount)
        {
            if (amount <= 0) return;
            MaxHealth += amount;
            CurrentHealth += amount;
        }
    }
}

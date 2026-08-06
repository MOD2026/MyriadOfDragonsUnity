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

        /// <summary>Permanent (for this match) Attack increase from a buff spell.</summary>
        public void BuffAttack(int amount)
        {
            if (amount <= 0) return;
            Attack += amount;
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

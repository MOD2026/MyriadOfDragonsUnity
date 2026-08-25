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

        /// <summary>The lane bonus (Front Attack / Middle Health, Part II §2.3) already folded
        /// into Attack/MaxHealth above, tracked separately so <see cref="ReapplyLaneBonuses"/> -
        /// Windstep/Seismic Swap moving this unit to a different lane (GPT spec, LOCKED
        /// 2026-08-25) - can remove exactly the old bonus and add exactly the new one, without
        /// touching any spell buff/Shield/etc. also folded into the same two fields.</summary>
        private int _laneAttackBonusApplied;
        private int _laneHealthBonusApplied;

        /// <summary>Suppressible triggered-ability package (LOCKED 2026-08-25, GPT, register
        /// commit 2b54084): which of the four named cards' passives this copy carries, resolved
        /// once from Definition.Id at construction - None for every other card.</summary>
        public readonly CardTriggerAbility Ability;

        /// <summary>One flag covers all four abilities' "once per unit per match" - a given copy
        /// only ever has one Ability, so there is never a case where two different triggers need
        /// independent used-state on the same instance.</summary>
        public bool HasUsedTrigger { get; private set; }

        /// <summary>Clashes remaining under Silence - triggers are suppressed while &gt; 0.
        /// Decremented once per clash (LaneBattleResolver.ResolveLaneClash's end-of-clash sweep,
        /// same point Vulnerability's mark expires). Consumed trigger uses are NOT restored when
        /// this reaches 0 - only an unused trigger becomes checkable again (per the locked Silence
        /// contract).</summary>
        private int _silencedClashesRemaining;
        public bool IsSilenced => _silencedClashesRemaining > 0;

        public BattleCardInstance(Card definition, bool isPlayerOwned, int laneAttackBonus, int laneHealthBonus)
        {
            Definition = definition;
            IsPlayerOwned = isPlayerOwned;
            Attack = definition.Attack + laneAttackBonus;
            MaxHealth = definition.Health + laneHealthBonus;
            CurrentHealth = MaxHealth;
            HasTaunt = definition.Class == CardClass.Knight;
            _laneAttackBonusApplied = laneAttackBonus;
            _laneHealthBonusApplied = laneHealthBonus;
            Ability = CardTriggerAbilities.For(definition.Id);
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

            // Shield Discipline (Novice Knight, LOCKED 2026-08-25): this unit's first incoming
            // damage this match is reduced by 1, floored at 0 - never a negative "heal". Applied
            // to the raw incoming amount, same stage as Vulnerability's own +1, so the two cancel
            // to a wash when both apply rather than interacting with Shield absorption order.
            if (Ability == CardTriggerAbility.ShieldDiscipline && !HasUsedTrigger && !IsSilenced)
            {
                amount = System.Math.Max(0, amount - 1);
                HasUsedTrigger = true;
            }

            if (Shield > 0)
            {
                int absorbed = System.Math.Min(Shield, amount);
                Shield -= absorbed;
                amount -= absorbed;
            }

            int newHealth = System.Math.Max(0, CurrentHealth - amount);

            // Ash Rebirth (Phoenix, LOCKED 2026-08-25): the first time this unit would be
            // defeated, it stays at 1 HP instead - once per match. Checked against the health the
            // damage would otherwise produce, not "IsAlive now", so it only ever fires on the hit
            // that would actually cause the defeat, not every subsequent hit on an already-1-HP unit.
            if (newHealth <= 0 && Ability == CardTriggerAbility.AshRebirth && !HasUsedTrigger && !IsSilenced)
            {
                newHealth = 1;
                HasUsedTrigger = true;
            }

            CurrentHealth = newHealth;
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

        /// <summary>Marks this unit's one-time trigger as spent - Hex Spark/Battle Mend call this
        /// from LaneBattleResolver's trigger-resolution pass; Shield Discipline/Ash Rebirth set it
        /// themselves inside ApplyDamage.</summary>
        public void MarkTriggerUsed()
        {
            HasUsedTrigger = true;
        }

        /// <summary>Applies Silence for `clashes` clashes. Extends rather than shortens an
        /// existing Silence (a second cast landing mid-duration should not reduce it) - this
        /// implementation's own inference, since the locked contract does not specify stacking
        /// behaviour.</summary>
        public void ApplySilence(int clashes)
        {
            if (clashes > _silencedClashesRemaining) _silencedClashesRemaining = clashes;
        }

        /// <summary>Called once per clash for every living unit on both sides, same sweep point
        /// as ExpireVulnerabilityMarkAtClashEnd - ticks Silence down by one clash. Consumed
        /// trigger uses are never restored here; only IsSilenced changes.</summary>
        public void ExpireSilenceAtClashEnd()
        {
            if (_silencedClashesRemaining > 0) _silencedClashesRemaining--;
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

        /// <summary>Windstep/Seismic Swap (GPT spec, LOCKED 2026-08-25): removes exactly the lane
        /// bonus this unit was carrying and applies exactly the new lane's, so moving a unit into
        /// or out of Front/Middle correctly changes its Attack/MaxHealth - a straight relocate
        /// with no recompute would leave a unit permanently carrying a bonus from a lane it no
        /// longer occupies, or missing one it now qualifies for. CurrentHealth shifts by the same
        /// delta as MaxHealth (mirrors BuffMaxHealth's own reasoning) and is clamped like
        /// ApplyDamage - losing a Health bonus can, in the rare case a unit was already at exactly
        /// that much Health, legitimately defeat it; that is a natural consequence of the same
        /// Health model everywhere else, not a special case invented for repositioning.</summary>
        public void ReapplyLaneBonuses(int newAttackBonus, int newHealthBonus)
        {
            Attack = Attack - _laneAttackBonusApplied + newAttackBonus;
            _laneAttackBonusApplied = newAttackBonus;

            int healthDelta = newHealthBonus - _laneHealthBonusApplied;
            MaxHealth += healthDelta;
            CurrentHealth = System.Math.Max(0, System.Math.Min(MaxHealth, CurrentHealth + healthDelta));
            _laneHealthBonusApplied = newHealthBonus;
        }
    }
}

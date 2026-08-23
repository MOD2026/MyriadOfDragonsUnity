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
        /// <summary>tickCount defaults to "always past the gate" so every existing caller that
        /// doesn't yet pass a real tick count (tests predating the clash-3 rule, any future
        /// non-AvatarStrike-only use) keeps its exact prior behaviour - only a caller that
        /// explicitly supplies BattleController.TickCount gets the real gate applied.</summary>
        public static bool IsCastable(AvatarSpell spell, int energy, int tickCount = int.MaxValue) =>
            spell != null && spell.IsOffCooldown && spell.EnergyCost <= energy
            && (spell.Effect != SpellEffect.AvatarStrike || tickCount >= BattleController.MinimumCombatTickForAvatarStrike);

        /// <summary>True as soon as any one spell in the list is castable - the aggregate the
        /// hint cue itself shows.</summary>
        public static bool AnyCastable(IEnumerable<AvatarSpell> spells, int energy, int tickCount = int.MaxValue)
        {
            if (spells == null) return false;
            foreach (AvatarSpell spell in spells)
            {
                if (IsCastable(spell, energy, tickCount)) return true;
            }
            return false;
        }

        /// <summary>Cast-rejection reason hint (2026-08-22, owner: "show a clear reason - cooldown
        /// vs not enough Energy vs wrong phase"). Mirrors exactly the order BattleController.
        /// TryCastSpell itself checks (phase, then cooldown, then Energy), so a reason this reports
        /// is exactly the reason TryCastSpell would have rejected the same attempt for - no
        /// invented rule, no UI state.</summary>
        public enum SpellCastRejectReason
        {
            /// <summary>Not actually rejected - GetRejectReason returns this when the spell is
            /// castable, so a caller can tell "no reason" apart from "on cooldown" etc.</summary>
            None,
            WrongPhase,
            OnCooldown,
            NotEnoughEnergy,

            /// <summary>SPELL_CATALOG_v1.md §2 direct-strike safety rule: an AvatarStrike spell
            /// cannot be cast before combat tick/clash 3.</summary>
            TooEarlyForAvatarStrike,
        }

        /// <summary>tickCount defaults to "always past the gate" for the same backward-
        /// compatibility reason as IsCastable's own default - only a caller that explicitly
        /// passes BattleController.TickCount gets TooEarlyForAvatarStrike checked at all.</summary>
        public static SpellCastRejectReason GetRejectReason(AvatarSpell spell, BattlePhase phase, int energy, int tickCount = int.MaxValue)
        {
            if (spell == null) return SpellCastRejectReason.None;
            if (phase != BattlePhase.Combat) return SpellCastRejectReason.WrongPhase;
            if (!spell.IsOffCooldown) return SpellCastRejectReason.OnCooldown;
            if (spell.EnergyCost > energy) return SpellCastRejectReason.NotEnoughEnergy;
            if (spell.Effect == SpellEffect.AvatarStrike && tickCount < BattleController.MinimumCombatTickForAvatarStrike)
                return SpellCastRejectReason.TooEarlyForAvatarStrike;
            return SpellCastRejectReason.None;
        }

        /// <summary>Plain-language reject message for the existing hint/status text surface - one
        /// specific sentence per reason, never the old blanket "not enough Energy, or still
        /// cooling down" that made the player guess which one actually applied. Takes the live
        /// Energy value directly (rather than re-deriving it) so the "not enough" message can
        /// state exactly how much the player has.</summary>
        public static string DescribeRejectReason(AvatarSpell spell, SpellCastRejectReason reason, int energy)
        {
            if (spell == null) return string.Empty;
            return reason switch
            {
                SpellCastRejectReason.WrongPhase => $"{spell.Name} can only be cast during Combat.",
                SpellCastRejectReason.OnCooldown => $"{spell.Name} is still cooling down - {spell.CooldownRemaining} tick(s) left.",
                SpellCastRejectReason.NotEnoughEnergy => $"Not enough Energy for {spell.Name} - needs {spell.EnergyCost}, have {energy}.",
                SpellCastRejectReason.TooEarlyForAvatarStrike => $"{spell.Name} cannot strike the Avatar before clash {BattleController.MinimumCombatTickForAvatarStrike}.",
                _ => string.Empty,
            };
        }
    }
}

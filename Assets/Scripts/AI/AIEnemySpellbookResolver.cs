using System.Collections.Generic;
using MyriadOfDragons.Battle;

namespace MyriadOfDragons.AI
{
    /// <summary>
    /// Real gap flagged in the Full 36-Spell Catalogue Diagnosis (LOCKED 2026-08-24): "AI
    /// spellbooks currently mirror the PLAYER's progression-derived loadout rather than having
    /// their own stage/archetype-authored one." This resolves it with a stage/archetype-authored
    /// loadout keyed on the AI's own real difficulty tier (see SoloAIScalingSystem) instead of the
    /// player's ownedSpellIds/equippedSpellIds - who the player actually is (grinding hard,
    /// undergeared, mid-event) no longer leaks into what the enemy can cast.
    ///
    /// Deliberately reuses two numbers that are already locked rather than inventing new ones:
    /// AIDifficultyTier's own avatar-level band boundaries (SoloAIScalingSystem's Novice/
    /// Apprentice/Veteran/Master/Titan cutoffs, <=10/<=25/<=50/<=80/81+) and SpellUnlockResolver's
    /// existing per-spell AvatarLevel gates (Ember Wave L5, Rallying Gale L8, Stone Judgment L12).
    /// Each tier resolves as "the strongest Avatar-level-gated loadout a player AT THE TOP of this
    /// tier's own band could have equipped" - directly consistent with how SoloAIScalingSystem
    /// already treats each tier as "as strong as the strongest player who'd still be facing this
    /// difficulty," for HP/Resource. Reuses SpellLoadoutAutoEquip's exact "highest magnitude wins
    /// per effect type" selection, which for a scripted AI (no manual loadout choice to respect)
    /// is the right behavior rather than the staleness problem it is for a player's own loadout -
    /// an AI opponent should always play its single best option per effect type.
    ///
    /// Deliberately excludes every Stage-gated spell (Cinder Lash, Fault Line, Vital Spark,
    /// Renewal, Banner of Ashes) and both SpellBookGrant-gated spells (Sun Lance, Tempest Brand):
    /// passing unlockedStageIds: null to SpellUnlockResolver structurally excludes the Stage-kind
    /// rules, and the SpellBookGrant-kind rules are never satisfied by this resolver at all (see
    /// SpellUnlockResolver's own class doc). This is a real, NOT-YET-DECIDED gap, not an oversight:
    /// there is no locked mapping anywhere from "AI difficulty tier" to "which Campaign stages /
    /// spell books that tier's opponent would have cleared," and guessing one here would be
    /// inventing a balance number this class was explicitly told not to invent. Flagged back for a
    /// real design pass (a LOCKED_DECISIONS_REGISTER.md entry, same as the Phase-1 catalog itself
    /// got) before those 7 spells can enter the AI's pool.
    /// </summary>
    public static class AIEnemySpellbookResolver
    {
        public static List<AvatarSpell> ResolveSpellbook(AIDifficultyTier tier)
        {
            return SpellLoadoutAutoEquip.AutoEquip(TierRepresentativeAvatarLevel(tier), unlockedStageIds: null);
        }

        /// <summary>The top of each tier's own already-locked avatar-level band (SoloAIScalingSystem.
        /// DetermineTier) - not a new number, the existing boundary read back out.</summary>
        private static int TierRepresentativeAvatarLevel(AIDifficultyTier tier) => tier switch
        {
            AIDifficultyTier.Novice => 10,
            AIDifficultyTier.Apprentice => 25,
            AIDifficultyTier.Veteran => 50,
            AIDifficultyTier.Master => 80,
            AIDifficultyTier.Titan => 999, // Titan's band is 81+, open-ended - every AvatarLevel gate in the Phase-1 catalog (max L12) is already satisfied well below this.
            _ => 1,
        };
    }
}

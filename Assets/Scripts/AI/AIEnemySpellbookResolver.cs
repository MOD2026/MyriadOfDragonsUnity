using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;

namespace MyriadOfDragons.AI
{
    /// <summary>
    /// Real gap flagged in the Full 36-Spell Catalogue Diagnosis (LOCKED 2026-08-24): "AI
    /// spellbooks currently mirror the PLAYER's progression-derived loadout rather than having
    /// their own stage/archetype-authored one." Resolved with a loadout keyed purely on the AI's
    /// own real difficulty tier (see SoloAIScalingSystem) - it never reads player ownedSpellIds/
    /// equippedSpellIds/stage completion/Spell Book claims, so who the player actually is (grinding
    /// hard, undergeared, mid-event) can never leak into what the enemy can cast.
    ///
    /// Two independent pools feed the same tier, both cumulative and both sourced from already-
    /// locked material rather than invented here:
    ///
    /// 1. AvatarLevel-gated spells (Ember Wave L5, Rallying Gale L8, Stone Judgment L12) - resolved
    ///    by feeding each tier's own already-locked avatar-level band-top (SoloAIScalingSystem.
    ///    DetermineTier's own <=10/<=25/<=50/<=80/81+ cutoffs) through the existing
    ///    SpellUnlockResolver AvatarLevel gates.
    /// 2. Stage/SpellBookGrant-gated spells (Cinder Lash, Vital Spark, Fault Line, Renewal, Sun
    ///    Lance, Banner of Ashes, Tempest Brand) - previously excluded here entirely (no locked
    ///    tier-to-stage mapping existed). Now real: "AI Tier -> Stage-Gated Spell Access" (LOCKED
    ///    2026-08-24, docs/LOCKED_DECISIONS_REGISTER.md) is a separate, authored progression
    ///    fiction, not derived from SpellUnlockResolver's stage-id gates at all - StageGatedPoolByTier
    ///    below is that locked table, verbatim.
    ///
    /// Both pools are merged and run through SpellLoadoutAutoEquip's same "highest Magnitude wins
    /// per effect type" selection - correct for a scripted AI with no manual-loadout choice to
    /// respect, and per the locked decision, this means Cinder Lash/Vital Spark/Sun Lance/Tempest
    /// Brand become eligible at their tier but stay unselected (a weaker same-effect-type option
    /// always outclasses them by Magnitude); Fault Line/Renewal/Banner of Ashes do materially
    /// change tier loadouts, since nothing in the AvatarLevel-gated pool competes with them for
    /// LaneDamage/LaneHeal/LaneAttackBuff at the exact Magnitude/effect combination they occupy.
    /// </summary>
    public static class AIEnemySpellbookResolver
    {
        /// <summary>AI Tier -> Stage-Gated Spell Access (LOCKED 2026-08-24): cumulative, authored
        /// progression fiction. Verbatim from the register - do not re-derive from
        /// SpellUnlockResolver's stage gates, which encode the PLAYER's own campaign-stage
        /// progression and are a different, unrelated fact.</summary>
        private static readonly Dictionary<AIDifficultyTier, string[]> StageGatedPoolByTier = new Dictionary<AIDifficultyTier, string[]>
        {
            [AIDifficultyTier.Novice] = new[] { "cinder_lash", "vital_spark" },
            [AIDifficultyTier.Apprentice] = new[] { "cinder_lash", "vital_spark", "fault_line", "renewal" },
            [AIDifficultyTier.Veteran] = new[] { "cinder_lash", "vital_spark", "fault_line", "renewal", "sun_lance", "banner_of_ashes" },
            [AIDifficultyTier.Master] = new[] { "cinder_lash", "vital_spark", "fault_line", "renewal", "sun_lance", "banner_of_ashes", "tempest_brand" },
            [AIDifficultyTier.Titan] = new[] { "cinder_lash", "vital_spark", "fault_line", "renewal", "sun_lance", "banner_of_ashes", "tempest_brand" },
        };

        /// <summary>Every SpellEffect AISpellCaster.TrySelectCast's EffectPriority loop actually
        /// visits - see AISpellCaster.cs. Any other effect (LaneShield/Cleanse/Dispel/
        /// Vulnerability/AllLaneAttackBuff/CrossLaneDamage/AllLaneDamage/DrawCards) has no
        /// TryPickTarget case and no EffectPriority entry, so the AI never even attempts it -
        /// winning a loadout slot via raw Magnitude ranking (SelectHighestMagnitudePerEffect,
        /// below) does not make it castable.</summary>
        private static readonly HashSet<SpellEffect> AiCastableEffects = new HashSet<SpellEffect>
        {
            SpellEffect.LaneHeal, SpellEffect.LaneDamage, SpellEffect.LaneAttackBuff,
            SpellEffect.AvatarStrike, SpellEffect.Reposition,
        };

        public static List<AvatarSpell> ResolveSpellbook(AIDifficultyTier tier)
        {
            List<AvatarSpell> catalog = AvatarSpell.CreatePhase1Catalog();

            List<AvatarSpell> avatarLevelPool = SpellUnlockResolver.ResolveUnlockedSpells(TierRepresentativeAvatarLevel(tier), unlockedStageIds: null);

            IEnumerable<string> stageGatedIds = StageGatedPoolByTier.TryGetValue(tier, out string[] ids) ? ids : System.Array.Empty<string>();
            List<AvatarSpell> stageGatedPool = catalog.Where(s => stageGatedIds.Contains(s.Id)).ToList();

            // Dead-loadout-slot fix (LOCKED 2026-08-25): the per-spell impact diagnostic found
            // Cleansing Root (Cleanse) and Oracle Sight (DrawCards) winning VeteranPlus loadout
            // slots via Magnitude ranking alone, then firing zero times across 1500 trials -
            // AISpellCaster structurally never attempts either effect (see AiCastableEffects
            // above). Filtering the pool to AI-castable effects BEFORE the highest-Magnitude
            // selection below means that slot now goes to a real, eligible spell instead - a
            // loadout-selection fix, not a gate/magnitude change to any spell.
            List<AvatarSpell> pool = avatarLevelPool.Concat(stageGatedPool).Distinct()
                .Where(s => AiCastableEffects.Contains(s.Effect)).ToList();
            // Loadout expansion (LOCKED 2026-08-25): "AI may equip up to the same 6-slot cap,
            // same effect/AvatarStrike rules, tier-gated as before" - reuses the same
            // avatar-level-representative mapping this class already had, rather than a second
            // AI-specific slot table.
            int slotCount = SpellLoadoutAutoEquip.RequiredSlotCount(TierRepresentativeAvatarLevel(tier));
            return SpellLoadoutAutoEquip.SelectHighestMagnitudePerEffect(pool, slotCount);
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

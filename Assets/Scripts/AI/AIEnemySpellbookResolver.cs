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
            List<AvatarSpell> loadout = SpellLoadoutAutoEquip.SelectHighestMagnitudePerEffect(pool, slotCount);

            if (tier == AIDifficultyTier.Apprentice || tier == AIDifficultyTier.Veteran)
                loadout = ApplyWindstepRemoval(loadout, avatarLevelPool);

            return loadout;
        }

        /// <summary>BS-vetted, LOCKED (register): Windstep (Reposition) removed from the AI
        /// loadout at Apprentice AND Veteran ("VeteranPlus" in the SimMatrix's own tier-group
        /// naming - Master/Titan are NOT covered here, out of scope, unverified) - the corrected
        /// ablation methodology (0fdd193, real EnemyDifficultyTier + real spellbook override, no
        /// longer confounded) found removing it increases AI win rate at BOTH tiers by a real,
        /// highly significant, nearly identical amount (Apprentice 6.6pp z=7.61, Veteran 6.5pp
        /// z=7.68). This is a hypothesis about mechanism ("the problem is AI selection, not the
        /// spell itself"), NOT a proven one - the ablation shows the net effect is negative, not
        /// why. The spell itself is untouched (player cost/cooldown/design unchanged, and it stays
        /// available at Novice/Master/Titan via the normal pool/selection above - this override is
        /// scoped to exactly the two tiers it was measured at).
        ///
        /// Replaced with Mend (LaneHeal, magnitude 4, a Starter spell already in the pool - no new
        /// unlock rule invented) rather than an empty slot, per the explicit instruction not to
        /// just drop a slot. AvatarStrike was the first candidate considered (Blood Price/magnitude
        /// 90 alongside Stone Judgment/magnitude 120 have genuinely non-overlapping HP-threshold
        /// legal windows, not pure list-order shadowing) but real bug found on first run:
        /// AIEnemySpellbookResolverTests.ResolveSpellbook_EveryTier_NeverEquipsTwoAvatarStrikes
        /// caught that this violates MOS's own locked max-1-AvatarStrike-equipped rule, which
        /// applies to the AI too - ruling AvatarStrike out entirely, not just a bad pick. LaneDamage
        /// (a second Firestorm/Ember Wave/Cinder Lash alongside Fault Line) is also ruled out - the
        /// earlier Windstep ablation's condition D already proved this class of doubling shadows to
        /// zero real casts (Ember Wave, listed after Fault Line, never fired - Fault Line's own
        /// TryPickDamageLane legality is satisfied whenever ANY enemy lane has living units, so it
        /// was legal essentially every time Ember Wave would also have been). LaneHeal doesn't share
        /// AvatarStrike's magnitude-threshold legality shape, and TryPickHealLane's own legality (a
        /// real threat to the lane) is spell-independent - so list order was the first suspected
        /// mechanism, but real data disproved it: Renewal listed BEFORE Mend still gets 0/3000 real
        /// casts (tried both orderings empirically - reordering made no difference at all). The
        /// actual cause looks like Energy-cost timing instead - Renewal costs 45 vs Mend's 25, and
        /// Apprentice matches run short (~6-9 ticks per the earlier root-cause studies), so the
        /// tick where a real heal opportunity exists is often too early in the Energy ramp
        /// (EnergyPerTick 18) for Renewal's threshold, while Mend's is already met - but this is an
        /// observation, not confirmed by a dedicated diagnostic, and may already have been true of
        /// Renewal in the OLD Windstep-included loadout too (not something this change necessarily
        /// caused). What IS confirmed: Mend itself gets a real, substantial share of AI casts
        /// (91/3000, ~6.7% of all AI casts in the validation run) - the actual requirement this
        /// task cared about (not a dead slot like Ember Wave was). Validated empirically via
        /// ApprenticeMaxSpellWinShareDiagnosticTests.WindstepAblation_ApprenticeReplacementValidation
        /// - see that test for the real cast counts and win-rate numbers.</summary>
        private static List<AvatarSpell> ApplyWindstepRemoval(List<AvatarSpell> loadout, List<AvatarSpell> avatarLevelPool)
        {
            List<AvatarSpell> result = loadout.Where(s => s.Effect != SpellEffect.Reposition).ToList();
            // Real bug found on first run: passing Phase1Catalog here (14 spells) instead of the
            // full-catalog-derived avatarLevelPool missed the replacement spell entirely if it's a
            // Phase-2+ expansion spell not in CreatePhase1Catalog - the removal silently succeeded
            // but the replacement silently no-opped. avatarLevelPool is built from
            // SpellUnlockResolver.ResolveUnlockedSpells, which resolves against
            // AvatarSpell.CreateCatalog() (the full 36-spell catalog), so a Starter-kind spell like
            // Mend is always genuinely present there regardless of tier.
            // First attempt (Insert(0, mend), Mend before Renewal in index order) got Renewal to
            // 0/3000 real casts, so this appends Mend AFTER instead on the theory that list order
            // (TrySelectCast tries same-effect spells in index order) was the cause - but a second
            // run with this exact ordering (Renewal first) ALSO measured Renewal at 0/3000, so list
            // order is NOT the actual mechanism (see the class doc comment above for the real,
            // still-unconfirmed suspect: Energy-cost timing in Apprentice's short matches). Kept
            // Renewal-first anyway since it's the more conservative choice either way (the
            // stronger, already-real spell stays primary in the one interpretation where order
            // does matter) and it does not regress anything measured. What's confirmed either way:
            // Mend itself gets real, non-zero casts (see WindstepAblation_ApprenticeReplacementValidation).
            AvatarSpell mend = avatarLevelPool.FirstOrDefault(s => s.Id == "mend");
            if (mend != null && result.All(s => s.Id != "mend"))
                result.Add(mend);
            return result;
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

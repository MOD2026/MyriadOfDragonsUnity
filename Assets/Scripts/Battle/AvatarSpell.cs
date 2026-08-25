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

        /// <summary>Grant a Shield to every living friendly unit in the target lane (replaces, doesn't stack).</summary>
        LaneShield,

        /// <summary>Remove the Vulnerability mark from every living friendly unit in the target lane.</summary>
        Cleanse,

        /// <summary>Remove Shield from every living enemy unit in the target lane.</summary>
        Dispel,

        /// <summary>Mark every living enemy unit in the target lane: their next hit taken deals +1 damage, then the mark is consumed (or expires unused at the end of the clash it was cast into).</summary>
        Vulnerability,

        /// <summary>Permanently raise the Attack of every living friendly unit across all three lanes (subject to the same +3/unit spell-buff cap as LaneAttackBuff).</summary>
        AllLaneAttackBuff,

        /// <summary>Damage every living enemy unit in the target lane at Magnitude, and every living enemy unit in each lane adjacent to it (Front-Middle-Back, no wrap-around) at SecondaryMagnitude.</summary>
        CrossLaneDamage,

        /// <summary>Damage every living enemy unit independently in all three enemy lanes at Magnitude.</summary>
        AllLaneDamage,

        /// <summary>Draw Magnitude cards for the caster from their own draw pile - never creates cards, safely no-ops once the pile is empty.</summary>
        DrawCards,
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

        /// <summary>Wave 4: CrossLaneDamage's adjacent-lane damage (its primary-lane damage is
        /// still Magnitude). Unused (0) by every other effect type.</summary>
        public readonly int SecondaryMagnitude;

        /// <summary>Ticks remaining before this can be cast again. 0 means ready.</summary>
        public int CooldownRemaining { get; private set; }

        /// <summary>id/school are trailing optional params, not inserted into the existing
        /// positional order - every pre-existing call site (production catalog and several test
        /// files that construct synthetic spells directly) keeps compiling unchanged. A spell
        /// built without an explicit id gets "" (never a real catalog member, never matches a
        /// real ownedSpellIds/equippedSpellIds entry - correct for a synthetic test spell that
        /// isn't part of the real catalog anyway).</summary>
        public AvatarSpell(string name, string description, int energyCost, int cooldownTicks,
            SpellEffect effect, int magnitude, string id = "", SpellSchool school = SpellSchool.Andras,
            int secondaryMagnitude = 0)
        {
            Id = id;
            Name = name;
            Description = description;
            EnergyCost = energyCost;
            CooldownTicks = cooldownTicks;
            Effect = effect;
            Magnitude = magnitude;
            School = school;
            SecondaryMagnitude = secondaryMagnitude;
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
                    return DealLaneDamage(opponent, targetLane, Magnitude);

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

                case SpellEffect.LaneShield:
                    foreach (BattleCardInstance unit in LivingUnits(caster, targetLane))
                    {
                        unit.ApplyShield(Magnitude);
                    }
                    return 0;

                case SpellEffect.Cleanse:
                    foreach (BattleCardInstance unit in LivingUnits(caster, targetLane))
                    {
                        unit.ClearVulnerabilityMark();
                    }
                    return 0;

                case SpellEffect.Dispel:
                    foreach (BattleCardInstance unit in LivingUnits(opponent, targetLane))
                    {
                        unit.ClearShield();
                    }
                    return 0;

                case SpellEffect.Vulnerability:
                    foreach (BattleCardInstance unit in LivingUnits(opponent, targetLane))
                    {
                        unit.MarkVulnerable();
                    }
                    return 0;

                case SpellEffect.AllLaneAttackBuff:
                    foreach (Lane lane in caster.Lanes.Keys.ToList())
                    {
                        foreach (BattleCardInstance unit in LivingUnits(caster, lane))
                        {
                            unit.BuffAttack(Magnitude);
                        }
                    }
                    return 0;

                case SpellEffect.CrossLaneDamage:
                    int crossLaneTotal = DealLaneDamage(opponent, targetLane, Magnitude);
                    foreach (Lane adjacent in AdjacentLanes(targetLane))
                    {
                        crossLaneTotal += DealLaneDamage(opponent, adjacent, SecondaryMagnitude);
                    }
                    return crossLaneTotal;

                case SpellEffect.AllLaneDamage:
                    int allLaneTotal = 0;
                    foreach (Lane lane in opponent.Lanes.Keys.ToList())
                    {
                        allLaneTotal += DealLaneDamage(opponent, lane, Magnitude);
                    }
                    return allLaneTotal;

                case SpellEffect.DrawCards:
                    for (int i = 0; i < Magnitude; i++)
                    {
                        caster.DrawCard();
                    }
                    return 0;

                default:
                    return 0;
            }
        }

        /// <summary>Shared by LaneDamage/CrossLaneDamage/AllLaneDamage: damage every living
        /// defender in the lane, or - if it has none - let the damage through to the Avatar at
        /// the same fixed scale lane-combat overflow already uses (LaneBattleResolver.
        /// ApplyDamageToLane). Without this an undefended lane silently absorbed the spell instead
        /// of the damage reaching the Avatar (see LaneDamage's original 2026-08-06 bug note).
        /// Returns the Avatar damage dealt (0 unless the lane was undefended).</summary>
        private static int DealLaneDamage(PlayerBattleState opponent, Lane lane, int magnitude)
        {
            List<BattleCardInstance> defenders = LivingUnits(opponent, lane);
            if (defenders.Count == 0)
            {
                int through = Math.Min(magnitude * AvatarDamageScale, opponent.AvatarHealth);
                opponent.AvatarHealth -= through;
                return through;
            }

            foreach (BattleCardInstance unit in defenders)
            {
                unit.ApplyDamage(magnitude);
            }
            // Dead units are pruned during lane resolution (LaneBattleResolver), not here - keeping
            // removal in one place avoids two different rules about when a corpse stops occupying
            // its slot.
            return 0;
        }

        /// <summary>Front-Middle-Back, no wrap-around (catalog lock): Front/Back each have exactly
        /// one neighbor, Middle has both.</summary>
        private static IEnumerable<Lane> AdjacentLanes(Lane lane)
        {
            switch (lane)
            {
                case Lane.Front:
                    yield return Lane.Middle;
                    break;
                case Lane.Middle:
                    yield return Lane.Front;
                    yield return Lane.Back;
                    break;
                case Lane.Back:
                    yield return Lane.Middle;
                    break;
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

        /// <summary>
        /// Wave 2 (Full 36-Spell Catalogue Diagnosis, LOCKED 2026-08-24, "expand to 19 using
        /// already-implemented effects"): five Phase-2-catalog spells that use only the four live
        /// <see cref="SpellEffect"/> values, so no new Cast() case or state is needed - unlike the
        /// rest of the 22-spell Phase-2 list (LaneShield, CrossLaneDamage, Silence, etc.), which
        /// stays genuinely blocked until those effect contracts exist.
        ///
        /// Locked corrections applied here, not the catalog doc's raw numbers: Celestial Verdict
        /// 110-&gt;100 Energy (110 exceeds BattleController.MaxEnergy=100 - literally uncastable as
        /// printed). Blood Price Energy 38-&gt;55 ("the price is commitment cost, no self-damage" -
        /// see the register's own note on why 38 was too cheap for a 90-flat direct strike).
        /// </summary>
        public static List<AvatarSpell> CreatePhase2ExpansionSpells()
        {
            return new List<AvatarSpell>
            {
                new AvatarSpell("Magma Rend", "Deal 6 damage to every enemy unit in a lane.",
                    energyCost: 62, cooldownTicks: 6, SpellEffect.LaneDamage, magnitude: 6,
                    id: "magma_rend", school: SpellSchool.Andras),

                // Locked correction: Energy 38->55.
                new AvatarSpell("Blood Price", "Strike the enemy Avatar directly for 90.",
                    energyCost: 55, cooldownTicks: 4, SpellEffect.AvatarStrike, magnitude: 90,
                    id: "blood_price", school: SpellSchool.Andras),

                new AvatarSpell("Grave Mend", "Restore 9 Health to every friendly unit in a lane.",
                    energyCost: 68, cooldownTicks: 7, SpellEffect.LaneHeal, magnitude: 9,
                    id: "grave_mend", school: SpellSchool.Ktini),

                // Locked correction: Energy 110->100 (110 exceeds the hard-capped MaxEnergy=100 -
                // uncastable as originally printed in the catalog doc). Magnitude (150 flat) is
                // unaffected - that's Avatar-HP damage, not Energy, and the doc's own balance
                // section already stacks it against a pool measured in the hundreds/thousands.
                new AvatarSpell("Celestial Verdict", "Strike the enemy Avatar directly for 150.",
                    energyCost: 100, cooldownTicks: 9, SpellEffect.AvatarStrike, magnitude: 150,
                    id: "celestial_verdict", school: SpellSchool.Pnevmas),

                new AvatarSpell("Aegis Return", "Restore 7 Health to every friendly unit in a lane.",
                    energyCost: 52, cooldownTicks: 5, SpellEffect.LaneHeal, magnitude: 7,
                    id: "aegis_return", school: SpellSchool.Ktini),
            };
        }

        /// <summary>
        /// Wave 3 (Full 36-Spell Catalogue Diagnosis, LOCKED 2026-08-24): Shields, Cleanse,
        /// Dispel, Vulnerability, and Thunder Decree's all-lane buff - eight spells, five new
        /// SpellEffect values, real BattleCardInstance state (Shield, Vulnerability mark, capped
        /// spell-Attack-buff tracking).
        ///
        /// Locked corrections applied here, not the catalog doc's raw numbers: Veil of Zeus
        /// shield 6-&gt;10/unit (register: "6 is trivial against Fault Line's own 5 lane damage;
        /// 10 makes it actually absorb something"). Shields don't stack additively - a second
        /// Shield replaces a weaker one rather than adding (BattleCardInstance.ApplyShield).
        /// Cleanse targets the caster's own lane (removes a hostile Vulnerability mark placed on
        /// it); Dispel targets the enemy's lane (strips their Shield) - the catalog doc's own
        /// "hostile"/"positive" modifier language, applied against the only two modifier types
        /// that exist as of this wave. This mapping is this implementation's own inference from
        /// that language, not a separately re-confirmed locked line - flagged here rather than
        /// presented as pre-locked fact.
        /// </summary>
        public static List<AvatarSpell> CreatePhase3ExpansionSpells()
        {
            return new List<AvatarSpell>
            {
                new AvatarSpell("Ember Guard", "Grant a Shield absorbing 5 damage to every friendly unit in a lane.",
                    energyCost: 30, cooldownTicks: 4, SpellEffect.LaneShield, magnitude: 5,
                    id: "ember_guard", school: SpellSchool.Andras),

                new AvatarSpell("Earthward", "Grant a Shield absorbing 7 damage to every friendly unit in a lane.",
                    energyCost: 42, cooldownTicks: 5, SpellEffect.LaneShield, magnitude: 7,
                    id: "earthward", school: SpellSchool.Ktini),

                new AvatarSpell("Stonewall", "Grant a Shield absorbing 8 damage to every friendly unit in a lane.",
                    energyCost: 50, cooldownTicks: 6, SpellEffect.LaneShield, magnitude: 8,
                    id: "stonewall", school: SpellSchool.Ktini),

                // Locked correction: Shield magnitude 6->10/unit.
                new AvatarSpell("Veil of Zeus", "Grant a Shield absorbing 10 damage to every friendly unit in a lane.",
                    energyCost: 58, cooldownTicks: 6, SpellEffect.LaneShield, magnitude: 10,
                    id: "veil_of_zeus", school: SpellSchool.Pnevmas),

                new AvatarSpell("Cleansing Root", "Remove the Vulnerability mark from every friendly unit in a lane.",
                    energyCost: 20, cooldownTicks: 3, SpellEffect.Cleanse, magnitude: 0,
                    id: "cleansing_root", school: SpellSchool.Ktini),

                new AvatarSpell("Gale Break", "Remove Shield from every enemy unit in a lane.",
                    energyCost: 26, cooldownTicks: 3, SpellEffect.Dispel, magnitude: 0,
                    id: "gale_break", school: SpellSchool.Pnevmas),

                new AvatarSpell("Infernal Mark", "Mark every enemy unit in a lane: their next hit taken deals +1 damage.",
                    energyCost: 24, cooldownTicks: 3, SpellEffect.Vulnerability, magnitude: 0,
                    id: "infernal_mark", school: SpellSchool.Andras),

                new AvatarSpell("Thunder Decree", "Permanently grant +1 Attack to every friendly lane.",
                    energyCost: 65, cooldownTicks: 7, SpellEffect.AllLaneAttackBuff, magnitude: 1,
                    id: "thunder_decree", school: SpellSchool.Pnevmas),
            };
        }

        /// <summary>
        /// Wave 4 (Full 36-Spell Catalogue Diagnosis, LOCKED 2026-08-24, "CrossLane/AllLane
        /// damage+DrawCards+Reposition"): five of the wave's seven catalog spells - Ashfall/
        /// Stormchain (CrossLaneDamage), Scorched Sky (AllLaneDamage), Leyline Draw/Oracle Sight
        /// (DrawCards). Reposition's two spells (Windstep, Seismic Swap) are deliberately NOT
        /// here - see the class doc's own flag on CreateCatalog for why.
        ///
        /// Locked correction applied here, not the catalog doc's raw number: Oracle Sight Energy
        /// 42-&gt;30 (register: "still draw-2/CD4, now cheaper-but-slower vs Leyline Draw's 36E,
        /// neither dominates").
        ///
        /// CrossLaneDamage's adjacent-lane damage uses AvatarSpell.SecondaryMagnitude, a field
        /// unused (0) by every other effect type - Ashfall is 4 main/1 adjacent, Stormchain is 3
        /// main/2 adjacent, matching the catalog doc's own two numbers per row exactly.
        /// </summary>
        public static List<AvatarSpell> CreatePhase4ExpansionSpells()
        {
            return new List<AvatarSpell>
            {
                new AvatarSpell("Ashfall", "Deal 4 damage to every enemy unit in a lane, and 1 to each adjacent lane.",
                    energyCost: 70, cooldownTicks: 7, SpellEffect.CrossLaneDamage, magnitude: 4,
                    id: "ashfall", school: SpellSchool.Andras, secondaryMagnitude: 1),

                new AvatarSpell("Stormchain", "Deal 3 damage to every enemy unit in a lane, and 2 to each adjacent lane.",
                    energyCost: 58, cooldownTicks: 6, SpellEffect.CrossLaneDamage, magnitude: 3,
                    id: "stormchain", school: SpellSchool.Pnevmas, secondaryMagnitude: 2),

                new AvatarSpell("Scorched Sky", "Deal 3 damage to every enemy unit, independently, in all three enemy lanes.",
                    energyCost: 95, cooldownTicks: 8, SpellEffect.AllLaneDamage, magnitude: 3,
                    id: "scorched_sky", school: SpellSchool.Andras),

                new AvatarSpell("Leyline Draw", "Draw 2 cards.",
                    energyCost: 36, cooldownTicks: 4, SpellEffect.DrawCards, magnitude: 2,
                    id: "leyline_draw", school: SpellSchool.Ktini),

                // Locked correction: Energy 42->30.
                new AvatarSpell("Oracle Sight", "Draw 2 cards.",
                    energyCost: 30, cooldownTicks: 4, SpellEffect.DrawCards, magnitude: 2,
                    id: "oracle_sight", school: SpellSchool.Pnevmas),
            };
        }

        /// <summary>
        /// The real, full castable catalog (32 as of Wave 4 - CreatePhase1Catalog's 14, Wave 2's
        /// 5, Wave 3's 8, plus this wave's 5) - the source of truth for anything that must resolve
        /// a spell id into a real AvatarSpell: BattleController.ResolveMatchSpellbook,
        /// SpellBookGrant's id validation, and SpellUnlockResolver's own iteration - a catalog
        /// member with no Rule entry (Aegis Return; Ember Guard/Earthward/Gale Break/Thunder
        /// Decree, whose Phase-2 catalog Unlock column only gives a bare chapter number with no
        /// stage-level precision to build a real Rule from) simply never unlocks, the same
        /// behaviour a stale/unrecognized id already had.
        ///
        /// NOT 36 yet, and deliberately so: Reposition's two spells (Windstep "move one friendly
        /// unit to an adjacent lane", Seismic Swap "exchange two units, any lanes") are the one
        /// piece of Wave 4 left out. Every other effect implemented so far - Wave 1 through the
        /// rest of Wave 4 - acts uniformly on "every living unit in a lane"; Cast()'s own signature
        /// (caster, opponent, targetLane) has no way to name a SPECIFIC unit or a second
        /// destination lane, which Reposition genuinely needs (which unit moves, and - for Seismic
        /// Swap - which two units, possibly in two different lanes, swap). Extending Cast()'s
        /// targeting arity and building real single-unit-selection UI is a materially bigger
        /// change than every effect shipped in Waves 1-4 so far, not a mechanical add-a-case -
        /// flagged rather than guessed at (an automatic "always move the lowest-Health unit"
        /// heuristic would be exactly that kind of guess). 32/36 real; 4 blocked total (these 2
        /// Reposition spells, plus Volcanic Prison/Titan Seal - Wave 5's Silence package, blocked
        /// since no suppressible-ability system exists at all).
        /// </summary>
        public static List<AvatarSpell> CreateCatalog()
        {
            List<AvatarSpell> catalog = CreatePhase1Catalog();
            catalog.AddRange(CreatePhase2ExpansionSpells());
            catalog.AddRange(CreatePhase3ExpansionSpells());
            catalog.AddRange(CreatePhase4ExpansionSpells());
            return catalog;
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;

namespace MyriadOfDragons.AI
{
    /// <summary>
    /// Mirrored PvE AI spell heuristics (SPELL_CATALOG_v1 §5 / Option B). Plain methods only —
    /// BattleController.AdvanceCombatTick invokes <see cref="TryCastDuringCombatTick"/>.
    ///
    /// AUDITED 2026-08-24 against the Mirrored AI-Spellcasting Simulation Matrix's real results
    /// (docs/LOCKED_DECISIONS_REGISTER.md): the matrix found AI cast rate ~99.9% of legal
    /// opportunities against a locked 25-70% band, and traced it to this class only checking §5's
    /// unconditional rules (loadout/economy/cadence - all already correct) while skipping §5's own
    /// per-spell-type restraint clauses entirely. Two real fixes, both grounded in already-real,
    /// already-computable game state - no new mechanic invented, no per-unit damage-distribution
    /// duplicated (see the project's own standing warning against an external replica of combat
    /// math silently drifting from the real rules):
    ///
    /// - Mend/Vital Spark ("...when that lane has a living unit and the heal changes survival"):
    ///   previously fired on ANY missing Health at all, which is why it fired almost every tick.
    ///   "Changes survival" is operationalized as "a real clash is about to threaten this lane" -
    ///   the opposing lane has at least one living attacker (so damage will actually land here this
    ///   tick) AND at least one friendly living unit's CurrentHealth is at or below that opposing
    ///   lane's total living Attack (the same total-Attack figure LaneBattleResolver.
    ///   ResolveLaneClash itself starts from, read here rather than duplicated). This is
    ///   deliberately NOT a prediction of exactly which unit dies - ApplyDamageToLane's real
    ///   taunt-first/sequential distribution is genuine per-unit logic this class does not
    ///   reimplement - just "is there a real, present threat to this lane right now."
    /// - Firestorm/Cinder Lash/Ember Wave ("...preferring a lane where the damage can defeat a
    ///   unit"): previously ignored entirely - picked purely by living-unit COUNT. Now prefers any
    ///   lane where the spell's own flat Magnitude would actually drop a living unit's
    ///   CurrentHealth to 0 (AvatarSpell.Cast's real LaneDamage effect applies Magnitude uniformly
    ///   per living unit, so this is exact, not approximated), falling back to the old
    ///   highest-living-count tiebreak only when no lane offers a kill.
    ///
    /// Two clauses deliberately left unchanged, not silently reinterpreted:
    /// - War Cry/Rallying Gale ("...only if the chosen AI lane contains at least two living cards
    ///   and no stronger immediate heal/damage response exists"): the >=2-card check was already
    ///   real. "No stronger response exists" is already structurally guaranteed by EffectPriority
    ///   itself - TrySelectCast tries LaneHeal then LaneDamage before LaneAttackBuff and returns on
    ///   the first legal pick, so a buff is only ever attempted when no heal/damage cast was legal
    ///   this tick. No separate check was added; this comment exists so a future reader doesn't
    ///   "fix" what's already correct by a different mechanism.
    /// - Divine Bolt/Sun Lance/Stone Judgment ("...only when it is lethal OR materially shortens a
    ///   reachable finish"): only the lethal half is implemented (see TryPickTarget's AvatarStrike
    ///   case, unchanged). "Materially shortens a reachable finish" has no locked numeric threshold
    ///   anywhere (what fraction of remaining Health counts as "material"?) - flagged as genuinely
    ///   ambiguous rather than guessed at. Left as lethal-only deliberately: that is the STRICTER,
    ///   more conservative reading, which works with this fix's own goal (less over-casting) rather
    ///   than against it - loosening this gate would only make AvatarStrike fire MORE often.
    /// </summary>
    public static class AISpellCaster
    {
        public const int MinTickForAnySpell = 2;
        public const int MinTickForAvatarStrike = 3;

        public static bool TryCastDuringCombatTick(BattleController controller)
        {
            if (controller == null || controller.Phase != BattlePhase.Combat) return false;
            if (controller.TickCount < MinTickForAnySpell) return false;

            if (!TrySelectCast(
                    controller.EnemySpellbook,
                    controller.EnemyEnergy,
                    controller.TickCount,
                    controller.EnemyState,
                    controller.PlayerState,
                    out int spellIndex,
                    out Lane targetLane))
                return false;

            // AI Spell Cast Probability Gate (LOCKED 2026-08-24): §5's tactical clauses above
            // already gated candidacy (quality) - this gates frequency, a separate problem. Rolled
            // only now that a legal candidate genuinely exists ("no roll if no valid candidate");
            // a failed roll is a deliberate pass, no reroll, no state mutated.
            if (!controller.RollAiSpellCastProbabilityGate()) return false;

            return controller.TryCastEnemySpell(spellIndex, targetLane, out _);
        }

        /// <summary>Deterministic spell + lane pick for one AI cast attempt.</summary>
        public static bool TrySelectCast(
            IReadOnlyList<AvatarSpell> spellbook,
            int energy,
            int tickCount,
            PlayerBattleState aiSide,
            PlayerBattleState playerSide,
            out int spellIndex,
            out Lane targetLane)
        {
            spellIndex = -1;
            targetLane = Lane.Front;

            if (spellbook == null || spellbook.Count == 0 || tickCount < MinTickForAnySpell)
                return false;

            foreach (SpellEffect effect in EffectPriority)
            {
                if (effect == SpellEffect.AvatarStrike && tickCount < MinTickForAvatarStrike)
                    continue;

                for (int i = 0; i < spellbook.Count; i++)
                {
                    AvatarSpell spell = spellbook[i];
                    if (spell.Effect != effect) continue;
                    if (!spell.IsOffCooldown || spell.EnergyCost > energy) continue;
                    if (!TryPickTarget(spell.Effect, spell, aiSide, playerSide, out Lane lane))
                        continue;

                    spellIndex = i;
                    targetLane = lane;
                    return true;
                }
            }

            return false;
        }

        private static readonly SpellEffect[] EffectPriority =
        {
            SpellEffect.LaneHeal,
            SpellEffect.LaneDamage,
            SpellEffect.LaneAttackBuff,
            SpellEffect.AvatarStrike,
        };

        private static bool TryPickTarget(
            SpellEffect effect,
            AvatarSpell spell,
            PlayerBattleState aiSide,
            PlayerBattleState playerSide,
            out Lane lane)
        {
            switch (effect)
            {
                case SpellEffect.LaneHeal:
                    return TryPickHealLane(aiSide, playerSide, out lane);

                case SpellEffect.LaneDamage:
                    return TryPickDamageLane(playerSide, spell, out lane);

                case SpellEffect.LaneAttackBuff:
                    return TryPickBuffLane(aiSide, out lane);

                case SpellEffect.AvatarStrike:
                    if (playerSide.AvatarHealth > spell.Magnitude) break;
                    lane = Lane.Front;
                    return true;

                default:
                    break;
            }

            lane = Lane.Front;
            return false;
        }

        /// <summary>"...when that lane has a living unit and the heal changes survival" - only
        /// considers a lane where the opposing lane's own real total living Attack (the same
        /// figure LaneBattleResolver.ResolveLaneClash itself sums) is actually about to threaten a
        /// living friendly unit here, not merely "some Health is missing." See the class doc for
        /// why this stops short of predicting exactly which unit would die.</summary>
        private static bool TryPickHealLane(PlayerBattleState aiSide, PlayerBattleState opposingSide, out Lane lane)
        {
            lane = Lane.Front;
            int bestMissing = 0;

            foreach (Lane candidate in AllLanes)
            {
                int missing = MissingHealthInLane(aiSide, candidate);
                if (missing <= 0) continue;
                if (!LaneIsUnderRealThreat(aiSide, opposingSide, candidate)) continue;
                if (missing > bestMissing)
                {
                    bestMissing = missing;
                    lane = candidate;
                }
            }

            return bestMissing > 0;
        }

        /// <summary>True when this lane has a real, present clash threat this tick: the opposing
        /// lane has at least one living attacker, and at least one living friendly unit here would
        /// be at or below that opposing lane's total Attack if the clash resolved right now.</summary>
        private static bool LaneIsUnderRealThreat(PlayerBattleState aiSide, PlayerBattleState opposingSide, Lane lane)
        {
            int incomingAttack = opposingSide.Lanes[lane].Cards.Where(c => c.IsAlive).Sum(c => c.Attack);
            if (incomingAttack <= 0) return false;

            return aiSide.Lanes[lane].Cards.Any(c => c.IsAlive && c.CurrentHealth <= incomingAttack);
        }

        /// <summary>"...preferring a lane where the damage can defeat a unit" - AvatarSpell.Cast's
        /// real LaneDamage effect applies Magnitude uniformly to every living unit in the lane, so
        /// "can defeat a unit" is exact here (CurrentHealth &lt;= Magnitude), not approximated.
        /// Falls back to the old highest-living-count tiebreak only when no lane offers a kill.</summary>
        private static bool TryPickDamageLane(PlayerBattleState playerSide, AvatarSpell spell, out Lane lane)
        {
            lane = Lane.Front;
            int bestCount = 0;
            bool foundKillLane = false;

            foreach (Lane candidate in AllLanes)
            {
                int living = LivingCount(playerSide, candidate);
                if (living <= 0) continue;

                bool canKill = playerSide.Lanes[candidate].Cards.Any(c => c.IsAlive && c.CurrentHealth <= spell.Magnitude);

                if (canKill && !foundKillLane)
                {
                    // First kill-capable lane found - always beats any non-kill lane seen so far.
                    foundKillLane = true;
                    bestCount = living;
                    lane = candidate;
                }
                else if (canKill == foundKillLane && living > bestCount)
                {
                    // Both kill-capable (or both not) - tiebreak on living count, same as before.
                    bestCount = living;
                    lane = candidate;
                }
            }

            return bestCount > 0;
        }

        private static bool TryPickBuffLane(PlayerBattleState aiSide, out Lane lane)
        {
            foreach (Lane candidate in AllLanes)
            {
                if (LivingCount(aiSide, candidate) >= 2)
                {
                    lane = candidate;
                    return true;
                }
            }

            lane = Lane.Front;
            return false;
        }

        private static int LivingCount(PlayerBattleState side, Lane lane) =>
            side.Lanes[lane].Cards.Count(c => c.IsAlive);

        private static int MissingHealthInLane(PlayerBattleState side, Lane lane)
        {
            int missing = 0;
            foreach (BattleCardInstance unit in side.Lanes[lane].Cards)
            {
                if (!unit.IsAlive) continue;
                missing += unit.MaxHealth - unit.CurrentHealth;
            }

            return missing;
        }

        private static readonly Lane[] AllLanes = { Lane.Front, Lane.Middle, Lane.Back };
    }
}

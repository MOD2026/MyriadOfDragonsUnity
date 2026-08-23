using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;

namespace MyriadOfDragons.AI
{
    /// <summary>
    /// Mirrored PvE AI spell heuristics (SPELL_CATALOG_v1 §5 / Option B). Plain methods only —
    /// BattleController.AdvanceCombatTick invokes <see cref="TryCastDuringCombatTick"/>.
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
                    return TryPickHealLane(aiSide, out lane);

                case SpellEffect.LaneDamage:
                    return TryPickDamageLane(playerSide, out lane);

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

        private static bool TryPickHealLane(PlayerBattleState aiSide, out Lane lane)
        {
            lane = Lane.Front;
            int bestMissing = 0;

            foreach (Lane candidate in AllLanes)
            {
                int missing = MissingHealthInLane(aiSide, candidate);
                if (missing <= 0) continue;
                if (missing > bestMissing)
                {
                    bestMissing = missing;
                    lane = candidate;
                }
            }

            return bestMissing > 0;
        }

        private static bool TryPickDamageLane(PlayerBattleState playerSide, out Lane lane)
        {
            lane = Lane.Front;
            int bestCount = 0;

            foreach (Lane candidate in AllLanes)
            {
                int living = LivingCount(playerSide, candidate);
                if (living > bestCount)
                {
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

using System.Collections.Generic;
using System.Linq;

namespace MyriadOfDragons.Battle
{
    /// <summary>One spell's aggregate cast record across a match.</summary>
    public readonly struct SpellCastTally
    {
        public readonly string SpellName;
        public readonly int TimesCast;
        public readonly int TotalAvatarDamage;

        public SpellCastTally(string spellName, int timesCast, int totalAvatarDamage)
        {
            SpellName = spellName;
            TimesCast = timesCast;
            TotalAvatarDamage = totalAvatarDamage;
        }
    }

    /// <summary>
    /// Post-battle summary/analysis (damage dealt, spells cast, key moments), computed entirely
    /// from data BattleController already tracks - CombatLedger and SpellCastLog. No new tracking
    /// infrastructure: this is a pure presentation-layer consumer of existing, already-tested data,
    /// exactly like CombatFeedFormatter is for the live mid-match feed. "Key moments" reuses
    /// CombatFeedFormatter's own per-tick/per-cast line text (one source of truth for how a clash
    /// is described in plain language) rather than authoring a second, potentially-drifting phrasing.
    /// </summary>
    public readonly struct MatchAnalysis
    {
        public readonly int TicksResolved;
        public readonly int TotalDamageToPlayerAvatar;
        public readonly int TotalDamageToEnemyAvatar;
        public readonly int TotalSiegeDamageToPlayerAvatar;
        public readonly int TotalSiegeDamageToEnemyAvatar;
        public readonly IReadOnlyList<SpellCastTally> PlayerSpellTally;
        public readonly IReadOnlyList<SpellCastTally> EnemySpellTally;

        /// <summary>Plain-language lines for the moments worth calling out in a post-battle
        /// summary: the single biggest-damage tick, every siege trigger, and every spell cast that
        /// landed direct Avatar damage - in chronological order. A strict subset of what
        /// CombatFeedFormatter.DescribeTick/DescribeSpellCast can produce for the full ledger, not
        /// a second invented list.</summary>
        public readonly IReadOnlyList<string> KeyMoments;

        public MatchAnalysis(int ticksResolved, int totalDamageToPlayerAvatar, int totalDamageToEnemyAvatar,
            int totalSiegeDamageToPlayerAvatar, int totalSiegeDamageToEnemyAvatar,
            IReadOnlyList<SpellCastTally> playerSpellTally, IReadOnlyList<SpellCastTally> enemySpellTally,
            IReadOnlyList<string> keyMoments)
        {
            TicksResolved = ticksResolved;
            TotalDamageToPlayerAvatar = totalDamageToPlayerAvatar;
            TotalDamageToEnemyAvatar = totalDamageToEnemyAvatar;
            TotalSiegeDamageToPlayerAvatar = totalSiegeDamageToPlayerAvatar;
            TotalSiegeDamageToEnemyAvatar = totalSiegeDamageToEnemyAvatar;
            PlayerSpellTally = playerSpellTally;
            EnemySpellTally = enemySpellTally;
            KeyMoments = keyMoments;
        }
    }

    public static class MatchAnalyzer
    {
        public static MatchAnalysis Analyze(IReadOnlyList<CombatTickRecord> ledger, IReadOnlyList<SpellCastRecord> spellCasts)
        {
            ledger ??= new List<CombatTickRecord>();
            spellCasts ??= new List<SpellCastRecord>();

            int totalPlayerDamage = ledger.Sum(t => t.DamageToPlayerAvatar);
            int totalEnemyDamage = ledger.Sum(t => t.DamageToEnemyAvatar);
            int totalPlayerSiege = ledger.Sum(t => t.SiegeDamageToPlayerAvatar);
            int totalEnemySiege = ledger.Sum(t => t.SiegeDamageToEnemyAvatar);

            List<SpellCastTally> playerTally = TallySpells(spellCasts.Where(c => c.CastByPlayer));
            List<SpellCastTally> enemyTally = TallySpells(spellCasts.Where(c => !c.CastByPlayer));

            var keyMoments = new List<string>();

            CombatTickRecord? biggestTick = null;
            int biggestTotal = 0;
            foreach (CombatTickRecord tick in ledger)
            {
                int combined = tick.DamageToPlayerAvatar + tick.DamageToEnemyAvatar;
                if (combined > biggestTotal)
                {
                    biggestTotal = combined;
                    biggestTick = tick;
                }
            }
            if (biggestTick.HasValue && biggestTotal > 0)
            {
                keyMoments.AddRange(CombatFeedFormatter.DescribeTick(biggestTick.Value)
                    .Where(line => !line.Contains("no lane broke through")));
            }

            foreach (CombatTickRecord tick in ledger)
            {
                if (tick.SiegeDamageToPlayerAvatar > 0 || tick.SiegeDamageToEnemyAvatar > 0)
                {
                    keyMoments.AddRange(CombatFeedFormatter.DescribeTick(tick)
                        .Where(line => line.Contains("siege")));
                }
            }

            foreach (SpellCastRecord cast in spellCasts.Where(c => c.AvatarDamageDealt > 0))
            {
                keyMoments.Add(CombatFeedFormatter.DescribeSpellCast(cast));
            }

            return new MatchAnalysis(
                ticksResolved: ledger.Count,
                totalDamageToPlayerAvatar: totalPlayerDamage,
                totalDamageToEnemyAvatar: totalEnemyDamage,
                totalSiegeDamageToPlayerAvatar: totalPlayerSiege,
                totalSiegeDamageToEnemyAvatar: totalEnemySiege,
                playerSpellTally: playerTally,
                enemySpellTally: enemyTally,
                keyMoments: keyMoments.Distinct().ToList());
        }

        private static List<SpellCastTally> TallySpells(IEnumerable<SpellCastRecord> casts)
        {
            return casts
                .GroupBy(c => c.SpellName)
                .Select(g => new SpellCastTally(g.Key, g.Count(), g.Sum(c => c.AvatarDamageDealt)))
                .OrderByDescending(t => t.TimesCast)
                .ToList();
        }
    }
}

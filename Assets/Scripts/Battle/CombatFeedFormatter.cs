using System.Collections.Generic;
using System.Linq;

namespace MyriadOfDragons.Battle
{
    /// <summary>
    /// Combat Tick Feed (2026-08-22) - turns the already-resolved, already-tested combat data
    /// (CombatTickRecord/LaneClashResult/SpellCastRecord) into short plain-language lines a player
    /// can actually read mid-fight ("what just happened"), per the owner's readability request.
    /// A plain static class with no MonoBehaviour/Update/coroutine dependency, so every line this
    /// produces is directly EditMode-testable against real resolved combat, and it invents no
    /// numbers of its own - every figure it prints already exists on the record it formats.
    /// </summary>
    public static class CombatFeedFormatter
    {
        /// <summary>One or more plain-language lines describing everything that happened in a
        /// single resolved tick: per-lane deaths and overflow, then siege, in that order. Never
        /// empty - a tick where nothing broke through still gets one line saying so, so the feed
        /// never looks like it silently missed a clash.</summary>
        public static List<string> DescribeTick(CombatTickRecord record)
        {
            var lines = new List<string>();

            // Same multiplier ResolveTurn already applied to reach DamageToPlayerAvatar/
            // DamageToEnemyAvatar (totalOverflow * multiplier) - reporting the raw pre-multiplier
            // OverflowToA/B here would understate what actually landed on the Avatar, so this
            // reuses the exact public formula rather than inventing a second one.
            int multiplier = LaneBattleResolver.AvatarDamageMultiplierForTick(record.TickNumber);

            foreach (LaneClashResult lane in record.LaneResults)
            {
                string laneName = lane.Lane.ToString();

                if (lane.DefeatedCardNamesA != null && lane.DefeatedCardNamesA.Count > 0)
                {
                    lines.Add($"Clash {record.TickNumber}: {laneName} lane - your {string.Join(", ", lane.DefeatedCardNamesA)} fell.");
                }
                if (lane.DefeatedCardNamesB != null && lane.DefeatedCardNamesB.Count > 0)
                {
                    lines.Add($"Clash {record.TickNumber}: {laneName} lane - enemy {string.Join(", ", lane.DefeatedCardNamesB)} fell.");
                }
                if (lane.OverflowToB > 0)
                {
                    lines.Add($"Clash {record.TickNumber}: {laneName} lane breaks through - {lane.OverflowToB * multiplier} dmg hits the enemy Avatar.");
                }
                if (lane.OverflowToA > 0)
                {
                    lines.Add($"Clash {record.TickNumber}: {laneName} lane breaks through - {lane.OverflowToA * multiplier} dmg hits your Avatar.");
                }
            }

            if (record.SiegeDamageToEnemyAvatar > 0)
            {
                lines.Add($"Clash {record.TickNumber}: the enemy board is empty - siege deals {record.SiegeDamageToEnemyAvatar} to the enemy Avatar.");
            }
            if (record.SiegeDamageToPlayerAvatar > 0)
            {
                lines.Add($"Clash {record.TickNumber}: your board is empty - siege deals {record.SiegeDamageToPlayerAvatar} to your Avatar.");
            }

            if (lines.Count == 0)
            {
                lines.Add($"Clash {record.TickNumber}: no lane broke through - HP unchanged this clash.");
            }

            return lines;
        }

        /// <summary>One plain-language line for a successfully-cast spell. Only ever the player's
        /// own cast today - see SpellCastRecord's own comment.</summary>
        public static string DescribeSpellCast(SpellCastRecord cast)
        {
            return cast.AvatarDamageDealt > 0
                ? $"Clash {cast.TickNumber}: you cast {cast.SpellName} on {cast.TargetLane} - {cast.AvatarDamageDealt} dmg to the Avatar."
                : $"Clash {cast.TickNumber}: you cast {cast.SpellName} on {cast.TargetLane}.";
        }

        /// <summary>
        /// The feed the Battle activity rail actually shows: up to <paramref name="maxLines"/>
        /// lines, newest clash first, each tick's own lane/siege lines followed by that same
        /// tick's spell casts (if any) in the order they were cast. Pure function of the two logs
        /// passed in - callable from a plain EditMode test with no live BattleController needed,
        /// and also what GameBootstrap.RefreshActivityLog calls against the real ones.
        /// </summary>
        public static List<string> BuildFeedLines(IReadOnlyList<CombatTickRecord> ledger,
            IReadOnlyList<SpellCastRecord> spellCasts, int maxLines = 6)
        {
            var result = new List<string>();
            if (ledger == null) return result;

            for (int i = ledger.Count - 1; i >= 0 && result.Count < maxLines; i--)
            {
                CombatTickRecord record = ledger[i];
                var tickLines = new List<string>();
                tickLines.AddRange(DescribeTick(record));
                if (spellCasts != null)
                {
                    tickLines.AddRange(spellCasts.Where(c => c.TickNumber == record.TickNumber).Select(DescribeSpellCast));
                }

                foreach (string line in tickLines)
                {
                    if (result.Count >= maxLines) break;
                    result.Add(line);
                }
            }

            return result;
        }
    }
}

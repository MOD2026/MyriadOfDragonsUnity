using System;
using MyriadOfDragons.Save;

namespace MyriadOfDragons.Frontier
{
    /// <summary>Locked beta numbers, copied from the published, validated server catalog
    /// (CloudCode/CC10Frontier/CC10Rows.cs, CC10RowSet - CC10RowSet.Validate refuses to start on a
    /// catalog that drifts from these). Preview only: the server remains the authority and
    /// enforces every one of these independently.</summary>
    public static class Cc10Rules
    {
        public const int TavernMaxLevel = 10;
        public const int TavernTotalGold = 5400;
        public const int TavernTotalMaterials = 5400;
        public const int TavernTotalMinutes = 26 * 60 + 45;

        public const int MissionStaminaCost = 10;
        public const int MissionGoldReward = 300;
        public const int MissionMaterialReward = 200;

        public const int GoldCapPerUtcDay = 900;
        /// <summary>Base 300 + the locked +10% guild bonus; the bonus can never raise the cap.</summary>
        public const int MaxExpeditionGoldPerReport = 330;
        public const int CampaignMaterialsSourceTotal = 2300;

        public struct MissionRule
        {
            public string Type;
            public int Minutes;
            public int DailyLimit;
            public int RefreshHours;
            /// <summary>Tavern level that must be COMPLETED before this mission unlocks.</summary>
            public int UnlockTavernLevel;
            public string NodeId;
        }

        public static readonly MissionRule[] MissionRules =
        {
            new MissionRule { Type = Cc10MissionType.NpcPatrol, Minutes = 30, DailyLimit = 3, RefreshHours = 8, UnlockTavernLevel = 2, NodeId = "patrol_road" },
            new MissionRule { Type = Cc10MissionType.RelicRescue, Minutes = 60, DailyLimit = 2, RefreshHours = 12, UnlockTavernLevel = 4, NodeId = "ruined_shrine" },
            new MissionRule { Type = Cc10MissionType.VeinConvoy, Minutes = 120, DailyLimit = 1, RefreshHours = 24, UnlockTavernLevel = 6, NodeId = "titan_vein" },
        };

        public static bool TryGetMissionRule(string type, out MissionRule rule)
        {
            foreach (MissionRule r in MissionRules)
            {
                if (r.Type == type) { rule = r; return true; }
            }
            rule = default;
            return false;
        }

        public static int CardTrainingCost(int level) => CollectionTrainingRules.XpCostForNextLevel(level);
        public static int CardLevelCap => CollectionSchemaRules.MaxCardLevel;
    }

    /// <summary>Legal display transitions, copied from the enum sets in the published contract
    /// (MissionStatus, CargoStatus). Used only to flag an impossible jump for a reload - the client
    /// never enforces these; the server is the sole authority.</summary>
    public static class Cc10StateMachine
    {
        public static bool IsMissionTerminal(string status) =>
            status == Cc10MissionStatus.Claimed || status == Cc10MissionStatus.Failed
            || status == Cc10MissionStatus.Abandoned || status == Cc10MissionStatus.Expired;

        public static bool IsMissionTransitionLegal(string from, string to)
        {
            if (from == to) return true; // idempotent replay
            if (IsMissionTerminal(from)) return false;
            switch (from)
            {
                case Cc10MissionStatus.Active:
                    return to == Cc10MissionStatus.Completed || to == Cc10MissionStatus.Failed
                        || to == Cc10MissionStatus.Abandoned || to == Cc10MissionStatus.Expired;
                case Cc10MissionStatus.Completed:
                    return to == Cc10MissionStatus.Claimed || to == Cc10MissionStatus.Expired;
                default:
                    return false;
            }
        }

        public static bool IsCargoTerminal(string status) =>
            status == Cc10CargoStatus.Claimed || status == Cc10CargoStatus.Intercepted
            || status == Cc10CargoStatus.Abandoned || status == Cc10CargoStatus.Expired
            || status == Cc10CargoStatus.Failed;

        public static bool IsCargoTransitionLegal(string from, string to)
        {
            if (from == to) return true;
            if (IsCargoTerminal(from)) return false;
            switch (from)
            {
                case Cc10CargoStatus.Accepted:
                    return to == Cc10CargoStatus.Scouting || to == Cc10CargoStatus.Abandoned || to == Cc10CargoStatus.Expired;
                case Cc10CargoStatus.Scouting:
                    return to == Cc10CargoStatus.Active || to == Cc10CargoStatus.Abandoned || to == Cc10CargoStatus.Expired;
                case Cc10CargoStatus.Active:
                    return to == Cc10CargoStatus.Delivered || to == Cc10CargoStatus.Intercepted
                        || to == Cc10CargoStatus.Abandoned || to == Cc10CargoStatus.Expired || to == Cc10CargoStatus.Failed;
                case Cc10CargoStatus.Delivered:
                    return to == Cc10CargoStatus.Claimed || to == Cc10CargoStatus.Expired;
                default:
                    return false;
            }
        }

        public static bool IsMissionClaimable(string status) => status == Cc10MissionStatus.Completed;
        public static bool IsCargoClaimable(string status) => status == Cc10CargoStatus.Delivered;
    }

    /// <summary>Display-only server clock: one server UTC sample plus a monotonic elapsed counter,
    /// so device clock rollback/advance cannot move a timer (authority rule 3: GetServerUtc() is
    /// the only clock any rule uses; this is a display projection of it, nothing more).</summary>
    public sealed class Cc10ServerClock
    {
        private readonly Func<long> _monotonicMs;
        private long _sampleUtcMs;
        private long _sampleMonotonicMs;
        private bool _hasSample;

        public Cc10ServerClock(Func<long> monotonicMs = null)
        {
            _monotonicMs = monotonicMs ?? (() => System.Diagnostics.Stopwatch.GetTimestamp()
                * 1000L / System.Diagnostics.Stopwatch.Frequency);
        }

        public bool HasSample => _hasSample;

        public void Sample(long serverUtcMs)
        {
            if (serverUtcMs <= 0) return;
            _sampleUtcMs = serverUtcMs;
            _sampleMonotonicMs = _monotonicMs();
            _hasSample = true;
        }

        public long DisplayNowUtcMs => _hasSample ? _sampleUtcMs + (_monotonicMs() - _sampleMonotonicMs) : 0;

        public long RemainingMs(long targetUtcMs)
        {
            if (!_hasSample || targetUtcMs <= 0) return 0;
            long left = targetUtcMs - DisplayNowUtcMs;
            return left > 0 ? left : 0;
        }

        public static string FormatRemaining(long ms)
        {
            if (ms <= 0) return "Ready";
            long totalMinutes = (ms + 59999) / 60000;
            long h = totalMinutes / 60, m = totalMinutes % 60;
            return h > 0 ? h + "h " + m + "m" : m + "m";
        }
    }
}

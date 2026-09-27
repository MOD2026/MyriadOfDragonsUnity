using System;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Save;

namespace MyriadOfDragons.Frontier
{
    /// <summary>Locked beta numbers from docs/CC10_BETA_AUTHORITY_2026-09-27.md, kept only so the
    /// client can PREVIEW what the server will enforce. The server stays the authority.</summary>
    public static class Cc10Rules
    {
        public const int TavernMaxLevel = 10;
        public const int TavernTotalGold = 5400;
        public const int TavernTotalMaterials = 5400;
        public const int TavernTotalMinutes = 26 * 60 + 45;

        public const int MissionStaminaCost = 10;
        public const int MissionGoldReward = 300;
        public const int MissionMaterialReward = 200;

        public const string KindNpcPatrol = "NpcPatrol";
        public const string KindRelicRescue = "RelicRescue";
        public const string KindVeinConvoy = "VeinConvoy";

        public struct MissionRule
        {
            public string Kind;
            public int Minutes;
            public int DailyLimit;
            public int RefreshHours;
        }

        public static readonly MissionRule[] MissionRules =
        {
            new MissionRule { Kind = KindNpcPatrol, Minutes = 30, DailyLimit = 3, RefreshHours = 8 },
            new MissionRule { Kind = KindRelicRescue, Minutes = 60, DailyLimit = 2, RefreshHours = 12 },
            new MissionRule { Kind = KindVeinConvoy, Minutes = 120, DailyLimit = 1, RefreshHours = 24 },
        };

        /// <summary>The shared 900 Gold UTC-day cap (Empire Expedition + CC10).</summary>
        public static int SharedDailyGoldCap => EmpireExpeditionOpenValues.DailyExpeditionGoldCap ?? 900;

        public static bool TryGetMissionRule(string kind, out MissionRule rule)
        {
            foreach (MissionRule r in MissionRules)
            {
                if (r.Kind == kind) { rule = r; return true; }
            }
            rule = default;
            return false;
        }

        /// <summary>Preview only: true when claiming <paramref name="gold"/> would cross the shared
        /// cap. Per the authority record the WHOLE claim is rejected - never a partial payout.</summary>
        public static bool ClaimWouldCrossGoldCap(int usedToday, int gold) =>
            gold > 0 && usedToday + gold > SharedDailyGoldCap;

        public static int CardTrainingCost(int level) => CollectionTrainingRules.XpCostForNextLevel(level);
        public static int CardLevelCap => CollectionSchemaRules.MaxCardLevel;
    }

    /// <summary>Legal display transitions. The server owns transitions; the client uses this table
    /// to detect an impossible state jump and reload authoritative state instead of trusting it.</summary>
    public static class Cc10StateMachine
    {
        public const string Accepted = "Accepted";
        public const string Scouting = "Scouting";
        public const string Active = "Active";
        public const string Completed = "Completed";
        public const string Failed = "Failed";
        public const string Abandoned = "Abandoned";
        public const string Claimed = "Claimed";
        public const string Delivered = "Delivered";
        public const string Intercepted = "Intercepted";
        public const string Expired = "Expired";

        public static bool IsTerminal(string state) =>
            state == Claimed || state == Failed || state == Abandoned
            || state == Intercepted || state == Expired;

        public static bool IsMissionTransitionLegal(string from, string to)
        {
            if (from == to) return true; // idempotent replay
            switch (from)
            {
                case Accepted: return to == Scouting || to == Abandoned || to == Expired;
                case Scouting: return to == Active || to == Abandoned || to == Expired;
                case Active: return to == Completed || to == Failed || to == Abandoned || to == Expired;
                case Completed: return to == Claimed || to == Expired;
                default: return false;
            }
        }

        public static bool IsCargoTransitionLegal(string from, string to)
        {
            if (from == to) return true;
            switch (from)
            {
                case Accepted: return to == Scouting || to == Abandoned || to == Expired;
                case Scouting: return to == Active || to == Abandoned || to == Expired;
                case Active: return to == Delivered || to == Intercepted || to == Abandoned
                                    || to == Expired || to == Failed;
                case Delivered: return to == Claimed || to == Expired;
                default: return false;
            }
        }

        public static bool IsClaimable(string state) => state == Completed || state == Delivered;
    }

    /// <summary>Display-only server clock: one server UTC sample plus a monotonic elapsed counter,
    /// so device clock rollback/advance cannot move a timer. Never used to authorise anything.</summary>
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

        /// <summary>Milliseconds until <paramref name="targetUtcMs"/>; 0 when reached or unknown.</summary>
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

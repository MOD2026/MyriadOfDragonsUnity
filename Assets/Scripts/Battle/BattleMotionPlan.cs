using System.Collections.Generic;

namespace MyriadOfDragons.Battle
{
    /// <summary>The Battle presentation beats that get in-game motion.</summary>
    public enum BattleMotionKind
    {
        PlacementLand,
        StartBattle,
        ClashPulse,
        AttackLunge,
        HitFeedback,
        ResultReveal,
    }

    /// <summary>One beat's timing and scale envelope. Duration is 0 (and the envelope flat) when the
    /// beat is static - Reduced Motion removes the motion, never the beat's outcome.</summary>
    public readonly struct BattleMotionStep
    {
        public readonly float Duration;
        public readonly float StartScale;
        public readonly float PeakScale;
        public bool IsStatic => Duration <= 0f;

        public BattleMotionStep(float duration, float startScale, float peakScale)
        {
            Duration = duration;
            StartScale = startScale;
            PeakScale = peakScale;
        }
    }

    /// <summary>
    /// Pure, presentation-only motion rules for Battle (no UnityEngine dependency, so EditMode can
    /// test them - EditMode cannot run the coroutines that apply them). Nothing here reads or
    /// changes combat state: the clash reaction helpers only READ an already-resolved
    /// <see cref="LaneClashResult"/> to decide which side shows a hit.
    ///
    /// Envelope: scale starts at StartScale, rises/falls to PeakScale at 40% of the duration, then
    /// settles to exactly 1 at the end - so any beat that is interrupted and then completed (or
    /// reset) returns its target to rest size.
    /// </summary>
    public static class BattleMotionPlan
    {
        /// <summary>Fraction of a beat's duration at which the scale reaches its peak.</summary>
        public const float PeakAt = 0.4f;

        /// <summary>Hit-tint strength for a side that lost units or took Avatar overflow.</summary>
        public const float HeavyHitTint = 0.45f;

        /// <summary>Hit-tint strength for a side that traded blows but lost nothing.</summary>
        public const float LightHitTint = 0.18f;

        /// <summary>How long a reduced-motion hit tint is held as a still marker (seconds).</summary>
        public const float StaticHitHoldSeconds = 0.35f;

        public static BattleMotionStep For(BattleMotionKind kind, bool reduceMotion)
        {
            if (reduceMotion) return new BattleMotionStep(0f, 1f, 1f);

            switch (kind)
            {
                case BattleMotionKind.PlacementLand: return new BattleMotionStep(0.22f, 1.00f, 1.12f);
                case BattleMotionKind.StartBattle: return new BattleMotionStep(0.35f, 0.85f, 1.08f);
                case BattleMotionKind.ClashPulse: return new BattleMotionStep(0.18f, 1.00f, 1.04f);
                case BattleMotionKind.AttackLunge: return new BattleMotionStep(0.20f, 1.00f, 1.10f);
                case BattleMotionKind.HitFeedback: return new BattleMotionStep(0.16f, 1.00f, 0.94f);
                case BattleMotionKind.ResultReveal: return new BattleMotionStep(0.30f, 0.94f, 1.02f);
                default: return new BattleMotionStep(0f, 1f, 1f);
            }
        }

        /// <summary>Scale at normalized time t (0..1). Always exactly 1 for a static step and
        /// exactly 1 at t >= 1.</summary>
        public static float EvaluateScale(BattleMotionStep step, float t)
        {
            if (step.IsStatic || t >= 1f) return 1f;
            if (t <= 0f) return step.StartScale;
            if (t < PeakAt) return Lerp(step.StartScale, step.PeakScale, t / PeakAt);
            return Lerp(step.PeakScale, 1f, (t - PeakAt) / (1f - PeakAt));
        }

        /// <summary>Hit-tint alpha at normalized time t. Full motion fades out to 0; Reduced Motion
        /// holds the same strength as a still marker (no fade) for the whole hold.</summary>
        public static float HitTintAlpha(float strength, float t, bool reduceMotion)
        {
            if (t >= 1f) return 0f;
            if (reduceMotion) return strength;
            return strength * (1f - (t < 0f ? 0f : t));
        }

        /// <summary>Which reaction one side of a resolved lane clash shows: a recoil when it lost
        /// units or its Avatar took overflow, otherwise an attack lunge (it traded blows). Reads
        /// the already-resolved result only.</summary>
        public static BattleMotionKind ReactionFor(LaneClashResult lane, bool sideA) =>
            WasHeavilyHit(lane, sideA) ? BattleMotionKind.HitFeedback : BattleMotionKind.AttackLunge;

        public static float HitTintStrengthFor(LaneClashResult lane, bool sideA) =>
            WasHeavilyHit(lane, sideA) ? HeavyHitTint : LightHitTint;

        private static bool WasHeavilyHit(LaneClashResult lane, bool sideA)
        {
            IReadOnlyList<string> defeated = sideA ? lane.DefeatedCardNamesA : lane.DefeatedCardNamesB;
            int overflow = sideA ? lane.OverflowToA : lane.OverflowToB;
            bool cleared = sideA ? lane.SideACleared : lane.SideBCleared;
            return (defeated != null && defeated.Count > 0) || overflow > 0 || cleared;
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * (t < 0f ? 0f : t > 1f ? 1f : t);
    }
}

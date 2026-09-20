namespace MyriadOfDragons.Combat
{
    /// <summary>The runtime gameplay-presentation beats the Battle screen animates. Presentation
    /// only - none of these decide or change a combat rule, damage, reward, Save or navigation.</summary>
    public enum BattleBeat
    {
        FormationEnter,
        BattleStart,
        CardArrival,
        Hit,
        CriticalHit,
        Defeat,
        SpellImpact,
        ResultReveal,
        Retry,
        Replay,
        ReturnToEmpire,
    }

    /// <summary>How one beat plays. <see cref="Animated"/> false is the reduced-motion contract:
    /// no fade, scale, drift, flash or rotation - the beat either lands immediately
    /// (<see cref="StaticHoldMs"/> == 0, a pure transition) or shows a static marker for
    /// <see cref="StaticHoldMs"/> and then removes it (informational feedback).</summary>
    public readonly struct BattleBeatTiming
    {
        public readonly int AnimatedMs;
        public readonly int StaticHoldMs;
        public readonly bool Animated;
        public readonly bool AllowsFlash;

        public BattleBeatTiming(int animatedMs, int staticHoldMs, bool animated, bool allowsFlash)
        {
            AnimatedMs = animatedMs;
            StaticHoldMs = staticHoldMs;
            Animated = animated;
            AllowsFlash = allowsFlash;
        }
    }

    /// <summary>What a single lane's clash looked like, in presentation terms only.</summary>
    public enum ClashCue
    {
        None,
        Hit,
        CriticalHit,
        Defeat,
    }

    /// <summary>
    /// Pure timing/classification policy for Battle's runtime presentation. Coroutines cannot run
    /// in EditMode (CLAUDE.md non-negotiable #6), so every decision GameBootstrap's presentation
    /// makes lives here as a plain testable function and GameBootstrap only supplies timing.
    /// Sibling of <see cref="CombatPresentationPolicy"/>, which it does not modify.
    /// </summary>
    public static class BattleBeatPolicy
    {
        /// <summary>Ceiling on any animated beat, so no transition can read as sluggish.</summary>
        public const int MaxAnimatedMs = 600;

        /// <summary>Static marker hold for informational feedback under reduced motion - long
        /// enough to read, short enough that it never lingers over the next tick (ticks are 2.2 s).</summary>
        public const int InformationalHoldMs = 700;

        public static BattleBeatTiming Resolve(BattleBeat beat, bool reducedMotion)
        {
            switch (beat)
            {
                case BattleBeat.Hit:
                    return Informational(280, 450, reducedMotion, flash: false);
                case BattleBeat.CriticalHit:
                    return Informational(420, InformationalHoldMs, reducedMotion, flash: false);
                case BattleBeat.Defeat:
                    return Informational(450, InformationalHoldMs, reducedMotion, flash: false);
                case BattleBeat.SpellImpact:
                    return Informational(CombatPresentationPolicy.SpellImpactMs, InformationalHoldMs, reducedMotion, flash: true);
                case BattleBeat.CardArrival:
                    return Transition(CombatPresentationPolicy.CardPlayMs, reducedMotion, flash: false);
                case BattleBeat.FormationEnter:
                    return Transition(220, reducedMotion, flash: false);
                case BattleBeat.BattleStart:
                    return Transition(260, reducedMotion, flash: true);
                case BattleBeat.ResultReveal:
                    return Transition(220, reducedMotion, flash: false);
                case BattleBeat.Retry:
                case BattleBeat.Replay:
                    return Transition(180, reducedMotion, flash: false);
                case BattleBeat.ReturnToEmpire:
                    return Transition(140, reducedMotion, flash: false);
                default:
                    return new BattleBeatTiming(0, 0, false, false);
            }
        }

        /// <summary>Pure state change (a screen/board arriving): animated normally, immediate and
        /// static under reduced motion - never a shorter version of the same animation.</summary>
        private static BattleBeatTiming Transition(int animatedMs, bool reduced, bool flash) =>
            reduced
                ? new BattleBeatTiming(0, 0, false, false)
                : new BattleBeatTiming(Clamp(animatedMs), 0, true, flash);

        /// <summary>Feedback that tells the player something happened (a hit, a defeat, which
        /// spell landed): it must stay visible under reduced motion, so it becomes a static hold
        /// instead of vanishing.</summary>
        private static BattleBeatTiming Informational(int animatedMs, int holdMs, bool reduced, bool flash) =>
            reduced
                ? new BattleBeatTiming(0, holdMs, false, false)
                : new BattleBeatTiming(Clamp(animatedMs), 0, true, flash);

        private static int Clamp(int ms) => ms < 0 ? 0 : ms > MaxAnimatedMs ? MaxAnimatedMs : ms;

        /// <summary>Classifies one lane's resolved clash. Defeat outranks a critical hit, which
        /// outranks a plain hit; an uncontested lane has no cue at all.</summary>
        public static ClashCue ClassifyClash(bool contested, int defeatedCards, bool laneCleared, int overflowDamage)
        {
            if (!contested) return ClashCue.None;
            if (defeatedCards > 0) return ClashCue.Defeat;
            if (laneCleared || overflowDamage > 0) return ClashCue.CriticalHit;
            return ClashCue.Hit;
        }

        public static BattleBeat BeatFor(ClashCue cue) => cue switch
        {
            ClashCue.Defeat => BattleBeat.Defeat,
            ClashCue.CriticalHit => BattleBeat.CriticalHit,
            _ => BattleBeat.Hit,
        };
    }
}

namespace MyriadOfDragons.Battle
{
    /// <summary>Which Chapter 1 tutorial cinematic is running - the opening (before Formation) or
    /// the victory close (before the tutorial victory result). Tutorial defeat and every normal
    /// match never construct a CinematicSequence at all.</summary>
    public enum CinematicKind
    {
        Opening,
        Victory,
    }

    /// <summary>
    /// Plain, Unity-independent timer/state machine for one cinematic run - deliberately holds no
    /// UnityEngine reference so it is fully testable without Play Mode (CLAUDE.md: "Real logic in
    /// plain testable methods; MonoBehaviours supply only timing"). GameBootstrap drives this with
    /// real Time.deltaTime via a coroutine in Play Mode; tests drive it directly with Advance()/
    /// Skip() calls, no coroutine ticking required.
    ///
    /// Exact semantics from Myriad_of_Dragons_Chapter_1_Cinematic_Unity_Handoff.md's "Exact skip
    /// semantics": "First accepted Skip request wins. Later Skip inputs are ignored until the
    /// destination is ready." - Skip and natural completion both simply set IsComplete; whichever
    /// happens first is the one that matters, and both leave the same IsComplete/IsSkipped state
    /// for the caller to inspect.
    /// </summary>
    public class CinematicSequence
    {
        public CinematicKind Kind { get; }
        public float DurationSeconds { get; }
        public float ElapsedSeconds { get; private set; }

        /// <summary>True only if Skip (not natural completion) is what finished this run - exposed
        /// for tests/telemetry; both paths reach IsComplete identically otherwise, per the
        /// handoff's "Natural completion and Skip use the same exit operation and destination."</summary>
        public bool IsSkipped { get; private set; }

        public bool IsComplete { get; private set; }

        public CinematicSequence(CinematicKind kind, float durationSeconds)
        {
            Kind = kind;
            DurationSeconds = durationSeconds;
        }

        /// <summary>Advances the timer by a real elapsed-time delta. A no-op once already
        /// complete, so a stray late call (e.g. one more coroutine frame after Skip already fired)
        /// can never un-skip or re-extend a finished run.</summary>
        public void Advance(float deltaSeconds)
        {
            if (IsComplete) return;
            ElapsedSeconds += deltaSeconds;
            if (ElapsedSeconds >= DurationSeconds) IsComplete = true;
        }

        /// <summary>First accepted Skip wins; every call after the first (or after natural
        /// completion) is a no-op - see this class's own doc comment.</summary>
        public void Skip()
        {
            if (IsComplete) return;
            IsSkipped = true;
            IsComplete = true;
        }
    }
}

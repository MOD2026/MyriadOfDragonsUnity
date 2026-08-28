namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Central gate for whether <em>decorative</em> (non-informational) motion should play.
    ///
    /// It is a plain static class - no MonoBehaviour, no UnityEngine dependency - for two reasons:
    /// 1. Accessibility. A single <see cref="ReduceMotion"/> switch lets a reduced-motion
    ///    preference suppress cosmetic animation without editing every animation hook. (There is
    ///    no settings screen yet, and that screen is Metagame-owned, so nothing sets this from a
    ///    UI today; it defaults to off so behaviour is unchanged until something opts in.)
    /// 2. Testability. The decision is a pure method, so the EditMode suite can assert it even
    ///    though it cannot run the coroutines the animations themselves live in.
    ///
    /// This governs only decorative polish (e.g. the idle hand shimmer). It is deliberately NOT a
    /// switch for gameplay-critical feedback such as damage numbers.
    /// </summary>
    public static class MotionPolicy
    {
        /// <summary>
        /// When true, decorative motion is suppressed. Defaults to false, so nothing changes
        /// until a caller explicitly opts in.
        /// </summary>
        public static bool ReduceMotion { get; set; } = false;

        /// <summary>
        /// Whether a decorative animation should play right now.
        ///
        /// <paramref name="isPlaying"/> is passed in rather than read from Application.isPlaying so
        /// this stays a pure, testable function. Coroutines only run in Play mode (the EditMode
        /// suite drives Initialize()/RefreshAll() with isPlaying == false), so decorative motion is
        /// always off outside Play mode, regardless of the <see cref="ReduceMotion"/> setting.
        /// </summary>
        public static bool ShouldPlayDecorativeMotion(bool isPlaying) => isPlaying && !ReduceMotion;
    }
}

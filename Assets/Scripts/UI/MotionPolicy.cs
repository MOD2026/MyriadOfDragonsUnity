namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Central gate for whether <em>decorative</em> (non-informational) motion should play.
    ///
    /// It is a plain static class - no MonoBehaviour, no UnityEngine dependency - for two reasons:
    /// 1. Accessibility. A single <see cref="ReduceMotion"/> switch lets a reduced-motion
    ///    preference suppress cosmetic animation without editing every animation hook. Settings
    ///    persists the preference on the profile and applies it via
    ///    <see cref="PlayerSettingsService.ApplyFromProfile"/> / SetReduceMotionEnabled.
    /// 2. Testability. The decision is a pure method, so the EditMode suite can assert it even
    ///    though it cannot run the coroutines the animations themselves live in.
    ///
    /// This governs only decorative polish (e.g. the idle hand shimmer, pending opacity pulse,
    /// non-essential screen fades). It is deliberately NOT a switch for essential state feedback
    /// such as press acknowledgement, disabled/locked look, or gameplay-critical damage numbers.
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

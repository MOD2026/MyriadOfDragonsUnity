using Unity.Services.Authentication;

namespace MyriadOfDragons.Metagame
{
    /// <summary>Real, safe accessor for the stable pseudonymous player id retention telemetry
    /// events need (register: "playerId (stable pseudonymous)"). Isolated in its own tiny class
    /// so RetentionTelemetryEvents itself stays free of any Unity Gaming Services dependency -
    /// that class's own explicit design goal (see its doc comment: "keeps this class free of any
    /// Unity Gaming Services dependency"). Never throws - a call site building a telemetry event
    /// at a real gameplay moment (a claim, a cap hit) must never risk failing that gameplay over
    /// an auth-state read.</summary>
    public static class RetentionTelemetryPlayerId
    {
        public static string CurrentOrEmpty()
        {
            try
            {
                return AuthenticationService.Instance.IsSignedIn ? AuthenticationService.Instance.PlayerId : string.Empty;
            }
            catch
            {
                // UnityServices never initialized yet, or any other UGS-side failure - a telemetry
                // id lookup must degrade to "unknown," never throw into the real call site.
                return string.Empty;
            }
        }
    }
}

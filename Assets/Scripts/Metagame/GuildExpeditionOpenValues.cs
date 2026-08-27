namespace MyriadOfDragons.Metagame
{
    public enum GuildExpeditionActionStatus
    {
        Applied,
        OpenValuesNotLocked,
    }

    public sealed class GuildExpeditionActionResult
    {
        public GuildExpeditionActionStatus Status;
        public string Message;
    }

    /// <summary>
    /// GUILD EXPEDITION shell — unlike Guild Hall / Mail (pure stubs), this presenter is already
    /// wired to a real, deployed CloudCode module (GuildExpeditionGateway.cs,
    /// nonprod-validation). The objective/milestone catalog stays OPEN (it mirrors the real
    /// deployed scaffold, not invented numbers) but production ACTIONS — Consume/Submit/Claim —
    /// refuse until backend eligibility is locked, so a real player never sees a raw backend
    /// failure ("Consume failed: unknown", "Consume: null response.") from a screen that was
    /// never meant to be live yet. Same split as GuildHallEntryOpenValues.
    /// </summary>
    public static class GuildExpeditionOpenValues
    {
        /// <summary>Short, player-facing status copy for the shell's StatusLine. Deliberately
        /// separate from <see cref="StatusNote"/> for the same reason as
        /// GuildHallEntryOpenValues.PlayerStatus - that string is a developer/transaction
        /// diagnostic, not something a player should ever read.</summary>
        public static string PlayerStatus => "Guild Expedition isn't live yet.";

        public static string StatusNote =>
            "Guild Expedition is wired to the real CloudCode module, but production actions " +
            "(Consume/Submit/Claim) stay OPEN until backend eligibility is locked - no player-" +
            "facing calls to a not-yet-validated backend.";

        public static readonly bool? ActionsEnabled = null;
        public static bool AreActionsConfigured => ActionsEnabled.HasValue;

        public static GuildExpeditionActionResult TryAction() => new GuildExpeditionActionResult
        {
            Status = GuildExpeditionActionStatus.OpenValuesNotLocked,
            Message = StatusNote,
        };
    }
}

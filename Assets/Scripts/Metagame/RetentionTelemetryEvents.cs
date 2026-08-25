using System;
using UnityEngine;

namespace MyriadOfDragons.Metagame
{
    /// <summary>Real event-emission helper (CLAUDE.md non-negotiable #6: real logic in plain
    /// testable methods) - builds a fully-populated RetentionTelemetryEvent for each of the 4
    /// LOCKED minimum event types (register: "Retention telemetry: Unity Analytics as the sink"
    /// §"Telemetry events to send"). Plain static class, zero MonoBehaviour, fully EditMode
    /// testable - callers (gameplay/UI code, later) own deciding WHEN to call these and what to
    /// do with the result (enqueue via RetentionTelemetryOutbox); this class only builds the
    /// event data correctly.
    ///
    /// playerId is a required parameter, not read from AuthenticationService internally - keeps
    /// this class free of any Unity Gaming Services dependency (that belongs to the gateway),
    /// and keeps it trivially testable with a fake id.</summary>
    public static class RetentionTelemetryEvents
    {
        public const int SchemaVersion = 1;

        public const string EventTypeModeRunCompleted = "mode_run_completed";
        public const string EventTypeModeRewardClaimed = "mode_reward_claimed";
        public const string EventTypeFeatureEntry = "feature_entry";
        public const string EventTypeDailyCapReached = "daily_cap_reached";

        public static RetentionTelemetryEvent ModeRunCompleted(string playerId, string mode, string runId, string outcome) =>
            Build(EventTypeModeRunCompleted, playerId, mode, runId, outcome);

        public static RetentionTelemetryEvent ModeRewardClaimed(string playerId, string mode, string runId, string outcome) =>
            Build(EventTypeModeRewardClaimed, playerId, mode, runId, outcome);

        /// <summary>runId/outcome are null - a feature-entry event marks reaching a screen, not
        /// completing a run.</summary>
        public static RetentionTelemetryEvent FeatureEntry(string playerId, string mode) =>
            Build(EventTypeFeatureEntry, playerId, mode, runId: null, outcome: null);

        /// <summary>runId is null - a daily-cap event isn't tied to one specific run.</summary>
        public static RetentionTelemetryEvent DailyCapReached(string playerId, string mode, string outcome) =>
            Build(EventTypeDailyCapReached, playerId, mode, runId: null, outcome: outcome);

        private static RetentionTelemetryEvent Build(string eventType, string playerId, string mode, string runId, string outcome) =>
            new RetentionTelemetryEvent
            {
                eventId = Guid.NewGuid().ToString("N"),
                playerId = playerId,
                eventType = eventType,
                clientOccurredAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                schemaVersion = SchemaVersion,
                appBuild = Application.version,
                mode = mode,
                runId = runId,
                outcome = outcome,
            };
    }
}

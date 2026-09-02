using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;

namespace MyriadOfDragons.Metagame
{
    /// <summary>One retention-telemetry event, matching the LOCKED schema (register:
    /// "Retention telemetry architecture - LOCKED, do NOT touch PlayerProfile", "Retention
    /// telemetry: Unity Analytics as the sink" §"Telemetry events to send"). Deliberately does
    /// NOT carry serverReceivedAtUtc - that field is server-authoritative (the gateway/Cloud Code
    /// stamps it on receipt), never client-set, per the locked spec's own clock-tampering
    /// concern ("server receipt time is authoritative"). clientOccurredAtUtc is carried purely as
    /// an offline-delay diagnostic, never used for cohort ordering.</summary>
    [Serializable]
    public sealed class RetentionTelemetryEvent
    {
        public string eventId;
        public string playerId;
        public string eventType;
        public long clientOccurredAtUtc;
        public int schemaVersion;
        public string appBuild;
        public string mode;
        public string runId;
        public string outcome;
    }

    [Serializable]
    public sealed class RetentionTelemetryGatewayResult
    {
        public bool success;
        public string errorCode;
    }

    /// <summary>Real client gateway for the retention-telemetry Cloud Code module (register:
    /// "reuse the existing authenticated Cloud Code/gateway pattern... to validate, deduplicate,
    /// and forward events to an append-only analytics sink"). Mirrors IFriendsGateway/
    /// IBazaarGateway/IChatSocialGateway's own shape exactly: EnsureSignedInAsync before every
    /// call, {"request":{...}} wrapping the module's real parameter names. The gateway is the
    /// trust boundary - it validates/dedupes/stamps serverReceivedAtUtc and forwards to Unity
    /// Analytics server-side (register: "Unity Analytics as the sink, gateway stays the trust
    /// boundary"); this client never talks to Unity Analytics directly.</summary>
    public interface IRetentionTelemetryGateway
    {
        Task<RetentionTelemetryGatewayResult> SendEventAsync(RetentionTelemetryEvent evt, CancellationToken cancellationToken);
    }

    public sealed class UnityCloudCodeRetentionTelemetryGateway : IRetentionTelemetryGateway
    {
        private const string ModuleName = "Telemetry";

        /// <summary>RELEASE BLOCKER FIX (2026-09-03): no `Telemetry` Cloud Code module exists in
        /// CloudCode/ (only Bazaar, Chat, Friends, GuildExpedition, PermitWeekKey, SocialSafety do)
        /// - every real flush attempt hit a live 404 "Module could not be found", spamming rc11
        /// logs. RetentionTelemetryOutbox.FlushAsync already treats any gateway failure as
        /// "leave queued, retry next opportunistic flush" (never loses events, never blocks the
        /// caller - see its own doc comment), but it still attempted the network/auth round trip
        /// every single flush with no chance of success. Gating here - before any network or auth
        /// call - stops the guaranteed-failing call and its 404 log spam while the queue keeps
        /// accumulating locally exactly as before. Flip to true (and delete this gate) once an
        /// authorized Telemetry module is actually deployed; see
        /// docs/RELEASE_BLOCKER_TELEMETRY_404_2026-09-03.md.</summary>
        public static bool ModuleDeployed = false;

        public async Task<RetentionTelemetryGatewayResult> SendEventAsync(RetentionTelemetryEvent evt, CancellationToken cancellationToken)
        {
            if (evt == null || string.IsNullOrWhiteSpace(evt.eventId) || string.IsNullOrWhiteSpace(evt.eventType))
                return new RetentionTelemetryGatewayResult { errorCode = "INVALID_REQUEST" };

            if (!ModuleDeployed)
                return new RetentionTelemetryGatewayResult { success = false, errorCode = "MODULE_NOT_DEPLOYED" };

            await EnsureSignedInAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return await CloudCodeService.Instance.CallModuleEndpointAsync<RetentionTelemetryGatewayResult>(
                ModuleName, "SendEvent",
                new Dictionary<string, object> { { "request", ToRequestDictionary(evt) } }).ConfigureAwait(false);
        }

        private static Dictionary<string, object> ToRequestDictionary(RetentionTelemetryEvent evt) => new Dictionary<string, object>
        {
            { "eventId", evt.eventId },
            { "playerId", evt.playerId },
            { "eventType", evt.eventType },
            { "clientOccurredAtUtc", evt.clientOccurredAtUtc },
            { "schemaVersion", evt.schemaVersion },
            { "appBuild", evt.appBuild },
            { "mode", evt.mode },
            { "runId", evt.runId },
            { "outcome", evt.outcome },
        };

        private static async Task EnsureSignedInAsync(CancellationToken cancellationToken)
        {
            await UnityServices.InitializeAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync().ConfigureAwait(false);
            }
        }
    }
}

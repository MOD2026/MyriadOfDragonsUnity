# Telemetry module (Cloud Code) — retention telemetry gateway

Implements the locked retention-telemetry architecture (docs/LOCKED_DECISIONS_REGISTER.md). Endpoint: `Telemetry.SendEvent` (only one).

- Client (`RetentionTelemetryOutbox`, bounded 500, ordered flush) -> this gateway (auth, validate, dedupe by eventId, server receipt timestamp) -> analytics sink (a copy only).
- No PlayerProfile / save-schema change. Only a per-player Cloud Save key `telemetry_seen` (bounded list of 500 eventIds, own AccessToken).
- No raw events stored; D1/D7/D14/D28 are query-time definitions in `RetentionCohorts` (never stored). No rewards/economy effect; response is `{success, errorCode}` only.
- Delivery is at-least-once (sink called before the id is recorded); records carry eventId for sink-side dedupe.
- Invalid events return success=true + `DROPPED_*` so a poison event cannot block the ordered client queue.

## External dependencies / open decisions (not invented)
- Unity Analytics REST sink endpoint/credentials/format: not in repo. Default `NotConfiguredTelemetrySink` fails closed (`SINK_NOT_CONFIGURED`), so events stay queued.
- D30: the lock defines D28 only; a D30 window needs a BS/owner decision.
- Client gate `UnityCloudCodeRetentionTelemetryGateway.ModuleDeployed` intentionally left false until deployed and verified in nonprod-validation.
- Not deployed anywhere.

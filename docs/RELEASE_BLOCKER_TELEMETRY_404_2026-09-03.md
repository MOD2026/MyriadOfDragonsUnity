# Release blocker: Telemetry 404 — 2026-09-03

## Symptom
rc11 runtime repeatedly logs `404 Not Found — Module could not be found — Telemetry`.
`RetentionTelemetryGateway.cs` (`UnityCloudCodeRetentionTelemetryGateway`) calls Cloud Code
module `Telemetry`, function `SendEvent`.

## Investigation
Checked every reachable location for an authorized `Telemetry` CloudCode module:
- `CloudCode/` in this checkout: `Bazaar, Chat, Friends, GuildExpedition, PermitWeekKey, SocialSafety` — no `Telemetry`.
- `MoD_worktree_be_canonical/CloudCode/`: `Bazaar, BazaarP2PSettlement, BazaarSettlement, Chat, ChatModeration, Friends, GuildExpedition, PermitWeekKey, SocialSafety` — no `Telemetry`.
- `git branch -a --list "*telemetry*"` and `git log --all --grep` for a telemetry-module commit — no matches.

**No authorized Telemetry module implementation exists anywhere reachable.** Deploying one
would mean inventing an unauthorized server-side implementation and analytics contract,
which is explicitly out of scope.

## Fix applied: gate the flush, not fabricate the module
`RetentionTelemetryOutbox.FlushAsync` already treats any gateway failure (explicit
unsuccessful result, thrown exception, or the flush deadline) as "leave the event queued,
retry on the next opportunistic flush" — it never loses events and never blocks the caller.
But it still attempted a real network + auth round trip on every flush, guaranteed to fail
against a module that isn't deployed, which is what produced the repeated live 404s.

`UnityCloudCodeRetentionTelemetryGateway.SendEventAsync` now checks a static
`ModuleDeployed` flag (defaults `false`) before doing anything else — before
`EnsureSignedInAsync`, before `CallModuleEndpointAsync` — and short-circuits to
`{ success = false, errorCode = "MODULE_NOT_DEPLOYED" }` when it is `false`. This:
- Stops the guaranteed-failing network call and its 404 log spam.
- Leaves `RetentionTelemetryOutbox.Enqueue` completely unaffected — events keep queuing
  locally exactly as before (bounded at 500, oldest dropped first).
- Changes no player-facing behavior: retention telemetry was already fire-and-forget and
  invisible to the player; this only changes what happens on the network, not any UI/reward/
  save path.
- Invents no analytics payload or schema — `RetentionTelemetryEvent`/`RetentionTelemetryEvents`
  are untouched.

## How to un-gate
Once an authorized `Telemetry` Cloud Code module is implemented and deployed to
`nonprod-validation` (and later production), flip
`UnityCloudCodeRetentionTelemetryGateway.ModuleDeployed` to `true` and delete this gate/doc.
No other change should be needed — the outbox and event-building code are already correct
and already tested against a fake gateway.

## Verification
- `dotnet build`/`dotnet test` not applicable (this is a client-side `Assets/Scripts` change,
  not a CloudCode module).
- EditMode run (`RetentionTelemetryTests`, `MetagameRetentionTelemetryEmitTests`,
  `BattlePassShellTests`, `DailyLoginQuestsShellTests`, `EmpireExpeditionShellTests`): 44 tests
  executed, 0 `error CS`, `telemetry_fix_results.xml` shows `Passed` on every result (54/54,
  including nested assertions). HEAD at run time: `d8a6b58f`.
- Friends and Battle contracts untouched — this change touches only
  `Assets/Scripts/Metagame/RetentionTelemetryGateway.cs`.
- No CloudCode deployment was made or needed for this fix (client-only change; nothing to
  redeploy in Bazaar/Chat/Friends/GuildExpedition/PermitWeekKey/SocialSafety).

## Remaining dependency
An authorized `Telemetry` Cloud Code module (schema, validation/dedup rules, and the Unity
Analytics sink wiring per the locked retention-telemetry register) still needs to be designed
and implemented by whoever owns that spec before `ModuleDeployed` can be flipped to `true`.
That implementation is out of scope for this fix — this task closes the release blocker
(the 404 spam) without inventing that module.

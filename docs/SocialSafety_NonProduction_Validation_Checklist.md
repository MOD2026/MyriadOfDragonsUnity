# SocialSafety Non-Production Validation Checklist

Status: **Pending manual confirmation**

This checklist is for a controlled, non-production validation only. Do not use production data,
production player accounts, or production environment settings. Do not record credentials, tokens,
raw account IDs, project IDs, or environment IDs.

## Preconditions

- [ ] The Unity project is linked to the intended Unity project.
- [ ] A separate non-production environment is selected and visibly confirmed.
- [ ] Cloud Code is enabled in that non-production environment.
- [ ] Cloud Save is enabled in that non-production environment.
- [ ] Two disposable anonymous test accounts are available. Label them **A** and **B**.
- [ ] No production environment ID or player data is being used.
- [ ] Local server build status: Release build succeeded with **zero warnings**.
- [ ] Local server test status: **43/43 passed**, 0 failed, 0 skipped.
- [ ] Unity client-test result: **Pending manual confirmation**.

## Remote Config

Create these settings in the selected non-production environment only:

| Key | Type | Default | Maximum accepted |
|---|---|---:|---:|
| `socialSafety.rateLimit.perMinute` | Integer | 10 | 100 |
| `socialSafety.rateLimit.perDay` | Integer | 100 | 1,000 |

Expected behavior:

- [ ] Missing values use the defaults: 10 per minute and 100 per day.
- [ ] Malformed values use the defaults.
- [ ] Zero values use the defaults.
- [ ] Negative values use the defaults.
- [ ] Values above the maximum use the defaults; they are not clamped.
- [ ] Exact maximum values are accepted: 100 per minute and 1,000 per day.
- [ ] Configuration failure does not disable rate limiting.

Observed configuration results:

| Key | Value entered | Observed effective value | Pass/fail | Sanitised evidence reference |
|---|---|---:|---|---|
| per-minute |  |  |  |  |
| per-day |  |  |  |  |

## Access-Policy Verification

Use only the selected non-production environment. Record error categories, not response bodies
containing credentials, tokens, raw IDs, or other secrets.

- [ ] A direct client write to a SocialSafety relationship key is denied.
- [ ] A direct client write to the SocialSafety rate-limit key is denied.
- [ ] A Cloud Code call using the authenticated player context succeeds.
- [ ] A player cannot read another player's relationship or rate-limit data.
- [ ] A player cannot write another player's relationship or rate-limit data.
- [ ] Record the exact observed error category for each denied operation.

## Endpoint Validation

Run each endpoint with account A and a non-self target. Record every result in the evidence table
below.

- [ ] Block.
- [ ] Duplicate block is successful and idempotent.
- [ ] Unblock.
- [ ] Repeated unblock is successful and reports no additional change.
- [ ] Mute.
- [ ] Duplicate mute is successful and idempotent.
- [ ] Unmute.
- [ ] Repeated unmute is successful and reports no additional change.
- [ ] Self-target request is rejected.
- [ ] Blank or invalid target request is rejected.
- [ ] Offline or service-unavailable behavior returns a safe failure category.
- [ ] Concurrent duplicate requests produce one canonical persisted record.
- [ ] Rate-limit exhaustion returns `RATE_LIMITED`.
- [ ] Minute-window reset occurs after the fixed 60-second window using server time.
- [ ] Day-window reset occurs after the fixed 24-hour window using server time.
- [ ] Relationship state persists after signing out and signing back in.
- [ ] Account B cannot observe or mutate account A's private relationship state.

## Security and Privacy

- [ ] Actor identity is derived from the authenticated session.
- [ ] The client does not send an actor ID.
- [ ] Raw SDK exceptions are not exposed to the client.
- [ ] No credentials, project IDs, environment IDs, tokens, or raw account IDs are recorded here.
- [ ] No Guild officer role bypasses the access checks.
- [ ] SubmitReport remains unimplemented.
- [ ] Guild operations remain unimplemented in this slice.
- [ ] Direct Messaging operations remain unimplemented in this slice.

## Evidence Template

Create one row for each check. Use account labels A and B only. Use a sanitised local reference such
as a test-run number or screenshot label; do not paste tokens, credentials, raw IDs, or full SDK
exception text.

| Date/time | Non-production environment name | Account label | Endpoint/check | Expected result | Observed result | Pass/fail | Sanitised evidence reference | Follow-up owner |
|---|---|---|---|---|---|---|---|---|
|  |  | A/B |  |  |  |  |  |  |
|  |  | A/B |  |  |  |  |  |  |
|  |  | A/B |  |  |  |  |  |  |
|  |  | A/B |  |  |  |  |  |  |
|  |  | A/B |  |  |  |  |  |  |

## Stop Conditions

Stop the validation immediately and record the sanitised error category if:

- [ ] Any production environment is selected.
- [ ] The access policy blocks a legitimate Cloud Code write.
- [ ] A direct client write succeeds.
- [ ] Cross-player access succeeds.
- [ ] Raw exception, token, credential, or other secret data is exposed.
- [ ] Relationship or rate-state persistence fails.
- [ ] Unexpected billing or deployment permission appears.
- [ ] Any source-code change appears necessary.

## Rollback and Cleanup

- [ ] Remove disposable test relationship and rate-state data.
- [ ] Undeploy or disable any test-only module or configuration, where applicable.
- [ ] Do not modify production data or configuration.
- [ ] Record what was cleaned up and the sanitised evidence reference.

Cleanup record:

| Date/time | Environment label | Data/configuration removed or disabled | Owner | Sanitised evidence reference |
|---|---|---|---|---|
|  |  |  |  |  |

Live non-production validation remains pending. This checklist does not claim deployment,
production verification, or Unity client test completion.

# Permit Week-Key Cloud Code module

This is the Phase 1 server module scaffold for the later Option A server-authoritative Ascension
Permit design. It is authored but not deployed. Design source: `docs/PERMIT_WEEK_KEY_SERVER_STUB_v1.md`.

**This is not a replacement for anything in this week's build.** The shipping client-side stopgap
(`ManualTrustedWeekKey`, 4 Permits/week, 8-Permit hoard cap) is untouched by this scaffold -
`PlayerProfile.cs` has no new field and no local Save-shape change. This module exists so the
server-authoritative design is proven out and ready to deploy once the Dashboard/environment
validation path is clear, the same role `CloudCode/SocialSafety/` already serves for block/mute.

## Design contract (from the stub doc)

- **Key format:** ISO-8601 week key `YYYY-Www` (example `2026-W34`), computed here in
  `IsoWeekKey.cs` directly against the ISO 8601 rule (Monday-start weeks, week 1 contains the
  year's first Thursday) rather than a framework-specific API, since this scaffold can't verify
  API surface availability against the actual Cloud Code runtime without deploying.
- **Authority:** the backend issues the week key from its own UTC clock
  (`IPermitWeekKeyClock.CurrentIsoWeekKey`). No request parameter can supply or override it - the
  client cannot construct an eligibility key of its own.
- **Claim identity:** `accountId + activityId + ISO week key` is the idempotent claim record. A
  second `ClaimWeeklyPermit` call in the same server-computed week returns the prior grant's
  outcome (`alreadyClaimed: true`, `granted: 0`) rather than granting again - a changed client
  clock cannot create a second claim, because the week key it's compared against is never
  client-supplied.
- **Client role:** `GetPermitStatus` (read-only) returns balance, the server's current week key,
  whether this week is already claimed, and the active weekly rate/hoard cap; `ClaimWeeklyPermit`
  submits a claim. Neither endpoint takes a client-provided week key or timestamp.

## Dependencies

- Server SDK: `Com.Unity.Services.CloudCode.Core` `0.0.5`.
- Server UGS API client: `Com.Unity.Services.CloudCode.Apis` `0.0.26`.

Same package versions as `CloudCode/SocialSafety/` - see that module's README for the source of
those version numbers.

## Remote Config

Weekly rate and hoard cap are read from Remote Config with a protective fallback if missing,
malformed, non-positive, or unavailable (same pattern as SocialSafety's rate limits):

- `permitWeekKey.weeklyRate`: integer, fallback `4`.
- `permitWeekKey.hoardCap`: integer, fallback `8`.

No Dashboard values have been created or published for this task. These fallbacks match the stub
doc's stated design target (4/week, hoard 8), not the current shipping stopgap's numbers - the two
are intentionally decoupled, since this module doesn't read or write the client's local
`ManualTrustedWeekKey` state at all.

## Validation

```powershell
dotnet build CloudCode/PermitWeekKey/PermitWeekKey.sln --configuration Release
dotnet test CloudCode/PermitWeekKey/PermitWeekKey.ServerTests/PermitWeekKey.ServerTests.csproj --configuration Release --no-build
```

The Release solution configuration intentionally excludes the server test project from build/publish output.

## Status

Authored locally; server-tested locally. Not deployed. No `ugs` CLI config, Dashboard values, or
non-production environment work exists for this module - deploying it would need the same
Dashboard-access path documented for `CloudCode/SocialSafety/` (see
`docs/SocialSafety_NonProduction_Validation_Checklist.md` for the shape that validation should
take), not attempted here.

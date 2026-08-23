# Guild Expedition Cloud Code module

This is a **first-pass, deliberately scoped-down** server module scaffold. It is authored but not
deployed. Design source: `GUILD_EXPEDITION_COMPETITION_RECONCILIATION_2026-08-23.md` (Drive WIP) -
that document is itself a **discussion packet, "READY FOR IMPLEMENTATION: No"** as of the date in
its filename, not an implementation lock. This scaffold implements only the part CC scoped in for
a first pass: the personal attempt/score/claim loop.

## Scope

**Implemented:**

- **Attempts** (§4.2): a per-account regenerating bank, +3/UTC-day up to a cap of 6, no paid/ad/
  item refill path anywhere in this module. `ConsumeExpeditionAttempt`.
- **Objective scoring** (§4.2, §4.3): verified objective completions score a fixed, server-resolved
  point value (never client-supplied), best-result-once per objective ID per Expedition week,
  clamped to the personal 1,000-point weekly cap. `SubmitExpeditionObjectiveResult`.
- **Personal milestone claims** (§5.1): the five Guild Contribution bands (100/250/400/700/1000 ->
  25/35/40/45/55), each independently idempotent per Expedition week, gated on the account's own
  current-week total actually reaching that band's threshold. `ClaimExpeditionMilestone`.
- **Expedition week key** (§3.1): Monday-00:00-UTC-to-Monday, server-computed only - no request in
  this module accepts a week key, timestamp, or point value from the caller (there's a reflection
  test asserting this directly, same as PermitWeekKey's equivalent).

**Deliberately deferred** (per CC's explicit scope-down instruction - this is a big packet, and
guild membership/roles/offices is a separate system):

- **Guild membership, eligibility snapshots, the 24-hour new-member wait** (§4.2, §7). This module
  currently scores whichever `context.PlayerId` calls it, with no guild affiliation check at all -
  that's the single biggest gap between this scaffold and a real launch, and it's a hard
  prerequisite for everything below.
- **Shared route/node meters** (§4.1) - the 3-route/4-node shared map, 3-of-4/4-of-4 completion
  thresholds, raid modifiers. Node scoring here is modeled as pure personal objective completion;
  there is no multi-member shared progress state at all.
- **Raid phases** (§4.1, §4.3) - the manifest includes three illustrative raid objective IDs so the
  personal scoring path can be exercised, but there is no raid-availability gating (day 5+, or
  earlier if all routes reach 3/4) and no shared raid-phase meter.
- **Guild-level collective rewards** (§5.2) - the 25%/50%/75%/100% guild-completion bands and
  season league rank 1-25 rewards. Those require an actual guild-wide aggregate, which requires
  membership first.
- **Season/league aggregation, promotion/relegation, offices** (§2.1, §5.2) - explicitly out of
  scope per CC; a separate system.
- **Breadth bonus, top-30 capped aggregation** (§1.2, §4.2) - guild-level, same membership
  dependency.
- **The event manifest system** (§4.1) - `StaticExpeditionManifest` is an in-code placeholder table
  of illustrative objective IDs/point values (see its own doc comment), standing in for what should
  be per-Expedition-week data content. **A real manifest needs to replace this before any real
  Expedition ships** - the specific point values here are not tuned economy numbers.
- **The Ascension Permit portion of the milestone bands** (§5.1). The doc is explicit that
  Expedition's four weekly Permit milestones "consume the complete 4/week trusted ceiling" that
  `CloudCode/PermitWeekKey/` issues against, and that activating the paired 8-held cap alongside
  4/week issuance is its own review gate (§5.1, §9 item 5). Reconciling two modules' issuance
  against one shared weekly ceiling is a real cross-service design decision - not something to
  improvise inside this scaffold. This module grants only Guild Contribution; the Permit amounts
  from §5.1's table are not implemented anywhere.
- **The loophole controls in §7** beyond basic idempotency/optimistic-concurrency (alt-farm
  detection, anomaly telemetry, guild-hop/kick-before-payout handling) - all guild-membership- or
  telemetry-dependent.

## Dependencies

Same package versions as `CloudCode/SocialSafety/` and `CloudCode/PermitWeekKey/`:
`Com.Unity.Services.CloudCode.Core` `0.0.5`, `Com.Unity.Services.CloudCode.Apis` `0.0.26`.

## Remote Config

- `guildExpedition.attemptsPerDay`: integer, fallback `3`.
- `guildExpedition.maxBankedAttempts`: integer, fallback `6`.
- `guildExpedition.personalWeeklyCap`: integer, fallback `1000`.

Fallbacks match §4.2's stated launch recommendations. No Dashboard values have been created.

## Validation

```powershell
dotnet build CloudCode/GuildExpedition/GuildExpedition.sln --configuration Release
dotnet test CloudCode/GuildExpedition/GuildExpedition.ServerTests/GuildExpedition.ServerTests.csproj --configuration Release --no-build
```

The Release solution configuration intentionally excludes the server test project from build/publish output.

## Status

Authored locally; server-tested locally. Not deployed. Same "not safe as local-save/client-
authoritative content" caveat the source packet states in its own §8 applies in full - this
scaffold only proves the attempt/score/claim loop's shape; it is not a complete or deployable
Guild Expedition backend.

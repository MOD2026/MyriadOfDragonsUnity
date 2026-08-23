# CloudCode Track Status (2026-08-23)

Reference doc for whoever picks up CloudCode deployment next. Four server modules were authored
today, in this order: `SocialSafety` (validated, pre-existing scaffold), `PermitWeekKey`,
`GuildExpedition`, `Bazaar`. All four are **authored and locally server-tested only — none are
deployed.** All four share the same rigor: `dotnet build --configuration Release` (0 errors) +
`dotnet test` against a real or in-memory store, no `PlayerProfile.cs`/Save-shape change, no
Dashboard values created, no `ugs` CLI config anywhere in the repo.

**Deployment readiness is not equal across the four.** `SocialSafety` is furthest along — it has a
written non-production validation checklist and is the one currently being walked through against
the real Unity Dashboard. `PermitWeekKey`, `GuildExpedition`, and `Bazaar` are authored-only: no
Dashboard work, no non-production validation plan written for them yet.

## At a glance

| Module | Tests | Build | Deployment status |
|---|---:|---|---|
| `CloudCode/SocialSafety/` | 43/43 | 0 errors, 2 warnings | **Furthest along** — has a written non-production validation checklist; Dashboard walkthrough in progress today |
| `CloudCode/PermitWeekKey/` | 33/33 | 0 errors, 2 warnings | Authored only — no Dashboard work |
| `CloudCode/GuildExpedition/` | 38/38 | 0 errors, 2 warnings | Authored only — no Dashboard work |
| `CloudCode/Bazaar/` | 33/33 | 0 errors, 0 warnings | Authored only — no Dashboard work |

Test counts confirmed fresh via `dotnet test` against each module's own `.ServerTests.csproj`
today. 147 tests total across the four modules, all passing.

---

## SocialSafety — block/mute relationships

**What it does:** `BlockAccount`/`UnblockAccount`/`MuteAccount`/`UnmuteAccount`. Server-owned
relationship records (SHA-256-keyed by actor+kind+target, no raw account ID in the key), optimistic
concurrency via Cloud Save's `WriteLock` with one stale-write reconciliation retry, a durable
per-actor rate limiter (Remote-Config-backed per-minute/per-day caps, fallback 10/100), sanitized
error codes (no raw SDK exception text reaches the client).

**Deferred/unimplemented:** `SubmitReport` (not implemented — social-safety reporting is a separate
slice). Guild-scoped block/mute is out of scope. The production access-policy config
(`config/social-safety-access-policy.json`) remains **draft and deployment-blocking** — per
`docs/Metagame_Handoff.md` §8, it's unverified whether Cloud Code's own trusted writes (using
`context.AccessToken`) are denied by the Player write-deny rule inside that policy; this needs
either Unity documentation, support confirmation, or a controlled non-production test before real
traffic touches it.

**Deployment path:** `docs/SocialSafety_NonProduction_Validation_Checklist.md` is the written plan —
covers Remote Config setup, access-policy verification, all 8 endpoint behaviors, security/privacy
checks, and rollback. As of this doc, every checkbox in it is still unchecked; live non-production
validation has not been run.

---

## PermitWeekKey — server-authoritative weekly Permit claims

**What it does:** `GetPermitStatus` (read-only) and `ClaimWeeklyPermit`. Server-computed ISO-8601
week key (`YYYY-Www`, Monday-anchored — see `IsoWeekKey.cs`, implemented directly against the ISO
rule rather than a framework API this scaffold can't verify against the real Cloud Code runtime).
Claim identity is `accountId + activityId + week key`; a repeat claim in the same server week
returns `alreadyClaimed: true` rather than granting again. Neither endpoint request type can carry
a week key, timestamp, or point value — the server is the sole source for all three.

**Deferred/unimplemented:** Nothing beyond the design doc's own stated non-goals. Explicitly **not**
a replacement for the shipping client-side stopgap (`ManualTrustedWeekKey`, still 4/week + 8-hoard
in `PlayerEmpireData.cs`/`CollectionSchemaRules.cs`) — `PlayerProfile.cs` has no new field. Remote
Config fallbacks (`permitWeekKey.weeklyRate` = 4, `permitWeekKey.hoardCap` = 8) intentionally match
the stub doc's design target and are decoupled from the live stopgap's own numbers.

**Deployment path:** none written yet. Would follow the same Dashboard-access shape as
SocialSafety's checklist (per this module's own README) — not drafted for PermitWeekKey
specifically.

---

## GuildExpedition — personal attempt/score/claim loop (Phase 1 of a much larger design)

**What it does:** `ConsumeExpeditionAttempt` (regenerating bank, +3/UTC-day capped at 6, no refill
path anywhere), `SubmitExpeditionObjectiveResult` (server-resolved objective points via
`StaticExpeditionManifest`, best-result-once per objective per Expedition week, clamped to a
1,000-point personal weekly cap), `ClaimExpeditionMilestone` (the five Guild-Contribution-only
personal bands from the design doc's §5.1: 100/250/400/700/1000 → 25/35/40/45/55 GC, each
independently idempotent per week). Source doc
(`GUILD_EXPEDITION_COMPETITION_RECONCILIATION_2026-08-23.md`, Drive WIP) is itself a discussion
packet — "READY FOR IMPLEMENTATION: No" — so this module is a deliberately narrow first pass, not
full coverage of that packet.

**Deferred/unimplemented** (all documented in the module's own README):
- Guild membership, eligibility snapshots, the 24-hour new-member wait — **the single biggest
  gap**; this module currently scores whichever `PlayerId` calls it, with zero guild-affiliation
  check.
- Shared route/node meters, raid-phase gating, guild-level collective rewards (§5.2),
  season/league aggregation, promotion/relegation, offices, breadth bonus, top-30 capped
  aggregation — all guild-membership-dependent.
- `StaticExpeditionManifest` is an explicitly-labeled in-code placeholder table of illustrative
  objective IDs/points, not tuned content — a real event manifest system has to replace it before
  anything ships.
- **The Ascension Permit portion of the milestone bands** — see "Cross-cutting gaps" below.

**Deployment path:** none written. Not usable for anything beyond proving the personal-loop
mechanics without a guild membership system in front of it.

---

## Bazaar — Phase 1 tradeable-card marketplace (list/buy/sell/escrow)

**What it does:** `ListBazaarItem`, `BuyBazaarItem`, `CancelBazaarListing`, `GetBazaarWallet`.
Enforces rarity 1-4 + `Tradeable`-flag eligibility, the 7-day acquisition hold (from mint) or
72-hour relist hold (after a Bazaar purchase), self-trade rejection, a seller's rolling "max 5
completed sales per 7 days" cap, and the exact CC-locked 12% sale tax split (6% burned / 6%
Treasury). Idempotency-keyed purchases — a retried call with the same (buyer, key) returns the
original outcome rather than double-debiting. Real persistence: `CloudSaveBazaarStore` uses Cloud
Save's **Game Data / Custom Items** feature (one shared `customId`, `"bazaar-board"`, holding both
`ItemInstance` and `BazaarListing` records, since ownership moves between two different accounts on
every sale) — verified against the actual installed `Com.Unity.Services.CloudCode.Apis` 0.0.26
assembly by reflection, not assumed from docs alone. Wallets and per-seller sale history stay on
the ordinary per-player Cloud Save pattern the other three modules use, explicitly keyed by account
ID rather than always `context.PlayerId` (a purchase must credit the seller, not just debit the
buyer).

**Deferred/unimplemented** (all documented in the module's own README):
- The one-time capped Treasury genesis-liquidity reverse auction (`BAZAAR_PHASE1_CC_ACCEPT_2026-
  08-23.md` decisions 3-4) — a separate, later module by design; a fundamentally different
  one-time mechanism (Treasury buys lowest asks at a uniform clearing price, once, budget never
  refills), not part of the recurring list/buy/sell loop this module covers.
- "Seller retains one usable copy" (system packet §1) — needs real Collection copy-count
  integration this module doesn't have.
- **Gold fee enforcement** — see "Cross-cutting gaps" below.
- Account-age/activity/step-up-auth gates, linked-account/device-cluster/wash-pattern detection —
  identity/telemetry systems that don't exist. Self-trade rejection IS implemented (needs nothing
  external).
- Listing expiry (no scheduler/cron in this scaffold — only explicit seller cancellation).
- True cross-entity transactional atomicity — the source packet's own §5 lists "atomic database
  transactions and escrow" as a still-open backend dependency; this scaffold doesn't invent a
  workaround, only per-entity optimistic-lock retry plus idempotency.
- Full append-only ownership/wallet ledger history — only the current balance and the rolling-sale
  timestamps the business rules actually need are modeled.
- At real scale: Custom Items caps at 2,000 keys per `customId` — a high-volume marketplace would
  need sharding across multiple `customId`s or Game Data's query/index support, neither
  implemented here.

**Deployment path:** none written.

---

## Cross-cutting gaps (span more than one module)

1. **Permit issuance reconciliation between PermitWeekKey and GuildExpedition.**
   `GUILD_EXPEDITION_COMPETITION_RECONCILIATION_2026-08-23.md` §5.1 states Guild Expedition's four
   weekly Permit milestones "consume the complete 4/week trusted ceiling" that PermitWeekKey
   issues against — the two modules are meant to share one weekly allowance, not mint separately.
   Per `OWNER_REVIEW_LOG.md` this principle is already decided, but the actual *wiring* between the
   two modules (which one is authoritative for the shared counter, how a call to one affects what
   the other will grant) is unbuilt. `GuildExpedition`'s milestone claims currently grant Guild
   Contribution only — no Permit code exists in that module at all, specifically to avoid
   improvising this reconciliation. Real future work, not a bug in either module today.

2. **Bazaar's Gold listing fee has no server-side enforcement.** The 2% listing fee (minimum 50
   Gold) is computed and returned by `ListBazaarItem`, but nothing actually charges it — Gold lives
   only in the frozen `PlayerProfile` Save file, with no server-authoritative ledger this (or any)
   trusted Cloud Code module can debit from. This is a real architecture question for a later
   frozen-Save-shape conversation, not something to route around by inventing a parallel Gold
   ledger inside Bazaar.

## What "authored but not deployed" means in practice

None of the four modules have: a `ugs` CLI config file anywhere in the repo, any Remote Config
values actually created on a real environment, a non-production environment selected, or any
Dashboard-side module upload. Every Remote-Config-backed number in all four modules is currently
running on its documented in-code fallback only. Deploying any of them for real needs, at minimum,
the trusted-server/backend dependency that `PHASE1_BAZAAR_SYSTEM_PACKET_2026-08-23.md` §5 and
`BAZAAR_PHASE1_CC_ACCEPT_2026-08-23.md` both call out — authenticated account IDs, server clock,
atomic escrow, fraud telemetry, dispute/audit access — none of which this repo's scaffolds attempt
to provide; that's explicitly outside what a Cloud Code module itself can supply.

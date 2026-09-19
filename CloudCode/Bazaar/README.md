# Bazaar Cloud Code module

First-pass server module scaffold. Authored but not deployed. Design source:
`PHASE1_BAZAAR_SYSTEM_PACKET_2026-08-23.md` (Drive WIP) and
`docs/BAZAAR_PHASE1_CC_ACCEPT_2026-08-23.md`. The accept doc's own text is blunt about where this
stands: **"No Bazaar code is assigned to any seat until [the trusted-server dependency] has an
owner and an approach."** That's still true for a real deployment - this scaffold exists for the
same reason `CloudCode/SocialSafety`, `CloudCode/PermitWeekKey`, and `CloudCode/GuildExpedition`
do: to prove the design out locally, ready to deploy once that dependency is resolved, not to
bypass it.

## Scope

**Implemented** - the core list/buy/cancel loop, per the accept doc's locked decisions 1-2 and the
system packet's §§1-3:

- **Listing** (`ListBazaarItem`): validates ownership, the `Tradeable` flag, rarity 1-4 band, and
  the correct hold - 7-day acquisition hold from mint, or the shorter 72-hour relist hold if this
  copy's most recent acquisition was itself a Bazaar purchase. Returns the Gold listing fee due
  (2% of ask, minimum 50) as a computed value - see "Deferred: Gold fee enforcement" below.
- **Buying** (`BuyBazaarItem`): rejects self-trades, checks buyer balance, enforces the seller's
  rolling "max 5 completed sales per 7 days" cap, applies the 12% sale tax (6% burned / 6%
  Treasury) exactly as locked, transfers ownership, closes the listing. Idempotency-keyed: a
  retried call with the same (buyer, key) returns the original outcome rather than debiting twice.
- **Cancelling** (`CancelBazaarListing`): seller-only, returns the instance to `Owned`.
- **Wallet read** (`GetBazaarWallet`).

**Persistence backend: CLOSED.** `CloudSaveBazaarStore` is a real implementation, using Cloud
Save's **Game Data / Custom Items** feature - confirmed via docs.unity.com/en-us/cloud-save/
concepts/game-data and verified against the actual installed `Com.Unity.Services.CloudCode.Apis`
0.0.26 assembly by reflection (not assumed from docs alone): `ICloudSaveDataApi.
GetCustomItemsAsync`/`SetCustomItemAsync`/`DeleteCustomItemAsync` exist with the same
`(executionContext, accessToken, projectId, customId, ...)` shape as this project's existing
player-scoped calls, `customId` standing in for `playerId`. Both `ItemInstance` and
`BazaarListing` live under one fixed shared `customId` ("bazaar-board") since ownership moves
between two different accounts on every sale; Custom Items' default Access Class is exactly
"readable by any player, writeable only from a server," matching the packet's server-authoritative
requirement precisely. Wallets and the idempotent-buy-result ledger stay on ordinary player-scoped
Cloud Save, keyed explicitly by account ID rather than always `context.PlayerId` (a purchase must
credit the seller, not just debit the buyer). See `CloudSaveBazaarStore`'s own doc comment for the
full reasoning, including the known scale limitation (2,000 keys per customId - fine for Phase 1,
would need sharding or Game Data's query/index support at real scale, not implemented here).
`Bazaar.ServerTests` still exercises all business logic against the in-memory reference store from
the first pass; that coverage is unaffected by adding the real store alongside it.

**Deliberately deferred:**

- **The one-time capped Treasury genesis-liquidity reverse auction** (accept doc decisions 3-4). A
  separate, later module per CC's own instruction - a fundamentally different one-time mechanism
  (Treasury buys lowest asks at a uniform clearing price, once, budget never refills), not part of
  the recurring list/buy/sell loop.
- **"Seller retains one usable copy"** (system packet §1). Requires knowing the seller's total
  owned copy count for a `definitionId`, which lives in the Collection/Save system this module has
  no integration with. Not enforced here.
- **Gold fee enforcement.** The 2% listing fee is Gold-denominated, but Gold is currently Save-side
  only (`PlayerProfile`, frozen) - there is no server-authoritative Gold ledger this trusted module
  can debit from. `ListBazaarItem` computes and returns the fee due; nothing here actually charges
  it. A real launch needs either a server-side Gold ledger or a different fee currency.
- **Account-age/activity/step-up-auth gates, linked-account/device-cluster detection, price-outlier
  and wash-pattern detection** (system packet §3, decision 5 non-goals). All require identity/
  telemetry systems this scaffold doesn't have. Self-trade rejection (comparing seller and buyer
  IDs) IS implemented, since it needs nothing external.
- **Listing expiry.** No scheduler/cron exists in this scaffold; only explicit seller-initiated
  cancellation is implemented.
- **True cross-entity database transactions / escrow.** Cloud Save still has none. `BuyItemAsync`
  is instead a resumable forward-recovery saga (durable settlement journal written before the
  first mutation, each step idempotent - listing claim carries a settlementId, wallets record
  applied settlement ids in the same save as the balance change), so a mid-settlement failure is
  completed by retrying with the same idempotency key and can never charge or credit twice.
  `BazaarReconciliationSweep` (library class, deliberately NOT a CloudCodeFunction) recovers journals
  stuck InProgress for buyers who never retry, and repairs stale active-index entries, discovering work
  through the active-listing index (a claimed listing's SettlementId locates its journal). Still open:
  deciding how/when it is invoked (endpoint or scheduler, run cadence, any minimum journal age, alerting
  - none invented here), and a live `nonprod-validation` run of the cross-account wallet path (wallet
  reads/writes now use `ServiceToken`, same fix as Friends BE-FRIENDS-STORAGE-014).
- **The append-only ownership/wallet ledger history** (system packet §2). Only the current balance
  and the rolling-sale timestamps the business rules actually need are modeled; full transaction
  history is not.

## Dependencies

Same package versions as the other three modules: `Com.Unity.Services.CloudCode.Core` `0.0.5`,
`Com.Unity.Services.CloudCode.Apis` `0.0.26`.

## Remote Config

- `bazaar.listingFeePercent`: integer, fallback `2`.
- `bazaar.listingFeeMinimumGold`: integer, fallback `50`.
- `bazaar.saleTaxPercent`: integer, fallback `12`.
- `bazaar.saleTaxBurnPercent`: integer, fallback `6`.

These are CC-locked numbers, not placeholders - the Remote Config layer exists for operational
tunability (matching the other modules' pattern), not because the values are undecided. Holds (7
days / 72 hours) and the sales cap (5 per 7 days) are fixed constants, not Remote Config-backed, in
this scaffold.

## Validation

```powershell
dotnet build CloudCode/Bazaar/Bazaar.sln --configuration Release
dotnet test CloudCode/Bazaar/Bazaar.ServerTests/Bazaar.ServerTests.csproj --configuration Release --no-build
```

## Status

Authored locally; server-tested locally. Not deployed. `BazaarModule` has its parameterless
constructor back (defaults to `CloudSaveBazaarStore`), matching the other three modules, now that
the persistence backend gap is closed. This is a scaffold for the recurring list/buy/sell loop
only; genesis liquidity and the Gold ledger remain open, separately-owned decisions.

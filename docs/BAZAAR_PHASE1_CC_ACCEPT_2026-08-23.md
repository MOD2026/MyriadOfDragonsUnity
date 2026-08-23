# Bazaar Phase-1 — Command Centre LOCK (2026-08-23)

**Status:** Design **LOCKED**. Owner reopened Market/Bazaar for Phase 1 on 2026-08-23 (previously
Phase 2-only per `EMPIRE_SCHEMA_LOCK_2026-08-22.md` / `Economy_Blueprint.md`).
**No code exists yet.** This is a from-scratch system — see gate below before any is written.

**Source packets (Drive WIP, all CC-reviewed):**
1. `PHASE1_BAZAAR_SYSTEM_PACKET_2026-08-23.md` — catalogue, ledger, non-goals, server dependency
2. `BAZAAR_GENESIS_LIQUIDITY_RECOMMENDATION_2026-08-23.md` — mechanism choice
3. `BAZAAR_GENESIS_LIQUIDITY_NUMBERS_2026-08-23.md` — locked numbers

## Locked decisions

| # | Decision | Verdict |
|---|---|---|
| 1 | Phase 1 catalogue: rarity 1-4 base cards only, `Tradeable` provenance, seller retains one usable copy; Event Medals and Gold/Gem-acquired (Soulbound) cards never tradeable | **Locked** |
| 2 | Ownership/ledger: server-side `ItemInstance`/`OwnershipLedgerEntry`/`BazaarListing`/wallet ledger, additive only, atomic list/buy/sell, idempotency keys; nothing in `PlayerProfile` beyond an optional read-only balance alias | **Locked** |
| 3 | Genesis liquidity mechanism: **one-time capped Treasury reverse auction** — Treasury buys lowest asks at a uniform clearing price, burns every purchased card, budget never refills | **Locked** |
| 4 | Genesis numbers: earliest Day 60 (never launch day); ≥500 eligible-cluster snapshot; activation gate ≥125 valid bids incl. ≥100 at ≤400 Credits; 7-UTC-day window, once; ≤100 accepted lots; 40,000 Market Credit gross ceiling (35,200 max to players after 12% tax); miss the gate → no launch, no loosened rules | **Locked** |
| 5 | Explicit non-goals: no cash-out, no account transfer, no Gold/Gem/Event Medal bridge in any direction, no rarity 5-7 trading, no change to Empire/economy locks | **Locked** |

## Still open — blocks implementation

1. **Trusted-server/backend dependency** (`PHASE1_BAZAAR_SYSTEM_PACKET_2026-08-23.md` §5) —
   authenticated account IDs, server clock, atomic escrow, fraud telemetry, dispute/audit access.
   No owner assigned. This, not the numbers, is what's actually blocking code.
2. Day-60 gate assumes live server-side account-age/activity tracking that doesn't exist yet — a
   launch-timing detail, not a Phase-1-coding blocker today.

**No Bazaar code is assigned to any seat until #1 has an owner and an approach.** Cursor's current
Block AC explicitly excludes Bazaar/trading code — that stands.

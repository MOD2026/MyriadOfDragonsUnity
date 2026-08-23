# Empire schema — deep review (Command Centre)

**Source packet:** ChatGPT `EMPIRE_SAVE_AND_PROGRESSION_SCHEMA_PACKET_2026-08-22.md`  
**Status:** **NOT LOCKED.** Direction OK; fix holes below before owner Yes/No.

Interactive copy: Cursor canvas `empire-schema-loopholes`.

## Do not rubber-stamp

| # | Hole | Future bite |
|---|---|---|
| G1 | Gate map tops out ~Ch7; bible needs **10 chapters** | Ch8–10 undefined → softlock or ad-hoc hacks |
| G2 | Gate as chapter key vs today’s “win stage → unlock next” | Breaks live unlock tests + current phone loop unless dual-condition designed |
| G3 | Barracks **L30 ceiling** vs live formula (**20 slots at L50**) | Schema lies about progression end; UI/players expect L30 = done |
| E1 | Empire Gold sink while **Normal Battle** still prints 250g/25gems free | Empire “grind” is fake; Castle rush from farm gold. Phase-1 Offline A is Gold → instant `ReadyToCollect`, not Gold+time. |
| E2 | Client `completesAtUtcTicks` | Save edit / clock cheat; worse once Gems speed-ups exist |
| S1 | Profile level fields vs `PlayerEmpireData` dual-write | Historical 0-card deck class of bugs if completion API isn’t singular |

## Live math packet under-sells

- AI HP is scaled from **player `StartingAvatarHealth`** (includes Castle). Castle is mostly **shared fight length**, not a player-only tank cheat.
- Production match gives **both sides player ResourceCap**. Castle Resource is shared budget.
- Real asymmetric Empire power today: **Barracks deck size** + **Gate access** (if shipped).
- Barracks still computes unused regen/replenish — shipping Barracks UI that shows them = FAKE DEPTH (§J).

## Collision with §B chapter gates

Packet makes Gate the route key. Constitution also wants Avatar/collection/mini-game gates. Lock a hierarchy: **Gate necessary, not sufficient** (or explicitly replace the others).

## Revised owner questions (after ChatGPT patch)

1. Castle/Barracks/Gate only, Academy/Embassy later?  
2. Phase-1 Gold → instant `ReadyToCollect` (Offline A), one slot, no Gems — **and** Normal Battle farm policy paired? Gold+time is Phase 2 only.
3. Gate meta route clearance for **Ch1–10**, necessary but not sufficient vs stage unlocks?  
4. Additive `empireConstruction` **with** status + costCharged + idempotent complete?  
5. Barracks: **(A)** retune formula so L30 = 20 slots, or **(B)** keep L50 formula and stop calling L30 the Barracks ceiling?

No Unity Empire work until those five are answered on a **revised** packet.

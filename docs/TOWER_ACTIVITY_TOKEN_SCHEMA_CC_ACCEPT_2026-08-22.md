# Activity-token schema — CC ACCEPT (2026-08-22)

**Packet:** ChatGPT `DUAL_PRESTIGE_TOWER_ACTIVITY_TOKEN_SCHEMA_2026-08-22.md`  
**Status:** **DESIGN LOCKED.** No Unity until Tower implementation order (after Empire faucet — already done — Barracks → construction → Gate → Castle sim).

## READY FOR CC — answers

| # | Question | Answer |
|---|---|---|
| 1 | Three-token split | **Yes** — Deck Marks / Tactical Fragments / Focus Crests |
| 2 | Exact daily + weekly grants/caps | **Yes** — daily 3/1/0; weekly bands as table; weekly max 47/9/12; +7 dailies → 68/16/12 |
| 3 | Focus Crests prestige-only | **Yes** |
| 4 | Trusted calendar before production claims | **Yes** — local time display-only (same class as Empire construction) |
| 5 | Additive `towerActivity` on PlayerProfile + migration + both seats | **Yes** |

## Clarification (locked with accept)

Satellite mini-games (Formation Trial / Siege Route) **use the same three tokens**, smaller grants, still 0 Gold/Gems. Exact satellite numbers are a later addendum — they must not exceed Daily Tower intake.

Overflow hits stack cap and **discards** — never converts.

## Implementation still blocked

No `PlayerProfile` field until Command Centre opens Save with both seats. Tokens are schema-ready, not coded.

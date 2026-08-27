# The nine 2017 legacy events — program tracker

**Owner decision (2026-08-27): keep ALL nine events, fine-tune so they do not clash. Do not discard any.**
Method, also the owner's: **dissect one event at a time**, verify its numbers and patterns, then run a
simulation, and only then call that event "fail-proven". One at a time — not nine in parallel.

## Why this file exists

Until now this program lived in two places that cannot hold it:

1. **The source material sat only in a session scratchpad** (`%TEMP%\claude\...\scratchpad\`). That is a
   temp directory. It does not survive the session, it is invisible to every other seat, and it directly
   violates the standing rule that anything load-bearing lives in git. **Now copied to
   `docs/events_2017_source/`** — `EVENTS_2017_for_BS.txt` (36 KB), `game_mech_13082017.txt` (65 KB),
   `Event_Rules_2015.txt` (2.5 KB).
2. **The only locked event lived as 58 lines inside `LOCKED_DECISIONS_REGISTER.md`** with no index, so
   "how far did the event program get?" was unanswerable without grepping the register.

## Status — 1 of 9 dissected

| # | Event (2017 source name) | Line | Core loop | Dissected | Numbers | Simulated | Fail-proven |
|---|---|---:|---|---|---|---|---|
| 1 | **Chest** (Chest Hunt) | 48 | Explore 3 areas, collect 8 fragment types x2, boss grants missing | YES | YES (corrected) | in-engine run pending | **NO** |
| 2 | **Valley of the Cursed One** | 110 | Boss hunt; boss-attraction items, signal fire, locked free attack | no | no | no | no |
| 3 | **Guild vs Guild / Siege** | 167 | Roles (Guardian/Assault/Healer), morale, revive, siege meter, captains | no | no | no | no |
| 4 | **Battle Royale** | 236 | Headcount vs rival teams, attack meter, headcount boosters | no | no | no | no |
| 5 | **Puzzle Event** | 270 | Puzzle-piece drops, Instinct item, point boosters | no | no | no | no |
| 6 | **Conqueror** | 297 | Neutral-ground AI, rival rally points, hotspots, land rewards | no | no | no | no |
| 7 | **Area Clearance** | ~362 | Clear areas vs guards/players, bribe/flag economy | no | no | no | no |
| 8 | **Castle Breach** | ~392 | Attack/defend castle, surrendered troops replenish, flag cost per attack | no | no | no | no |
| 9 | **Captives** | ~448 | Captives from AI or players, food economy | no | no | no | no |

**1 of 9 dissected. Events 2-9 have never been examined** - not by BS, not by any seat. The source text
for each is in `docs/events_2017_source/EVENTS_2017_for_BS.txt` at the line numbers above.

### The shared-economy problem is visible already, before any dissection

Reading across the nine, **the same items recur in almost every event**: Signal Fire, High Security,
Potions, Flags, boost cards (points / keys / attack / area / headcount / castle), and "there will be a
daily limited time event" hotspot modifiers appear in event after event. **That is the clash the owner
asked about, and it is structural, not incidental** - nine events drawing on one item pool and one daily
attention budget. A per-event dissection will not surface it; a cross-event contention pass will.

## Chest Hunt — locked, but NOT clear

Two unresolved problems, both recorded at `0b39beb` and neither yet fixed:

- ~~The duplicate rate is a knife-edge.~~ **RETRACTED 2026-08-27.** The set is **8 fragment types x 2
  copies**, not 16 distinct items, and duplicate share is an **output** of that structure (~37% from
  uniform draws), not an assumption that can swing. Completion with the boss is ~63% at 22 drops.
  The knife-edge was an artifact of treating an output as an input.
- **The entry budget overrun is now the main dial** — completion moves 63%→69% between 22 and 23 drops.
  Resolved: 21 hard cap, boss consumes one of the remaining entries.

**Chest Hunt is not fail-proven and must not be treated as done.** Locking its numbers was step two of
four.

## Source material — what is actually in it

`Event_Rules_2015.txt` is the 2015 wiki-era rules (Chest/Keys/Golden Time/Bonus cards/trading
restriction). `game_mech_13082017.txt` (65 KB) is the 2017 mechanics document and is the primary source.
`EVENTS_2017_for_BS.txt` is the extract prepared for BS.

**Known structural elements across the 2017 events**, from the source: Wooden vs Gold chests, Normal vs
Gold keys, event items, Lock Potions, food, gold, points-booster cards, key-drop-booster cards, signal
fire, experience boost, Unlock Points, Golden Time windows, Rival rankings.

## The clash risk the owner named

The owner's instruction is not merely "keep them" — it is "make sure they do not clash." Three overlapping
solo daily loops are already flagged as an open merge decision elsewhere in the register. **Nine events
sharing boosters, keys, currencies and daily time budgets is exactly where clashes appear**, and a
one-at-a-time dissection will not surface them by itself. A cross-event contention pass is required after
the individual dissections — currency sources, entry budgets, daily time demand, and booster-card stacking.

## Next action

Chest Hunt's simulation, and the two open defects above, **before** starting event 2. Finishing one event
properly is the method the owner asked for; starting eight more dissections while event 1 has an unresolved
knife-edge would produce nine half-answers.

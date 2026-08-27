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

| # | Event | Dissected | Numbers locked | Simulated | Fail-proven |
|---|---|---|---|---|---|
| 1 | **Chest Hunt** | YES | YES (`0b39beb`) | **NO** | **NO** |
| 2–9 | *(eight remaining — names to be extracted from the 2017 source)* | no | no | no | no |

## Chest Hunt — locked, but NOT clear

Two unresolved problems, both recorded at `0b39beb` and neither yet fixed:

- **The duplicate rate is a knife-edge.** At a 35% duplicate rate the player nets 16.56 unique items
  against 16 needed — completes. At 40% it nets 15.44 — **fails**. A five-point swing in one assumption
  flips the whole event between completable and impossible. **That is too tight to ship.**
- **The entry budget overruns by one**: 22 entries against a 21 cap.

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

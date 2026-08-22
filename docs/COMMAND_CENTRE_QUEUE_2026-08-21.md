# Command Centre — Ops Board

**Bible:** `docs/CORE_SYSTEMS_CONSTITUTION.md` §0 wartime + **§B 10-chapter spine**  
**Doctrine:** (1) fastest path to whole playable game (2) token efficiency. Polish later.

**Status:** Ch1–3 **DONE** (63 stages). Planned Ch4–10 (30 each). Gates + mini-games = locked intent, not built.

---

## Claude — NEXT (paste below): Chapter 4 fill

## After that
Ch5→Ch10 bulk fills. Separately: chapter gates + first mini-game (may need Metagame/`HomePagePresenter` — escalate, don’t silent-edit frozen contract).

## Parked
HUD/HomeV3/animation, Empire, per-stage feel polish, abstract XP/spell tables.

---

## CLAUDE PASTE

```
Read docs/CORE_SYSTEMS_CONSTITUTION.md §0 + §B + §C first. Obey wartime doctrine.
(1) fastest path to whole playable game (2) token efficiency. No polish. No decision cards. No XP/spell/Empire redesign. No HUD. No mini-game implementation in this pass (intent only in §B).

TASK (one pass): Fill Chapter 4 stages 4-1 through 4-30.
Chain must be: 1-1..1-12 → 2-1..2-21 → 3-1..3-30 → 4-1..4-30.
Win 3-30 unlocks 4-1; win 4-N unlocks 4-(N+1); 4-30 → null for now (Ch5 comes next pass — do not invent 5-1 yet unless required by an existing test pattern; prefer null terminal until Ch5 fill).

CONTENT RULES (same as Ch2/Ch3 builders):
- Pool+stride data-driven builder; real Catalog ids; AF-measured; no within-stage duplicate ids; no exact full-roster clone of ANY existing stage (offset the pool like Ch3 vs Ch2).
- Escalate via enemy decks only — §C knobs locked.
- Rewards escalate from 3-30; leave headroom for Ch5+.
- StoryDatabase templated pre/post for 4-1..4-30 matching titles/enemy names.

TESTS: Chapter4FullDepthTests (or generalize) — full chain through 4-30, unlock loop, AF wins for new stages, config valid, distinct rosters. Fix stale end-of-list assertions (3-30 is no longer terminal). Unity closed; EditMode; real counts; never -quit.

DONE: files + test numbers. Stop. No product essays.
```

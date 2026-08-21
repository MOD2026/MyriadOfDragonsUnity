# Command Centre — Ops Board

**Bible:** `docs/CORE_SYSTEMS_CONSTITUTION.md` (**§0 wartime LOCKED**)  
**Doctrine:** (1) fastest path to whole playable game (2) token efficiency. Polish later.

**Status:** Ch1 **1-12** + Ch2 **2-1..2-21** live. Next Claude: **bulk fill 3-1..3-30**.

---

## Claude — NEXT assignment (paste below)

## Parked
§M abstract locks, HUD/HomeV3/animation, Empire, per-stage feel tuning.

---

## CLAUDE PASTE

```
Read docs/CORE_SYSTEMS_CONSTITUTION.md §0 + §B + §C first. Obey wartime doctrine:
(1) fastest path to whole playable game (2) token efficiency. No polish. No decision cards. No XP/spell/Empire redesign. No HUD.

TASK (one pass): Fill Chapter 3 campaign stages 3-1 through 3-30. Chain must be ordered 1-1..1-12 then 2-1..2-21 then 3-1..3-30. Win 2-21 unlocks 3-1; win 3-N unlocks 3-(N+1); 3-30 is terminal (GetNextStageId null) unless an existing pattern needs a stub — do not invent Chapter 4.

CONTENT RULES (same as Ch1/Ch2 depth passes):
- Real card ids from Catalog only; AF-measured decks; reuse weak ids across stages OK; no duplicate ids within one stage; no exact full-roster clone of another stage.
- Escalate via enemy decks only — do NOT change global combat knobs (§C locked).
- Gold/gem rewards escalate from 2-21; leave readable steps across 3-1..3-30.
- Reuse compact data-driven builder pattern from BuildChapter2DepthStages — do not hand-type 30 arrays if a pool+stride works.
- StoryDatabase: templated pre/post for 3-1..3-30 matching stage titles/enemy names. No essay docs.

TESTS: Extend/generalize Chapter2FullDepthTests (or add Chapter3FullDepthTests) for full 1..3 chain + config valid. Fix any stale end-of-list assertions. Unity closed; EditMode; real pass/fail counts; never -quit.

DONE when: 3-1..3-30 in code, unlock chain continuous, EditMode green for suites you touch, short DONE: files + test numbers. Stop. No product decision proposals.
```

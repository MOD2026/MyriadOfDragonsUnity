# Command Centre — Ops Board

**Bible:** §0 deep review · §I Empire DESIGN LOCKED · dual-prestige mini-games  
**Empire:** feasibility **ACCEPTED** (offline A, Barracks paid milestones, ReadyToCollect)  
**Campaign:** Ch1–6 **DONE** (153 stages). Claude → **Ch7**.

---

## VS Code / Metagame — NEXT (paste): Economy Faucet

Do **not** assign this to Claude (touches `HomePagePresenter`).

## Claude — NEXT: Chapter 7 (paste at bottom)

## ChatGPT — NEXT: Dual-prestige mini-game packet (paste below)

## Parked
Empire construction/UI code until faucet green; TD implementation; HUD polish.

---

## VS CODE PASTE — Economy Faucet only

```
Metagame seat. Read docs/EMPIRE_SCHEMA_LOCK_2026-08-22.md + EMPIRE_FEASIBILITY_AUDIT (Codex folder).

TASK (one slice ONLY): Economy faucet correction.
- Ordinary Normal Battle victory: grant 0 Gold and 0 Gems (fix HomePagePresenter.HandleMatchCompleted non-campaign branch that currently +250g/+25gems).
- Tutorial: already no economy rewards — prove with tests; do not add rewards.
- Campaign first-clear Gold/Gems + claimedStageRewardIds dedup: UNCHANGED.
- Campaign replay: still 0 economy (existing contract).
- Avatar XP / RecordMatchResult: do not change unless a test forces a touch — prefer leave XP as-is.
- Route spends through existing patterns; prefer CurrencyManager if that file already owns grants — match project norms.
- Focused EditMode tests: repeat Normal victories do not change gold/gems; Tutorial does not; Campaign first-clear still grants once.
- Unity closed; real pass/fail; never -quit.
- Do NOT touch Empire construction, Gate, Barracks formula, Castle, Shop SKUs, HUD polish.

DONE: files + test numbers. Stop.
```

---

## CHATGPT PASTE — Dual prestige mini-games

```
Design seat. No Unity. Do not reopen Empire schema locks.

KEEP: Empire-powered daily + weekly mini-games that create upgrade hunger; Tower-style climb OK; rewards beef deck with caps.

REPLACE pure-Empire-as-only-ranking with DUAL PRESTIGE (locked intent in CORE_SYSTEMS §B):
1) Card prestige = Campaign mastery (+ future PvP). Power = deck/formation/spells/Avatar.
2) Empire prestige = daily clear + weekly highest Tower floor (+ ≤2 satellite mini-games). Power = Empire buildings.

DELIVERABLE packet:
- EmpirePower mapping: Barracks→capacity, Castle→tower durability (named distinct from match HP), Gate→sector unlock only
- Daily rule + Weekly highest-floor rule + reward tables with hard caps
- What leaderboards exist (Empire vs Card) — never one board that mixes both into one “rank”
- Soft walls every 10 tower floors; anti-gold-faucet vs Empire sinks
- Wartime: docs only; TD code after faucet + Empire construction slices

READY FOR CC. Stop.
```

---

## CLAUDE PASTE — Chapter 7

```
Read docs/CORE_SYSTEMS_CONSTITUTION.md §0 + §B + §C.
Wartime + deep review. No polish. No Empire/XP/HUD.

TASK: Fill Chapter 7 stages 7-1..7-30.
Chain: … → 6-30 → 7-1..7-30 → null (no Ch8 yet).
Pool+stride; no exact roster clone of any prior stage; AF-measured; SetShuffleSeedForTests.
Escalate decks only; §C locked. Rewards from 6-30 with headroom for Ch8–10 (report gold at 7-30).
StoryDatabase templated pre/post.
TESTS: Chapter7FullDepthTests; fix 6-30 terminal stale asserts. Unity closed; never -quit.
DONE: files + counts + gold at 7-30. Stop.
```

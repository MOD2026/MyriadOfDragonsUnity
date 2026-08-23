# CC MUST-READ — before Claude acts as Command Centre

**Rule:** Do not assign Block AA or any new block until this list is read (in order). Skimming the board without the locks will re-open parked Softs.

**Project root:** `C:\Users\zihan\Downloads\MyriadOfDragonsUnity\`  
**Drive WIP (optional mirror):** `G:\My Drive\card game\Game mech\MOD\Phase 1\Game mech\WIP\`

---

## Pass 1 — Seat + board (must, ~10 min)

| # | File | Why |
|---|---|---|
| 1 | `docs/CC_HANDOVER_TO_CLAUDE_2026-08-23.md` | You are CC; Cursor is WH; authority |
| 2 | `docs/SEAT_ASSIGNMENT.md` | Live seat map |
| 3 | `docs/AI_SEAT_CHART_LOCKED.md` | Path ownership history; note 2026-08-23 supersede at top |
| 4 | `docs/ZIHAN_COMMAND_CENTRE.md` | Live board (rewrite this as your board) |
| 5 | `docs/OWNER_REVIEW_LOG.md` | What is accepted / parked / locked (read **tail ~80 lines** + search Gate, Castle, Soft) |
| 6 | `.cursor/rules/golden-rule-ship-asap.mdc` | Owner golden rule + Soft bans |

---

## Pass 2 — Design constitution (must, ~20 min)

| # | File | Why |
|---|---|---|
| 7 | `docs/MOS_v1.1.md` | Design constitution (priority under code+tests) |
| 8 | `docs/CORE_SYSTEMS_CONSTITUTION.md` | Debt/build order; Gate → Castle; chapter-gate intent |
| 9 | `docs/AI_CONTRIBUTING.md` | Frozen files; EditMode rules; no shared-file thrash |
| 10 | `docs/EMPIRE_SCHEMA_LOCK_2026-08-22.md` | Castle/Barracks/Gate locks; necessary≠sufficient Gate |
| 11 | `docs/CASTLE_PHASE1_IMPLEMENTER_BRIEF_BLOCK_AA_v1.md` | Next coding block (Castle) — firm checklist |

---

## Pass 3 — MVP status (must, ~10 min)

| # | File | Why |
|---|---|---|
| 12 | `docs/MVP_PLAYABLE_GATE_v1.md` | First-session gate = **already green** |
| 13 | `docs/MVP_FIRST_SESSION_SCRIPT_v1.md` | What Zihan can play now |
| 14 | `docs/MVP_PLAYABLE_GATE_EVIDENCE_v1.md` | Evidence pointers (skim) |
| 15 | `docs/PROGRESS_STATUS.md` | Screen-level done/not (skim) |

---

## Pass 4 — Parked Softs (read so you do NOT reopen)

| # | File | Why |
|---|---|---|
| 16 | `docs/STAGE_1_2_AF_AI_ON_CAUSE_FRAME_v1.md` (also Drive WIP) | **Parked** evidence frame — do not assign |
| 17 | `docs/BLOCK_Q_STAGE_1_2_AF_AI_ON_CAUSE.md` | Parked |
| 18 | `docs/MVP_POST_FIRST_SESSION_SOFTS_v1.md` | Soft list — many closed/parked; do not spiral |

**Also parked without a dedicated reopen:** FullDepth Ch2–10 AF+AI-on combat fails (W.1 report) — separate future block only if Zihan asks.

---

## Pass 5 — Code truth (must grep/read before Gate/Castle assigns)

| # | Path | Why |
|---|---|---|
| 19 | `Assets/Scripts/Empire/PlayerEmpireData.cs` | Castle/Gate/Barracks formulas; `MinimumGateLevelForChapter`; Castle HP/Resource helpers |
| 20 | `Assets/Scripts/UI/HomePagePresenter.cs` | `TryLaunchCampaignStage` + `GateBlockedMessage` |
| 21 | `Assets/Scripts/UI/CampaignMapPresenter.cs` | `CampaignLaunchOutcome.BlockedByGate`; MVP window; chapter titles |
| 22 | `Assets/Scripts/UI/EmpirePresenter.cs` | Construction UI; Gate row summary |
| 23 | `Assets/Scripts/Save/EmpireConstructionService.cs` | Construction state machine (read; Save shape frozen) |
| 24 | `Assets/Tests/Editor/GateCampaignLaunchTests.cs` + `GateRouteTests.cs` + `GateTestSupport.cs` | Gate accepted evidence |
| 25 | Frozen — **do not shape-change:** `PlayerProfile.cs`, `SaveSystem.cs`, `SaveMigration.cs`, `SaveManager.cs` |

---

## Pass 6 — Optional / later (not required before first AA assign)

- `docs/CAMPAIGN_NAMING_KIT_CC_ACCEPT_2026-08-22.md` + naming kit on Drive  
- `docs/SINGLE_BIBLE_MASTER_PLAN_2026-08-22.md` (long)  
- `docs/MOS_SINGLE_BIBLE_v1.md`  
- Drive WIP shop/burn packets (already mostly locked — do not reopen economy)

---

## After reading — first CC act

1. Rewrite `docs/ZIHAN_COMMAND_CENTRE.md` with a firm board.  
2. Assign **Block AA** (Castle) with **one** owner + file lock.  
3. Paste firm WH work if AA does not need Cursor, or idle WH with **Stop / wait for paste** (not soft “you may”).  
4. Do **not** assign Ch1-2 Soft or FullDepth roster retune unless Zihan reopens.

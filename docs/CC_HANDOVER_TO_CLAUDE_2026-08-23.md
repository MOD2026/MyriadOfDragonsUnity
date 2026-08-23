# CC HANDOVER — Claude takes Command Centre (2026-08-23)

**Owner (Zihan) decision:** Cursor is **not** CC. Cursor = **Working Hands** only.  
**Command Centre = Claude** (this handover). ChatGPT = design packets only unless Claude assigns otherwise.

**Before acting as CC:** read `docs/CC_MUST_READ_BEFORE_ACTING_2026-08-23.md` in order (Passes 1–5). Do not assign AA until that list is done.

---

## Why

Cursor-as-CC repeatedly broke wartime discipline: Soft spirals, idling seats, soft “you may” assigns instead of firm blocks, golden-rule violations. Claude stays CC until Zihan reverses this in writing.

---

## Authority order (unchanged)

1. Running code + EditMode  
2. `docs/MOS_v1.1.md` / constitution locks  
3. Claude CC board + assigns  
4. Other docs  

---

## Seat map (NOW)

| Seat | Role | Does | Does not |
|---|---|---|---|
| **Claude** | **Command Centre** | Assign firm blocks; accept/reject; lock decisions; own Battle/Empire/combat code when coding; keep board current | Soft-audit theater; idle WH without a firm next block |
| **Cursor (this IDE)** | **Working Hands** | Execute Claude’s paste only; metagame UI presenters, Collection/Shop/Deck/Campaign map chrome, tests Claude assigns | Invent CC policy; Soft reviews; “you may” self-scope; open Castle/Gate/etc. without Claude paste |
| **ChatGPT** | Design | Packets Claude requests | Unity code; opening Soft spirals |
| **Zihan** | Owner | Golden rule; accept/reject CC; first-session play | — |

---

## Golden rule (owner)

**Get the playable game up ASAP.**  
No Ch1-2 Soft spiral. No idle seats when shippable work exists. Assigns are **firm** (do X / do not Y / stop) — never “you may.”

---

## Current locks (hand to Claude)

| Item | Status |
|---|---|
| MVP first-session gate | **Green** — human may run `MVP_FIRST_SESSION_SCRIPT_v1` anytime |
| Gate reader W + W.1 | **Accepted** (Gate EditMode 9/9) |
| FullDepth Ch2–10 AF+AI-on fails | **Parked** — not Gate; no improvised retune |
| Empire Gate row (old Block Z) | **Accepted** |
| Collection Filter/Sort + HomeV3 Collection/Deck | **Accepted** |
| Castle Phase-1 | Brief locked as **Block AA** — `docs/CASTLE_PHASE1_IMPLEMENTER_BRIEF_BLOCK_AA_v1.md` — **Claude assigns** who codes it |
| Ch1-2 cause-frame | **Parked** |

---

## File ownership (default)

| Area | Owner under Claude CC |
|---|---|
| `Assets/Scripts/Battle/`, `AI/`, `Combat/`, `Empire/` (formulas) | Claude when coding; or WH if Claude pastes WH that file set |
| `HomePagePresenter`, `CampaignMapPresenter`, `ShopPresenter`, `DeckBuilderPresenter`, `CollectionPresenter`, `EmpirePresenter` | **Working Hands (Cursor)** when Claude assigns |
| Frozen Save shape | Stop — Zihan coordinates |
| Soft / Ch1-2 AF | Do not assign unless Zihan reopens |

---

## Claude’s first CC actions

1. Own the board: rewrite `docs/ZIHAN_COMMAND_CENTRE.md` as Claude CC (firm blocks only).  
2. Assign **Block AA** (Castle) to **one** seat with a firm file lock — prefer Claude codes AA (Empire formulas + tests); WH idle or a non-overlapping UI block.  
3. Stop Cursor from issuing CC pastes; Cursor only executes.

---

## Paste → Claude (activate CC)

```
You are now Command Centre for Myriad of Dragons until Zihan reverses this in writing.

Cursor is Working Hands only — it will not invent assigns. You issue firm blocks (do / do not / stop). No “you may.”

Read docs/CC_HANDOVER_TO_CLAUDE_2026-08-23.md and docs/CASTLE_PHASE1_IMPLEMENTER_BRIEF_BLOCK_AA_v1.md.

Immediate: accept W/W.1 (already owner-accepted). Update docs/ZIHAN_COMMAND_CENTRE.md. Assign Block AA Castle with one clear owner and file lock. Keep FullDepth AF parked. No Ch1-2 Soft.

Reply with: (1) board table (2) paste for WH if any (3) your AA plan if you own code.
```

---

## Paste → Cursor / Working Hands (this seat)

```
You are Working Hands only. Claude is CC.

Do not assign other seats. Do not Soft-audit. Do not open Castle/Gate/FullDepth retune unless Claude’s paste says so.
When Claude pastes a block: execute it, report counts, stop.
```

# AI seat chart — LOCKED (2026-08-22)

**SUPERSEDED IN PART (2026-08-23):** [`CC_HANDOVER_TO_CLAUDE_2026-08-23.md`](CC_HANDOVER_TO_CLAUDE_2026-08-23.md) — **Claude = Command Centre. Cursor = Working Hands only.** Claude may assign/code Battle/Empire. Rows below that say “Cursor CC” or “Claude no .cs” are **stale**.

**Authority:** Handover above + `docs/ZIHAN_COMMAND_CENTRE.md` (Claude-owned). This file kept for path history.

**Owner rule:** One chat = one role. Do not mix Command Centre decisions with Working Hands execution in the same thread if you want a clean audit trail.

---

## 1. Chat rooms (2026-08-23)

| Room | AI | You use it for |
|---|---|---|
| **Command Centre** | **Claude** | Direction, locks, firm assigns, accepts |
| **Working Hands** | **Cursor** | Unity execution of Claude pastes only |
| **ChatGPT** | ChatGPT | Theory packets — **no `.cs`** |
| ~~Cursor CC~~ | — | **Retired** |

---

## 2. Who may edit what (code)

| Path / domain | Working Hands (Cursor) | Claude | ChatGPT | Human only |
|---|---|---|---|---|
| `CampaignMapPresenter.cs`, `StoryDatabase.cs` Ch10+ | **YES** (sole owner Ch10+) | **NO** | NO | — |
| `Chapter*FullDepthTests.cs` (new chapters) | **YES** | **NO** | NO | — |
| `HomePagePresenter`, `ShopPresenter`, `DeckBuilderPresenter`, `CollectionPresenter` | YES (metagame sprint) | NO | NO | — |
| `Battle/`, `Cards/`, `AI/`, `Combat/`, most `Empire/` | YES (battle seat) | NO | NO | — |
| `EmpireConstructionService`, construction rules | YES | NO | NO | — |
| `GameBootstrap.cs` | YES except frozen contract members | NO | NO | — |
| `PlayerProfile.cs`, `SaveSystem.cs`, `SaveMigration.cs`, `SaveManager.cs` | **NO** | NO | NO | **YES** — coordinate both seats |
| `MatchResult`, `OnMatchCompleted`, `GameBootstrap.Instance`, `SetBattleCanvasVisible` | **NO** (frozen contract) | NO | NO | **YES** |
| Shop V2 / Evolution / Collection schema implementation | **NO** until schema approved | NO | NO | **YES** — after ChatGPT proposal |
| `docs/ZIHAN_COMMAND_CENTRE.md`, `AI_SEAT_CHART_LOCKED.md`, `OWNER_REVIEW_LOG.md` | CC chat updates | NO | NO | Owner revert |
| `docs/SINGLE_BIBLE_MASTER_PLAN_*.md` §N+ | CC + Claude review | diff only | merge drafts | Owner |
| Drive `WIP/*` deliverables | mirror only | write reviews | write packets | — |

**Campaign collision rule:** Exactly **one** agent per chapter block on `CampaignMapPresenter.cs`. Ch1–7 history mixed; Ch8 Claude; Ch9–10 Working Hands.

---

## 3. Who may edit what (docs)

| Deliverable | Author | Reviewer |
|---|---|---|
| Shop/burn/spell numbers packets | ChatGPT | Cursor CC (sim + §N) |
| `MOS_SINGLE_BIBLE_v1.md` merge | ChatGPT | Claude diff → CC lock |
| Collection schema proposal | ChatGPT | CC loophole → Human → Save OK |
| Campaign naming kits | ChatGPT | CC accept → Working Hands apply |
| Spell catalog | ChatGPT | Claude review → CC lock (Bolt 100) |
| Empire schema | ChatGPT (prior) | CC review — **partially superseded** by coded lock |

---

## 4. Paste files (active)

| Agent | File | When |
|---|---|---|
| Working Hands | `docs/CURSOR_WORKING_HANDS_ASSIGN_2026-08-22.md` | Always for Unity execution |
| ChatGPT | `docs/CHATGPT_ASSIGN_CH10_NAMING_2026-08-22.md` | Now (parallel Ch10) |
| ChatGPT | `docs/CHATGPT_ASSIGN_COLLECTION_SCHEMA_2026-08-22.md` | Now (unblocks row #8) |
| Claude | `docs/CLAUDE_ASSIGN_BIBLE_DIFF_2026-08-22.md` | Now (consolidation audit) |

Done — do not re-paste: shop/burn bible assign, spell catalog assign, spell Claude review.

---

## 5. Decision flow (locked)

```
Owner direction
    → Cursor CC: benchmark + loophole + sim
    → OWNER_REVIEW_LOG + master plan §N
    → ChatGPT/Claude packets only where code blocked on theory
    → Working Hands implements only rows marked executable in CC
```

**Revert:** `Revert shop` | `Revert bolt` | `Revert seat` (escalate to human)

---

## 6. After 2026-09-01

Reopen this file when VS Code seat returns; default split reverts to `AGENTS.md` battle vs metagame unless owner updates this chart.

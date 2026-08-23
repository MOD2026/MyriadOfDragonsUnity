# Zihan — one page only

**Updated:** 2026-08-23 (evening refresh) — **CC = Claude** · **Coding seat = separate Claude room**
(`myriadofdragonsunity-a1` — sessions have restarted under new names before, check `ListAgents` if
unreachable) · **Working Hands = Cursor**.

---

## Golden rule

Ship playable ASAP. Firm assigns only. No Ch1-2 Soft spiral. No idle theater.

---

## Canonical docs (do not add a 4th "master" file — edit one of these)

| Role | File |
|---|---|
| Live status / board | `docs/ZIHAN_COMMAND_CENTRE.md` (this file) |
| Master plan | `docs/SINGLE_BIBLE_MASTER_PLAN_2026-08-22.md` |
| Constitution | `docs/MOS_v1.1.md` + `docs/CORE_SYSTEMS_CONSTITUTION.md` |
| Decision history | `docs/OWNER_REVIEW_LOG.md` (long — this board is the summary) |

## Real baseline: 717/761, Block AA + AB both accepted

Root cause of the day-long stall mystery: a corrupted `Library/` Asset Database artifact, not
`SaveSystem` or any product code — found via Process Monitor trace, fixed. Standard batch-run
command is now `powershell -File tools/run_editmode_tests.ps1` (timeout-guarded; supports
`-ClassListFile` batched-per-class mode, the reliable way to get a real count on this project).
Remaining 44 failures: all traced test-by-test to the known parked AF/AI-on combat-balance category
or pre-existing gaps confirmed against this morning's original baseline — no unexplained regressions.

## Board

| Seat | Status |
|---|---|
| **Claude (this room)** | Command Centre |
| **Claude (coding room)** | Holding — CloudCode track paused (see below); last assign was warm-cache batched verification |
| **Cursor (WH)** | Block AC continuing — metagame UI push, currently mid-DeckBuilderPresenter.cs fix |
| **ChatGPT** | Purchase-package portfolio v2 in progress (rotating offers, subscription, bundle-math redo — first pass had real genre-pattern gaps) |
| **You** | Nothing blocking right now |

## CloudCode/server track — PAUSED, explicitly not MVP work

Four modules authored + locally tested today (SocialSafety, PermitWeekKey, Guild Expedition,
Bazaar) — see `docs/CLOUDCODE_TRACK_STATUS_2026-08-23.md`. `MVP_PLAYABLE_GATE_v1.md` explicitly
excludes server/Permit-week-key work as a non-goal — this whole track was CC scope drift chasing
the trusted-server design question, not an MVP push. **Paused by owner decision 2026-08-23.**
Everything authored is committed, tested, and ready whenever this resumes — nothing lost by
stopping. Remaining before it could deploy: figure out the actual module-publish mechanism (no
`ugs` CLI in repo), 2 disposable test accounts, a Play Mode manual verification pass (none of which
any AI session can do alone).

## Standing instruction: new UI replaces old UI

HomeV3/new-UI work should retire old UI, not layer alongside it indefinitely. `ShopV1StubCatalog`
kept only for `PurchaseForTests` hooks is the acceptable exception (test-only, not player-facing).
Check periodically whether old UI is still reachable by a real player before assuming a new screen
means the old one is gone.

## Active — Block AC (Cursor)

Shop/Avatar/Empire/Collection/DeckBuilder UI. Recent: Permit-rate UI fix, Stamina ladder enforcement,
Novice Pack retirement, pity-counter display, hardcoded-constant audit, DeckBuilder click-safety fix
(root-caused by coding room: `Background` Image missing `raycastTarget = false`, DeckBuilderPresenter.cs
~line 262-264) — in progress. Does not touch `CardDatabase.cs`, Gate/Castle files, `PlayerEmpireData.cs`,
`Assets/Tests/Editor/*`, Bazaar, Chat/Social. No APK.

## Market/Bazaar, Guild Expedition, Raid Troops — design fully locked, all blocked on the same thing

Trusted-server/backend dependency — unassigned, no owner, no approach chosen. Not urgent (all
post-MVP). Raid Troops additionally needs a formal `Guild_Competition_Rewards_v1.md` §6 amendment
(owner sign-off required, MOS v1.2 doc) before implementation.

## Chat — foundation-only, not assigned

`Assets/Scripts/Social/` exists, design/review-only per `AI_CONTRIBUTING.md` §7.

## Economy corrections locked today

Ascension Permits 8/week→4/week, hoard 16→8 (code + tests both fixed, live drift caught and closed).
Campaign Gem recompute locked (772,551→6,504, milestone-weighted) — **not yet implemented**, belongs
to WH (`CampaignMapPresenter.cs` stage reward formulas, metagame-owned). Novice Pack retired from
live Shop grid (bypassed pity system). Stamina purchase ladder now enforced (was unlimited).

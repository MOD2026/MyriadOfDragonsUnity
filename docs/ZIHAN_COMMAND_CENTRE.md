# Zihan — one page only

**Updated:** 2026-08-23 (post-cleanup) — **CC = Claude** · **Coding seat = separate Claude room** ·
**Working Hands = Cursor** · see `docs/CC_HANDOVER_TO_CLAUDE_2026-08-23.md`

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
| Decision history | `docs/OWNER_REVIEW_LOG.md` |

Everything else is either a narrow single-topic packet with an explicit "Supersedes" line, or
belongs in `docs/_archive/`. `docs/MOS_SINGLE_BIBLE_v1.md` (a duplicate draft skeleton) was archived
2026-08-23.

## Repo hygiene (2026-08-23 cleanup)

- **First commit since Ch7 landed:** `a20249e` — checkpoint of everything accepted from Block M
  through the current metagame push (Gate, Castle Block AA, Empire construction, Collection/Shop
  backend, HomeV3 UI, Social foundation). There is now an actual revert point. **New rule: commit
  after every CC-accepted block, red baseline or not** — "wait until fully green" is what let 36+
  hours of work sit uncommitted.
- 104 stray `*_results.xml` / ad-hoc log files that had accumulated at repo root across blocks were
  deleted; `.gitignore` now excludes `/*.xml`, `/*.log`, and `.claude/settings.local.json` at the
  repo root so this can't silently recur.
- Stale duplicate project folder `G:\...\Game mech\_DELETE_MyriadOfDragonsUnity_20260806-155959\`
  — **flagged for you to delete**, blocked by the sandbox from a recursive delete outside this repo.
- Timeout-guarded test runner added: `tools/run_editmode_tests.ps1` (see below).

## Timeout guard for Unity batch runs (new standard)

`powershell -File tools/run_editmode_tests.ps1` replaces the bare `Unity.exe -batchmode ...`
invocation everywhere (`AI_CONTRIBUTING.md` §5, `CLAUDE.md`). Kills the run and exits 124 if the log
stalls for 2+ minutes or the whole run exceeds 25 minutes, instead of hanging silently — built after
a real run sat stuck for 50+ minutes on 2026-08-23 (root cause: suspected Windows file-lock/AV
contention inside `SaveSystem.Save`'s write sequence). **Needed from you:** add a Windows Defender
(or your AV) exclusion for `C:\Users\zihan\Downloads\MyriadOfDragonsUnity` and your Windows temp
folder — that's a security-setting change no AI seat can make. I'll give you the exact command if
you want it.

## Real baseline (2026-08-23, fresh full EditMode run — supersedes every prior per-block count)

**652 / 761 passing, 109 failed** as of the last completed run. Not 81/81, not the various "9/9" /
"8/8" per-block counts in `OWNER_REVIEW_LOG.md` — those were true in isolation, never re-verified
together. Block AB (coding seat) is mid-repair; the run that stalled and was killed today is being
re-attempted. Gate (`GateCampaignLaunchTests`, previously accepted "9/9") showed 5 failing in the
109 — nothing gets re-accepted without a fresh green full run, not a scoped one.

## Board

| Seat | Status |
|---|---|
| **Claude (this room)** | **Command Centre** — reads, assigns, accepts/rejects, does not edit `.cs` itself |
| **Claude (coding room)** | **Coding seat** — Block AB (full-suite baseline repair) in progress; first attempt stalled 50+ min, killed, re-running |
| **Cursor** | **Working Hands** — Block AC (Shop/Avatar/Empire/new-UI metagame push), parallel to AB |
| **ChatGPT** | Idle — Bazaar packet + genesis-liquidity follow-up both delivered and reviewed (below) |
| **You** | Needed: AV exclusion (above) + delete the stale Drive duplicate (above) |

---

## Active block — Block AB: full-suite baseline repair (coding seat)

Not Block AA. AA does not open until AB reports a green full run via `tools/run_editmode_tests.ps1`.
Scope: 28-cluster "expected enough real cards" Setup failures + triage of the other ~80 of the 109
failures. Files: test-support helpers in the affected suites + `CardDatabase.cs` if that's the root
cause. Frozen files, Save shape, `CampaignMapPresenter.cs`, and economy numbers are out of scope.

## Active block — Block AC: metagame push (Cursor, parallel to AB)

Shop, Avatar, Empire screens, new UI (HomeV3). Does not touch `CardDatabase.cs`, Gate/Castle files,
or any `Assets/Tests/Editor/*` file Block AB owns. Market/Bazaar and Chat remain explicitly excluded.

## Market/Bazaar — design fully LOCKED, still blocked on one thing

Full decision chain reviewed and accepted 2026-08-23 — see `docs/BAZAAR_PHASE1_CC_ACCEPT_2026-08-23.md`
for the consolidated lock. Catalogue, ledger, genesis mechanism (capped Treasury reverse auction),
and genesis numbers (Day 60 earliest, 500-cluster snapshot, 40,000 Credit ceiling, no-launch
fallback if the participation gate isn't met) are all locked. **The only remaining blocker is the
trusted-server/backend dependency — unassigned, no owner, no approach chosen.** That is what's
actually stopping code, not the numbers. Cursor's Block AC still excludes Bazaar/trading code.

## Chat — foundation-only, not assigned

`Assets/Scripts/Social/` exists (now committed) but is still design/review-only per
`AI_CONTRIBUTING.md` §7 pending a trusted-backend ownership decision. Not assigned to any seat.

## Cursor: Block AC. Stand by on APK, Gate/Castle files, any test file, Bazaar/Chat code.

## ChatGPT: idle. Next ask, if any, comes from CC.

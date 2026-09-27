# Document Authority and Status — 2026-09-16

This file resolves live-document ambiguity. Historical reports remain unchanged as historical records; they do not override the active rules below.

## Active authority order

1. Running production code and passing tests.
2. Owner-approved design decisions and frozen contracts.
3. `docs/MOS_v1.1.md` — current governing MOS until a newer MOS is explicitly approved.
4. `docs/AI_CONTRIBUTING.md` and the seat instruction files (`AGENTS.md`, `CLAUDE.md`, `GEMINI.md`).
5. `docs/CC8_ROOM_ROLES_AND_SHOWCASE_PLAN_2026-09-14.md` — current room routing and evidence protocol.
6. Current handover and gap documents under `handover/`.
7. Historical seat reports, archived plans, transcripts, and superseded decision sheets.

## MOS version status

- `docs/MOS_v1.1.md`: governing MOS referenced by the seat instruction files.
- `docs/MOS_v1.2.md`: draft working reference, **not governing until owner approval**. It must not silently supersede v1.1.
- `docs/_archive/`: historical material only.

## Release / beta status

The project is not entitled to a numeric beta percentage from the current evidence. Use:

> Current prototype; beta readiness not established.

Do not describe the project as beta-ready until the control board's gates are satisfied: clean pinned source, no unexplained failures, loaded-profile runtime captures for beta-reachable screens, UI sign-off, and release evidence.

## UI package status

External PNG/mockup packages are design references until a Unity implementation, focused tests, and real runtime evidence identify the same screen/state. A package existing on disk is not integration evidence.

## Economy / Event Ledger status

`EventLedgerActive` remains disabled. Staging fixtures, DTOs, and seams are inert and do not authorize production rewards, balances, claims, Cloud Code deployment, or Save changes.

## Unity concurrency status

The two-slot validation pilot is operational. Separate project paths may run concurrently through the project-scoped runner. Same-project runs still serialize. Missing results XML or zero executed tests is blocked, never passed.

## Historical-document rule

Older files may contain statements such as “open,” “planned,” “complete,” or different test baselines because they record the state at their own timestamp. Those statements are not current status unless this document, the current control board, or a newer owner-approved decision explicitly carries them forward.

## Current reality snapshot

- Battle/tutorial code has substantial focused coverage, but the full UI replacement and runtime evidence matrix is incomplete.
- AE has a real cinematic teardown failure pending correction.
- Battle has 36 spell definitions but not 36 individually generated and runtime-accepted animations.
- Several external UI correction packages still lack code integration and/or runtime proof.
- rc31 remains frozen; rc32 remains a candidate, not a release approval.

## CC10 current authority overlay — 2026-09-27

The following CC10 documents supersede the older CC10 product-direction status
for current beta scope. The older direction document remains historical
provenance and must not be used to report CC10 as deferred:

- `CC10_BETA_FULL_SCOPE_IMPLEMENTATION_CONTRACT_2026-09-27.md` — authoritative
  full-beta system scope and implementation contract, supplied by BS.
- `CC10_FRONTIER_RUNTIME_IMPLEMENTATION_HANDOFF_2026-09-27.md` — runtime visual
  source-path and presentation handoff, supplied through the UI/UX/AD lane.

The external handoff files are currently stored outside this repository under
the owner-controlled Codex output folders. Until their text is copied into a
tracked authority packet, this section is the repository pointer and the
current-status override for CC10. The older
`docs/CC10_PVE_PERSISTENT_FRONTIER_PRODUCT_DIRECTION_2026-09-27.md` remains a
historical proposal record; its Phase 2/3 deferrals and
`NOT APPROVED FOR IMPLEMENTATION` status are superseded by the full-beta
contract and must not be used to close or defer a CC10 implementation task.

### CC10 system-completeness rule

All nine CC10 systems are beta scope: card leveling, Tavern/missions,
world-map exploration, NPC hotspots, guild territory, individual/guild
research, individual/guild rankings, minigame, and cargo missions. A system is
not complete merely because its room is idle, its branch is clean, or a design
packet exists. “No more task” is permitted only when the system has all of:

1. mechanics implementation;
2. authoritative service/persistence implementation;
3. runtime UI integration;
4. approved runtime art handoff;
5. focused tests and simulation evidence;
6. integration handoff with commit/path and unresolved blockers recorded.

### CC10 room ownership overlay

For CC10, use this ownership map instead of the older CC8 snapshot where they
conflict:

| Room | CC10 responsibility |
|---|---|
| BS | Mechanics and policy contract; no Unity implementation |
| AD | Art provenance and visual acceptance; no Unity implementation |
| UI/UX | Runtime-ready visual assets and presentation handoff |
| BE | Cloud Code/Cloud Save, UTC authority, receipts, idempotency, settlement and concurrency |
| CR | Battle-owned hooks and Battle integration only |
| WH | Metagame/minigame integration and tests |
| GUI | Unity presenters, navigation, controls and state rendering |
| MS | In-engine economy/progression simulation and balance tests |
| FR/LK | Integration, build and release evidence only |
| VE/AN | Runtime evidence and acceptance |
| MT | Marketing only; no game implementation |

No room may be declared complete from a status report alone. Every assignment
must name the next artifact, owner, allowed scope, acceptance evidence and
unresolved dependencies. Existing rooms receive paste-ready prompts; no new
room, branch or worktree is created unless the owner explicitly requests it.

## Release-candidate document decision (E resolved under GR1/GR2)

Keep both LK reports, but do not treat them as competing descriptions of one release:

- `LK-RELEASE-051-rc31-FROZEN.md` is the current release baseline. It describes the
  immutable, tagged rc31 candidate and its completed gate. Use rc31 when a current
  frozen reference is required.
- `LK-RELEASE-059-rc32-GATE-EVIDENCE.md` is the next-candidate record. It describes
  newer content with a clean build/test result, but it is explicitly not tagged,
  frozen, or runtime-capture-approved. Use it only for rc32 progression work.

The reports must remain separate because they refer to different commits, builds,
runtime hashes, and acceptance states. Do not merge their numbers or delete either
report: rc31 is provenance for the frozen baseline, while rc32 is the evidence needed
to decide whether a later freeze is worthwhile. Under GR1, rc32 can replace rc31 only
after its missing tag and runtime evidence gates are satisfied. Under GR2, no duplicate
re-run or document rewrite is needed while those states remain unchanged.

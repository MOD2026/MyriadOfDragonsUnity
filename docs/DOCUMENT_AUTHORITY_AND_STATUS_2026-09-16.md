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

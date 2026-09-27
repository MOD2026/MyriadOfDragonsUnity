# CC8 Room Roles and Showcase Execution Plan — 2026-09-14

Status: CURRENT CC8 routing authority — 2026-09-14.

This document supersedes the room-routing, seat-role, dispatch, and blocker-protocol portions of:

- `docs/AI_SEAT_CHART_LOCKED.md`
- `docs/SEAT_ASSIGNMENT.md`
- `docs/ZIHAN_COMMAND_CENTRE.md`
- `docs/COMMAND_CENTRE_QUEUE_2026-08-21.md`

Those files remain historical project records. If they conflict with this document about room identity, room purpose, dispatch routing, or blocker handling, this document wins. Document-version and current-status reconciliation is defined in `docs/DOCUMENT_AUTHORITY_AND_STATUS_2026-09-16.md`. This plan does not override `MOS_v1.1.md`, `AI_CONTRIBUTING.md`, frozen contracts/files, locked design decisions, or an explicit owner approval. Those remain higher authority.

## Authority and operating vocabulary

- **Room name = role.** When Zihan types a room name without a new assignment, that room is idling; do not invent work for it.
- **A room is active only after a concrete artifact is assigned.** The assignment must name the owner, files/assets or evidence, acceptance condition, and prohibited scope.
- **Direct Codex rooms** may be dispatched directly through the app. **Claude rooms** receive a copy-ready prompt for Zihan to paste; CC8 must not claim that a prompt was delivered when it was only prepared.
- **A report is not completion by itself.** Completion requires the requested artifact, exact branch/worktree or output path, real test/evidence numbers where applicable, and unresolved blockers.
- **No duplicate dispatch.** Do not resend a task or rerun a build unless new evidence changes the decision.
- **No fabricated evidence.** Dummy users/fixtures are allowed only for isolated non-production tests and must be labeled synthetic; they never clear release, native-capture, economy, or production-deployment gates.
- **No blanket cleanup.** Large transcripts, Codex worktrees, Unity `Library` folders, RAV findings, and generated artifacts are not deletion targets by implication. Cleanup requires an explicit inventory, exact paths, ownership check, and owner-approved scope.

## Golden routing rules

1. A room name is a role, not permission to edit arbitrary files. Route by the real implementation surface and the room's purpose.
2. UI/UX produces visual assets, layout mockups, PNGs, before/after compositions, and asset specifications. UI/UX does not implement Unity code.
3. AE owns Unity-native C# animation implementation and animation runtime orchestration. AE does not invent mechanics or alter Save/contracts.
4. CR owns Battle implementation and Battle-owned code surfaces. MS cannot edit Battle files merely because a task is described as economy.
5. WH owns assigned metagame/minigame integration and test implementation in its isolated worktrees. WH does not enable production events without BE and owner approval.
6. BE owns server/Cloud Code/deployment contracts and staging deployment preparation. BE does not enable production rewards or use real accounts without explicit authority.
7. AD reviews art provenance, approved asset reuse, composition, accessibility presentation, and visual acceptance. AD does not silently commission or substitute art.
8. BS handles open-ended mechanics, policy, and systems brainstorming. BS recommendations are not authority until the owner records approval.
9. VE produces native runtime evidence and measurements. AN accepts evidence only after the required VE packet is complete.
10. ST owns narrative, tutorial copy, timing, and player-facing instructional content. ST does not change mechanics.
11. GUI integrates approved UI/UX outputs into Unity UI code after the visual brief exists. GUI does not invent a new visual direction.
12. MS produces metagame/economy proposals and balance/reconciliation analysis. It may implement only files explicitly assigned to it; Battle-owned files remain with CR.
13. MT is marketing-only. It does not receive coding, deployment, test-fixture, or game-development tasks.

14. UI/UX is an **art/design-production seat**, not a code-review or defect-audit seat. Its deliverable is visible PNG/mockup/layout work for the game: before/after compositions, spacing corrections, overlap removal, safe-area geometry, and asset specifications. A UI/UX task is not valid unless it requests an actual visual artifact or a concrete asset handoff.
15. The UI/UX reference/document conversation currently visible in Codex is not evidence of an active asset-production room. Do not route work there unless its room identity and output location are explicitly verified. Until then, prepare a copy-ready prompt for the actual UI/UX room or ask Zihan for that room's identity.

## Blocker protocol

- A role-owned implementation blocker is assigned to that role immediately.
- An open-ended design/policy question goes to BS and AD for a recommendation packet.
- An art blocker requires actual art/PNG evidence or an exact approved asset path; prose alone does not clear it.
- No generic audit, confirmation-only, or invented-data task is dispatched unless it is an essential acceptance gate.
- Synthetic users and fixtures are allowed only in isolated non-production tests. They are never release evidence or production accounts.
- Tablet-only compression issues remain excluded from the beta standing order.

### Operational-stability blocker

The reported large `~/.codex` transcripts and RAV scans of Codex worktrees are treated as an operational-risk report, not as a project fact or deletion order. Until independently verified:

- keep Unity fully closed before native capture, scans, or cleanup-sensitive operations;
- use isolated worktrees for implementation and evidence;
- do not delete/archive active conversations, worktrees, `Library`, `Temp`, or RAV-flagged files from CC8;
- preserve the exact process IDs, paths, timestamps, and logs if a hang recurs;
- pause only the affected lane, not the whole project, and resume after the process/lock state is clear.

This separates app-health troubleshooting from game-development authority. It must not be used to justify deleting protected worktrees, modifying rc31, or accepting incomplete evidence.

## Current room map

| Room | Primary purpose | Current actionable lane |
|---|---|---|
| CC8 | Command, routing, acceptance, records | Keep the board, role map, dependencies, and owner decisions coherent |
| CR | Battle code and integration | Review `cr/battle-deckcurve-sharedenergy-integration` at `4fa567e3`; no rc31 merge or freeze without owner review |
| WH | Minigame/metagame implementation and tests | Review `wh/minigame-review-v1` at `7d186ef6`; keep EventLedger disabled |
| AE | Unity-native animation | Produce runtime evidence for `d9394f66`: normal, skip, Reduced Motion, replay/return |
| UI/UX | PNG/mockup/visual asset production | Create Battle/tutorial before-after mockups for overlap and unused space; do not perform a text-only audit |
| GUI | UI code integration | Wait for approved UI/UX PNGs and geometry brief; then integrate smallest scoped fix |
| VE | Native runtime capture/evidence | Capture AE and later UI/UX-integrated builds at verified 1920x1080 |
| AN | Acceptance/validation | Wait for complete VE evidence; do not accept from code inspection |
| BS | Open-ended systems/policy recommendations | Produce final Event Ledger sign-off table; do not authorize code |
| AD | Art/visual acceptance | Verify approved assets, manifests, safe area, Reduced Motion, and composition |
| BE | Cloud Code/server/deployment | Prepare non-production EventLedger contract; keep `EventLedgerActive=false` |
| MS | Economy/metagame proposals | Resolve existing EventMedal balance policy; no Battle-file edits |
| ST | Narrative/tutorial copy and timing | Maintain tutorial copy/timing handoff; update only when an actual copy decision exists |
| MT | Marketing | No game-development assignment |
| LK | Release gate | Wait for rc32 full-suite and player-build evidence |
| VS / FR | VIP / Friends | No task until a concrete defect exists |

## Showcase-first execution plan

### Gate 1 — Battle/tutorial visual slice

1. UI/UX creates actual 1920x1080 Battle/tutorial mockups and PNG assets resolving the top overlap and unused-space defects.
2. AD verifies asset provenance and visual differentiation.
3. GUI integrates only the approved visual geometry into an isolated worktree.
4. AE completes the opening animation runtime evidence on its isolated branch.
5. CR verifies Battle contracts remain unchanged.
6. VE captures native runtime evidence.
7. AN accepts only the complete evidence packet.

### Gate 2 — Playable local showcase

1. CR's integrated Battle branch remains separate from rc31 and is reviewed for merge.
2. WH's six authored Tactical Puzzles and real daily-seed chain remain available in local/non-production builds.
3. EventLedger stays disabled; the showcase must not imply production event rewards.
4. ST supplies the approved opening/tutorial copy and timing.
5. VE captures a clean showcase build only after UI/UX and AE evidence exists.

### Gate 3 — Production event readiness (not required for the first showcase)

1. Owner approvals for expiry, spend grace, cap clamp, shop allowlist, rollover, and existing balances are recorded.
2. BE deploys and tests a trusted server-authoritative ledger in staging.
3. Idempotency, concurrency, audit, expiry, balance, and allowlist tests pass.
4. GUI builds event-shop states only after Product/owner messaging approval.
5. `EventLedgerActive` remains false until the complete approval and evidence gate passes.

## Current non-blocking facts

- Frozen rc31 remains the only release candidate and must not be changed.
- The Battle/deck/Shared Energy integration branch has focused 130/130 green and the known 8 baseline full-suite failures.
- The minigame review branch has 90/90 focused coverage and no production event authority.
- AE has 13/13 focused animation tests; runtime visual evidence is still required.
- Event Ledger policy is reconciled as a recommendation but still requires written owner approvals and a trusted backend.

## CC8 acceptance rule

No room is considered productive merely because it is active. A room is complete when it returns the requested artifact, real test/evidence numbers where applicable, exact branch/worktree, and blockers. A room is held when its prerequisite is genuinely absent; holding is not a reason to invent work or reroute the task to an unrelated role.

## Current inconsistency resolution

The previous UI/UX misrouting came from three separate errors: stale seat documents, an unverified room identity, and a code-oriented prompt sent to a visual-production role. The corrected rule is now explicit: UI/UX produces visible game-design assets; GUI integrates them; AE animates approved assets in Unity C#; VE captures runtime evidence; AN accepts only the complete packet. If the correct UI/UX asset room is unavailable, the lane is blocked and the owner receives a copy-ready prompt — a reference chat is never treated as the asset room.

# Markdown Cleanup Conflict Map — 2026-09-16

## Inventory

- Main checkout: 128 tracked Markdown files.
- Main checkout: 197 untracked Markdown files.
- Main checkout: 10 modified Markdown files.
- Main checkout: no exact duplicate Markdown groups.
- Peer `.worktrees`: 127 exact duplicate groups, intentionally excluded from cleanup because they belong to separate worktrees.

## Why broad cleanup is still blocked

The main checkout contains untracked material with active-looking names. It is not safe to delete or merge these by filename because ownership and provenance are unclear. The modified files also include shared coordination records that may contain peer updates.

## Conflicting or ambiguous active documents

1. `docs/MOS_v1.1.md` vs `docs/MOS_v1.2.md` — **resolved (A1):** v1.1 governs; v1.2 remains a draft.
2. `docs/BATTLE-REMAINING-OWNER-DECISIONS-0.8.md` vs `docs/BATTLE-REMAINING-OWNER-DECISIONS-0.9-SIGNED.md` — **resolved:** 0.9 is the signed successor; 0.8 is history.
3. `docs/CC_CO_CONTROL_BOARD.md` vs newer handover/gap reports — **resolved (D1):** the board is historical; the authority/status document is current.
4. `docs/CC8_ROOM_ROLES_AND_SHOWCASE_PLAN-2026-09-14.md` and current room prompts — **resolved (C1):** CC8 is the current routing/protocol source; older prompts are historical.
5. `tools/seat_reports/LK-RELEASE-051-rc31-FROZEN.md` vs `tools/seat_reports/LK-RELEASE-059-rc32-GATE-EVIDENCE.md` — resolved under GR1/GR2: keep both, use rc31 as the current frozen baseline, and use rc32 only as the next-candidate progression record. They must not be merged into one release status.
6. Battle progression packets 0.2 through 0.9 — **not mergeable:** drafts, owner-review sheets, signed decisions, and implementation reports must remain partitioned.
7. `tools/seat_reports/` — 143 untracked reports are append-only evidence candidates, not cleanup duplicates.
8. `node_modules/` Markdown — generated dependency documentation, not project documentation; safe cleanup requires explicit approval to remove the dependency tree.

## Safe actions already completed

- Added `docs/DOCUMENT_AUTHORITY_AND_STATUS_2026-09-16.md` as the active reconciliation point.
- Updated the active seat instructions to point at that authority/status document.
- Did not delete peer worktrees, untracked reports, node_modules, or modified coordination files.

## Protocol now in force

- Use the authority order in `docs/DOCUMENT_AUTHORITY_AND_STATUS_2026-09-16.md`.
- Use the blocker protocol for blocked gates; no extra signature is required (B).
- Treat CC8 as the current routing/protocol source (C1).
- Treat the control board as historical unless its status is carried into the authority/status document (D1).
- Keep rc31 and rc32 separate: rc31 is the frozen baseline; rc32 is the untagged next candidate.
- Do not delete untracked reports, peer worktrees, `node_modules`, or modified coordination files by filename.

## Remaining owner-review items

1. “Other” documents still need classification: marketing material, imported handovers, generated outputs, and old seat reports are mixed together.
2. Battle progression packets need individual classification as signed authority, implementation evidence, or historical draft.
3. The 143 untracked seat reports remain append-only evidence candidates; no deletion decision has been made for them.

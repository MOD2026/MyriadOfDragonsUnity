# Markdown Cleanup Decision Index — 2026-09-16

This is the short operational index for the Markdown cleanup. It prevents old reports
from being mistaken for live instructions without deleting evidence.

## One source of truth

Use `docs/DOCUMENT_AUTHORITY_AND_STATUS_2026-09-16.md` for current status, authority,
routing, release-candidate state, and blocker protocol. All other Markdown is either
supporting evidence, historical record, generated material, or external material.

## Classification rules

| Category | Meaning | Action |
|---|---|---|
| Current | Governing rules, signed decisions, current routing, or active handover | Keep and link from the authority document |
| Historical | Old decisions, old room prompts, superseded reports, prior candidates | Keep as evidence; never treat as current instructions |
| Evidence | Test logs, acceptance reports, capture notes, build/release records | Keep with its candidate or handover; do not merge across candidates |
| Generated/external | Marketing files, imported documents, generated outputs, dependency docs | Keep outside the active docs set; delete only after an explicit path-level review |
| Peer-owned | Worktrees and modified shared coordination files | Do not move, edit, or delete |

## Decisions already made

- MOS v1.1 governs; MOS v1.2 is draft.
- The signed Battle decision packet supersedes earlier drafts.
- CC8 is the active routing/protocol document.
- The control board is historical unless its status is copied into the authority file.
- rc31 is the frozen release baseline; rc32 is the next untagged candidate.
- Blocked gates use the blocker protocol; no extra signature is required.
- The 12 obsolete archive files already removed are the only deletion batch approved so far.

## What remains, with the solution

1. Battle packets: retain all versions, label the signed packet as current, and label
   earlier packets as historical. Do not merge them.
2. Seat reports: retain them as append-only evidence and maintain a small index of
   current, blocked, superseded, and historical reports. Do not bulk-delete.
3. Marketing/imported/generated material: keep it outside the active authority set;
   review by directory, not filename, before any deletion.
4. Peer worktrees and modified coordination files: leave untouched.

## Completion rule

The Markdown set is considered clean when a reader can answer “what governs now?” from
the authority document alone, and every other file is clearly classified by this index.
Old evidence does not need to be deleted to be non-authoritative.


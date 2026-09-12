# Revamp V2 Release-Integration Authority — 2026-09-12

## Decision

Owner Zihan (Game Director) directly approved the named Revamp V2 UI items for implementation and release-lane integration. No separate signature form is required. Scope remains limited to Building Detail, Avatar/Profile, Friend Requests, Loading Sigil v3, and PackOpen overlay/post-purchase entry point. Save-schema changes, new gameplay mechanics, invented rewards, and rejected or conditional art remain excluded.

## Commits eligible for LK evaluation

These previously held commits may proceed to LK review, subject to each owner’s ordinary tests, captures, and clean file-ownership evidence:

- CR: `a0d1cbba...` and `c0c16f8c...` (signed progression/selection work; verify boundaries).
- VS: `6eaa21a0eedd98702b40f225ee7d75aaca7ef5f8` plus `a4a15e21b9af144ed0cf54d639a54db27e021b14` (paired startup/approved asset move).
- MS: `e411cd64f7f3c960c5b7b7ee42ccc485f83b023e` (Home/Collection/DeckBuilder overlap pass; conditional on LK tests).
- FR: `993dbb05996a5c1c8966c4a39c45d1ddb5e4bd4d` (docs-only handoff).

The commits listed in prior audits as conflicting, scope-expanding, deployment-coupled, or lacking required gates remain ineligible until their stated blockers are resolved. This note changes authority status only; it does not certify runtime behavior or replace LK evidence review.

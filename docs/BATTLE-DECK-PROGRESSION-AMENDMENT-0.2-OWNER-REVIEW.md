# Battle / Deck Progression Amendment v0.2 — Owner Review

Status: PROPOSED; MOS v1.1 and existing save contracts remain authoritative.

## Candidate non-linear milestones (proposal, not locked)

| Barracks level | Total cards | Formation | Reserve | Hotkeys |
|---:|---:|---:|---:|---:|
| 1 | 7 | up to 7 | 0–1 | up to 1 |
| 3 | 8 | up to 8 | 0–2 | up to 2 |
| 5 | 9 | up to 9 | 0 | up to 3 |
| 8 | 10 | up to 9 | 1 | up to 1 |
| 10 | 11 | up to 9 | 2 | up to 2 |
| 15 | 12 | up to 9 | 3 | up to 3 |
| 20 | 13 | up to 9 | 4 | up to 3 |
| 30 | 15 | up to 9 | 6 | up to 3 |

These levels intentionally soften early cliffs and make the 15-card cap coincide with the existing Barracks cap. MS/owner must approve or replace every row; the table is not an implementation instruction.

## Card, reserve, and duplicate rules

Cards come from one deck/draw pool. Formation deployment removes cards from the available pool; the remaining cards form the reserve. The player selects up to three eligible reserve instances as hotkeys. Fewer than three means fewer entries, with no automatic replacement. Duplicate instances are allowed exactly as supplied by the deck source; duplicate filtering, rarity quotas, and role quotas are OWNER/MS decisions.

## Combat economy and replay

Option C is recorded as owner-approved: Resource is formation-only; spells and reinforcement share Energy. Reinforcement Energy cost equals the card’s ResourceCost; existing Energy cap/regeneration remain initially. Reinforcement is optional at ticks 4/8. Legal lanes are selected uniformly from the legal set with one draw from a separate reinforcement RNG stream. The authoritative local match seed, stream derivation, draw ordinal, and replay storage format require CR/owner approval. No rerolls. Invalid card, closed window, insufficient Energy, no legal lane, or controller rejection is a strict no-op.

## Migration / rollback

Existing saves with Barracks capacity 16/18/20 cannot be changed by this document. Zihan must choose: preserve current capacity, clamp to 15 with compensation, or defer the new curve. Any chosen path needs versioned migration, catch-up protection, and a reversible rollback window. No save-schema edit is authorized here.

## Decision ownership

- Zihan (owner): approve curve rows, 15-cap timing, migration/rollback, duplicate policy, and whether the proposal is beta or post-beta.
- MS: approve deck-depth economy, reserve diversity, catch-up costs, and anti-dominance evidence.
- CR: specify local match seed, separate RNG stream, replay semantics, and acceptance tests.
- BS/AD: recommend player-truthful copy, touch/clarity, and no-op behavior; do not sign economy values.
- VE: provide native captures only after the above signatures.

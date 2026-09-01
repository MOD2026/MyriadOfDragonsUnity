# CC6 → CR7 Battle Handover

**Date:** 2026-09-02
**Scope:** owner-approved Battle economy and deck-progression redesign

## Locked owner direction

- Replace the current deck-cap direction with a planned, non-linear progression from approximately 7 cards to a maximum of 15.
- Barracks remains the deck-capacity owner unless the owner explicitly changes that decision.
- End-state target: 9 player formation cards plus up to 6 reserve cards.
- Player selects 3 reserve cards as reinforcement hotkeys.
- Reinforcement is optional and executes with one tap at combat ticks 4 and 8.
- Reinforcement uses Energy; Resource is used for formation only.
- Lane selection is uniform seeded-random among legal lanes, with no rerolls.
- Insufficient Energy, closed window, invalid card, or no legal lane is a strict no-op.
- Existing one-tap random spell targeting remains unchanged; Reposition remains explicit.
- No sacrifice, replacement, END TURN, or AUTO BATTLE mechanic is authorized.

## CR7 implementation tasks

1. Read `docs/MOS_v1.1.md` and `docs/AI_CONTRIBUTING.md` before editing.
2. Publish a versioned mechanics/spec amendment before changing the live rule: old Barracks 10–20 behavior, new 7→15 rule, rationale, affected systems, and save/data migration. MOS remains the higher authority until that amendment is recorded.
3. Define the non-linear 7→15 milestone table with MS/owner; do not use a linear 7,8,9…15 curve without approval.
4. Define reserve generation from the progression-derived deck and the behavior when fewer than 3 eligible cards exist.
5. Define duplicate-card handling and prevent reserve/hotkey rules from creating attack-card, cheap-card, or synergy dominance.
6. Implement one-tap reinforcement using the real Battle path, with Energy cost initially mapped from `Card.ResourceCost` unless the signed balance table changes it.
7. Use a deterministic match seed and separate RNG stream for reinforcement lane selection; document replay behavior and no-reroll semantics.
8. Preserve atomic failure behavior: no card removal, deployment, or Energy mutation on failure.
9. Reconcile `DeckSlotCount` migration for existing profiles; do not edit frozen save files without human coordination.
10. Add EditMode tests for milestone/reserve behavior, 3-hotkey selection, one-tap deployment, uniform seeded legal-lane selection, failure no-ops, Energy settlement, and existing spell/Reposition behavior.
11. Run the required Battle baseline before and after logic changes with Unity fully closed; report real totals and compiler markers.

## Non-negotiables

- Do not implement against an unapproved UI image. UI must first show the owner-approved three-hotkey one-tap Battle reference, and Zihan must approve the art.
- UI-098 (`battle_faithful_sample_replacement_v8.png`) is the latest conditional reference only; it is not approved production art.
- Do not invent a six-card reserve at progression levels where the deck does not contain six reserve cards.
- Do not silently preserve the old 10–20 progression while claiming the 7→15 direction is implemented.
- Do not edit `PlayerProfile.cs`, `SaveSystem.cs`, `SaveMigration.cs`, `Data/SaveManager.cs`, or frozen tests without human coordination.
- Preserve the existing `MatchResult`, `OnMatchCompleted`, `GameBootstrap.Instance`, and `SetBattleCanvasVisible(bool)` contracts.
- MT is not involved.

## Existing Battle work to preserve

- One-tap random spell targeting and fixture coverage: `cd626ff` → `9013d90` → `2be12b6`.
- Spell feedback wiring: `419f168`.
- Fixture/interruption coverage: `e3595cb`.
- Avatar/Combat FX evidence seams: `99cb0f9`.

**Handover status:** direction approved by owner; detailed progression, reserve, migration, and balance implementation remains open for CR7/MS execution. No production code changed by this document.

# Battle Remaining Owner Decisions v0.9 — SIGNED

Owner: Zihan (Game Director)  |  Decision date: 2026-09-02  
Status: OWNER-SIGNED; CR handoff ready within boundaries.

| Gate | Signed decision | Affected production authority | Save boundary |
|---|---|---|---|
| Deck curve | YES — non-linear 7→15: L1=7, L3=8, L5=9, L8=10, L10=11, L15=12, L20=13, L30=15. | `Assets/Scripts/Empire/PlayerEmpireData.cs`: `PaidBarracksMilestones`, `DeckSlotsAtPaidMilestone`, `MaxDeckSlotCount`, `DeckSlotsForBarracksLevel`. | Frozen `PlayerProfile`, `SaveSystem`, `SaveMigration`, and save tests are not edited. |
| Migration | YES — Option A: preserve existing saved 16/18/20 values outside Battle; cap newly constructed beta decks at 15. | CR/MS must apply the cap only to new beta construction/runtime selection; legacy values remain readable. | No schema migration, clamp, compensation, or destructive rewrite. |
| Hotkeys | YES — 4 player-selected left-side skill hotkeys + 3 player-selected right-side reinforcement hotkeys. | Battle/UI binding in `BattleController` and `GameBootstrap` only; runtime availability may show fewer reinforcement entries when eligible reserve is smaller. | Hotkey state is match/UI-scoped and not persisted. |

## Preserved signed contracts

Shared Energy remains approved (formation Resource; spells/reinforcement Energy; reinforcement cost equals card.ResourceCost). Replay fallback remains fail-closed with explicit lane selection only when `_reinforcementRng == null`. Global lane wording remains the owner-approved Front/Middle/Back lines. No replay or lane wording changes are made here.

## CR handoff and guardrails

CR may implement only the listed authorities and must leave Save files/schema untouched. Tests must prove all eight curve milestones, Option A legacy handling, 4+3 selection, fewer-than-three reinforcement entries, and unchanged replay/lane behavior. Any conflict with frozen Save contracts stops work and returns to Zihan; BS does not authorize exceptions.

Owner signature statement: `I, Zihan, approve the deck curve, Option A migration behavior, and 4+3 hotkey split above on 2026-09-02.`

## Implementation status clarification

The 7→15 curve is **OWNER-APPROVED DESIGN, NOT YET LIVE**. Current code still uses the old 10/11/12/14/16/18/20 curve until CR implements the approved change. Option A means existing saved 16/18/20 values remain readable and unchanged outside Battle; newly constructed beta decks are capped at 15. No Save schema change, migration, clamp, compensation, or destructive rewrite is permitted.

# CC10 — PvE-First Persistent Frontier Product Direction

**Origin:** Business Strategy (BS) proposal, recorded by Command Centre on 2026-09-27  
**Status:** PROPOSED / DESIGN REVIEW ONLY / NOT APPROVED FOR IMPLEMENTATION  
**Mechanics:** No new mechanics, currencies, save fields or multiplayer paths are authorized by this document.

## Direction recorded

Myriad of Dragons should evolve from a chapter-only card battler into a PvE-first persistent frontier while preserving the existing dark-fantasy identity and current Battle engine.

Proposed product loop:

> Build empire → accept Tavern mission → scout or attack NPC activity → run an expedition → improve cards and formation → later protect or intercept temporary cargo → join guild territory → mature into state conflict.

### Phase 1 — PvE foundation

Candidate vertical slice:

- Tavern mission hub;
- NPC-rich world-map activity;
- Titan-vein expeditions;
- empire project choices;
- existing Battle resolution;
- mission journal and persistent claim handling.

Candidate mission categories:

- faction contracts;
- NPC patrols;
- rescue missions;
- relic recovery;
- convoy protection;
- Titan-vein expeditions.

The proposal explicitly rejects a mining loop. World activity should use NPCs, patrols, ruins, caravans, anomalies, relics, bosses and faction activity.

### Phase 2 — Mercenary cargo bridge

Long-term candidate loop:

> Assign mercenaries → select expedition → choose escort formation → cargo travels → NPCs or, later, players may intercept → existing Battle resolves → cargo is delivered, intercepted or safely returned.

Permanent progression must remain protected. Only explicitly defined temporary cargo or mission haul may become exposed.

### Phase 3 — Persistent multiplayer

Deferred candidates:

- guild territories;
- persistent states;
- state-versus-state conflict;
- state consolidation/merging;
- seasonal war zones.

These are not Phase 1 requirements and must not be implemented from this proposal.

## Compatibility with approved authority

This direction is provisionally compatible with MOS v1.1 sections 6, 10–14 if the following remain true:

- Battle continues to own combat resolution and emits `MatchResult`.
- Metagame owns missions, campaign progression and reward presentation.
- Save changes are additive, migrated and tested.
- Existing Gold, Gems and Event Medals are reused until a separate economy decision is approved.
- No permanent card, avatar, building, identity or progression property becomes PvP loot.
- Guild, marketplace, cargo PvP and state systems remain deferred until their own specifications are approved.
- No mining loop or client-authoritative ownership path is introduced.

## BS clearance gaps — must be resolved before implementation

Business Strategy must provide an approved Phase 1 specification covering:

1. The smallest first playable slice and its success criteria.
2. Tavern mission data model, mission lifecycle and upgrade unlocks.
3. NPC map activity types, spawn/refresh rules and deterministic test fixtures.
4. Expedition states: accepted, scouting, active, failed, abandoned, completed and claimed.
5. Mission journal state model and idempotent claim behavior.
6. Empire project choices, costs, persistence and failure/cancellation rules.
7. Reward tables using existing currencies and existing reward contracts.
8. Offline/time manipulation policy and server-authority assumptions.
9. Bot/farm review for every new faucet and sink.
10. Player retention hypotheses and measurable validation metrics.
11. The exact onboarding route for the first Tavern mission.
12. Required EditMode, PlayMode, balance-simulation and human-playtest evidence.

For Phase 2, BS must additionally define temporary cargo as a Property or explicitly state that it is not transferable property, then specify target discovery, expiry, attack eligibility, delivery, interception, disconnect, timeout, duplicate-claim and rollback behavior.

## AD/UIUX clearance gaps — must be resolved before presentation implementation

Art Direction/UIUX must provide an approved, non-flattened presentation package covering:

1. Tavern mission-hub composition and mission-state treatments.
2. NPC world-map activity markers and their readable states.
3. Titan-vein expedition visuals that fit the approved mythic-arcanepunk direction.
4. Mission categories, faction markers and reward/package iconography.
5. Expedition progress, risk, completion, failure and claim states.
6. Empire project choice presentation without implying an unapproved system.
7. Mission-journal layout and mobile typography.
8. Approved runtime asset paths, dimensions, transparency and Reduced Motion variants.
9. Mobile readability checks at actual card, HUD and button sizes.
10. Clear separation between exploration artwork and runtime-approved assets.

AD must not imply cargo PvP, new currencies, corruption, Titan-surge combat modifiers or faction-specific combat rules before those mechanics are separately approved.

## Implementation gate

No room may implement this direction until all of the following exist:

- BS-approved Phase 1 specification;
- AD/UIUX-approved runtime presentation package;
- MOS impact note if any rule changes;
- Save/economy impact review;
- Battle contract review confirming `MatchResult` remains the boundary;
- focused tests and balance-simulation plan;
- human onboarding/playtest acceptance criteria.

## Current decision

**Accepted as a product direction for specification and validation.**  
**Not accepted for runtime implementation.**  
Phase 2 cargo PvP and Phase 3 persistent multiplayer remain explicitly gated.

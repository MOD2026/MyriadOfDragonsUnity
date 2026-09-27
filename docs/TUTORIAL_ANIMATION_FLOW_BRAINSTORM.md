# First-session tutorial animation flow brainstorm

Status: **V1 CONTRACT ALIGNED** — ST handoff `ST-TUTORIAL-ANIMATION-V1-HANDOFF-003` is owner-approved for presentation-only implementation. Timing, callbacks, skip/resume, replay boundaries, and Reduced Motion are resolved; no mechanics, economy, Save schema, or navigation changes are authorized.

| AD beat | Existing support | New code / art or audio | Contract check |
|---|---|---|---|
| Castle restoration payoff | **Deferred from V1.** Empire data exposes castle level (`Assets/Scripts/UI/AvatarPresenter.cs:117-170`) and story backgrounds exist, but no supported restoration sequence, first-run trigger, or canonical V1 art/state exists. | Not part of the V1 implementation. | No conflict: explicitly deferred; do not add costs, rewards, Save state, or new navigation. |
| Starter-card reveal | **Supported.** `StartApprovedTutorialBattle()` calls `GrantApprovedStarterCardsIfMissing()` (`Assets/Scripts/UI/GameBootstrap.cs:1109-1122`); a public test seam exists at line 2349. | A reveal animation/audio is presentation work; no gameplay code required if it wraps the existing grant. | Must preserve the existing starter-card IDs and idempotent grant; no new cards, currency, or reward values. |
| Formation teaching moment | **Supported.** The guided battle exposes placement and formation guidance; the tutorial data contains `lanes`, `picker`, `resource`, and `start` steps (`Assets/Resources/Data/Story/tutorial_steps.json`). | Timing/highlight animation can be added around current controls; no new mechanics. | Keep 3×3 formation, existing Resource rules, and placement-only Back copy where appropriate. Do not teach unapproved global bonuses or a fixed reserve. |
| First controlled battle | **Supported.** `StartApprovedTutorialBattle()` creates a fixed tutorial-only encounter and uses existing formation/combat flow (`Assets/Scripts/UI/GameBootstrap.cs:1109-1200`). Guided sequence tests are the authoritative behavior check. | Presentation beats may animate existing Start Battle, spell, and result hooks; no combat-resolution changes. | Preserve automatic tick combat, player-only spell input, existing tutorial opponent spell exclusion, and current rewards/Save guards. |
| Return to Empire payoff / next goal | **Partial.** Battle exposes `OnReturnToCityRequested` and `ReturnToCityForTests()` (`Assets/Scripts/UI/GameBootstrap.cs:721,6557-6561`); Home handles completion (`Assets/Scripts/UI/HomePagePresenter.cs:262`). | A post-result animation or caption is new presentation; no new reward or progression should be implied. | Return must use existing callback/navigation and existing reward path. Story README states persistence is not implemented, so replay/seen-state cannot be assumed. |

## Recommended smallest V1 package

Use existing flow only: (1) short, skippable starter-card reveal after the approved grant; (2) one highlighted formation instruction at a time using existing tutorial guidance; (3) a brief Start Battle handoff into the existing controlled encounter; and (4) a result/Return-to-Empire beat that reuses the current callback and shows only already-authoritative outcome text. Defer castle-restoration animation until an owner-approved art/state hook exists. Keep all timing presentation-owned, reduced-motion safe, and replayable without changing Save or rewards.

## Decisions resolved by the ST handoff

- V1 is the five-beat presentation-only sequence: opening, starter reveal, Formation, controlled battle, Return to Empire.
- Supplied opening plays once (8.04s); skip uses the same completion callback. Resume/replay must not duplicate grants or overlays.
- Reduced Motion uses static holds/dissolves while preserving state boundaries and callbacks.
- Castle restoration is explicitly deferred because no supported implementation/art exists.

## Remaining CR blocker

**None in the product contract.** CR may implement the bounded presentation seam against the existing Chapter 1 tutorial/Formation callbacks. Required evidence (playback, skip equivalence, callback ordering, resume/replay, Reduced Motion, and no duplicate grants) is an acceptance gate, not an unresolved design decision. Frame-sheet extraction remains an AD/VE evidence task and does not change the approved V1 scope.

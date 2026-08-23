# UI Animation Readiness Report

> **2026-08-11 guild motion addition:** Add restrained motion for league promotion/relegation,
> rank movement, milestone claims, contribution gain/store spend, donation recharge, help completion,
> research activation/completion and seven-day office appointment/expiry. Guild Champion may use a
> distinct ceremonial frame animation; functional rank must remain readable with motion disabled.
> Do not use casino-like effects for leaderboard placement or ordinary contribution gains.

Date: 2026-08-08

## Overview

This report reviews the current UI surfaces in the project and identifies where animation support will be required. The existing UI is functional and procedural, but it currently has no animated transitions, interaction feedback, or overlay motion. The intent here is to define animation readiness at the presentation layer without implementing any motion yet.

## General animation readiness assessment

The UI is broadly ready for animation in the following areas:

- Screen transitions between home, campaign, shop, Formation, collection, and battle
- Button interaction feedback for navigation, purchase, confirm, close, and action buttons
- Modal popup open/close behavior for detail panels, story overlays, tutorial panels, lane selection, and match results
- Stateful reveal animations for reward, purchase, save confirmation, and empty-state transitions
- Card-focused interactions such as zoom, selection, and detail reveal
- Notification and loading feedback for asynchronous or transition-heavy flows

## Frozen motion specification mapping

All motion values and rules below are mapped to the approved UI/UX Bible v0.5 Motion Specification and are the source of truth for timing/behavior in this report.

| Interaction class | Frozen timing | Rule source |
|---|---:|---|
| Button feedback | 80ms | UI/UX Bible v0.5 Motion Specification |
| Card selection | 150ms | UI/UX Bible v0.5 Motion Specification |
| Panel transition | 220ms | UI/UX Bible v0.5 Motion Specification |
| Battle impact | 250-350ms | UI/UX Bible v0.5 Motion Specification |

Motion behavior rules that must be used exactly from UI/UX Bible v0.5:

- approved easing rules
- interruption behavior
- skip behavior
- speed-control behavior

No custom timing alternatives are allowed in preparation or implementation documents.

## Screen-by-screen animation map

| Screen / surface | Required animation areas | Priority | Notes |
|---|---|---:|---|
| Home page | Screen enter, button press, button hover, tutorial dialogue reveal, bottom dock transitions, resource pill emphasis | High | Main navigation surface and first impression layer. |
| Campaign map | Screen transition in/out, stage node hover, stage node press, modal open/close, unlock reveal, battle launch transition | High | Strong candidate for polished state and route transitions. |
| Stage detail modal | Popup open, popup close, panel entry, launch button feedback, close button feedback | Medium | A standard modal flow with clear entry/exit behavior. |
| Shop | Screen transition, card tile hover/press, purchase confirmation feedback, resource value pulse, empty-state reveal | High | Commerce flow benefits from clear purchase feedback and card emphasis. |
| Formation | Panel transition, card add/remove animation, deck counter update emphasis, confirm/save feedback, empty-state reveal, scroll container motion | High | This screen has the most interaction-driven state changes. |
| Collection | Screen transition, search field focus, card tile hover/press, detail panel open/close, empty-state reveal, filter/sort placeholder feedback | High | Card browsing and detail selection will require strong presentation polish. |
| Battle screen | Screen enter, hand card hover/press, card selection, lane selection, spell targeting, card detail overlay open/close, result overlay open/close, tutorial overlay, resource bar changes, turn transition, damage/heal feedback, loading transition | Critical | Highest complexity and biggest animation payoff. |
| Card detail overlay | Overlay open/close, card zoom, stat panel reveal, action button press, dismiss animation | High | Strong visual focus for card inspection. |
| Lane picker overlay | Overlay open/close, lane button hover/press, selection confirmation, dismissal | Medium | Important for tactile turn-based interaction. |
| Match result overlay | Overlay open/close, reward reveal, score summary reveal, button emphasis, confetti/shine style effect | High | Reward presentation is a natural animation target. |
| Tutorial overlay | Overlay fade/slide, text reveal, portrait transition, arrow pointer motion, step-by-step progression | High | Tutorial flow is highly dependent on clear sequential motion. |
| Spell tooltip | Tooltip appear/disappear, pointer alignment, scale/pulse, dismissal | Medium | Lightweight but visible to the player. |
| Story overlay | Overlay open/close, vertical text reveal, portrait crossfade, narrative beat progression, button feedback | Medium | Narrative presentation benefits from smooth pacing. |

## Required animation categories

### 1. Screen transitions

These will be needed whenever the player moves between major UI surfaces:

- Home to campaign
- Home to shop
- Home to Formation
- Home to collection
- Home to battle
- Returning to home from any child surface
- Battle to result / reward flow

Motion timing and behavior for these transitions must use the frozen values/rules defined in the UI/UX Bible v0.5 Motion Specification.

### 2. Button interaction feedback

Button states are present across nearly every screen and should support:

- Hover highlight
- Press feedback
- Disabled-state feedback
- Success/confirmation feedback

Examples:
- Navigation buttons on the home page
- Back buttons on campaign, shop, Formation, and collection
- Purchase buttons in shop
- Confirm and recommended Formation buttons
- Battle action buttons

Button interactions use the frozen 80ms timing and approved easing/interruption/skip/speed-control rules from UI/UX Bible v0.5.

### 3. Popup and modal motion

Modal surfaces are already a strong part of the current UI and will need consistent motion behavior:

- Stage detail modal
- Collection detail panel
- Battle card detail overlay
- Lane picker overlay
- Match result overlay
- Tutorial overlay
- Story overlay

Popup and modal motion uses the frozen 220ms panel transition timing plus approved easing/interruption/skip/speed-control rules from UI/UX Bible v0.5.

### 4. Reward and state reveal

These are especially important in progression and commerce flows:

- Shop purchase success
- Deck save confirmation
- Match reward reveal
- Level-up or progression milestone feedback
- Currency increment pulses

State-reveal motion uses frozen timing classes from UI/UX Bible v0.5:

- button-level feedback: 80ms
- card-focused reveal: 150ms
- panel-level reveal: 220ms
- battle-impact reward emphasis: 250-350ms where battle-impact class applies

### 5. Card interaction animation

Card-based surfaces will require interaction polish:

- Card hover lift
- Card press feedback
- Card zoom preview
- Card flip or reveal transition where applicable
- Selection highlight for battle hand and collection detail flows

Card interactions use frozen timing classes from UI/UX Bible v0.5:

- selection and card emphasis: 150ms
- card detail panel open/close: 220ms
- any battle-impact card hit reaction: 250-350ms

### 6. Notification and feedback motion

The UI should support lightweight feedback for temporary state changes:

- Purchase denied or insufficient currency
- Deck full warning
- Search no-results state
- Battle action confirmations
- Tutorial step progression

Notification and feedback motion must use approved easing/interruption/skip/speed-control rules from UI/UX Bible v0.5 and must select timing from the frozen timing classes above.

### 7. Loading and transition handling

Some flows imply a loading or transition state even if full loading screens are not implemented yet:

- Battle launch from campaign or home
- Story sequence transition before battle
- Large collection or deck rebuild operations
- Any future asynchronous asset or data load

Loading and transition handling must use frozen timing classes and approved easing/interruption/skip/speed-control rules from UI/UX Bible v0.5.

## Highest-impact animation targets

The highest-value animation work should focus on these areas first:

1. Battle screen overlays and card interactions
2. Match result and reward presentation
3. Modal popup system for campaign, collection, and tutorial flows
4. Button feedback across all core navigation surfaces
5. Formation add/remove and save confirmation

## Phased implementation sequence

This sequence follows [docs/UI_Screen_Inventory_2026-08-08.md](docs/UI_Screen_Inventory_2026-08-08.md) priority tiers, avoids simultaneous redesign of all presenters, and keeps Battle for the proven stage.

### Phase 1 - Shared foundation proof on a lower-risk screen

- Validate shared animation hooks on one lower-risk metagame screen using frozen motion timings/rules.

### Phase 2 - Additional metagame screens

- Expand motion hooks to additional metagame surfaces after Phase 1 validation.

### Phase 3 - Collection and Formation card-heavy surfaces

- Apply card and panel motion classes to Collection and Formation using frozen timing classes.

### Phase 4 - Battle and overlays

- Apply motion to Battle and overlays only after shared behavior is proven on earlier phases due to regression sensitivity.

### Phase 5 - Final consistency and regression pass

- Verify cross-screen timing-class consistency and compliance with approved easing/interruption/skip/speed-control rules.

## Implementation readiness

The current UI structure is animation-ready in principle because the presenters already separate logical surfaces, overlays, and interactive states. However, the project does not yet have a reusable animation layer, so these motions would need to be introduced through a shared framework rather than ad hoc per presenter.

### Current state

- No animation system is implemented.
- No shared transition controller exists yet.
- No reusable animation hooks are defined for screens, windows, popups, or buttons.

### Readiness conclusion

The UI is ready for animation design and implementation planning, but it still needs a central animation architecture to avoid duplicated per-screen coroutine logic.

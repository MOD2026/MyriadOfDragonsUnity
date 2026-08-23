# UI Architecture Proposal v1

> **MOS v1.2 addition — 2026-08-11:** Guild/multiplayer UI is now Phase 1. Add a Guild presenter
> family only after trusted identity and guild state exist. UI may request actions and render
> returned state; it never calculates ranks, scores, contribution, timer reduction, office buffs,
> R4/R5 authority or rewards. Required surfaces are defined in
> `Guild_Competition_Rewards_v1.md` §7.

Date: 2026-08-08

## Scope

This proposal outlines an engineering-focused UI architecture aligned to the approved UI/UX Bible v0.5 and Brand Bible v0.3. It is designed for the current project structure, where UI is constructed procedurally at runtime from presenter scripts rather than through scene-prefab wiring. The focus is on architecture, composition, state flow, and reuse rather than visuals.

## Design goals

- Preserve the current procedural runtime UI approach.
- Separate screen lifecycle from UI construction and presentation.
- Reduce duplication across presenters and overlays.
- Introduce a predictable manager-based structure for navigation, windows, popups, theming, animation, and layout.
- Keep the system extensible for future screens without coupling each screen directly to Unity UI primitives.

## Proposed architecture overview

The framework should be organized around a small set of managers and a screen/window abstraction layer.

### Core layers

1. Screen layer
  - Represents a logical screen such as Home, Campaign, Shop, Formation, Collection, or Battle.
   - Owns screen-specific data binding and interaction behavior.
   - Does not directly manage low-level Unity UI object creation.

2. Presentation layer
   - Builds and updates UI views through shared managers and reusable components.
   - Receives lifecycle events from the screen layer.

3. Service layer
   - Includes managers for navigation, windows, popups, themes, animation, and responsive layout.
   - Centralizes cross-cutting behavior.

## 1. Screen Manager

### Responsibility

The Screen Manager owns the lifecycle of screens and coordinates transitions between them.

### Responsibilities

- Register available screens.
- Show/hide/switch screens.
- Maintain a screen stack for navigation history.
- Ensure only one active screen root is visible at a time where appropriate.
- Notify screens when they become active, inactive, paused, or dismissed.
- Handle screen teardown and cleanup.

### Proposed contract

- ShowScreen(screenId, context)
- CloseScreen(screenId)
- ReplaceScreen(screenId, context)
- PushScreen(screenId, context)
- PopScreen()
- OnScreenOpened(screen)
- OnScreenClosed(screen)

### Engineering notes

- This manager should be responsible for orchestration only.
- It should not own visual styling or layout specifics.
- It should work with screen presenters that expose a simple lifecycle interface.

## 2. Window Manager

### Responsibility

The Window Manager manages major full-screen or large framed surfaces such as campaign map views, collection views, Formation views, and battle UI surfaces.

### Responsibilities

- Create and manage window roots.
- Keep a registry of active windows.
- Support modal and non-modal windows.
- Provide window ordering and stacking.
- Support transitions such as enter/exit and focus changes.

### Proposed contract

- OpenWindow(windowId, context)
- CloseWindow(windowId)
- BringToFront(windowId)
- SetWindowEnabled(windowId, enabled)
- GetWindow(windowId)

### Engineering notes

- Window Manager should sit above the Screen Manager for larger surfaces.
- It should be useful for persistent or semi-persistent surfaces that need overlay semantics.
- It should not contain screen-specific logic; it should only manage window lifecycle and presentation state.

## 3. Popup Manager

### Responsibility

The Popup Manager handles temporary overlays such as dialogs, confirmations, detail panels, tutorial prompts, and result overlays.

### Responsibilities

- Open/close popup instances.
- Maintain popup stack order.
- Support modal dimming and focus lock.
- Route popup input to the topmost popup.
- Support popup result callbacks.

### Proposed contract

- ShowPopup(popupId, context, callback)
- DismissPopup(popupId)
- DismissTopPopup()
- SetPopupBlocking(bool)
- GetActivePopupCount()

### Engineering notes

- The Popup Manager should be generic enough to support both simple prompts and richer content panels.
- It should be able to host shared popup templates without tying them to a specific screen.
- It should support the same overlay lifecycle as the battle card detail and result panels.

## 4. Navigation Manager

### Responsibility

The Navigation Manager handles app navigation behavior between screens and modal surfaces.

### Responsibilities

- Define navigation routes or screen IDs.
- Resolve screen transitions from actions such as back, home, open collection, open shop, or launch battle.
- Preserve navigation history for back-stack behavior.
- Coordinate with the Screen Manager and Window Manager.

### Proposed contract

- NavigateTo(route, context)
- GoBack()
- RegisterRoute(routeName, screenId)
- CanGoBack()

### Engineering notes

- This manager should be a thin orchestration layer rather than embedded logic inside presenters.
- Presenters should raise navigation intents instead of directly instantiating other screens.
- This reduces direct coupling between screens and improves testability.

## 5. Theme Manager

### Responsibility

The Theme Manager centralizes reusable visual tokens and styling semantics for the UI framework.

### Responsibilities

- Store semantic theme values such as colors, spacing, radii, font sizes, and component states.
- Provide palette accessors for button, panel, header, modal, badge, and text variants.
- Support light/dark or alternate theme variants if needed.
- Expose theme values to layout and component factories.

### Frozen source-of-truth requirements

- Theme values are sourced from UI/UX Bible v0.5 and Brand Bible v0.3 and must not be redefined in architecture documents.
- Spacing system: 8dp grid.
- Minimum touch targets: 48dp (Android) and 44pt (iOS).
- Corner radii: 12dp and 10dp, applied by component role per the approved Bibles.
- Typography scale pairs: 28/34, 20/26, 16/22, 12/16.
- Color system: Brand Bible canonical palette tokens and faction tokens are the only valid token source.

### Proposed contract

- GetColor(token)
- GetSpacing(token)
- GetTypography(token)
- GetComponentStyle(componentType, variant)
- ApplyTheme(target)

### Engineering notes

- The Theme Manager should not own behavior; it should supply tokens and style references.
- It should be the only place where raw color literals are interpreted.
- This helps prevent style drift across presenters.
- These values are frozen design tokens, not placeholders.

## 6. Animation Manager

### Responsibility

The Animation Manager manages reusable entrance, exit, emphasis, and transition animations for screens, windows, and popups.

### Responsibilities

- Play screen transitions and popup animations.
- Support simple built-in animation types such as fade, slide, scale, and pop.
- Coordinate animation timing and cancellation.
- Expose a common animation API that screens and popups can use.

### Proposed contract

- Play(target, animationType, options)
- PlaySequence(target, animations)
- Cancel(target)
- RegisterAnimationPreset(name, preset)

### Engineering notes

- The manager should remain generic and configurable.
- It should support both simple shared animations and screen-specific animation definitions.
- This avoids each presenter needing to manage coroutine logic independently.

## 7. Responsive Layout System

### Responsibility

The Responsive Layout System provides layout rules that adapt to screen size and orientation while preserving the current procedural UI model.

### Responsibilities

- Convert logical layout definitions into actual anchored RectTransform values.
- Support portrait-first layout behaviors for mobile screens.
- Define breakpoint-based layout strategies.
- Allow screens to request layout presets such as compact, standard, and wide.
- Support reusable layout regions such as header, content, footer, modal, and action bar.

### Frozen source-of-truth requirements

- Responsive spacing, padding, and rhythm must resolve from the 8dp spacing grid.
- Interactive controls resolved by layout must satisfy minimum touch targets of 48dp (Android) and 44pt (iOS).
- Typography assignments in each layout region must map to the approved scale pairs: 28/34, 20/26, 16/22, 12/16.
- Corner treatment resolved by layout templates must use approved 12dp or 10dp radii by component role.
- Palette and faction presentation in responsive variants must use Brand Bible canonical palette and faction token mappings.

### Proposed contract

- ResolveLayoutPreset(screenId, viewportSize)
- ApplyLayout(target, layoutDefinition)
- RegisterLayoutPreset(name, definition)
- GetSafeAreaInsets()

### Engineering notes

- Because the current UI is procedural and runtime-built, this system should work by generating layout definitions rather than by relying on prefab-based layout components.
- Layout definitions should be declarative and reusable across screens.
- This system should be the bridge between the UI Bible’s layout rules and the runtime presenter code.
- Layout rule definitions must reference the approved Bible tokens directly and must not introduce alternate token values.

## Suggested object model

A lightweight object model would keep the architecture manageable:

- UIContext
  - Carries data needed for a screen, popup, or window.
- UIElement
  - A generic abstraction for a visual unit that can be presented by the framework.
- UIScreen
  - A logical screen with lifecycle hooks.
- UIWindow
  - A larger presentation surface with window semantics.
- UIPopup
  - A transient overlay with result handling.
- UILayoutDefinition
  - A declarative definition for anchors, spacing, regions, and breakpoints.

## Integration model

The current presenters can be migrated gradually by wrapping their existing logic in the new architecture:

1. Each presenter becomes a screen or popup implementation.
2. The presenter exposes lifecycle methods such as OnShow, OnHide, OnRefresh, and OnDestroy.
3. The Screen Manager coordinates transitions.
4. Shared UI construction helpers are routed through the Theme Manager and Responsive Layout System.
5. Popups and windows use the Popup Manager and Window Manager instead of creating their own overlay patterns directly.

## Phased implementation sequence

This sequence uses the priority tiers in [docs/UI_Screen_Inventory_2026-08-08.md](docs/UI_Screen_Inventory_2026-08-08.md), avoids parallel redesign of all presenters, and defers Battle until shared systems are proven.

### Phase 1 - Shared foundation proof on a lower-risk screen

- Build shared factories/components (header shell, button variants, modal wrapper, token plumbing, layout helpers) on a lower-risk metagame screen.
- Validate architecture behavior and token application with no gameplay changes.

### Phase 2 - Additional metagame screens

- Expand proven shared components to Home, Campaign, and Shop surfaces.
- Keep existing navigation behavior and interaction semantics unchanged.

### Phase 3 - Collection and Formation card-heavy surfaces

- Apply shared system to Collection and Formation views after metagame shell stability is confirmed.
- Standardize card/list/detail component composition using the same frozen tokens.

### Phase 4 - Battle and overlays

- Apply shared system to Battle and related overlays only after earlier phases validate architecture stability.
- Keep Battle logic and gameplay flow intact while refactoring presentation structure.

### Phase 5 - Final consistency and regression pass

- Perform cross-screen consistency pass (tokens, component variants, spacing/typography adherence).
- Run full UI regression checks to ensure no behavior drift.

## Benefits

- Reduces duplication across screens and overlays.
- Makes UI state transitions easier to reason about.
- Gives the project a single path for navigation, modal behavior, themeing, and animation.
- Keeps the architecture compatible with the existing procedural Runtime UI style.

# UI Technical Debt Report

Date: 2026-08-08

## Scope

This report reviews the current UI architecture in the project and identifies implementation-level debt that would affect maintainability, consistency, and future UI work. The analysis is limited to the existing presenter-based UI implementation and does not include refactoring or production changes.

## 1. Duplicated code

| Area | Evidence | Impact |
|---|---|---|
| Canvas creation | Repeated across [Assets/Scripts/UI/HomePagePresenter.cs](Assets/Scripts/UI/HomePagePresenter.cs), [Assets/Scripts/UI/CampaignMapPresenter.cs](Assets/Scripts/UI/CampaignMapPresenter.cs), [Assets/Scripts/UI/ShopPresenter.cs](Assets/Scripts/UI/ShopPresenter.cs), [Assets/Scripts/UI/DeckBuilderPresenter.cs](Assets/Scripts/UI/DeckBuilderPresenter.cs), [Assets/Scripts/UI/CollectionPresenter.cs](Assets/Scripts/UI/CollectionPresenter.cs), and [Assets/Scripts/UI/GameBootstrap.cs](Assets/Scripts/UI/GameBootstrap.cs) | Canvas, CanvasScaler, GraphicRaycaster, and reference resolution are configured repeatedly in nearly every screen. |
| Header bar construction | Repeated in home, campaign, shop, Formation, and collection presenters | Similar structure, positioning, and back-button behavior are rebuilt multiple times. |
| Button creation | Each presenter creates buttons inline using separate object construction logic | Button behavior and styling are duplicated rather than centralized. |
| Text creation | Presenters use local helpers like CreateTextElement, but the base pattern is still repeated across files | Typography and text object setup are not standardized. |
| Scroll/list container setup | Collection and Formation both build scroll view, viewport, content, and layout objects in a similar way | Repeated layout scaffolding for list surfaces. |
| Modal overlay construction | Campaign modal, collection detail, battle overlays, and story overlay each build dim backgrounds and panel containers | Overlay behavior is consistent but implemented independently. |

## 2. Hardcoded colors

| Area | Examples | Impact |
|---|---|---|
| Screen backgrounds | Home, campaign, shop, Formation, collection, and battle screens all set background colors directly in code | Color choice is scattered across presenters and difficult to unify. |
| Header and panel surfaces | Repeated dark neutral colors such as 0.05f/0.05f/0.08f and 0.12f/0.14f/0.2f appear in multiple presenters | Visual consistency is hard to maintain across screens. |
| Buttons | Button colors are set inline in each screen (for example, red, gold, green, purple) | No shared button palette or semantic state model exists. |
| Text colors | Text color values such as gold/yellow and white are repeated in multiple files | Typography styling is not centralized. |
| Battle UI palette | [Assets/Scripts/UI/GameBootstrap.cs](Assets/Scripts/UI/GameBootstrap.cs) defines a large set of inline colors for panels, buttons, health bars, and status visuals | This is the most concentrated example of color duplication and state-specific styling in the project. |

## 3. Hardcoded sizes

| Area | Examples | Impact |
|---|---|---|
| Canvas reference resolution | 1920x1080 is repeated in several presenters | Layout assumptions are locked to one resolution and duplicated across files. |
| Header sizes | Common header heights such as 100 or 110 are used repeatedly | The same shell dimensions are rebuilt with no shared sizing contract. |
| Button sizes | Buttons use fixed widths/heights in home, collection, shop, Formation, and battle UI | Screen responsiveness and reusability are limited. |
| Card/tile sizes | Collection and Formation both define card/tile sizes manually | The grid layout is not governed by a reusable card component sizing model. |
| Modal/panel sizes | Stage detail, card detail, and result panels each use specific dimensions | Panel sizing logic is repeated instead of being parameterized. |

## 4. Magic numbers

| Area | Examples | Impact |
|---|---|---|
| Layout anchoring | Repeated anchorMin/anchorMax/pivot/anchoredPosition values across screens | Layout behavior is difficult to interpret and preserve. |
| Spacing values | HorizontalLayoutGroup spacing, grid spacing, and offset values are embedded directly in individual methods | Layout tuning requires editing multiple presenters manually. |
| UI band coordinates | [Assets/Scripts/UI/GameBootstrap.cs](Assets/Scripts/UI/GameBootstrap.cs) contains many constants such as TitleY0, EnemyY0, StatusY0, HandY0, and LineupButtonsY0 | The screen geometry is highly coupled to the battle presenter and difficult to reuse. |
| Card and overlay offsets | Offset values for card art, title positions, body text, and modal panels are scattered across the code | Fine tuning and alignment become fragile. |

## 5. Duplicated layouts

| Layout pattern | Evidence | Impact |
|---|---|---|
| Screen shell | Home page, campaign map, shop, Formation, and collection all share a similar top bar + content surface + action area pattern | The shared shell should be represented by a reusable abstraction rather than recreated per screen. |
| Modal container | Stage detail modal, collection detail panel, battle overlays, and story overlay all use a dimmed backdrop plus a centered/panelled content container | Modal behavior is repeated and would benefit from a single overlay wrapper. |
| Grid/list surfaces | Collection and Formation both use a scrollable content container with a grid layout | Repeated list scaffolding is a strong candidate for a shared list component. |
| Resource row | Home page and shop both build a compact resource display row with pills or counters | Resource presentation is duplicated and should be standardized. |
| Button row | Navigation dock and action bars are similar in structure but implemented independently | A simple action-row component could consolidate these patterns. |

## 6. Reusable utility candidates

| Candidate | Why it is a good fit | Current usage |
|---|---|---|
| Shared canvas/shell factory | Centralizes canvas creation, scaler setup, event system creation, and background setup | Repeated across every presenter |
| Shared header component | Standardizes title, back button, and optional action slot | Repeated across multiple metagame screens |
| Shared modal/overlay wrapper | Standardizes dim background, panel container, close action, and focus behavior | Repeated in campaign, collection, battle, and story flows |
| Shared button factory | Centralizes button creation, sizing, text, colors, and states | Repeated across every UI screen |
| Shared text factory | Standardizes font, size, alignment, color, and rich text handling | Repeated in every presenter |
| Shared panel/container factory | Standardizes background panels and common padding/margins | Repeated across multiple screens |
| Shared scroll-list wrapper | Standardizes viewport, content, layout, and empty state behavior | Repeated in collection and Formation |
| Shared card tile component | Standardizes card tile visual structure and state slots | Repeated in collection, Formation, and shop |
| Shared resource-pill component | Standardizes currency/resource presentation | Repeated in home and shop |
| Shared overlay/message dialog | Standardizes dialog and prompt presentation | Repeated in tutorial, result, and story flows |
| Shared stateful panel factory for battle UI | Centralizes repeated band/panel layouts and styling | Strongly needed in the large battle presenter |

## Recommendations only

1. Consolidate all screen root creation into a shared UI shell utility so presenters no longer recreate canvas setup logic.
2. Introduce a shared header and action-bar abstraction for the metagame screens.
3. Create a single modal/overlay abstraction for dialogs, panels, and full-screen focus surfaces.
4. Centralize color tokens and size tokens so presenters use semantic values rather than inline literals.
5. Extract a shared button/text/panel factory to reduce repeated object construction and inconsistent styling.
6. Treat the battle screen as a special-case layout system with a dedicated helper layer rather than continuing to build every section inline.
7. Preserve the current procedural UI approach, but move the repeated UI construction logic into reusable helper utilities.

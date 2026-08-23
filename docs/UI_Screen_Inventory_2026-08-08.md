# UI Screen Inventory and Implementation Planning

> **2026-08-11 inventory extension:** Add Phase 1 Guild Overview, Members/R1–R5 Management, Chat,
> Announcements, Donations, Help, Research, Guild Store, Rankings, Hall of Fame, Event Board,
> Reward Claim and Appointed Offices. These are one coherent Guild destination with subviews, not
> separate Home buttons. Power Showcase is prestige-only; operational controls are permission-aware.

Date: 2026-08-08

## 1. Complete screen inventory

| Screen / surface | Entry point | Presenter | Dependencies |
|---|---|---|---|
| Home page / main menu | [Assets/Scenes/HomePagePresenter.unity](Assets/Scenes/HomePagePresenter.unity) and [Assets/Scripts/UI/HomePagePresenter.cs](Assets/Scripts/UI/HomePagePresenter.cs) | HomePagePresenter | SaveManager, PlayerProfile, BattleController, CampaignStageData, StoryDatabase, Resources/UI/Backdrops, Resources/UI/Panels, Resources/UI/Buttons, Resources/UI/Portraits |
| Campaign map | Launched from HomePagePresenter via the campaign navigation action | CampaignMapPresenter | CampaignStageData, SaveSystem.CurrentProfile, PlayerProfile.unlockedStageIds, StoryDatabase, StoryOverlayPresenter, Resources/UI/Backdrops |
| Stage detail modal | Opened from CampaignMapPresenter | CampaignMapPresenter | CampaignStageData, StoryDatabase, StoryOverlayPresenter |
| Shop | Launched from HomePagePresenter via the shop navigation action | ShopPresenter | PlayerProfile, SaveManager, UnityEngine.UI, Resources/UI assets |
| Formation (implemented as DeckBuilderPresenter, legacy name retained for code traceability) | Launched from HomePagePresenter via the deck-builder navigation action | DeckBuilderPresenter | PlayerProfile, SaveManager, CardDatabase, Card, Empire data, UnityEngine.UI |
| Collection | Launched from HomePagePresenter via the collection navigation action | CollectionPresenter | PlayerProfile, SaveManager, CardDatabase, Card, filter/sort interfaces, UnityEngine.UI |
| Battle screen | Runtime bootstrap via [Assets/Scripts/UI/GameBootstrap.cs](Assets/Scripts/UI/GameBootstrap.cs) | GameBootstrap | GameBootstrapLoader, BattleController, CardDatabase, PlayerProfile, Empire data, SoloAIScalingSystem, Story/tutorial data, Resources/UI/Backdrops, Resources/UI/Frames, Resources/UI/Portraits, Resources/UI/Bars, Resources/UI/Icons, Resources/UI/VFX |
| Card detail overlay | Built inside the battle screen flow from GameBootstrap | GameBootstrap | CardDatabase, Battle state, UI layout helpers |
| Lane picker overlay | Built inside the battle screen flow from GameBootstrap | GameBootstrap | BattleController, lane state, UI buttons |
| Match result overlay | Built inside the battle screen flow from GameBootstrap | GameBootstrap | BattleController, MatchResult, reward flow |
| Tutorial overlay | Built inside the battle screen flow from GameBootstrap | GameBootstrap | Tutorial data assets, Story/tutorial resources |
| Spell tooltip | Built inside the battle screen flow from GameBootstrap | GameBootstrap | Spell definitions, UI layout helpers |
| Story overlay | Triggered from campaign map or other flow entry points | StoryOverlayPresenter | StorySequence, StorySpeaker, StoryDatabase, Resources/UI/Portraits |

## 2. Shared UI components

| Component | Used by | Notes |
|---|---|---|
| Top header bar | Home page, campaign map, shop, deck builder, collection | Repeated shell element with title and back/actions |
| Bottom navigation dock | Home page | Primary metagame navigation surface |
| Back button | Campaign map, shop, deck builder, collection | Common exit/return control |
| Currency bar | Home page, shop, possibly other metagame surfaces | Displays gold, gems, energy state |
| Modal dialog / panel | Campaign map stage details, collection detail, battle overlays | Requires dimmed backdrop and close action |
| Confirmation window | Purchase and deck actions | Reused for destructive or state-changing flows |
| Card widget | Formation, collection, battle hand/detail | Repeated interactive card representation |
| Scroll list | Collection, deck builder, shop | Supports long lists and content panels |
| Search bar | Collection | Required for collection filtering workflow |
| Filter panel | Collection | Planned UI surface for future filtering control |
| Sort panel | Collection | Planned UI surface for future sorting control |
| Tabs / segmented control | Shop and future metagame surfaces | Useful if multiple content modes are introduced |
| Button set | All screens | Shared styling and state handling |
| Empty state panel | Collection, deck builder, shop | Required for empty and no-result conditions |
| Resource badge / pill | Home page, shop, battle status | Repeated small stat/amount display |
| Panel container | All screens | Shared layout primitive for sections and cards |

## 3. Screen dependency map

| Screen | Depends on shared components |
|---|---|
| Home page | Top header bar, bottom navigation dock, currency bar, button set, modal dialog, empty state panel |
| Campaign map | Top header bar, back button, modal dialog, button set, panel container |
| Stage detail modal | Modal dialog, button set, panel container |
| Shop | Top header bar, back button, currency bar, scroll list, card widget, button set, panel container |
| Formation | Top header bar, back button, scroll list, card widget, panel container, button set, confirmation window |
| Collection | Top header bar, back button, search bar, filter panel, sort panel, scroll list, card widget, modal dialog, empty state panel |
| Battle screen | Panel container, button set, resource badge, modal dialog, card widget, scroll list (if hand is treated as list), tooltip component |
| Card detail overlay | Modal dialog, card widget, button set |
| Lane picker overlay | Modal dialog, button set |
| Match result overlay | Modal dialog, button set, resource badge |
| Tutorial overlay | Modal dialog, button set |
| Spell tooltip | Tooltip component, button set |
| Story overlay | Modal dialog, panel container, button set |

## 4. UI asset inventory

| Category | Inventory |
|---|---|
| Icons | UI/Icons/play match button, UI/Icons/Tutorial_Arrow, UI/StatusIcons/Burn, UI/StatusIcons/Regeneration, UI/StatusIcons/Rage, UI/StatusIcons/Lightning |
| Backgrounds | UI/Backdrops/Zihan_City_NO NAMES, UI/Backdrops/Dark_Forest, UI/Backdrops/Desert_Ruins, UI/Backdrops/Arenas/Lava_Fortress |
| Frames | UI/Frames/{tier}, UI/Frames/Avatar_Circle_Frame, UI/Frames/NineSlice/Popup_Frame |
| Fonts | Fonts/ENDORALT, built-in LegacyRuntime.ttf / Arial.ttf fallback |
| Sprites | UI/Panels/ui_hud_backing, UI/Buttons/btn_play_massive, UI/Slots/Empty_Slot, UI/Bars/Health_Empty, UI/Bars/{fillArtName}, UI/Portraits/{portraitName} |
| Card assets | Card art resolved through CardDatabase and Resources/CardArt, plus runtime card data from CardDatabase |
| Effects | UI/VFX/Fire_Explosion, UI/VFX/Heal_Ring, UI/VFX/Magic_Circle, UI/VFX/Lightning_Strike, UI/VFX/Blood_Splash, UI/VFX/Critical_Slash, UI/VFX/Holy_Beam, UI/VFX/Shadow_Explosion |
| Existing reusable assets | Canvas, CanvasScaler, GraphicRaycaster, EventSystem, procedural Image/Text/Button primitives, runtime-generated panel and card objects |

## 5. Redesign priority

| Priority | Screens / surfaces |
|---|---|
| Critical | Battle screen, card detail overlay, lane picker overlay, match result overlay, tutorial overlay |
| High | Home page, collection, deck builder, shop, campaign map |
| Medium | Story overlay, spell tooltip, stage detail modal |
| Low | Empty state panels and shared utility panels if they remain purely presentational |

## 6. Engineering estimate

| Screen / surface | Estimate | Why |
|---|---|---|
| Home page | Medium | Requires a shared shell and several repeated widgets, but the layout is comparatively compact. |
| Campaign map | Medium | Needs a small number of interactive states and a modal detail surface, but the screen structure is straightforward. |
| Stage detail modal | Small | Single panel with a small interaction set. |
| Shop | Medium | Requires a list/grid of purchase cards and shared currency presentation, but the logic surface is limited. |
| Formation | Large | Requires two major panels, list state management, add/remove actions, and repeated card widgets. |
| Collection | Large | Requires list rendering, search/filter/sort state, detail panel behavior, and empty-state handling. |
| Battle screen | XL | Highest complexity due to multiple panels, overlays, dynamic card state, hand state, and interaction timing. |
| Card detail overlay | Medium | Requires controlled presentation of card state over the battle UI, but the scope is contained. |
| Lane picker overlay | Medium | Needs modal interaction and state binding to lane selection. |
| Match result overlay | Medium | Presents outcome state and reward information over the battle surface. |
| Tutorial overlay | Medium | Requires overlay lifecycle and text/portrait presentation. |
| Spell tooltip | Small | Lightweight overlay with limited state. |
| Story overlay | Medium | Requires portrait/text panel behavior and sequence lifecycle. |

## 7. Risks

| Risk | Technical impact |
|---|---|
| Runtime-generated UI objects are created procedurally, which increases layout fragility and makes shared-component reuse harder to enforce. | Higher implementation effort and more regression risk during refactors. |
| Multiple screens create their own canvases and UI roots, increasing the chance of duplicate event systems, overlapping canvases, and lifecycle conflicts. | Screen transitions may behave inconsistently or leave orphaned UI objects behind. |
| Several presenters depend on save state and runtime data loaded from SaveManager / PlayerProfile, which can change during screen transitions. | UI state may become stale or fail to refresh correctly. |
| Battle UI is built from a large single presenter with many nested layout and overlay responsibilities. | Refactoring the battle surface carries high coordination and regression risk. |
| UI asset availability is partially dependent on Resources loading, and some referenced assets may be missing or not imported. | Screens may render with fallback behavior or missing visuals. |
| The collection and deck-builder screens depend on CardDatabase and card lookup data that may not be fully normalized. | Rendering and state binding may be inconsistent across screens. |
| Existing code uses multiple custom UI primitives created inline rather than through a shared component model. | Reuse of common shell behavior will require structural consolidation. |

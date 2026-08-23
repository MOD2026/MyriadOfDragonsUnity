# UI Asset Audit

> **2026-08-11 missing guild asset family:** No approved production family currently covers guild
> leagues, R1–R5 ranks, appointed offices, leaderboard placement, Hall of Fame, donations, help,
> research or the Guild Store. Treat any legacy social/VIP badge as reference-only. A future pack
> must provide coherent dark-fantasy emblems for Iron/Bronze/Silver/Gold/Dragon leagues; R1–R5;
> five appointed offices; rank 1/top 3/top 10 frames; guild/default emblems; donation/help/research/
> store icons; and accessible monochrome/small-size variants. No text may be baked into art.

Date: 2026-08-08

## Scope

This audit reviews the existing UI assets under [Assets/Resources/UI](Assets/Resources/UI) and the current UI presenter code to classify each asset family by readiness for reuse in the current project state.

## Classification legend

- READY: assets are present, referenced by current UI code, and compatible with the current runtime UI usage.
- NEEDS IMPROVEMENT: assets are present and referenced, but have quality, consistency, naming, or compatibility issues that would require adjustment.
- OBSOLETE: assets are present but not used by the current UI code path or appear to be legacy/outdated relative to the current implementation.
- UNKNOWN: assets exist in the repository but are not clearly referenced by the current UI code or cannot be confidently classified from the current evidence.

## Fonts

| Asset | Status | Evidence | Notes |
|---|---|---|---|
| Fonts/ENDORALT | READY | Referenced by [Assets/Scripts/UI/GameBootstrap.cs](Assets/Scripts/UI/GameBootstrap.cs) | Primary runtime font in current UI code. |
| Fonts/ENDOR___ | UNKNOWN | Present in resources but not referenced by current UI code | Could be a fallback or alternate font asset, but not currently used. |
| Built-in LegacyRuntime.ttf / Arial.ttf fallback | READY | Used directly in presenters such as [Assets/Scripts/UI/HomePagePresenter.cs](Assets/Scripts/UI/HomePagePresenter.cs) and [Assets/Scripts/UI/CollectionPresenter.cs](Assets/Scripts/UI/CollectionPresenter.cs) | Functional fallback path for runtime text rendering. |

## Icons

| Asset | Status | Evidence | Notes |
|---|---|---|---|
| UI/Icons/play match button | READY | Referenced by [Assets/Scripts/UI/GameBootstrap.cs](Assets/Scripts/UI/GameBootstrap.cs) | Currently used for the primary battle action button. |
| UI/Icons/Tutorial_Arrow | READY | Referenced by [Assets/Scripts/UI/GameBootstrap.cs](Assets/Scripts/UI/GameBootstrap.cs) | Used by the tutorial overlay arrow. |
| UI/StatusIcons/Burn | READY | Referenced by [Assets/Scripts/UI/GameBootstrap.cs](Assets/Scripts/UI/GameBootstrap.cs) | Used by effect/status presentation. |
| UI/StatusIcons/Regeneration | READY | Referenced by [Assets/Scripts/UI/GameBootstrap.cs](Assets/Scripts/UI/GameBootstrap.cs) | Used by effect/status presentation. |
| UI/StatusIcons/Rage | READY | Referenced by [Assets/Scripts/UI/GameBootstrap.cs](Assets/Scripts/UI/GameBootstrap.cs) | Used by effect/status presentation. |
| UI/StatusIcons/Lightning | READY | Referenced by [Assets/Scripts/UI/GameBootstrap.cs](Assets/Scripts/UI/GameBootstrap.cs) | Used by effect/status presentation. |
| UI/Icons/attack | UNKNOWN | Present in the folder but not referenced by the current UI code | Might be an unused asset or an older naming convention. |
| UI/Icons/coin_stack, gem, dragon_coin, hp_potion, mana_potion, etc. | UNKNOWN | Present in the folder but not referenced by current UI presenters | Likely unused or reserved for future metagame UI. |
| UI/Icons/btn_close_x, ui_badge_red, ui_vip_badge, ui_social_btn_plate | UNKNOWN | Present in the folder but not referenced by current presenters | Likely legacy or unused in current implementation. |

## Frames

| Asset | Status | Evidence | Notes |
|---|---|---|---|
| UI/Frames/Avatar_Circle_Frame | READY | Referenced by [Assets/Scripts/UI/GameBootstrap.cs](Assets/Scripts/UI/GameBootstrap.cs) | Used for avatar portrait framing. |
| UI/Frames/NineSlice/Popup_Frame | READY | Referenced by [Assets/Scripts/UI/GameBootstrap.cs](Assets/Scripts/UI/GameBootstrap.cs) | Used for popup styling. |
| UI/Frames/{tier} | READY | Referenced dynamically by [Assets/Scripts/UI/GameBootstrap.cs](Assets/Scripts/UI/GameBootstrap.cs) | Current code expects tier-specific frame assets such as Common/ Rare / Epic / Legendary. |
| UI/Frames/Common_Card_Frame | READY | Present in the frames folder and referenced by the dynamic tier lookup | Likely usable for current card framing. |
| UI/Frames/Rare_Card_Frame | READY | Present in the frames folder and referenced by the dynamic tier lookup | Likely usable for current card framing. |
| UI/Frames/Epic_Card_Frame | READY | Present in the frames folder and referenced by the dynamic tier lookup | Likely usable for current card framing. |
| UI/Frames/Legendary_Card_Frame | READY | Present in the frames folder and referenced by the dynamic tier lookup | Likely usable for current card framing. |
| UI/Frames/Panel_Frame | NEEDS IMPROVEMENT | Present but not clearly tied to a current runtime path | Likely usable but not clearly integrated into the current presenter implementation. |
| UI/Frames/Card_Glow, Hover_Frame, Selected_Frame, Target_Highlight, Target_Highlight_critical | NEEDS IMPROVEMENT | Present in the frames folder and used by the battle UI conceptually, but not clearly referenced by the current presenters | These assets likely need consolidation into a more explicit stateful frame system. |
| UI/Frames/Character_Portrait_Frame | UNKNOWN | Present but not clearly referenced in current UI code | Could be a legacy portrait frame asset. |

## Backgrounds

| Asset | Status | Evidence | Notes |
|---|---|---|---|
| UI/Backdrops/Zihan_City_NO NAMES | READY | Referenced by [Assets/Scripts/UI/HomePagePresenter.cs](Assets/Scripts/UI/HomePagePresenter.cs) | Used for the home screen background. |
| UI/Backdrops/Dark_Forest | READY | Referenced by [Assets/Scripts/UI/CampaignMapPresenter.cs](Assets/Scripts/UI/CampaignMapPresenter.cs) | Used for the campaign map background. |
| UI/Backdrops/Desert_Ruins | READY | Referenced by [Assets/Scripts/UI/CampaignMapPresenter.cs](Assets/Scripts/UI/CampaignMapPresenter.cs) | Fallback or alternate campaign map background. |
| UI/Backdrops/Arenas/Lava_Fortress | READY | Referenced by [Assets/Scripts/UI/GameBootstrap.cs](Assets/Scripts/UI/GameBootstrap.cs) | Used for the battle arena backdrop. |
| Other backdrop assets such as Ancient_Temple, Celestial_Palace, Floating_Islands, Frozen_Citadel, Infernal_Hellscape, Lava_Arena, Ocean_Battlefield, Underground_Cavern | UNKNOWN | Present in the resources folder but not referenced by the current presenters | Likely available for future content or alternate themes. |

## Buttons

| Asset | Status | Evidence | Notes |
|---|---|---|---|
| UI/Buttons/btn_play_massive | READY | Referenced by [Assets/Scripts/UI/HomePagePresenter.cs](Assets/Scripts/UI/HomePagePresenter.cs) | Used by the bottom navigation button styling. |
| UI/Icons/btn_login_primary, btn_close_x, ui_social_btn_plate | UNKNOWN | Present in the resources tree but not referenced by current UI presenters | These appear to be older or unused button assets. |
| UI/Buttons/btn_play_massive | NEEDS IMPROVEMENT | Present and used, but the current code applies it as a generic sprite without a formal button variant system | Works, but would benefit from a standardized button-style contract. |

## Panels

| Asset | Status | Evidence | Notes |
|---|---|---|---|
| UI/Panels/ui_hud_backing | READY | Referenced by [Assets/Scripts/UI/HomePagePresenter.cs](Assets/Scripts/UI/HomePagePresenter.cs) | Used for the home header bar background. |
| UI/Popups/ui_modal_window, ui_panel_modal, ui_dimmer_overlay | UNKNOWN | Present in the resources tree but not referenced by current presenters | Likely intended for modal and popup surfaces but not currently wired in. |
| UI/Frames/Panel_Frame | UNKNOWN | Present in frame assets but not clearly applied in current UI code | Could be a reusable panel frame asset. |

## Card frames

| Asset | Status | Evidence | Notes |
|---|---|---|---|
| Common_Card_Frame | READY | Referenced through the dynamic tier lookup in [Assets/Scripts/UI/GameBootstrap.cs](Assets/Scripts/UI/GameBootstrap.cs) | Ready for card frame use. |
| Rare_Card_Frame | READY | Referenced through the dynamic tier lookup in [Assets/Scripts/UI/GameBootstrap.cs](Assets/Scripts/UI/GameBootstrap.cs) | Ready for card frame use. |
| Epic_Card_Frame | READY | Referenced through the dynamic tier lookup in [Assets/Scripts/UI/GameBootstrap.cs](Assets/Scripts/UI/GameBootstrap.cs) | Ready for card frame use. |
| Legendary_Card_Frame | READY | Referenced through the dynamic tier lookup in [Assets/Scripts/UI/GameBootstrap.cs](Assets/Scripts/UI/GameBootstrap.cs) | Ready for card frame use. |
| Avatar_Circle_Frame | READY | Referenced by [Assets/Scripts/UI/GameBootstrap.cs](Assets/Scripts/UI/GameBootstrap.cs) | Used for avatar portrait framing. |
| Character_Portrait_Frame | UNKNOWN | Present but not clearly referenced by current presenters | Might be intended for portrait presentation. |
| Card_Glow, Hover_Frame, Selected_Frame | NEEDS IMPROVEMENT | Present but not clearly wired to current UI state logic | Likely useful only if a formal stateful card frame system is introduced. |

## Element icons

| Asset | Status | Evidence | Notes |
|---|---|---|---|
| UI/Icons/icon_element_andras | UNKNOWN | Present in the resources tree but not referenced by current presenters | Could support faction or element presentation. |
| UI/Icons/icon_element_ktini | UNKNOWN | Present in the resources tree but not referenced by current presenters | Could support faction or element presentation. |
| UI/Icons/icon_element_pnevmas | UNKNOWN | Present in the resources tree but not referenced by current presenters | Could support faction or element presentation. |
| UI/Icons/icon_class_strong, icon_class_charisma, icon_class_tough | UNKNOWN | Present in the resources tree but not referenced by current presenters | Likely reserved for future card/class detail UI. |

## Rarity icons

| Asset | Status | Evidence | Notes |
|---|---|---|---|
| Existing card frame assets effectively serve as rarity visual indicators in current UI code | READY | Referenced by the card frame lookup in [Assets/Scripts/UI/GameBootstrap.cs](Assets/Scripts/UI/GameBootstrap.cs) | The current implementation uses frame assets to represent rarity rather than dedicated rarity icons. |
| UI/StatusIcons/Crystal, Gold, Luck, etc. | UNKNOWN | Present in the status icon set but not directly referenced by the presenters reviewed here | Could be used for rarity or reward visuals in future UI. |

## Overall summary

The current UI asset set is partially ready for reuse, but the project is still relying on a small subset of the available assets. The most clearly usable assets are:

- fonts and core fallback typography
- battle-related icon and frame assets used by the current battle screen
- home/background assets used by the current metagame screens
- card frame assets for tier-based card presentation

The main audit findings are:

1. A large number of assets are present but not wired into the current runtime UI code.
2. Several assets appear to be legacy or intended for a different UI structure than the current implementation.
3. Some assets are structurally useful but would need a more formal naming and usage convention to be reliably reused.

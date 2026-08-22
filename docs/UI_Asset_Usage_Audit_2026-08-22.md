# UI Asset Usage Audit — 2026-08-22

**Method:** string-ref scan of `Assets/**/*.cs` + `*.json` for `UI/...` paths and `home_*_v3` names.  
Not a full Unity dependency graph — dynamic loads can hide refs. Card art lives under `Resources/CardArt/` (separate from `UI/Icons`).

| Metric | Count |
|---|---:|
| Total images under `Resources/UI` | **350** |
| Clearly referenced by runtime strings | **~49** |
| Unreferenced / parked dump | **~301** |

## Owner verdict (Command Centre)

| Bucket | Examples | Action |
|---|---|---|
| **LIVE** | Home backdrop `Zihan_City_NO NAMES`; HomeV3 resource pills + 4 nav icons; nav button states v2; campaign map backdrops; battle VFX/StatusIcons that GameBootstrap loads; Cinematics Ch1 open/close; `Audio/Music/Battle_Theme` | Keep |
| **PARKED (do not ship forever)** | Entire `UI/Icons/ai generated asset/` (**121+** files); `UI/Backdrops/*_CANDIDATE` / `*_LEGACY_BACKUP`; unused HomeV3 frames/tiles (13 of 20 HomeV3 files unused) | Keep on disk for now; **exclude from mental “game art”**; later move to `_UnusedUI/` or delete after you confirm |
| **ORPHAN chrome** | Most top-level `UI/Icons/*.png` (tabs, coins, empire tab, campaign act BGs as `.jpg.png`), extra Portraits, Arenas not loaded, Popups | Same as parked — inventory for Shop/Empire/Guild later, not battle-critical |
| **CARD ART** | `Resources/CardArt/` via `Card.ResourcePath()` | Live path for cards — **not** under UI/Icons |

## LIVE (high confidence)

- `UI/Backdrops/Zihan_City_NO NAMES` — Home
- `UI/Backdrops/Dark_Forest`, `Desert_Ruins` (+ some Arenas fallbacks)
- `UI/Buttons/btn_home_nav_*_v2`
- `UI/HomeV3/home_icon_{story,cards,shop,battle}_v3`
- `UI/HomeV3/home_resource_{gold,gems,energy}_pill_v3`
- `UI/Portraits/Paladin` (overused as default enemy portrait)
- Battle: selected `UI/VFX/*`, `UI/StatusIcons/*` via GameBootstrap
- `Cinematics/Chapter1/Opening/*`, `.../Victory/*`
- `Audio/Music/Battle_Theme`

## UNUSED notable (safe to quarantine later)

- `UI/Icons/ai generated asset/**` (biggest bloat; includes 4K battle BGs)
- `UI/Backdrops/Zihan_City_NO NAMES_CANDIDATE`, `*_LEGACY_BACKUP`
- HomeV3 unused: identity frame, crest, nav dock/tiles, hero tiles, tutorial banner/button
- `UI/Backdrops/Arenas/*` except those actually referenced
- Extra portraits (Angel, Assassin, Kraken, …) while story mostly uses Paladin

## Do not delete yet

Wartime rule: **audit first, delete after owner OK**. Quarantine folder preferred over silent delete (APK size / “where did my art go”).

## Next cleanup pass (Cursor or offline)

1. Move `UI/Icons/ai generated asset/` → `Assets/_UnusedUI/ai_generated_asset/` (out of Resources so it stops shipping in builds)  
2. Pick one city backdrop; delete or archive CANDIDATE + LEGACY  
3. Wire real portraits for Ch1 bosses (OrcScout/Kaelen/Gorn paths referenced in StoryDatabase — verify files exist under those names)

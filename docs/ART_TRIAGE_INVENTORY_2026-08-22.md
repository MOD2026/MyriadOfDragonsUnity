# Art triage inventory — 2026-08-22

Policy: `docs/ART_TRIAGE_POLICY_2026-08-22.md`. Usage scan: `docs/UI_Asset_Usage_Audit_2026-08-22.md`.

## Batch done this pass

1. Quarantined `UI/Icons/ai generated asset/` → `Assets/_ArtTriage/Quarantine/ai_generated_asset_dump/` (out of Resources / APK).
2. Quarantined city backdrop CANDIDATE + LEGACY_BACKUP → `Assets/_ArtTriage/Quarantine/backdrops/`.
3. Live city backdrop `Zihan_City_NO NAMES.jpg` **kept** in Resources.

## KEEP_LIVE (do not move)

- Home backdrop + HomeV3: home_icon_story_v3, home_icon_cards_v3, home_icon_shop_v3, home_icon_battle_v3, home_resource_gold_pill_v3, home_resource_gems_pill_v3, home_resource_energy_pill_v3
- Buttons `btn_home_nav_*_v2`
- Battle VFX/StatusIcons currently loaded by GameBootstrap
- Cinematics Ch1 Opening/Victory
- `Audio/Music/Battle_Theme`
- CardArt under `Resources/CardArt/`

## REUSE_BANK — HomeV3 unwired (high quality, wire later)

- `UI/HomeV3/home_tile_story_hero_v3`
- `UI/HomeV3/home_tile_cards_hero_v3`
- `UI/HomeV3/home_tile_shop_hero_v3`
- `UI/HomeV3/home_tile_battle_hero_v3`
- `UI/HomeV3/home_hud_identity_frame_v3`
- `UI/HomeV3/home_identity_crest_v3`
- `UI/HomeV3/home_nav_dock_frame_v3`
- `UI/HomeV3/home_nav_tile_normal_v3`
- `UI/HomeV3/home_nav_tile_hover_v3`
- `UI/HomeV3/home_nav_tile_pressed_v3`
- `UI/HomeV3/home_nav_tile_disabled_v3`
- `UI/HomeV3/home_tutorial_banner_frame_v3`
- `UI/HomeV3/home_start_tutorial_button_v3`

## REUSE_BANK — Icons still in Resources (good candidates; rename when wiring)

Quality note: many are usable for Empire/Shop/Guild/NPC; filenames are messy (`.png.png`, spaces). Prefer rename when Metagame wires them — do not mass-rename until code refs exist.

- `UI/Icons/empire tab.png`
- `UI/Icons/shop tab.png`
- `UI/Icons/social tab.png`
- `UI/Icons/collection tab.png`
- `UI/Icons/battle tab.png`
- `UI/Icons/icon_gold.png`
- `UI/Icons/gem.png`
- `UI/Icons/gems.png`
- `UI/Icons/icon_stamina.png`
- `UI/Icons/dragon_coin.png`
- `UI/Icons/icon_element_andras.png`
- `UI/Icons/icon_element_ktini.png`
- `UI/Icons/icon_element_pnevmas.png`
- `UI/Icons/npc_kyra_merchant.png.png`
- `UI/Icons/npc_nikator_advisor.png.png`
- `UI/Icons/npc_pythia_oracle.png.png`
- `UI/Icons/npc_vulkanos_smith.png.png`
- `UI/Icons/bg_campaign_act1_outpost.jpg.png`
- `UI/Icons/bg_campaign_act2_ktini.jpg.png`
- `UI/Icons/bg_campaign_act3_andras.jpg.png`
- `UI/Icons/bg_campaign_act4_pnevmas.jpg.png`
- `UI/Icons/bg_campaign_act5_olympus.jpg.png`
- `UI/Icons/bg_story_war_room.jpg.png`
- `UI/Icons/ui_dialogue_box.png.png`
- `UI/Icons/play match button.png`
- `UI/Icons/Tutorial_Arrow.png`

## QUARANTINE

- `_ArtTriage/Quarantine/ai_generated_asset_dump/` — includes master sheet + 4K BG dumps + duplicate cutouts. **Mine for reuse** into ReuseBank before delete.
- `_ArtTriage/Quarantine/backdrops/` — city candidates

## Visual judgment samples (this session)

| Asset | Verdict | Why |
|---|---|---|
| HomeV3 story icon + hero tile | REUSE/KEEP high | Dark epic, clean silhouette, matches brand |
| City backdrop live | KEEP_LIVE | Strong Empire/home mood |
| Portraits/Paladin | KEEP but overused | Good quality; baked PALADIN label; used as default for almost all stages |
| empire tab icon | REUSE_BANK | Clean temple silhouette for Empire entry |
| ai generated master sheet | QUARANTINE | Reference sheet not a runtime sprite; cutouts may duplicate live VFX |
| VFX Lightning_Strike | KEEP_LIVE weak | Readable but low-res; OK as small FX, not hero art |

## Next triage passes (ask owner)

1. Pull best cutouts from quarantine dump into `ReuseBank/` with clean names (status icons, rarity frames, arena BGs).
2. Fix `.png.png` renames only when wiring code.
3. Owner OK to delete quarantine after ReuseBank mining.
4. Do **not** start Empire/Shop UI polish until systems packets survive doc loophole review.


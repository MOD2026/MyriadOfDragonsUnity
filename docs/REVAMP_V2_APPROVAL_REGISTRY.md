# Revamp V2 asset-state registry

Registry owner: UI/UX Codex. **No asset is APPROVED_PRODUCTION without a dated owner approval record.** Package references are conditional design evidence only.

## Classification

| Asset/path (package-relative) | Dimensions | SHA-256 / source | State coverage | Classification | Approval evidence / owner |
|---|---:|---|---|---|---|
| `01_home_first_login.png` | 1672x941 | `7DFCA63D…F5C5A3` | startup/home first login | APPROVED_PRODUCTION | Zihan owner decision, 2026-09-02 |
| `02_home.png` | 1672x941 | `1E8174118F8F66899DB83CD7FF732415233868DF7DCE99CECD31EB3D4B92B75E` | Home | APPROVED_PRODUCTION | Zihan owner approval, 2026-09-02 |
| `03_empire_map.png` | 1672x941 | `5ADDE60C…F4DED2` | Empire overview | REJECTED | Zihan owner decision, 2026-09-02; conflicting Empire image; use approved empire_revamp_v2 instead |
| `04_building_detail.png` | 1672x941 | `2C841C5D…A8EA75` | building detail | APPROVED_PRODUCTION | Zihan direct owner approval, 2026-09-12 |
| `05_daily_login_quests.png` | 1672x941 | `7F21AF2A…D478F30` | daily login/quests | APPROVED_PRODUCTION | Zihan owner decision, 2026-09-02 |
| `06_collection.png` | 1672x941 | `D4752039…E235709` | collection/deck | APPROVED_PRODUCTION | Zihan owner decision, 2026-09-02 |
| `07_shop.png` | 1920x1080 | `926F4631…BDFB179` | shop | REJECTED | Zihan owner decision, 2026-09-02; superseded for this slice |
| `08_bazaar.png` | 1672x941 | `18D030C9…581FBD9` | Bazaar | APPROVED_PRODUCTION | Zihan owner decision, 2026-09-02 |
| `09_social.png` | 1672x941 | `7BABD322…A677136` | social/chat | REJECTED | Zihan owner decision, 2026-09-02; all Social art rejected |
| `12_battle_pass.png` | 1672x941 | `09DD9E26…167D02E` | battle pass | APPROVED_PRODUCTION | Zihan owner decision, 2026-09-02 |
| `13_battle_result.png` | 1672x941 | `EE822697…200AD3` | results | APPROVED_PRODUCTION | Zihan owner decision, 2026-09-02 |
| `14_tutorial_story_overlay.png` | 1672x941 | `983B06E9…7AD11D2` | tutorial/story overlay | APPROVED_PRODUCTION | Zihan owner decision, 2026-09-02 |
| `avatar_profile_states_8_distinct.png` | 1672x941 | `CA140D3C…373994` | Avatar/profile states | APPROVED_PRODUCTION | Zihan direct owner approval, 2026-09-12 |
| `settings_states_9_distinct.png` | 1672x941 | `B5BA6A7E…609F9C47` | Settings states | REJECTED | Zihan owner decision, 2026-09-02 |
| `requests_friends_states_9_distinct.png` | 1672x941 | `39106B08…992E8F991` | requests/friends | APPROVED_PRODUCTION | Zihan direct owner approval, 2026-09-12 |
| `Assets/Resources/UI/FriendsV1/friends_roster_aligned_to_chat_v1_native_1920x1080_logo_removed_v3.png` | 1920x1080 | `5317F065EF415FF848789C398FB3B973941AF6FD866F32C569ED6FC4F579F003` | Friends roster/empty state | APPROVED_PRODUCTION | Zihan owner approval, 2026-09-02; logo/wordmark removed, aligned to Chat visual, no lock treatment; supersedes the older logo-bearing Friends visual |
| `guild_states_9_distinct.png` | 1672x941 | `FCFE959C…D15CFD4` | Guild states | REJECTED | Zihan owner decision, 2026-09-02; Group F rejected |
| `campaign_manifest_states_v1.png` | 1672x941 | `738AAFFC…AC0B7ED` | campaign | REJECTED | Zihan owner decision, 2026-09-02; Group F rejected |
| `memory_expedition_states_4_distinct.png` | 1672x941 | `EA218299…F00431C8` | Memory Expedition | REJECTED | Zihan owner decision, 2026-09-02; Group F rejected |
| `empire_building_details_11_individual.png` | 1672x941 | `1CC50536…F2EA6E16` | 11 Empire building details | CURRENT_CONDITIONAL | Approval not found / Empire owner |
| `Home_Empire_RevampV2_011/empire_revamp_v2_conditional.png` | 1672x941 | `B751236F780D8D99352475EFC3C8F2A315D30F4512B75CAC390065A4233E8F64` | Empire overview | APPROVED_PRODUCTION | Zihan owner approval, 2026-09-02 |
| `Assets/Resources/UI/SoloCircuitV1/solo_circuit_backdrop_landscape_v1.png` | 1920x1080 | `84B25E7FF7FDBB65FEA2578238FA290537351CE23FAEC5AD4711AF96D68AF18F` | Solo Circuit backdrop | APPROVED_PRODUCTION | Owner approval recorded 2026-09-02; repository asset is present and awaits presenter binding/native capture | VS/VE bind and capture |
| `battle_faithful_sample_replacement_v19.png` | 1672x941 | package file | Battle UI-109 | REJECTED | Zihan owner decision, 2026-09-02; all Battle art rejected |
| `battle_faithful_sample_replacement_v18.png` | 1672x941 | package file | Battle UI-108 | REJECTED | Zihan owner decision, 2026-09-02; all Battle art rejected |
| `battle_ui110_idle.png` | 1672x941 | package file | motion idle | REJECTED | Zihan owner decision, 2026-09-02; all Battle art rejected |
| `battle_ui110_active_motion.png` | 1672x941 | package file | motion active | REJECTED | Zihan owner decision, 2026-09-02; all Battle art rejected |
| `battle_ui110_motion_storyboard.png` | 1672x941 | package file | motion timing/z-order | REJECTED | Zihan owner decision, 2026-09-02; all Battle art rejected |
| `battle_ui110_reduced_motion.png` | 1672x941 | package file | reduced motion | REJECTED | Zihan owner decision, 2026-09-02; all Battle art rejected |
| `Battle_Art_UIUX003/*` | fixed specs | see `BATTLE_ART_PACKAGE_MANIFEST.md` | Battle art primitives | CURRENT_CONDITIONAL | AD approval not found |

All remaining PNGs in the package are aliases, crops, or earlier Battle iterations enumerated in `REVAMP_V2_PACKAGE_INDEX.md`; absent explicit approval they are `CURRENT_CONDITIONAL` when referenced by the state matrix, or `SUPERSEDED_HISTORY` when explicitly superseded there. Byte-identical aliases retain the same classification as their canonical source and are not interchangeable approvals.

## Explicit pending assets

`02_home.png` and `Home_Empire_RevampV2_011/empire_revamp_v2_conditional.png` are **APPROVED_PRODUCTION** by dated owner approval from Zihan on 2026-09-02. `SoloCircuitV1/solo_circuit_backdrop_landscape_v1.png` is also **APPROVED_PRODUCTION** by dated owner approval from Zihan on 2026-09-02. The Building Detail, Avatar/Profile, and Friend Requests candidates are **APPROVED_PRODUCTION** by Zihan direct owner instruction on 2026-09-12. Loading Sigil v3 and PackOpen are approved by the same direct owner instruction on 2026-09-12; PackOpen approval includes the post-purchase entry point. Battle Borders/Popups are excluded from the non-Battle first slice.

## Owner approval record — 2026-09-12

Zihan directly approved the Revamp V2 items for implementation and release-lane integration: Bazaar V2, Battle Pass V2, Tutorial/Story overlay, Building Detail, Avatar/Profile, Friend Requests, Pack Open, and Loading Sigil v3. No separate signature form is required. This approval does not authorize Save-schema changes, new gameplay mechanics, invented rewards, or rejected/conditional art outside the named items.

All `Social_RevampV2_012/*` and `09_social.png` are **REJECTED** by Zihan on 2026-09-02. They must not be bound or used as production references. Chat is accepted separately through the approved Chat UI package, preserving the eight-contact-point rule.

`chat_thread_states_reference_v1.png` is **REJECTED** by Zihan on 2026-09-02: too messy and presentation/web-like for a mobile game. The separate Chat UI package is **ACCEPTED** by Zihan on 2026-09-02, retaining the eight-contact-point rule. `friends_state_empty_v2.png` is **REJECTED** by Zihan on 2026-09-02: the lock treatment misrepresents an empty Friends state. `home_revamp_v2_conditional.png` is **REJECTED** by Zihan on 2026-09-02; `02_home.png` remains the canonical approved Home artwork. The logo-free Friends v3 native export is **APPROVED_PRODUCTION** by Zihan on 2026-09-02. All other Empire art is unapproved; only `Home_Empire_RevampV2_011/empire_revamp_v2_conditional.png` remains approved.

## Smallest next implementation gate

**SoloCircuitV1 -> SoloCircuitPresenter** is now approval-cleared and remains gated only on VE confirming the native capture contract. VS must not bind any other unclassified asset, invent runtime values, or change Battle/Bazaar/Chat ownership.

## Next approval actions

## Owner contact-sheet decisions — 2026-09-02

- **Approved:** `01_home_first_login.png`, `02_home.png`; `Home_Empire_RevampV2_011/empire_revamp_v2_conditional.png`; `05_daily_login_quests.png`; `12_battle_pass.png`; `06_collection.png`; `08_bazaar.png`; `friends_roster_aligned_to_chat_v1_native_1920x1080_logo_removed_v3.png`; accepted Chat UI package; `13_battle_result.png`; `14_tutorial_story_overlay.png`.
- **Rejected/superseded for this slice:** `03_empire_map.png`, `07_shop.png`, all Social art, all Battle art, `settings_states_9_distinct.png`, and all Group F Guild/minigame/overlay art including `guild_states_9_distinct.png`, `memory_expedition_states_4_distinct.png`, `campaign_manifest_states_v1.png`, Guild Hall, Mail, and Tactical Puzzle references.
- **Empire rule:** only `Home_Empire_RevampV2_011/empire_revamp_v2_conditional.png` is approved; every other Empire image remains unapproved.
- **Chat/Friends rule:** Chat remains accepted under the eight-contact-point limit; Friends remains accepted only in the logo-free v3 visual. The lock-treatment Friends empty state remains rejected.

1. CC6 records dated approval for each intended production reference (or marks it superseded). SoloCircuitV1 approval is recorded above from Zihan on 2026-09-02.
2. AD approves art-family and alpha/import metadata, including `Battle_Art_UIUX003`.
3. VE captures approved states and records hashes/dimensions from rendered output.
4. VS implements only the first approved slice after those records exist.

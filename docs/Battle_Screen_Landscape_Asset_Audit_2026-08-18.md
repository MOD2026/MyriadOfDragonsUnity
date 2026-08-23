# Battle Screen Landscape — Asset Audit & Constrained Plan (2026-08-18)

**Status: Section 3's fix IMPLEMENTED in `GameBootstrap.cs` (2026-08-18) per user go-ahead in lieu
of an available Command Centre response. `BattleLogicTests`+`TutorialGuidanceTests` run clean
(109/110, only the pre-existing out-of-scope `HomeBanner_ShowsApprovedCopy` failing). Sections 1
and 2 (the measurements and diagnosis) are unchanged and still accurate. The three open questions
in Section 6 are still open - `battle_page_frame.png`/`Ocean_Battlefield.png` remain unused, V4's
region boundaries were kept as-is (not re-litigated), and rarity frames were NOT visually
normalized (still four genuinely different aspect ratios, now rendered honestly instead of forced
into one box). Not committed. A populated 16:9 screenshot is the next step, then Command Centre
sign-off when available.

Addendum: the same "forced into one shared box" defect was also found and fixed in avatar
portrait rendering (`CreatePortrait`) - `Avatar_Circle_Frame.png` (0.8 w/h) was being stretched to
a hardcoded 1:1 square, visibly ovaling the ring. Portraits (which also vary in aspect from each
other, e.g. Paladin 0.691 vs Orc_King 0.800) are now fit within a box sized to the frame's own real
aspect instead of stretched to match it. Unrelated to the region/card-tile work above but same
audit, same root cause class, so recorded here.**

## Why this exists

Both the V3 and V4 landscape layouts were rejected on visual review. Neither attempt measured
the actual asset files before choosing region/slot dimensions — both assumed a uniform card
aspect ratio and stretched frame art to fit hand-picked pixel boxes, which is what produced the
"flattened/stretched" and "structure cannot support the actual portrait card assets" findings.
This document replaces assumption with measurement: every number below is a real pixel dimension
read directly from `Assets/Resources/`, not a guess.

## 1. Measured asset dimensions

| Asset | Path | Size (px) | Aspect (w/h) |
|---|---|---:|---:|
| Card art (sampled: archer, barbarian, ancient_dragon) | `CardArt/*_logo.jpg` | 3300×5100 | **0.647** |
| Common/Rare/Epic card frame | `UI/Frames/{Common,Rare,Epic}_Card_Frame.png` | 340×460 | 0.739 |
| Legendary card frame | `UI/Frames/Legendary_Card_Frame.png` | 400×460 | **0.870** |
| Empty board slot | `UI/Slots/Empty_Slot.png` | 170×200 | 0.850 |
| Friendly/Enemy slot | `UI/Slots/{Friendly,Enemy}_Slot.png` | 195×200 | 0.975 |
| Health bar (empty) | `UI/Bars/Health_Empty.png` | 475×170 | 2.794 |
| Health bar (fill) | `UI/Bars/Health_Fill.png` | 415×102 | 4.069 |
| Resource bar (fill) | `UI/Bars/Mana_Fill.png` | 425×111 | 3.829 |
| Avatar circle frame | `UI/Frames/Avatar_Circle_Frame.png` | 400×500 | 0.800 |
| Character portrait frame | `UI/Frames/Character_Portrait_Frame.png` | 400×500 | 0.800 |
| Portrait: Paladin | `UI/Portraits/Paladin.png` | 201×291 | 0.691 |
| Portrait: Orc_King | `UI/Portraits/Orc_King.png` | 216×270 | 0.800 |
| Attack icon | `UI/Icons/attack.jpg` | 700×700 | 1.000 |
| Battle border frame | `UI/Borders/battle_page_frame.png` | 2880×4334 | **0.665** |
| Battle win frame | `UI/Borders/battle_win_frame.png` | 640×448 | 1.429 |
| Popup frame (nine-slice) | `UI/Frames/NineSlice/Popup_Frame.png` | 724×600 | 1.207 |
| Panel frame | `UI/Frames/Panel_Frame.png` | 690×420 | 1.643 |
| Battle backdrop | `UI/Icons/ai generated asset/Ocean_Battlefield.png` | 325×300 | 1.083 (but only 325px wide) |

## 2. What this actually tells us

1. **Card art (0.647) and card frames (0.739–0.870) are NOT the same aspect ratio as each
   other, and the frames are not even the same aspect ratio as one another.** Legendary is
   noticeably wider (0.870) than Common/Rare/Epic (0.739). Any layout that forces every card
   into one fixed-size box — what every prior attempt did — either stretches the art, stretches
   the frame, or both, for at least the Legendary tier no matter what single box size is chosen.
   **This is very likely the literal, asset-level reason the last two visual reviews rejected
   "flattened/stretched" cards**, independent of any region-table math.
2. **`battle_page_frame.png` is a portrait asset (0.665 aspect, 2880×4334)** — the same shape as
   the card art itself. If this was intended as the battle screen's outer border in a 16:9
   canvas, it cannot be used full-bleed without severe stretching or an unusable crop. Either a
   landscape-cropped/redrawn variant is needed from Command Centre, or this asset should be
   dropped from the battle screen's chrome entirely (used elsewhere, e.g. a portrait modal, or
   not used at all here).
3. **The backdrop (`Ocean_Battlefield.png`) is only 325px wide.** Stretched to 1920px it will
   visibly upscale/blur. Acceptable as a background wash behind opaque HUD/board panels (which is
   most of the screen), unacceptable as a crisp focal element.
4. **Health/Resource bars are already landscape-shaped (2.8–4.1 aspect)** — no constraint problem
   here, these fit a landscape HUD strip natively.
5. **Portraits are inconsistent with each other too** (Paladin 0.691 vs Orc_King 0.800) — current
   code force-squares them via `AddSquareFitter`, which crops both to varying degrees. Lower
   priority than the board-card problem but worth flagging.

## 3. The fix this points to (no new art required)

Stop forcing card frames into one fixed pixel box. Instead:
- Render each frame at its **own native aspect ratio** (`Image.preserveAspect = true`, no
  `Type.Sliced` stretch), **sized to a common row height**, not a common width. A Legendary card
  will then naturally render slightly wider than a Common/Rare/Epic card in the same row — this is
  honest to the art, not a bug, and needs zero new assets.
- Fit the card illustration inside the frame's own inner window, not inside the frame's full
  bounding box — the frame art has painted border/corner ornamentation outside its actual "art
  window," so an inset fraction (not yet measured pixel-precisely — needs a quick visual check
  against each frame, not just its overall bounding box) is required per frame type. This should
  be verified visually before implementation, not assumed from bounding-box dimensions alone.
- Treat `battle_page_frame.png` as **not usable** for the landscape battle screen's outer chrome
  until Command Centre supplies a landscape variant or explicitly says to drop it.
- Treat the backdrop as a **wash**, not a focal image — keep opaque board/HUD panels covering
  most of its area, matching what was already the de-facto approach.

## 4. Proposed region table (draft — not yet implemented)

Same six functional regions as the rejected V4 handoff (Top HUD / lane labels / two 3×3 boards /
lane totals / activity+spell rail / hand dock+primary action), same 16:9 1920×1080 canvas. The
change is **not** the region boundaries — those were reasonably sound — it's that board slot cells
stop assuming a single card shape:

- Each lane row still spans the full board-zone width, three slots per row, per the accepted
  three-slots-per-lane requirement.
- Each **slot cell** reserves a fixed WIDTH (for row-spacing math, same as before) but the card
  tile inside it is sized by **row height only**, at the card's own native aspect, left/center
  anchored inside the cell rather than stretched to fill it. This is a direct, measured fix for
  finding #1 above.
- Everything else (HUD, lane totals, activity/spell rail, hand dock, primary action) is unaffected
  by this finding and can likely carry over from the rejected V4 table as-is, pending Command
  Centre confirmation that the region boundaries themselves (not the card-rendering bug) were
  never the actual complaint.

## 5. Appendix — full portrait and spell-icon reference (added 2026-08-18)

Extending the audit for the eventual rebuild, past just what the current rejected layout touches.

**Every avatar portrait** (`UI/Portraits/*.png`), for reference if the rebuild ever shows a
player-selected or per-opponent avatar instead of the current hardcoded pair:

| Portrait | Size (px) | Aspect (w/h) |
|---|---:|---:|
| Angel | 202×277 | 0.729 |
| Assassin | 211×301 | 0.701 |
| Beast_Master | 213×291 | 0.732 |
| Demon_Queen | 208×277 | 0.751 |
| Fire_Dragon | 201×277 | 0.726 |
| Forest_Titan | 218×271 | 0.804 |
| Ice_King | 205×277 | 0.740 |
| Kraken | 201×276 | 0.728 |
| Necromancer | 201×276 | 0.728 |
| Orc_King | 216×270 | 0.800 |
| Paladin | 201×291 | 0.691 |
| Phoenix | 202×289 | 0.699 |

All twelve sit in a tighter 0.69–0.80 band (unlike the card frames' 0.74/0.87 split), so a shared
"portrait box" aspect around 0.72–0.75 would only mildly letterbox/pillarbox any of them — much
less of a constraint than the card-frame situation.

Note, not a defect: `CreatePortrait` is currently called with a **hardcoded** portrait name
("Paladin" for the player, "Orc_King" for the enemy) regardless of who's actually playing/who the
opponent is — only the enemy's *display name* (`_aiProfile.DisplayName`) is dynamic, not their
portrait art. Worth knowing before a rebuild, since fixing it is a behavior change, not a
visual-layout one.

**Spell icons actually wired into the spell rail** (`UI/StatusIcons/*.png`, via `SpellIconSprite`):

| Effect | Icon | Size (px) | Aspect (w/h) |
|---|---|---:|---:|
| LaneDamage | Burn.png | 316×452 | 0.699 |
| LaneHeal | Regeneration.png | 320×452 | 0.708 |
| LaneAttackBuff | Rage.png | 183×217 | 0.843 |
| AvatarStrike | Lightning.png | 183×216 | 0.847 |

None of these are square either, but the current code already renders them with
`preserveAspect = true` (not stretched) — no defect here, included for completeness only.

## 6. Open questions for Command Centre

1. Is `battle_page_frame.png` meant to be used on the battle screen at all? If yes, a landscape
   variant is needed — it cannot be used as-is.
2. Confirm the V4 region *boundaries* (Top HUD/board/rail/hand/action percentages) are still
   approved, and the only structural defect was the card-rendering approach documented here.
3. Should the four rarity frames be visually normalized (new art) in a future pass, or is
   "same height, native width per rarity" the accepted permanent behavior?

## 7. Next step

Awaiting Command Centre review/response before any `GameBootstrap.cs` edits resume. No code
changes have been made as part of this audit.

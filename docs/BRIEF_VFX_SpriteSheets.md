# BRIEF: Animated VFX sprite sheets — Myriad of Dragons

**For:** an image-generation AI, or an artist.
**Deliverable:** PNG sprite sheets, to the exact spec in §3.
**No code required.** The engine side is already written and waiting for these files.

---

## 1. Why this is needed

The game already has single-frame VFX PNGs (Fire_Explosion, Holy_Beam, Lightning_Strike, etc.).
The code fades and scales them, which is the ceiling of what one static frame can do — a fireball
that grows and fades, rather than a fireball that *burns*.

What is missing is **frame-by-frame animation**: a strip of frames the engine plays in sequence.
That is the single highest-value art upgrade available, and it needs no new code beyond a short
playback routine.

---

## 2. Style reference — match this or the new art will not fit

The existing set is dark high-fantasy: painterly, heavy black outlines, saturated cores with
soft outer glow, gold/bronze ornamentation, rendered as if lit from within. Think a mobile CCG
like Raid or Summoners War rather than flat vector or pixel art.

Existing files to match are in `Assets/Resources/UI/VFX/`:
Blood_Splash, Critical_Slash, Fire_Explosion, Heal_Ring, Holy_Beam, Ice_Explosion,
Lightning_Strike, Magic_Circle, Poison_Cloud, Resurrection_Glow, Shadow_Explosion, Shield_Bubble.

**Open a few of those first and match their palette, line weight and glow treatment.**

---

## 3. Exact technical spec

| Property | Requirement |
|---|---|
| Format | PNG, **transparent background** (real alpha — not black, not white, not a checkerboard pattern drawn into the pixels) |
| Layout | **Single horizontal strip**, frames left to right, equal width, no padding or gutters |
| Frame count | **8 frames** |
| Frame size | **256 x 256 px** |
| Sheet size | therefore **2048 x 256 px** |
| Naming | `<Effect>_sheet_8.png` — e.g. `Fire_Explosion_sheet_8.png`. The trailing number is the frame count and the code reads it. |
| Content | The effect only. **No captions, no titles, no filename text, no dimension labels, no borders, no drop shadows onto the background.** |

### Critical: no baked-in text
A previous asset batch had captions like `"FULL HEALTH BAR"` and `"1024x1024 PNG"` painted into
the images. Those files had to be discarded or manually cropped. **Any text inside the image
makes the asset unusable**, because it appears on screen during play.

### Animation shape
Each 8-frame strip should read as a complete beat:
- **frames 1–2**: anticipation / onset (small, bright)
- **frames 3–5**: peak (largest, most intense)
- **frames 6–8**: dissipation (fading, spreading, breaking up)

Frame 1 should start near-empty and frame 8 should end near-empty, so the effect can be played
once and removed cleanly without a visible pop.

---

## 4. Priority order

Deliver in this order. The first four are used on every single cast and clash.

| # | File | Used for | Notes |
|---|---|---|---|
| 1 | `Fire_Explosion_sheet_8.png` | Firestorm spell, lane damage | Orange/red bloom, embers on dissipation |
| 2 | `Critical_Slash_sheet_8.png` | Lane clash where a lane breaks | Fast diagonal slash, white-hot core, brief |
| 3 | `Heal_Ring_sheet_8.png` | Mend spell | Green/gold rising ring, gentle, no violence |
| 4 | `Lightning_Strike_sheet_8.png` | Divine Bolt (direct Avatar damage) | Vertical bolt, hard flash at peak |
| 5 | `Blood_Splash_sheet_8.png` | Ordinary lane clash | Restrained — this fires very often, must not dominate |
| 6 | `Shadow_Explosion_sheet_8.png` | Ktini element card play | Purple/black, inward collapse then burst |
| 7 | `Holy_Beam_sheet_8.png` | Andras element card play | Vertical golden beam, descending |
| 8 | `Magic_Circle_sheet_8.png` | Pnevmas element / War Cry buff | Rotating rune circle, blue/cyan |

Anything beyond #8 is optional.

---

## 5. What the engine will do with these

For context only — no action needed:

- The sheet is sliced into 8 equal frames and played once at roughly 12–16 fps, then destroyed.
- Effects are drawn over a lane or over the whole screen, typically 60–190 px on screen, so
  **fine detail below about 4 px will not be visible** — favour bold silhouettes over intricate
  filigree.
- They render over busy painted arena backdrops, so **strong internal contrast matters**; a
  low-contrast wisp will disappear against the background.

## 6. Delivery

Place the finished PNGs in:
```
Assets/Resources/UI/VFX/Sheets/
```
Filenames exactly as listed in §4. No subfolders, no variants, no `(1)` suffixes.

## 7. Rejection checklist

An asset will be sent back if any of these are true:
- [ ] Any text, caption, watermark or dimension label is visible in the image
- [ ] The background is opaque, or transparency is faked with a drawn checkerboard
- [ ] Frames are unequal widths, or there is padding between frames
- [ ] The sheet is not exactly 2048 x 256
- [ ] Frame 1 or frame 8 is at full intensity (causes a visible pop)
- [ ] The style does not match the existing `UI/VFX/` set

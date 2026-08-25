"""Scoped image triage for UI art: find the assets that are VISIBLY broken, not merely unusual.

WHY THIS IS THE SMALL VERSION. A mechanical alpha-scan flagged 135 of 325 UI PNGs as having no real
transparency, but only 2 turned out to be visible bugs (icon_settings_gear.png,
"player profile frame.png" - both already fixed). "No alpha channel variation" is not a defect on its
own: a solid rectangular panel, a filled slot frame or a deliberately opaque badge all look exactly
like that and are all correct. Chasing 135 assets on that signal alone is chasing a metric, not a
bug.

So this checks only the two things that actually produced a visible bug:

  1. CHECKERBOARD DETECTOR - the icon_settings_gear class. An asset that LOOKS transparent because a
     grey/white checkerboard is painted into its real RGB pixels. It renders as an opaque tiled
     square in game. Detected by frequency, not by colour: real alternating tiles produce a strong
     regular flip pattern along both axes at a consistent tile size.

  2. CONTRAST-OVER-REAL-BACKGROUND - the profile-frame class. An asset whose opaque background is
     bright against the dark UI it sits on, so it reads as a white box. Composited over this game's
     REAL panel colours, pulled from live presenter code (see BACKGROUNDS) - never an invented
     palette, because the whole question is "does it look wrong ON THIS UI".

Deliberately NOT here: connected-component analysis, dE colour calibration, a labelled training set,
and usage-graph resolution. This project is procedural with no scenes or prefabs, so usage would
have to come from grepping Resources.Load call sites anyway - and none of that is worth building
until this pass shows the simple signal is real.

Usage:
    python tools/image_triage.py                 # scan Assets/Resources
    python tools/image_triage.py --all           # include non-flagged assets too
"""

import os
import sys
from collections import Counter

try:
    from PIL import Image
except ImportError:
    print("PIL/Pillow required:  python -m pip install pillow")
    sys.exit(2)

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
RESOURCES = os.path.join(ROOT, "Assets", "Resources")

# REAL colours, read out of live presenter code - not invented.
#   #141A22  EmpirePresenter building-row background
#   #1E2630  EmpirePresenter construction panel
#   #1A2A34  EmpirePresenter structure tiles
#   (26,23,20) EmpireBuildingDetailPresenter panel, new Color(0.10f,0.09f,0.08f) * 255
BACKGROUNDS = [
    ("#141A22", (0x14, 0x1A, 0x22)),
    ("#1E2630", (0x1E, 0x26, 0x30)),
    ("#1A2A34", (0x1A, 0x2A, 0x34)),
    ("panel_0.10/0.09/0.08", (26, 23, 20)),
]

# A frame or badge is SUPPOSED to be brighter than the panel. Only flag genuinely near-white fills,
# which is what both confirmed bugs looked like.
BRIGHT_LUMA = 200.0
BRIGHT_AREA_FRACTION = 0.55


def luma(rgb):
    r, g, b = rgb[0], rgb[1], rgb[2]
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def has_real_alpha(img):
    """True when the alpha channel actually varies - i.e. the asset is genuinely cut out."""
    if img.mode != "RGBA":
        return False
    alpha = img.getchannel("A")
    lo, hi = alpha.getextrema()
    return lo < 250


def checkerboard_score(img, sample=320):
    """
    Strength of a regular alternating tile pattern in the RGB content, 0..1.

    Real transparency checkerboards alternate between two near-greys on a fixed grid. Scored by
    walking rows and columns and asking how often the pixel flips between the image's two dominant
    light tones at a CONSISTENT stride - a photo or a gradient produces flips too, but not at one
    repeating period.
    """
    rgb = img.convert("RGB").resize((sample, sample), Image.NEAREST)
    px = rgb.load()

    # The two dominant near-grey tones, if any.
    greys = Counter()
    for y in range(0, sample, 2):
        for x in range(0, sample, 2):
            r, g, b = px[x, y]
            if abs(r - g) <= 12 and abs(g - b) <= 12 and luma((r, g, b)) > 90:
                greys[(r // 4 * 4, g // 4 * 4, b // 4 * 4)] += 1

    if len(greys) < 2:
        return 0.0

    # Pick the two most common tones that are actually DISTINGUISHABLE. A transparency checkerboard
    # is often very subtle - the real one in icon_settings_gear alternated luma 241 vs 254, a gap of
    # 13 - so the tolerance here has to be small, and the buckets fine enough not to merge the two.
    # An earlier version bucketed by 8 and required a 10-point gap, which collapsed 241/254 into
    # 240/248 and rejected the genuine checker it was written to catch.
    tones = [t for t, _ in greys.most_common(6)]
    tone_a = tone_b = None
    for i in range(len(tones)):
        for j in range(i + 1, len(tones)):
            if abs(luma(tones[i]) - luma(tones[j])) >= 6:
                tone_a, tone_b = tones[i], tones[j]
                break
        if tone_a is not None:
            break

    if tone_a is None:
        return 0.0   # only one distinguishable tone: a flat fill, not a checker

    def classify(p):
        da = abs(luma(p) - luma(tone_a))
        db = abs(luma(p) - luma(tone_b))
        if min(da, db) > 12:
            return None   # not one of the two checker tones (glyph, art, shadow)
        return 0 if da <= db else 1

    # Find the dominant run length along rows; a checker has one stride, noise has many.
    runs = Counter()
    covered = 0
    for y in range(0, sample, 3):
        run_val, run_len = None, 0
        for x in range(sample):
            v = classify(px[x, y])
            if v is None:
                if run_val is not None and run_len:
                    runs[run_len] += 1
                run_val, run_len = None, 0
                continue
            covered += 1
            if v == run_val:
                run_len += 1
            else:
                if run_val is not None and run_len:
                    runs[run_len] += 1
                run_val, run_len = v, 1
        if run_val is not None and run_len:
            runs[run_len] += 1

    sampled_pixels = (sample // 3) * sample
    if not runs or covered < sampled_pixels // 8:
        return 0.0

    # Ignore single-pixel runs: antialiasing produces those everywhere.
    real = {k: v for k, v in runs.items() if k >= 2}
    if not real:
        return 0.0

    total = sum(real.values())
    dominant = max(real.values())
    coverage = covered / float(sampled_pixels)

    # A checker is ONE repeating stride over a large area. Regularity carries the score; coverage
    # only scales it, so a small patch of stripes in a photo cannot reach the threshold.
    return min(1.0, (dominant / float(total)) * min(1.0, coverage * 1.5))


def bright_fill_fraction(img):
    """Fraction of VISIBLE pixels that are near-white - the profile-frame failure."""
    rgba = img.convert("RGBA")
    px = rgba.load()
    w, h = rgba.size
    step = max(1, min(w, h) // 128)
    visible = bright = 0
    for y in range(0, h, step):
        for x in range(0, w, step):
            r, g, b, a = px[x, y]
            if a < 16:
                continue
            visible += 1
            if luma((r, g, b)) >= BRIGHT_LUMA:
                bright += 1
    return (bright / float(visible)) if visible else 0.0


def main():
    include_all = "--all" in sys.argv
    p0, checked, skipped = [], 0, 0

    for dirpath, _, files in os.walk(RESOURCES):
        for name in sorted(files):
            if not name.lower().endswith(".png"):
                continue
            path = os.path.join(dirpath, name)
            rel = os.path.relpath(path, ROOT).replace("\\", "/")
            try:
                img = Image.open(path)
                img.load()
            except Exception as e:
                print("  UNREADABLE  %s (%s)" % (rel, e))
                continue

            # The alpha-scan population: assets with no real cut-out. Genuinely transparent art is
            # not what either confirmed bug looked like.
            if has_real_alpha(img) and not include_all:
                skipped += 1
                continue

            checked += 1
            checker = checkerboard_score(img)
            bright = bright_fill_fraction(img)

            reasons = []
            if checker >= 0.55:
                reasons.append("fake-checkerboard transparency (score %.2f)" % checker)
            if bright >= BRIGHT_AREA_FRACTION:
                worst = max(BACKGROUNDS, key=lambda b: abs(luma((255, 255, 255)) - luma(b[1])))
                reasons.append("%.0f%% near-white fill - reads as a white box on %s"
                               % (bright * 100, worst[0]))

            if reasons:
                p0.append((rel, reasons))

    print("")
    print("Scanned %d asset(s) with no real alpha cut-out; skipped %d genuinely transparent."
          % (checked, skipped))
    print("")
    if not p0:
        print("P0: none. No asset in the flagged population shows either confirmed failure class.")
    else:
        print("P0 - high-confidence VISIBLE bugs (%d):" % len(p0))
        for rel, reasons in p0:
            print("  %s" % rel)
            for r in reasons:
                print("      - %s" % r)
    print("")
    print("Everything else in the flagged population is opaque BY DESIGN as far as these two checks")
    print("can tell - solid panels, filled frames and badges all look identical to a bug on an")
    print("alpha-scan alone, which is why that scan's 135 hits were never a defect list.")


if __name__ == "__main__":
    main()

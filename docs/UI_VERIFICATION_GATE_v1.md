# UI Verification Gate v1

**Status: LOCKED by the owner, 2026-08-27.** Binding on every room (CC/CR/VS/WH) immediately.

The screen-capture harness (`Assets/Tests/Editor/ScreenContactSheetGenerator.cs`, output
`%TEMP%\MyriadOfDragonsContactSheetOutput\`) is **no longer an optional diagnostic. It is a release
gate.**

**A passing test suite alone no longer permits a UI task to be called complete.**

---

## 0. Why this exists (do not re-litigate)

Serialized state and naming conventions repeatedly disagreed with runtime reality on this project.
1799 tests passed green while every sprite failed to load, 42 ornamental borders collapsed, and Home
carried 22 touchpoints (~3x the Hick's Law ceiling).

- A sprite *reference* is not proof a sprite *rendered*.
- A button *name* is not proof of its actual *hit region*.
- A `.meta` says what someone intended; only loading the asset says what Unity built.

This is not over-engineering. Do not infer visual reality from filenames, metadata, or serialized
references. **Measure behaviour at runtime.**

The three-layer model:

| Layer | Proves |
|---|---|
| Automated runtime assertions | structural and behavioural visual facts |
| Contact-sheet review | screen-level composition |
| Human sign-off | taste, hierarchy, visual quality |

No layer substitutes for another.

---

## 1. Required workflow

### 1a. BEFORE dispatching a UI task

The task **cannot enter implementation** until it has:

- a clean **baseline capture** for every affected screen;
- the **contact sheet reviewed by the task owner** (opened and looked at, not just generated);
- a **written target list**: screens affected, primary action, expected navigation owner, known exceptions;
- a **recorded baseline** for interactive count, geometry, and loaded sprites.

> **If the baseline capture is missing or unread, the task is NOT ready for dispatch.**

### 1b. AFTER a UI change lands

The implementer runs, **in a clean Unity session**, all five:

1. all relevant EditMode tests;
2. the screen-capture harness;
3. automated geometry/input assertions;
4. a **before/after contact-sheet comparison**;
5. **individual inspection of every changed screen.**

The result is **attached to the change record**. A green test run without a reviewed capture is
incomplete work, and will be sent back.

### 1c. BEFORE claiming "looks right"

A reviewer signs off **explicitly**, item by item:

- [ ] no unintended overlap or clipping;
- [ ] all text readable at the target resolution;
- [ ] buttons visibly match their hitboxes;
- [ ] sprites actually rendered, not merely referenced;
- [ ] hierarchy and primary action match the locked IA;
- [ ] no stale or missing asset fallback;
- [ ] no regression on adjacent screens.

> **"Tests pass" is not equivalent to visual approval.**

---

## 2. What fails automatically

The project enforces **runtime-measured** structural correctness. Fail the UI validation run for:

- missing required sprites **at runtime**;
- an interactive control with **no valid hit target**;
- **overlapping interactive rectangles**, except explicitly registered modal/overlay exceptions;
- **text bounds exceeding** its intended container;
- **clipped or off-screen primary controls**;
- **duplicate or unreachable navigation targets**;
- a button whose **visible graphic and hitbox are different objects** without an approved reason;
- Home or another top-level screen **exceeding its IA limit**.

### Visible-action limits (hard)

| Surface | Max actionable controls visible at once |
|---|---:|
| Home / top-level shell | **8** |
| Secondary screen | **10** |
| Modal / overlay | **4** |

Counting rules:
- **read-only resource labels do NOT count;**
- **controls inside a deliberately opened drawer or tab do NOT count** against the parent screen;
- the **five persistent destinations are navigation roots, not five additional Home buttons** —
  they are not counted as part of Home's 8.

### Warnings — logged, human-reviewed, never auto-fail

- dense whitespace imbalance;
- inconsistent spacing;
- weak visual hierarchy;
- ornamental framing that feels excessive;
- typography technically inside bounds but hard to read;
- artwork clashing with the screen's tone;
- a screen structurally valid but visually unattractive.

These need a human eye and an approved reference. **Do not turn any of them into a pixel heuristic
that fails a build.**

---

## 3. Interaction with existing locks

- The **ornamental-border reduction rule** (LOCKED, still unapplied) lives in the *warning* tier for
  automation, but a border-reduction task is still a UI task and takes the full §1 workflow.
- The **locked Home IA** (5 destinations + swipeable feed; Social is a global drawer; Avatar tile
  cut) is the reference the "hierarchy and primary action match the locked IA" check is measured
  against.

---

## 4. AMENDMENT 2026-08-27 — BS review, verified and benchmarked

BS reviewed §2 and narrowed it. Benchmark run (required gate): industry guidance is explicit that
**"a visual report that cannot fail a build is documentation, not a test"** — so hard failures stay.
But it is equally explicit that a visual gate must **absorb anti-aliasing and pixel offsets without
failing**, and that false-positive filtering is the difference between a gate teams keep and a gate
teams disable. BS's narrowing is therefore ACCEPTED — our original §2 would have produced exactly the
false-positive noise the benchmark warns kills these systems.

### 4a. Overlap rule — NARROWED (was too broad)

- **Do NOT fail on raw rectangle intersection.** Badges, labels inside buttons, decorative overlays,
  and intentionally-covered inactive content all legitimately intersect.
- **Fail only when two INDEPENDENTLY ACTIONABLE targets compete for the same tap region** — i.e. the
  tap is genuinely ambiguous, or one target is unreachable.

### 4b. Text rule — NARROWED (was too broad)

- **Do NOT fail on raw text-bound overflow.** A conservative box with all glyphs still visible is fine.
- **Fail only on:** actual clipping, unreadable truncation, or collision with a protected control.

### 4c. Added hard failure (was missing)

- **Modal/overlay dismissal or back navigation unavailable.** A player trapped in an overlay is a
  hard failure. This was not in the original list and should have been.

### 4d. Exception mechanism — MANIFEST, not annotations

An inline annotation becomes the routine escape route and the gate becomes theatre. Instead: a
**machine-checked exception manifest**. Every entry requires:

| Field | Required |
|---|---|
| Unique ID | yes |
| Exact screen + control scope | yes |
| Reason | yes |
| Owner | yes |
| Expiry build/date | yes |
| Explicit geometry relationship (e.g. "badge overlaps parent button") | yes |

**The validator FAILS on expired, unmatched, or overbroad exceptions.** An exception that no longer
matches real geometry is itself a failure — that is what stops the manifest rotting into a
permanent suppression list.

### 4e. 8/10/4 actionable-control limits — OWNER DECISION PENDING, DO NOT IMPLEMENT AS HARD FAIL YET

**This is a live conflict, not a settled amendment.** The owner locked 8/10/4 as HARD FAILURES. BS
argues they are design-review thresholds, not correctness invariants — "a modal with five controls is
not automatically broken" — and recommends WARN, with a Home-specific target of
**"five root destinations plus one featured action."**

CC's read: BS is right that these are not correctness invariants, and the benchmark agrees the
Hick's Law fix is categorisation rather than amputation. But this is the owner's own locked number
and CC does not overturn an owner lock on advice. **Until the owner rules: implement as WARN, and do
not let a control count block a build.** Everything else in §2/§4 is live.

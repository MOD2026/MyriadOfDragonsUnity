# Handover to new coordination room — 2026-08-27, 00:10

Written by the outgoing CC session at the owner's request. **Read `docs/LOCKED_DECISIONS_REGISTER.md`
first — this file is only the "what's live right now" layer on top of it.**

---

## 1. Read these, in this order, before doing anything

1. **`CLAUDE.md`** — project constitution. Non-negotiables, ownership boundaries, frozen files.
2. **`docs/LOCKED_DECISIONS_REGISTER.md`** — the real system of record. **At the start of every turn
   read BOTH tables at its top**: STANDING ORDERS (owner constraints) and PENDING DISPATCH.
3. **`tools/seat_mailbox.md`** — the live CC↔VS channel. Has real watchers; writing there reaches VS.
4. This file.

**The conversation is never the system of record.** The previous room ran ~15 hours and degraded —
the owner correctly spotted it. Everything load-bearing is in git. Nothing was lost.

---

## 2. The single most important thing found in the last hour

**This project's test suite is structurally blind to what a player sees.**

```
218  EditMode test files
  4  assert a sprite actually loaded (1.8%)
 33  assert geometry/overlap
  7  PlayMode test files
  0  PlayMode tests that check ANY visual
```

1799 tests passed green while every sprite failed to load, 42 ornamental borders collapsed, and Home
carried 22 touchpoints (~3× the Hick's Law ceiling). **None of those were findable by the suite.**
That is why the whole previous session was the owner pasting screenshots one at a time.

**The fix already exists and nobody was using it:**
`Assets/Tests/Editor/ScreenContactSheetGenerator.cs` captures **all 24 presenter screens** to
individual PNGs plus a tiled `_ContactSheet.png` in one EditMode run.
Output: `%TEMP%\MyriadOfDragonsContactSheetOutput\`

**USE THIS ROUTINELY** — before dispatching a UI task, after a UI change lands, and whenever any room
claims a screen "looks right." It replaces the paste-a-screenshot loop entirely.

Proof it works: opening `SoloCircuit.png` immediately surfaced real bugs no test caught (below).

---

## 3. Live bugs found in captures, NOT yet dispatched

**SoloCircuit screen** (from the 23:57 capture, still live):
- BACK button overlaps the header text.
- All three trial titles (`ORDER THE RANKS` / `MUSTER THE RANKS` / `READ THE FIELD`) have their
  description text rendering **on top of** the title.
- Zero chrome (one of 3 known zero-chrome screens, with GameBootstrap and TacticalPuzzle).

Not dispatched because VS/WH were deliberately kept off UI during the Home rebuild.

---

## 4. Room status right now

| Room | Address | Status |
|---|---|---|
| **CR** | `myriadofdragonsunity-0c` | **ACTIVE — Home IA rebuild.** Built it; full suite running; then must run the contact sheet and inspect captures before reporting done. ~17 test files assert removed old buttons and need updating (instructed: update to new reality, do NOT weaken/delete). |
| **VS** | `myriadofdragonsunity-b3` | Non-UI work: AvatarStrike flipbook verify, `[runtime]` placeholder audit, possibly the second stall location. Verify identity before dispatching — session names churn constantly. |
| **WH** | Cursor — **no live session** | **Owner must paste everything manually.** Last task: hang investigation, instrumenting between-fixture (not just around ShopV1ChromeTests). |

**Verify every peer's identity with a checkable fact (commit hash / file in progress) before
dispatching.** Names churn; this caused real misdirected work in the previous session.

---

## 5. Uncommitted work in the tree — do not blanket-stage

**70 modified/untracked files right now.** Multiple rooms share this one worktree.

- **NEVER** `git add -A` or wildcard pathspecs (`git add -- "*.cs.meta"`). Both caused real
  misattribution incidents. Use explicit file lists.
- **`git diff <file>` before staging** any file another room might also be touching.
- Notable in-flight: `HomePagePresenter.cs` (CR's rebuild), `WhHangProfileTrace.cs` (WH's
  instrumentation), ~10 `*ShellTests.cs` (CR updating for the rebuild).

---

## 6. What's locked and must not be re-litigated

All verified against real code + real benchmarks. Full detail in the register.

- **Home IA**: 5 destinations (Home/Battle/Quests-Events/Collection/Empire) + swipeable feed.
  Social (Chat/Mail/Friends) is a **global drawer, not a 6th destination**. Avatar tile **cut** —
  identity header owns it. Tutorial banner is new-player-only, then converts to Events.
- **All 25 presenters classified** — including the last four: DeckBuilder→Collection,
  SoloCircuit→Quests/Events, PermitWeekKey→merged Permit entry, GuildHallEntry→Guild tab.
- **Ornamental-border reduction rule** (locked, **not yet applied** — deliberately a separate pass
  from the nav rebuild):
  > `FramedPanel` only for modal roots, featured/hero content, card art, result/reward surfaces, and
  > the screen's one primary CTA. All repeated rows, tabs, HUD/resource elements, and secondary
  > controls must be borderless unless selected/focused.

  Plus: max 3 heavy frames visible at once; no nested frames; selected tabs use accent underline.
- **Economy**: Loyalty ladder all 7 rungs, Avatar XP removed→Materials, `MaxMaterialsPerDay=275`.

---

## 7. Open items needing the owner or GPT

1. **BS prompt already drafted, not yet sent** — asks whether visual defects (text overlap, element
   overlap, touchpoint count) should **fail a build automatically** vs. needing human review. This is
   the systematic fix for §2. The outgoing room gave the owner this prompt; it may not have been sent.
2. **Border-reduction pass** — locked, unassigned, waits until the nav rebuild is verified.
3. **SoloCircuit overlap bugs** — §3, unassigned.
4. **Second stall location** — a suite stall near `SocialFoundationTests`, distinct from the known
   `ShopV1ChromeTests` hang. WH proved the Shop hang happens *between fixtures*; this may be the same
   cause at a different point.

---

## 8. Hard-won lessons — do not relearn these

- **A grep is not an observation.** The outgoing CC repeatedly asserted UI facts from `grep` that the
  rendered output contradicted (claimed `EmpireBuildingDetail` had no back button; it has two,
  lines 78-79). Same family: a `.meta` says what someone *intended*; only loading the asset says
  what Unity *built*. **Measure behaviour.**
- **Green tests ≠ correct.** Both the Materials cycle bonus (always clipped to zero) and every UI
  defect above passed a full green suite. Assert the real value, not the absence of a crash.
- **Logged ≠ delivered.** The outgoing CC wrote a summary line *claiming* a verdict table existed
  instead of pasting the table. CR caught it by grepping and refused to build on it. Paste real
  content; never reference content you haven't written down.
- **Verify-first pays every time.** CR caught **four** stale dispatches this way — each one was work
  already done hours earlier.
- **Give GPT prompts directly in chat, in a fenced code block, every time.** The owner cannot reach
  GPT/WH through CC. An artifact link is an archive, never delivery. (Standing order.)

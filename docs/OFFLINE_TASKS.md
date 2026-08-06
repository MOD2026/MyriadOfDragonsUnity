# Work that does not need Claude

Written 2026-08-06. Everything here can be done without an AI session running, and each task says
**exactly what to bring back** so the next session can pick it up cold.

Ordered by value. T1 is worth more than everything below it combined.

**How to feed results back:** append to `docs/SESSION_NOTES.md` (create it) under a heading per
task, e.g. `## T1 play test — 2026-08-07`. Then start the next session with:
*"Read docs/OFFLINE_TASKS.md and docs/SESSION_NOTES.md and continue."*

---

## T1. Play the game and write down what breaks — **highest value, blocks everything else**

Roughly a third of the project has **never been proven to work**. Unity's EditMode runner cannot
execute anything driven by `Update()` or a coroutine, so the test suite is structurally blind to
it. 77 passing tests say nothing about any of the following.

Open `C:\Users\zihan\Downloads\MyriadOfDragonsUnity` in Unity 6000.5.6f1, press Play, and check each line:

| # | What to check | What "broken" looks like |
|---|---|---|
| 1 | Combat advances on its own every ~2.2s | Board freezes after formation; clash counter stuck |
| 2 | The first-run intro and tutorial appear | Nothing shows, or a modal you cannot dismiss |
| 3 | Typewriter text completes on tap | Text stuck mid-line; tapping does nothing |
| 4 | Spell buttons cast and go on cooldown | Button does nothing, or fires while greyed out |
| 5 | Reinforcement windows open at clash 4 and 8 | No prompt, or cards cannot be placed |
| 6 | Lane picker opens, cards can be placed and recalled | Recall does not refund Resource |
| 7 | Result screen names the outcome correctly | Says "VICTORY" when neither Avatar fell |
| 8 | **Win a match, quit fully, relaunch** | **Avatar Level reset to where it started** |

**Row 8 is the single most important check in this document.** It is the only end-to-end proof
that the save system built this session actually works in a real Play session. The unit tests
cover the file format thoroughly; they cannot cover "Unity wrote it to the real
`persistentDataPath` on this machine."

To verify it by hand, the profile is at:

```
C:\Users\zihan\AppData\LocalLow\<CompanyName>\<ProductName>\player_profile.json
```

Open it in a text editor. You should see `avatarLevel`, `matchesPlayed`, `matchesWon`,
`currentWinStreak`, `seenIntro` and a `lastSavedUtcTicks` that changes each match.

**Bring back:** the numbered list with pass/fail per row, plus the exact text of any red error in
the Console (the first error matters far more than the ten that follow it).

---

## T2. Decide the four open design questions

All four are written up with measurements in `docs/Mechanics_Gap_Analysis.md` §1. They need a
decision, not research — and they are the natural thing to cross-reference with another AI.

**T2a — Adopt the siege rule?** *(one-line change)*
Knockouts at the mid profile go 51% → 83% with match length unchanged. In
`Assets/Scripts/Battle/LaneBattleResolver.cs`:

```csharp
public static bool ExposedAvatarSiegeEnabled = false;   // -> true to adopt
```

If you adopt it, also delete the test `ExposedAvatarSiegeTests.SiegeIsOffByDefault`, which exists
purely to make this decision visible in a diff.

**T2b — Should the AI cast spells?** The opponent currently never casts, has no Energy, gets no
Back-lane bonus, and never reinforces. Consequence: **no balance measurement in this project has
ever included the spell layer.** Deciding this decides whether `Energy` becomes per-side state.

**T2c — What level does a new player start at?** Currently seeded at the old hardcoded 25/15/25.
A real 1/1/1 start cannot ship yet: level-1 matches end in ~3.1 ticks and the cheapest spell costs
25 Energy at 18/tick, so a new player can never cast anything.

**T2d — Stat scale x1 vs x10.** Open since before this session.

**Bring back:** a decision per item. "Adopt siege at 6%", "AI stays passive", etc. One line each is
enough — the reasoning is already in the doc.

---

## T3. Import the 18 rescued UI icons

18 PNGs were recovered from a stale Drive copy today and are now in
`Assets/Resources/UI/Icons/`. They arrived **without `.meta` files**, so Unity has not imported
them and they cannot be loaded as sprites yet:

```
battle tab · collection tab · empire tab · shop tab · social tab
play match button · player profile frame · top hud backing · building nameplates
gems · icon_gold · icon_food · icon_stamina · icon_flags · ui_badge_red
icon_class_charisma · icon_class_strong · icon_class_tough
```

Open the project once — Unity generates the `.meta` files automatically. Then confirm each one's
**Texture Type is "Sprite (2D and UI)"** in the Inspector; anything left as a default texture
returns `null` from `Resources.Load<Sprite>` and silently renders nothing.
`Assets/Editor/CardArtImportSettings.cs` may already handle this — worth checking whether it
covers `UI/Icons` or only `CardArt`.

These are precisely the assets the home screen needs, which makes T4 possible.

**Bring back:** confirmation they import as sprites, and any that look wrong (wrong size, no
transparency).

---

## T4. Home screen — gather, don't build

`docs/BRIEF_HomePage_MainMenu.md` specifies it fully and it has **zero combat dependency**, which
makes it the best parallel task in the project. What can be done offline is deciding the layout
against the real icons from T3: which tab goes where, what the top HUD shows, what the play button
looks like.

A sketch on paper is genuinely enough.

**Bring back:** a photo or description of the layout, and which of the 18 icons maps to which
element.

---

## T5. Second device setup

Do this once on the other machine, while nothing else is running:

1. Install Unity **6000.5.6f1** via Unity Hub — that exact version, not "Unity 6, close enough".
2. `New-Item -ItemType Directory -Force C:\Users\<you>\Downloads\MyriadOfDragonsUnity`
3. `& "G:\My Drive\MOD\Sync-MOD.ps1" -Direction Pull -LocalPath "C:\Users\<you>\Downloads\MyriadOfDragonsUnity"`
4. Open that folder in Unity Hub. First open takes a few minutes.

**Never point Unity at `G:\` — not to open, not to create.** That has now cost two sessions. Read
the warning at the top of `G:\My Drive\MOD\OPEN_ME_FIRST.md` first.

**Bring back:** whether the pull worked and whether the project opens clean.

---

## T6. Housekeeping after the next reboot

Google Drive is holding locks on four dead folders. A reboot (or quitting and restarting Google
Drive from the system tray) releases them, then:

```powershell
Remove-Item -Recurse -Force "G:\My Drive\card game\Game mech\MOD\Phase 1\Game mech\_DELETE_*"
Remove-Item -Recurse -Force "G:\My Drive\MOD\MyriadOfDragonsUnity\_DELETE_*"
```

Pure clutter, no urgency, nothing depends on it.

---

## T7. Content authoring — no code required

Both are plain JSON under `Assets/Resources/Data/`, and the loaders already handle them:

- **Story chapters** — `Data/Story/story_chapters.json`. Only `prologue` exists. The save system
  now records which chapters a player has read (`seenChapterIds`), so more chapters have somewhere
  to be tracked. Same shape as the existing entries: `id`, `title`, `beats[]` with
  `speaker` / `portrait` / `text` / `background`.
- **VFX sprite sheets** — `docs/BRIEF_VFX_SpriteSheets.md` is written for an image-gen AI or an
  artist and needs no code at all.

**Bring back:** the JSON (I will validate and wire it), or the generated sheets.

---

## What NOT to do offline

- **Don't run the balance simulation and act on one run.** n=400 gives roughly ±5 points of
  run-to-run noise — the max-profile baseline measured 49.8%, 50.3% and 58.3% across three runs of
  identical code. Treat any difference under ~10 points as unresolved.
- **Don't hand-edit `player_profile.json` and expect it to survive.** Malformed JSON gets
  quarantined to `.corrupt-<timestamp>` and replaced with a fresh profile. That is deliberate, but
  it means a typo costs you the file.
- **Don't refactor combat.** Every behavioural change is supposed to come with an EditMode test,
  and an untested change to `LaneBattleResolver` is how the last balance regression happened.

---

## State as of 2026-08-06

| | |
|---|---|
| Test suite | **77/77 passing** |
| The game | `C:\Users\zihan\Downloads\MyriadOfDragonsUnity` |
| Scratch project | `C:\MOD\TestMod6Aug` (3D Cross-Platform template) |
| Cross-device transfer | `G:\My Drive\MOD\` — suitcase only, never open Unity from it |
| Drive link | https://drive.google.com/drive/folders/1rcXFfKp-iJir68GNoyuxlHZH7FicoH6i |

Run the suite (Unity must be closed):

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\zihan\Downloads\MyriadOfDragonsUnity" -runTests -testPlatform EditMode -testResults "results.xml" -logFile "run.log"
```

Never add `-quit` — the run silently does nothing.

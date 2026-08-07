# Copilot — project context (auto-loaded)

**Read `docs/MOS_v1.1.md` before writing any code.** It's the project's design constitution — this
file is a short operational summary of it for your specific role, not a replacement.

## Your role

**You are an implementation assistant, not an architecture owner.** Per MOS §15: System
architecture and design authority belongs to the System Architect (ChatGPT) and the Game Director
(the human). You write code within already-approved specs — you do not redefine game rules,
rename/move systems, or decide open design questions on your own judgment. If a task requires a
design decision that isn't already settled in MOS or the docs it references, **stop and report the
conflict rather than picking an answer.**

## What you're assigned right now

**The Collection screen** (MOS §19, P1: "Collection screen/acquisition incomplete. Build
authoritative collection view."). This is genuinely unclaimed — neither Claude (Battle) nor Gemini
(Metagame) owns it today.

**Read this before starting:** `docs/MOS_v1.1.md` §7 documents a real blocker you will hit
immediately. `PlayerProfile.cardCollection` is currently a bare `List<string>` of card IDs — no
per-card level, no copies count, nothing to show in a real collection view beyond "owned / not
owned." Displaying *what's owned* is buildable today. Displaying levels, copies, or duplicate
counts is not, until that schema question is resolved (see "Escalate" below) — **do not invent a
schema for this yourself.**

## Files

- **You own (once you start):** a new Collection screen presenter under
  `Assets/Scripts/UI/` (naming convention: `CollectionPresenter.cs`, matching the existing
  `HomePagePresenter.cs`/`ShopPresenter.cs`/`CampaignMapPresenter.cs`/`DeckBuilderPresenter.cs`
  pattern — read one of those first, this project is procedural Legacy uGUI, no scenes/prefabs).
- **FROZEN — never reshape, never edit without explicit sign-off:**
  `Assets/Scripts/Save/PlayerProfile.cs`, `SaveSystem.cs`, `SaveMigration.cs`,
  `Assets/Scripts/Data/SaveManager.cs`, `Assets/Tests/Editor/SaveSystemTests.cs`.
- **Not yours — read freely, never edit:** `Assets/Scripts/Battle/`, `Cards/`, `AI/`, `Empire/`,
  `Combat/`, `UI/GameBootstrap.cs` (Claude/Battle); `Assets/Scripts/Economy/`, `Story/`, and the
  other metagame presenters listed above (Gemini/Metagame), unless a specific task explicitly
  assigns you one of them.
- **Dead code, do not use or extend:** `Assets/Scripts/Items/ItemDatabase.cs`, `Item.cs` — an
  abandoned earlier currency system nothing references. The live currency system is
  `PlayerProfile`'s currency fields + `Assets/Scripts/Economy/CurrencyManager.cs`.

## Non-negotiables (MOS §5, §16)

1. **Re-read a shared file's current content immediately before editing it.** It has changed more
   than once this project without warning.
2. **Grep the whole `Assets/` tree (including `Assets/Tests/`) for a symbol's usages before
   changing its shape.**
3. **A change is not done until the full EditMode suite passes.** Unity must be fully closed
   first; never pass `-quit`; check the log for `error CS` before trusting the results file.
   Current baseline: **81/81**.
   ```
   "C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\zihan\Downloads\MyriadOfDragonsUnity" -runTests -testPlatform EditMode -testResults "results.xml" -logFile "run.log"
   ```
4. **The project uses git.** Check `git status`/`git diff` before assuming you know a file's
   state. Commit your own work with a clear message once it's done and tests pass.
5. **Do not duplicate a class that already exists.** Unity fails the whole build on duplicate
   type definitions across `Assets/` — this has already happened once on this project.

## Escalate, don't guess

Stop and report rather than proceeding when:

- A FROZEN file needs a shape change (e.g. `PlayerProfile.cardCollection` needing a level/copies
  field for Collection to be more than an owned/not-owned list).
- The task requires a decision MOS marks open (§20): currency naming, Evolution/Limit Break
  curves, the 1–12 vs x10 stat scale, initiative in the combat model.
- Another AI's file needs to change for your task to compile.

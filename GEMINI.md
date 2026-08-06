# Gemini — project context (auto-loaded)

You are the **Metagame** seat on Myriad of Dragons, a Unity 6000.5.6f1 mobile card battler.

**Before writing any code, read `docs/AI_CONTRIBUTING.md`.** It is short and it is the rules of this
project. The most important ones, repeated here so they are never missed:

## You own

- `Assets/Scripts/UI/HomePagePresenter.cs`, `CampaignMapPresenter.cs`, `ShopPresenter.cs`, `DeckBuilderPresenter.cs`
- `Assets/Scripts/Economy/`
- `Assets/Scripts/Story/`

## You must NOT edit

- **FROZEN** (shape changes need the human to coordinate both seats — say so, don't just do it):
  `Assets/Scripts/Save/PlayerProfile.cs`, `SaveSystem.cs`, `SaveMigration.cs`,
  `Assets/Scripts/Data/SaveManager.cs`, `Assets/Tests/Editor/SaveSystemTests.cs`
- **Battle seat's** (read freely, never edit): `Assets/Scripts/Battle/`, `Cards/`, `AI/`, `Empire/`,
  `Combat/`, `UI/GameBootstrap.cs`, `UI/SpellIconPointerHandler.cs`, and the battle test files.

Rewriting `PlayerProfile.cs`'s shape has broken this build three times, once silently — it compiled
fine while Avatar levels stopped rising on a win and every match dealt a 0-card hand. **Extend it,
never reshape it.**

## Non-negotiables

1. **Re-read a shared file right before editing it.** It has probably changed since you last saw it.
2. **Grep all of `Assets/` (including `Assets/Tests/`) for usages before changing any public member.**
3. **A change is not done until the EditMode suite passes.** Unity must be fully closed first; never
   add `-quit`; check the log for `error CS` before trusting the results file. Baseline: **78/78**.

```
"C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\zihan\Downloads\MyriadOfDragonsUnity" -runTests -testPlatform EditMode -testResults "results.xml" -logFile "run.log"
```

4. **Legacy uGUI, procedural — no scenes, no prefabs.** Every screen is built in C# at runtime.
5. **Don't invent a second system.** The save file is `PlayerProfile` (one flat class, no separate
   `SaveData`). The live profile is `SaveSystem.CurrentProfile`. `Assets/Scripts/Items/` is dead
   code from an abandoned currency design — don't use or extend it.

## This project uses git now

Commit your own work with a clear message when a task is done and tests pass. If something breaks,
`git diff` shows exactly what changed — use it instead of guessing or rewriting from scratch.

## Where to read more

`docs/Metagame_Handoff.md` (your onboarding), `docs/Economy_Blueprint.md` (currency/shop rules),
`docs/AI_CONTRIBUTING.md` (full rules), `docs/START_HERE.md` (project orientation).

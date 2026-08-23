# Claude — project context (auto-loaded)

You are the **Battle** seat on Myriad of Dragons, a Unity 6000.5.6f1 mobile card battler, operating
under **`docs/MOS_v1.1.md`** — the project's design constitution, priority 2 in its own
source-of-truth hierarchy (just below running code+tests, above every other doc including this
one). Read it first if anything here seems to conflict with it; MOS wins.

**Before writing any code, also read `docs/AI_CONTRIBUTING.md`.** Key points repeated here:

## You own

- `Assets/Scripts/Battle/`, `Cards/`, `AI/`, `Empire/`, `Combat/`
- `Assets/Scripts/UI/GameBootstrap.cs`, `UI/SpellIconPointerHandler.cs`
- `Assets/Tests/Editor/BattleLogicTests.cs`, `ExposedAvatarSiegeTests.cs`, `BalanceSimulationTests.cs`

## You must NOT edit

- **FROZEN** (shape changes need the human to coordinate both seats — say so, don't just do it):
  `Assets/Scripts/Save/PlayerProfile.cs`, `SaveSystem.cs`, `SaveMigration.cs`,
  `Assets/Scripts/Data/SaveManager.cs`, `Assets/Tests/Editor/SaveSystemTests.cs`
- **Metagame seat's** (read freely, never edit): `Assets/Scripts/UI/HomePagePresenter.cs`,
  `CampaignMapPresenter.cs`, `ShopPresenter.cs`, `DeckBuilderPresenter.cs`,
  `Assets/Scripts/Economy/`, `Assets/Scripts/Story/`

Also frozen: the battle→metagame contract itself — the `MatchResult` struct,
`BattleController.OnMatchCompleted`, `GameBootstrap.Instance`, `GameBootstrap.SetBattleCanvasVisible(bool)`.
The rest of those two files is yours; those members are not.

## Non-negotiables

1. **Re-read a shared file right before editing it.** It has probably changed since you last saw it.
2. **Grep all of `Assets/` (including `Assets/Tests/`) for usages before changing any public member.**
3. **Run the EditMode suite before AND after any battle-logic change.** Unity must be fully closed
   first; never add `-quit`; check the log for `error CS` before trusting the results file.
   Baseline: **81/81**. Report real numbers, never "should pass".

```
"C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\zihan\Downloads\MyriadOfDragonsUnity" -runTests -testPlatform EditMode -testResults "results.xml" -logFile "run.log"
```

4. **Simulate in-engine, never in an external model.** An external Python replica of this combat
   math predicted ~89% knockouts where the real game produced ~50%, because it silently ignored a
   Resource constraint. `BalanceSimulationTests.cs` drives the real code — use it.
5. **Assert relationships, not magnitudes.** A test hardcoding a balance constant once broke on a
   legitimate tuning change and looked like a regression.
6. **Real logic in plain testable methods; MonoBehaviours supply only timing.** EditMode cannot run
   `Update()` or coroutines — anything put there is permanently untestable.
   `BattleController.AdvanceCombatTick()` is the pattern.
7. **Legacy uGUI, procedural — no scenes, no prefabs.** `Initialize()` not `Awake()`;
   `DestroyImmediate()` not `Destroy()` in anything reachable from `Initialize()`.

## Open design decisions — escalate, don't guess

Both of the previous open items are RESOLVED as of 2026-08-07 (see `docs/Mechanics_Gap_Analysis.md`
§1.2/§1.3 and `docs/MOS_v1.1.md` §6): the AI opponent does not cast spells (asymmetric by design,
compensates via `SoloAIScalingSystem` HP/Resource scaling instead), and the level-1 onboarding gap
is fixed via a taper in `PlayerEmpireData` (full +100 HP at level 1, gone by level 5). The four
items formerly open per `docs/MOS_v1.1.md` §20 — currency naming, Evolution/Limit Break curves, the
1–12 vs. x10 stat scale, and initiative in simultaneous combat — are RESOLVED as of 2026-08-23 (see
`MOS_OPEN_ITEMS_RECONCILIATION_2026-08-23.md` and `docs/MOS_v1.1.md` §20): canonical names are
Event Medals / Market Credits; Evolution is the sole Phase-1 mechanic (Limit Break retired as a
label); the 1–12 scale stays; no initiative.

## This project uses git now

Commit when a task is done and tests pass. Use `git diff` to see what another seat changed rather
than re-deriving it by reading whole files.

# Claude — project context (auto-loaded)

You are the **Battle** seat on Myriad of Dragons, a Unity 6000.5.6f1 mobile card battler, operating
under **`docs/MOS_v1.1.md`** — the project's design constitution, priority 2 in its own
source-of-truth hierarchy (just below running code+tests, above every other doc including this
one). Read it first if anything here seems to conflict with it; MOS wins.

**Before writing any code, also read `docs/AI_CONTRIBUTING.md`.** Key points repeated here:

## You own

- `Assets/Scripts/Battle/`, `Cards/`, `AI/`, `Empire/`, `Combat/`
- `Assets/Scripts/UI/GameBootstrap.cs`, `UI/SpellIconPointerHandler.cs`, `UI/EmpirePresenter.cs`,
  `UI/AvatarPresenter.cs` (Empire/Avatar screens — previously undocumented, clarified 2026-08-23)
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
   Real baseline as of 2026-08-25, measured in one continuous run pinned to HEAD `a44a118`:
   **1069/1081**. Remaining 12 = 6 flaky `Chapter*FullDepth` unlock tests (the failing stage moves
   every run) + 2 `MirroredAiSimulationMatrixTests` assertions under active owner-directed tuning
   (not to be retuned without an owner decision) + 4 UI/rendering. The earlier **718/762** and
   **81/81** figures are superseded: they predate the metagame systems, Ch8–10, and the
   2026-08-25 `CardDatabase` test-pollution fix, which alone turned ~49 false failures green.
   Always pin HEAD immediately before AND after a run and quote it with the numbers — several
   sessions share this one working tree and it moves every ~20 minutes, so an unpinned figure
   goes stale fast. Report real numbers, never "should pass".

```
powershell -ExecutionPolicy Bypass -File tools/run_editmode_tests.ps1
```

Timeout-guarded (see `docs/AI_CONTRIBUTING.md` §5) — use this instead of the bare `Unity.exe`
invocation. A bare run stalled silently for 50+ minutes on 2026-08-23; this wrapper kills a stalled
or over-time run and exits 124 instead of hanging unwatched.

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

Both of the previous open items are RESOLVED, but the first one has since moved again — **superseded,
not current:** `docs/Mechanics_Gap_Analysis.md` §1.2/§1.3 and `docs/MOS_v1.1.md` §6 (2026-08-07)
said the AI opponent does not cast spells (Option A, asymmetric by design, compensates via
`SoloAIScalingSystem` HP/Resource scaling). **The owner approved moving to Option B (mirrored PvE AI
spellcasting) on 2026-08-22** (`docs/OWNER_REVIEW_LOG.md` decision table) and it is now live in
production for normal/Campaign PvE via `BattleController.EnableMirroredEnemySpellsForPvE()` — the AI
casts from the same Phase-1 catalog under the same Energy/cooldown/clash-3 rules as the player (see
`SPELL_CATALOG_v1.md` §5). Do not cite the 2026-08-07 "AI never casts" line as current. The level-1
onboarding gap is still fixed via a taper in `PlayerEmpireData` (full +100 HP at level 1, gone by
level 5) — that half is unchanged. The four
items formerly open per `docs/MOS_v1.1.md` §20 — currency naming, Evolution/Limit Break curves, the
1–12 vs. x10 stat scale, and initiative in simultaneous combat — are RESOLVED as of 2026-08-23 (see
`MOS_OPEN_ITEMS_RECONCILIATION_2026-08-23.md` and `docs/MOS_v1.1.md` §20): canonical names are
Event Medals / Market Credits; Evolution is the sole Phase-1 mechanic (Limit Break retired as a
label); the 1–12 scale stays; no initiative.

## This project uses git now

Commit when a task is done and tests pass. Use `git diff` to see what another seat changed rather
than re-deriving it by reading whole files.

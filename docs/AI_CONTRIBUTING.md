# AI_CONTRIBUTING — the rules every AI session must follow

**Paste this file (or a link to it) at the start of every AI session working on this project.**

It exists because shared files were rewritten by different sessions at least three times in one day,
once causing a silent regression that no compiler error caught: `PlayerProfile.ApplyDataToEmpire()`
became an empty method, so `DeckSlotCount` stayed 0, so every match dealt a 0-card hand — and
separately `RecordMatchResult` stopped raising Avatar level, so winning silently stopped making the
player stronger. Both compiled fine. Both were found only by running the test suite.

This document is the fix. It is short on purpose so it actually gets read.

---

## 1. The five rules

1. **Never change a FROZEN file's shape** (§2) — no renaming/removing/retyping public fields,
   methods, or properties; no changing what an existing method does. Adding a genuinely new member
   is allowed. If you believe a frozen file must change shape, **stop and say so** rather than doing
   it — the human coordinates that change across both sessions.
2. **Never edit a file another seat owns** (§3). Read it freely. If it needs to change, say what
   needs changing and let its owner do it.
3. **Re-read a shared file immediately before editing it.** Do not trust your memory of it, an
   earlier message in the conversation, or a zip someone sent you. It has probably changed. This
   session, `PlayerProfile.cs` changed three times in one conversation.
4. **Before changing any public member, grep the whole `Assets/` tree for its usages** — including
   `Assets/Tests/`. A clean compile in your own file means nothing if a test or another seat's
   presenter referenced what you just renamed.
5. **Run the full EditMode suite before you claim you're done** (§5). "It compiles" is not "it
   works" — see the regression above, which compiled perfectly.

---

## 2. FROZEN files — shape changes need coordination, not a unilateral edit

These are the seams both seats depend on. Every collision this project has had was in one of them.

| File | Why frozen |
|---|---|
| `Assets/Scripts/Save/PlayerProfile.cs` | The entire save file's shape. Battle reads levels/history; metagame reads currencies/collection. Rewritten 3+ times, twice breaking the other seat. |
| `Assets/Scripts/Save/SaveSystem.cs` | Load/save I/O, atomic write, corruption quarantine, the `CurrentProfile` singleton. Simplifying this away has already silently removed crash-safety once. |
| `Assets/Scripts/Save/SaveMigration.cs` | Normalises/repairs loaded saves. Must stay in step with `PlayerProfile`'s fields. |
| `Assets/Scripts/Data/SaveManager.cs` | Façade both seats call. Trivially small — and exactly why it silently breaks when the thing underneath it moves. |
| **The battle→metagame contract specifically:** `MatchResult` struct, `BattleController.OnMatchCompleted`, `GameBootstrap.Instance`, `GameBootstrap.SetBattleCanvasVisible(bool)` | The only supported way the two halves talk. The rest of those two files is Battle-owned (§3); *these members* are frozen. |

**Extend, don't reopen.** Need a new currency, a new stat, a new list? Add a new field/method rather
than reshaping existing ones, or put the logic in a new file that calls the existing public surface.

---

## 3. Ownership — who edits what

Two seats today. Add rows, don't reassign existing ones, if more are added.

| Area | Path | Owner | Others may |
|---|---|---|---|
| Battle logic | `Assets/Scripts/Battle/` | **Battle** | read |
| Cards & synergy | `Assets/Scripts/Cards/` | **Battle** | read |
| AI opponent | `Assets/Scripts/AI/` | **Battle** | read |
| Empire/progression math | `Assets/Scripts/Empire/` | **Battle** | read |
| Combat animation | `Assets/Scripts/Combat/` | **Battle** | read |
| Battle UI | `Assets/Scripts/UI/GameBootstrap.cs`, `SpellIconPointerHandler.cs` | **Battle** | read |
| Battle/balance tests | `Assets/Tests/Editor/BattleLogicTests.cs`, `ExposedAvatarSiegeTests.cs`, `BalanceSimulationTests.cs` | **Battle** | read |
| Metagame UI | `Assets/Scripts/UI/HomePagePresenter.cs`, `CampaignMapPresenter.cs`, `ShopPresenter.cs`, `DeckBuilderPresenter.cs` | **Metagame** | read |
| Economy | `Assets/Scripts/Economy/` | **Metagame** | read |
| Story data | `Assets/Scripts/Story/` | **Metagame** | read |
| Save system | `Assets/Scripts/Save/`, `Assets/Scripts/Data/` | **FROZEN — §2** | read; propose changes |
| Save tests | `Assets/Tests/Editor/SaveSystemTests.cs` | **FROZEN — §2** | read |
| Docs | `docs/` | shared | anyone may add; don't rewrite another seat's doc |

### Dead code — do not use, do not extend

`Assets/Scripts/Items/ItemDatabase.cs` and `Item.cs` are an **abandoned earlier currency system**
(`ItemId.Gold`, `.Food`, `.DragonCoin`, `.Gem`, `.Flag`…). Nothing reads or writes them. The live
currency system is `Assets/Scripts/Economy/` + `PlayerProfile`'s currency fields. Don't add
references to `ItemDatabase`; don't "unify" it with the real system without an explicit decision.

---

## 4. Where things actually live (so nobody invents a second one)

Before building anything, check whether it exists:

| Concept | It already lives here |
|---|---|
| The save file | `PlayerProfile` — one flat `[Serializable]` class, serialised directly by JsonUtility. There is no separate `SaveData` class (an earlier one was deleted). |
| The live profile | `SaveSystem.CurrentProfile` (`SaveSystem.Profile` and `SaveManager.SaveData` are the same object). **Never call `PlayerProfile.LoadOrCreate()` for gameplay** — two copies of the profile in one session is a real bug this singleton exists to prevent. |
| Currencies | `PlayerProfile.gold/gems/eventMedals/guildContribution/dragonRelics/stamina`, plus `Economy/CurrencyManager.cs` for type-agnostic access |
| Match economy (deck size, resource cap, Avatar HP) | Derived by `PlayerEmpireData.InitializeTCGModifiers()` from the four levels. **Never hardcode these.** |
| Match outcome | `MatchResult` via `BattleController.OnMatchCompleted` |

---

## 5. Definition of done

A change is not done until **all** of these pass:

```
"C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\zihan\Downloads\MyriadOfDragonsUnity" -runTests -testPlatform EditMode -testResults "results.xml" -logFile "run.log"
```

- Unity must be **fully closed** first — it holds an exclusive lock (check for `Unity.exe` /
  `UnityPackageManager.exe` processes).
- **Never add `-quit`** — the run silently does nothing.
- Check `run.log` for `error CS` and `Aborting batchmode` **before** trusting `results.xml` — a
  compile failure means no tests ran at all.
- Every behavioural change gets an EditMode test.
- Report the actual pass/fail numbers. Never say "should work" or "tests should pass."

Current baseline: **78/78 passing.**

---

## 6. Coding conventions

- **Legacy uGUI, procedural.** No scenes, no prefabs — every screen is built in C# at runtime.
- Unity **6000.5.6f1** exactly.
- Comments explain **why**, not what — many record a bug that was actually hit. Don't delete them.
- **Assert relationships, not magnitudes** in tests. A test that hardcoded a balance constant broke
  on a legitimate tuning change and looked like a regression.
- Real logic goes in plain testable methods; MonoBehaviours supply only timing
  (`BattleController.AdvanceCombatTick()` is the pattern). EditMode cannot run `Update()` or
  coroutines, so anything put there is permanently untestable.
- `Initialize()`, not `Awake()`, for anything tests construct. `DestroyImmediate()`, not `Destroy()`,
  in anything reachable from `Initialize()`.

---

## 7. Escalate instead of guessing

Say so and stop, rather than proceeding, when:

- A FROZEN file needs a shape change.
- Another seat's file needs to change for your work to compile.
- A design decision is genuinely open (the two live ones: **should the AI opponent cast spells?**
  and **what level does a new player start at?** — see `docs/Mechanics_Gap_Analysis.md` §1).
- You were handed a claim about the code (from a human relaying another AI, or an old zip) that
  doesn't match what's actually in the files. **Verify against the live files first.** Several
  reported "bugs" this project have referenced files that do not exist.

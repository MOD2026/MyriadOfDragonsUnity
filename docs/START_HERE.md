# START HERE — Myriad of Dragons

Read this first. It is the entry point for a new session and points at everything else.

Last updated: 2026-08-06. Suite status: **77/77 passing.**

---

## 1. Where the project lives

| Purpose | Path |
|---|---|
| **Working copy — edit and run here** | `C:\Users\zihan\Downloads\MyriadOfDragonsUnity` |
| Backup / cross-machine sync | `G:\My Drive\MOD\MyriadOfDragonsUnity` |

**Work on the local Downloads copy — never on the Drive one.** Hosting the live project on Google
Drive was tried and cost most of a session in failures — three distinct ones:

1. **Windows MAX_PATH.** The old Drive path (74 chars) pushed Unity package files to 266
   characters against a 260 limit, with `LongPathsEnabled = 0`. Symptoms were misleading:
   "could not read `.asmdef`", then a wall of bogus `CS0433` duplicate-assembly errors in
   `com.unity.burst`.
2. **Sync hydration.** Files that exist but are not materialised locally read as missing.
3. **File locks.** `UnityPackageManager.exe` left `.tmp-<pid>-*` folders that could not be
   deleted even after killing the process; they had to be renamed out of the way. This has hit
   Drive-hosted project attempts twice now.

`C:\Users\zihan\Downloads\MyriadOfDragonsUnity` is 45 characters — well under the danger zone —
and none of this occurs. Sync `Assets`, `Packages`, `ProjectSettings` and `docs` to Drive.
**Never sync `Library`, `Temp` or `Logs`** — they are machine-specific caches and syncing them is
what corrupted the project.

Was `C:\MOD\MyriadOfDragons` until 2026-08-06, when the two were confirmed identical (0 files
missing or differing, 77/77 tests passing at the new path) and consolidated to this one, so they
can't silently drift apart. `Sync-MOD.ps1`'s default `-LocalPath` was updated to match.

There are leftover `_DELETE_*` folders on Drive under `G:\My Drive\MOD\MyriadOfDragonsUnity\`.
Safe to delete whenever Drive releases them (usually needs a reboot or a Drive restart).

---

## 2. Running the tests

```
Unity.exe -batchmode -projectPath "C:\MOD\MyriadOfDragons" -runTests -testPlatform EditMode -testResults "results.xml" -logFile "run.log"
```

- **Never add `-quit`** — the run silently does nothing.
- Unity must be **closed** first; it needs exclusive project access.
- If a run hangs, delete `Library/Bee` (a stale build lock).
- Paths with spaces break Unity's argument parser — another reason to stay on `C:\MOD`.

---

## 3. The other documents, and when to read them

All in `docs/`:

| File | Read it when |
|---|---|
| `OFFLINE_TASKS.md` | **Resuming after a break, or working without an AI session.** Ranked task list, each with what to bring back. Results go in `SESSION_NOTES.md`. |
| `Mechanics_Gap_Analysis.md` | **Auditing what is and isn't built, or handing the project to a second reviewer.** Every mechanic classified as shipped / unverified / stub / missing / open-decision, with the four questions most worth a second opinion at the top. Start here after this file. |
| `Findings_2026-08-06.md` | Background: architecture, the Avatar Health investigation, and a table of proposals already rejected and why. Superseded on "what's not built" by the gap analysis above. |
| `BRIEF_HomePage_MainMenu.md` | Building the home screen. Fully specified, zero combat dependency — the best parallel task. |
| `BRIEF_VFX_SpriteSheets.md` | Commissioning animated VFX. For an image-gen AI or artist; no code. |
| `V4_Blueprint_Review.md` | Reviewing an external design proposal — shows the flaws found in the last one. |
| `Combat_Design_V4_Counter_Proposal.md` | Background on why the current combat rules are what they are. |
| `V4_Blueprint_Fixes.md` | Per-issue fixes proposed for the V4 blueprint. |
| `balance_sim_reference.py` | **Historical only — do not trust its numbers.** See §5. |

---

## 4. State of play

**Working and tested (48 tests):** Formation → Combat → Resolved match flow; lethal lane combat
with overflow; 12-clash cap with Health-fraction tie-breaker; escalating damage (x6 / x9 / x12);
Back-lane Energy; lane-sum elemental advantage; slot weighting (rarity 5-7 take two slots);
formation synergy; reinforcement windows at clashes 4 and 8; AI difficulty tiers and archetypes;
the lane picker; story and tutorial overlays.

**Built but unverifiable by the suite** — anything driven by `Update()` or a coroutine, which
Unity's EditMode runner cannot execute: the combat timer, all animations, and the story/tutorial
overlays. These need manual play testing.

**Added 2026-08-06 (this session):** a real save system (`Assets/Scripts/Save/`) — versioned JSON
at `Application.persistentDataPath`, atomic write-then-swap, corruption quarantine, migration path,
and a guard against overwriting a profile written by a newer build on the other machine.
Progression, match record and story/tutorial state now survive a restart. Also the exposed-Avatar
**siege rule**, built and measured but **switched off** pending a design decision — see
`Mechanics_Gap_Analysis.md` §1.1.

**Not built:** home screen, deck builder, collection screen, card levels, economy/inventory,
Castle/Barracks/Gate levelling, cloud save, multiplayer/guild, audio. Full breakdown with
severities in `Mechanics_Gap_Analysis.md`.

---

## 5. The most important lesson from this session

Balance was argued from an external Python model. **The model was wrong**, and it was wrong in a
way that looked completely plausible: it predicted ~89% knockouts at 400 Avatar HP; the real game
produced ~50%, reproducibly. The cause was that the model filled all nine board slots while
ignoring Resource cost, so its boards were bigger than any real board.

The fix was to move the simulation *inside* Unity (`Assets/Tests/Editor/BalanceSimulationTests.cs`)
where it drives the real `LaneBattleResolver` and `CardDatabase`. **Simulate in-engine. A replica
of the combat maths drifts from the game the moment it omits a constraint, and it will omit one
silently.**

---

## 6. Open decisions — none of these are settled

1. **Knockout rate is ~51%, target is >70% — now diagnosed, with a fix built and measured but
   switched off.** The cause is structural, not numeric: overflow only reaches an Avatar through a
   lane with no living defenders, so once *both* boards are wiped, total Attack on both sides is
   zero and no damage of any kind is possible. The match can only run out the clock.
   `LaneBattleResolver.ExposedAvatarSiegeEnabled` (default `false`) makes an empty board bleed 6%
   of its own Avatar pool per tick from tick 7, taking the mid profile to 83% knockouts with match
   length unchanged. **Adopting it is a one-line change and a real design decision** — full
   measurements, the two tuning findings, and the rejected alternatives are in
   `Mechanics_Gap_Analysis.md` §1.1.

2. **Stat scale (x1 vs x10).** Card stats are 1-12 today. A x10 rescale is proposed. The usual
   justification ("buildings need granular bonuses") does **not** hold, because buildings are
   forbidden from granting flat card stats. The real argument is percentage effects. If adopted it
   must be atomic across six systems — see `Findings_2026-08-06.md` §4.1.

3. **Rarity curve.** The proposed x10 curve makes wide and tall boards *equal* on raw stats, not
   "wide wins by 36%" as claimed. High rarity wins on Resource efficiency, low rarity on slot
   efficiency. Defensible, but the stated justification is wrong and should be corrected.

---

## 7. Conventions

- Comments explain **why**, not what. Many record a bug that was actually hit.
- Every behavioural change gets an EditMode test.
- **Assert relationships, not magnitudes** — a test that hardcoded a constant once broke on a
  legitimate balance change and looked like a regression.
- Put real logic in plain testable methods; let MonoBehaviours supply only timing. See
  `BattleController.AdvanceCombatTick()`.
- Legacy uGUI only. Procedural UI, no scenes or prefabs. `Initialize()`, not `Awake()`.
  `DestroyImmediate()`, not `Destroy()`, in anything reachable from `Initialize()`.

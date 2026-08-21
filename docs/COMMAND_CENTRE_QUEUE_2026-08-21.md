# Command Centre Control Board — 2026-08-21

**Role:** System Architect / Command Centre  
**Project:** `C:\Users\zihan\Downloads\MyriadOfDragonsUnity`  
**External Bible:** `C:\Users\zihan\Documents\Codex\2026-08-11\referenced-chatgpt-conversation-this-is-an\COMMAND_CENTRE_HANDOVER_2026-08-21.md`  
**Status:** Tick feed ACCEPTED (text OK). Animation of feed = **post-MVP polish** (do not start now). ASAP release path = commit Group A → Android smoke → ship Chapter 1 loop ugly-but-playable → then UI/animation pass.

---

## 0. Credit wartime map (active)

| Seat | Budget now | Use for | Do not use for |
|---|---|---|---|
| **Claude** | Remaining weekly only | Release blockers in Battle only (see §3) | Combat animation polish, Home/Deck art |
| **ChatGPT** | Idle | Release checklist copy if asked | Unity / anim briefs that delay ship |
| **VS Code** | Idle | **After** Group A committed + playable APK | Home/Deck polish, feed→animation |
| **Cursor Command Centre** | Ration | Release sequencing + commit when asked | Building animations now |

**Long-term Bible ownership still stands** (Claude = Battle, VS Code = Metagame UI, ChatGPT = Design).

---

## 1. Authority order (read this, ignore the rest)

1. `COMMAND_CENTRE_HANDOVER_2026-08-21.md` (ops / vertical slice)
2. `HANDOVER_MYRIAD_OF_DRAGONS_2026-08-16.md` (ownership, UI gates, asset register)
3. `docs/MOS_v1.1.md` (constitution; §19 live change log)
4. `UI_RENDERING_FORENSIC_DIAGNOSIS_2026-08-21.md` (UI blocked until primitives)
5. `MVP_RELEASE_GATE_2026-08-18.md` (release sequence)

**Historical / do not implement from:** `docs/UI_Architecture_v1.md` (portrait-era), Battle V3–V6 packets, `docs/MOS_v1.2.md` (not promoted over v1.1), duplicate Codex `work/docs_update/*` copies, old `SESSION_HANDOFF_2026-08-07.md`.

**Parked worktrees:** `C:\Users\zihan\Downloads\MOD_BattleV7` (detached `c13882c`, V7 only when capacity + clean root). Do not merge into main dirty tree casually.

---

## 2. Dirty tree — sorted feature groups

`main` is **29 commits ahead** of origin + large uncommitted working tree. **Do not reset/discard.** Group before any commit.

### Group A — Chapter 1 vertical slice + balance (KEEP / near-commit ready)

**Intent:** Playable campaign loop, stamina, decks, shop integrity, guidance, balance audit.

| Area | Files |
|---|---|
| Battle / economy knobs | `PlayerEmpireData.cs` (Turn1 0.6; win **+1** / loss **+0**), `LaneBattleResolver.cs` (overflow **×4**, OT 6/8), `PlayerBattleState.cs` (shuffle seed), `SimpleAIOpponent.cs`, `AIOpponentScaling.cs`, `BattleController.cs` (minor), `CurrencyManager.cs` |
| Campaign / metagame flow | `CampaignMapPresenter.cs`, `CampaignStageData.cs`, `HomePagePresenter.cs`, `ShopPresenter.cs`, `DeckBuilderPresenter.cs` (functional), `GameBootstrap.cs` (large — contains A + parked B/C) |
| Save | `PlayerProfile.cs`, `SaveMigration.cs` |
| Docs | `docs/MOS_v1.1.md`, `docs/MVP_COMBAT_PROGRESSION_CONSTITUTION_2026-08-21.md` |
| Tests (untracked + modified) | All `Chapter1*`, `Campaign*`, `Shop*`, `NormalBattle*`, `AIFormation*`, `AcquiredCard*`, `ReleaseProfile*`, `FreshProfile*`, `TutorialGuidanceTests`, related modified tests |

**Validation already run:** focused balance/playability/AF suite **121/121**.  
**Human gate before commit:** manual Home → Story → 1-1 → AF → Battle → Home → 1-2.

### Group B — Parked Battle presentation / music / cinematics (DO NOT COMMIT with A)

| Item | Notes |
|---|---|
| `GameBootstrap.cs` Battle Release Layout / presentation churn | Mixed into same file as A — needs isolation or explicit accept before commit |
| `Assets/Resources/Audio/` + `BattleMusicTests.cs` | Parked per Bible |
| `Assets/Resources/Cinematics/` + `CinematicSequence.cs` + `ChapterOneCinematicTests.cs` | Parked until Battle visual acceptance |
| `BattleReleaseLayoutTests.cs`, `TutorialHandDockGeometryTests.cs` | Presentation-adjacent |

**Rule:** No commit of A that silently ships B’s visual/music/cinematic behaviour without Command Centre sign-off.

### Group C — Rejected / staging UI assets (DO NOT COMMIT)

| Item | Notes |
|---|---|
| `Assets/Resources/UI/HomeV3/` | Staging only; forensics gate not passed |
| Backdrop candidate/legacy backups | Keep uncommitted |
| `HomeReleaseGateTests.cs`, `DeckBuilderReleaseGateTests.cs` | UI gate tests — park with C |
| Modified city backdrop binary | Treat as UI; freeze |

### Group D — Social / CloudCode (OUT OF MVP — quarantine)

| Item | Notes |
|---|---|
| `Assets/Scripts/Social/` + Social* tests | Not MVP |
| `CloudCode/` (278 files) | Backend; do not mix into vertical-slice commits |
| `docs/SocialSafety_*`, Guild docs | Design-only / deferred |

### Group E — Repo hygiene (safe cleanup, no feature value)

| Item | Action |
|---|---|
| `*.log`, `*-results.xml`, `chapter1-balance-*`, `home-*.log`, `full-*.log`, `test*.log` | Add to `.gitignore`; delete from working tree when convenient |
| `.utmp/` | Ignore |
| `.claude/` | Local agent state — ignore, do not commit |
| `Assets/_Recovery/` | Quarantine; do not delete without approval |
| Root test junk | Same as logs |

### Group F — Docs to classify (no code)

| Keep as live | Archive / historical label |
|---|---|
| `MOS_v1.1.md`, `MVP_COMBAT_PROGRESSION_CONSTITUTION_2026-08-21.md`, `AI_CONTRIBUTING.md`, `Shop_V1_Release_Contract.md`, `Battle_Mechanics_Summary.md` (historical numbers — prefer constitution where they conflict) | `UI_Architecture_v1.md`, `Design_Session_Battle_UI_*`, `V4_*`, `Combat_Design_V4_*`, `SESSION_HANDOFF_2026-08-07`, `MOS_v1.2.md` (until explicitly promoted), duplicate Codex `work/docs_update` copies |

---

## 3. ASAP release path (shortest)

**Definition of “released” for now:** fresh install → Tutorial → Deck confirm → Story 1-1/1-2/1-3 win → rewards persist → Return to City. Ugly UI OK. Guild/Social/Evolution/marketplace **out**.

| Order | Task | Who | Why |
|---|---|---|---|
| **1** | `commit Group A` (functional vertical slice + tests; **exclude** HomeV3 / Social / CloudCode / backdrop candidates) | Cursor when you say **commit Group A** | Uncommitted work is the #1 ship risk |
| **2** | One Android/dev build smoke of that loop | You | EditMode ≠ device |
| **3** | Fix only **release blockers** found in smoke (crash, softlock, unwinnable, save wipe) | Claude Battle / Metagame as owned | No polish |
| **4** | Optional: spell “can cast” hint (tiny Battle UX) | Claude if credit left | Cheap; not required to ship |
| **5** | UI polish + **tick-feed → animation** | VS Code / Claude later | After shippable loop exists |

**Explicitly NOT next:** converting activity rail to animation, HomeV3, Battle V7 merge, Collection Evolution, Social.

### DONE recently
Tick feed (text), constitution, Chapter 1 AF curve, post-victory story, isolation audit.

---

## 4. Open decisions (escalate, don’t code)

1. Battle visual authority: Release Layout vs V7.  
2. Whether `GameBootstrap` must be split before next Battle UI pass (recommended; not this Claude slice unless required for isolation).  
3. Currency naming (`eventMedals` vs Event Tokens).  
4. Collection schema / Evolution curves — blocked on MOS decision.

---

## 5. PASTE-READY CLAUDE TASK (credit burn — DO THIS NOW)

```text
You are the Battle seat for Myriad of Dragons (Unity 6000.5.6f1).
Project: C:\Users\zihan\Downloads\MyriadOfDragonsUnity

READ FIRST:
1) docs/COMMAND_CENTRE_QUEUE_2026-08-21.md §3 ASAP release path
2) docs/MVP_COMBAT_PROGRESSION_CONSTITUTION_2026-08-21.md (LOCKED — do not retune)
3) docs/AI_CONTRIBUTING.md

CONTEXT:
Command Centre is committing Group A (functional Chapter 1 slice). Tick feed text is ACCEPTED.
Owner wants ASAP release. Use remaining weekly credit on Battle release UX that helps casting.

OBJECTIVE (ONE slice only):
Spell affordability hint during Combat — when the player can afford at least one Avatar spell,
surface a clear existing-UI cue so Combat is not “watch numbers and guess”.

REQUIREMENTS:
1) Prefer reusing existing spell rail / spell button / caption surfaces in GameBootstrap.
   Do NOT invent a new screen. Do NOT animate. Do NOT redesign layout.
2) Logic must be plain/testable (can-cast derived from real Energy + spell costs), not Update()-only.
3) Show during Combat only; clear/hide on Formation / Resolved / new match.
4) Tutorial + normal + Campaign all covered via existing RefreshAll path if possible.
5) Do NOT change: AvatarDamageMultiplier, Turn1ResourceFraction, siege %, stage decks, +1/+0 levels,
   MatchResult / OnMatchCompleted, CombatFeedFormatter wording except if a one-line cross-link is needed.
6) Do NOT edit: HomePagePresenter, CampaignMapPresenter, ShopPresenter, DeckBuilderPresenter,
   PlayerProfile/SaveMigration, Story/*, Social/*, CloudCode/*, HomeV3.

METHOD:
1) If Unity is open: BLOCKED: Unity is open — stop. Never kill Unity.
2) Grep spell UI / Energy display / TryCastSpell before coding.
3) Add focused EditMode tests for: can-cast true/false, clears on new match, constitution numbers unchanged.
4) Run filter (Unity closed):
   CombatTickFeedTests|BattleLogicTests|TutorialEncounterWinTests|NormalBattleAutoFormationTests|Chapter1CampaignPlayabilityTests
   (+ your new fixture)
5) Report real counts. MOS §19 one row only if green.
6) Do NOT commit. Do NOT push.

DONE WHEN: focused suite green + short summary of cue location and files touched.
```

---

## 5b. PASTE-READY CLAUDE TASK #2 (after #5 returns green, if credit remains)

```text
You are the Battle seat. Same project.

OBJECTIVE: Chapter 1 release regression gate — run ONE focused EditMode filter covering the shippable loop and fix ONLY real reds you introduce or that block ship (no drive-by refactors).

Filter:
ChapterOneProgressionTests|ChapterOnePostVictoryStoryTests|Chapter1CombatBalanceAuditTests|Chapter1CampaignPlayabilityTests|CampaignStaminaEntryContractTests|CampaignMatchContextLifecycleTests|NormalBattleSavedDeckIntegrationTests|CombatTickFeedTests|BattleLogicTests|ShopCurrencyIntegrityTests|ReleaseProfilePersistenceContractTests

Rules: Unity closed; no commit; no Home/Deck visual work; no constitution retunes; stop after one green run; report counts.
```

---

## 6. PASTE-READY CHATGPT TASK (parallel, no Unity)

```text
You are Design Director / Command Centre support for Myriad of Dragons. Do not write Unity code.

Using:
- COMMAND_CENTRE_HANDOVER_2026-08-21.md
- HANDOVER_MYRIAD_OF_DRAGONS_2026-08-16.md (authority matrix + asset register)
- UI_RENDERING_FORENSIC_DIAGNOSIS_2026-08-21.md
- docs/COMMAND_CENTRE_QUEUE_2026-08-21.md

Produce TWO short decision packets only:
1) Battle presentation authority: recommend Release Layout (current main tree) OR V7 clean worktree — one choice, with commit implications for dirty Group B.
2) Phase 2 UI preflight brief for VS Code: exact six primitive proofs + manifest fields required before any Home/Deck presenter edit. No new images. No code.

Stop after the two packets. Do not regenerate art.
```

---

## 7. What Command Centre already completed (do not redo)

- Structural review + Phase 1 systemic balance + constitution (×4, +1 win / +0 loss)  
- Chapter 1 AF curve + unlock repair + post-victory story  
- Group B isolation audit (music/cinematics)  
- Dirty-tree / credit queue board  

**Next human action:** paste §5 into Claude **now** (credit burning).  
**Cursor:** wait for Claude return → review only.  
**ChatGPT / VS Code:** idle.

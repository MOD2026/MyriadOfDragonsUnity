# Session Handoff — 2026-08-07

**Purpose:** this conversation's context window is about to run out. Paste this whole file at the
start of the next session so Claude picks up exactly where this one left off — operating mode,
review history, and one confirmed-outstanding blocking item that shipped anyway.

---

## 1. Operating mode currently in effect

The user has set a standing instruction for this project (not yet superseded — carry it forward
until told otherwise):

> **May, without asking first:** read any file, grep/search the repo, inspect `git status`/`log`/
> `diff`/`show`, run the Unity EditMode suite, run validation/build commands, compare commits,
> inspect logs.
>
> **May NOT, without explicit instruction each time:** modify any file, commit, push, or update
> documentation — *unless* explicitly assigned an implementation task or explicitly asked for docs
> (this handoff file itself was explicitly requested, which is why it exists).
>
> **Default role: independent reviewer and architecture validator.**

Practical effect: the last several turns of this session were code reviews of GitHub
Copilot-implemented tasks (via VS Code), submitted for Claude to review against MOS v1.1 and
report Critical Issues / Recommended Improvements / Verdict — Claude does not implement metagame
work, only reviews it and continues battle-side work when explicitly asked to resume it.

## 2. Governance state

- **`docs/MOS_v1.1.md`** is the approved, frozen governing document (master copy is a `.docx` on
  Google Drive, same WIP folder as v1.0 — this repo file is the working mirror so every AI seat
  can actually read it locally). Tagged in git as `mos-v1.1-governance-frozen`.
- AI ownership per MOS §15: Game Director = the user, System/Design Architect = ChatGPT, Battle =
  Claude, Metagame systems + bulk content = Gemini. **GitHub Copilot** was onboarded this session
  as a fourth seat, scoped as an **implementation assistant** (not an architecture owner) via
  `.github/copilot-instructions.md` — currently executing MOS's P1 priority queue one task at a
  time, each submitted for Claude review before merge.
- `CLAUDE.md` / `GEMINI.md` both point to MOS v1.1 as governing; both were corrected this session
  to the current test baseline and to mark the AI-spell-casting and level-1-onboarding decisions
  resolved (they'd gone stale, still listing closed decisions as open).

## 3. Git / repo state (verified at handoff time, not from memory)

- Remote `origin` → `https://github.com/mod2026/MyriadOfDragonsUnity.git` — connected, but
  **local `main` is 4 commits ahead of `origin/main` and none of them have been pushed.** Nobody
  has been asked to push; don't push without explicit instruction.
- Working tree is clean — nothing uncommitted as of this handoff.
- Commit log, newest first:
  ```
  2604bf8 Implement P1 Owned Card Deck Builder          <- reviewed, APPROVED, no blocking issues
  fc99ad4 Implement P1 Shop Acquisition Pipeline         <- reviewed, shipped with a KNOWN UNFIXED
                                                             blocking issue, see section 4 below
  d8bbe9a Implement P1 Collection Screen + home nav      <- reviewed, APPROVED with minor notes
  86e901f Governance: MOS v1.1, Copilot onboarding, sync all three AI context files
  575f988 Initial commit - Myriad of Dragons codebase    <- NOT Claude's commit; made via VS Code
                                                             mid-session (Gemini's Story-system
                                                             rebuild + part of an archetype-sweep
                                                             test). Verified safe (81/81) before
                                                             building on top of it.
  15cea27 Formally resolve the AI-spell-casting decision and the level-1 doc entry
  317e8b5 Level-1 onboarding HP taper, and fix a Story-system seam break
  b64cbce Campaign map: read stage unlock status from the save file
  ```
- **Golden Baseline: 81/81 EditMode tests passing**, last confirmed immediately after the Deck
  Builder review (this handoff).

## 4. Outstanding item — confirmed still unfixed, was flagged as blocking

**`Assets/Scripts/UI/ShopPresenter.cs`, lines ~54 and ~59** — the shop's on-screen item copy still
promises more than the acquisition pipeline delivers, and one promise describes a reward type that
doesn't exist as a grantable object in this game at all:

| SKU | Description on screen | Actually granted |
|---|---|---|
| `pack_novice` | "Contains **3** basic warrior & strategist cards." | 1 card (`"warrior"`) |
| `pack_dragon` | "Guaranteed **1 Epic Dragon card & 2 Rare spells**." | 1 card (`"dragon"`), no spells |

Avatar spells (Firestorm/Mend/War Cry/Divine Bolt) are a fixed, universal kit every player already
has — there is no per-player collectible "spell" object this pipeline (or any current system)
could grant, so "2 Rare spells" isn't just an under-delivery, it's describing something structurally
impossible to fulfil as written. This was raised explicitly in the Shop Acquisition Pipeline review
as a blocking issue before merge. **Verified at handoff time: the commit shipped without this fix.**
Re-raise this at the start of the next session rather than assuming it was addressed.

## 5. Other open items carried forward (not new, just re-listing so nothing gets lost)

From `docs/MOS_v1.1.md` §19/§20, still genuinely open:

- Currency naming conflict: code uses `eventMedals`/`dragonRelics`; `Economy_Blueprint.md` uses
  "Event Tokens"/"Market Credits". Pick one canonical name before wiring further.
- Evolution/Limit Break curves — undefined, needs simulation before coding (same discipline as the
  siege rule and onboarding taper both went through).
- 1–12 vs. x10 stat scale — still open, still not urgent.
- Whether "initiative" has any place in the current simultaneous-combat model — open.
- **Deck Builder's *active deck* still isn't persisted** to `PlayerProfile.activeDeckCardIds` (the
  just-approved change fixed the *owned-cards collection panel* only — reading/writing the actual
  built deck was out of scope for that task and remains a separate, already-tracked MOS gap).

Already resolved this session, do not re-litigate: AI opponent will not cast spells (asymmetric by
design, documented in MOS §6/§8); level-1 onboarding Health taper adopted and tested; exposed-Avatar
siege rule adopted at 6%. The archetype-weighted-selection-by-difficulty *proposal* (from the
n=2000 deep sweep) was explicitly measured but **not implemented** pending review — still awaiting
a go/no-go, not shipped either way.

## 6. Where everything else lives

`docs/MOS_v1.1.md` (governing doc, read first) · `docs/AI_CONTRIBUTING.md` (ownership/FROZEN list)
· `docs/Metagame_Handoff.md` · `docs/Battle_Mechanics_Summary.md` · `docs/Economy_Blueprint.md` ·
`CLAUDE.md` / `GEMINI.md` / `.github/copilot-instructions.md` (per-seat auto-loaded context).

**Test command** (Unity must be fully closed first, never pass `-quit`):
```
"C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\zihan\Downloads\MyriadOfDragonsUnity" -runTests -testPlatform EditMode -testResults "results.xml" -logFile "run.log"
```

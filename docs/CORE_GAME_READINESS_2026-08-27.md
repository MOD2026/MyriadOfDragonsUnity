# Core game readiness — theory before code

**Owner instruction, 2026-08-27: "we don't need to work on the event now... i am to work on the theory
and make sure everything stick and work before coding. u don't even have the basic game why are u even
starting on the event coding. learn u crawl before u walk and fly. theory first."**

**The instruction is correct and the criticism lands.** I was running 20,000-trial balance simulations
for Chest Hunt — event 1 of 9 — and dispatching in-engine simulation work, while nine features refuse
every action a player can take and the core loop has unverified holes. **All event work is HALTED.**

---

## What is actually REAL

The core loop closes. This is verified in code, not assumed:

| Piece | Evidence |
|---|---|
| Battle resolves | `BattleController.AdvanceCombatTick()`, tick-based, testable |
| Match reports out | `OnMatchCompleted` → `MatchResult` (frozen contract) |
| Result is recorded | `_profile.RecordMatchResult(playerWon)` at `GameBootstrap.cs:6016` |
| Progression applies | `PlayerEmpireData.ApplyMatchResult` — ResourceCap/HP/deck size |
| It persists | `SaveSystem` / `SaveMigration` (frozen) |
| Deck building | `DeckBuilderPresenter` 974 lines |
| Collection | `CollectionPresenter` 1,067 lines |
| Campaign | `CampaignMapPresenter` 2,761 lines |
| Tutorial | `Assets/Scripts/Tutorial/` — contracts + validation |
| Puzzle | 2,579 lines across 9 files, reachable via Empire's War Room |

**So the crawl exists.** Battle → result → progression → deck → battle is a closed, persisted loop.

## What is a SHELL — refuses every player action

Nine features present a UI and decline to do anything. Each has an `*OpenValues` class whose methods
return `Refuse(...)` / `NotLocked`:

| Feature | Refusal sites |
|---|---:|
| Bazaar | 6 |
| Friends | 6 |
| ChatSocial | 5 |
| MailInbox | 5 |
| BattlePass | 3 |
| GuildHallEntry | 2 |
| MemoryExpedition | 2 |
| VipSubscription | 1 |
| DailyLoginQuests | 1 |

**These are not unbuilt. They are built and switched off, waiting on numbers nobody decided.** The
Bazaar is the clearest case: real server-authoritative `ListItemAsync`/`BuyItemAsync`/`CancelListingAsync`
through Cloud Code, a 528-line presenter, and every action refused.

**This is the theory debt.** Nine features' worth of undecided numbers, not nine features' worth of
missing code.

## What is BROKEN in the core, right now

Measured tonight, not inferred:

1. **Resource pills render garbled** — label and value overlap in `HomeV3UiLibrary.CreateResourcePill`,
   used by Home, Collection, and Empire. **Three or four screens, the most-seen ones.**
2. **227 labels are under the 22px font floor.** Legibility, not aesthetics.
3. **81 labels fail WCAG AA contrast**; 132 have a glyph pixel below AA.
4. **8 EditMode tests fail**, 3 of them TacticalPuzzle — the most-shipped event.
5. **Build is 641 MB against Play's 150 MB cap.** Root-caused: zero Android texture overrides, one
   64 MB icon. Fixable by import settings.
6. **The AI difficulty ceiling is 64.9%** and three of four AI archetypes have never shipped —
   every real match a player has ever fought is `Balanced`.

## The honest sequence

**Crawl (now):** fix what is broken in the loop players already touch — pills, font floor, failing
tests, build size. Nothing here needs a design decision; it is all measured and actionable.

**Walk (next):** decide the numbers for the shells that make the game a *game* rather than a battle
simulator — DailyLoginQuests, BattlePass, MailInbox first, because they are the retention spine and
have the fewest refusal sites. **Theory work, not coding.**

**Fly (later):** Bazaar, then events. **Bazaar before events**, because the owner has stated trading is
the key of the game and it is a TCG — and because events feed a collection whose exchange layer does not
work yet.

**Events are last, not first.** Chest Hunt's area table was re-derived tonight against a measured 42–65%
band and is recorded for when it is wanted. **It is not wanted yet.**

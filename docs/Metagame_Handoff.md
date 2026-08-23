# Myriad of Dragons — Metagame Handoff

> **2026-08-11 authority update:** Read `MOS_v1.2.md` and
> `Guild_Competition_Rewards_v1.md` before using this older handoff. Guild identity, chat,
> one-to-one direct messaging, R1–R5 governance, donations, help, research, store, leagues,
> achievement rankings, rewards and appointed offices are now Phase 1. Any statement below that
> places guilds at P3, calls multiplayer entirely absent, or cites 78/78 as the current result is
> historical. Protected Git baseline is 81/81; latest verified social-contract working-tree result
> is 100/100.

**Written 2026-08-07, for an AI picking up metagame work (home screen, shop, campaign map, deck
builder, collection, economy UI) with no prior context on this project.** Battle mechanics (combat,
cards, AI opponent, progression math) is a separate collaborator's domain and is only covered here
at the integration points the metagame actually touches.

**Read this whole document before writing or changing anything in `Assets/Scripts/Save/` or
`Assets/Scripts/Economy/`.** §7 explains why that specific warning exists.

---

## 1. Project basics

| | |
|---|---|
| **Project root** | `C:\Users\zihan\Downloads\MyriadOfDragonsUnity` — work here, never on the Google Drive copy (`G:\My Drive\MOD\...` is a sync target only; opening Unity from it has broken the project three separate ways — MAX_PATH, sync-hydration gaps, file locks). |
| **Unity version** | 6000.5.6f1 exactly. |
| **UI framework** | Legacy uGUI, procedural — **no scenes, no prefabs.** Every screen is built at runtime in C# (`new GameObject(...)`, `AddComponent<...>()`). This is a deliberate project convention, not a placeholder — follow it. |
| **Test suite** | Protected Git baseline 81/81; latest verified social-contract working-tree result 100/100, pending clean isolation. |

Run the suite (Unity must be fully closed first — it holds an exclusive project lock):

```
"C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\zihan\Downloads\MyriadOfDragonsUnity" -runTests -testPlatform EditMode -testResults "results.xml" -logFile "run.log"
```

Never add `-quit` — it silently no-ops the run. Parse `results.xml` for pass/fail; grep the log for
`error CS\d+` or `Aborting batchmode` to catch a compile failure before trusting the results file.

---

## 2. What already exists — don't rebuild these, extend them

All under `Assets/Scripts/UI/`, all procedural uGUI, all functional enough to click through today:

| File | What it does today |
|---|---|
| `HomePagePresenter.cs` | Top HUD (name/level/gold/gems/energy), bottom nav dock (Story/Cards/Shop/To Battle), tutorial dialogue overlay, subscribes to `BattleController.OnMatchCompleted` to award match rewards. |
| `CampaignMapPresenter.cs` | 3 hardcoded stages (`1-1`, `1-2`, `1-3`), stage detail modal, "Launch Battle" button. |
| `ShopPresenter.cs` | 4 hardcoded SKUs (card pack, dragon booster, gold vault, energy potion), purchase flow with affordability checks. |
| `DeckBuilderPresenter.cs` | 8 hardcoded cards, tap-to-add/remove deck list, 20-card cap. |

**All four are genuinely rough** — hardcoded data, not yet reading from the actual save file in
several places (see §6). They're real starting points, not stubs to throw away.

---

## 3. The current Save/Economy API — read this section precisely, it has changed shape several times

### 3.1 `PlayerProfile` (`Assets/Scripts/Save/PlayerProfile.cs`)

The **entire save file** is one flat, `[Serializable]` class — JsonUtility serialises it directly,
there is no separate "SaveData" wrapper (an earlier version of this project had one; it was removed
in favour of this flatter shape). Current fields, exactly as they exist right now:

```csharp
// Identity
public string playerName = "Sovereign";
public int level = 1;

// Currencies (see §4 for what each is FOR)
public int gold = 1000;
public int gems = 150;
public int eventMedals = 0;
public int guildContribution = 0;
public int dragonRelics = 0;
public int stamina = 100;
public int maxStamina = 100;

// Progression materials (not yet consumed by anything)
public int dragonEssence, heroSouls, evolutionStones, limitCores, skillTomes;

// Onboarding
public bool hasSeenIntro;
public List<string> seenChapters;

// Campaign / decks / inventory
public List<string> unlockedStageIds = new() { "1-1", "1-2" };
public List<string> activeDeckCardIds = new() { "c1", "c3", "c4", "c6" };
public List<string> cardCollection = new();
public List<TradeableAssetInstance> inventoryAssets = new();

// Empire/base progression (battle-side, feeds match economy)
public int avatarLevel = 1, castleLevel = 1, barracksLevel = 1, gateLevel = 1;

// Match history
public int winStreak, totalMatches, totalWins;

// Also available: PlayerName/Level/Gold/Gems/Stamina/MaxStamina/EventMedals/
// GuildContribution/DragonRelics as capitalised properties wrapping the fields above -
// both spellings work, use whichever matches the surrounding file's style.
```

Key methods: `Save()`, `AddGold(int)`, `AddGems(int)`, `TrySpendGold(int)`, `TrySpendGems(int)`,
`UnlockStage(string)`, `RestoreStamina(int)`, `MarkChapterSeen(string)`, `HasSeenChapter(string)`,
`RecordMatchResult(bool isVictory)` (battle-side, don't call this from metagame code).

**`TrySpendGold`/`TrySpendGems` exist but nothing outside these two methods enforces "don't go
negative" on direct field writes** — `profile.gold -= 500` without a balance check is how the
existing `ShopPresenter.cs` does it today, which works only because it checks affordability first.
Prefer `TrySpendGold`/`TrySpendGems` in new code; they're the same amount of typing and they're
safe by construction.

### 3.2 `SaveSystem` (`Assets/Scripts/Save/SaveSystem.cs`)

```csharp
SaveSystem.CurrentProfile   // the ONE shared, live profile for the running session (lazy-loaded)
SaveSystem.Profile          // identical - same underlying singleton, two names, both used elsewhere
SaveSystem.Save(profile)    // static, atomic write-then-swap, returns bool
SaveSystem.Load() / Load(out SaveLoadStatus)
SaveSystem.Exists / SaveSystem.Delete()
```

**Always read/write through `SaveSystem.CurrentProfile`, never `PlayerProfile.LoadOrCreate()`
directly**, unless you specifically want a disposable, non-shared copy (tests only). Two
independently-loaded copies of the profile in the same session is the exact bug this singleton
exists to prevent — one system's change becomes invisible to another until a restart.

It has real safety behaviour worth knowing about: atomic write (a crash mid-save can't truncate the
file), corrupt/unreadable saves are quarantined (renamed aside, never deleted) rather than silently
replaced, and a scratch-directory override for tests (`OverrideRootDirectoryForTests`/
`ClearRootDirectoryOverride`) so tests never touch a real machine's save file.

### 3.3 `SaveManager` (`Assets/Scripts/Data/SaveManager.cs`)

Thin façade some UI code uses instead of the `Save` namespace directly:

```csharp
SaveManager.SaveData   // -> SaveSystem.CurrentProfile (same object, different name, again)
SaveManager.Load()     // touches CurrentProfile to force the lazy load
SaveManager.Save()     // -> SaveSystem.Save(SaveSystem.CurrentProfile)
```

### 3.4 `CurrencyManager` (`Assets/Scripts/Economy/CurrencyManager.cs`)

A currency-type-agnostic API over the same profile, useful when code needs to handle "whichever
currency" generically rather than hardcoding `.gold`/`.gems`:

```csharp
CurrencyManager.GetBalance(CurrencyType.Gold)
CurrencyManager.AddCurrency(CurrencyType.DragonRelic, amount)
CurrencyManager.SpendCurrency(CurrencyType.Gems, amount)
CurrencyManager.ExecutePlayerTrade(seller, buyer, TradeableAssetInstance asset, int relicPrice)
```

`CurrencyType` and `TradeableAssetInstance` live in `Assets/Scripts/Economy/CurrencyDefinitions.cs`.

### 3.5 A currency system that is DEAD — do not use it, do not extend it

`Assets/Scripts/Items/ItemDatabase.cs` and `Item.cs` define a **completely separate**,
**disconnected** set of currencies (`ItemId.Gold`, `.Food`, `.DragonCoin`, `.Gem`, `.Flag`, ...)
from an earlier design pass. Nothing reads or writes through it — it's dead code that happens to
still compile. The canonical currency system is §3.1–3.4 above. If you see `ItemDatabase` referenced
anywhere, that reference is stale; don't add new ones. It's a reasonable candidate for deletion, but
that's not this handoff's call to make unilaterally.

---

## 4. Economy design — the agreed principles, so new work doesn't drift from them

Full reasoning in `docs/Economy_Blueprint.md`; the load-bearing rules, condensed:

| Currency | Role | Cashes out? |
|---|---|---|
| **Gold** | Soft currency, earned by play | Never |
| **Gems** | Hard currency, real-money IAP | Never |
| **Event Medals** | Seasonal, event-only, expires | Never, non-tradeable |
| **Guild Contribution** | Reputation/bound currency | Never, non-tradeable |
| **Dragon Relics** | Player-to-player trade medium | Not in the current phase — see below |

**Hard rules, settled after extended discussion, not open for casual re-litigation:**

1. **Identity and Progression never transfer between accounts. Only Property (tradeable items) may
   ever move.** Account/character/reputation/history stays put, always.
2. **Money buys speed, never exclusive competitive power.** A whale and an F2P player can own the
   same card; the whale just got there faster.
3. **Event-exclusive rewards can never appear in the shop, in any bundle, at any price.** This
   should be enforced by a query-level filter on shop inventory, not by content-author discipline.
4. **No real-money cash-out path exists anywhere in this project, in this phase.** No withdraw
   button, not even a disabled one — a disabled button still signals "this has real value" to a
   player and to app-store review. `dragonRelics` is a closed-loop player-trade currency for now;
   if it ever gains a cash-out path, that's a Year-2/3 decision gated on legal review, not a UI
   feature to add speculatively.
5. **No account-transfer/account-selling feature, official or otherwise.** Not in scope, ever,
   without a separate explicit decision.
6. **Every new reward/currency faucet gets a "how would a bot farm this" pass before shipping.**

---

## 5. The battle → metagame integration contract

The battle layer is a separate system (`Assets/Scripts/Battle/`, `GameBootstrap.cs`) that the
metagame calls into and listens to. The two integration points that matter:

### 5.1 Getting a result back from a match

```csharp
BattleController.OnMatchCompleted  // event Action<MatchResult>
```

```csharp
public readonly struct MatchResult
{
    public readonly bool IsVictory;
    public readonly int TicksTaken;
    public readonly int PlayerHealthRemaining, PlayerMaxHealth;
    public readonly int EnemyHealthRemaining, EnemyMaxHealth;
    public readonly string OutcomeReason; // empty for a straight knockout, populated for a tick-cap decision
}
```

Deliberately carries **no reward amount** — the battle layer has no economy to source one from.
Deciding what a win is worth (gold/gems/relics) is entirely the metagame's job. `HomePagePresenter.
HandleMatchCompleted` is the existing example of consuming this event.

### 5.2 Showing/hiding the battle screen

```csharp
GameBootstrap.Instance?.SetBattleCanvasVisible(bool visible)
```

The battle UI is a persistent canvas that gets shown/hidden, not instantiated/destroyed per match.
Use this rather than searching the scene for a Canvas component — `BattleController` has no Canvas
ancestor, so that search returns null (this was a real, already-fixed bug).

---

## 6. Known gaps — the actual task list

In rough priority order:

1. **Campaign unlock state is hardcoded, not read from the save file.** `CampaignMapPresenter.
   chapterStages` sets `isUnlocked` as a literal `true/true/false` in the constructor call. It
   should read `profile.unlockedStageIds.Contains(stage.stageId)` instead — `PlayerProfile` already
   has the data (`unlockedStageIds`) and the mutator (`UnlockStage`), nothing currently connects
   them to what the campaign map actually displays.
2. **Deck builder doesn't persist anything.** `DeckBuilderPresenter.activeDeck` is a local
   `List<DeckCardData>` that's forgotten the moment the screen closes — it never reads
   `profile.activeDeckCardIds` on open or writes back to it on close/change. Also its 8-card
   `collectionCards` list is hardcoded rather than derived from `profile.cardCollection`.
3. **The shop doesn't grant anything.** Every `ShopItemData.onPurchase` in `ShopPresenter.cs`
   spends currency and logs a message like "Added 3 cards to collection" — no code actually adds
   anything to `profile.cardCollection`. Currency leaves the wallet; nothing arrives.
4. **3 of 5 currencies are inert.** `eventMedals`, `guildContribution`, and `dragonRelics` exist as
   fields with full property/accessor support but nothing in any UI grants, spends, or displays
   them yet. Gold and Gems are the only currencies actually wired end-to-end.
5. **No collection screen.** `profile.cardCollection` has nowhere to be viewed — there's a deck
   *builder* (pick from a fixed list) but no screen showing what the player actually owns.
6. **No marketplace UI.** `CurrencyManager.ExecutePlayerTrade` and `TradeableAssetInstance` are a
   real, working trade primitive with zero UI in front of them.
7. **No way to acquire `TradeableAssetInstance`s at all** — nothing mints one. Whatever grants a
   collectible (event reward, milestone, shop pack) needs to actually construct these.
8. **Phase 1 direct messaging contracts now exist, but the trusted backend, persistence,
   moderation implementation, and UI remain unimplemented.** Identity bootstrap is implemented;
   guild/social contracts exist; DM contracts are now represented in the provider-neutral social
   layer, but no real DM backend, retention enforcement, or UI has been added yet.

9. **Phase 1 trusted social-safety block/mute slice is now locally authored and tested.** The linked Unity project
   has a non-empty Cloud Project ID, and the Unity Dashboard evidence confirms Cloud Save and
   Cloud Code availability in `production`; `ProjectSettings.asset`'s legacy `cloudEnabled: 0`
   is inconclusive metadata only. The Unity client uses `com.unity.services.cloudcode` `2.10.4`,
   selected from the official Unity Registry as a stable release compatible with Unity 2021.3+.
   The top-level `CloudCode/SocialSafety/` module uses the official server packages
   `Com.Unity.Services.CloudCode.Core` `0.0.5` and `Com.Unity.Services.CloudCode.Apis` `0.0.26`.
   `BlockAccount`, `UnblockAccount`, `MuteAccount` and `UnmuteAccount` use verified
   `IExecutionContext.PlayerId` identity and real Cloud Save server APIs. The module and client
   gateway are authored; 13 server tests pass, while the new Unity client fixture awaits manual
   Unity Test Runner execution. Nothing is deployed or called against production. `SubmitReport` is deferred:
   the accepted request has neither an idempotency key nor an evidence identifier, so retry-safe
   reports cannot be implemented without a minimal additive contract amendment and Command Centre
   approval. No idempotency key is derived from report text.

---

## 7. Save/PlayerProfile collision history — read before touching these files

`PlayerProfile.cs` and `SaveSystem.cs` have been rewritten several times this project by different
collaborators working in parallel, each time changing the class shape (a wrapped-`SaveData` design,
then a flat design, then back) and each time breaking something the other collaborator's code
depended on — compile errors, and once a real regression where `ApplyDataToEmpire()`/
`RecordMatchResult` silently became no-ops, meaning wins stopped raising Avatar level and every
match dealt an empty hand, with no compiler error to catch either.

**If you need to change `PlayerProfile.cs`'s shape** (add/rename/remove a field, change what a
method does): grep the whole `Assets/Scripts/` tree for every current usage first
(`SaveManager.cs`, `CurrencyManager.cs`, `HomePagePresenter.cs`, `ShopPresenter.cs`,
`GameBootstrap.cs`, and this project's `Assets/Tests/Editor/SaveSystemTests.cs`), and run the full
EditMode suite before considering it done — a clean compile is not the same as a working save
system, and this project has now hit both failure modes more than once.

---

## 8. Social-safety server hardening status (2026-08-12)

The Phase 1 block/mute server slice is authored locally and server-tested locally. Relationship
records use the Cloud Save `Item.WriteLock` returned by reads and pass it to `SetItemBody.WriteLock`;
one stale-write reconciliation re-reads authoritative state before retrying. Record IDs are derived
from the existing SHA-256 relationship key, with separate block and mute namespaces. No raw account
ID is used in a key or record ID. The server response contract remains unchanged.

Durable relationship mutation limiting is now authored locally. One actor-scoped O(1) rate record is
stored under a hashed `socialSafety.rateLimit.` key. Block, Unblock, Mute and Unmute reserve the same
minute/day counters using Cloud Save optimistic locking and trusted server time. A stale reservation
is re-read and reconciled once; remaining contention returns sanitized `CONFLICT`. A reservation is
made before relationship storage, so a later relationship failure consumes that quota unit by design.

The production configuration reader uses the installed official `Com.Unity.Services.CloudCode.Apis`
`0.0.26` Remote Config API. Exact keys and expected positive integer types are:

- `socialSafety.rateLimit.perMinute`: integer, default `10`
- `socialSafety.rateLimit.perDay`: integer, default `100`

The reader passes `context.AccessToken`, `context.ProjectId` and `context.EnvironmentId` at runtime;
no credentials, project IDs or environment IDs are embedded in code. If Remote Config is unavailable,
missing, malformed or non-positive, the protective `10`/`100` fallback is used explicitly. No
Dashboard values were created or published in this task; the user must create those two Remote Config
settings in the intended environment before deployment to make the limits configurable operationally.

`config/social-safety-access-policy.json` remains draft and deployment-blocking. Unity documentation,
support confirmation, or a controlled non-production test is still required to verify that Cloud
Code writes made with `context.AccessToken` are not denied by the Player write-deny rule. This task
did not deploy or call production.

The beginner-friendly controlled validation procedure is documented in
[SocialSafety_NonProduction_Validation_Checklist.md](SocialSafety_NonProduction_Validation_Checklist.md).
Live non-production validation remains pending; no deployment or production verification is claimed.

Status distinctions: authored locally; server-tested locally (43/43); Unity-client behavior has not
been manually observed by the user in this task; policy behavior is unverified; deployment was not
attempted; production behavior is not verified.

Claude follow-up: perform a read-only review of the three SocialSafety implementation files and
tests, checking the optimistic-lock contract and the unchanged client response casing. Do not launch
Unity or deploy Cloud Code.

---

## 9. Where to go deeper

| Doc | Read when |
|---|---|
| `docs/Economy_Blueprint.md` | Designing any new currency, shop SKU, or trade mechanic — full principles, the currency conversion graph, and an explicit "what this will never build" list. |
| `docs/Battle_Mechanics_Summary.md` | Anything touching the battle/metagame boundary in more depth than §5 above. |
| `docs/Mechanics_Gap_Analysis.md` | Full shipped/stub/missing inventory across the whole project, not just metagame. |
| `docs/BRIEF_HomePage_MainMenu.md` | The original home-screen spec (predates the current `HomePagePresenter.cs` — useful for intent, not necessarily current implementation). |
| `docs/START_HERE.md` | General project orientation, Unity/path setup, testing conventions. |

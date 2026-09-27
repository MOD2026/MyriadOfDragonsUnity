# CC10 Frontier Contract Export — Dependency Manifest

**For:** GUI's Unity bundle (WH → GUI handoff per this dispatch).
**Source:** `wh/cc10-beta-metagame-integration`, commit `3a70da09` (BE contract read through `fe0a2f5d`).
**Scope:** the minimal WH-owned client contract — DTO field mappings, World Map
`LayoutX`/`LayoutY`, caller-only `OccupantDisplayId`/`YourDisplayId`, Central contest
guild-color fields, and the Vein Relay minigame contract fields. Everything else WH owns
(`Cc10FrontierClient.cs`, `Cc10FrontierGateway.cs`, `Cc10ViewModels.cs`,
`Cc10FrontierPresenter.cs`, and this Frontier module's EditMode/PlayMode tests) is
**excluded** from this export — those are WH's own shell implementation, not the contract
GUI needs to build presenters against. No other room's files and no unrelated commit
history are included; this is two files plus this manifest, copied at a single point in
time, not a git history slice.

## Files in this export

| File | Lines | Purpose |
|---|---:|---|
| `Cc10Contracts.cs` | 710 | Every DTO, enum-as-string-constants, endpoint name, and error code the published CC10Frontier CloudCode module returns or accepts. This is the actual wire contract. |
| `Cc10Rules.cs` | 262 | Locked/derived preview constants (Tavern track totals, mission rule table, phase-unlock table, Vein Relay numbers, `GuildColorPaletteSize`) and pure state-machine helpers (`Cc10StateMachine`, `Cc10ServerClock`). Nothing here is authoritative — every value is either echoed from the server or a locked constant the server independently validates; GUI should treat this file the same way WH does: **preview only**, never a substitute for a real server rejection. |

## Namespace

Both files live in `namespace MyriadOfDragons.Frontier`. Keep that namespace when dropping
these into GUI's own assembly, or the two files stop compiling against each other.

## Compile dependencies

- **`Cc10Contracts.cs`** — zero project dependencies. Only `using System;` and
  `using System.Collections.Generic;`. Drops into any Unity assembly with no asmdef
  reference changes required.
- **`Cc10Rules.cs`** — one project dependency: `using MyriadOfDragons.Save;`, used for
  exactly two lines (`Cc10Rules.CardTrainingCost` → `CollectionTrainingRules.XpCostForNextLevel`,
  `Cc10Rules.CardLevelCap` → `CollectionSchemaRules.MaxCardLevel`). These two lines are
  **not part of the CC10 contract** — they're a leftover convenience wrapper around the
  Collection/Save-owned training system, unrelated to this dispatch's scope. If GUI's
  bundle does not already reference the `Assets/Scripts/Save/` assembly, either:
  1. add that reference (it's the same frozen, read-only Save code the rest of the project
     already depends on), or
  2. delete those two lines locally — nothing else in `Cc10Rules.cs` or `Cc10Contracts.cs`
     calls them.

No `UnityEngine`, `UnityEngine.UI`, or `Unity.Services.*` reference is required by either
file in this export.

## What each requested item maps to

| Dispatch item | Type(s) in this export |
|---|---|
| CC10 DTO mappings | All types in `Cc10Contracts.cs` — DTOs, `Cc10Endpoints`, `Cc10Errors`, and the enum-as-string constant classes (`Cc10MapPhase`, `Cc10MissionStatus`, `Cc10MissionType`, `Cc10SpotStatus`, `Cc10ResearchScope`/`Status`, `Cc10CargoStatus`, `Cc10MinigameStatus`, `Cc10MinigameLane`, `Cc10RankingScope`, `Cc10SeasonState`, `Cc10ContestStatus`). |
| World Map `LayoutX`/`LayoutY` | `Cc10MapNodeDto.layoutX` / `.layoutY` (int, int) — server-authoritative, hand-authored, identical for every player/read. |
| Caller-only `OccupantDisplayId`/`YourDisplayId` | `Cc10MapNodeDto.occupantDisplayId` (string, null unless the caller owns that node) and `Cc10FrontierSnapshot.yourDisplayId` (string, always the caller's own pseudonym). Both are privacy-safe by construction — never another player's identity. |
| Central contest guild-color fields | `Cc10ContestDistrictDto.enrolledGuildColorKey` / `.ownerGuildColorKey` (`int?`, 0–11, null when no guild is enrolled/resolved) and `Cc10Rules.GuildColorPaletteSize` (= 12) in `Cc10Rules.cs`. Server names an index only; the palette itself is GUI's own art asset. |
| Vein Relay contract fields | `Cc10MinigameSessionDto` (rulesetVersion/status/startedUtcMs/expiresUtcMs/verifiedScore/correctSelections/submittedTranscriptHash/startReceiptId), `Cc10MinigameActionDto` (roundIndex/selectedLane/clientTick — clientTick is informational only), `Cc10MinigameLane`, `Cc10MinigameStatus`, and the four minigame endpoints in `Cc10Endpoints` (`CreateMinigameSession`/`StartMinigameSession`/`SubmitMinigameResult`/`ClaimMinigameResult`/`AbandonMinigameSession`). |

## Known JsonUtility limitation (carried over from WH's own tests)

Unity's `JsonUtility` does not deserialize `System.Nullable<T>` value types
(`int?`, etc.). Fields such as `Cc10MinigameSessionDto.verifiedScore`/`correctSelections`
and `Cc10ContestDistrictDto.enrolledGuildColorKey`/`ownerGuildColorKey` will not populate
via `JsonUtility.FromJson` alone if GUI's bundle uses stock `JsonUtility` for
deserialization. WH's own `CloudCodeService.CallModuleEndpointAsync<T>` path does not hit
this limitation (it does not use `JsonUtility`), so it is only a concern if GUI adds a
second, separate JSON-parsing path over the same DTOs.

## Authority boundary (unchanged from WH's own rule)

Nothing in this export computes or authorizes a reward, timer, eligibility, ownership,
threat, or ranking value. Every non-constant field is either echoed directly from a real
server response, or (in `Cc10Rules.cs`) a locked preview number the server independently
re-validates on every real call. GUI should follow the same rule WH does: use these types
to render server state and build request bodies, never to decide whether an action is
actually allowed — only a real server response (or rejection) settles that.

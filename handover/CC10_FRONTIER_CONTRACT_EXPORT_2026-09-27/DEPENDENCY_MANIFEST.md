# CC10 Frontier Contract Export — Dependency Manifest

**For:** GUI's Unity bundle / FR (WH → GUI handoff).
**Source:** `wh/cc10-beta-metagame-integration`. BE contract read through **`ec4b49b3`** (World Map DTO shape unchanged since **`bab7aab1`**)

**CC11 (BE `7ac011a1` seams, wired live at BE-CC11-005 `0b822dcd`, WH-CC11-006):** all seven
CloudCodeFunctions are now real and wired client-side: `GetWorldMapBaseSnapshot`,
`PlaceWorldMapBase`, `RelocateWorldMapBase` (24h cooldown via `RATE_LIMITED`), `GetCargoSelectionCatalog`,
`AcceptCargoParticipants`, `GetGuildHallManagement` (keyed off the existing `GuildId` from
`RefreshGuildIdentityAsync`/`RefreshGuildStateAsync` — never an invented guildId), and
`GetRankingSeasonSource`. `Cc10FrontierClient` exposes `BasePlacement`/`BaseCells`/
`BaseOccupancyVersion`, `CargoCatalog`, `GuildManagement`, `RankingSeasonSource`, plus
`PlaceWorldMapBaseAsync`/`RelocateWorldMapBaseAsync`/`AcceptCargoParticipantsAsync`. The two
mutating commands fail closed while offline or with no state loaded (never an optimistic local
placement/lock) and reload the canonical base snapshot / cargo catalog on both success and CAS
conflict. Base placement itself is still not surfaced in any Beta UI (out of scope per this
task and the `bab7aab1` note below) — only the gateway contract is wired.
(approved private per-player World Map occupancy decision, 2026-09-28), which **supersedes**
the earlier occupancy pass `fe0a2f5d`; season gating and color tokens amended by **`7718db3`** then **`bef39415`** (below).
**Scope:** the minimal WH-owned client contract only — DTO field mappings, the canonical
World Map snapshot, Central contest guild-color tokens, and the Vein Relay contract fields.
Everything else WH owns (`Cc10FrontierClient.cs`, `Cc10FrontierGateway.cs`,
`Cc10ViewModels.cs`, `Cc10FrontierPresenter.cs`, tests) is **excluded**; this is two source
files plus this manifest, a point-in-time copy, not a history slice.

## Files in this export

| File | Purpose |
|---|---|
| `Cc10Contracts.cs` | Every DTO, string-constant "enum", endpoint name and error code the CC10Frontier CloudCode module returns or accepts. This is the wire contract. |
| `Cc10Rules.cs` | Locked/derived **preview** constants and pure helpers (`Cc10StateMachine`, `Cc10ServerClock`, `Cc10Rules.TryParseGuildColorToken`). Nothing here is authoritative; the server re-validates every value. |

Namespace for both: `MyriadOfDragons.Frontier` (keep it, or the two files stop compiling together).

## Compile dependencies

- `Cc10Contracts.cs` — none beyond `System` / `System.Collections.Generic`.
- `Cc10Rules.cs` — `using MyriadOfDragons.Save;` for two unrelated Collection-training
  convenience lines (`Cc10Rules.CardTrainingCost`, `Cc10Rules.CardLevelCap`). Either reference
  the Save assembly or delete those two lines; nothing else uses them.
- No `UnityEngine`, `UnityEngine.UI` or `Unity.Services.*` reference is required.

## Guild identity (BE ec4b49b3) — `GetCallerGuildIdentity`

Endpoint constant: `Cc10Endpoints.GetCallerGuildIdentity`. **Pure read, zero arguments** (`Task<Cc10GuildIdentityResult>`);
the client never sends a guildId, a player id, or any hash of one - this endpoint is the ONLY authoritative source
for "what guild is the caller currently in". `Cc10FrontierClient.RefreshGuildIdentityAsync()` wraps it.

| Field | Type | Meaning |
|---|---|---|
| `hasGuild` | bool | `true` only for a currently-valid membership, re-verified live server-side on every call. |
| `guildId` | string? | The caller's own plaintext guild id, or `null`/empty when `hasGuild` is `false`. |
| `membershipEpoch` | long | Set only when `hasGuild` is `true`. |
| `membershipExpiresUtcMs` | long | Set only when `hasGuild` is `true`. |

`hasGuild=false, guildId=null` is a **valid, successful** no-guild state (`success=true`) — never-joined, left, revoked
and expired all collapse to this one shape; BE does not distinguish them and this client must not try to. The only
case this client fails closed on is `success=false` (`AUTHENTICATION_REQUIRED`/`AUTHORITY_UNAVAILABLE`/
`STORAGE_UNAVAILABLE`/anything else) or a transport failure — both clear `Cc10FrontierClient.GuildId` and
`GuildIdentity` rather than keep serving a stale value, and `LastGuildIdentityRefresh` names why. A self-contradictory
positive response (`hasGuild=true` with an empty `guildId`) is treated the same way — never trusted.

The returned `guildId` feeds only the **existing** requests that already accept one: the `EnrollContestDistrict`
payload (`Cc10ViewModels`' contest row) and `RefreshGuildStateAsync(guildId)` (`GetGuildState`) — no new endpoint
was added to consume it. No Unity UI wires this yet (out of scope for this pass); `Cc10FrontierPresenter`'s
`guildId`/`callerPseudonym` host-gap providers are unchanged.

## World Map contract## World Map contract (BE bab7aab1) — `GetWorldMapSnapshot`

Endpoint constant: `Cc10Endpoints.GetWorldMapSnapshot`. **Pure read, no request body**
(`Task<WorldMapSnapshotResult>`); nothing is written. Result type `Cc10WorldMapSnapshotResult`
(extends `Cc10ResultBase`) → `snapshot` (`Cc10WorldMapSnapshotDto`).

| Field | Type | Meaning |
|---|---|---|
| `schemaVersion` | string | `"cc10.worldmap.v1"` (`Cc10WorldMapSnapshotSchema.V1`). A client must refuse any other value. |
| `mapVersion` | string | Server map catalog version (e.g. `worldmap-beta-1`). |
| `serverUtc` | long | Epoch **milliseconds**, server clock. Display only. |
| `unlockedPhase` | string | `Tutorial` \| `Outer` \| `Inner` \| `Central` (`Cc10WorldMapPhaseToken`). |
| `occupancyVersion` | int | Per-player monotonic; +1 only on a successful `ExpandNode`. Lower than the one already held = stale, ignore. |
| `ownOccupiedNodes[]` | `{ nodeId, phaseId }` | The **caller's own** occupied nodes only. Sorted by phase, then ordinal `nodeId`. `phaseId` uses the same four tokens. |
| `centralContest[]` | `{ districtId, seasonId, ownershipState, guildColorToken }` | Central Realm contest rows. Sorted by ordinal `districtId`. `ownershipState` is `Unclaimed` \| `GuildOwned` (`Cc10ContestOwnershipState`). |

Notes for GUI:
- **Phase vocabulary differs from `MapPhase`.** The snapshot uses band tokens
  (`Tutorial/Outer/Inner/Central`); the rest of the contract uses `HomeOutpost/OuterMarches/InnerReach/CentralRealm`
  (`Cc10MapPhase`). Do not mix them. Mapping: HomeOutpost=Tutorial, OuterMarches=Outer, InnerReach=Inner, CentralRealm=Central.
- There is **no** `ownershipVersion` and **no** top-level `seasonId`; `seasonId` is per contest row.
- **`guildColorToken`** is `"GC01"`..`"GC12"` (`Cc10Rules.GuildColorPaletteSize` = 12), an opaque label for
  the client's own fixed palette — `null` when `Unclaimed`. It carries no guild id, name or member list.
  `Cc10Rules.TryParseGuildColorToken` returns a 0-based palette index for a well-formed token and `false`
  for anything else (render neutral; never guess a color). The palette itself is GUI's art asset.
- `MapNodeDto.layoutX` / `layoutY` (int, server-authored, identical for every player) are **unchanged** and
  remain the only source of a node's on-screen position.

### Final season-gating (BE bef39415 "final amendment", supersedes bab7aab1 and 7718db3; server-enforced)

1. `centralContest` is empty until the player's `unlockedPhase` is `Central`, and while `GuildTerritory` is emergency-disabled.
2. **Display season** = the newest `Accepting` ("Active") season if one exists, else the newest `Frozen`/`Published` season.
   `Created` and `Archived` seasons expose nothing; no display season → empty list.
3. **Only settled districts carry ownership and a color.** Enrolling while `Accepting` confers **no** ownership and allocates **no** color:
   an enrolled district is still `Unclaimed` with a `null` token. `ResolveContestDistrict` (operator-gated, exactly once per district, only
   while the season is `Frozen`; a second call is `AlreadyCommitted`) settles it and allocates the guild's **season-unique** token.
   Settled districts are immutable and stay visible (`GuildOwned` + token) through `Frozen` and `Published`, and vanish when the season is `Archived`.
4. `Unclaimed` rows therefore also appear in `Frozen`/`Published`, where enrollment is closed (`WINDOW_CLOSED`); the snapshot carries no season state,
   so the client cannot tell and must let the server reject. An already-owned district of an un-Archived season cannot be re-enrolled (`DISTRICT_TAKEN`).
5. **Tokens are season-unique and one-to-one** (hash index = starting preference, first free index allocated at settlement, immutable for the season).
   Palette full → `COLOR_PALETTE_EXHAUSTED`; non-unique stored set or missing allocation → `COLOR_TOKEN_COLLISION` (atomic rejects; plain copy via
   `Cc10Errors.ColorPaletteExhausted` / `ColorTokenCollision`). A non-unique stored set makes the server expose **no** colors. Re-enrolling the same guild
   in the same district → `AlreadyCommitted`.
6. Known BE caveat: `AutoAdvanceSeasons` skips `Frozen`, so settlement needs an operator `FreezeSeason`.
7. Beta is **per-player `ExpandNode` expansion only**: no base placement, wells, relocation/teleport, or cross-player/global occupancy.

An empty `centralContest` is therefore a valid, expected state — not an error.

### Client handling rules (WH `Cc10FrontierClient` / `Cc10ViewModels`) — GUI should follow

- **Occupancy reset.** `occupancyVersion` is per-player monotonic, so a lower value is normally stale. It is accepted as a full-cache **reset**
  (`Cc10WorldMapRefresh.AcceptedAfterReset`, replaces — never merges) only when the **map epoch changed** (`mapVersion` differs, or the set of contest
  `seasonId` values differs) **and** `serverUtc` is not older than the held snapshot's (an out-of-order older response can never pass). Otherwise `Stale`.
- **No season-epoch field.** The snapshot has no top-level `seasonId`/epoch, and BE has **not** confirmed one belongs in the DTO (MS finding B1), so none is
  modelled. Season change is inferred only from per-row `seasonId`. If BE later adds one, it replaces this inference — request it from BE, do not add it locally.
- **`ownershipState`** (BE 7302a996) — `Unclaimed` (open, or enrolled-but-unsettled; the only state that offers **Enroll**), `GuildOwned` (settled to a guild;
  the **only** state with a `guildColorToken`), `ExplicitlyUnowned` (operator-recorded "no owner": token `null`, neutral text, **no** color, **no** Enroll,
  no action — the client ignores any token attached to it), `Enrolled` (defensive only; BE does **not** emit it — open BS/BE token-timing conflict, MS E1).
  Any other value = unknown → neutral, no color, no action. Newest settled record per district wins; enrolling in a settled district is rejected
  `DISTRICT_TAKEN`. Enroll additionally needs a known guild id and an online, non-paused screen; the snapshot has no season state, so `Unclaimed` in a closed
  season is still rejected by the server (`WINDOW_CLOSED`).

### Legacy `contestDistricts` — not in the export

`GetFrontierState.contestDistricts` (declared by BE, never populated) is **not modelled**: `Cc10FrontierSnapshot` has no such field and nothing in
`Cc10Contracts.cs`/`Cc10Rules.cs` depends on it. Contest state comes only from `GetWorldMapSnapshot.centralContest`. `Cc10ContestDistrictDto` remains
solely as the `contestDistrict` result of `EnrollContestDistrict`/`ResolveContestDistrict` commands. **GUI/FR presenters that still read
`snapshot.contestDistricts` (and the removed `*GuildColorKey` fields) will not compile against this export and must migrate to `Cc10FrontierClient.WorldMap`.**

### Removed from the export (deprecated by bab7aab1)

These no longer exist in `Cc10Contracts.cs`, and GUI must not re-add them:
per-node occupant/display-id, the caller display-id on the frontier snapshot, raw guild pseudonyms on
`ContestDistrictDto`, and the integer guild color keys. `ContestDistrictDto` now carries only
`districtId`, `status`, `callerGuildEligible`, `enrolledGuildColorToken`, `ownerGuildColorToken`.
(`Cc10TerritoryDto.ownerGuildPseudonym` on the **caller's own guild** territory read via `GetGuildState` is a
separate, unchanged BE field and was not part of that removal.)

## Central contest — command results

`EnrollContestDistrict` / `ResolveContestDistrict` return `Cc10CommandResult.contestDistrict`
(`Cc10ContestDistrictDto`) with the same token fields. Enrollment eligibility, ownership and season are
entirely server-decided; request bodies carry only ids (`districtId`, `guildId`, `seasonId`).

## Vein Relay contract (unchanged since BE 6e93d10d)

- Lifecycle (real server statuses): `Available` → `Active` (via `StartMinigameSession`, starts the 30 s deadline) →
  `Verified` / `Failed` / `Expired` → `Claimed`; `Abandoned` from `Available` or `Active`. `Started`/`Submitted` are
  declared for wire compatibility only — never produced.
- `SubmitMinigameResult` body: `sessionId`, `actions[]` (exactly 12, strict round order 0–11: `roundIndex`,
  `selectedLane` = `Front|Middle|Back`, `clientTick` **informational only**), `transcriptHash`.
  The client never submits a score or verdict. `rulesetVersion` is read from the session DTO, **not** sent.
- Start / Claim / Abandon bodies carry `sessionId` only. Endpoints: `CreateMinigameSession`, `StartMinigameSession`,
  `SubmitMinigameResult`, `ClaimMinigameResult`, `AbandonMinigameSession`.
- Types: `Cc10MinigameSessionDto`, `Cc10MinigameActionDto`, `Cc10MinigameLane`, `Cc10MinigameStatus`, `Cc10RankingScope.Minigame`.

## Known JsonUtility limitation

Unity's `JsonUtility` does not deserialize `System.Nullable<T>` (`int?`). Affected fields:
`Cc10MinigameSessionDto.verifiedScore` / `correctSelections`. (The contest color fields are now plain strings,
so they are **not** affected.) WH's transport (`CloudCodeService.CallModuleEndpointAsync<T>`) does not use
`JsonUtility`; this only matters if GUI adds its own second JSON path over the same DTOs.

## Authority boundary

Nothing in this export computes or authorizes a reward, timer, eligibility, ownership, phase, season, threat, color
or ranking value. Every non-constant field is echoed from a real server response; `Cc10Rules.cs` values are preview
constants the server re-validates on every call. Use these types to render server state and build request bodies —
never to decide whether an action is allowed.

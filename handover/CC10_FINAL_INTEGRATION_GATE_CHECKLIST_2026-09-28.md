# CC10 Final Integration Gate — dependency-closed checklist for FR

Prepared by WH (coordination role) on 2026-09-28. **Scope:** reconcile what exists so FR can integrate CC10 in one
ordered pass. **Out of scope / forbidden here:** Home UI, Tower Defense, base placement, wells, new mechanics, new currencies.
Every fact below was read from git or a repo document at the time of writing; anything not verifiable in the repo is labelled
**UNVERIFIED** or **EXTERNAL**. Nothing here was deployed or run against UGS.

## 0. Verdict

**FR is NOT ready to integrate.** Five blockers, in dependency order:

| # | Blocker | Owner |
|---|---|---|
| B1 | FR's server (CloudCode) and both client lines (GUI, FR) are still on the `fe0a2f5d` World Map contract. BE has since removed identity/int-color fields and added `GetWorldMapSnapshot`, guild color tokens, season gating, UGS hardening. | FR + BE + GUI |
| B2 | Two divergent forks of the same six `Assets/Scripts/Frontier/*` files (GUI `gui/home-social-backdrop`, WH `wh/cc10-beta-metagame-integration`). One lineage must win before FR merges. | Codex/FR |
| B3 | MS has not validated the current BE (`bef39415`) or WH tip. Last MS report is against BE `64c29a97` / WH `345dee16`. | MS |
| B4 | Attestation is fail-closed (`Unverified`) until the replay service + UGS config exist. Both are **PREPARED, NOT DEPLOYED**; no staging evidence exists. | CR + BE + external UGS operator |
| B5 | GUI map still ships "PlacementWell" assets/wording and reads removed fields; BS ruling on wells naming is not in the repo. | GUI + BS |

## 1. Pins (drift found — dispatch pins are not the current tips)

| Track | Dispatch pin | Actual, verified | Note |
|---|---|---|---|
| BE | `7718db3` | branch `be/canonical-final-b33a63f` tip **`bef39415`** (5 min after `7718db3`) | `bef39415` changes Central contest semantics again (§3). **Pin BE = `bef39415`.** BE reports module tests 366/366 (BE's own claim; not re-run by WH). |
| WH | `59140902` | `59140902` + the commit that adds this checklist | WH branch `wh/cc10-beta-metagame-integration` (pushed). 59140902 alone is missing the `COLOR_*` error copy and the `bef39415`-accurate manifest. |
| MS | "final validation report" | `docs/MS_CC10_WORLDMAP_FINAL_SOURCES_VALIDATION_2026-09-28.md` (commit `ec0db5bf`) | Validates BE `64c29a97` ≡ `bab7aab1` + WH `345dee16`. **Predates** `7718db3`, `bef39415`, `59140902`. Two earlier MS reports exist (`..._PRIVATE_WORLDMAP_CONTRACT_...`, `..._BAB7AAB1_REVALIDATION_...`). |
| GUI | "interactive-map patch" | `620354ce` (interactive map model) + `ce790dfa` (drop display-id/base-placement) on `gui/home-social-backdrop`, tip `eea7460a` | **Not on FR.** Its own commit says "Contract DTO fields untouched (revised BE occupancy DTO pending)". |
| FR | "Titan-Vein route" | `fr/final-beta-r3` tip **`96d41792`** ("integrate Titan-Vein awakening on FR"); duplicate branchless `4da88933` | This is the **Chapter 1 story cinematic** (touches `GameBootstrap.cs`, `CinematicSequence.cs`, cinematic tests). It is **not** the CC10 Vein Convoy / `titan_vein_depths` map route. Confirm the dispatch meant the cinematic. |
| CR | "replay deployment package" | `8b883d0d` on `cr/battle-attestation-adapter`; adapter/core already on FR as `b5074d2a` | Package doc `docs/CC10_HEADLESS_REPLAY_DEPLOYMENT_PACKAGE_2026-09-28.md`: **prepared, NOT deployed**. Handler/CLI files are **absent from FR and BE**. |
| UGS | "external deployment evidence" | **none exists.** BE `tools/cc10_attestation_operator/UGS_DEPLOYMENT_HANDOFF.md`: "PREPARED, NOT EXECUTED… no Valid / Invalid / Unverified staging evidence." | EXTERNAL. Related BE commits: `3bb7a80a`, `64c29a97`, `738f59b9`. |

## 2. What FR actually contains today (verified by grep on `fr/final-beta-r3`)

| Item | FR | BE tip |
|---|---|---|
| Vein Relay (`StartMinigameSession`, `MinigameActionDto`) | yes | yes |
| Tavern `ActiveMissionSlots`, `titan_vein_depths` | yes | yes |
| `ReplayTranscriptBlob`, `CC10HeadlessReplay.cs`, `CC10AttestationOps.cs` | yes (older revision) | yes |
| **`GetWorldMapSnapshot`** | **no** | yes |
| **Guild color tokens / `COLOR_TOKEN_COLLISION`** | **no** | yes |
| **UGS hardening (`HTTPS_REQUIRED`, secret-manager config)** | **no** | yes |
| Client `occupantDisplayId` / `ownerGuildColorKey` (removed by BE) | **still present** — FR presenter renders `occupantDisplayId` (`Cc10FrontierPresenter.cs` ~L813) | removed |
| `RetentionTelemetryOutbox.FlushDeadline` (hang guard) | yes | n/a |

## 3. Contract truth to integrate (BE `bef39415`, read directly)

- `GetWorldMapSnapshot`: pure read, no body. Fields exactly: `schemaVersion` (`cc10.worldmap.v1`), `mapVersion`, `serverUtc`, `unlockedPhase`,
  `occupancyVersion`, `ownOccupiedNodes[{nodeId,phaseId}]`, `centralContest[{districtId,seasonId,ownershipState,guildColorToken}]`.
  Phase tokens `Tutorial/Outer/Inner/Central` (≠ `MapPhase` names). Own occupancy is private per player; no occupant id, no guild identity anywhere.
- **Central contest (final amendment):** display season = newest `Accepting`, else newest `Frozen`/`Published`; `Created`/`Archived` expose nothing.
  **Enroll confers no ownership and no color** (row stays `Unclaimed`, token `null`). `ResolveContestDistrict` (operator-gated, once per district,
  only while `Frozen`) settles and allocates the season-unique token; settled districts stay visible through `Frozen`/`Published`, vanish at `Archived`.
  New errors `COLOR_PALETTE_EXHAUSTED`, `COLOR_TOKEN_COLLISION`. BE-stated caveat: `AutoAdvanceSeasons` skips `Frozen`, so settlement needs an operator `FreezeSeason`.
- The authoritative BS decision documents (`..._OCCUPANCY_BASE_PLACEMENT_DECISION_...`, `..._CENTRAL_SEASON_TOKEN_DECISION_...`) are cited by name in BE/MS
  material but are **not in the repo** — **UNVERIFIED** here; BS must confirm `bef39415` matches them (MS finding C3 said BS's "Active-only colors" contradicted the
  season lifecycle; `bef39415` resolves it by settling during `Frozen`, which is a design choice BS has not been shown to accept in-repo).
- Client contract source of truth: WH export `handover/CC10_FRONTIER_CONTRACT_EXPORT_2026-09-27/` (`Cc10Contracts.cs`, `Cc10Rules.cs`, `DEPENDENCY_MANIFEST.md`).

## 4. MS findings — status against current sources (from the `ec0db5bf` report)

| Finding | Status now | Evidence / action |
|---|---|---|
| C1 token uniqueness not implemented | **BE changed** (`7718db3`, reworked `bef39415`: season-unique allocation at settlement) | MS must re-run the collision/uniqueness model against `bef39415`. |
| C2 gate was "latest non-Archived" | **BE changed** twice | Re-validate against the `bef39415` display-season rule (§3). |
| C3 lifecycle contradiction (no `GuildOwned` could ever show) | **Addressed by BE design** (settle in `Frozen`) — needs BS acceptance | BS + MS sign-off; note the operator `FreezeSeason` dependency. |
| C4 WH did not render the snapshot | **Fixed** in WH `59140902` (view models read `client.WorldMap`; contest reads `centralContest` only) | WH suites at the gate commit: EditMode 116/116, PlayMode 10/10, 0 `error CS`. MS to confirm. |
| C5a `mapVersion` is a hand-kept literal, no layout in snapshot | **Open (BE, low)** | Layout still arrives via `GetFrontierState` nodes (`layoutX/Y`). |
| C5b WH rejects any lower `occupancyVersion`, no reset path | **Open (WH, low)** | Client keeps the stale map until restart after a legitimate server-side lowering. Not changed here (would be a behaviour decision). |

## 5. Dependency-closed order (each step needs everything above it)

1. **BS (decisions, no code):** (a) accept `bef39415` Central semantics or amend; (b) rule on wells wording — GUI's map still names its node markers
   "PlacementWell" (`Cc10FrontierPresenter.cs`, atlas `UI/CC10/WorldMap/PlacementWellStates_*`, 11 references) although BE/BS say "no wells".
   Either approve the asset name as a pure marker or require a rename; (c) confirm the "Titan-Vein route" item (cinematic vs map node).
2. **BE:** freeze at `bef39415` (or a named later tip) and publish the contract. Confirm module tests on that exact commit.
3. **WH:** export = this branch (`59140902` + gate commit). FR/GUI take `Cc10Contracts.cs`, `Cc10Rules.cs`, `Cc10FrontierClient.cs` as the client contract lineage.
4. **GUI:** rebase the interactive-map patch onto the WH contract: delete reads of `ownerGuildColorKey`/`enrolledGuildColorKey`/`occupantDisplayId`/`yourDisplayId`/`*GuildPseudonym`;
   drive markers from `Cc10FrontierClient.WorldMap` (`ownOccupiedNodes`, `centralContest`, `guildColorToken` via `Cc10Rules.TryParseGuildColorToken`, unknown = neutral);
   resolve the wells wording per step 1b; keep server-decided actions only. GUI must **not** edit WH-owned contract files — request changes.
5. **MS:** revalidate in-engine against BE `bef39415` + WH tip (+ GUI step 4 if MS validates presentation). Required: token uniqueness, display-season gate,
   settlement-in-Frozen path, privacy scan, `occupancyVersion`, reconnect/offline.
6. **CR + BE + UGS operator (EXTERNAL, parallel to 2–5, blocks settlement realism):** stand up the warm headless replay worker (the package measured a **12.1 s** cold
   Editor launch vs BE's 5 s default timeout / 25 s ceiling — a warm pool or a raised `cc10.headlessReplay.timeoutMs` is required; Linux server build and licence
   for unattended use are **UNVERIFIED**); deploy `CloudCode/CC10Frontier` to `nonprod-validation` only; set Remote Config + secrets per the BE handoff; run the readiness
   `check` to `RESULT: READY`; capture **Valid, Invalid and Unverified** attestation evidence. Until this exists every attestation is `Unverified` (retryable, no settlement).
7. **FR:** apply in this order: BE server files (`bab7aab1..bef39415` incl. UGS hardening) → WH contract/client → GUI map rebase → CR handler/CLI (only if FR ships the
   package; it is a server-side deployable, not a client dependency) → story cinematic (`96d41792`, already on FR). Resolve the `Assets/Scripts/Frontier/*` file conflict per step 4 ownership.

## 6. File ownership for the six overlapping Frontier files

| File | Contract owner | Note |
|---|---|---|
| `Cc10Contracts.cs`, `Cc10Rules.cs` | **WH** (mirrors BE) | GUI/FR must not diverge; export copies are in the handover folder. |
| `Cc10FrontierClient.cs`, `Cc10FrontierGateway.cs` | **WH** | Adds `RefreshAllAsync`, `RefreshWorldMapAsync`, `LastWorldMapRefresh`. |
| `Cc10ViewModels.cs` | **WH** (rows/state), GUI consumes | GUI's fork of this file is the conflict source. |
| `Cc10FrontierPresenter.cs` | **GUI** | GUI-owned presentation; WH's presenter is a flat-panel shell superseded by GUI's for production. |
| `Cc10VeinRelay.cs`, `Cc10WorldMapLayout.cs` | **GUI** | Exist only on GUI/FR. |

## 7. Acceptance evidence FR must collect (all on the merged tree, same commit)

- [ ] `error CS` count = 0 in the Unity log **before** trusting any result (never add `-quit`; use `tools/run_editmode_tests.ps1` — **pass `-ProjectPath` explicitly**:
      its default points at the main checkout, which silently tests the wrong tree).
- [ ] EditMode and PlayMode: WH suite (`Cc10FrontierTests`, `Cc10FrontierPresenterPlayModeTests`), GUI map suites (`Cc10WorldMap*`, `Cc10FrontierProductionEntryPointTests`),
      `Cc10VeinRelay*`, attestation tests, Chapter 1 cinematic tests. WH baseline at the gate commit: EditMode 116/116, PlayMode 10/10, 0 `error CS`.
- [ ] Privacy scan: no `occupant*`, `yourDisplayId`, guild pseudonym/id/name, integer color key anywhere in client DTOs or rendered rows.
- [ ] No wells / base placement / relocation / global occupancy in UI copy, assets names approved per step 1b, or endpoints.
- [ ] Full-suite run watched for the historical `ShopV1ChromeTests` stall (guard: `RetentionTelemetryOutbox.FlushDeadline` is on FR; regression tests on branch
      `fix/shop-hang-timeout-safe-teardown`, commit `57e2605c`, test-only — merge optional).
- [ ] External: UGS deploy log, `check` READY output, and the three attestation-outcome captures (Valid/Invalid/Unverified). **Absence = not shippable as verified settlement.**
- [ ] MS revalidation report against the merged commit hashes.

## 8. Known limitations FR should expect (not blockers)

- Contest `Enroll` is offered on every `Unclaimed` row when a guild id is known; the snapshot has no season state, so in `Frozen`/`Published` the server answers
  `WINDOW_CLOSED` (shown as plain copy). In production FR the guild id is `null` today (`670aea4c`: no real guild-membership source wired), so Enroll stays disabled
  and Guild Territory/Rankings stay read-only — this is intentional, not a defect.
- `GetRankingView` still needs a season id the shell has no picker for.
- Vein Relay play surface and Battle attestation adapter belong to GUI/CR, not the WH shell.

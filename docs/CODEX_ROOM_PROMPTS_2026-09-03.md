# Codex Room Prompts — Revamp V2

Use this file as the current dispatch source. Every room must return `COMPLETE`, `BLOCKED`, or `NO ACTIONABLE TASK`, with exact files, tests/evidence, commit hash, and next owner. Work in an isolated branch unless explicitly told otherwise. Do not integrate into LK's release lane while VE/AN are open.

## MS — first-login and Daily Login

```text
Implement the first-login landing flow using the approved Startup asset `UI/RevampV2Approved/Startup/startup_first_login_v1`.

New players see the landing after authentication; Start Tutorial / Continue begins the existing tutorial; after the existing first-login/tutorial milestone, route directly to Home. Bind the existing approved Daily Login asset `UI/DailyLoginQuestsV1/daily_login_quests_landscape_v1` and use ST-PLAYER-LANDING-COPY-101.md for copy.

Use existing profile/milestone fields only. Do not add Save schema, calendar-week tracking, timers, rewards, or new navigation contracts. Preserve Home navigation and Back behavior. Add focused tests. Work on an isolated branch and report exact files, commit hash, and results.
```

## WH — approved asset packaging

```text
Package only already-approved assets that are absent from the Unity project. Search the project and source folders first. Verify the exact registry hash, dimensions, alpha mode, Unity .meta import, and destination path.

Priority: Daily Login, Bazaar, Battle Pass, Battle Result, Collection, Tutorial/Story. Do not generate art, bind runtime code, package conditional/rejected art, or touch the VE/LK candidate. Commit only verified asset and .meta files and report hashes.
```

## UI/UX — next candidate art

```text
Search existing output folders first. Then create production-quality CANDIDATE assets for Building Detail, Avatar/Profile states, and Friend Requests states, excluding the approved Friends v3 visual.

Target 1920x1080 landscape, approved Myriad of Dragons V2 language, safe margins, no invented mechanics/rewards. Exclude Social, Settings, Shop, Guild, Group F, and Battle combat art. Deliver distinct PNGs and a manifest with dimensions and hashes. Mark NEEDS OWNER APPROVAL; do not package or bind.
```

## FR — Friends and Chat

```text
On an isolated branch, bind the approved Friends v3 visual and accepted Chat UI package through the existing presenter seams.

Keep Social art, rejected Friends shells, logo-bearing assets, and lock-treatment empty-state art unbound. Do not touch Save, Cloud Code, identity, or navigation. Add focused tests, run Friends/Chat tests, and report files, hash, and results.
```

## VS — approved non-Battle bindings

```text
On an isolated branch, implement only approved non-Battle presenter bindings that remain unbound. Verify exact registry path/hash and preserve raycast/input behavior.

Do not bind conditional or rejected art. Do not touch Battle, Save, Friends, Chat, Home, or LK's release lane. Add focused tests and report exact commit/hash and results.
```

## BS — durable Battle authority

```text
Commit the existing `docs/BATTLE-REMAINING-OWNER-DECISIONS-0.9-SIGNED.md` as the canonical tracked Battle authority, preserving its signed text exactly. Do not recreate missing BS-001 from memory. Redirect documentation citations from the absent BS-001 only when supported by the signed document.

Do not change Battle mechanics, Save files, or frozen contracts. Return the durable authority path and commit hash.
```

## CR — paste manually into the Claude CR room

```text
After BS makes the signed authority durable, verify CR-BATTLE-015 against it. Bind the approved Battle Result asset `UI/RevampV2Approved/BattleResult/battle_result_v2` through the existing GameBootstrap result-overlay seam if not already bound.

Do not touch rejected Battle combat art, Save files, replay contracts, or navigation contracts. Add focused coverage, run Battle regression tests, and report exact files, hash, and results. Hold any Energy/mechanics revert until authority reconciliation is complete.
```

## ST — content implementation handoff

```text
Use `tools/seat_reports/ST-PLAYER-LANDING-COPY-101.md` to provide exact player-facing strings for first-login landing, Continue, tutorial completion, Return Home, and Daily Login preview.

Use only existing mechanics and MOS terminology. Do not add timers, rewards, currencies, or unsupported week claims. No runtime code or art changes; return the copy handoff for MS.
```

## VE — Windows execution room, not validator-only room

**Operational requirement:** VE capture must be assigned to a separate Claude-based Windows/GUI
execution room. The Codex VE room is validator-only and cannot control the Unity player window. This
room must run on the Windows host with access to the LK build, PowerShell, window control, and PNG/
JSON output paths.

```text
Capture the current LK-frozen candidate only after reading LK's latest freeze report. Verify exact HEAD, tracked dirt, and Runtime.dll SHA-256 at start and end.

Launch the build in a Windows GUI execution environment at 1920x1080 with `-screen-width 1920 -screen-height 1080 -screen-fullscreen 0`, attach with `-AttachProcessId`, and capture every required screen. Each PNG needs independent SHA-256, dimensions, route metadata, and a sidecar. Visually inspect every PNG. Reject stale/duplicate pixels, wrong dimensions, wrong routes, or drift. If the room cannot control the Windows player, report BLOCKED with that exact capability gap; do not work on validator scripts instead.
```

## Release order

```text
Correct Startup binding -> LK rebuilds/refreezes -> Windows VE captures -> AN validates -> LK integrates queued approved UI -> final rebuild/capture.
```

## Permanent exclusions

Shop, Social, Settings, Guild, Campaign, Memory Expedition, Group F, rejected Friends variants, and all Battle combat art remain rejected. No room may bind or generate them without a new dated owner approval.

# Alignment audit — 2026-08-27

Full deep-dive requested by the owner after CC's Event Medals error. Everything below is verified
against real code at audit time, not asserted from memory or from a peer's report.

---

## 1. CC errors found, and what caused each

| # | Error | Root cause | Status |
|---|---|---|---|
| 1 | **Claimed Event Medals have no live source. They are minted 1/day, ungated, on every daily login.** Locked twice; a BS prompt was built on the false premise. | Grepped the RESULT FIELD (`EventMedalsGranted`) instead of the WRITE SITE (`AddCurrency(..., EventMedal, ...)`). | Corrected `92b4778`. Fix dispatched. **Open: what to do with medals already in live saves — owner/BS call, not a coding decision.** |
| 2 | **Wrote "portrait mobile" into multiple GPT prompts. The game is landscape 1920x1080.** | Asserted a fundamental without ever opening a file. Caught by the UI seat, not by CC. | In `CLAUDE.md` (`f03ad63`). Portrait-dependent BS guidance flagged suspect. |
| 3 | **Locked 5 UI decisions with no benchmark** (typography, empty states, contrast, interaction states, transitions), against an explicit standing gate. | Treated BS's own "UNCONFIRMED" as discharging CC's obligation. It does not. | Audited `23b3850`, benchmarked `f2704e2`. Two errors surfaced by doing so. |
| 4 | **Dispatched to VS by SendMessage while VS reports in `tools/seat_mailbox.md`.** Two tasks never arrived; VS was called unresponsive three times while shipping. | CC locked the mailbox as VS's channel and then never read it. | Resolved `c98e4c1`. **Mailbox must be read every turn.** |
| 5 | **Assigned the UI validator to CR when VS had already built it.** | Consequence of #4. | Resolved — VS keeps it. |
| 6 | **Drifted the whole room into economy design** (spell books, Loyalty, Event Medals) while UI was the stated priority. | CC generated its own BS backlog under GR1 and let it displace the owner's priority. | Locked `a9a5d72`; partially lifted for build-only work. |
| 7 | **Long replies after the owner asked three times for short ones.** | — | Locked `7bb441f`. |

**Pattern across #1, #2 and #3:** CC asserted checkable facts without checking them. Every rule
written to prevent this was locked *by CC*, then broken *by CC* within a day.

## 2. Verified clean

- **Frozen files untouched.** `PlayerProfile.cs`, `SaveSystem.cs`, `SaveMigration.cs`,
  `SaveManager.cs`, `SaveSystemTests.cs` — no working-tree modifications.
- **Full production currency-write audit.** Every non-test `AddCurrency` site enumerated. Event
  Medals via Daily Login was the ONLY ungated leak. Gems have no production faucet outside the
  simulation, consistent with IAP-only design.
- **Home IA rebuild** — gate-verified, 1799/1803, zero Home findings, 22 -> 9 buttons, missing-map
  root cause fixed and visually confirmed on a real capture.
- **Contrast gate** — real, validated against a capture, 156 findings after VS removed a
  self-caught anti-aliasing artifact.

## 3. Locked but NOT built — the "design answered is not shipped" inventory

Verified by checking whether the type actually exists in `Assets/Scripts/`.

| Lock | Built? |
|---|---|
| Type scale + frame tiers (`UIDesignTokens`) | **YES** — shipped, applied to Home |
| Procedural border generator + scrim/shadow tokens | **YES** — `5ec115f`, not yet applied to any screen |
| Contrast validator | **YES** — built, deliberately NOT armed |
| Empty-state component | **NO** — no type exists |
| Interaction states (9 states) | **NO** — no type exists |
| Persistent shell + transitions | **NO** — no type exists |
| Nav-graph edges + safelist | **NO** — nodes only, no edges |
| Memory Expedition chapters | **NO** — and the minigame itself is UI shell only, no real logic |
| `SpellBookGrant` wiring | **NO** — still **0** production callers |

**9 locked designs, 3 built.** The design backlog is clear; the build backlog is not.

## 4. Known defects, unfixed

| Defect | Owner |
|---|---|
| 21 contrast findings under 2:1 — genuinely unreadable text | WH (scoped exception `ede5f57`) |
| 156 contrast findings total | staged after the 21 |
| Shop missing per-product art (a JPEG, not an import flag) | inventory task |
| Shop text truncation (2 Title + 1 PityLine) | spawned |
| GameBootstrap text truncation | spawned |
| TacticalPuzzle overlap | spawned |
| Home TopHud resource-pill label/value overlap | spawned |
| `SpellBookGrant` unreachable — most of the spell catalogue | VS |
| Event Medals minted ungated | WH |

## 5. Process changes that actually stuck

- Grep the WRITE, not the report field.
- Verify every factual premise before it enters a GPT prompt.
- Read `tools/seat_mailbox.md` every turn — it is VS's only channel.
- "BS said UNCONFIRMED" never discharges CC's benchmark obligation.
- A capture, not a test result, is what closes a UI task.

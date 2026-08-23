# Zihan — one page only

**Updated:** 2026-08-23 (CC full-read pass) — **CC = Claude** · **Coding seat = separate Claude room** · **Working Hands = Cursor** · see `docs/CC_HANDOVER_TO_CLAUDE_2026-08-23.md`

---

## Golden rule

Ship playable ASAP. Firm assigns only. No Ch1-2 Soft spiral. No idle theater.

---

## Real baseline (2026-08-23, fresh full EditMode run — this supersedes every prior per-block count)

**652 / 761 passing, 109 failed.** This is **not** 81/81 and **not** the various "9/9" / "8/8" per-block
numbers logged in `OWNER_REVIEW_LOG.md` — those were each true in isolation but were never
re-verified together. Running the **whole** suite in one pass (required by `AI_CONTRIBUTING.md` §5)
surfaces contamination none of the per-block runs could see.

- 28 of the 109 failures share one Setup assertion — *"expected enough real cards to fill a
  full-size deck"* — across `GateCampaignLaunchTests`, `TutorialGuidanceTests`,
  `NormalBattleAutoFormationTests`, every `Chapter*FullDepthTests`, and more. `card_data.json` has
  85 cards (tracked, unmodified) — not a content shortage. Most likely cause: `CardDatabase.Instance`
  singleton state or another static leaking across fixtures only when the full 761-test run
  executes back-to-back — a class of bug this project has hit before (`AI_CONTRIBUTING.md` intro).
- The remaining ~80 are scattered across Shop currency/persistence, Tutorial guidance, Campaign
  input contracts, and combat balance — not yet triaged one by one.
- **Gate (`GateCampaignLaunchTests`, previously accepted "9/9") now shows 5 failing.** Castle Block
  AA's own new tests (`CastlePhase1RelationshipTests`, `CastleScalePublishGateTests`) did not run in
  this pass's failure list, so AA's code may be locally sound — but nothing gets called "accepted"
  again until it's proven inside a green full run, not a scoped one.
- **No commits since `5b983dc` (Ch7 fill).** Every block described as "accepted" in
  `OWNER_REVIEW_LOG.md` from Block M onward — Gate, Castle, Empire construction, Collection/Shop
  work, Ch8–10 — is sitting **uncommitted** in the working tree. That is the real risk right now,
  independent of the test failures: it can be lost or clobbered with no git history to recover from.

## Board

| Seat | Status |
|---|---|
| **Claude (this room)** | **Command Centre** — reads, assigns, accepts/rejects, does not edit `.cs` itself |
| **Claude (coding room)** | **Coding seat** — **Block AB issued 2026-08-23** (full-suite baseline repair) |
| **Cursor** | **Working Hands** — **Block AC issued 2026-08-23**: Shop/Avatar/Empire/new-UI metagame push, parallel to AB |
| **ChatGPT** | **Bazaar Phase-1 design packet issued 2026-08-23** — required before any trading/marketplace code |
| **You** | Owner reopened Market/Bazaar for Phase 1 (2026-08-23) — gated on ChatGPT's packet + CC review, not open for Cursor yet |

---

## Active block — Block AB: full-suite baseline repair (coding seat)

Not Block AA. AA does not open until AB reports a green full run (fresh `results.xml`, Unity closed,
no `-quit`). Scope: 28-cluster "expected enough real cards" Setup failures + triage of the other ~80
of the 109 failures found in the 2026-08-23 full run (652/761 passing). Files: test-support helpers
in the affected suites + `Assets/Scripts/Cards/CardDatabase.cs` if that's the root cause. Frozen
files, Save shape, `CampaignMapPresenter.cs`, and economy numbers are out of scope.

## Active block — Block AC: metagame push (Cursor, parallel to AB)

Shop, Avatar, Empire screens, new UI (HomeV3). Does not touch `CardDatabase.cs`, Gate/Castle files,
or any `Assets/Tests/Editor/*` file Block AB owns. **Market/Bazaar and Chat are explicitly excluded**
from AC — see below.

## Market/Bazaar — owner reopened for Phase 1 (2026-08-23), gated

`EMPIRE_SCHEMA_LOCK_2026-08-22.md` / `Economy_Blueprint.md` previously locked Bazaar to Phase 2.
Owner has now approved reopening it for Phase 1. Per the constitution's "doc loopholes before Unity"
rule, and because `Economy_Blueprint.md` itself flags Gold→Market Credits as **"the single most
dangerous edge"** (bot-farm real-value risk) with a forbidden-edges table already locked, **no
Bazaar/trading code starts until ChatGPT's design packet is CC-reviewed.** Zero Bazaar/Marketplace
code exists in the repo today — this is a from-scratch system, not a UI skin on existing logic.

## Chat — foundation-only, not assigned

`Assets/Scripts/Social/` (`ISocialService`, `SocialContracts`, `SocialValidation`,
`UnityAuthenticationSocialService`) exists uncommitted but is exactly the "trusted guild/identity
service" `AI_CONTRIBUTING.md` §7 holds to **design/review only until ownership and service approach
are approved** — that constraint is unrelated to the Bazaar reopen and still stands. Not assigned to
any seat yet; needs an explicit owner/backend-approach decision first.

## Cursor: Block AC (Shop/Avatar/Empire/new UI). Still stand by on APK, Gate/Castle files, any test file, and Bazaar/Chat code specifically.

## ChatGPT: Bazaar Phase-1 design packet — numbers, ledger/ownership model, forbidden-edges enforcement. No code.

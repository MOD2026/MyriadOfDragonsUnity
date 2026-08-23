# MVP Playable Gate v1

**READY FOR CC — Yes.** Approved 2026-08-23 as the minimum gate before Zihan's first human play session.
**Owner lock:** Human playtest deferred until this gate is agent-green (EditMode spine + routes).

**CC path note:** Fresh players go **Tutorial → Home → Deck Builder (required) → Campaign 1-1**. The deck detour is intentional, not a defect.

**Agent status (2026-08-23):** First-session Soft queue **clear** (spine K + K.1 + L Softs + M AI-on resolve smokes). Owner optional: `MVP_FIRST_SESSION_SCRIPT_v1.md`.

**Re-verified 2026-08-23 against real state (post Avatar/Empire/Shop/Permit/Stamina overhaul):**
9/11 across the 5 MVP-tagged suites; the 2 failures are the same pre-existing `DeckBuilderReleaseGateTests`
geometry gaps already logged in `OWNER_REVIEW_LOG.md`'s Block AB triage, not new fallout. The
Tutorial → Home → Deck Builder → Campaign 1-1 spine itself holds end-to-end.

**Drift found in this re-verification, not yet closed:**
- No checklist line for the Avatar/Empire construction screen (`AvatarPresenter.cs`) — real, shipped, reachable from Home, post-dates this doc.
- No line for the Permit weekly-claim/hoard UI or the Stamina Gem-tier ladder — both real, both post-date this doc.
- The Shop checkmark below cites pack-receipt/overlay tests that exercise `pack_novice` (`ShopV1StubCatalog.NovicePackId`) — that pack is retired from the live Shop V2 grid (kept only for `PurchaseForTests` hooks), so this evidence covers less of the real live catalog than the checkmark implies.

## Greenlight checklist

- [x] **Tutorial → Home:** Agent-proven — `MvpOnboardingSpineTests` + tutorial reward guard / return-to-city clear.
- [x] **Campaign 1-1:** Agent-proven — spine launch/win + `CampaignLaunchFeedbackContractTests` blocked messages; AI-on resolve smoke for 1-1.
- [x] **Shop:** Agent-proven for pack receipt/overlay mechanics — evidence tests exercise the retired `pack_novice` SKU, not a live Shop V2 pack; re-verify against a real SKU before trusting this line as full live-Shop coverage. Optional human feel still open.
- [x] **Collection:** Agent-proven — burn/evolve UI honesty (Block J) + copy; optional human feel still open.
- [x] **Legal battle deck:** Agent-proven — Deck confirm in spine + normal battle saved-deck contracts. Soft: Deck Builder first-open guidance — `DeckBuilderFirstOpenSoftTests` (Block N).
- [ ] **Avatar/Empire screen:** Not yet covered by this gate — real shipped surface, needs its own evidence line.
- [ ] **Permit + Stamina ladder UI:** Not yet covered by this gate — real shipped surfaces, needs their own evidence lines.

## Evidence required for greenlight

- Agent EditMode spine + covering suites green (2026-08-23).
- Optional: one clean human run of `MVP_FIRST_SESSION_SCRIPT_v1` (owner).
- No blocking compile error, null reference, dead click, or unrecoverable screen.

## Explicit non-goals

- UI polish, final art, animation flair, pack-opening presentation, and visual tuning.
- Bazaar/trading or any Phase-2 market system.
- Server-issued Permit week key, server claims, or later trusted-week migration.
- Economy rebalance, Gold curve changes, Forge/Dust yield changes, Permit-cap changes, or extra content depth.

**Rule:** Human playtest begins only after this gate is green. Findings then become a single prioritized MVP list, not isolated visual tweaks.

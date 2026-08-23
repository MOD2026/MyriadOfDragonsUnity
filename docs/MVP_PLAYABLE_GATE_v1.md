# MVP Playable Gate v1

**READY FOR CC — Yes.** Approved 2026-08-23 as the minimum gate before Zihan's first human play session.
**Owner lock:** Human playtest deferred until this gate is agent-green (EditMode spine + routes).

**CC path note:** Fresh players go **Tutorial → Home → Deck Builder (required) → Campaign 1-1**. The deck detour is intentional, not a defect.

**Agent status (2026-08-23):** First-session Soft queue **clear** (spine K + K.1 + L Softs + M AI-on resolve smokes). Owner optional: `MVP_FIRST_SESSION_SCRIPT_v1.md`.

**Re-verified 2026-08-23 against real state (post Avatar/Empire/Shop/Permit/Stamina overhaul):**
Shop pack-receipt/overlay and contract evidence now exercise live Shop V2 SKUs (`CollectionPackCatalog.SingleSigilSkuId`, `ShopStaminaCatalog` ladder tiers). Permit weekly-claim/hoard and Stamina Gem-tier ladder have dedicated EditMode spine coverage. Avatar/Empire construction screen now has its own evidence line too — every row is closed.

**Drift found in this re-verification, now closed:**
- No checklist line for the Avatar/Empire construction screen (`AvatarPresenter.cs`/`EmpirePresenter.cs`) — real, shipped, reachable from Home, post-dates this doc. Closed below.

## Greenlight checklist

- [x] **Tutorial → Home:** Agent-proven — `MvpOnboardingSpineTests` + tutorial reward guard / return-to-city clear.
- [x] **Campaign 1-1:** Agent-proven — spine launch/win + `CampaignLaunchFeedbackContractTests` blocked messages; AI-on resolve smoke for 1-1.
- [x] **Shop:** Agent-proven on live Shop V2 catalog — `PackOpenOverlayTests` + `CollectionPackReceiptTests` (Single Sigil / Scout Cache), `ShopStaminaLadderUiTests` (Stamina ladder), `AcquiredCardToCombatContractTests` + `ReleaseProfilePersistenceContractTests` (Single Sigil gem pack + Stamina tier purchase, not retired `pack_novice`). Optional human feel still open.
- [x] **Collection:** Agent-proven — burn/evolve UI honesty (Block J) + copy; optional human feel still open.
- [x] **Legal battle deck:** Agent-proven — Deck confirm in spine + normal battle saved-deck contracts. Soft: Deck Builder first-open guidance — `DeckBuilderFirstOpenSoftTests` (Block N).
- [x] **Avatar/Empire screen:** Agent-proven (2026-08-23) — `EmpireAvatarScreenReachabilityTests` proves the real click-through spine Home → Empire → Avatar → Empire → Home never dead-ends (real production button clicks, not direct method calls); `EmpireConstructionHomeTests` covers the construction panel's own upgrade/collect mechanics in isolation (6/6, one stale-wording assertion fixed — the row's own `resourceBonus` value was always correct, only its expected label text ("Resource" vs. the row's real "Cap" wording) was stale). Known non-blocking finding logged separately, not fixed here (file out of scope): `HomePagePresenter.cs`'s nav callbacks call `Destroy()` instead of `DestroyImmediate()` on screen transitions (project rule #7) — harmless in real Play Mode, EditMode-only log noise.
- [x] **Permit + Stamina ladder UI:** Agent-proven — `HomeWeeklyPermitClaimTests` + `CollectionWeeklyPermitClaimTests` (weekly claim, hoard-full status, no dead-end); `ShopStaminaLadderUiTests` (four live Gem tiers on grid, ordered escalation, blocked skip with status).

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

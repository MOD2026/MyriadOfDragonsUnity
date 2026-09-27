# Battle Mechanics-to-Code Implementation Manifest v0.7

Status: HANDOFF; only OWNER-SIGNED rows may be implemented. MOS v1.1 remains authoritative.

| # | Decision / status | CR7 code boundary | Test invariant | Save/migration | UI dependency | Art dependency |
|---:|---|---|---|---|---|---|
| 1 | Hotkey progression — OWNER-OPEN | Bind reinforcement dock count to the approved progression/eligible-card count; no replacement. Touch only reinforcement presentation/input seam around `GameBootstrap` and `TryDeployReinforcement`. | Counts are monotonic; 0–3 visible; fewer eligible cards remain fewer; optional action. | None while live 10–20 is retained. | Rail supports 0/1/2/3 states and disabled outside ticks 4/8. | No fixed-six artwork; muted/active states required. |
| 2 | Reserve formula — OWNER-OPEN | Derive reserve from the same deck after formation; do not create a second pool. Use existing `DealFormationHand`/hand state. | Reserve count equals remaining deck instances; no generic mid-combat refill. | No schema change; any alternative requires migration. | No promise of six cards. | Dynamic reserve slots only. |
| 3 | Duplicate handling — OWNER-OPEN | Preserve source-supplied duplicate instances; no filtering, quota, or auto-replacement. | Duplicate cards remain selectable; no silent substitution. | None under unchanged policy. | Duplicate UI remains ordinary card identity. | No duplicate-specific art required. |
| 4 | Existing-save migration — OWNER-OPEN | Do not alter `DeckSlotCount` or save code for beta. Future cap migration is out of scope. | Existing 16/18/20 values round-trip unchanged. | No migration in beta; future migration needs separate version/rollback packet. | No compensation or migration UI. | None. |
| 5 | Shared Energy — OWNER-SIGNED direction; seed details OPEN | Formation remains Resource; combat spells and reinforcement use Energy. Reinforcement cost maps to `card.ResourceCost`; update only the approved combat input path. | Energy decrements once on success; insufficient Energy is no-op; spells/cooldowns unchanged. | None. | Energy label/cost must be readable and distinct from formation Resource. | No new economy art; affordability state needed. |
| 6 | Beta/live cap — OWNER-OPEN | Preserve live Barracks 10–20 and current `DeckSlotsForBarracksLevel`; do not add a 15 cap. | All current milestones remain valid; no extra formation slots implied. | No save migration. | Do not advertise 15 cards or six guaranteed reserves. | None. |
| 7 | Replay + lane fallback — OWNER-SIGNED behavior; seed schema OPEN | One uniform seeded draw among legal lanes, no reroll; if valid local seed/replay state is unavailable, require explicit card→lane. | Same input log reproduces lane; fallback never uses unseeded randomness; failures mutate nothing. | No schema change until CR7 defines local replay storage. | Show only “Deployed to {lane}”; explicit fallback picker must remain available. | Success/fallback states only; no technical seed display. |

## CR7 gate

CR7 may implement only rows with completed Zihan signatures. Rows 5 and 7 have owner-directed behavior recorded, but their seed/replay details remain open; treat those subfields as gated. No production Save, UI, or art changes are authorized by this manifest.

## Owner-signed content amendment (2026-09-02)

Lane-bonus wording is OWNER-SIGNED by Zihan: `Front: Gain +1 Attack per clash.` `Middle: Gain +1 Health per clash.` `Back: Gain +2 Energy per clash.` This is content binding only; CR mechanics and numeric resolution remain unchanged. See `docs/BATTLE-LANE-BONUS-DECISION-0.2.md`.

## CR-BATTLE-006 seed/replay finalization

Row 7 is now implementation-ready per CR’s supplied contract: `MatchRngSeed` is match-scoped; `_reinforcementRng` is an independent stream; input-log replay stores explicit fallback lanes but re-derives auto lanes; fallback triggers only when `_reinforcementRng == null`; replay uses a fresh `BattleController` with the same seed; deterministic replay is the invariant; migration/storage impact is no migration required. Canonical specification: `docs/BATTLE-SEED-REPLAY-SPECIFICATION-0.7.1.md`. The companion `CR-BATTLE-006-SEED-REPLAY-FINALIZATION.md` is not present in this checkout and remains an evidence-path gap.

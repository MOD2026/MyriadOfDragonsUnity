# Block Q — Stage 1-2 AF + AI-spells-ON 0/40 (CAUSE)

**Date:** 2026-08-23  
**Verdict:** **BALANCE Soft**, not a production softlock / wrong-target bug.

## Observed

| Stage | AF + AI-on (seeds 0–39) | Notes |
|---|---|---|
| 1-1 | resolve smoke OK | weaker roster |
| 1-2 | **0/40** wins | Block O |
| 1-3 | **4/40** (seeds 9,11,32,34) | Victory assert at seed 11 |

## Structural facts (Standing Review + EditMode)

- **1-2 only:** warrior×3 and identical 3/2 stats (`fire_worm`, `butcher`, `cursed_soldier`).
- Totals ATK 9 / HP 6 — between 1-1 (5/3) and 1-3 (11/10); raw totals alone do **not** explain 0/40 vs 1-3’s 4/40.
- Formation synergy tags for 1-2 are **not** a warrior trio: Warriors split by Element (`Ktini`→VenomStrike, `Andras`→DivineHeal) → 1-2 gets an Andras **pair** (+1 ATK), not a 3-of-a-kind. So class-uniformity ≠ full synergy trio.

## Probe (seed 11)

- 1-2 AI-on: enemy casts > 0 (e.g. Firestorm + Divine Bolt), match resolves, **defeat**.
- 1-2 AI-off: **victory** (taught-path / `Chapter1CampaignPlayabilityTests` intact).
- 1-3 AI-on: **victory** (not a global AI softlock).

→ Gap is **Option B spells + 1-2 roster shape**, not residual softlock / empty-lane / silent AI-off. KO paths leave `MatchResult.OutcomeReason` empty by design.

## CC later (out of Block Q)

- Do **not** force `IsVictory` on 1-2 AF+AI-on while rate stays ~0/40.
- Optional: Stage2EnemyDeck retune, or AI aggression Soft.
- If same-class / uniform 3-card pattern is treated as systemic risk: **data-only scan** of campaign 3-card rosters before cosmetic Block P.

**Evidence test:** `CampaignAfMirroredAiSpellWinnabilityTests.Stage1_2_AfAiOn_RootCauseDiagnostics_DocumentsBalanceNotBug`

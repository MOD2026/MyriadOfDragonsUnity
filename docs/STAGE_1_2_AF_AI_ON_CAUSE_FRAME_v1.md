# Stage 1-2 AF + AI-on Cause Frame v1

**READY FOR CC — Yes / No:** Approve this evidence-first decision frame before any Stage 1-2 change is proposed?

**Scope:** Block O framing only. No code, economy, Permit, Forge/Dust, Evolution-Gold, or first-session scope change.

## Facts from Q

| Scenario | Observed outcome |
|---|---:|
| Stage 1-2, all-Warrior player setup (identical 3/2 cards), Auto Formation + AI-on | **0 / 40 victories** |
| Stage 1-3, equivalent probe | **4 / 40 victories** |

These figures establish a repeatable loss pattern. They do **not** alone establish whether the cause is a bug or intended early-stage difficulty.

## CC evidence bar before a decision

The agent must preserve and report:

- the actual stage deck/configuration used;
- the exact player deck/hand, Auto Formation policy, AI-on setting, and seed/repeat policy;
- the final `IsVictory` result for every run;
- confirmation that the probe calls production launch, formation, AI, and combat-resolution paths rather than an alternate simulator;
- one regression test that fails before the chosen fix and passes after it.

## Bug versus Balance Soft

| Evidence branch | CC classification | Next action |
|---|---|---|
| Probe differs from the live route: wrong stage deck, missing campaign context, non-production Auto Formation/AI path, stale state, or a resolution/`IsVictory` mismatch. | **Bug** | Repair the broken production/test contract first. Re-run the same evidence bar. Do not tune enemy cards until this is green. |
| Live route is faithfully reproduced, but equal inputs produce inconsistent resolution or an impossible/incorrect combat state. | **Bug** | Fix determinism, state leakage, or combat correctness. Preserve the stage’s intended content while proving the regression. |
| Live route is faithfully reproduced; combat is deterministic and rule-correct; Stage 1-2’s configured all-Warrior pressure simply defeats the tested Auto Formation policy in all 40 runs. | **Balance Soft** | Queue one bounded Campaign tuning review after the MVP spine and first session are clear. Any proposed change must state the intended player policy and re-run 1-1/1-2/1-3 safety evidence. |
| Live route is faithfully reproduced; the test policy is deliberately weaker than the intended player policy, while the intended manual policy is already proven viable. | **Balance Soft / onboarding clarity** | Keep combat values closed. Improve later guidance only if the first human session shows players naturally use the losing policy without understanding alternatives. |

## Explicit holds

- **No economy or Permit reopen:** no Gold, Gems, Forge/Dust, Evolution Gold, Permit rate/cap, pack, or reward change is in scope.
- **First session stays clear:** `MVP_FIRST_SESSION_SCRIPT_v1` remains the next owner session once the MVP spine is green. Block O is agent evidence work, not a reason to delay it unless it reveals a true launch/resolution bug.
- Do not turn 0/40 into a target win-rate number without a separately approved balance brief.

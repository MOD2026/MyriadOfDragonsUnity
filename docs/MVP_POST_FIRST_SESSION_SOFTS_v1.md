# MVP Post-First-Session Softs v1

**READY FOR CC — Yes / No:** Approve this ranked softs queue after the first human session is clear?

**Entry condition:** Run this queue only after the MVP spine is green and the first-session script has no blocking issue. These are soft follow-ups, not economy or scope reopens.

| Rank | Soft | Why it is next | Evidence owner | Done when |
|---:|---|---|---|---|
| 1 | **AI-on `IsVictory` confidence for Campaign 1-2 / 1-3 (Block O)** | Campaign progression needs trustworthy victory/defeat resolution before feel work is trusted. | **Agent-testable** | Deterministic EditMode coverage proves 1-2 and 1-3 resolve with the intended `IsVictory` outcome under their defined test policy, without deadlock or cross-stage leakage. |
| 2 | **AI-spell feel** | AI spells may be technically correct but still feel unreadable, unfair, or remove player agency. | **Owner-only** for feel; agent can support with smoke/telemetry checks. | Zihan can answer: “I saw it, understood it, and still felt I had a meaningful response.” |
| 3 | **Pack-open feel** | Pack receipts/persistence can be correct while the reveal moment feels flat or confusing. | **Owner-only** for feel; agent can verify receipt/persistence. | Zihan can identify what was received, why it matters, and whether the reveal feels worth repeating. |

## Guardrails

- Do not change Gold, Gems, Evolution Gold ×1000, Forge/Dust yields, Permit issuance/caps, pack SKU values, or Save shape while clearing these softs.
- A human-feel note becomes one consolidated task only if it blocks comprehension or motivation; do not reopen screenshot-by-screenshot polish.
- Block O remains first because agent evidence can either clear it quickly or identify a real campaign-resolution defect before owner time is spent on feel.

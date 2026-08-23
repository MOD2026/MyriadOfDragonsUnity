# Permit Week-Key Server Stub v1

**READY FOR CC — Yes / No:** Lock this as the later server-authoritative replacement for `ManualTrustedWeekKey`?

**NOT FOR THIS WEEK'S BUILD.** This is a later Option A server design only. The shipping stopgap remains **8 Permits/week with a 16-Permit hoard cap**. The future design target remains **4/week with an 8-Permit hoard cap** once trusted server issuance exists.

## Trusted week key

| Item | Later Option A rule |
|---|---|
| Key format | ISO-8601 week key: `YYYY-Www` (example: `2026-W34`). |
| Authority | Backend game service issues the key from its own UTC clock; the client never calculates an authoritative eligibility key. |
| Claim identity | Server evaluates `accountId + activityId + ISO week key` as one idempotent weekly-claim record. |
| Client role | Client requests status, displays the returned balance/claim state, and submits a claim request. Local clock changes cannot create a second claim. |

## Replacement contract

| Today | Later server path |
|---|---|
| `ManualTrustedWeekKey` is a local/test stopgap. | Backend-issued ISO week key becomes the only eligibility authority. |
| Local weekly record is advisory. | Server weekly receipt is authoritative; client mirrors the response for presentation/offline recovery only. |
| 8/week, hoard 16. | Design target 4/week, hoard 8, enabled only with server issuance and reconciliation. |

## Save-safe migration one-pager

1. **Do not alter PlayerProfile for this stub.** No new local Save field is required to make a server the authority.
2. On first server-enabled login, backend reads or grants a one-time transition receipt according to the account's verified entitlement policy; it does not trust a device clock or replay old local week keys.
3. Existing local `ManualTrustedWeekKey` data is retained only as legacy client state during rollout and is never used to mint an additional server claim.
4. Backend responds with canonical Permit balance, current ISO week key, cap, and claim receipt. Client reconciles its display to that response.
5. After rollout telemetry and recovery support are proven, remove the manual key only in a separately approved migration/cleanup task.

## Non-goals

- No change to this week's Permit earn, caps, Economy values, player Save shape, or Unity code.
- No local-clock fallback that grants a weekly Permit claim.
- No purchase path for Permits.

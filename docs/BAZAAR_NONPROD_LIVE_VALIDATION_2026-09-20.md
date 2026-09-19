# Bazaar settlement saga + reconciliation sweep - live nonprod-validation run (2026-09-20)

**Environment:** `nonprod-validation` only (`c27cd7cc-...`), project `72b99c7b-5221-4822-9a04-73677f2c0124`.
Production was not touched (it still has zero Cloud Code modules). **Base commit:** `b6ee9fe5`.
All players/records are synthetic (`bzlive-*` ids, four anonymous players S=seller, B/C/D=buyers). No credential
appears in this document; player tokens were held in memory/a scratch file and deleted afterwards.

## Results

| # | Required validation | Result | Live evidence |
|---|---|---|---|
| 1 | Cross-account buyer debit | PASS | B wallet 1000 -> 900 on `BuyBazaarItem` (ask 100), HTTP 200 |
| 2 | Seller `ServiceToken` credit | PASS | S wallet 0 -> 88 (100 - 12% tax) - a write to another player's Cloud Save, no `Forbidden` |
| 3 | Saga resume | PASS | Partial settlement (C already debited 200, seller not credited, listing claimed, journal `InProgress`): C retried the same key -> success; C stayed **800** (not 600), S credited once (88 -> 264), instance -> C, listing removed from index |
| 4 | Idempotent retry | PASS | Same key replayed for K1 and K2: byte-identical response, wallets unchanged |
| 5 | Reconciliation sweep | PASS | Sweep #1 (caller = seller S, so every buyer record is cross-account): scanned 7, 2x `RecoveredSettlement` (D 700 -> 400, C 800 -> 600, each charged once; S 528 -> 968), healthy listing untouched; sweep #2: scanned 1, `isClean=true` |
| 6 | Stale-index repair | PASS | Removed: completed sale still indexed (buyer B, no re-charge, B stayed 900), cancelled listing, sold-without-journal listing, missing listing record. Final index = only the healthy live listing |

Final Cloud Save state read back directly: every purchased instance owned by its buyer (`state=Owned`,
`lastAcquisitionWasPurchase=true`), every settled listing `Sold` with its buyer's settlementId, index = `[healthy listing]`.

## Defects the live run found (in-memory fakes cannot reproduce token semantics) - both fixed in this commit

1. **Sweep aborted the whole board on one cross-account read.** Buyer idempotency results and settlement journals live in
   the *buyer's* player-scoped Cloud Save but were read/written with `AccessToken`. The sweep runs as some other caller, so
   reading another buyer's record was rejected (`BazaarStorageException` -> HTTP 422) and that single exception aborted the
   sweep partway. Fix: those four store methods use `ServiceToken` (same class as Friends BE-FRIENDS-STORAGE-014; wallets were
   already moved in d6ee7fbc).
2. **One bad listing must not abort the rest.** The sweep now reports a per-listing storage failure as `DeferredFailure` and
   continues; the next sweep retries it (every step is idempotent). Regression test added
   (`Sweep_AStorageFailureOnOneListing_IsDeferred_AndTheRestOfTheBoardIsStillReconciled`).

## Method / repeatable runbook (nothing here decides cadence, alerting, or ownership)

1. **Deploy target:** only ever `--environment-name nonprod-validation`. Confirm with `ugs env list` that production is not the active env.
2. **Temporary endpoint (never committed):** the sweep is a library class with no endpoint by design. To invoke it live, copy
   `CloudCode/Bazaar` (without `bin/obj`) to a scratch directory, add the file below, and
   `ugs deploy <scratch dir> --services cloud-code-modules --environment-name nonprod-validation --project-id <id>`.
3. **Players:** `POST https://player-auth.services.api.unity.com/v1/authentication/anonymous` with headers `ProjectId` and
   `UnityEnvironment: nonprod-validation` returns a real anonymous player (`userId`, `idToken`). Cloud Code calls are
   `POST https://cloud-code.services.api.unity.com/v1/projects/<id>/modules/Bazaar/<Function>` with `Authorization: Bearer <idToken>`
   and body `{"params": {"request": {...}}}`. Never print or store tokens beyond the run.
4. **Seed (admin CLI, service account):** wallets/journals are player data (`ugs cloud-save data player set --player-id ... --key
   bazaar_wallet|bazaar_settle_<hash>`); instances/listings/index are custom entity `bazaar-board`
   (`ugs cloud-save data custom set --custom-id bazaar-board --key instance_<hash>|listing_<hash>|bazaar_index`), where
   `<hash>` = first 32 hex chars of SHA-256 of the id/key. Write the index with `--writelock` when it already exists.
   The module has no endpoint that mints an `ItemInstance`, so live listings need seeded instances.
5. **Stuck states:** a partial settlement is seeded as: listing `Sold` with `settlementId = "<buyerId>|<key>"`, journal `InProgress`
   with frozen amounts, listing id still in the index; optionally the buyer wallet already debited with the settlement id in
   `appliedSettlementIds` (proves no double debit).
6. **Cleanup of the temporary endpoint:** redeploy the clean repo module from `CloudCode/Bazaar` and confirm
   `ugs cloud-code modules get Bazaar` lists only the 5 permanent endpoints. (Done: DateModified 2026-09-19T16:33:25Z UTC.)

Temporary endpoint source used (scratch copy only):

```csharp
public sealed class TempBazaarValidationModule
{
    private readonly BazaarReconciliationSweep _sweep;
    public TempBazaarValidationModule()
    {
        var store = new CloudSaveBazaarStore();
        var clock = new SystemBazaarClock();
        _sweep = new BazaarReconciliationSweep(store, new BazaarOperations(store, clock, new RemoteConfigBazaarRulesConfiguration()), clock);
    }

    [CloudCodeFunction("TempValidateReconcileBazaar")]
    public async Task<TempValidationSweepResult> Run(IExecutionContext context, IGameApiClient apiClient, TempValidationSweepRequest request)
    {
        var r = await _sweep.SweepAsync(context, apiClient, request?.MinJournalAgeMs ?? 0);
        return new TempValidationSweepResult { Scanned = r.Scanned, IsClean = r.IsClean,
            Findings = r.Findings.Select(f => new TempValidationFinding { ListingId = f.ListingId, Kind = f.Kind.ToString(), Detail = f.Detail }).ToList() };
    }
}
```

## Still open (explicitly NOT decided here)

- **Production exposure of the sweep:** whether it becomes a `CloudCodeFunction`, a scheduled trigger, or a support tool, who may invoke it, and
  any authorization on that entry point. (The temporary endpoint above was callable by any authenticated nonprod player while deployed.)
- Run cadence, minimum journal age, alert thresholds, on-call/ownership - product/ops decisions; `minJournalAgeMs` remains a caller-supplied argument.
- Journals orphaned before their listing was claimed are invisible to the sweep by design (they changed nothing).
- Production deployment of the Bazaar module (production has zero modules) requires explicit owner approval.
- Synthetic `bzlive-*` records remain in `nonprod-validation` Cloud Save (harmless, synthetic); remove at the owner's discretion.

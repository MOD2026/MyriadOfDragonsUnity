# Bazaar nonprod validation support (saga + reconciliation sweep)

Repeatable, **nonprod-validation-only** harness that exercises the Bazaar settlement saga and the
reconciliation sweep through the real deployed module, with real anonymous players, real Cloud Save,
and the real `ServiceToken` cross-account paths. In-memory tests cannot prove token semantics; this can
(it found two real defects on 2026-09-20 - see `docs/BAZAAR_NONPROD_LIVE_VALIDATION_2026-09-20.md`).

## Callable validation path (confirmed live)

| Validation | How it is invoked | Needs a temporary endpoint? |
|---|---|---|
| Real cross-account sale, buyer debit + seller `ServiceToken` credit | `ListBazaarItem` then `BuyBazaarItem` as two different real players | No - existing endpoints |
| Same-key retry / idempotency | `BuyBazaarItem` again with the same `idempotencyKey` | No |
| Saga resume | Seeded partial settlement, then the buyer retries `BuyBazaarItem` with the same key | No (seeding uses the admin CLI, because the module has no endpoint that mints an `ItemInstance`) |
| Reconciliation sweep recovery | `BazaarReconciliationSweep.SweepAsync` | **Yes** - the sweep is a library class with no entry point by design |
| Stale-index repair | same sweep | **Yes** |

`TempValidationEndpoint.cs.txt` is that temporary entry point. It is a `.txt` so it is never compiled into
`CloudCode/Bazaar`; `prepare` deploys a **scratch copy** of the module with it added, `cleanup` redeploys the
clean repo module and verifies only the five permanent endpoints remain. A guard test
(`BazaarEndpointSurfaceTests`) fails the build if any unreviewed `CloudCodeFunction` - including anything
like the temporary hook - is added to the repo module.

## Usage

Prerequisites: UGS CLI logged in with a service account (`ugs login`), .NET SDK, Python 3.

```
python bazaar_validation.py prepare  [--ugs <path-to-ugs>]   # deploy scratch copy + temp endpoint (nonprod-validation)
python bazaar_validation.py run      [--ugs <path>] [--allow-foreign-index-entries]
python bazaar_validation.py cleanup  [--ugs <path>]          # ALWAYS run afterwards
```

`run` prints PASS/FAIL per check and exits non-zero on any failure; it writes `_work/results.json` (no tokens).
It refuses to run if the live board index already has entries (the sweep would repair them too) unless
`--allow-foreign-index-entries` is passed. The environment is a hard-coded constant; there is no option to
target another environment, and the script asserts it is not `production`.

Note: on a machine where a Store-installed Python cannot see the UGS CLI's folder, copy `ugs.exe` somewhere
outside `AppData` and pass it with `--ugs`.

## What `run` asserts (28 checks; relationships, not economy values)

Buyer debited exactly the price; seller credited exactly its share; seller + burn + treasury == price;
same-key retry byte-identical and charges nothing; resume of a partially-settled sale does **not** re-debit the
buyer and credits the seller exactly once; the sweep (invoked by the seller, so every buyer record is
cross-account) recovers two stuck settlements with each buyer charged once, removes stale entries for a
completed sale / cancelled listing / pre-saga sold listing / missing record without touching money, leaves a
healthy listing alone, reports findings in index order, and a second sweep reports nothing and changes no
balance; instances end owned by their buyers. Prices, balances and journal amounts are synthetic fixture
inputs; the journal split is scaled from the module's own observed split, so no rule values are assumed.

Last full run: 2026-09-20, `nonprod-validation`, run id `r005915`, **28/28 passed**; `cleanup` confirmed the five
permanent endpoints, `HasError:false`.

## Owner decisions required (deliberately NOT made here)

1. **Does the sweep get a permanent entry point, and which kind?** (a `CloudCodeFunction`, a scheduled/triggered
   invocation, or an operator/support tool). No endpoint or scheduler was invented. Until decided, the only
   callable path is the temporary, scratch-deployed, nonprod-only hook above, which is callable by any
   authenticated player in nonprod-validation while deployed.
2. **Who may invoke it and with what authorization** (any player? service/admin only?) - required before any
   entry point exists, since the sweep mutates other players' wallets/records with server authority.
3. **Cadence, minimum journal age (`minJournalAgeMs` is a caller argument, default 0), alert thresholds, and
   on-call/ownership** for stuck settlements and stale index entries.
4. **Whether CI may run this harness** (it needs a service-account login and creates synthetic players/records in
   nonprod-validation) and who owns cleaning the synthetic `bzlive-*` records it leaves behind.
5. **Production**: Bazaar has never been deployed to production (production has zero modules); that requires
   explicit owner approval and is out of scope here.

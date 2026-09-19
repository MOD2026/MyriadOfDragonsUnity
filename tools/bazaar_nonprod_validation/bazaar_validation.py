#!/usr/bin/env python3
"""Bazaar settlement-saga + reconciliation-sweep validation harness - nonprod-validation ONLY.

  python bazaar_validation.py prepare   # deploy a SCRATCH COPY of the module + temporary sweep endpoint
  python bazaar_validation.py run       # real cross-account sale, retry, saga resume, sweep, stale-index repair
  python bazaar_validation.py cleanup   # redeploy the clean repo module; verify only permanent endpoints remain

Requires the UGS CLI logged in with a service account (`ugs login`) and .NET SDK. Pass --ugs <path> if `ugs`
is not on PATH. Player tokens live in this process's memory only - nothing token-bearing is written to disk.
Assertions are relationships (debit == price, seller credit + burn + treasury == price, no double charge...),
never economy magnitudes; prices/balances below are synthetic fixture inputs, not game values.
"""
import argparse, hashlib, json, os, shutil, subprocess, sys, time, urllib.error, urllib.request

PROJECT = "72b99c7b-5221-4822-9a04-73677f2c0124"
ENV = "nonprod-validation"
FORBIDDEN_ENVS = {"production"}
assert ENV not in FORBIDDEN_ENVS, "this harness must never target production"

HERE = os.path.dirname(os.path.abspath(__file__))
REPO_MODULE = os.path.normpath(os.path.join(HERE, "..", "..", "CloudCode", "Bazaar"))
WORK_DIR = os.path.join(HERE, "_work")
TEMP_FUNCTION = "TempValidateReconcileBazaar"
PERMANENT_ENDPOINTS = {"ListBazaarItem", "BuyBazaarItem", "CancelBazaarListing", "GetBazaarWallet", "QueryBazaarListings"}

BOARD = "bazaar-board"
INDEX_KEY = "bazaar_index"
WALLET_KEY = "bazaar_wallet"


def sha32(value):
    return hashlib.sha256(value.encode("utf-8")).hexdigest()[:32]


def instance_key(i): return "instance_" + sha32(i)
def listing_key(l): return "listing_" + sha32(l)
def settle_key(k): return "bazaar_settle_" + sha32(k)


# ----------------------------------------------------------------------------- CLI

class Ugs:
    def __init__(self, exe):
        self.exe = exe

    def run(self, *args, project=True):
        cmd = [self.exe, *args]
        if project:
            cmd += ["--project-id", PROJECT, "--environment-name", ENV]
        cmd.append("--json")
        out = subprocess.run(cmd, capture_output=True, text=True, timeout=300, stdin=subprocess.DEVNULL)
        text = out.stdout
        for i, ch in enumerate(text):
            if ch in "[{":
                try:
                    return json.loads(text[i:])
                except ValueError:
                    continue
        return {"_raw": text[:400], "_err": out.stderr[:400], "_rc": out.returncode}

    def deploy(self, path, dry_run=False):
        args = ["deploy", path, "--services", "cloud-code-modules"]
        if dry_run:
            args.append("--dry-run")
        return self.run(*args)

    def module_endpoints(self):
        info = self.run("cloud-code", "modules", "get", "Bazaar")
        return info, set((info.get("Endpoints") or {}).keys())

    def custom_get(self, key):
        r = self.run("cloud-save", "data", "custom", "get", "--custom-id", BOARD, "--keys", key)
        items = r.get("Items") or r.get("items") or [] if isinstance(r, dict) else []
        if not items:
            return None, None
        it = items[0]
        val = it.get("Value") if "Value" in it else it.get("value")
        wl = it.get("WriteLock") or it.get("writeLock")
        return (json.loads(val) if isinstance(val, str) else val), wl

    def custom_set(self, key, value, writelock=None):
        args = ["cloud-save", "data", "custom", "set", "--custom-id", BOARD, "--key", key, "--value", json.dumps(value, separators=(",", ":"))]
        if writelock:
            args += ["--writelock", writelock]
        return self.run(*args)

    def player_set(self, player_id, key, value):
        return self.run("cloud-save", "data", "player", "set", "--player-id", player_id, "--key", key, "--value", json.dumps(value, separators=(",", ":")))

    def index_append(self, ids):
        idx, wl = self.custom_get(INDEX_KEY)
        idx = idx or {"activeListingIds": []}
        idx["activeListingIds"] += ids
        self.custom_set(INDEX_KEY, idx, wl)


# ----------------------------------------------------------------------------- players (REST, tokens in memory only)

def _http(method, url, headers, body=None):
    data = None if body is None else json.dumps(body).encode("utf-8")
    req = urllib.request.Request(url, data=data, method=method, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=60) as resp:
            return resp.status, json.loads(resp.read().decode("utf-8") or "{}")
    except urllib.error.HTTPError as e:
        raw = e.read().decode("utf-8", "replace")
        try:
            return e.code, json.loads(raw)
        except ValueError:
            return e.code, {"raw": raw[:300]}


class Player:
    def __init__(self, label):
        status, body = _http("POST", "https://player-auth.services.api.unity.com/v1/authentication/anonymous",
                             {"ProjectId": PROJECT, "UnityEnvironment": ENV, "Content-Type": "application/json"}, {})
        assert status == 200, f"anonymous sign-in failed: {status}"
        self.label, self.id, self._token = label, body["userId"], body["idToken"]

    def call(self, fn, request=None):
        url = f"https://cloud-code.services.api.unity.com/v1/projects/{PROJECT}/modules/Bazaar/{fn}"
        status, body = _http("POST", url, {"Authorization": f"Bearer {self._token}", "Content-Type": "application/json"},
                             {"params": {"request": request} if request is not None else {}})
        return status, (body.get("output", body) if isinstance(body, dict) else body)

    def balance(self):
        status, out = self.call("GetBazaarWallet")
        assert status == 200, out
        return out["balanceCredits"]


# ----------------------------------------------------------------------------- prepare / cleanup

def cmd_prepare(ugs, args):
    if os.path.isdir(WORK_DIR):
        shutil.rmtree(WORK_DIR)
    copy = os.path.join(WORK_DIR, "BazaarValidationCopy")
    shutil.copytree(REPO_MODULE, copy, ignore=shutil.ignore_patterns("bin", "obj"))
    shutil.copyfile(os.path.join(HERE, "TempValidationEndpoint.cs.txt"), os.path.join(copy, "TEMP_NonprodValidationEndpoint.cs"))
    build = subprocess.run(["dotnet", "build", os.path.join(copy, "Bazaar.csproj")], capture_output=True, text=True)
    if build.returncode != 0:
        print(build.stdout[-1500:]); sys.exit("scratch copy failed to build")
    print("scratch copy builds; deploying it to", ENV)
    dry = ugs.deploy(copy, dry_run=True)
    print("dry run:", json.dumps(dry)[:200])
    ugs.deploy(copy)
    info, endpoints = ugs.module_endpoints()
    ok = TEMP_FUNCTION in endpoints and not info.get("HasError")
    print(f"deployed. endpoints={sorted(endpoints)} hasError={info.get('HasError')}")
    print("PREPARE", "OK" if ok else "FAILED")
    sys.exit(0 if ok else 1)


def cmd_cleanup(ugs, args):
    ugs.deploy(REPO_MODULE)
    info, endpoints = ugs.module_endpoints()
    ok = endpoints == PERMANENT_ENDPOINTS and not info.get("HasError")
    print(f"clean module redeployed. endpoints={sorted(endpoints)} hasError={info.get('HasError')} dateModified={info.get('DateModified')}")
    print("CLEANUP", "OK (temporary endpoint removed)" if ok else "FAILED - unexpected endpoint set")
    if os.path.isdir(WORK_DIR):
        shutil.rmtree(WORK_DIR)
    sys.exit(0 if ok else 1)


# ----------------------------------------------------------------------------- run

class Report:
    def __init__(self):
        self.rows = []

    def check(self, name, ok, detail=""):
        self.rows.append({"check": name, "pass": bool(ok), "detail": detail})
        print(("  PASS  " if ok else "  FAIL  ") + name + (f"  [{detail}]" if detail else ""))
        return ok


def cmd_run(ugs, args):
    rep = Report()
    _, endpoints = ugs.module_endpoints()
    if TEMP_FUNCTION not in endpoints:
        sys.exit(f"{TEMP_FUNCTION} is not deployed to {ENV}. Run `prepare` first (and `cleanup` afterwards).")
    idx, _ = ugs.custom_get(INDEX_KEY)
    foreign = (idx or {}).get("activeListingIds") or []
    if foreign and not args.allow_foreign_index_entries:
        sys.exit(f"The live board index already has {len(foreign)} entries; the sweep would repair them too. "
                 "Re-run with --allow-foreign-index-entries if that is acceptable in nonprod-validation.")

    run = "r" + time.strftime("%H%M%S")
    print(f"run id {run}; creating four real anonymous players in {ENV}")
    S, B, C, D = (Player(x) for x in "SBCD")
    mk = lambda n: f"bzlive-{run}-{n}"
    inst = lambda i, owner, state: {"instanceId": i, "definitionId": "card.bzlive.synthetic", "ownerId": owner, "rarity": 2, "tradeable": True,
                                    "state": state, "lastAcquiredUtcMs": 1000, "lastAcquisitionWasPurchase": False}
    now = int(time.time() * 1000)
    lst = lambda l, i, ask, state, st=None: {"listingId": l, "instanceId": i, "sellerId": S.id, "askCredits": ask, "state": state, "createdUtcMs": now,
                                             **({"settlementId": st} if st else {})}
    wallet = lambda p, bal, applied=None: {"accountId": p.id, "balanceCredits": bal, "recentSaleUtcMs": [], **({"appliedSettlementIds": applied} if applied else {})}

    START = 1000  # synthetic fixture balance
    ugs.player_set(S.id, WALLET_KEY, wallet(S, 0))
    ugs.player_set(B.id, WALLET_KEY, wallet(B, START))
    ugs.custom_set(instance_key(mk("inst1")), inst(mk("inst1"), S.id, 0))

    # ---- 1/2/4: real cross-account sale (buyer debit + seller ServiceToken credit), then same-key retry
    print("\n[1] cross-account sale + [2] seller ServiceToken credit + [4] idempotent retry")
    ASK1 = 100
    s0, b0 = S.balance(), B.balance()
    st, listed = S.call("ListBazaarItem", {"instanceId": mk("inst1"), "askCredits": ASK1})
    rep.check("seller lists a seeded instance", st == 200 and listed.get("success"), str(listed.get("errorCode")))
    l1 = listed["listingId"]
    st, first = B.call("BuyBazaarItem", {"listingId": l1, "idempotencyKey": "K1"})
    rep.check("buy succeeds", st == 200 and first.get("success"), str(first.get("errorCode")))
    price, seller_got = first["pricePaidCredits"], first["sellerReceivedCredits"]
    burn, treasury = first["taxBurnedCredits"], first["taxTreasuryCredits"]
    rep.check("buyer debited exactly the price (cross-account)", B.balance() == b0 - price, f"{b0}->{B.balance()}")
    rep.check("seller credited exactly its share (ServiceToken cross-account write)", S.balance() == s0 + seller_got, f"{s0}->{S.balance()}")
    rep.check("credits conserved: seller + burn + treasury == price", seller_got + burn + treasury == price)
    st2, second = B.call("BuyBazaarItem", {"listingId": l1, "idempotencyKey": "K1"})
    rep.check("same-key retry returns the identical result", st2 == 200 and second == first)
    rep.check("same-key retry charges nothing further", B.balance() == b0 - price and S.balance() == s0 + seller_got)

    def scaled(ask):  # split the fixture ask using the module's OWN observed split - no economy numbers assumed
        k, rem = divmod(ask, price)
        assert rem == 0, "fixture asks must be multiples of the first sale's ask"
        return k * seller_got, k * burn, k * treasury

    # ---- 3: saga resume from a genuinely partial settlement (buyer already debited, seller not credited)
    print("\n[3] saga resume from a partial settlement")
    ASK2 = ASK1 * 2
    sg2, bn2, tr2 = scaled(ASK2)
    ugs.player_set(C.id, WALLET_KEY, wallet(C, START - ASK2, [f"{C.id}|K2"]))
    ugs.custom_set(instance_key(mk("inst2")), inst(mk("inst2"), S.id, 1))
    ugs.custom_set(listing_key(mk("L2")), lst(mk("L2"), mk("inst2"), ASK2, 1, f"{C.id}|K2"))
    journal = lambda buyer, key, l, i, ask, split: {"settlementId": f"{buyer.id}|{key}", "phase": "InProgress", "buyerId": buyer.id, "sellerId": S.id,
                                                    "listingId": l, "instanceId": i, "priceCredits": ask, "sellerReceivesCredits": split[0],
                                                    "taxBurnCredits": split[1], "taxTreasuryCredits": split[2], "startedUtcMs": now}
    ugs.player_set(C.id, settle_key("K2"), journal(C, "K2", mk("L2"), mk("inst2"), ASK2, (sg2, bn2, tr2)))
    ugs.index_append([mk("L2")])
    c0, s1 = C.balance(), S.balance()
    st, resumed = C.call("BuyBazaarItem", {"listingId": mk("L2"), "idempotencyKey": "K2"})
    rep.check("resume completes the sale", st == 200 and resumed.get("success"), str(resumed.get("errorCode")))
    rep.check("buyer NOT charged a second time", C.balance() == c0, f"{c0}->{C.balance()}")
    rep.check("seller credited exactly once", S.balance() == s1 + sg2, f"{s1}->{S.balance()}")
    rep.check("resume replay is identical + still no charge", C.call("BuyBazaarItem", {"listingId": mk("L2"), "idempotencyKey": "K2"})[1] == resumed and C.balance() == c0)

    # ---- 5/6: sweep recovery (own + cross-account) and stale-index repair
    print("\n[5] reconciliation sweep + [6] stale-index repair (caller = seller, so every buyer record is cross-account)")
    ASK8, ASK9 = ASK1 * 3, ASK1 * 2
    sg8, bn8, tr8 = scaled(ASK8); sg9, bn9, tr9 = scaled(ASK9)
    ugs.player_set(D.id, WALLET_KEY, wallet(D, START))
    for n, ask, buyer, key, split in ((8, ASK8, D, "K4", (sg8, bn8, tr8)), (9, ASK9, C, "K5", (sg9, bn9, tr9))):
        ugs.custom_set(instance_key(mk(f"inst{n}")), inst(mk(f"inst{n}"), S.id, 1))
        ugs.custom_set(listing_key(mk(f"L{n}")), lst(mk(f"L{n}"), mk(f"inst{n}"), ask, 1, f"{buyer.id}|{key}"))
        ugs.player_set(buyer.id, settle_key(key), journal(buyer, key, mk(f"L{n}"), mk(f"inst{n}"), ask, split))
    ugs.custom_set(listing_key(mk("L10")), lst(mk("L10"), "bzlive-none", 50, 2))           # cancelled, still indexed
    ugs.custom_set(listing_key(mk("L11")), lst(mk("L11"), "bzlive-none", 50, 1))           # sold before the saga (no settlementId)
    ugs.index_append([mk("L8"), mk("L9"), mk("L10"), mk("L11"), mk("ghost")])              # ghost: no listing record
    ugs.custom_set(instance_key(mk("inst6")), inst(mk("inst6"), S.id, 0))
    st, healthy = S.call("ListBazaarItem", {"instanceId": mk("inst6"), "askCredits": ASK1})
    rep.check("a healthy live listing exists on the board", st == 200 and healthy.get("success"))
    ugs.index_append([l1])                                                                  # completed sale left stale in the index
    d0, c1, s2 = D.balance(), C.balance(), S.balance()
    pre_index, _ = ugs.custom_get(INDEX_KEY)

    st, sweep1 = S.call(TEMP_FUNCTION, {"minJournalAgeMs": 0})
    rep.check("sweep call succeeds (cross-account reads/writes)", st == 200, str(sweep1)[:200] if st != 200 else "")
    mine = {mk("L8"), mk("L9"), mk("L10"), mk("L11"), mk("ghost"), l1}
    kinds = {f["listingId"]: f["kind"] for f in sweep1.get("findings", [])}
    expect = {mk("L8"): "RecoveredSettlement", mk("L9"): "RecoveredSettlement", mk("L10"): "RemovedStaleIndexEntry",
              mk("L11"): "RemovedStaleIndexEntry", mk("ghost"): "RemovedStaleIndexEntry", l1: "RemovedStaleIndexEntry"}
    rep.check("sweep findings match expectations for every seeded case", {k: kinds.get(k) for k in mine} == expect, json.dumps({k[-6:]: kinds.get(k) for k in mine}))
    rep.check("healthy listing untouched by the sweep", healthy["listingId"] not in kinds)
    order = [f["listingId"] for f in sweep1.get("findings", []) if f["listingId"] in mine]
    rep.check("findings follow index order (deterministic)", order == [x for x in pre_index["activeListingIds"] if x in mine], "")
    rep.check("recovered buyer D charged exactly its price once", D.balance() == d0 - ASK8, f"{d0}->{D.balance()}")
    rep.check("recovered buyer C (cross-account) charged exactly its price once", C.balance() == c1 - ASK9, f"{c1}->{C.balance()}")
    rep.check("seller credited exactly the two recovered shares", S.balance() == s2 + sg8 + sg9, f"{s2}->{S.balance()}")
    rep.check("stale completed sale did not re-charge its buyer", B.balance() == b0 - price)
    final_index, _ = ugs.custom_get(INDEX_KEY)
    rep.check("stale/cancelled/legacy/ghost/completed entries are gone from the index", not (set(final_index["activeListingIds"]) & mine))
    rep.check("healthy listing still indexed", healthy["listingId"] in final_index["activeListingIds"])

    st, sweep2 = S.call(TEMP_FUNCTION, {"minJournalAgeMs": 0})
    rep.check("second sweep reports nothing for the seeded cases (idempotent replay)", st == 200 and not [f for f in sweep2.get("findings", []) if f["listingId"] in mine])
    rep.check("second sweep changed no balances", (D.balance(), C.balance(), S.balance()) == (d0 - ASK8, c1 - ASK9, s2 + sg8 + sg9))
    for n, buyer in ((1, B), (2, C), (8, D), (9, C)):
        rec, _ = ugs.custom_get(instance_key(mk(f"inst{n}")))
        rep.check(f"instance {n} is owned by its buyer and Owned", rec and rec["ownerId"] == buyer.id and rec["state"] == 0)

    passed = sum(1 for r in rep.rows if r["pass"])
    out = os.path.join(WORK_DIR if os.path.isdir(WORK_DIR) else HERE, "results.json")
    json.dump({"environment": ENV, "run": run, "passed": passed, "total": len(rep.rows), "checks": rep.rows}, open(out, "w"), indent=2)
    print(f"\n{passed}/{len(rep.rows)} checks passed; results (no tokens) -> {out}")
    print("Remember: run `cleanup` to remove the temporary endpoint. Synthetic bzlive-* records remain in", ENV)
    sys.exit(0 if passed == len(rep.rows) else 1)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("command", choices=["prepare", "run", "cleanup"])
    ap.add_argument("--ugs", default="ugs", help="path to the UGS CLI executable")
    ap.add_argument("--allow-foreign-index-entries", action="store_true")
    args = ap.parse_args()
    ugs = Ugs(args.ugs)
    {"prepare": cmd_prepare, "run": cmd_run, "cleanup": cmd_cleanup}[args.command](ugs, args)


if __name__ == "__main__":
    main()

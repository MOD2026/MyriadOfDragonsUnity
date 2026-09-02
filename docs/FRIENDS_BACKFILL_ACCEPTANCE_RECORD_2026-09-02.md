# Friends Backfill + Live Acceptance Record — 2026-09-02

Owner tasks closed by this record: BE-FRIENDS-STORAGE-014, BE-FRIENDS-LIVE-007.
Status: **COMPLETE**. All live evidence below is real, captured from actual
`nonprod-validation` runs — no invented ids or responses.

## 1. Deployment provenance

- Fix commit: `2c53d692` (`fix(friends): use ServiceToken for cross-account alias/index Cloud Save calls`)
- Seven-check test commit: `30d4f2ec` (`test(friends): add live 7-check happy-path validation (BE-FRIENDS-LIVE-007)`)
- Branch: `vip/plan-socket-mapping-v1`
- Deployed via: `ugs deploy CloudCode/Friends --services cloud-code-modules --environment-name nonprod-validation --project-id 72b99c7b-5221-4822-9a04-73677f2c0124`
- Verified live via `ugs cloud-code modules get Friends --environment-name nonprod-validation --project-id 72b99c7b-5221-4822-9a04-73677f2c0124 --json`:
  - `DateModified: 2026-09-02T09:43:07` (matches the ServiceToken fix deploy, confirmed newer than the pre-fix `2026-09-02T08:44:17`)
  - `HasError: false`
  - 7 endpoints present: `AcceptFriend`, `AddFriend`, `BackfillFriendsAliasReverseIndex`, `DeclineFriend`, `ListFriends`, `RemoveFriend`, `SendDailyGift`
- Local build/test verification before each deploy: `dotnet build` (CloudCode/Friends) 0 errors; `dotnet test` (Friends.ServerTests) 35/35 passed.

## 2. Root cause (BE-FRIENDS-STORAGE-014)

`CloudSaveFriendsStore.LoadIndexAsync/SaveIndexAsync/LoadCounterpartAliasAsync/SaveCounterpartAliasAsync`
used `context.AccessToken` on Cloud Save calls addressed at an explicit target
account id not guaranteed to equal the caller's own `context.PlayerId`.
Unity Cloud Save's player-scoped API only permits `AccessToken` calls for the
signed-in player's own data — cross-account addressing returned a genuine
`ApiException: Forbidden`, which `ClassifyStorageError` collapsed into the
generic `STORAGE_UNAVAILABLE` label. Fixed by switching those four methods to
`context.ServiceToken`, matching the store's existing Custom Items precedent.

## 3. Backfill acceptance evidence (BE-FRIENDS-STORAGE-014)

Real PlayMode run, two genuinely distinct authenticated identities
(`FriendsBackfillLiveExecutionPlayModeTests.RunBackfillTwiceWithTwoRealPlayerIds`),
post-fix, post-redeploy:

```
EVIDENCE playerIdA=afzHiO2BtxdIaShGOgKj8MeO5O55
EVIDENCE playerIdB=WWlNJ2a9GV7HZOhc6D3430UCj1nT
EVIDENCE RUN1 accountsScanned=2 aliasesUpdated=1 aliasesSkipped=1 accountsFailed=0 failedAccountIds=[] errors=[]
EVIDENCE RUN2 accountsScanned=2 aliasesUpdated=0 aliasesSkipped=2 accountsFailed=0 failedAccountIds=[] errors=[]
EVIDENCE TIMESTAMP_UTC=2026-09-02T09:45:35.9337608Z
EVIDENCE ENVIRONMENT=nonprod-validation
```

Acceptance: run 1 `accountsFailed: 0` — met. Run 2 `aliasesUpdated: 0` — met.
`results.xml`: `Passed`. `run.log`: 0 occurrences of `error CS`.

## 4. Seven friend/gift checks (BE-FRIENDS-LIVE-007)

Real PlayMode run, two independently-resumable authenticated identities via
`SwitchProfile` + non-clearing `SignOut`
(`FriendsLiveSevenCheckPlayModeTests.RunSevenChecksWithTwoRealPlayerIds`):

```
EVIDENCE playerIdA=7mx9FQZnPjtUwXBkSQG7Kq8RBrAu
EVIDENCE playerIdB=9ABZ9Wmy1A57e6eRp38V94O3okgY
EVIDENCE TIMESTAMP_UTC=2026-09-02T12:15:24.6878172Z
EVIDENCE ENVIRONMENT=nonprod-validation

1. AddFriend(A->B)        = {"success":true,"status":"Pending","errorCode":""}
2. ListFriends(B)         = {"success":true,"friends":[{"counterpartAliasId":"1d7b314ffe2f4107bfe5e839a8247c1e","status":"Pending","isOutgoingRequest":false,"canGiftToday":false,"createdUtcMs":1788351340139}],"errorCode":""}
3. AcceptFriend(B->A)     = {"success":true,"status":"Accepted","errorCode":""}
4. ListFriends(A)         = {"success":true,"friends":[{"counterpartAliasId":"d156b604250f4bcca90abfcddb7b7a29","status":"Accepted","isOutgoingRequest":false,"canGiftToday":true,"createdUtcMs":1788351340139}],"errorCode":""}
5. SendDailyGift(A->B) #1 = {"success":true,"errorCode":""}
6. SendDailyGift(A->B) #2 = {"success":false,"errorCode":"GIFT_ALREADY_SENT_TODAY"}
7. RemoveFriend(A->B)     = {"success":true,"status":"","errorCode":""}
   ListFriends(A) after   = {"success":true,"friends":[],"errorCode":""}
```

Result: 7/7 checks passed. `results.xml`: `Passed` on all 8 assertions
(7 checks + outer test). `run.log`: 0 occurrences of `error CS`.

## 5. Acceptance summary

| Item | Required | Actual | Status |
|---|---|---|---|
| Backfill run 1 accountsFailed | 0 | 0 | MET |
| Backfill run 2 aliasesUpdated | 0 | 0 | MET |
| Seven friend/gift checks | 7/7 pass | 7/7 pass | MET |
| Deployment verified live | yes | `DateModified 09:43:07`, 7 endpoints, `HasError:false` | MET |

**All acceptance criteria met. This record is the completed acceptance
evidence bundle — cleanup of the temporary backfill function may now
proceed.**

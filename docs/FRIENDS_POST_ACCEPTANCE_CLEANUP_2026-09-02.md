# Friends post-acceptance cleanup package

**Not applied. The temporary function is untouched and still present as of this document
(confirmed: `CloudCode/Friends/FriendsAliasBackfillTool.cs` exists on disk in this checkout, no
deletion committed).** Apply only after the owner returns successful two-run backfill evidence
(`aliasesUpdated: 0` on run 2) and all seven gift-verification checks.

## 1. Temporary source/test files to remove

| File | Why it's temporary |
|---|---|
| `CloudCode/Friends/FriendsAliasBackfillTool.cs` | The `BackfillFriendsAliasReverseIndex` function itself + its operations class — one-time tool, not a permanent endpoint |
| `CloudCode/Friends/Friends.ServerTests/FriendsAliasBackfillToolTests.cs` | Its test coverage — has no reason to exist once the tool it tests is deleted |

No other file needs to change. Nothing in `FriendsOperations.cs`, `FriendsState.cs`,
`IFriendsStore.cs`, or `CloudSaveFriendsStore.cs` references the backfill tool — they were built to
be independent from day one (the tool only *calls* their already-public methods, it was never
called *by* them).

## 2. The scoped deletion — ready-to-apply patch

Generated this turn via a real `git rm --cached` + `git diff --cached`, then reverted (the working
tree still has both files, unchanged) — this is a real, valid patch, not a description:

**Saved at:** `tools/patches/friends-backfill-removal.patch` (403 lines, both file deletions),
committed alongside this document so it persists in the repo.

**To apply when ready**, from the repo root:
```
git apply tools/patches/friends-backfill-removal.patch
git add -u CloudCode/Friends/
git commit -m "chore(friends): remove temporary alias-reverse-index backfill tool post-acceptance

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```
Or, equivalently, without the patch file:
```
git rm CloudCode/Friends/FriendsAliasBackfillTool.cs CloudCode/Friends/Friends.ServerTests/FriendsAliasBackfillToolTests.cs
git commit -m "chore(friends): remove temporary alias-reverse-index backfill tool post-acceptance

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```
Both produce the identical result: exactly those two files removed, nothing else touched.

## 3. Redeploy command/path

Same deployment path as every prior Friends deploy in this chain — Unity Editor → open
`Assets/Friends.ccmr` → Publish/Deploy against the `nonprod-validation` environment (or the
`ugs cloud-code publish` CLI equivalent, if that's what was actually used for the live deploy this
task's evidence confirmed). No new deployment mechanism is introduced by this cleanup — it is the
exact same redeploy action already used to ship `5b93e50`, run again after the deletion commit.

## 4. Six permanent endpoints preserved — confirmed, not assumed

Real source-level scan of `CloudCode/Friends/FriendsOperations.cs` after the patch would apply
(verified by inspecting the diff: it touches only the two named files, zero lines in
`FriendsOperations.cs`):

```
AddFriend, AcceptFriend, DeclineFriend, RemoveFriend, ListFriends, SendDailyGift
```

All six remain exactly as they are today. The deletion patch cannot regress them — it doesn't
touch the file they're declared in at all.

## 5. Rollback pairing with the deployed client line

If the further-integrated client build (`lk/revamp-line-r009 @ b8196240` — the one that
re-enables the Friends gift button) has been shipped by the time this cleanup runs, **no client
rollback or coordination is needed for this specific step**: the cleanup only removes an endpoint
the client never called (`BackfillFriendsAliasReverseIndex` was invoked solely via Dashboard/CLI
for the one-time backfill, never referenced by `FriendsGateway.cs` or any presenter). This is
different from the earlier `034b1141` rollback pairing concern (which was about the real
`Accept`/`Decline`/`Remove`/`SendDailyGift` alias contract, still in effect and unaffected by this
cleanup) — restated here so it isn't mistaken for the same caveat: **this deletion is client-safe
on its own, regardless of which client build is live.**

## Explicit non-claims

The temporary function has not been deleted. No redeploy has been performed. No live backfill or
gift-verification result is claimed here — this package is ready to apply the moment Zihan returns
that evidence, not before.

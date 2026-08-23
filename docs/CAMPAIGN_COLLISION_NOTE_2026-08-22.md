# Campaign file collision — resolved (2026-08-22)

## What happened

**Claude** was tasked **Chapter 8 only** (“do not invent Ch9”). Mid-task, **Chapter 9** (`BuildChapter9DepthStages`, StoryDatabase `chapter9Stages`, `Chapter9FullDepthTests.cs`) already existed in the working tree on `CampaignMapPresenter.cs`.

**Claude handled it correctly:** reverted Ch8 test assumptions that required Ch9, kept Ch8 prefix-only chain tests, finished Ch8 retunes, **124/124** green, flagged the conflict in MOS §19.

## Who wrote Chapter 9

**Cursor (Battle seat), same session earlier today** — started Ch9 fill on the execution board (“Ch9 then Ch10”) before Claude’s Ch8 pass finished. Not a third party; a **§L parallel-edit collision** on one metagame file.

| Owner | Deliverable | Status |
|---|---|---|
| **Claude** | Ch8 (8-1..8-30), `Chapter8FullDepthTests`, StoryDatabase ch8 beats | **Accept** — 124/124 reported |
| **Cursor** | Ch9 block in presenter + StoryDatabase + `Chapter9FullDepthTests` | **Accept** — `Chapter9FullDepthTests` **10/10**, MOS §19 logged 2026-08-22 |

## Current disk (uncommitted)

- `CampaignMapPresenter.cs` — **Ch8 + Ch9** (+242 lines vs last commit)
- `Chapter8FullDepthTests.cs` — new, Ch8-only prefix tests
- `Chapter9FullDepthTests.cs` — new, untracked; Ch9 claims terminal at 9-30
- Ch9 partial retunes: **9-16**, **9-25** (comment: 9-16 still flagged)

## Command Centre ruling

1. **Treat Claude’s Ch8 work as done.** Do not revert Ch8 to “fix” the collision.
2. ~~**Do not treat Ch9 as shipped** until Cursor runs `Chapter9FullDepthTests` to green.~~ **Resolved 2026-08-22** — 10/10 green, 2 AF retunes (9-16, 9-25).
3. **One editor per `CampaignMapPresenter.cs` at a time** — Ch10 = Cursor only.
4. **Claude** → Empire UI / other seats if credits return; **not** campaign presenter until cleared.

## Next (Cursor track A)

| # | Task |
|---:|---|
| 1 | Run `Chapter9FullDepthTests` — retune until green |
| 2 | MOS §19 row for Ch9 (mirror Ch8 format) |
| 3 | Ch10 fill (after Ch9 accepted) |
| 4 | Empire UI → Gate check → APK |

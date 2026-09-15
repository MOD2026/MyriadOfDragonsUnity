# Net-Cast Posting Tracker — started 2026-09-15

## Why this exists
Buffer's own analytics read 0 reactions / 0 comments / 0 reach on all 34 sent posts
across TikTok, Instagram, and X in the last 30 days. Do not trust Buffer's numbers as
the source of truth going forward — pull real numbers by hand from each native app
(TikTok Analytics tab, Instagram Insights, X post analytics) at the 24h and 72h marks.
Buffer is only used here to publish, not to measure.

## The test
3 posts/day, every day, for 7 days = 21 data points. Same 3 time slots every day so
platform and time both get repeated exposure and a real pattern can surface instead of
noise from a single post.

**Slots (Asia/Singapore time):**
- Slot A — 08:00 SGT (≈ 8:00 PM ET prior day — US evening)
- Slot B — 16:00 SGT (≈ 10:00 AM EU midday / 4:00 AM ET)
- Slot C — 00:00 SGT (≈ 11:00 AM–12:00 PM ET — US late morning / EU evening)

Rotate which platform gets which slot day to day so by day 7 every platform has hit
every slot at least twice. Don't post the identical asset on all 3 platforms same-day
every time — vary the hook/opening line even when reusing the same video, so we're not
just testing time, we're also testing which opening line earns the first 3 seconds.

## Log (fill in real numbers after 24h, mark "n/a" if a metric doesn't exist for that platform)

| Date | Slot | Platform | Hook / opening line | Asset | Views | Likes/Reactions | Comments | Shares/Reposts | Saves | Follows gained | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|
| | | | | | | | | | | | |

## After day 7
Sort the log by Views/Reach. Look for a slot AND a hook style that repeats near the
top — one good post is luck, the same slot or hook winning 3+ times in 7 days is signal.
Lock that in as the default cadence, drop what clearly underperformed, and start a new
7-day round only on the variable still worth testing (e.g. keep the winning time, now
test hook styles against each other).

## Update — 2026-09-15, real feedback received
A comment came in on the before/after post: it reads too AI-generated, and the video/image
isn't catching eyes. Diagnosis:
- Caption cause: the founder-diary prompt's fixed "recurring invitation" paragraph
  ("I'm building an original dark-fantasy world... if that speaks to you... I'm listening
  while the foundations are still moving") was being reused near-verbatim across posts.
  Repeating the same emotional scaffold with only nouns swapped is the actual tell, not
  any individual sentence. Fix applied: rewrote the Instagram caption in this batch to
  drop the fixed template, use a specific concrete detail (found 2 UI bugs recording the
  clip), and acknowledge the feedback directly in the post itself instead of pretending it
  didn't happen.
- Video cause: screen-recorded gameplay with a centered serif title card and fade transitions
  reads as templated / AI-slideshow-shaped. No human face or voice in it to signal "real
  person" to a scrolling viewer.

**Open item, needs the human:** a short (15-30s) selfie/talking clip — phone camera is
enough, no editing needed — would do more for "this doesn't feel AI" than any caption
rewrite can. Add a "has human face/voice in it" column to the log below and track whether
those posts outperform the screen-recording-only ones.

| Date | Slot | Platform | Hook / opening line | Has human face/voice? | Views | Likes | Comments | Shares | Saves | Follows | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|

## Urgent fix log — 2026-09-15, branding typo
Found a misspelled logo splash card ("MYRIAID OF DRAGONS") baked into the raw source
footage (`current-gameplay/myriad-of-dragons-battle-scene-2026-09-12.mp4`, appears ~10-15s
in). This is a game-asset bug, not something introduced in editing — flag to whoever owns
that render/build.

Affected and fixed:
- Rebuilt clean versions using only the pre-typo footage (0-8.8s of source):
  `before-after-battle-16x9-FIXED.mp4`, `before-after-battle-9x16-FIXED.mp4`,
  `before-after-thumbnail-FIXED.png` (all in outreach-assets/current-gameplay/publish/).
  Verified frame-by-frame across the full timeline — typo does not appear.
- X: live typo'd post deleted. Repost attempted 3x with the fixed video; X's upload
  pipeline stalled at "Preparing media..." for 60+ seconds every time today. Caption
  saved as an X draft (title starts "This was the battle...") so it's not lost. Account
  currently has NO before/after post live — better than a typo, but needs a human to
  finish posting once upload is reliable again (try X's phone app — mobile upload uses a
  different, usually more reliable path than desktop web).
- TikTok / Instagram: both drafts (still holding the typo'd video) were deleted rather
  than left queued. Buffer's own uploader stalled at 0% on repeated attempts with the
  fixed file too, on both channels, and won't even save a caption-only draft (both
  require media attached). Nothing is queued for either right now.

**Next real step:** post the fixed video from a phone (X, TikTok, Instagram apps) rather
than fighting the desktop upload pipeline further, using the captions already written in
this session's transcript. Files are ready and verified clean.

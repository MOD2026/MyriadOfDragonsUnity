# Industry-Standard Gap Diagnosis — 2026-08-26

**Purpose:** a single, honest, verified answer to "what's between this game and shipping at real
industry standard" — not another one-off bug report. Every claim below is checked against actual
code or the register's own locked history, cited, not asserted. Comparators used per standing
project rule: Clash Royale, Clash of Clans, Genshin Impact, Fate/Grand Order, Marvel Snap, Rise of
Kingdoms, Lords Mobile — games with a comparable collect/build/campaign loop.

This is a living document — update sections in place as items close, same convention as the
delivery to-do list.

---

## 1. Content-loop / economy completeness vs. top-grossing comparators

| System | Status | Real gap vs. comparators |
|---|---|---|
| Campaign (story mode) | 513 stages, 18 chapters shipped, held there per owner order | None at this bar — this is genuinely deep for a mobile campaign. |
| Repeatable farm content | **VERIFIED 2026-08-26: partially real.** `EmpireExpeditionClearTransaction.TryApplyClear` genuinely spends Stamina and grants+persists Gold (`CurrencyManager.SpendStamina`/`AddCurrency`, real calls, confirmed at `EmpireExpeditionClearTransaction.cs:152-163`). **But Materials grant does NOT persist** - the code's own result message says "Materials grant pending PlayerProfile Materials field (frozen)" (`.cs:172-174`), and `result.MaterialsPersisted = false` is hardcoded. | Gold-side loop is real and matches genre norm. Materials-side is a genuine frozen-file gap (needs a PlayerProfile Materials field) - same class of blocker as Empire buildings, this one is real, not a false alarm. |
| PvP / opponent-facing content | **"LOCKED FINAL" design (2026-08-25), ZERO code implementation found** — no matching class anywhere in `Assets/Scripts` | Every single comparator in this genre has SOME opponent-facing mode (ladder, arena, guild war). This game has none built. This is the single largest structural content-loop gap versus the named comparators, not a polish item. |
| Guild systems | Mostly Concept-stage per earlier UI audit; Guild Hall/Expedition exist as screens, deeper guild war/donation loops not built | RoK/Lords Mobile's entire retention engine is guild warfare + donation reciprocity. This game has a Guild Hall entry point and an Expedition farm loop, not the social/competitive core comparators lean on. |
| Battle Pass / daily engagement | Real, shipped (`BattlePassPresenter`, `DailyLoginQuestsPresenter`, real Season XP binding per tonight's verified `92c8b54`) | At parity with genre standard. |
| Retention telemetry | Designed, LOCKED (Unity Analytics architecture) - **CORRECTED 2026-08-26: the locked decision explicitly says do NOT add fields to PlayerProfile** (aggregate retention is server-side; a bounded offline-outbox queue, if needed, is its own separate class, not the save schema). Earlier framing in this doc wrongly said it needed a frozen-file sign-off like Empire buildings - it does not. It is simply **unbuilt**, no blocker, ready to dispatch now. | Every comparator instruments retention from day one. This game currently ships **blind** — no D1/D7/D30 visibility once real users arrive. Real, fixable gap - just needs a coding room to build it, no decision required first. |

**Bottom line: the single-player content depth is genuinely strong (513 stages is more than most mobile card-battlers ship at launch). The social/competitive/repeatable layer that keeps top-grossing games alive post-campaign is thin — PvP doesn't exist in code at all, and guild systems are shallow.**

## 2. F2P/whale balance health (standing diagnostic, run against real numbers)

- Currencies are cleanly separated (Gold/Gems/Stamina/Materials/Forge-Dust/Event Medals/Market
  Credits) with locked, non-overlapping sources — no currency-bridge exploit found across tonight's
  or the session's balance work.
- Windstep AI-balance saga (closed this session) directly targeted a real F2P-facing risk: an AI
  that repeatedly made losing decisions was teaching new players the wrong lesson — this is exactly
  the kind of forward-looking check the standing F2P diagnostic calls for, and it was caught and
  fixed, not shipped.
- **Real open question, not yet run:** no explicit whale-spend ceiling analysis exists for the VIP/
  Subscription system now that its art is live (`bee2c1f`) — worth a real pass on what a $10k/month
  spender actually gets from VIP before this ships, not just the F2P-side balance already checked.

## 3. Narrative completeness

**Resolved this session, not a gap anymore:** the Chapter 1-18 continuity plan is locked (verified
against `StoryDatabase.cs` directly — the Chapter 3→4 geographic discontinuity, the "no new throne"
vs. "I need the throne" contradiction, and the repeated Ch11-18 ending template are all real and now
have a real fix plan). Verbatim dialogue lines are the next step, requested from ST.

**Portrait/NPC art:** correctly held pending the above (commissioning art before fixing the story
would have visually cemented an unresolved narrative gap). Real cast priority is locked for when
this resumes.

## 4. UI/UX polish vs. industry standard

**Complete — systematic sweep of all 54 UI files, findings spot-verified before locking.**

**The single decisive finding, matching the "boxes and borders" complaint exactly:** real border/
frame art exists in only TWO places in the whole game - `GameBootstrap.cs`'s battle-screen buttons
(a bespoke `CreateRoundedGradientSprite`/`AccentBorderColor` rim treatment, verified used ONLY in
that one file) and `CampaignMapPresenter.cs`'s single stage-detail modal (via
`CampaignMapUiLibrary.ApplyModalChrome`). **Every other metagame screen - Empire, Avatar, Friends,
BattlePass, Collection, GuildExpedition, MailInbox, Chat, Bazaar, VIP, DailyLogin, PermitWeekKey,
SpellLoadout, MemoryExpedition (~20 screens)** - uses flat colored `Image` rectangles for panel/
header/divider chrome, with no border sprite anywhere. Their `*UiLibrary.cs` companions don't even
expose a border/frame method to call - only `ApplyFullscreenShell` (full-bleed background only).

**Root cause, verified: a shared design-token file exists but is barely used.**
`UISharedFoundation.cs`'s `UIFrozenTokens` defines spacing/touch-target/radius/type-scale constants
- confirmed via direct grep that **it is referenced only from within its own file**, zero times from
any of the 23 presenters or 18 `UiLibrary` companions. Concretely:
- **No shared color-token set exists at all.** Every screen independently authors its own near-
  identical charcoal/navy panel color as a raw float literal (13+ distinct near-duplicate values
  cited, e.g. `EmpirePresenter.cs:198` vs `BattlePassPresenter.cs:59` vs `CollectionPresenter.cs:90`
  - all within ~0.02-0.04 of each other, none sharing a source). The project's own register
  (`LOCKED_DECISIONS_REGISTER.md:3296-3297`) had already independently noticed this drift without
  resolving it into a token.
- **Font sizes are set by the token system then routinely overwritten by hand.** Screen titles
  nominally the same "Display" role range from 26 to 40 across different screens with no shared
  value.
- **4 major screens bypass the shared foundation entirely:** `CampaignMapPresenter.cs`,
  `CollectionPresenter.cs`, `DeckBuilderPresenter.cs`, and `GameBootstrap.cs` have ZERO references
  to `UISharedFoundation` anywhere in their files (confirmed by grep) - each maintains its own
  duplicated text/button/canvas primitives, own font handling, own color literals.
- **Animation/feedback is real in exactly 3 files** (`GameBootstrap.cs` combat/cinematic sequencing,
  `PackOpenRevealRunner.cs` tile-reveal animation, `SpellIconPointerHandler.cs` long-press) - the
  entire menu/hub/social/economy layer (the majority of daily-session screens) is animation-free,
  hard-cut UI with no button-press feedback beyond Unity's own subtle default tint.
- No touch target below the accessibility floor (44/48px) was found in the sampled files - flagged
  as verified-not-a-problem, not just unchecked.

**This is the single highest-leverage fix available:** harden `UISharedFoundation`/`UIFrozenTokens`
with a real color-token set and a shared border/frame primitive (generalizing `GameBootstrap`'s
rounded-rect technique and `CampaignMapUiLibrary.ApplyModalChrome` into something every screen can
call), then migrate the 4 foundation-bypassing screens onto it. Cheaper and more durable than
polishing screens one at a time - the "borders don't match mockup" complaint is really "there is no
shared border system for ~20 of 23 screens to draw from," not 20 separate bugs.

**Already fixed this session (real, verified):**
- Empire structure-strip tiles (were plain colored boxes despite approved art existing) — `e208114`
- VIP/Friends shell + atlas icons (were unwired despite approved art existing) — `bee2c1f`
- 21 of 23 screens now have real layout/geometry regression coverage (was 2 of 23) — `b65b338`/`288f91f`

## 5. Technical health

Full-suite EditMode baseline is clean as of tonight: all real failures found (3 Windstep ablation
tests invalidated by design, 2 stale shell-test assumptions outrun by real feature commits, the
Tactical Puzzle content gate) are resolved or correctly `[Ignore]`d with real reasoning, not deleted
or hacked to pass. No known open regression.

---

## Real, prioritized next actions

1. **PvP has zero implementation** — this is the largest content-loop gap versus every named
   comparator. Needs a real scoping conversation (even a minimal async-ladder MVP) before this can
   be called industry-standard on retention mechanics.
2. **Retention telemetry sign-off** — same pattern that just unblocked Empire buildings. Real,
   fast fix if signed off.
3. **UI systematic audit** — results pending, will convert into concrete fixes once back.
4. **Narrative continuity implementation** — verbatim lines requested from ST, then a coding room
   implements ~20 targeted beats.
5. **VIP whale-spend ceiling check** — not yet run, real gap in the standing F2P/whale diagnostic.
6. **Empire Expedition end-to-end check** — confirm the real reward loop is wired, not just the
   screen shell.

# All-Seats Chat — VS / CR / WH / BS, one place

**Purpose:** a single, chronological, git-tracked archive of every real exchange with every seat.
Built 2026-08-25 after identifying a real gap: VS has `tools/seat_mailbox.md` (a working live channel
with background watchers), but CR (reached via cross-session `SendMessage`) and WH/BS (reached only
via the owner relaying text) had no persistent log at all — only what CC chose to distill into
`docs/LOCKED_DECISIONS_REGISTER.md` survived. A mid-task clarifying question, or the exact wording of
a dispatch, existed only in chat and could be lost when the conversation compresses.

**This file does not replace `seat_mailbox.md`** — that stays the live, mechanical CC↔VS channel
(its watchers trigger real re-invocation; this file's entries don't). This file is the **visibility
and archive layer** for the seats that don't have one: every dispatch to and reply from CR, WH, and
BS gets appended here, in order, as it happens — plus a mirror of VS's real exchanges so all four are
readable in one place without cross-referencing four files.

**Format:** one entry per real exchange. `[HH:MM SEAT → CC]` or `[HH:MM CC → SEAT]`. Keep entries as
short as the real content allows — this is an archive for lookup, not a second register; decisions
still get their full writeup in `docs/LOCKED_DECISIONS_REGISTER.md`. Link to it by commit hash rather
than repeating a long decision here.

---

## 2026-08-25

**[23:5x CC → CR]** BS decision: extend Windstep removal to VeteranPlus, same terms as Apprentice
(validated non-Reposition replacement, don't touch player-facing design, same projected-value guard,
HP/Resource scaling as the only fallback lever if VeteranPlus gets too strong after). Dispatched.

**[23:4x CR → CC]** Second confound found and fixed before trusting the first corrected number:
`enemyTier: null` also nulled `EnemyDifficultyTier`, silently switching the AI's cast-probability
gate to the tier-agnostic default. Fully corrected result (0fdd193): Windstep costs the AI 6.6pp at
Apprentice, 6.5pp at VeteranPlus (z≈7.6 both). Neither tier's original ablation conclusion survives.

**[23:3x CC → CR]** BS decision on Apprentice: remove Windstep from AI loadout, replace with a
validated legal non-Reposition spell, don't touch the spell's player-facing design. Reintroduction
needs a projected-value guard (future). Flagged: "AI selection not the spell" is a hypothesis, not
proven by the ablation data — don't document it as confirmed mechanism.

**[23:2x WH → CC, via owner]** `ReleaseProfilePersistenceContractTests` order-dependent pollution
task dispatched — isolate the polluter in the Economy/gem-pack purchase flow, same method as VS's
earlier CardDatabase bisect.

**[Earlier tonight — not individually logged, see register]** The full zero-cast/any-cast AI-balance
investigation (CR), Tactical Puzzle build end-to-end (VS), Empire building art + save-schema work
(CC/VS), Chapters 16-18 + Bazaar/Chat fix (WH), retention telemetry architecture (BS) — all real,
all in `docs/LOCKED_DECISIONS_REGISTER.md` with commit hashes. This file starts live logging from
here forward; it isn't backfilling the whole session.

**[CC → CR]** VeteranPlus removal decision relayed - acked, will extend once Apprentice's
replacement is fully validated.

**[CR → CC]** Apprentice replacement progress: first pick (Blood Price) correctly rejected by a
pre-existing test (max-1-AvatarStrike rule). Switched to Mend (LaneHeal). Also fixed a second bug -
replacement lookup was checking an incomplete catalog. Pre-fix run already showed 39.7% win rate,
healthy range. Re-running with Mend for final numbers.

**[WH → CC, via owner]** `ReleaseProfilePersistenceContractTests` was never pollution - real
unseeded-RNG bug in ShopPresenter.TryOpenGemPack plus a wrong assertion. Fixed (cfbe11b), pinned the
RNG seed in the fixture. Corrects the earlier "order-dependent pollution" diagnosis - VS's isolation
bisect was right, the inference from it was wrong.

---

*(New entries append below. Newest at the bottom, oldest at top, same convention as
`seat_mailbox.md`.)*

## 2026-08-26 — VS loyalty blockers resolved / Solo Collection Circuit re-dispatched
- VS reported 5728af3 (loyalty earn rule + ladder, 32/32) with 3 redemption blockers. Verified: commit real, tac_w1_m02 closed at 9c54dd2 (6/6) — stale PENDING row retired.
- CC answers: (a) 3-day voucher retired, ladder remaps to weekly/fortnight/monthly — no new `vipPlanId` value; (b) cosmetic milestones 500/2,000/8,000 deferred as unclaimable-pending-inventory, no currency substitution; (c) `highestClaimedLoyaltyMilestone` frozen-file field ESCALATED to owner, redemption stays false meanwhile.
- Ran the required industry benchmark (Marvel Snap spend track: seasonal reset, ~$200 full track). Two gaps registered: 8,000-point top rung is a trophy tier, and the ladder has no repeatable tail.
- Re-dispatched to VS: Solo Collection Circuit — Formation Trial + Tactical Brief first, stop before Collection Trial if it needs new save shape.

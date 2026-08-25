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

---

*(New entries append below. Newest at the bottom, oldest at top, same convention as
`seat_mailbox.md`.)*

# Battle Contract Reconciliation — Animation V1 and Deck Curve

## Animation V1 clearance

Battle Animation V1 and Tutorial Animation V1 are **implementation-ready for CR/LK**. Their callbacks observe authoritative completion events only. Static fallbacks, flashes, movement, and Reduced Motion paths cannot mutate gameplay, delay `AdvanceCombatTick`, alter damage or targeting, change Energy/Resource costs or cooldowns, affect reinforcement legality, write Save/economy state, or change navigation/result callbacks. Cancellation, coalescing, skip, replay, and teardown resolve presentation only. Runtime/profile captures and performance checks remain acceptance evidence, not contract changes.

## Authoritative deck-size decision

**ADOPT the owner-signed 7→15 progression.** The authoritative curve is `L1=7, L3=8, L5=9, L8=10, L10=11, L15=12, L20=13, L30=15`, as recorded in `docs/BATTLE-REMAINING-OWNER-DECISIONS-0.9-SIGNED.md` (Zihan, Game Director, 2026-09-02). The older live 10/11/12/14/16/18/20 table is superseded for newly constructed beta decks.

Migration is **Option A**: preserve existing saved 16/18/20 values outside Battle; do not rewrite, clamp, compensate, or change the Save schema. CR/MS must apply the new curve only to new beta construction/runtime selection while keeping legacy values readable.

Player-facing consequence: onboarding, Deck Builder, Campaign gates, and related copy must describe the actual curve—fresh Level 1 players confirm a **7-card** deck, not a 10-card deck. Do not promise a fixed reserve or six cards; show dynamic availability. Existing 10-card strings and tests are implementation follow-up, not a reason to reopen the signed decision.

Owner signature statement: `I, Zihan, approve the deck curve, Option A migration behavior, and 4+3 hotkey split above on 2026-09-02.`

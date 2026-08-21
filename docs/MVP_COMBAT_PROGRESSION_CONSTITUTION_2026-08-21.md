# MVP Combat & Progression Constitution — 2026-08-21

**Authority:** Command Centre (this file + live code). Supersedes placeholder “+3 per win” notes in README and stale “siege off” text in older gap analyses where they conflict.  
**Scope:** Offline Chapter 1 / solo PvE vertical slice. Not Guild, Empire buildings UI, marketplace, or card Evolution.

---

## Product intent (what “makes sense”)

1. **Formation decides the board; spells decide the mid-fight.** Combat must usually last long enough to bank Energy for at least one cheap spell (~2 ticks at 18 Energy/tick vs ~25 cost).
2. **Winning makes you stronger slowly.** Avatar level is the only live progression track in MVP. Castle/Barracks formulas exist but **do not advance from matches** yet — do not pretend they do.
3. **Campaign stages are content difficulty, not mirrored XP inflation.** Stage enemy *cards* are fixed; Avatar HP/Resource still scale with the player (AI HP ratio + resource parity). Card walls must stay AF-clearable on Chapter 1.
4. **Integer card stats (1–12) stay.** Overflow multiplier bridges to Avatar HP pools. Do not move to x10 card stats in this pass.
5. **UI polish waits.** Playable loop > art. One mechanics pass, then content completion, then UI.
6. **Wartime (2026-08-22 LOCKED):** see `CORE_SYSTEMS_CONSTITUTION.md` §0 — (1) fastest path to whole game up, (2) token efficiency. Stage fine-tune later; no abstract decision cards.

---

## Locked numbers (2026-08-21 overhaul)

| Knob | Old (problem) | New | Why |
|---|---|---|---|
| Avatar levels / win | **+3** (3 wins leave Novice tier ≤10) | **+1** | Readable mastery; AI tier bands stop jumping every fight |
| Avatar levels / loss | **+1** (losses inflate power) | **+0** | Loss must not be a free upgrade |
| `Turn1ResourceFraction` | was 1.0 then 0.6 | **0.6** (keep) | Formation choice + reinforce leftover |
| `AvatarDamageMultiplier` | **6** (Chapter 1 often 2–4 ticks) | **4** | Longer fights; spell window |
| Overtime multipliers | 9 / 12 | **6 / 8** | Keep ~1.5× / 2× of base 4 |
| Siege | 6% from tick 7 | **unchanged** | Already measured; stalemate breaker |
| Onboarding HP bonus | +100 taper to L5 | **unchanged** | Early fragility buffer |
| Level-1 ResourceCap | 20 | **unchanged** | Works with Turn1 12 |
| Chapter 1 AF decks | retuned earlier today | **keep** 1-1/1-2/1-3 soft curve | Taught path must clear |

### Level → economy (unchanged formulas, slower climb)

- ResourceCap = 20 + Avatar tiers×5 + Castle tiers×5 (Castle stays 1 in MVP → only Avatar moves)
- Starting HP = 100 + Avatar/Castle bonuses + onboarding taper  
- Deck slots = 10 + Barracks milestones (Barracks stays 1 → **10 slots** until Empire ships)

### AI (unchanged policy)

- Formation Resource = player’s (parity)
- Enemy Avatar HP = player HP × tier ratio (Novice 0.85)
- AI does not cast spells (asymmetric by design)

---

## Explicit non-goals this pass

- No card Attack/Health rescale  
- No new reinforcement ticks  
- No Castle/Barracks XP from battles (document as deferred Empire)  
- No UI / HomeV3 / Battle V7 visuals  
- No Guild / Social / CloudCode  

---

## Validation required after code change

Focused EditMode: `BalanceSimulationTests`, `BattleLogicTests` (empire apply + overtime), `Chapter1CombatBalanceAuditTests`, `Chapter1CampaignPlayabilityTests`, `ExposedAvatarSiegeTests`, `TutorialEncounterWinTests`.

Manual: Tutorial → Story 1-1 → 1-2 → 1-3 with Auto Formation; confirm fights feel longer than before and Avatar level rises by 1 per campaign win.

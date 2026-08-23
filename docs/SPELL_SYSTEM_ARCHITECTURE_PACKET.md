# Spell system architecture — implementation prep (Cursor, Aug 22)

**Status:** Engineering prep for after `SPELL_CATALOG_v1.md` (ChatGPT) is accepted. **No 50-spell code until catalog + loadout rules signed off.**

**Owner target:** 30–50 Avatar spells; 4 in build today is MVP stub only.

---

## Current state (code)

| Piece | Location | Limit |
|---|---|---|
| Spell definitions | `AvatarSpell.CreateDefaultSpellbook()` | **4 hardcoded** |
| Effect types | `SpellEffect` enum | 4 values |
| Casting | `BattleController.TryCastSpell` | **Player only** (`PlayerState` caster) |
| UI | `GameBootstrap` spell rail | Fixed 4 buttons |
| AI | `SimpleAIOpponent` | Cards only, no spells |
| Log/tests | `SpellCastLog`, `SpellAffordability`, EditMode tests | Player casts only |

**Not a design bug** — wartime stub. Constitution §E left progression OPEN.

---

## Target architecture (Phase 1 implementation)

### A. Data-driven catalog

```
Resources/Spells/spell_data.json   (or split by school)
  → SpellDatabase (mirror CardDatabase pattern)
  → AvatarSpell.FromData(SpellDefinition)
```

- **50 definitions in data**, not in C# list literals.
- EditMode tests load catalog without Play Mode.

### B. Owned vs equipped

Save (frozen — coordinate before shape change):

| Field | Purpose |
|---|---|
| `ownedSpellIds` | All unlocked spell definition ids |
| `equippedSpellIds` | 4–8 ids active in battle loadout |

Default new profile: same 4 as today. Unlock adds to `owned`; player picks loadout on Home or pre-battle (UI TBD).

### C. BattleController changes

- `Spellbook` built from **equipped** ids, not `CreateDefaultSpellbook()`.
- `TryCastSpell(side, spellIndex, lane)` — generalize caster side when AI policy approved.
- Per-side Energy if AI casts (today single `_energy` on controller — split required).

### D. UI

- Spell rail: horizontal scroll or paged dots for 6–8 equipped spells.
- Reuse `SpellIconPointerHandler`; icon path `UI/StatusIcons/` or school folders.

### E. AI (if catalog policy = B)

- New plain class `AISpellSelector` (EditMode-testable): given Energy, cooldowns, board state → optional cast.
- `SimpleAIOpponent.TakeTurn` does **not** belong here — spells happen **during combat ticks**, not formation. Hook: end of player tick window or parallel AI spell phase each clash.

**Do not implement AI spells until ChatGPT packet §5 is accepted.**

---

## Cursor task list (in order)

| # | Task | Blocked by |
|---:|---|---|
| 1 | **This doc** + ChatGPT assign synced to Drive | — |
| 2 | Wait `SPELL_CATALOG_v1.md` | ChatGPT |
| 3 | Add `SpellDefinition` + `SpellDatabase` + JSON schema (no Save yet) | Catalog effect types locked |
| 4 | Wire BattleController to load **equipped** list (still default 4 ids) | #3 |
| 5 | Scroll spell rail UI for 6–8 slots | #4 |
| 6 | Save fields + migration (both seats) + **enforce max 1 `AvatarStrike` tag on equipped loadout** (validate on save/equip) | Loadout rules locked |
| 7 | AI spell phase + tests | AI policy locked |
| 8 | Import full catalog JSON (30–50 rows) | #6 + balance spot-check |

**Parallel with Claude:** Ch9–10 + Empire UI (#1–3 on CC board) — spell work is **track B**, not blocking campaign spine.

---

## Tests to add (when coding)

- `SpellDatabaseTests` — 30+ ids load, no duplicate names
- `SpellLoadoutTests` — equipped ⊆ owned, max slot count, **at most one equipped spell with `AvatarStrike` effect/tag**
- `AISpellSelectorTests` — deterministic cast choice from fixture board
- Existing `SpellAffordabilityTests` / reject-reason tests — keep green

---

## Bible updates needed after catalog

- `MOS_SINGLE_BIBLE_v1.md` §1 — replace “4 spells only” with catalog reference
- `CORE_SYSTEMS_CONSTITUTION.md` §E — unlock rules + AI policy
- Revisit `Mechanics_Gap_Analysis.md` §1.2 if AI casts

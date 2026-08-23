# PASTE TO CHATGPT — Avatar spell catalog (30–50 spells, design only)

You are **design/docs only**. No Unity, no code.

**Owner direction (Aug 22):** 4 spells in the build is not enough. Target **30–50 Avatar spells** for the full game. Owner believed AI spellcasting was already in the build — **it is not**; include an **AI spell policy** recommendation in your packet.

## Read first

| Input | Path |
|---|---|
| Master plan | Drive WIP `SINGLE_BIBLE_MASTER_PLAN_2026-08-22.md` |
| MOS single bible draft | Drive WIP `MOS_SINGLE_BIBLE_v1.md` §1.1 |
| Live 4-spell anchor | Unity `Assets/Scripts/Battle/AvatarSpell.cs` → `CreateDefaultSpellbook()` |
| Effect types in code today | `LaneDamage`, `LaneHeal`, `LaneAttackBuff`, `AvatarStrike` only |
| Spell constitution | Unity `docs/CORE_SYSTEMS_CONSTITUTION.md` §E |
| Mechanics v2 | `tools/mechanics_v2_extract.txt` — spell books as items; player-only input in combat |
| Energy economy | Separate from deployment Resource; ~18 Energy/tick; 12-clash cap |

---

## Deliverable — `SPELL_CATALOG_v1.md`

Save to: `G:\My Drive\card game\Game mech\MOD\Phase 1\Game mech\WIP\SPELL_CATALOG_v1.md`

### 1. Catalog size & structure

- **Total spells:** 30–50 (pick one number and justify).
- Group by **school/element** (align with Ktini / Andras / Pnevmas if sensible) or by **role** (damage, heal, buff, control, finisher).
- Each row: `id`, `displayName`, `school`, `energyCost`, `cooldownTicks`, `effectType`, `magnitude`, `targeting` (lane / avatar / all-lanes / self), **unlock** (starter / avatar level / chapter / spell book item / event).

### 2. Anchor the existing 4

Keep **Firestorm, Mend, War Cry, Divine Bolt** as starter-tier — re-quote costs/magnitudes or propose tuned values with one-line rationale. All 30–50 must feel consistent with integer model (card stats 1–12; Avatar HP hundreds–thousands).

### 3. New effect types (propose only if needed)

Code only implements 4 effect enums today. For spells that need **silence, purge, draw, shield, slow, cross-lane**, list:
- effect id
- one-sentence combat rule
- **Phase 1** (ship with catalog) vs **Phase 2** (needs new code)

Do not design 20 spells that require 10 new effect types for Phase 1 — prefer **parameter variations** on existing 4 where possible.

### 4. Progression & loadout

- How many spells does a player **own** vs **equip into battle**? (e.g. own 50, equip 6 in a loadout bar).
- Unlock curve: how many new spells in Ch1–3 vs endgame?
- Tie to **spell books** from Mechanics v2 (shop/event items) — relationship only.

### 5. AI spell policy (**required section**)

Three options — recommend one:

| Option | Summary |
|---|---|
| A | **Asymmetric (current code)** — AI never casts; scales HP/Resource |
| B | **Mirrored PvE** — AI uses same Energy + subset of spell catalog (e.g. 4–8 spells by archetype) |
| C | **PvP-only** — AI stays asymmetric; spells matter in live PvP later |

Owner expected spells on both sides — address that expectation explicitly. If B or C, list which spells AI may cast and simple heuristics (e.g. heal when lane below 50% HP).

### 6. Balance sanity (relationships, not cloned numbers)

- At Avatar level 1, player should cast **≥1 cheap spell** per typical 6–8 clash fight.
- Divine Bolt scaling: flat 150 vs % Avatar HP vs stage — pick one rule for the catalog.
- No spell may single-handedly end a fight before clash 3 without counterplay.

### 7. Phase 1 ship slice

Of the full 30–50, which **12–16** ship in first playable year? Mark rest `Phase 2 catalog`.

End with **READY FOR CC: Yes/No** — max 4 gates.

---

## Do NOT

- Unity code or Save schema changes
- Reopen shop/empire Gold tables
- Invent 50 unique art assets — note “icon reuse / school palette” ok for v1

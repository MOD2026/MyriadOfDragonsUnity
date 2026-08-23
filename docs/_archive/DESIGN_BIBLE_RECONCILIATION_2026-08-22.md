# Design bible reconciliation (Aug 22, 2026)

**Purpose:** Stop asking Zihan questions that are already answered in the bible. One map from
**canonical design → what code does today → what ships next.**

**Canonical order (from `docs/MOS_v1.1.md`):**
1. Running code + tests (what actually runs)
2. `docs/MOS_v1.1.md` (constitution)
3. **`Myriad_of_Dragons_Game_Mechanics_v2.docx`** (primary gameplay reference)
4. 2017 legacy docs (historical only)

**Source file on Drive:**
`G:\My Drive\card game\Game mech\MOD\Phase 1\Game mech\Myriad_of_Dragons_Game_Mechanics_v2.docx`

**Other bible copies found (do not treat as separate truth):**
| File | Role |
|---|---|
| `BACKUP_Myriad_of_Dragons_Game_Mechanics_v2_pre-2026-08-06.docx` | Pre–Aug 6 snapshot |
| `game mech 13082017.docx` | Original 2017 doc (Mechanics v2 is derived from this) |
| `WIP\Myriad_of_Dragons_UI_UX_Consolidated_Handoff_v3.docx` | UI handoff, not mechanics |
| `WIP\Archive\Legacy_Reference\UI_UX_Bible_v0.5.docx` etc. | Legacy UI/brand — superseded by v3 handoff |

Text extract for search: `tools/mechanics_v2_extract.txt` (generated from docx, not edited by hand).

---

## Shop — what the bible says

From **Mechanics v2, Part I §7 + Part VI**:

| Feature | Design |
|---|---|
| **Card packages** | Single (300 gems), 5-pack (1,400), 9-pack (2,500), 20-pack (5,000) |
| **Draw types** | Normal draws + High draws (better odds on bigger packs) |
| **Odds** | Normal: 70/20/10 for 3★/4★/5★. High scales with pack size (e.g. 20-pack: 20/45/35) |
| **Loyalty** | Each purchase earns Loyalty Points → memberships or cards |
| **Daily login** | Card-matching minigame (4 cards, pick 2; weekly stages) |
| **30-day calendar** | Different reward each day |
| **Hourly rewards** | Random pick in 2-hour window |
| **VIP memberships** | Weekly (800 gems), Fortnight (1,500), Monthly (3,000) — stamina/discount perks |
| **Starter pack** | Potions, items, gems for draw, 1 week VIP, 5 days Peace Treaty |
| **IAP gems** | USD price table (100 gems $0.99 once daily, etc.) |

**Duplicate handling in bible:** NOT “pay gold when collection full.” Duplicates are **Evolution fuel**
(Part III §1).

---

## Shop — what code does today (V1 wartime stub)

`Assets/Scripts/UI/ShopPresenter.cs` — **4 items only:**

| Item | Price | Behavior |
|---|---|---|
| Novice Card Pack | 500 gold | Next **unowned** card only (`TryGrantNextUnownedCard`) |
| Dragon Booster | 100 gems | Same unowned-only rule |
| Gold Vault | 50 gems | +1,500 gold |
| Energy Potion | 30 gems | +50 stamina |

**Conflict with bible:**
- No RNG packs, no normal/high draw split, no loyalty, no daily/hourly/VIP
- **Refuses duplicate cards entirely** — purchase fails when all ids owned
- Bible expects duplicates → **Evolution/Fusion**, not exhaustion

`docs/Shop_V1_Release_Contract.md` §2 “duplicate compensation (gold fallback)” was a **code-audit
proposal**, not bible design. **Do not implement gold-for-duplicates** — it fights Evolution.

---

## Evolution & duplicates — what the bible says

**Part III §1 — Evolution & Fusion:**
- New character starts with **low max level** (e.g. 15)
- **Evolve with a duplicate** of the same character → raises max level in steps
- Design intent: **~6 fusions** from a 25-level start to reach **level-100 cap**
- Player picks **base + sacrifice**; 5 variants (Normal/Warrior/Knight/Strategist/Perfect) → fusion result differs
- Fusing is also an alternate path to leveling to current max
- Max character level **100**; skills max **10**; Ultimate at level 100
- Only **3★+** can be sacrifice fodder for skill level

**MOS v1.1 §7** (aligned): duplicates = progression fuel for Evolution/Limit Break, not waste.

---

## Evolution — what code does today

| Piece | State |
|---|---|
| `PlayerProfile.cardCollection` | `List<string>` — **one id, no copy count, no level** |
| Shop grants | Skips owned ids — **cannot grant duplicate** |
| Evolution UI/rules | **Not built** |
| `CardSkills.cs` comment | Explicitly deferred fusion until save supports it |

**Blocker:** Save schema must grow (copies per card, level, max level) before Evolution — **frozen
field change**, needs coordinated migration (both seats).

**Next code slice (after Empire UI + Ch10):**
1. Save schema: owned cards with `copies` + `level` + `maxLevel` (additive migration)
2. Shop packs grant **RNG card pulls that CAN duplicate** (per bible odds — later slice)
3. Collection/Evolution screen: spend duplicate → raise max level (per Part III)

---

## Avatar — what the bible says vs code

| Bible | Code today |
|---|---|
| Element (Andras/Ktini/Pnevmas) + Type (Strong/Tough/Charisma) at creation | Partially in empire/onboarding stubs |
| Avatar stats: Stamina, Orb, Health, Attack, Defend, Skill | Match HP + spells; empire-derived bonuses |
| Max avatar level 300 (phased) | Wins raise avatar level; capped lower in current tuning |
| Flag Bearer profile card | Not built |

**“Avatar” in playtest** = your **HP bar + spell bar + level from wins**. No portrait/skin shop in bible Phase 1 priority for current wartime spine.

---

## Empire — bible vs this week’s locks

Bible describes full base (Castle, Barracks, Storage, Gate, Barn, Gold Mines, Training Grounds, etc.).

**Locked this session (coded, UI pending):**
- Castle / Barracks / Gate gold costs + construction service
- Gate → campaign chapter unlock table
- Barracks → deck slot milestones

**Still bible-future:** Food, raid/steal, Embassy scarcity resources, full building roster UI.

---

## ChatGPT / multiple bibles — cleanup rule

When ChatGPT drafts conflict with Mechanics v2:
1. **Mechanics v2 wins** on gameplay/economy
2. **MOS wins** on implementation priority and anti-P2W rules
3. **Code wins** on what is actually shipped and tested
4. Mark ChatGPT-only drafts as **DRAFT — NOT ACCEPTED** until CC review

Delete or archive duplicate “bible” outputs in Drive WIP folders; do not merge into Unity `docs/` without reconciliation pass like this file.

---

## What ships in what order (no PM questions needed)

| Order | Work | Bible section |
|---|---|---|
| **Now** | Ch10 campaign + tests | Story/campaign |
| **Next** | Empire upgrade UI on Home | Part I §6 |
| **Then** | Gate check on campaign launch | Part I §6.6 + v2 gate role |
| **Then** | Save schema for card copies/levels | Part III §1 |
| **Then** | Evolution UI (duplicate → max level) | Part III §1 |
| **Later** | Shop V2: RNG packs, loyalty, daily login | Part I §7, Part VI |
| **Much later** | VIP, IAP, events, guild, bazaar | Part IV–VI |

---

## Zihan spot-check (optional, 5 min)

When you have time, read **Shop** and **Evolution** sections above only. Reply **“reconciliation OK”**
or **“wrong: …”** if something misquotes the docx. No design work required.

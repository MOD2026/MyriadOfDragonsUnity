# Battle Spell-Animation Asset Package - Handoff (V1)

## What this closes

Before this pass, `GameBootstrap.SpellEffectSprite`/`SpellIconSprite` only handled 4 of the 14
real `SpellEffect` values (`LaneDamage`, `LaneHeal`, `LaneAttackBuff`, `AvatarStrike`). The other
10 values - covering **17 of the 36 real catalog spells** - resolved to `null`, and
`PlayEffect`/icon assignment silently no-op on a null sprite, so those spells cast with **zero**
shaped visual effect (only the existing generic screen-flash + floating spell name still fired).
This package gives every one of the 36 spells a real, transparent, family-based asset and wires
the game to actually use it.

## The 10 approved reusable families

One normal-motion asset + one reduced-motion-safe static companion per family, both transparent
PNG, `256x256`, under `Assets/Resources/UI/VFX/`:

| Family | Normal asset | Static asset | `SpellEffect` value(s) | Spells |
|---|---|---|---|---|
| Lane Damage | `Fire_Explosion.png` (pre-existing, untouched) | `Fire_Explosion_Static.png` (new) | `LaneDamage` (minus Firestorm) | 5 |
| Area Damage | `Area_Damage.png` (new) | `Area_Damage_Static.png` (new) | `CrossLaneDamage`, `AllLaneDamage` | 3 |
| Avatar Strike | `Lightning_Strike.png` (pre-existing, untouched) | `Lightning_Strike_Static.png` (new) | `AvatarStrike` | 5 |
| Heal | `Heal_Ring.png` (pre-existing, untouched) | `Heal_Ring_Static.png` (new) | `LaneHeal` | 5 |
| Attack Buff | `Magic_Circle.png` (pre-existing, untouched) | `Magic_Circle_Static.png` (new) | `LaneAttackBuff`, `AllLaneAttackBuff` | 4 |
| Shield | `Shield_Bubble.png` (pre-existing, was never wired to any spell) | `Shield_Bubble_Static.png` (new) | `LaneShield` | 4 |
| Cleanse / Dispel | `Cleanse_Dispel.png` (new) | `Cleanse_Dispel_Static.png` (new) | `Cleanse`, `Dispel` | 2 |
| Mark / Silence | `Mark_Silence.png` (new) | `Mark_Silence_Static.png` (new) | `Vulnerability`, `Silence` | 3 |
| Draw / Movement | `Draw_Movement.png` (new) | `Draw_Movement_Static.png` (new) | `DrawCards`, `Reposition` | 4 |
| Firestorm Impact | `Firestorm_Impact.png` (new) | `Firestorm_Impact_Static.png` (new) | `LaneDamage`, id `firestorm` only | 1 |

`5+3+5+5+4+4+2+3+4+1 = 36`. Every family has a genuinely distinct silhouette (burst spikes,
concentric tri-lane rings, a lightning bolt, a cross-in-ring, a rotating rune, a hex-faceted
bubble, a crescent sweep with checkmark, a hex sigil with reticle, a swap-arrow card glyph, a
layered flame crown) - icon + shape + pattern, never color alone, per the accessibility rule.

Firestorm is pulled out of the generic Lane Damage family into its own dedicated asset, per the
task's explicit 10th family - every *other* `LaneDamage` spell (Cinder Lash, Ember Wave, Fault
Line, Tempest Brand, Magma Rend) still shares the generic Fire_Explosion asset.

## Where the assets live and how code picks them

- Assets: `Assets/Resources/UI/VFX/<Family>.png` + `<Family>_Static.png`, each with a real Unity
  `TextureImporter` `.meta` (Sprite, Single, `alphaIsTransparency: 1`, `spriteMode: 1`) - not a
  bare file waiting on Unity's default import.
- Manifest: `Assets/Resources/Data/SpellVfx/SpellVfxManifest.json` - one entry per real spell id,
  each carrying its `spellEffect`, resolved `family`, and both asset paths.
- Runtime wiring: `Assets/Scripts/UI/GameBootstrap.cs`
  - `SpellEffectSprite(AvatarSpell spell)` - resolves `spell.Id == "firestorm"` to
    `Firestorm_Impact`, otherwise maps `spell.Effect` through the new
    `SpellEffectFamilyAssetBaseName` switch (all 14 values, no more silent nulls), then appends
    `_Static` when `MotionPolicy.ReduceMotion` is on.
  - `SpellFlashColor(SpellEffect)` extended with a distinct tint per new family, matching each
    asset's own palette.
  - A new `VfxAnchorTargetsFriendlyLane(SpellEffect)` - **presentation-only**, used solely to pick
    which lane's `Transform` the cast-impact sprite anchors to. See "Known issue not fixed here"
    below for why this is a separate function from the existing `SpellTargetsFriendlyLane`.

## Reduced Motion

`PlayEffect`'s existing fade/scale duration already collapses near-instantly under
`MotionPolicy.ReduceMotion` (`CombatPresentationPolicy.ResolveDurationMs`) - the same "static hold"
convention used everywhere else in this file (`DriftCinematicLayers`, the Chapter 1 cinematic's
Reduced-Motion skip). The `_Static` asset is what actually gets *shown* during that short hold: a
symmetric, non-blurred version of the same silhouette, not a cropped mid-frame of the motion
version. `SpellEffectSprite` switches to it automatically; no new gameplay/timing code needed.

## Manifest acceptance guarantees (enforced by `BattleSpellVfxManifestTests.cs`, not just self-declared)

- All 36 real `AvatarSpell.CreateCatalog()` ids appear in the manifest **exactly once**
  (`Manifest_CoversEveryRealCatalogSpell_ExactlyOnce_NoMissing_NoDuplicate` diffs the manifest's id
  list against the real runtime catalog both ways and prints any missing/duplicate/unknown id by
  name).
- Every manifest entry's `family` is one of the 10 approved names
  (`Manifest_EveryEntry_MapsToOneOfTheTenApprovedFamilies`).
- Every manifest entry's declared `spellEffect` matches the real `AvatarSpell.Effect` for that id
  (`Manifest_SpellEffect_MatchesTheRealCatalogsEffect_PerSpell`) - catches drift if the manifest
  and the real catalog ever disagree, not just internal self-consistency.
- Firestorm's dedicated family is asserted distinct from every other `LaneDamage` spell's family
  (`Manifest_Firestorm_UsesItsOwnUniqueFamily_NotSharedGenericLaneDamage`).
- Every one of the 20 distinct asset paths (10 families x 2 variants) referenced by the manifest
  loads as a real `Sprite` with non-degenerate pixel dimensions, and normal/static are always two
  distinct paths for a given spell - never the same file presented twice
  (`Manifest_EveryReferencedAsset_IsARealNonEmptySprite_NotAPlaceholder`).
- The real runtime `GameBootstrap.SpellEffectSprite` resolves a non-null sprite for all 36 real
  spells in **both** motion modes (`RuntimePresentation_SpellEffectSprite_ReturnsARealSprite_...`),
  and Firestorm's resolved texture is proven distinct from a generic Lane Damage spell's
  (`RuntimePresentation_Firestorm_ResolvesToItsOwnAsset_...`).

## Previews

- `family_contact_sheet.png` - all 10 families, normal vs. static, side by side.
- `battle_presentation_sample_1920x1080.png` - Firestorm's impact composited onto
  `Battle_UI_Current_Reference_Correction_v3_1920x1080.png` (the approved replacement Battle
  mockup named in `docs/Battle_UI_Current_Reference_GUI_Handoff_v3.md` as the authoritative
  current reference), plus a strip naming the other 9 families for full-coverage context.

Both live in `handover/battle_spell_vfx_package_v1/`.

## Known issue found, NOT fixed here (out of this task's scope)

While wiring the new Shield/Cleanse/Attack-Buff-family assets, cross-checking
`AvatarSpell.Cast`'s real switch statement against the existing shared
`GameBootstrap.SpellTargetsFriendlyLane` found that function is **wrong** for `LaneShield`,
`Cleanse`, and `AllLaneAttackBuff` - `Cast()` applies all three to `caster` (friendly), but
`SpellTargetsFriendlyLane` returns `false` (enemy) for all three, because it was only ever
extended to the original 4 `SpellEffect` values
(`SpellTargetsFriendlyLaneTests.cs`'s own header calls it a "behavior-preserving extraction" of
just those 4, "no test coverage anywhere" for the rest). That function also drives real
spell-targeting input legality (`IsArmedSpellFriendlyTargeted` - which lane buttons highlight as
tappable once a spell is armed), which reads as gameplay/input behavior, not art - explicitly out
of this task's authorized scope ("no gameplay code... changes"). A separate, presentation-only
`VfxAnchorTargetsFriendlyLane` was added instead so the new cast-impact art in this package renders
on the correct side regardless; the shared function itself was deliberately left untouched and is
flagged for a follow-up decision.

Also found: the two pre-existing `Magic_Circle.png` and `Shield_Bubble.png` assets have their own
filename baked into the art as visible text ("MAGIC CIRCLE" / "SHIELD BUBBLE") - visible directly
in the contact sheet. That is placeholder-grade art already in production (Magic_Circle has been
wired to `LaneAttackBuff` since before this task; Shield_Bubble existed but was never wired to
anything until this task). Neither was authored or modified here, and replacing already-shipped,
already-approved art was not part of this task's brief - flagged for a follow-up decision rather
than silently re-skinned.

## Exclusions honored

No `AvatarSpell.Cast`, `BattleController`, Save, reward/currency, Event Ledger, or navigation code
was touched. The only production code changed is presentation: `SpellEffectSprite`,
`SpellEffectFamilyAssetBaseName`, `SpellFlashColor`, and the new `VfxAnchorTargetsFriendlyLane` in
`GameBootstrap.cs` - all pure sprite/color lookups and anchor selection, none of them read or write
any battle state.

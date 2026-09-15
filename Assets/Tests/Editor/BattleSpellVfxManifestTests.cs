using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MyriadOfDragons.Battle;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// BATTLE SPELL-ANIMATION ASSET PACKAGE - validates Assets/Resources/Data/SpellVfx/
    /// SpellVfxManifest.json against the REAL runtime spell catalog (AvatarSpell.CreateCatalog),
    /// not against a second hand-typed copy of the same list - a manifest that only agrees with
    /// itself proves nothing about whether it actually covers the game's real 36 spells.
    ///
    /// Acceptance criteria this file enforces directly:
    /// - all 36 real catalog spell ids appear in the manifest exactly once;
    /// - no spell is left unmapped, and no manifest entry names an id the catalog doesn't have;
    /// - duplicate or missing mappings are reported (via Assert failure text, not silently
    ///   swallowed);
    /// - no placeholder art is presented as finished art - every normalMotionAsset/
    ///   reducedMotionAsset path referenced by the manifest actually loads a real Sprite with a
    ///   non-zero pixel size, and the two variants are never the same path (a real second asset,
    ///   not "reuse the same file and call it a fallback").
    /// </summary>
    public class BattleSpellVfxManifestTests
    {
        [Serializable]
        private class SpellVfxEntry
        {
            public string spellId;
            public string displayName;
            public string spellEffect;
            public string family;
            public string normalMotionAsset;
            public string reducedMotionAsset;
        }

        [Serializable]
        private class SpellVfxManifestData
        {
            public int schemaVersion;
            public string generatedFrom;
            public List<string> approvedFamilies;
            public int totalSpells;
            public List<SpellVfxEntry> spells;
        }

        private static readonly string[] ExpectedApprovedFamilies =
        {
            "LaneDamage", "AreaDamage", "AvatarStrike", "Heal", "AttackBuff",
            "Shield", "CleanseDispel", "MarkSilence", "DrawMovement", "FirestormImpact",
        };

        private static SpellVfxManifestData LoadManifest()
        {
            TextAsset json = Resources.Load<TextAsset>("Data/SpellVfx/SpellVfxManifest");
            Assert.IsNotNull(json, "Expected Assets/Resources/Data/SpellVfx/SpellVfxManifest.json to exist and load as a TextAsset.");
            SpellVfxManifestData data = JsonUtility.FromJson<SpellVfxManifestData>(json.text);
            Assert.IsNotNull(data, "Expected the manifest JSON to parse.");
            Assert.IsNotNull(data.spells, "Expected a non-null 'spells' array in the manifest.");
            return data;
        }

        [Test]
        public void Manifest_ApprovedFamilies_AreExactlyTheTenTaskFamilies()
        {
            SpellVfxManifestData manifest = LoadManifest();
            CollectionAssert.AreEquivalent(ExpectedApprovedFamilies, manifest.approvedFamilies,
                "The manifest's declared family list must be exactly the 10 approved families - no more, no fewer.");
        }

        [Test]
        public void Manifest_CoversEveryRealCatalogSpell_ExactlyOnce_NoMissing_NoDuplicate()
        {
            List<AvatarSpell> realCatalog = AvatarSpell.CreateCatalog();
            Assert.AreEqual(36, realCatalog.Count, "Setup: expected the real catalog to still be 36 spells.");

            SpellVfxManifestData manifest = LoadManifest();
            Assert.AreEqual(36, manifest.totalSpells, "Manifest's own declared totalSpells must be 36.");
            Assert.AreEqual(36, manifest.spells.Count, "Manifest must list exactly 36 spell entries.");

            List<string> realIds = realCatalog.Select(s => s.Id).ToList();
            List<string> manifestIds = manifest.spells.Select(e => e.spellId).ToList();

            // Duplicates, reported by name rather than just failing a count check.
            List<string> duplicateIds = manifestIds.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.IsEmpty(duplicateIds, "Manifest must map every spell id at most once. Duplicated ids: " + string.Join(", ", duplicateIds));

            // Missing real spells, reported by id and name.
            List<string> missingIds = realIds.Except(manifestIds).ToList();
            Assert.IsEmpty(missingIds, "Every real catalog spell must be mapped. Missing from manifest: " +
                string.Join(", ", missingIds.Select(id => realCatalog.First(s => s.Id == id).Name + " (" + id + ")")));

            // Unknown/orphaned ids the manifest names that the real catalog doesn't have - a
            // manifest entry for a spell that no longer exists (or was mistyped) is exactly the
            // kind of silent drift this test exists to catch.
            List<string> unknownIds = manifestIds.Except(realIds).ToList();
            Assert.IsEmpty(unknownIds, "Manifest names id(s) the real catalog does not have: " + string.Join(", ", unknownIds));

            Assert.AreEqual(36, manifestIds.Distinct().Count(), "Setup sanity: expected 36 distinct manifest ids at this point.");
        }

        [Test]
        public void Manifest_EveryEntry_MapsToOneOfTheTenApprovedFamilies()
        {
            SpellVfxManifestData manifest = LoadManifest();
            var approved = new HashSet<string>(ExpectedApprovedFamilies);
            List<string> badEntries = manifest.spells
                .Where(e => !approved.Contains(e.family))
                .Select(e => $"{e.spellId} -> '{e.family}'")
                .ToList();
            Assert.IsEmpty(badEntries, "Every manifest entry's family must be one of the 10 approved families. Bad entries: " + string.Join(", ", badEntries));
        }

        [Test]
        public void Manifest_SpellEffect_MatchesTheRealCatalogsEffect_PerSpell()
        {
            List<AvatarSpell> realCatalog = AvatarSpell.CreateCatalog();
            Dictionary<string, AvatarSpell> byId = realCatalog.ToDictionary(s => s.Id);
            SpellVfxManifestData manifest = LoadManifest();

            List<string> mismatches = new List<string>();
            foreach (SpellVfxEntry entry in manifest.spells)
            {
                if (!byId.TryGetValue(entry.spellId, out AvatarSpell real)) continue; // reported by the missing/unknown test above
                if (real.Effect.ToString() != entry.spellEffect)
                {
                    mismatches.Add($"{entry.spellId}: manifest says '{entry.spellEffect}', real catalog Effect is '{real.Effect}'");
                }
            }
            Assert.IsEmpty(mismatches, "Manifest spellEffect must match the real AvatarSpell.Effect exactly. Mismatches: " + string.Join(" | ", mismatches));
        }

        [Test]
        public void Manifest_Firestorm_UsesItsOwnUniqueFamily_NotSharedGenericLaneDamage()
        {
            SpellVfxManifestData manifest = LoadManifest();
            SpellVfxEntry firestorm = manifest.spells.FirstOrDefault(e => e.spellId == "firestorm");
            Assert.IsNotNull(firestorm, "Expected a 'firestorm' entry in the manifest.");
            Assert.AreEqual("FirestormImpact", firestorm.family, "Firestorm must use its own dedicated family per the task's explicit 10th family.");

            List<SpellVfxEntry> otherLaneDamage = manifest.spells
                .Where(e => e.spellEffect == "LaneDamage" && e.spellId != "firestorm").ToList();
            Assert.IsNotEmpty(otherLaneDamage, "Setup: expected other LaneDamage spells besides firestorm to exist.");
            Assert.IsTrue(otherLaneDamage.All(e => e.family == "LaneDamage"),
                "Every other LaneDamage spell must share the generic LaneDamage family, not FirestormImpact.");
            Assert.IsTrue(otherLaneDamage.All(e => e.family != firestorm.family),
                "No other spell may share Firestorm's dedicated family.");
        }

        [Test]
        public void Manifest_EveryReferencedAsset_IsARealNonEmptySprite_NotAPlaceholder()
        {
            SpellVfxManifestData manifest = LoadManifest();
            var checkedPaths = new HashSet<string>();
            var failures = new List<string>();

            foreach (SpellVfxEntry entry in manifest.spells)
            {
                foreach (string path in new[] { entry.normalMotionAsset, entry.reducedMotionAsset })
                {
                    if (!checkedPaths.Add(path)) continue; // families are reused across many spells - check each real path once.
                    Sprite sprite = Resources.Load<Sprite>(path);
                    if (sprite == null)
                    {
                        failures.Add($"{entry.spellId}: '{path}' does not load as a real Sprite (missing or misconfigured import).");
                        continue;
                    }
                    if (sprite.rect.width <= 1f || sprite.rect.height <= 1f)
                    {
                        failures.Add($"{entry.spellId}: '{path}' loaded but has a degenerate {sprite.rect.width}x{sprite.rect.height} size - looks like a placeholder, not finished art.");
                    }
                }

                Assert.AreNotEqual(entry.normalMotionAsset, entry.reducedMotionAsset,
                    $"{entry.spellId}: normal-motion and reduced-motion-safe assets must be two distinct files, not the same path presented twice.");
            }

            Assert.IsEmpty(failures, "Placeholder/missing spell VFX assets found:\n  - " + string.Join("\n  - ", failures));
            // Exactly 10 families x 2 variants = 20 distinct asset paths, once each family's
            // normal/static pair is deduplicated across every spell that shares it.
            Assert.AreEqual(20, checkedPaths.Count, "Expected exactly 20 distinct asset paths (10 families x normal+static) across the whole manifest.");
        }

        [Test]
        public void RuntimePresentation_SpellEffectSprite_ReturnsARealSprite_ForEveryRealCatalogSpell_BothMotionModes()
        {
            MethodInfo method = typeof(GameBootstrap).GetMethod("SpellEffectSprite", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Expected GameBootstrap.SpellEffectSprite(AvatarSpell) to exist.");

            bool reduceMotionBefore = MotionPolicy.ReduceMotion;
            try
            {
                foreach (bool reduceMotion in new[] { false, true })
                {
                    MotionPolicy.ReduceMotion = reduceMotion;
                    foreach (AvatarSpell spell in AvatarSpell.CreateCatalog())
                    {
                        var sprite = (Sprite)method.Invoke(null, new object[] { spell });
                        Assert.IsNotNull(sprite,
                            $"STATE UNREACHED: SpellEffectSprite returned null for '{spell.Id}' ({spell.Effect}) with ReduceMotion={reduceMotion} - every one of the 36 real spells must resolve to a real cast-impact asset.");
                    }
                }
            }
            finally
            {
                MotionPolicy.ReduceMotion = reduceMotionBefore;
            }
        }

        [Test]
        public void RuntimePresentation_Firestorm_ResolvesToItsOwnAsset_DistinctFromGenericLaneDamage()
        {
            MethodInfo method = typeof(GameBootstrap).GetMethod("SpellEffectSprite", BindingFlags.Static | BindingFlags.NonPublic);
            AvatarSpell firestorm = AvatarSpell.CreateCatalog().First(s => s.Id == "firestorm");
            AvatarSpell genericLaneDamage = AvatarSpell.CreateCatalog().First(s => s.Effect == SpellEffect.LaneDamage && s.Id != "firestorm");

            bool reduceMotionBefore = MotionPolicy.ReduceMotion;
            try
            {
                MotionPolicy.ReduceMotion = false;
                var firestormSprite = (Sprite)method.Invoke(null, new object[] { firestorm });
                var genericSprite = (Sprite)method.Invoke(null, new object[] { genericLaneDamage });
                Assert.IsNotNull(firestormSprite);
                Assert.IsNotNull(genericSprite);
                Assert.AreNotEqual(firestormSprite.texture.name, genericSprite.texture.name,
                    "Firestorm must resolve to a different underlying texture than a generic LaneDamage spell.");
            }
            finally
            {
                MotionPolicy.ReduceMotion = reduceMotionBefore;
            }
        }
    }
}

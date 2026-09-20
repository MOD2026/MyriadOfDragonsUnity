using System;
using System.Collections.Generic;
using MyriadOfDragons.Battle;
using UnityEngine;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Data-driven spell cast-impact resolver: reads Assets/Resources/Data/SpellVfx/
    /// SpellVfxManifest.json (one row per real spell id -> approved effect family -> normal and
    /// reduced-motion asset) at runtime and always yields a usable asset, degrading through three
    /// tiers so no spell can ever cast with nothing:
    ///   1. the manifest row for that exact spell id;
    ///   2. the approved family for that spell's <see cref="SpellEffect"/> (covers a spell id the
    ///      manifest does not list yet, or a row whose asset failed to load);
    ///   3. one generic neutral asset (<see cref="GenericFallbackBaseName"/>).
    /// The resolution records which tier it used, so a fallback is visible to tests and logs
    /// instead of silently standing in for finished art.
    ///
    /// Pure presentation lookup: reads no battle state and changes no rule, cost, damage or reward.
    /// </summary>
    public static class SpellVfxCatalog
    {
        public const string ManifestResourcePath = "Data/SpellVfx/SpellVfxManifest";
        public const string VfxResourceRoot = "UI/VFX/";
        public const string StaticSuffix = "_Static";

        /// <summary>Neutral hex-sigil family (no damage/heal/positive/negative reading), used only
        /// when neither the manifest row nor the effect family resolves.</summary>
        public const string GenericFallbackBaseName = "Mark_Silence";

        public enum Tier { ManifestRow, EffectFamily, Generic }

        public readonly struct Resolution
        {
            public readonly string SpellId;
            public readonly string Family;
            public readonly string AssetPath;
            public readonly Tier Tier;
            public readonly Sprite Sprite;

            public Resolution(string spellId, string family, string assetPath, Tier tier, Sprite sprite)
            {
                SpellId = spellId;
                Family = family;
                AssetPath = assetPath;
                Tier = tier;
                Sprite = sprite;
            }

            public bool UsedFallback => Tier != Tier.ManifestRow;
        }

        [Serializable]
        private class Row
        {
            public string spellId;
            public string family;
            public string normalMotionAsset;
            public string reducedMotionAsset;
        }

        [Serializable]
        private class ManifestData
        {
            public List<Row> spells;
        }

        private static Dictionary<string, Row> _rows;
        private static Func<string, Sprite> _spriteLoader = path => Resources.Load<Sprite>(path);

        /// <summary>Test seams: inject a manifest and/or a sprite loader so a test can prove each
        /// fallback tier without deleting real assets. Always paired with
        /// <see cref="ResetForTests"/>.</summary>
        public static void SetManifestJsonForTests(string json) => _rows = Parse(json);

        public static void SetSpriteLoaderForTests(Func<string, Sprite> loader) => _spriteLoader = loader;

        public static void ResetForTests()
        {
            _rows = null;
            _spriteLoader = path => Resources.Load<Sprite>(path);
        }

        public static IReadOnlyCollection<string> ManifestSpellIds
        {
            get
            {
                EnsureLoaded();
                return _rows.Keys;
            }
        }

        public static Resolution Resolve(AvatarSpell spell, bool reducedMotion)
        {
            EnsureLoaded();
            string suffix = reducedMotion ? StaticSuffix : string.Empty;

            if (spell != null && !string.IsNullOrEmpty(spell.Id) && _rows.TryGetValue(spell.Id, out Row row))
            {
                string rowPath = reducedMotion ? row.reducedMotionAsset : row.normalMotionAsset;
                Sprite rowSprite = string.IsNullOrEmpty(rowPath) ? null : _spriteLoader(rowPath);
                if (rowSprite != null)
                    return new Resolution(spell.Id, row.family, rowPath, Tier.ManifestRow, rowSprite);
            }

            if (spell != null)
            {
                string familyPath = VfxResourceRoot + FamilyAssetBaseName(spell.Effect) + suffix;
                Sprite familySprite = _spriteLoader(familyPath);
                if (familySprite != null)
                    return new Resolution(spell.Id, null, familyPath, Tier.EffectFamily, familySprite);
            }

            string genericPath = VfxResourceRoot + GenericFallbackBaseName + suffix;
            return new Resolution(spell?.Id, null, genericPath, Tier.Generic, _spriteLoader(genericPath));
        }

        public static Sprite ResolveSprite(AvatarSpell spell, bool reducedMotion) =>
            Resolve(spell, reducedMotion).Sprite;

        /// <summary>The approved family asset for a spell effect - the tier-2 fallback, and the
        /// same mapping the manifest rows were generated from. Every SpellEffect value is listed.</summary>
        public static string FamilyAssetBaseName(SpellEffect effect) => effect switch
        {
            SpellEffect.LaneDamage => "Fire_Explosion",
            SpellEffect.CrossLaneDamage => "Area_Damage",
            SpellEffect.AllLaneDamage => "Area_Damage",
            SpellEffect.AvatarStrike => "Lightning_Strike",
            SpellEffect.LaneHeal => "Heal_Ring",
            SpellEffect.LaneAttackBuff => "Magic_Circle",
            SpellEffect.AllLaneAttackBuff => "Magic_Circle",
            SpellEffect.LaneShield => "Shield_Bubble",
            SpellEffect.Cleanse => "Cleanse_Dispel",
            SpellEffect.Dispel => "Cleanse_Dispel",
            SpellEffect.Vulnerability => "Mark_Silence",
            SpellEffect.Silence => "Mark_Silence",
            SpellEffect.DrawCards => "Draw_Movement",
            SpellEffect.Reposition => "Draw_Movement",
            _ => GenericFallbackBaseName,
        };

        private static void EnsureLoaded()
        {
            if (_rows != null) return;
            TextAsset json = Resources.Load<TextAsset>(ManifestResourcePath);
            _rows = Parse(json != null ? json.text : null);
        }

        private static Dictionary<string, Row> Parse(string json)
        {
            var rows = new Dictionary<string, Row>();
            if (string.IsNullOrEmpty(json)) return rows;

            ManifestData data;
            try { data = JsonUtility.FromJson<ManifestData>(json); }
            catch (ArgumentException) { return rows; }

            if (data?.spells == null) return rows;
            foreach (Row row in data.spells)
            {
                if (row == null || string.IsNullOrEmpty(row.spellId)) continue;
                rows[row.spellId] = row; // last row wins; duplicates are the manifest test's job to reject.
            }
            return rows;
        }
    }
}

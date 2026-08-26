using UnityEngine;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Asserts that shared-chrome sprites actually LOAD and can actually 9-SLICE.
    ///
    /// WHY THIS EXISTS: two real bugs were invisible to a green 1782/1785 suite, because nothing
    /// anywhere asserted which visual path rendered - real art, or the flat-colour fallback. The
    /// third item below is a theory of mine that this file's own assertion DISPROVED, kept because
    /// a retraction is more useful to the next reader than a deleted claim:
    ///   1. All 8 SharedFoundation .meta files were truncated mid-token, so every sprite failed to
    ///      load and every button fell back to flat colour.
    ///   2. RETRACTED - I claimed these sprites were imported Tight and so could never 9-slice.
    ///      The assertion below, written to prove it, showed they come back FullRect: Unity forces
    ///      FullRect whenever a border is defined, which makes the .meta's spriteMeshType field
    ///      inert. Nobody should bulk-edit spriteMeshType on this basis. The check stays as a
    ///      forward guard, not as evidence of a bug that existed.
    ///   3. My own CombatResolutionStage loaded the AvatarStrike sheet from a path with no
    ///      Resources folder. My test asserted the layer was ENABLED, not that it had a SPRITE - a
    ///      null sprite renders as a tinted quad and passed.
    ///
    /// Every one of those is a silent degradation: the game keeps running and looks plausible. The
    /// only defence is asserting the real path was taken, which is what this file does.
    /// </summary>
    public class SharedChromeSpriteIntegrityTests
    {
        /// <summary>The shared chrome every restyled screen draws through. Named explicitly rather
        /// than globbed, so deleting one is a visible test change instead of silent coverage loss.</summary>
        private static readonly string[] SharedChromeSprites =
        {
            "UI/SharedFoundation/ui_button_primary_normal_v1",
            "UI/SharedFoundation/ui_button_primary_pressed_v1",
            "UI/SharedFoundation/ui_button_secondary_normal_v1",
            "UI/SharedFoundation/ui_button_secondary_pressed_v1",
            "UI/SharedFoundation/ui_content_panel_v1",
            "UI/SharedFoundation/ui_list_row_v1",
            "UI/SharedFoundation/ui_modal_dialog_v1",
        };

        [Test]
        public void EverySharedChromeSprite_ActuallyLoads()
        {
            // Catches truncated/corrupt metas and wrong Resources paths. Both produce a null sprite,
            // and a null sprite renders as a plain tinted rectangle - which looks like a design
            // choice rather than a failure.
            foreach (string path in SharedChromeSprites)
            {
                Sprite sprite = Resources.Load<Sprite>(path);
                Assert.IsNotNull(sprite,
                    "'" + path + "' failed to load. Every screen drawing this falls back to flat " +
                    "colour, which is indistinguishable from intended styling at a glance.");
            }
        }

        [Test]
        public void EveryBorderedChromeSprite_CanActuallyNineSlice()
        {
            // A 9-slice border is only applied when the sprite has FullRect geometry. A Tight sprite
            // silently renders Simple - borders defined, never drawn - which is exactly the "flat
            // boxes" symptom with real art loaded and correct colours.
            //
            // FullRect sprites are a plain quad: 4 vertices. Tight meshes fit the alpha outline and
            // produce a different count. Checked through the loaded Sprite rather than by parsing
            // .meta text, so this tests what Unity actually built, not what the importer intended.
            foreach (string path in SharedChromeSprites)
            {
                Sprite sprite = Resources.Load<Sprite>(path);
                if (sprite == null) continue;   // reported by the load test; not double-failed here

                if (sprite.border == Vector4.zero) continue;   // no border to slice

                Assert.AreEqual(4, sprite.vertices.Length,
                    "'" + path + "' defines a 9-slice border " + sprite.border + " but is imported " +
                    "Tight (" + sprite.vertices.Length + " vertices). Image.Type.Sliced requires " +
                    "FullRect, so the border is never applied and the frame silently disappears. " +
                    "Fix: set spriteMeshType to FullRect on this asset.");
            }
        }

        [Test]
        public void TheAvatarStrikeFlipbook_LoadsFromWhereTheCodeAsksForIt()
        {
            // My own bug, pinned so it cannot regress. CombatResolutionStage loads this sheet by
            // path; the asset lived outside any Resources folder, so the load returned null and the
            // strike layer rendered as a blank tinted quad while every assertion I had written
            // still passed.
            Sprite sheet = Resources.Load<Sprite>("VFX/avatarstrike_bespoke_sheet");

            Assert.IsNotNull(sheet,
                "The AvatarStrike flipbook does not load from 'VFX/avatarstrike_bespoke_sheet'. " +
                "The asset sits at Assets/Art/VFX/, which is NOT under a Resources folder, so " +
                "Resources.Load cannot see it. Either move it under Assets/Resources/ or change " +
                "the call to the real path - but do not leave the layer enabled with a null sprite, " +
                "because that renders as a plain rectangle and looks deliberate.");
        }
    }
}

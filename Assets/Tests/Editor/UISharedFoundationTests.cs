using System.Collections.Generic;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Real foundation tests for the color-token set and border/frame primitive added to
    /// UISharedFoundation (LOCKED 2026-08-26, register: docs/INDUSTRY_STANDARD_GAP_DIAGNOSIS_
    /// 2026-08-26.md §4 - "UISharedFoundation/UIFrozenTokens defines tokens referenced only from
    /// within its own file, zero presenters use it; no shared color-token set exists at all; real
    /// border/frame art exists in only 2 of ~23 screens with no shared primitive to draw from").
    /// This pass builds and tests the foundation only - migrating existing screens onto it is a
    /// separate, later task, per the explicit instruction not to do a wide rollout in this pass.
    /// </summary>
    public class UISharedFoundationTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---------- Color tokens: real, cited, regression-locked ----------

        [Test]
        public void ColorTokens_MatchTheRealCitedValues_NotInvented()
        {
            // Regression lock, RELOCKED 2026-08-26 against Myriad_of_Dragons_Visual_Authority_
            // Memory.md's "Materials and palette" section (CC-authorized relock, superseding the
            // first-pass consolidation this test originally locked) - if one of these ever drifts,
            // it means someone changed the token without updating the citation, which is exactly
            // the kind of silent drift the token set exists to prevent.
            Assert.AreEqual(new Color(0.06f, 0.07f, 0.1f), UIFrozenTokens.ColorBackground);
            Assert.AreEqual(new Color(0.1f, 0.11f, 0.15f), UIFrozenTokens.ColorPanel);
            Assert.AreEqual(new Color(0.045f, 0.05f, 0.075f), UIFrozenTokens.ColorHeader);
            Assert.AreEqual(new Color(0.62f, 0.52f, 0.34f, 0.6f), UIFrozenTokens.ColorAccentBronze);
            Assert.AreEqual(new Color(0.13f, 0.16f, 0.23f), UIFrozenTokens.ColorSecondary);
            Assert.AreEqual(new Color(0.14f, 0.36f, 0.24f), UIFrozenTokens.ColorAccentEmerald);
            Assert.AreEqual(new Color(0.32f, 0.7f, 0.74f), UIFrozenTokens.ColorAccentCyan);
            Assert.AreEqual(new Color(0.55f, 0.16f, 0.14f), UIFrozenTokens.ColorAccentRed);
            Assert.AreEqual(new Color(0.92f, 0.9f, 0.8f), UIFrozenTokens.ColorTextPrimary);
        }

        [Test]
        public void ColorTokens_AreAllDistinctFromEachOther()
        {
            // A real, useful token set has no two tokens accidentally colliding - guards against
            // a copy-paste mistake silently making two "different" surfaces render identically.
            var tokens = new[]
            {
                UIFrozenTokens.ColorBackground, UIFrozenTokens.ColorPanel, UIFrozenTokens.ColorHeader,
                UIFrozenTokens.ColorAccentBronze, UIFrozenTokens.ColorSecondary, UIFrozenTokens.ColorAccentEmerald,
                UIFrozenTokens.ColorAccentCyan, UIFrozenTokens.ColorAccentRed, UIFrozenTokens.ColorTextPrimary,
            };
            for (int i = 0; i < tokens.Length; i++)
                for (int j = i + 1; j < tokens.Length; j++)
                    Assert.AreNotEqual(tokens[i], tokens[j], $"Token {i} and token {j} are identical.");
        }

        [Test]
        public void BackgroundAndPanelTokens_AreDarkSurfaces_TextTokenIsLightForContrast()
        {
            // Real, checkable design invariant - dark UI needs light text, not the specific hue.
            // Luminance approximation (perceptual weights), not exact - just proves the surfaces
            // are meaningfully darker than the text token, so text stays legible.
            float bgLum = Luminance(UIFrozenTokens.ColorBackground);
            float panelLum = Luminance(UIFrozenTokens.ColorPanel);
            float textLum = Luminance(UIFrozenTokens.ColorTextPrimary);
            Assert.Less(bgLum, 0.3f, "Background token should be a dark surface.");
            Assert.Less(panelLum, 0.3f, "Panel token should be a dark surface.");
            Assert.Greater(textLum, 0.6f, "Text token should be light enough to read on a dark surface.");
        }

        private static float Luminance(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        // ---------- Border/frame primitive: real sprite, real fallback behavior ----------

        [Test]
        public void CreateRoundedPanelSprite_ProducesARealNonNullSprite_WithA9SliceBorder()
        {
            Sprite sprite = UISharedFoundation.CreateRoundedPanelSprite(
                UIFrozenTokens.ColorAccentBronze, UIFrozenTokens.ColorPanel, cornerRadius: 18, size: 64);
            _spawned.Add(sprite);
            _spawned.Add(sprite.texture);

            Assert.IsNotNull(sprite, "Must produce a real Sprite, not null.");
            Assert.IsNotNull(sprite.texture, "Sprite must carry a real texture.");
            Assert.AreEqual(64, sprite.texture.width);
            Assert.AreEqual(64, sprite.texture.height);
            // A 9-slice border of all zeros would mean the sprite never actually slices - it
            // would stretch its rounded corners across any panel size instead of preserving them.
            Vector4 border = sprite.border;
            Assert.Greater(border.x, 0f, "Sprite must have a real 9-slice border, not (0,0,0,0).");
            Assert.AreEqual(18f, border.x, 0.01f);
            Assert.AreEqual(border.x, border.y);
            Assert.AreEqual(border.x, border.z);
            Assert.AreEqual(border.x, border.w);
        }

        [Test]
        public void CreateRoundedPanelSprite_CornersAreTransparent_CenterIsOpaque()
        {
            // Real proof the rounding math actually rounds something, not just returns a flat
            // rect with border metadata attached - a real bordered/framed look, not a lie.
            const int size = 64;
            const int radius = 18;
            Sprite sprite = UISharedFoundation.CreateRoundedPanelSprite(
                new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 1f), cornerRadius: radius, size: size);
            _spawned.Add(sprite);
            _spawned.Add(sprite.texture);

            Color corner = sprite.texture.GetPixel(0, 0);
            Color center = sprite.texture.GetPixel(size / 2, size / 2);
            Assert.Less(corner.a, 0.05f, "The extreme corner pixel should be fully transparent (rounded away).");
            Assert.Greater(center.a, 0.95f, "The center pixel should be fully opaque.");
        }

        [Test]
        public void CreateRoundedPanelSprite_GradientRuns_TopColorAtTop_BottomColorAtBottom()
        {
            const int size = 64;
            Color top = new Color(1f, 0f, 0f, 1f);
            Color bottom = new Color(0f, 0f, 1f, 1f);
            // cornerRadius 0 so corner-rounding alpha shaping doesn't interfere with reading the
            // raw gradient color at the texture's own edge rows.
            Sprite sprite = UISharedFoundation.CreateRoundedPanelSprite(top, bottom, cornerRadius: 0, size: size);
            _spawned.Add(sprite);
            _spawned.Add(sprite.texture);

            Color topRow = sprite.texture.GetPixel(size / 2, size - 1);
            Color bottomRow = sprite.texture.GetPixel(size / 2, 0);
            Assert.Greater(topRow.r, 0.9f, "Top row should be close to the top color (red).");
            Assert.Less(topRow.b, 0.1f);
            Assert.Greater(bottomRow.b, 0.9f, "Bottom row should be close to the bottom color (blue).");
            Assert.Less(bottomRow.r, 0.1f);
        }

        [Test]
        public void ApplyFramedPanel_WithNoRealArt_FallsBackToTheProceduralSprite_NotAFlatRect()
        {
            var go = new GameObject("FramedPanelTestTarget", typeof(RectTransform), typeof(Image));
            _spawned.Add(go);
            Image img = go.GetComponent<Image>();

            UISharedFoundation.ApplyFramedPanel(img, "UI/DoesNotExist/NoSuchFrame",
                UIFrozenTokens.ColorAccentBronze, UIFrozenTokens.ColorPanel);

            Assert.IsNotNull(img.sprite, "With no real art, the procedural fallback sprite must still be applied - never a bare flat Image.");
            Assert.AreEqual(Image.Type.Sliced, img.type, "Must be Sliced so the 9-slice border scales correctly.");
            Assert.AreEqual(Color.white, img.color, "Color must be white so the sprite's own baked gradient shows through untinted.");
        }

        [Test]
        public void ApplyFramedPanel_NullTarget_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => UISharedFoundation.ApplyFramedPanel(
                null, null, UIFrozenTokens.ColorAccentBronze, UIFrozenTokens.ColorPanel));
        }

        [Test]
        public void CreateFramedPanel_BuildsARealRectTransform_WithTheFrameAlreadyApplied()
        {
            var parent = new GameObject("FramedPanelParent", typeof(RectTransform));
            _spawned.Add(parent);

            RectTransform panel = UISharedFoundation.CreateFramedPanel(
                parent.transform, "TestPanel", new Vector2(300f, 200f), null,
                UIFrozenTokens.ColorAccentBronze, UIFrozenTokens.ColorPanel);

            Assert.IsNotNull(panel);
            Assert.AreEqual(new Vector2(300f, 200f), panel.sizeDelta);
            Assert.AreEqual(parent.transform, panel.parent);
            Image img = panel.GetComponent<Image>();
            Assert.IsNotNull(img);
            Assert.IsNotNull(img.sprite, "CreateFramedPanel must leave the panel with a real frame sprite applied.");
            Assert.AreEqual(Image.Type.Sliced, img.type);
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Typography + frame-tier design tokens (register: "Typography - LOCKED 2026-08-27" and
    /// "Framing - LOCKED 2026-08-27: four tiers + weighted ratio, replacing 'max 3 heavy
    /// frames'"). 1920x1080 landscape reference, 1.25 modular type scale. A vocabulary and a
    /// ceiling, not a requirement to show every size/tier on one screen.
    /// </summary>
    public static class UIDesignTokens
    {
        public enum TypeTier
        {
            T1Micro,    // 22px / w500 / lh 26  - compact metadata, timers, secondary labels
            T2Utility,  // 28px / w500 / lh 34  - resource labels, rows, tooltips
            T3Body,     // 35px / w400-500 / lh 42 - descriptions, instructional copy
            T4Control,  // 44px / w600 / lh 52  - buttons, tabs, navigation labels
            T5Section,  // 55px / w600 / lh 66  - section headings, mode names
            T6Hero,     // 69px / w700 / lh 83  - primary CTA, featured title
            T7Display,  // 86px / w700 / lh 103 - rare opening / major result headline
        }

        public struct TypeSpec
        {
            public int FontSize;
            public FontStyle Style;
            public float LineHeight;
        }

        /// <summary>HARD FLOOR: no player-facing text may render below this, regardless of tier.</summary>
        public const int AbsoluteMinFontSize = 22;

        /// <summary>HARD FLOOR: body copy and interactive (button/control) labels specifically.</summary>
        public const int BodyAndInteractiveMinFontSize = 28;

        public static TypeSpec Spec(TypeTier tier)
        {
            switch (tier)
            {
                case TypeTier.T1Micro: return new TypeSpec { FontSize = 22, Style = FontStyle.Normal, LineHeight = 26 };
                case TypeTier.T2Utility: return new TypeSpec { FontSize = 28, Style = FontStyle.Normal, LineHeight = 34 };
                case TypeTier.T3Body: return new TypeSpec { FontSize = 35, Style = FontStyle.Normal, LineHeight = 42 };
                case TypeTier.T4Control: return new TypeSpec { FontSize = 44, Style = FontStyle.Bold, LineHeight = 52 };
                case TypeTier.T5Section: return new TypeSpec { FontSize = 55, Style = FontStyle.Bold, LineHeight = 66 };
                case TypeTier.T6Hero: return new TypeSpec { FontSize = 69, Style = FontStyle.Bold, LineHeight = 83 };
                case TypeTier.T7Display: return new TypeSpec { FontSize = 86, Style = FontStyle.Bold, LineHeight = 103 };
                default: return new TypeSpec { FontSize = AbsoluteMinFontSize, Style = FontStyle.Normal, LineHeight = 26 };
            }
        }

        /// <summary>Applies a type tier's font size + style to an existing Text. Does not touch
        /// colour, alignment, or wrap mode - callers set those per their own context.</summary>
        public static void Apply(Text text, TypeTier tier)
        {
            if (text == null) return;
            TypeSpec spec = Spec(tier);
            text.fontSize = spec.FontSize;
            text.fontStyle = spec.Style;
        }

        public enum FrameTier
        {
            Tier1Hero,    // 3-4px, high-contrast ornament, inner glow, strong shadow, 90-100% fill
            Tier2Section, // 2px, restrained ornament, medium shadow, 70-85% fill
            Tier3Utility, // 1px or accent line, no ornament, minimal shadow, 35-60% translucent
            Tier4Surface, // no border - spacing/tint/divider only, 10-35% translucent
        }

        /// <summary>Fill alpha to use for a frame tier's backing colour (mid-band of each tier's
        /// documented range). Border thickness/ornament is carried by which sprite ApplyFramedPanel
        /// picks, not by this token - Tier 1-2 use the real bordered sprite, Tier 3-4 should call
        /// ApplyFramedPanel with a null/flat sprite (or skip it) and only use this fill.</summary>
        public static float FillAlpha(FrameTier tier)
        {
            switch (tier)
            {
                case FrameTier.Tier1Hero: return 0.95f;
                case FrameTier.Tier2Section: return 0.78f;
                case FrameTier.Tier3Utility: return 0.48f;
                case FrameTier.Tier4Surface: return 0.22f;
                default: return 1f;
            }
        }

        // ScrimPanelOpacity removed 2026-08-27 alongside AddSemiTransparentScrimPanel
        // (UISharedFoundation.cs) - see that removal's comment for why.
    }
}

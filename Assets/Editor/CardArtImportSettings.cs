using UnityEditor;
using UnityEngine;

namespace MyriadOfDragons.EditorTools
{
    /// <summary>
    /// Recreated after a local Assets-folder loss on 2026-08-05 (this file wasn't in the Drive
    /// backup - everything else was restored from there). Automatically imports any texture
    /// under Resources/CardArt, Resources/UI, or Resources/Branding as a UI Sprite, so card art
    /// and UI images don't need a manual per-file Inspector step after import.
    /// </summary>
    public class CardArtImportSettings : AssetPostprocessor
    {
        // Ornate hand-painted frame borders (cropped from the uploaded reference-sheet PNGs,
        // caption removed - see Resources/UI/Frames/NineSlice/) need a pixel spriteBorder so
        // Image.Type.Sliced stretches only the straight edges and leaves the painted corner
        // ornamentation un-squished. 100px is an approximate best-effort measurement (the art
        // isn't perfectly symmetric - hand-painted, not a precision UI kit), not pixel-exact.
        private const string NineSlicePathMarker = "/Resources/UI/Frames/NineSlice/";
        private static readonly Vector4 NineSliceBorder = new Vector4(100, 100, 100, 100);

        // 2026-08-05: the rarity card frames (Common/Rare/Epic/Legendary_Card_Frame) and the
        // lane slot frames (Friendly/Enemy/Empty/etc _Slot) were being rendered with
        // Image.Type.Simple - a flat non-uniform stretch to fit whatever rect they're dropped
        // into, which doesn't match their native aspect ratio and visibly warps the painted
        // corner ornamentation ("the icons are distorted"). A flat 20%-of-dimension border
        // (not individually measured per file - same caveat as NineSliceBorder above) is enough
        // for Image.Type.Sliced to keep those corners undistorted regardless of the target rect.
        private const string CardFramePathSuffix = "_Card_Frame.png";
        private const string SlotPathSuffix = "_Slot.png";

        private void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            bool isManagedArt = path.Contains("/Resources/CardArt/")
                || path.Contains("/Resources/UI/")
                || path.Contains("/Resources/Branding/");

            if (!isManagedArt) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;

            if (path.Contains(NineSlicePathMarker))
            {
                importer.spriteBorder = NineSliceBorder;
            }
            else if (path.Contains("/Resources/UI/Frames/") && path.EndsWith(CardFramePathSuffix))
            {
                importer.spriteBorder = FractionalBorder(importer, 0.20f);
            }
            else if (path.Contains("/Resources/UI/Slots/") && path.EndsWith(SlotPathSuffix))
            {
                importer.spriteBorder = FractionalBorder(importer, 0.20f);
            }
            else if (path.Contains("/Resources/UI/Bars/"))
            {
                // Horizontal-only 9-slice: these bars were rebuilt (see the build script note in
                // the README) as [ornate left cap | plain middle | arrow right cap], so only the
                // middle should stretch. Vertical borders stay 0 - the bar is a single band
                // top to bottom and slicing it vertically would just blur it.
                importer.GetSourceTextureWidthAndHeight(out int width, out _);
                float cap = width * 0.26f;
                importer.spriteBorder = new Vector4(cap, 0f, cap, 0f);
            }
        }

        /// <summary>
        /// TextureImporter.GetSourceTextureWidthAndHeight reads the source file's actual
        /// dimensions before import finishes - the officially supported way to get pixel size
        /// at OnPreprocessTexture time, rather than depending on System.Drawing (not guaranteed
        /// available/referenced in Unity's Editor assembly) or the (not-yet-populated) imported
        /// texture itself.
        /// </summary>
        private static Vector4 FractionalBorder(TextureImporter importer, float fraction)
        {
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            float bx = width * fraction;
            float by = height * fraction;
            return new Vector4(bx, by, bx, by);
        }
    }
}

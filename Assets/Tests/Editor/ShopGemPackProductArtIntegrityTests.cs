using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Asserts the four Shop gem-pack product illustrations actually load and can paint —
    /// same failure class SharedChromeSpriteIntegrityTests guards against (null sprite → flat
    /// colour while the suite stays green).
    /// </summary>
    public class ShopGemPackProductArtIntegrityTests
    {
        private static readonly string[] ProductArtPaths =
        {
            "UI/ShopV1/product_art_pack_single_sigil",
            "UI/ShopV1/product_art_pack_scout_cache",
            "UI/ShopV1/product_art_pack_warband_cache",
            "UI/ShopV1/product_art_pack_legion_cache",
        };

        [Test]
        public void EveryGemPackProductArt_ActuallyLoads()
        {
            foreach (string path in ProductArtPaths)
            {
                Sprite sprite = Resources.Load<Sprite>(path);
                Assert.IsNotNull(sprite,
                    "'" + path + "' failed to load. Shop product wells fall back to JPEG/flat " +
                    "placeholders when this is null.");
            }
        }

        [Test]
        public void EveryGemPackProductArt_KeepsSourceResolution_600x1120()
        {
            // maxTextureSize below 1120 silently downscales on import; wells are ~149x280 with
            // preserveAspect, but the asset itself must remain the authored 600x1120.
            foreach (string path in ProductArtPaths)
            {
                Sprite sprite = Resources.Load<Sprite>(path);
                Assert.IsNotNull(sprite, path);
                Assert.AreEqual(600, sprite.texture.width,
                    "'" + path + "' texture width is " + sprite.texture.width +
                    " after import (expected 600). Check maxTextureSize.");
                Assert.AreEqual(1120, sprite.texture.height,
                    "'" + path + "' texture height is " + sprite.texture.height +
                    " after import (expected 1120). Check maxTextureSize.");
            }
        }

        [Test]
        public void EveryGemPackProductArt_RendersOnAnImage_NotNullSprite()
        {
            foreach (string path in ProductArtPaths)
            {
                Sprite sprite = Resources.Load<Sprite>(path);
                Assert.IsNotNull(sprite, path);

                var go = new GameObject("ShopProductArtProbe", typeof(RectTransform), typeof(Image));
                try
                {
                    Image img = go.GetComponent<Image>();
                    img.sprite = sprite;
                    img.type = Image.Type.Simple;
                    img.preserveAspect = true;
                    img.color = Color.white;

                    Assert.IsNotNull(img.sprite,
                        "'" + path + "' assigned to Image but sprite is still null — would paint flat colour.");
                    Assert.AreSame(sprite, img.sprite,
                        "'" + path + "' Image.sprite is not the loaded product art.");
                    Assert.AreEqual(600f, img.sprite.rect.width, 0.1f, path + " sprite.rect.width");
                    Assert.AreEqual(1120f, img.sprite.rect.height, 0.1f, path + " sprite.rect.height");
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            }
        }
    }
}

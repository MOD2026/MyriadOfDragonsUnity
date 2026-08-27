using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using MyriadOfDragons.AI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Asserts the three approved art sets (Enemy Crest Emblems, Empty-State Illustrations,
    /// Loading Ember Sigil Frames) actually load, retain their authored dimensions without
    /// silent downscaling, and can render on an Image component without falling back to flat colour.
    /// Follows the SharedChromeSpriteIntegrityTests / ShopGemPackProductArtIntegrityTests pattern.
    /// </summary>
    public class ImportedArtSetsIntegrityTests
    {
        private static readonly (string path, AIDifficultyTier tier)[] EnemyCrestPaths =
        {
            ("UI/EnemyCrestsV1/enemy_crest_novice_v1", AIDifficultyTier.Novice),
            ("UI/EnemyCrestsV1/enemy_crest_apprentice_v1", AIDifficultyTier.Apprentice),
            ("UI/EnemyCrestsV1/enemy_crest_veteran_v1", AIDifficultyTier.Veteran),
            ("UI/EnemyCrestsV1/enemy_crest_master_v1", AIDifficultyTier.Master),
            ("UI/EnemyCrestsV1/enemy_crest_titan_v1", AIDifficultyTier.Titan),
        };

        private static readonly string[] EmptyStatePaths =
        {
            "UI/EmptyStatesV1/empty_state_no_friends_v1",
            "UI/EmptyStatesV1/empty_state_no_mail_v1",
            "UI/EmptyStatesV1/empty_state_no_guild_v1",
            "UI/EmptyStatesV1/empty_state_collection_filter_v1",
            "UI/EmptyStatesV1/empty_state_all_quests_claimed_v1",
            "UI/EmptyStatesV1/empty_state_battle_pass_not_started_v1",
        };

        private static readonly string[] LoadingSigilPaths =
        {
            "UI/LoadingSigilV1/loading_ember_sigil_v3_frame_01",
            "UI/LoadingSigilV1/loading_ember_sigil_v3_frame_02",
            "UI/LoadingSigilV1/loading_ember_sigil_v3_frame_03",
            "UI/LoadingSigilV1/loading_ember_sigil_v3_frame_04",
            "UI/LoadingSigilV1/loading_ember_sigil_v3_frame_05",
            "UI/LoadingSigilV1/loading_ember_sigil_v3_frame_06",
            "UI/LoadingSigilV1/loading_ember_sigil_v3_frame_07",
            "UI/LoadingSigilV1/loading_ember_sigil_v3_frame_08",
        };

        [Test]
        public void EveryEnemyCrest_ActuallyLoads_AndMapsToDifficultyTier()
        {
            Assert.AreEqual(5, EnemyCrestPaths.Length, "Must have exactly 5 difficulty tier crests.");
            foreach (var (path, tier) in EnemyCrestPaths)
            {
                Sprite sprite = Resources.Load<Sprite>(path);
                Assert.IsNotNull(sprite,
                    $"'{path}' failed to load for tier {tier}. Battle enemy HUD crest would fall back to flat colour.");
            }
        }

        [Test]
        public void EveryEnemyCrest_KeepsSourceResolution_512x512()
        {
            foreach (var (path, _) in EnemyCrestPaths)
            {
                Sprite sprite = Resources.Load<Sprite>(path);
                Assert.IsNotNull(sprite, path);
                Assert.AreEqual(512, sprite.texture.width,
                    $"'{path}' texture width is {sprite.texture.width} (expected 512). Check maxTextureSize.");
                Assert.AreEqual(512, sprite.texture.height,
                    $"'{path}' texture height is {sprite.texture.height} (expected 512). Check maxTextureSize.");
            }
        }

        [Test]
        public void EveryEnemyCrest_RendersOnAnImage_NotNullSprite()
        {
            foreach (var (path, _) in EnemyCrestPaths)
            {
                Sprite sprite = Resources.Load<Sprite>(path);
                Assert.IsNotNull(sprite, path);

                var go = new GameObject("EnemyCrestProbe", typeof(RectTransform), typeof(Image));
                try
                {
                    Image img = go.GetComponent<Image>();
                    img.sprite = sprite;
                    img.type = Image.Type.Simple;
                    img.preserveAspect = true;
                    img.color = Color.white;

                    Assert.IsNotNull(img.sprite, $"'{path}' assigned to Image but sprite is null.");
                    Assert.AreSame(sprite, img.sprite, $"'{path}' Image.sprite is not the loaded crest.");
                    Assert.AreEqual(512f, img.sprite.rect.width, 0.1f, $"{path} width");
                    Assert.AreEqual(512f, img.sprite.rect.height, 0.1f, $"{path} height");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }
        }

        [Test]
        public void EveryEmptyStateIllustration_ActuallyLoads()
        {
            Assert.AreEqual(6, EmptyStatePaths.Length, "Must have exactly 6 empty state illustrations.");
            foreach (string path in EmptyStatePaths)
            {
                Sprite sprite = Resources.Load<Sprite>(path);
                Assert.IsNotNull(sprite,
                    $"'{path}' failed to load. Empty-state panels would fall back to flat colour.");
            }
        }

        [Test]
        public void EveryEmptyStateIllustration_KeepsSourceResolution_800x600()
        {
            foreach (string path in EmptyStatePaths)
            {
                Sprite sprite = Resources.Load<Sprite>(path);
                Assert.IsNotNull(sprite, path);
                Assert.AreEqual(800, sprite.texture.width,
                    $"'{path}' texture width is {sprite.texture.width} (expected 800). Check maxTextureSize.");
                Assert.AreEqual(600, sprite.texture.height,
                    $"'{path}' texture height is {sprite.texture.height} (expected 600). Check maxTextureSize.");
            }
        }

        [Test]
        public void EveryEmptyStateIllustration_RendersOnAnImage_NotNullSprite()
        {
            foreach (string path in EmptyStatePaths)
            {
                Sprite sprite = Resources.Load<Sprite>(path);
                Assert.IsNotNull(sprite, path);

                var go = new GameObject("EmptyStateProbe", typeof(RectTransform), typeof(Image));
                try
                {
                    Image img = go.GetComponent<Image>();
                    img.sprite = sprite;
                    img.type = Image.Type.Simple;
                    img.preserveAspect = true;
                    img.color = Color.white;

                    Assert.IsNotNull(img.sprite, $"'{path}' assigned to Image but sprite is null.");
                    Assert.AreSame(sprite, img.sprite, $"'{path}' Image.sprite is not the loaded illustration.");
                    Assert.AreEqual(800f, img.sprite.rect.width, 0.1f, $"{path} width");
                    Assert.AreEqual(600f, img.sprite.rect.height, 0.1f, $"{path} height");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }
        }

        [Test]
        public void EveryLoadingSigilFrame_ActuallyLoads_V3Sequence()
        {
            Assert.AreEqual(8, LoadingSigilPaths.Length, "Must have exactly 8 loading sigil frames.");
            for (int i = 0; i < LoadingSigilPaths.Length; i++)
            {
                string path = LoadingSigilPaths[i];
                Sprite sprite = Resources.Load<Sprite>(path);
                Assert.IsNotNull(sprite,
                    $"'{path}' frame {i + 1} failed to load. Loading transition sigil would be missing frame.");
            }
        }

        [Test]
        public void EveryLoadingSigilFrame_KeepsSourceResolution_256x256()
        {
            foreach (string path in LoadingSigilPaths)
            {
                Sprite sprite = Resources.Load<Sprite>(path);
                Assert.IsNotNull(sprite, path);
                Assert.AreEqual(256, sprite.texture.width,
                    $"'{path}' texture width is {sprite.texture.width} (expected 256). Check maxTextureSize.");
                Assert.AreEqual(256, sprite.texture.height,
                    $"'{path}' texture height is {sprite.texture.height} (expected 256). Check maxTextureSize.");
            }
        }

        [Test]
        public void EveryLoadingSigilFrame_RendersOnAnImage_NotNullSprite()
        {
            foreach (string path in LoadingSigilPaths)
            {
                Sprite sprite = Resources.Load<Sprite>(path);
                Assert.IsNotNull(sprite, path);

                var go = new GameObject("LoadingSigilProbe", typeof(RectTransform), typeof(Image));
                try
                {
                    Image img = go.GetComponent<Image>();
                    img.sprite = sprite;
                    img.type = Image.Type.Simple;
                    img.preserveAspect = true;
                    img.color = Color.white;

                    Assert.IsNotNull(img.sprite, $"'{path}' assigned to Image but sprite is null.");
                    Assert.AreSame(sprite, img.sprite, $"'{path}' Image.sprite is not the loaded sigil frame.");
                    Assert.AreEqual(256f, img.sprite.rect.width, 0.1f, $"{path} width");
                    Assert.AreEqual(256f, img.sprite.rect.height, 0.1f, $"{path} height");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }
        }
    }
}

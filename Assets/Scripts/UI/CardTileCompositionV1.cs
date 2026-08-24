using System;
using System.Collections.Generic;
using MyriadOfDragons.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Deck/Collection card-tile composition V1 — 400×600 RGBA frame + per-card portrait tiles.
    /// Layer order (back → front): portrait → frame → name → class/school → cost/ATK/HP text.
    /// Runtime catalog covers every authored portrait tile; explicitly blocked identities use legacy tiles.
    /// </summary>
    public static class CardTileCompositionV1
    {
        public const string ResourceRoot = "UI/CardTiles/V1/";
        public const string FrameSpriteName = "card_tile_frame_v1";

        /// <summary>Cards intentionally held on the legacy rarity-frame tile pending reliable art.</summary>
        public static readonly IReadOnlyList<string> LegacyCardIds = new[]
        {
            "dragon_tamer",
            "ancient_dragon",
            "forest_fairy",
        };

        private static readonly IReadOnlyList<string> AuthoredCardIds = BuildAuthoredCardIds();

        /// <summary>Asset-driven V1 catalog; avoids duplicating all card IDs in source code.</summary>
        public static IReadOnlyList<string> CompositionCardIds => AuthoredCardIds;

        /// <summary>Compatibility alias retained for existing tests/callers from the four-card proof.</summary>
        [Obsolete("Use CompositionCardIds; V1 is no longer a four-card proof of concept.")]
        public static IReadOnlyList<string> PocCardIds => CompositionCardIds;

        public static readonly Vector2 NativeSize = new Vector2(400f, 600f);

        public struct CardTileContent
        {
            public string CardId;
            public string DisplayName;
            public string ClassSchoolLine;
            public int Cost;
            public int Attack;
            public int Health;
            public Sprite FallbackPortrait;
        }

        public static bool HasPack => LoadFrame() != null && CompositionCardIds.Count > 0;

        public static bool IsPocCard(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return false;
            for (int i = 0; i < CompositionCardIds.Count; i++)
            {
                if (CompositionCardIds[i] == cardId) return true;
            }

            return false;
        }

        public static bool ShouldUseComposition(string cardId) => HasPack && IsPocCard(cardId);

        public static Sprite LoadFrame() => LoadSprite(FrameSpriteName);

        public static Sprite LoadPortraitTile(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return null;
            return LoadSprite($"card_tile_art_{cardId}_v1");
        }

        public static CardTileContent ContentFromCard(
            string cardId,
            string displayName,
            int cost,
            int attack,
            int health,
            Card resolved,
            Sprite fallbackPortrait)
        {
            string classSchool = FormatClassSchoolLine(resolved);
            return new CardTileContent
            {
                CardId = cardId,
                DisplayName = displayName,
                ClassSchoolLine = classSchool,
                Cost = cost,
                Attack = attack,
                Health = health,
                FallbackPortrait = fallbackPortrait,
            };
        }

        public static string FormatClassSchoolLine(Card resolved)
        {
            if (resolved == null) return string.Empty;
            string element = resolved.Element.ToString();
            string role = resolved.Class.ToString();
            return $"{element} · {role}";
        }

        /// <summary>
        /// Builds V1 composition layers on <paramref name="cardRoot"/> (sibling order = draw order).
        /// Returns false when the pack or card ID is not in the POC set.
        /// </summary>
        public static bool TryBuildLayers(Transform cardRoot, CardTileContent content, bool compact)
        {
            if (cardRoot == null || !ShouldUseComposition(content.CardId)) return false;

            Sprite portraitTile = LoadPortraitTile(content.CardId);
            Sprite frame = LoadFrame();
            if (portraitTile == null || frame == null) return false;

            int costSize = compact ? 16 : 18;
            int nameSize = compact ? 12 : 14;
            int classSize = compact ? 10 : 12;
            int statSize = compact ? 16 : 18;

            Image portrait = CreateImageLayer(cardRoot, "CardPortrait", portraitTile, preserveAspect: true);
            SetNormalizedRect(portrait.rectTransform, Layout.PortraitLeft, Layout.PortraitBottom, Layout.PortraitRight, Layout.PortraitTop);
            if (portrait.sprite == null && content.FallbackPortrait != null)
            {
                portrait.sprite = content.FallbackPortrait;
                portrait.color = Color.white;
            }

            Image frameImage = CreateImageLayer(cardRoot, "CardFrame", frame, preserveAspect: false);
            SetNormalizedRect(frameImage.rectTransform, Layout.FrameLeft, Layout.FrameBottom, Layout.FrameRight, Layout.FrameTop);

            Text name = CreateTextLayer(cardRoot, "Name", content.DisplayName, nameSize, TextAnchor.MiddleCenter);
            name.color = new Color(0.95f, 0.94f, 0.84f);
            SetNormalizedRect(name.rectTransform, Layout.NameLeft, Layout.NameBottom, Layout.NameRight, Layout.NameTop);

            Text classSchool = CreateTextLayer(cardRoot, "ClassSchool", content.ClassSchoolLine, classSize, TextAnchor.MiddleCenter);
            classSchool.color = new Color(0.78f, 0.82f, 0.88f);
            SetNormalizedRect(classSchool.rectTransform, Layout.ClassLeft, Layout.ClassBottom, Layout.ClassRight, Layout.ClassTop);

            Text cost = CreateTextLayer(cardRoot, "Cost", $"{content.Cost}", costSize, TextAnchor.MiddleCenter);
            cost.color = new Color(0.95f, 0.9f, 0.55f);
            cost.fontStyle = FontStyle.Bold;
            SetNormalizedRect(cost.rectTransform, Layout.CostLeft, Layout.CostBottom, Layout.CostRight, Layout.CostTop);

            Text atk = CreateTextLayer(cardRoot, "AtkStat", $"ATK {content.Attack}", statSize, TextAnchor.MiddleCenter);
            atk.color = new Color(0.9f, 0.95f, 0.9f);
            atk.fontStyle = FontStyle.Bold;
            SetNormalizedRect(atk.rectTransform, Layout.AtkLeft, Layout.AtkBottom, Layout.AtkRight, Layout.AtkTop);

            Text hp = CreateTextLayer(cardRoot, "HpStat", $"HP {content.Health}", statSize, TextAnchor.MiddleCenter);
            hp.color = new Color(0.9f, 0.95f, 0.9f);
            hp.fontStyle = FontStyle.Bold;
            SetNormalizedRect(hp.rectTransform, Layout.HpLeft, Layout.HpBottom, Layout.HpRight, Layout.HpTop);

            return true;
        }

        /// <summary>Expected child names in sibling order for EditMode hierarchy proofs.</summary>
        public static readonly string[] ExpectedLayerNames =
        {
            "CardPortrait",
            "CardFrame",
            "Name",
            "ClassSchool",
            "Cost",
            "AtkStat",
            "HpStat",
        };

        public static class Layout
        {
            public const float FrameLeft = 0f;
            public const float FrameBottom = 0f;
            public const float FrameRight = 1f;
            public const float FrameTop = 1f;

            public const float PortraitLeft = 0.06f;
            public const float PortraitBottom = 0.26f;
            public const float PortraitRight = 0.94f;
            public const float PortraitTop = 0.92f;

            public const float NameLeft = 0.08f;
            public const float NameBottom = 0.18f;
            public const float NameRight = 0.92f;
            public const float NameTop = 0.26f;

            public const float ClassLeft = 0.08f;
            public const float ClassBottom = 0.12f;
            public const float ClassRight = 0.92f;
            public const float ClassTop = 0.18f;

            public const float CostLeft = 0.06f;
            public const float CostBottom = 0.84f;
            public const float CostRight = 0.24f;
            public const float CostTop = 0.96f;

            public const float AtkLeft = 0.06f;
            public const float AtkBottom = 0.04f;
            public const float AtkRight = 0.48f;
            public const float AtkTop = 0.12f;

            public const float HpLeft = 0.52f;
            public const float HpBottom = 0.04f;
            public const float HpRight = 0.94f;
            public const float HpTop = 0.12f;
        }

        public static Vector2 DisplaySizeForDeck(bool compact) =>
            compact ? new Vector2(150f, 225f) : new Vector2(190f, 285f);

        public static Vector2 DisplaySizeForCollection() => new Vector2(210f, 315f);

        private static Sprite LoadSprite(string fileNameWithoutExtension)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExtension)) return null;
            return Resources.Load<Sprite>(ResourceRoot + fileNameWithoutExtension);
        }

        private static IReadOnlyList<string> BuildAuthoredCardIds()
        {
            const string prefix = "card_tile_art_";
            const string suffix = "_v1";
            Sprite[] sprites = Resources.LoadAll<Sprite>(ResourceRoot.TrimEnd('/'));
            var ids = new List<string>();
            for (int i = 0; i < sprites.Length; i++)
            {
                string spriteName = sprites[i] != null ? sprites[i].name : string.Empty;
                if (!spriteName.StartsWith(prefix, StringComparison.Ordinal)
                    || !spriteName.EndsWith(suffix, StringComparison.Ordinal))
                    continue;

                string cardId = spriteName.Substring(prefix.Length,
                    spriteName.Length - prefix.Length - suffix.Length);
                if (!string.IsNullOrEmpty(cardId)) ids.Add(cardId);
            }

            ids.Sort(StringComparer.Ordinal);
            return ids.AsReadOnly();
        }

        private static Image CreateImageLayer(Transform parent, string objectName, Sprite sprite, bool preserveAspect)
        {
            GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            Image image = obj.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = preserveAspect;
            image.color = sprite != null ? Color.white : Color.clear;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateTextLayer(Transform parent, string objectName, string content, int fontSize, TextAnchor alignment)
        {
            GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            Text text = obj.GetComponent<Text>();
            text.text = content ?? string.Empty;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        public static void SetNormalizedRect(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}

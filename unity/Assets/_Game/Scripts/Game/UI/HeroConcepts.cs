using System.Collections.Generic;
using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// The owner's concept paintings of each hero's base form, from Resources/HeroConcepts/{heroId}.png.
    /// Heroes without paintings use a baked portrait of their own model where available;
    /// procedural fallback remains only until that portrait has been authored/baked.
    /// </summary>
    public static class HeroConcepts
    {
        /// <summary>A card shows head to hips: this share of the painting's width, and a little taller than wide.</summary>
        public const float CardWidthShare = 0.72f;
        public const float CardAspect = 1.15f;
        private const float TopMargin = 0.02f;

        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Sprite> Cards = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Sprite> Fulls = new Dictionary<string, Sprite>();

        // Name convention: {id}.png (base), {id}_evo_1.png, {id}_evo_2.png, {id}_evo_3.png.
        // Missing evolved art falls back to the original painted base form; never synthesize an unrelated character.
        private static string FormKey(string heroId, int evolution) =>
            evolution <= 0 ? heroId : heroId + "_evo_" + Mathf.Clamp(evolution, 1, 3);

        public static bool HasForm(string heroId, int evolution) =>
            Texture(FormKey(heroId, evolution)) != null;

        public static int AvailableForm(string heroId, int evolution) =>
            evolution > 0 && HasForm(heroId, evolution) ? evolution : 0;

        private static Texture2D Texture(string heroId)
        {
            if (!Textures.TryGetValue(heroId, out var texture) || texture == null)
            {
                texture = Resources.Load<Texture2D>("HeroConcepts/" + heroId);
                // Authored concept art takes precedence. If an original painting is not
                // available, use a baked portrait of this hero's own existing 3D prefab.
                // The Editor baker writes these to Resources/HeroPortraits/{id}.png.
                // Evolution-specific paintings always retain priority over the base bake.
                if (texture == null && !heroId.Contains("_evo_"))
                    texture = Resources.Load<Texture2D>("HeroPortraits/" + heroId);
                Textures[heroId] = texture;
            }
            return texture;
        }

        public static bool Has(string heroId) => Texture(heroId) != null;

        /// <summary>
        /// Crops head-and-shoulder card paintings while preserving the head in
        /// square portrait images. Tall original paintings keep the legacy 72%
        /// width crop; square images use 82% for less aggressive clipping.
        /// The rectangle never extends outside the source texture.
        /// </summary>
        public static Rect CardCropRect(int textureWidth, int textureHeight)
        {
            if (textureWidth <= 0 || textureHeight <= 0) return Rect.zero;
            var squareLike = textureWidth >= textureHeight * 0.9f;
            var width = textureWidth * (squareLike ? 0.82f : CardWidthShare);
            var usableHeight = textureHeight * (1f - TopMargin);
            var height = Mathf.Min(width * CardAspect, usableHeight);
            return new Rect((textureWidth - width) * 0.5f,
                usableHeight - height, width, height);
        }

        /// <summary>The upper body, for hero cards in lists.</summary>
        public static Sprite Card(string heroId) => Card(heroId, 0);

        public static Sprite Card(string heroId, int evolution)
        {
            heroId = FormKey(heroId, evolution);
            if (Cards.TryGetValue(heroId, out var sprite) && sprite != null) return sprite;
            var texture = Texture(heroId);
            if (texture == null) return null;
            var rect = CardCropRect(texture.width, texture.height);
            sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0f));
            sprite.name = heroId + "_card";
            Cards[heroId] = sprite;
            return sprite;
        }

        /// <summary>The whole painting, for the large hero view.</summary>
        public static Sprite Full(string heroId) => Full(heroId, 0);

        public static Sprite Full(string heroId, int evolution)
        {
            heroId = FormKey(heroId, evolution);
            if (Fulls.TryGetValue(heroId, out var sprite) && sprite != null) return sprite;
            var texture = Texture(heroId);
            if (texture == null) return null;
            sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0f));
            sprite.name = heroId + "_full";
            Fulls[heroId] = sprite;
            return sprite;
        }
    }
}

using System.Collections.Generic;
using ShinobiDominion.Core;
using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// Painted-looking backgrounds, frames and panels for the hero screens, all made in code.
    /// Each is drawn once and cached.
    /// </summary>
    public static class HeroArt
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        private static Texture2D NewTexture(int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        }

        private static Sprite Make(string key, Texture2D texture, Vector4 border = default)
        {
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        private static bool Cached(string key, out Sprite sprite) => Cache.TryGetValue(key, out sprite) && sprite != null;

        /// <summary>Card colours by rarity: inner glow, outer edge, and the frame's metal.</summary>
        public static (Color glow, Color edge, Color metal) Palette(HeroRarity rarity)
        {
            switch (rarity)
            {
                case HeroRarity.Legendary: return (new Color(1f, 0.78f, 0.36f), new Color(0.62f, 0.16f, 0.06f), new Color(0.95f, 0.72f, 0.28f));
                case HeroRarity.Elite: return (new Color(0.86f, 0.55f, 1f), new Color(0.24f, 0.10f, 0.42f), new Color(0.78f, 0.66f, 0.95f));
                default: return (new Color(0.62f, 0.86f, 0.86f), new Color(0.12f, 0.24f, 0.30f), new Color(0.70f, 0.74f, 0.78f));
            }
        }

        /// <summary>A sunburst behind a hero on a card: bright near the head, deep at the edges, with soft rays.</summary>
        public static Sprite CardBackdrop(HeroRarity rarity)
        {
            var key = "cardback" + rarity;
            if (Cached(key, out var cached)) return cached;
            var (glow, edge, _) = Palette(rarity);
            const int width = 128;
            const int height = 176;
            var texture = NewTexture(width, height);
            var center = new Vector2(width * 0.5f, height * 0.66f);
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var offset = new Vector2(x, y) - center;
                var distance = offset.magnitude / (height * 0.75f);
                var angle = Mathf.Atan2(offset.y, offset.x);
                var rays = 0.5f + 0.5f * Mathf.Sin(angle * 14f);
                var t = Mathf.Clamp01(distance - rays * 0.06f);
                var color = Color.Lerp(glow, edge, Mathf.SmoothStep(0f, 1f, t));
                texture.SetPixel(x, y, color);
            }
            texture.Apply();
            return Make(key, texture);
        }

        /// <summary>
        /// A heavy metal frame with an empty middle, nine-sliced so it fits any card. A dark outline, a
        /// bevelled band lit from above, and a thin dark line on the inside edge.
        /// </summary>
        public static Sprite CardFrame(HeroRarity rarity)
        {
            var key = "cardframe" + rarity;
            if (Cached(key, out var cached)) return cached;
            var (_, _, metal) = Palette(rarity);
            const int size = 96;
            const float radius = 12f;
            const float outline = 2.5f;
            const float band = 9f;
            const float inner = 2.5f;
            var texture = NewTexture(size, size);
            var ink = new Color(0.10f, 0.06f, 0.04f, 1f);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var depth = -RoundedDistance(x + 0.5f, y + 0.5f, size, size, radius);
                Color color;
                if (depth < 0f) color = Color.clear;
                else if (depth < outline) color = ink;
                else if (depth < outline + band)
                {
                    // Bevel: light on the outer half, shade on the inner half, and brighter towards the top.
                    var across = (depth - outline) / band;
                    var height = (float)y / (size - 1);
                    var light = Mathf.Lerp(1.25f, 0.7f, across) * Mathf.Lerp(0.82f, 1.12f, height);
                    color = new Color(metal.r * light, metal.g * light, metal.b * light, 1f);
                }
                else if (depth < outline + band + inner) color = ink;
                else color = Color.clear;
                color.a *= Mathf.Clamp01(depth + 0.5f);
                texture.SetPixel(x, y, color);
            }
            texture.Apply();
            var slice = Mathf.Ceil(radius + outline + band + inner + 2f);
            return Make(key, texture, new Vector4(slice, slice, slice, slice));
        }

        /// <summary>Clear at the top, dark at the bottom: lets a name sit on top of a painting.</summary>
        public static Sprite Shade
        {
            get
            {
                const string key = "shade";
                if (Cached(key, out var cached)) return cached;
                var texture = NewTexture(4, 64);
                for (var y = 0; y < 64; y++)
                for (var x = 0; x < 4; x++)
                    texture.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.SmoothStep(0.85f, 0f, y / 63f)));
                texture.Apply();
                return Make(key, texture);
            }
        }

        /// <summary>A soft round glow, white, to be tinted.</summary>
        public static Sprite Glow
        {
            get
            {
                const string key = "glow";
                if (Cached(key, out var cached)) return cached;
                const int size = 128;
                var texture = NewTexture(size, size);
                var center = (size - 1) * 0.5f;
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / center;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), 1.8f)));
                }
                texture.Apply();
                return Make(key, texture);
            }
        }

        /// <summary>A scroll-like panel: warm paper with a darker rim and a brown frame. Nine-sliced.</summary>
        public static Sprite Parchment
        {
            get
            {
                const string key = "parchment";
                if (Cached(key, out var cached)) return cached;
                const int size = 128;
                const float radius = 10f;
                var texture = NewTexture(size, size);
                var paper = new Color(0.93f, 0.85f, 0.68f);
                var rim = new Color(0.78f, 0.64f, 0.44f);
                var wood = new Color(0.45f, 0.28f, 0.15f);
                var ink = new Color(0.16f, 0.09f, 0.05f);
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var depth = -RoundedDistance(x + 0.5f, y + 0.5f, size, size, radius);
                    Color color;
                    if (depth < 0f) color = Color.clear;
                    else if (depth < 2f) color = ink;
                    else if (depth < 9f) color = wood * Mathf.Lerp(1.2f, 0.85f, (depth - 2f) / 7f);
                    else if (depth < 11f) color = ink;
                    else color = Color.Lerp(rim, paper, Mathf.Clamp01((depth - 11f) / 14f));
                    color.a = Mathf.Clamp01(depth + 0.5f);
                    texture.SetPixel(x, y, color);
                }
                texture.Apply();
                return Make(key, texture, new Vector4(30f, 30f, 30f, 30f));
            }
        }

        /// <summary>A red ribbon with notched ends, for a hero's name.</summary>
        public static Sprite Ribbon
        {
            get
            {
                const string key = "ribbon";
                if (Cached(key, out var cached)) return cached;
                const int width = 256;
                const int height = 48;
                var texture = NewTexture(width, height);
                var red = new Color(0.66f, 0.13f, 0.08f);
                for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    var v = (float)y / (height - 1);
                    var fromEnd = Mathf.Min(x, width - 1 - x);
                    // A notch cut into each end, and the ribbon fading as it reaches the tips.
                    var notch = Mathf.Abs(v - 0.5f) * 2f * 22f;
                    var inside = fromEnd > 22f - notch * 0.9f;
                    var fade = Mathf.Clamp01(fromEnd / 60f);
                    var shade = Mathf.Lerp(0.72f, 1.18f, v);
                    var edge = v < 0.08f || v > 0.92f ? new Color(0.95f, 0.72f, 0.3f) : red * shade;
                    edge.a = inside ? Mathf.Lerp(0.0f, 1f, fade) : 0f;
                    texture.SetPixel(x, y, edge);
                }
                texture.Apply();
                return Make(key, texture);
            }
        }

        /// <summary>
        /// A painted stage for the hero page: a dusk sky in the hero's colour, layered mountains, and a
        /// stone floor. Each affinity gets its own sky.
        /// </summary>
        public static Sprite Stage(Color sky)
        {
            var key = "stage" + ColorUtility.ToHtmlStringRGB(sky);
            if (Cached(key, out var cached)) return cached;
            const int width = 480;
            const int height = 270;
            var texture = NewTexture(width, height);
            var horizon = height * 0.36f;
            var top = Color.Lerp(sky, Color.black, 0.55f);
            var low = Color.Lerp(sky, Color.white, 0.45f);
            var far = Color.Lerp(sky, new Color(0.35f, 0.38f, 0.5f), 0.5f);
            var near = Color.Lerp(sky, new Color(0.12f, 0.12f, 0.2f), 0.7f);
            var stone = new Color(0.42f, 0.40f, 0.46f);
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                Color color;
                if (y > horizon)
                {
                    var v = (y - horizon) / (height - horizon);
                    color = Color.Lerp(low, top, Mathf.Pow(v, 0.8f));
                    var farRidge = horizon + 40f + Mathf.PerlinNoise(x * 0.012f, 3.1f) * 70f;
                    var nearRidge = horizon + 10f + Mathf.PerlinNoise(x * 0.02f + 9f, 1.7f) * 50f;
                    if (y < farRidge) color = Color.Lerp(far, low, 0.35f * (1f - (farRidge - y) / 80f));
                    if (y < nearRidge) color = near;
                }
                else
                {
                    // Flagstones in perspective, lit from the middle.
                    var depth = 1f - y / horizon;
                    var spread = (x - width * 0.5f) / (width * 0.5f);
                    var rowLine = Mathf.Abs(Mathf.Sin(Mathf.Pow(depth + 0.15f, 1.6f) * 22f)) < 0.07f;
                    var colLine = Mathf.Abs(Mathf.Sin(spread / (0.3f + depth) * 6f)) < 0.05f;
                    var light = Mathf.Lerp(1.15f, 0.55f, Mathf.Abs(spread) * 0.8f + depth * 0.35f);
                    color = stone * light;
                    if (rowLine || colLine) color *= 0.72f;
                    color = Color.Lerp(color, sky * 0.6f, 0.15f);
                }
                color.a = 1f;
                texture.SetPixel(x, y, color);
            }
            texture.Apply();
            return Make(key, texture);
        }

        /// <summary>A night forest clearing with a glowing ring, for the Summoning Gate.</summary>
        public static Sprite GateScene
        {
            get
            {
                const string key = "gatescene";
                if (Cached(key, out var cached)) return cached;
                const int width = 480;
                const int height = 270;
                var texture = NewTexture(width, height);
                var deep = new Color(0.03f, 0.09f, 0.10f);
                var mid = new Color(0.09f, 0.26f, 0.22f);
                var moss = new Color(0.20f, 0.42f, 0.22f);
                var ring = new Vector2(width * 0.5f, height * 0.58f);
                for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    var v = (float)y / (height - 1);
                    var color = Color.Lerp(mid, deep, v);
                    // Trunks and leaves as dark noisy bands near the sides.
                    var side = Mathf.Abs(x - width * 0.5f) / (width * 0.5f);
                    var leaves = Mathf.PerlinNoise(x * 0.03f, y * 0.03f);
                    if (leaves * side > 0.38f) color = Color.Lerp(color, deep, 0.7f);
                    if (y < height * 0.25f)
                    {
                        var ground = Mathf.PerlinNoise(x * 0.05f, y * 0.08f + 4f);
                        color = Color.Lerp(moss * 0.6f, moss, ground) * Mathf.Lerp(0.6f, 1f, 1f - side);
                    }
                    // The gate: a bright ring with a soft glow inside it.
                    var d = Vector2.Distance(new Vector2(x, y * 1.15f), new Vector2(ring.x, ring.y * 1.15f));
                    var ringLight = Mathf.Exp(-Mathf.Pow((d - 92f) / 5f, 2f));
                    var inner = Mathf.Clamp01(1f - d / 92f) * 0.35f;
                    color = Color.Lerp(color, new Color(0.45f, 0.95f, 1f), Mathf.Clamp01(ringLight + inner));
                    // Fireflies.
                    // Fireflies: a few scattered points, not a sprinkle over everything.
                    if (y > height * 0.25f && ((x * 73856093) ^ (y * 19349663)) % 613 == 0) color = Color.Lerp(color, new Color(0.9f, 1f, 0.6f), 0.85f);
                    color.a = 1f;
                    texture.SetPixel(x, y, color);
                }
                texture.Apply();
                return Make(key, texture);
            }
        }

        private static float RoundedDistance(float x, float y, int width, int height, float radius)
        {
            var qx = Mathf.Abs(x - width * 0.5f) - (width * 0.5f - radius);
            var qy = Mathf.Abs(y - height * 0.5f) - (height * 0.5f - radius);
            var outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }
    }
}

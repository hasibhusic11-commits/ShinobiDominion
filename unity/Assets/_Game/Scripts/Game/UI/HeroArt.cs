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

        /// <summary>
        /// The hero page's shrine hall: dark lacquered wood walls lit warm from the middle, two crimson pillars with
        /// gold bands framing the hero, a large sanctum circle glowing in the hero's affinity colour behind them, a
        /// dark beam with a gold rule along the top, and a polished plank floor reflecting the glow. Painted once per
        /// colour. <paramref name="centreX"/> is where the hero stands, as a share of the width.
        /// </summary>
        public static Sprite Shrine(Color aura, float centreX)
        {
            var key = "shrine" + ColorUtility.ToHtmlStringRGB(aura) + centreX.ToString("0.00");
            if (Cached(key, out var cached)) return cached;
            const int width = 960;
            const int height = 540;
            const float aspect = width / (float)height;
            const float floorLine = 0.27f;
            var texture = NewTexture(width, height);
            var pixels = new Color32[width * height];
            var wallLow = new Color(0.20f, 0.12f, 0.09f);
            var wallTop = new Color(0.05f, 0.04f, 0.08f);
            var gold = new Color(0.93f, 0.73f, 0.33f);
            var crimson = new Color(0.62f, 0.10f, 0.09f);
            var warm = new Color(1f, 0.72f, 0.38f);
            var ringCentre = new Vector2(centreX, 0.6f);
            const float ring = 0.25f;
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var u = (x + 0.5f) / width;
                var v = (y + 0.5f) / height;
                Color c;
                if (v > floorLine)
                {
                    var t = (v - floorLine) / (1f - floorLine);
                    c = Color.Lerp(wallLow, wallTop, Mathf.Pow(t, 0.8f));
                    // Lacquered wall panels and a carved rail.
                    var panel = Mathf.Repeat((u - centreX) * 16f, 1f);
                    if (panel < 0.035f) c *= 0.72f;
                    else if (panel < 0.07f) c *= 1.12f;
                    if (Mathf.Abs(v - 0.47f) < 0.006f) c = Color.Lerp(c, gold * 0.55f, 0.8f);
                    // Warm light pooled behind the hero.
                    var dl = new Vector2((u - centreX) * aspect, (v - 0.45f) * 1.2f).sqrMagnitude;
                    c += warm * (0.42f * Mathf.Exp(-dl / 0.45f));
                    // The sanctum circle: deep affinity colour inside, gold rings, faint spokes, soft outer glow.
                    var d = new Vector2((u - ringCentre.x) * aspect, v - ringCentre.y);
                    var r = d.magnitude;
                    if (r < ring)
                    {
                        var inner = Color.Lerp(aura * 0.55f, aura * 0.25f + new Color(0.02f, 0.02f, 0.05f), r / ring);
                        c = Color.Lerp(c, inner, 0.85f);
                        var angle = Mathf.Atan2(d.y, d.x);
                        if (r > ring * 0.62f && r < ring * 0.9f && Mathf.Abs(Mathf.Sin(angle * 6f)) < 0.025f) c = Color.Lerp(c, gold, 0.45f);
                    }
                    foreach (var line in new[] { 1f, 0.92f, 0.62f })
                        if (Mathf.Abs(r - ring * line) < 0.0042f) c = Color.Lerp(c, gold, line == 1f ? 0.95f : 0.6f);
                    c += aura * (0.35f * Mathf.Exp(-Mathf.Pow(Mathf.Max(r - ring, 0f) / 0.07f, 2f)) * (r > ring ? 1f : 0f));
                    // Two crimson pillars with gold bands, round-shaded.
                    foreach (var side in new[] { -1f, 1f })
                    {
                        var px = centreX + side * 0.205f;
                        var half = 0.024f;
                        var k = (u - px) / half;
                        if (Mathf.Abs(k) < 1f)
                        {
                            var shade = Mathf.Lerp(1.15f, 0.45f, (k + 1f) * 0.5f) * Mathf.Sqrt(1f - k * k + 0.05f);
                            var col = crimson * shade;
                            if (Mathf.Abs(v - 0.33f) < 0.012f || Mathf.Abs(v - 0.87f) < 0.012f || Mathf.Abs(v - 0.36f) < 0.004f) col = gold * shade * 1.05f;
                            c = col;
                        }
                    }
                    // The top beam.
                    if (v > 0.93f) c = Color.Lerp(new Color(0.08f, 0.05f, 0.05f), new Color(0.15f, 0.08f, 0.06f), (v - 0.93f) / 0.07f);
                    if (Mathf.Abs(v - 0.93f) < 0.004f) c = gold * 0.9f;
                }
                else
                {
                    // Polished planks running to the back, the sanctum glow reflected in them.
                    var depth = (floorLine - v) / floorLine;
                    var spread = (u - centreX) / (0.12f + depth * 0.9f);
                    var seam = Mathf.Abs(Mathf.Sin(spread * 9f)) < 0.06f;
                    c = Color.Lerp(new Color(0.24f, 0.13f, 0.08f), new Color(0.13f, 0.07f, 0.05f), depth);
                    if (seam) c *= 0.7f;
                    var reflect = Mathf.Exp(-((u - centreX) * (u - centreX) * 9f + depth * depth * 5f));
                    c += Color.Lerp(warm, aura, 0.5f) * (0.35f * reflect);
                    if (Mathf.Abs(v - floorLine) < 0.003f) c = gold * 0.5f;
                }
                // Vignette keeps the eye on the middle.
                var vx = (u - centreX) * 1.3f;
                var vy = v - 0.5f;
                c *= Mathf.Clamp01(1.08f - 0.9f * (vx * vx + vy * vy * 0.8f));
                c.a = 1f;
                pixels[y * width + x] = c;
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Make(key, texture);
        }

        /// <summary>A five-point star, white with a soft edge, for tinting (the evolution stars).</summary>
        public static Sprite Star
        {
            get
            {
                const string key = "star5";
                if (Cached(key, out var cached)) return cached;
                const int size = 128;
                var texture = NewTexture(size, size);
                var pixels = new Color32[size * size];
                var centre = (size - 1) * 0.5f;
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - centre) / centre;
                    var dy = (y - centre) / centre;
                    var r = Mathf.Sqrt(dx * dx + dy * dy);
                    var a = Mathf.Atan2(dx, dy);
                    // Radius of the star outline at this angle: five points, inner radius 0.45.
                    var sector = Mathf.Repeat(a, 2f * Mathf.PI / 5f) / (2f * Mathf.PI / 5f);
                    var tip = Mathf.Abs(sector - 0.5f) * 2f;
                    var edge = Mathf.Lerp(0.45f, 0.95f, tip * tip);
                    var alpha = Mathf.Clamp01((edge - r) * 22f);
                    var light = Mathf.Lerp(1f, 0.78f, Mathf.Clamp01(r / edge));
                    pixels[y * size + x] = new Color(light, light, light, alpha);
                }
                texture.SetPixels32(pixels);
                texture.Apply();
                return Make(key, texture);
            }
        }
    }
}

using System.Collections.Generic;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// The owner's painted building art (Resources/Buildings, imported by tools/art/import_art_packs.py), one
    /// picture per art level. The game keeps its 15 levels; the 10 pictures spread across them, so level 1 shows
    /// picture 1 and level 15 shows picture 10. Art only: no rule reads any of this.
    /// </summary>
    public static class BuildingSprites
    {
        public const int ArtLevels = 10;
        public const int GameLevels = 15;

        /// <summary>A vertical picture under the 30 degree camera looks squashed by cos(30); this undoes it.</summary>
        public const float Lift = 1.1547005f;

        public static readonly Quaternion Upright = Quaternion.Euler(0f, 45f, 0f);

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static int ArtLevel(int level) =>
            Mathf.Clamp(1 + Mathf.RoundToInt((Mathf.Clamp(level, 1, GameLevels) - 1) * (ArtLevels - 1) / (float)(GameLevels - 1)), 1, ArtLevels);

        public static Sprite For(string id, int level) => Load($"{id}_{ArtLevel(level):00}");

        public static bool Has(string id) => For(id, 1) != null;

        private static Dictionary<string, float> _baseWidths;

        /// <summary>
        /// Width in world units of the picture's base diamond. Art made to the framing in docs/art/tower_wall_prompts.md
        /// lists it in Resources/Buildings/art_meta.json (pixels), so weapons or lanterns may overhang the footprint.
        /// Other art fills its picture with the base, so the picture's width is used.
        /// </summary>
        /// <summary>A number from art_meta.json (pixels), or 0 when the picture is not listed.</summary>
        public static float MetaPixels(string name)
        {
            if (_baseWidths == null) BaseWidth(null);
            return _baseWidths.TryGetValue(name, out var pixels) ? pixels : 0f;
        }

        public static float BaseWidth(Sprite sprite)
        {
            if (_baseWidths == null)
            {
                _baseWidths = new Dictionary<string, float>();
                var meta = Resources.Load<TextAsset>("Buildings/art_meta");
                if (meta != null)
                    foreach (var pair in Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, float>>(meta.text))
                        _baseWidths[pair.Key] = pair.Value;
            }
            if (sprite == null) return 0f;
            return _baseWidths.TryGetValue(sprite.name, out var pixels) ? pixels / sprite.pixelsPerUnit : sprite.bounds.size.x;
        }

        public static Sprite Load(string name)
        {
            if (Cache.TryGetValue(name, out var sprite)) return sprite;
            sprite = Resources.Load<Sprite>("Buildings/" + name);
            Cache[name] = sprite;
            return sprite;
        }

        /// <summary>
        /// Painter's order for the iso view: things nearer the camera (smaller x + z) draw later. Sprites do not
        /// write depth, so this order decides which one covers the other.
        /// </summary>
        public static int Order(Vector3 ground) => Mathf.RoundToInt(-(ground.x + ground.z) * 20f);

        /// <summary>An upright sprite facing the camera, standing on <paramref name="foot"/> (world), sized to a screen width.</summary>
        public static SpriteRenderer Stand(Transform parent, string name, Sprite sprite, Vector3 foot, float screenWidth, int order, Color tint)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(foot, Upright);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = tint;
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var scale = screenWidth / Mathf.Max(sprite.bounds.size.x, 0.001f);
            go.transform.localScale = new Vector3(scale, scale * Lift, 1f);
            return renderer;
        }

        /// <summary>
        /// Puts a building picture on its footprint. The picture's bottom is the footprint's front corner (the
        /// one nearest the camera) and its width is the footprint's width on screen, (w + d) / sqrt 2.
        /// </summary>
        /// <summary>Draws before every standing picture, so shadows always lie under them.</summary>
        public const int ShadowOrder = -32000;

        private static Sprite _shadow;

        /// <summary>A soft rounded square, dark in the middle and fading out, built once in code.</summary>
        public static Sprite ShadowSprite
        {
            get
            {
                if (_shadow != null) return _shadow;
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "GroundShadow" };
                var pixels = new Color32[size * size];
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var dx = Mathf.Abs((x + 0.5f) / size * 2f - 1f);
                    var dy = Mathf.Abs((y + 0.5f) / size * 2f - 1f);
                    var d = Mathf.Pow(Mathf.Pow(dx, 4f) + Mathf.Pow(dy, 4f), 0.25f);
                    var a = Mathf.Clamp01((1f - d) / 0.35f);
                    pixels[y * size + x] = new Color32(10, 14, 6, (byte)(a * a * 255f));
                }
                texture.SetPixels32(pixels);
                texture.Apply(true, true);
                _shadow = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                return _shadow;
            }
        }

        /// <summary>
        /// A shadow lying on the ground, a little larger than the footprint and pushed towards the lower right,
        /// so the building looks planted instead of pasted on. Turns with its parent, like the footprint does.
        /// </summary>
        public static SpriteRenderer GroundShadow(Transform parent, float width, float depth, float alpha)
        {
            var go = new GameObject("GroundShadow");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0.22f, 0.015f, -0.12f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(width * 1.12f, depth * 1.12f, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = ShadowSprite;
            renderer.color = new Color(1f, 1f, 1f, alpha);
            renderer.sortingOrder = ShadowOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        public static IsoStand Attach(Transform root, BuildingDefinition definition, int level, Color tint)
        {
            var sprite = For(definition.id, level);
            if (sprite == null) return null;
            GroundShadow(root, definition.width, definition.depth, tint.a < 1f ? 0.25f : 0.55f);
            var renderer = Stand(root, "Art", sprite, root.position, 1f, 0, tint);
            var stand = renderer.gameObject.AddComponent<IsoStand>();
            stand.Init(definition.width, definition.depth, renderer);
            return stand;
        }
    }

    /// <summary>
    /// Keeps a building picture upright and on the footprint's front corner while its parent moves or turns.
    /// A turned building swaps width and depth; the picture itself never turns.
    /// </summary>
    public sealed class IsoStand : MonoBehaviour
    {
        private int _width;
        private int _depth;
        private SpriteRenderer _renderer;

        public SpriteRenderer Renderer => _renderer;

        /// <summary>Draws over everything else, for the placement ghost.</summary>
        public bool OnTop;

        /// <summary>Squash and stretch on top of the normal size (Feel.Pop). One when at rest.</summary>
        public Vector2 Juice = Vector2.one;

        /// <summary>Draws the picture as if the footprint were this share of its size (the Hero Base's small pedestal).</summary>
        public float ArtSize = 1f;

        /// <summary>Moves that smaller picture within the footprint, in world units; the art never turns.</summary>
        public Vector3 ArtOffset;

        /// <summary>Height of the picture in world units, for tap bounds.</summary>
        public float WorldHeight => _renderer.sprite.bounds.size.y * transform.lossyScale.y;

        public void Init(int width, int depth, SpriteRenderer renderer)
        {
            _width = width;
            _depth = depth;
            _renderer = renderer;
            Place();
        }

        public Vector2 Footprint()
        {
            var yaw = transform.parent != null ? Mathf.Repeat(transform.parent.eulerAngles.y, 180f) : 0f;
            var turned = Mathf.Abs(yaw - 90f) < 45f;
            return turned ? new Vector2(_depth, _width) : new Vector2(_width, _depth);
        }

        public void Place()
        {
            var parent = transform.parent;
            if (parent == null) return;
            var size = Footprint() * GridSpace.CellSize * ArtSize;
            var half = (size.x + size.y) * 0.25f;
            var centre = parent.position + ArtOffset;
            transform.SetPositionAndRotation(centre + new Vector3(-half, 0f, -half), BuildingSprites.Upright);
            var screenWidth = (size.x + size.y) * 0.70710678f;
            var scale = screenWidth / Mathf.Max(BuildingSprites.BaseWidth(_renderer.sprite), 0.001f);
            var parentScale = parent.lossyScale;
            transform.localScale = new Vector3(scale * Juice.x / parentScale.x, scale * BuildingSprites.Lift * Juice.y / parentScale.y, 1f / parentScale.z);
            _renderer.sortingOrder = OnTop ? short.MaxValue : BuildingSprites.Order(centre);
        }

        private void LateUpdate() => Place();
    }
}

namespace ShinobiDominion.Game
{
    /// <summary>A slow up-and-down float, for markers over buildings.</summary>
    public sealed class Bob : UnityEngine.MonoBehaviour
    {
        private UnityEngine.Vector3 _start;
        private float _phase;

        private void Start()
        {
            _start = transform.localPosition;
            _phase = UnityEngine.Random.value * 6.28f;
        }

        private void Update()
        {
            transform.localPosition = _start + UnityEngine.Vector3.up * (UnityEngine.Mathf.Sin(UnityEngine.Time.time * 2.4f + _phase) * 0.08f);
        }
    }
}

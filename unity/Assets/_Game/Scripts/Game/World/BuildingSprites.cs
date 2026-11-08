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

        /// <summary>A vertical picture under the 35.264 degree camera looks squashed by cos(35.264); this undoes it.</summary>
        public const float Lift = 1.2247449f;

        public static readonly Quaternion Upright = Quaternion.Euler(0f, 45f, 0f);

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static int ArtLevel(int level) =>
            Mathf.Clamp(1 + Mathf.RoundToInt((Mathf.Clamp(level, 1, GameLevels) - 1) * (ArtLevels - 1) / (float)(GameLevels - 1)), 1, ArtLevels);

        public static Sprite For(string id, int level) => Load($"{id}_{ArtLevel(level):00}");

        public static bool Has(string id) => For(id, 1) != null;

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
        public static IsoStand Attach(Transform root, BuildingDefinition definition, int level, Color tint)
        {
            var sprite = For(definition.id, level);
            if (sprite == null) return null;
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
            var size = Footprint() * GridSpace.CellSize;
            var half = (size.x + size.y) * 0.25f;
            transform.SetPositionAndRotation(parent.position + new Vector3(-half, 0f, -half), BuildingSprites.Upright);
            var screenWidth = (size.x + size.y) * 0.70710678f;
            var scale = screenWidth / Mathf.Max(_renderer.sprite.bounds.size.x, 0.001f);
            var parentScale = parent.lossyScale;
            transform.localScale = new Vector3(scale / parentScale.x, scale * BuildingSprites.Lift / parentScale.y, 1f / parentScale.z);
            _renderer.sortingOrder = OnTop ? short.MaxValue : BuildingSprites.Order(parent.position);
        }

        private void LateUpdate() => Place();
    }
}

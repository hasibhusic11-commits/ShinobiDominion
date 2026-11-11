using System.Collections.Generic;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShinobiDominion.Game
{
    /// <summary>Construction-mode grid: one line mesh, not 2,500 objects.</summary>
    public sealed class GridOverlay
    {
        public readonly GameObject Root;

        public GridOverlay(Transform parent)
        {
            float size = VillageGrid.Size * GridSpace.CellSize;
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            for (var i = 0; i <= VillageGrid.Size; i++)
            {
                var t = i * GridSpace.CellSize;
                indices.Add(vertices.Count);
                vertices.Add(new Vector3(t, 0.02f, 0));
                indices.Add(vertices.Count);
                vertices.Add(new Vector3(t, 0.02f, size));
                indices.Add(vertices.Count);
                vertices.Add(new Vector3(0, 0.02f, t));
                indices.Add(vertices.Count);
                vertices.Add(new Vector3(size, 0.02f, t));
            }

            var mesh = new Mesh { name = "GridOverlay" };
            mesh.SetVertices(vertices);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();

            Root = new GameObject("GridOverlay");
            Root.transform.SetParent(parent, false);
            Root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = Root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Mats.Overlay(new Color(1f, 1f, 1f, 0.22f));
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            Root.SetActive(false);
        }

        public bool Visible
        {
            get => Root.activeSelf;
            set => Root.SetActive(value);
        }
    }

    /// <summary>
    /// Cell feedback in one mesh. Invalid cells get an X as well as red, so the state
    /// never depends on colour alone.
    /// </summary>
    public sealed class CellHighlight
    {
        private readonly GameObject _root;
        private readonly Mesh _mesh;
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<int> _good = new List<int>();
        private readonly List<int> _bad = new List<int>();
        private readonly List<int> _marks = new List<int>();

        public CellHighlight(Transform parent, string name, Color goodColor, Color badColor, float height)
        {
            _height = height;
            _mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            _mesh.MarkDynamic();
            _root = new GameObject(name);
            _root.transform.SetParent(parent, false);
            _root.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = _root.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { Mats.OnTop(goodColor), Mats.OnTop(badColor), Mats.OnTop(new Color(1f, 1f, 1f, 0.95f)) };
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            _root.SetActive(false);
        }

        private readonly float _height;

        public void Clear()
        {
            _root.SetActive(false);
        }

        public void Show(IEnumerable<KeyValuePair<GridCoord, bool>> cells, bool marks = true)
        {
            _vertices.Clear();
            _good.Clear();
            _bad.Clear();
            _marks.Clear();
            foreach (var cell in cells)
            {
                var c = GridSpace.CellCenter(cell.Key);
                c.y = _height;
                if (cell.Value)
                {
                    AddQuad(c, 0.46f, 0.46f, 0f, _good);
                }
                else
                {
                    AddQuad(c, 0.46f, 0.46f, 0f, _bad);
                    if (!marks) continue;
                    AddQuad(c + Vector3.up * 0.005f, 0.5f, 0.06f, 45f, _marks);
                    AddQuad(c + Vector3.up * 0.005f, 0.5f, 0.06f, -45f, _marks);
                }
            }

            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.subMeshCount = 3;
            _mesh.SetTriangles(_good, 0);
            _mesh.SetTriangles(_bad, 1);
            _mesh.SetTriangles(_marks, 2);
            _mesh.RecalculateBounds();
            _root.SetActive(true);
        }

        private void AddQuad(Vector3 center, float halfLength, float halfWidth, float degrees, List<int> triangles)
        {
            var rotation = Quaternion.Euler(0f, degrees, 0f);
            var start = _vertices.Count;
            _vertices.Add(center + rotation * new Vector3(-halfLength, 0, -halfWidth));
            _vertices.Add(center + rotation * new Vector3(-halfLength, 0, halfWidth));
            _vertices.Add(center + rotation * new Vector3(halfLength, 0, halfWidth));
            _vertices.Add(center + rotation * new Vector3(halfLength, 0, -halfWidth));
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }
    }

    /// <summary>
    /// Draws the walls from the owner's painted pieces (Resources/Buildings, imported by
    /// tools/art/import_buildings_v2.py). Each segment is one tile long with a post at both ends, anchored on
    /// its front post: a wall draws the segments that leave it eastwards and northwards, so straights, corners,
    /// ends and junctions all join up. A wall with no neighbours stands as a lone post.
    /// </summary>
    public sealed class WallView
    {
        /// <summary>Half the post's width: its front corner sits this far towards the camera from the cell centre.</summary>
        private const float PostHalf = 0.17f;

        private readonly Transform _root;
        private readonly List<SpriteRenderer> _pieces = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _shadows = new List<SpriteRenderer>();
        private int _used;
        private int _shadowsUsed;

        public WallView(Transform parent)
        {
            _root = new GameObject("Walls").transform;
            _root.SetParent(parent, false);
        }

        public void SetVisible(bool visible) => _root.gameObject.SetActive(visible);

        public void Rebuild(Village village, int hiddenWallId, HashSet<int> gone = null)
        {
            _used = 0;
            _shadowsUsed = 0;
            foreach (var wall in village.Buildings)
            {
                if (!wall.Definition.IsWall || wall.Id == hiddenWallId) continue;
                if (gone != null && gone.Contains(wall.Id)) continue;
                var mask = ConnectionsExcluding(village, wall.Origin, hiddenWallId);
                if (gone != null) mask = WithoutFallen(village, wall.Origin, mask, gone);

                var art = BuildingSprites.ArtLevel(wall.Level);
                var center = GridSpace.CellCenter(wall.Origin);
                var post = center - new Vector3(PostHalf, 0f, PostHalf);
                Shadow(center);
                var north = (mask & Village.WallNorth) != 0;
                var east = (mask & Village.WallEast) != 0;
                if (east) Piece($"wall_seg_x_{art:00}", art, post, BuildingSprites.Order(center + new Vector3(0.5f, 0f, 0f)));
                if (north) Piece($"wall_seg_z_{art:00}", art, post, BuildingSprites.Order(center + new Vector3(0f, 0f, 0.5f)));
                if (mask == 0) Piece($"wall_post_v2_{art:00}", art, post, BuildingSprites.Order(center) + 1);
            }

            for (var i = _used; i < _pieces.Count; i++) _pieces[i].gameObject.SetActive(false);
            for (var i = _shadowsUsed; i < _shadows.Count; i++) _shadows[i].gameObject.SetActive(false);
        }

        /// <summary>
        /// A segment or lone post, sized so one tile step (art_meta.json, in pixels) spans one cell; the post uses
        /// the same pixel scale as the segments of its level.
        /// </summary>
        private void Piece(string name, int art, Vector3 foot, int order)
        {
            var sprite = BuildingSprites.Load(name);
            if (sprite == null) return;
            var step = BuildingSprites.MetaPixels($"wall_seg_x_{art:00}");
            if (step <= 0f) step = 184f;
            var screenWidth = sprite.rect.width / step * 0.70710678f;
            if (_used < _pieces.Count)
            {
                var piece = _pieces[_used];
                piece.gameObject.SetActive(true);
                piece.sprite = sprite;
                piece.sortingOrder = order;
                piece.transform.SetPositionAndRotation(foot, BuildingSprites.Upright);
                var scale = screenWidth / Mathf.Max(sprite.bounds.size.x, 0.001f);
                piece.transform.localScale = new Vector3(scale, scale * BuildingSprites.Lift, 1f);
            }
            else
            {
                _pieces.Add(BuildingSprites.Stand(_root, "WallPiece", sprite, foot, screenWidth, order, Color.white));
            }
            _used++;
        }

        private void Shadow(Vector3 center)
        {
            SpriteRenderer shadow;
            if (_shadowsUsed < _shadows.Count)
            {
                shadow = _shadows[_shadowsUsed];
                shadow.gameObject.SetActive(true);
            }
            else
            {
                shadow = BuildingSprites.GroundShadow(_root, 1f, 1f, 0.42f);
                _shadows.Add(shadow);
            }
            shadow.transform.position = center + new Vector3(0.18f, 0.012f, -0.1f);
            _shadowsUsed++;
        }

        /// <summary>A wall stops joining up to a neighbour that has been destroyed.</summary>
        private static int WithoutFallen(Village village, GridCoord cell, int mask, HashSet<int> gone)
        {
            bool Fallen(int x, int z)
            {
                if (!VillageGrid.InBounds(x, z)) return false;
                var neighbour = village.BuildingAt(new GridCoord(x, z));
                return neighbour != null && gone.Contains(neighbour.Id);
            }

            if (Fallen(cell.X, cell.Z + 1)) mask &= ~Village.WallNorth;
            if (Fallen(cell.X + 1, cell.Z)) mask &= ~Village.WallEast;
            if (Fallen(cell.X, cell.Z - 1)) mask &= ~Village.WallSouth;
            if (Fallen(cell.X - 1, cell.Z)) mask &= ~Village.WallWest;
            return mask;
        }

        private static int ConnectionsExcluding(Village village, GridCoord cell, int hiddenWallId)
        {
            var mask = village.WallConnectionMask(cell);
            if (hiddenWallId == 0) return mask;
            var hidden = village.Find(hiddenWallId);
            if (hidden == null) return mask;
            var o = hidden.Origin;
            if (o.X == cell.X && o.Z == cell.Z + 1) mask &= ~Village.WallNorth;
            if (o.X == cell.X + 1 && o.Z == cell.Z) mask &= ~Village.WallEast;
            if (o.X == cell.X && o.Z == cell.Z - 1) mask &= ~Village.WallSouth;
            if (o.X == cell.X - 1 && o.Z == cell.Z) mask &= ~Village.WallWest;
            return mask;
        }
    }

    /// <summary>Keeps world objects in sync with the Village model, and resolves taps to buildings.</summary>
    public sealed class VillageView
    {
        /// <summary>How large a hero stands by its Hero Base: big next to the small pedestal (owner, 2026-11-08).</summary>
        public const float BaseHeroScale3D = 1.35f;
        public const float BaseHeroScaleSprite = 1.35f;

        /// <summary>
        /// The Hero Base platform fills its square, and the hero stands on its glowing centre ring (owner, 2026-11-09,
        /// with the new platform art; this replaces the small pedestal and the hop down beside it).
        /// </summary>
        public const float PedestalSize = 1f;
        public static readonly Vector3 PedestalOffset = Vector3.zero;
        public const float PedestalTop = 0.3f * BuildingSprites.PictureShare;

        /// <summary>Where the hero stands: on the ring, where it appears.</summary>
        public static readonly Vector3 GuardOffset = PedestalOffset + Vector3.up * PedestalTop;

        // The optional animated prefab roster is not complete yet. Reuse bounded,
        // generated development meshes for missing characters instead of leaving
        // an assigned Hero Base visibly empty. This is NOT finished hero artwork.
        private sealed class FallbackFigure
        {
            public Mesh Mesh;
            public Material[] Materials;
        }

        private static readonly Dictionary<string, FallbackFigure> FallbackFigures =
            new Dictionary<string, FallbackFigure>();

        private static FallbackFigure FigureFor(HeroLook look)
        {
            if (!FallbackFigures.TryGetValue(look.Key, out var figure))
            {
                var builder = BuildingMeshes.Figure(look);
                figure = new FallbackFigure
                {
                    Mesh = builder.Build("HeroBaseFallback"),
                    Materials = builder.LitMaterials()
                };
                FallbackFigures.Add(look.Key, figure);
            }
            return figure;
        }

        private sealed class Entry
        {
            public GameObject Go;
            public Bounds Bounds;
            public string Signature;
            public GameObject Marker;
            public GameObject Works;
            public IsoStand Stand;
            public Renderer[] Hero;
        }

        private readonly Village _village;
        private readonly Transform _root;
        private readonly Dictionary<int, Entry> _views = new Dictionary<int, Entry>();
        private readonly WallView _walls;
        private VillageProps _props;
        private readonly System.Func<string, HeroLook> _heroLook;
        private int _hiddenId;
        private readonly HashSet<int> _gone = new HashSet<int>();

        // What each building looked like at the last sync, so placing, upgrading and moving can be celebrated.
        private readonly Dictionary<int, (int Level, GridCoord Origin)> _known = new Dictionary<int, (int, GridCoord)>();
        private bool _ready;

        public VillageView(Village village, Transform parent, System.Func<string, HeroLook> heroLook)
        {
            _village = village;
            _heroLook = heroLook;
            _root = new GameObject("Buildings").transform;
            _root.SetParent(parent, false);
            _walls = new WallView(parent);
            // The painted map (VillageMap) carries all the scenery and leaves its field clear, so no scattered props on it.
            _props = VillageMap.Loaded ? null : new VillageProps(village, parent);
            Sync();
        }

        /// <summary>Hides one building while it is being moved. 0 shows everything.</summary>
        public void SetHidden(int instanceId)
        {
            _hiddenId = instanceId;
            Sync();
        }

        public void SetVisible(bool visible)
        {
            _root.gameObject.SetActive(visible);
            _walls.SetVisible(visible);
            _props?.SetVisible(visible);
        }

        /// <summary>Removes a destroyed building from view. The village data itself is untouched.</summary>
        public void SetGone(int instanceId)
        {
            _gone.Add(instanceId);
            Sync();
        }

        public void Sync()
        {
            var alive = new HashSet<int>();
            foreach (var building in _village.Buildings)
            {
                if (building.Definition.IsWall || _gone.Contains(building.Id)) continue;
                alive.Add(building.Id);
                var look = building.AssignedHeroInstanceId != null ? _heroLook(building.AssignedHeroInstanceId) : null;
                var signature = $"{building.Definition.id}|{BuildingSprites.ArtLevel(building.Level)}|{look?.Key}";
                if (!_views.TryGetValue(building.Id, out var entry) || entry.Signature != signature)
                {
                    if (entry != null) Object.Destroy(entry.Go);
                    entry = Create(building, look, signature);
                    _views[building.Id] = entry;
                }

                var transform = entry.Go.transform;
                transform.localPosition = GridSpace.FootprintCenter(building.Footprint);
                transform.localRotation = GridSpace.ToQuaternion(building.Rotation);
                entry.Go.SetActive(building.Id != _hiddenId);
                var footprint = building.Footprint;
                var height = 1.5f;
                if (entry.Stand != null)
                {
                    entry.Stand.Place();
                    height = Mathf.Clamp(entry.Stand.WorldHeight * 0.75f, 0.6f, 4f);
                }
                entry.Bounds = new Bounds(transform.position + Vector3.up * (height * 0.5f),
                    new Vector3(footprint.Width * GridSpace.CellSize, height, footprint.Depth * GridSpace.CellSize));
                if (entry.Hero != null)
                    foreach (var renderer in entry.Hero)
                    {
                        if (renderer == null) continue;
                        renderer.sortingOrder = BuildingSprites.Order(transform.position) + 1;
                        // A hero drawn as one group (the 3D heroes) sorts by its group: one step above its base.
                        var group = renderer.GetComponentInParent<UnityEngine.Rendering.SortingGroup>();
                        if (group != null) group.sortingOrder = renderer.sortingOrder;
                    }
                Celebrate(building, entry);
            }

            var dead = new List<int>();
            foreach (var pair in _views)
                if (!alive.Contains(pair.Key))
                    dead.Add(pair.Key);
            foreach (var id in dead)
            {
                Object.Destroy(_views[id].Go);
                _views.Remove(id);
            }

            _walls.Rebuild(_village, _hiddenId, _gone);
            _props?.Refresh(_hiddenId);
            _ready = true;
        }

        /// <summary>
        /// Game feel for the village: a new building drops in with a squash and a puff of dust, an upgrade pops with a
        /// golden burst and its new level, and a moved building lands with a small bounce. Only after the first sync, so
        /// loading a village is quiet.
        /// </summary>
        private void Celebrate(BuildingInstance building, Entry entry)
        {
            // A building being moved is hidden; it celebrates when it lands, so its old place is kept until then.
            if (building.Id == _hiddenId) return;
            var had = _known.TryGetValue(building.Id, out var before);
            _known[building.Id] = (building.Level, building.Origin);
            if (!_ready || entry.Stand == null) return;
            var center = entry.Go.transform.position;
            var size = new Vector2(building.Footprint.Width, building.Footprint.Depth) * GridSpace.CellSize;
            if (!had)
            {
                Feel.Pop(entry.Stand, Feel.Tier.Medium);
                Feel.Dust(_root, center, size);
                GameSounds.Play(GameSound.Fall, 0.7f);
            }
            else if (building.Level > before.Level)
            {
                Feel.Pop(entry.Stand, Feel.Tier.Medium);
                Feel.LevelBurst(_root, center + Vector3.up * entry.Stand.WorldHeight * 0.45f, Mathf.Max(size.x, size.y) * 1.2f);
                GameSounds.Play(GameSound.Star);
                var viewer = Camera.main;
                if (viewer != null)
                    FloatingText.Create(_root, viewer, center + Vector3.up * (entry.Stand.WorldHeight * 0.8f + 0.4f), $"Level {building.Level}!", new Color(1f, 0.86f, 0.35f), 0.16f);
            }
            else if (before.Origin.X != building.Origin.X || before.Origin.Z != building.Origin.Z)
            {
                Feel.Pop(entry.Stand, Feel.Tier.Small);
                Feel.Dust(_root, center, size * 0.8f);
            }
        }

        /// <summary>A struck building flashes red for a moment (raids).</summary>
        public void HitFlash(int instanceId)
        {
            if (_views.TryGetValue(instanceId, out var entry) && entry.Stand != null) Feel.HitFlash(entry.Stand.Renderer);
        }

        /// <summary>The standing picture of a building, or null when it has none (walls, traps, fallen buildings).</summary>
        public IsoStand StandOf(int instanceId) => _views.TryGetValue(instanceId, out var entry) ? entry.Stand : null;


        private Entry Create(BuildingInstance building, HeroLook look, string signature)
        {
            var go = new GameObject($"{building.Definition.id}_{building.Id}");
            go.transform.SetParent(_root, false);
            go.transform.localPosition = GridSpace.FootprintCenter(building.Footprint);
            go.transform.localRotation = GridSpace.ToQuaternion(building.Rotation);
            var stand = BuildingSprites.Attach(go.transform, building.Definition, building.Level, Color.white);
            AddEffects(go.transform, building.Definition);
            Renderer[] hero = null;
            if (building.Definition.function == BuildingFunction.HeroBase)
            {
                // The platform, at full size (owner, 2026-11-09).
                if (stand != null)
                {
                    stand.ArtSize = PedestalSize;
                    stand.ArtOffset = PedestalOffset;
                    stand.Place();
                }
                var shadow = go.transform.Find("GroundShadow");
                if (shadow != null)
                {
                    shadow.localScale *= PedestalSize;
                    shadow.position = go.transform.position + PedestalOffset + new Vector3(0.12f, 0.015f, -0.06f);
                }
                var actor = HeroSprites.Spawn(look, go.transform);
                if (actor != null)
                {
                    actor.transform.localScale = Vector3.one * (actor.Solid ? BaseHeroScale3D : BaseHeroScaleSprite);
                    // It stands on the platform's ring.
                    HeroGuard.Attach(actor, go.transform, PedestalOffset + Vector3.up * PedestalTop, GuardOffset, look != null ? look.Color : Color.white);
                    hero = actor.GetComponentsInChildren<Renderer>(true);
                }
                else if (look != null)
                {
                    // Elites and any incomplete hero model still need to appear on the
                    // pedestal. BattleStage uses this same procedural development fallback.
                    var placeholder = new GameObject("HeroFallback");
                    placeholder.transform.SetParent(go.transform, false);
                    placeholder.transform.localPosition = GuardOffset;
                    placeholder.transform.localScale = Vector3.one * BaseHeroScale3D;
                    var figure = FigureFor(look);
                    placeholder.AddComponent<MeshFilter>().sharedMesh = figure.Mesh;
                    var renderer = placeholder.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = figure.Materials;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    // Keep the same guard placement and original elemental halo
                    // that finished 3D heroes receive.
                    HeroGuard.Attach(placeholder.transform, null, go.transform,
                        GuardOffset, GuardOffset, look.Color);
                    hero = placeholder.GetComponentsInChildren<Renderer>(true);
                }
            }
            return new Entry { Go = go, Signature = signature, Stand = stand, Hero = hero };
        }

        /// <summary>Small life for key buildings. Off at low quality, and never tied to any rule.</summary>
        private static void AddEffects(Transform parent, BuildingDefinition definition)
        {
            switch (definition.function)
            {
                case BuildingFunction.Research:
                    WorldEffects.Create(parent, "ChimneySmoke", new Vector3(0.62f, 2.0f, 0.3f), new Color(0.9f, 0.9f, 0.9f, 0.45f), new Color(1f, 1f, 1f, 0f), 3f, 3.2f, 0.5f, 0.5f, 0.05f);
                    break;
                case BuildingFunction.SummoningGate:
                    WorldEffects.Create(parent, "PortalMotes", new Vector3(0f, 0.6f, -0.1f), new Color(0.7f, 0.6f, 1f, 0.9f), new Color(0.5f, 0.9f, 1f, 0f), 7f, 2.2f, 0.6f, 0.16f, 0.9f);
                    break;
                case BuildingFunction.HeroAltar:
                    WorldEffects.Create(parent, "CrystalMotes", new Vector3(0f, 1.1f, 0f), new Color(0.6f, 0.95f, 1f, 0.9f), new Color(0.5f, 0.8f, 1f, 0f), 4f, 2f, 0.32f, 0.12f, 0.3f);
                    break;
            }
        }

        /// <summary>Floating markers over buildings with something to collect. Spatial, so the HUD stays clear.</summary>
        public void SetCollectMarkers(System.Func<BuildingInstance, bool> hasPending)
        {
            foreach (var pair in _views)
            {
                var building = _village.Find(pair.Key);
                if (building == null || building.Definition.produces == ResourceKind.None) continue;
                var show = hasPending(building);
                var entry = pair.Value;
                if (show && entry.Marker == null) entry.Marker = CreateMarker(entry.Go.transform, building.Definition.produces);
                if (entry.Marker != null) entry.Marker.SetActive(show);
            }
        }

        /// <summary>Builders at work over each building being upgraded (ADR-040): sparks and a little dust.</summary>
        public void SetUpgradeWorks()
        {
            foreach (var pair in _views)
            {
                var building = _village.Find(pair.Key);
                if (building == null) continue;
                var entry = pair.Value;
                if (building.Upgrading && entry.Works == null)
                {
                    var size = building.Footprint.Width;
                    entry.Works = new GameObject("UpgradeWorks");
                    entry.Works.transform.SetParent(entry.Go.transform, false);
                    WorldEffects.Create(entry.Works.transform, "Sparks", new Vector3(0f, 1.2f, 0f), new Color(1f, 0.8f, 0.35f, 1f), new Color(1f, 0.45f, 0.1f, 0f), 7f, 0.6f, 1.6f, 0.07f, size * 0.3f, 2.5f);
                    WorldEffects.Create(entry.Works.transform, "WorkDust", new Vector3(0f, 0.2f, 0f), new Color(0.85f, 0.76f, 0.6f, 0.5f), new Color(0.85f, 0.76f, 0.6f, 0f), 2.5f, 1.6f, 0.35f, 0.35f, size * 0.45f);
                }
                else if (!building.Upgrading && entry.Works != null)
                {
                    Object.Destroy(entry.Works);
                    entry.Works = null;
                }
            }
        }

        private static GameObject CreateMarker(Transform parent, ResourceKind kind)
        {
            // The owner's painted "ready to collect" bubble, bobbing over the building.
            var bubble = Resources.Load<Sprite>(kind == ResourceKind.Crystals ? "Props/collect_bubble_crystal" : "Props/collect_bubble_gold");
            var holder = new GameObject("CollectMarker");
            holder.transform.SetParent(parent, false);
            var art = BuildingSprites.Stand(holder.transform, "Bubble", bubble, parent.position + Vector3.up * 2.35f, 0.95f, BuildingSprites.Order(parent.position) + 40, Color.white);
            art.gameObject.AddComponent<Bob>();
            return holder;
        }

        /// <summary>A ghost for placement: the building's picture in a see-through tint, drawn over everything.</summary>
        public GameObject CreateGhost(BuildingDefinition definition, Color tint)
        {
            var go = new GameObject("PlacementGhost");
            go.transform.SetParent(_root, false);
            var stand = BuildingSprites.Attach(go.transform, definition, 1, new Color(tint.r, tint.g, tint.b, 0.7f));
            if (stand != null)
            {
                stand.OnTop = true;
                stand.Place();
            }
            return go;
        }

        /// <summary>
        /// Picks the building nearest the camera along the ray. Uses render bounds, so tapping a
        /// tall tower's roof selects the tower rather than whatever stands behind it.
        /// </summary>
        public BuildingInstance Pick(Ray ray)
        {
            BuildingInstance best = null;
            var bestDistance = float.MaxValue;
            foreach (var pair in _views)
            {
                if (pair.Key == _hiddenId) continue;
                if (pair.Value.Bounds.IntersectRay(ray, out var distance) && distance < bestDistance)
                {
                    bestDistance = distance;
                    best = _village.Find(pair.Key);
                }
            }

            foreach (var building in _village.Buildings)
            {
                if (!building.Definition.IsWall || building.Id == _hiddenId) continue;
                var center = GridSpace.CellCenter(building.Origin) + Vector3.up * 0.45f;
                var bounds = new Bounds(center, new Vector3(1f, 0.9f, 1f) * GridSpace.CellSize);
                if (bounds.IntersectRay(ray, out var distance) && distance < bestDistance)
                {
                    bestDistance = distance;
                    best = building;
                }
            }
            return best;
        }
    }
}

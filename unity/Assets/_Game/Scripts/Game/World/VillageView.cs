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
    /// Draws all walls as one rebuilt mesh from modular parts: a stone footing, a plaster and timber
    /// body and a tiled cap. Each neighbour adds an arm, so straights, corners, ends and junctions
    /// all join up. Levels change the materials, so upgrades show without new game logic.
    /// </summary>
    public sealed class WallView
    {
        private readonly MeshRenderer _renderer;
        private readonly MeshFilter _filter;

        public WallView(Transform parent)
        {
            var go = new GameObject("Walls");
            go.transform.SetParent(parent, false);
            _filter = go.AddComponent<MeshFilter>();
            _renderer = go.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = Mats.World;
        }

        public void SetVisible(bool visible) => _renderer.gameObject.SetActive(visible);

        public void Dispose()
        {
            if (_filter.sharedMesh != null) Object.Destroy(_filter.sharedMesh);
        }

        // The painted wall (owner's art pack) is a V: two arms meeting at the front, each ending in a post.
        // The importer cuts it into the arm that runs along +Z, the arm along +X, and a lone post. One arm is
        // one cell long: 186 px of picture across 1 / sqrt 2 world units of screen width.
        private const float WallArmPixels = 186f;
        private const float WallHalfThickness = 0.25f;
        private readonly List<SpriteRenderer> _pieces = new List<SpriteRenderer>();
        private int _used;
        private readonly List<SpriteRenderer> _shadows = new List<SpriteRenderer>();
        private int _shadowsUsed;

        /// <summary>Walls stand taller than painted, so a run reads as a wall rather than a fence from the village camera.</summary>
        private const float WallHeight = 1.35f;

        private static bool Painted => BuildingSprites.Load("wall_arm_z_01") != null;

        public void Rebuild(Village village, int hiddenWallId, HashSet<int> gone = null)
        {
            if (Painted)
            {
                RebuildPainted(village, hiddenWallId, gone);
                return;
            }

            var builder = new MeshBuilder();
            foreach (var building in village.Buildings)
            {
                if (!building.Definition.IsWall || building.Id == hiddenWallId) continue;
                if (gone != null && gone.Contains(building.Id)) continue;
                AddSegment(builder, village, building, hiddenWallId, gone);
            }

            var old = _filter.sharedMesh;
            _filter.sharedMesh = builder.Build("Walls");
            if (old != null) Object.Destroy(old);
        }

        private void RebuildPainted(Village village, int hiddenWallId, HashSet<int> gone)
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
                // The arms meet at the front corner of the wall's base, half a thickness towards the camera.
                var junction = center - new Vector3(WallHalfThickness, 0f, WallHalfThickness);
                Shadow(center);
                var north = (mask & Village.WallNorth) != 0;
                var east = (mask & Village.WallEast) != 0;
                var south = (mask & Village.WallSouth) != 0;
                var west = (mask & Village.WallWest) != 0;

                // Each wall draws the arms that leave it northwards and eastwards; the neighbour's arm brings the
                // post that stands on this cell. A cell nothing arrives at gets its own post.
                if (north) Piece($"wall_arm_z_{art:00}", junction, BuildingSprites.Order(center + new Vector3(0f, 0f, 0.5f)));
                if (east) Piece($"wall_arm_x_{art:00}", junction, BuildingSprites.Order(center + new Vector3(0.5f, 0f, 0f)));
                if (!south && !west) Piece($"wall_post_{art:00}", junction, BuildingSprites.Order(center) + 1);
            }

            for (var i = _used; i < _pieces.Count; i++) _pieces[i].gameObject.SetActive(false);
            for (var i = _shadowsUsed; i < _shadows.Count; i++) _shadows[i].gameObject.SetActive(false);
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
                shadow = BuildingSprites.GroundShadow(_renderer.transform, 1f, 1f, 0.42f);
                _shadows.Add(shadow);
            }
            shadow.transform.position = center + new Vector3(0.18f, 0.012f, -0.1f);
            _shadowsUsed++;
        }

        private void Piece(string name, Vector3 foot, int order)
        {
            var sprite = BuildingSprites.Load(name);
            if (sprite == null) return;
            var screenWidth = sprite.rect.width / WallArmPixels * 0.70710678f;
            SpriteRenderer piece;
            if (_used < _pieces.Count)
            {
                piece = _pieces[_used];
                piece.gameObject.SetActive(true);
                piece.sprite = sprite;
                piece.sortingOrder = order;
                piece.transform.SetPositionAndRotation(foot, BuildingSprites.Upright);
                var scale = screenWidth / Mathf.Max(sprite.bounds.size.x, 0.001f);
                piece.transform.localScale = new Vector3(scale, scale * BuildingSprites.Lift * WallHeight, 1f);
            }
            else
            {
                piece = BuildingSprites.Stand(_renderer.transform, "WallPiece", sprite, foot, screenWidth, order, Color.white);
                var size = piece.transform.localScale;
                piece.transform.localScale = new Vector3(size.x, size.y * WallHeight, 1f);
                _pieces.Add(piece);
            }
            _used++;
        }

        private static void AddSegment(MeshBuilder builder, Village village, BuildingInstance wall, int hiddenWallId, HashSet<int> gone)
        {
            var center = GridSpace.CellCenter(wall.Origin);
            var mask = ConnectionsExcluding(village, wall.Origin, hiddenWallId);
            if (gone != null) mask = WithoutFallen(village, wall.Origin, mask, gone);

            // Upgrades read at a glance: each level is taller, and each tier changes the materials.
            var skin = BuildingSkin.For("wall", wall.Level);
            var height = 0.82f + 0.03f * (wall.Level - 1);
            const float thickness = 0.56f;
            const float arm = 0.27f;

            var north = (mask & Village.WallNorth) != 0;
            var south = (mask & Village.WallSouth) != 0;
            var east = (mask & Village.WallEast) != 0;
            var west = (mask & Village.WallWest) != 0;
            var straight = (north && south && !east && !west) || (east && west && !north && !south);

            // Corners, junctions and ends get a heavier post, so the wall has a rhythm.
            var postHeight = straight ? height : height + 0.24f;
            var postWidth = straight ? thickness : thickness + 0.16f;
            Section(builder, center, new Vector2(postWidth, postWidth), postHeight, skin);
            if (!straight)
            {
                var top = center + Vector3.up * (postHeight + 0.2f);
                builder.Cylinder(top, 0.05f, 0.015f, 0.2f, 5, skin.Amber);
                if (skin.Tier >= 1) builder.Box(center + new Vector3(0f, height * 0.75f, -postWidth * 0.5f - 0.01f), new Vector3(0.16f, 0.14f, 0.02f), skin.WindowGlow, ArtSurface.Plain, 0f, 0.85f);
                if (skin.Tier >= 4) builder.Blob(top + Vector3.up * 0.26f, Vector3.one * 0.07f, skin.Crystal, ArtSurface.Plain, 0f, 1f, 5, 3);
            }

            var offset = 0.14f + arm * 0.5f + 0.09f;
            if (north) Section(builder, center + new Vector3(0, 0, offset), new Vector2(thickness, arm + 0.04f), height, skin);
            if (south) Section(builder, center + new Vector3(0, 0, -offset), new Vector2(thickness, arm + 0.04f), height, skin);
            if (east) Section(builder, center + new Vector3(offset, 0, 0), new Vector2(arm + 0.04f, thickness), height, skin);
            if (west) Section(builder, center + new Vector3(-offset, 0, 0), new Vector2(arm + 0.04f, thickness), height, skin);
        }

        private static void Section(MeshBuilder builder, Vector3 at, Vector2 size, float height, BuildingSkin skin)
        {
            var foot = height * 0.42f;
            builder.Box(at, new Vector3(size.x + 0.04f, foot, size.y + 0.04f), skin.Stone, skin.StoneSurface);
            builder.Box(at + Vector3.up * foot, new Vector3(size.x - 0.02f, height - foot, size.y - 0.02f), skin.Plaster, skin.WallSurface);
            builder.Box(at + Vector3.up * (height - 0.08f), new Vector3(size.x, 0.08f, size.y), skin.DarkWood, ArtSurface.Wood);
            var rail = at + Vector3.up * (height * 0.7f);
            var hx = size.x * 0.5f - 0.005f;
            var hz = size.y * 0.5f - 0.005f;
            builder.Quad(rail + new Vector3(-hx, -0.025f, -hz), rail + new Vector3(hx, -0.025f, -hz), rail + new Vector3(hx, 0.025f, -hz), rail + new Vector3(-hx, 0.025f, -hz), Vector3.back, skin.DarkWood, ArtSurface.Wood);
            builder.Quad(rail + new Vector3(-hx, -0.025f, -hz), rail + new Vector3(-hx, -0.025f, hz), rail + new Vector3(-hx, 0.025f, hz), rail + new Vector3(-hx, 0.025f, -hz), Vector3.left, skin.DarkWood, ArtSurface.Wood);
            builder.Frustum(at + Vector3.up * height, size.x + 0.16f, size.y + 0.16f, Mathf.Max(size.x - 0.26f, 0.08f), Mathf.Max(size.y - 0.26f, 0.08f), 0.2f, skin.Slate, skin.RoofSurface);
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

    /// <summary>Renders each building's real mesh once to a texture, for catalog cards.</summary>
    public sealed class BuildingThumbnails : System.IDisposable
    {
        private const int ThumbnailLayer = 31;
        private readonly Dictionary<string, RenderTexture> _textures = new Dictionary<string, RenderTexture>();
        private readonly Camera _camera;
        private readonly Transform _stage;

        public BuildingThumbnails(Transform parent)
        {
            _stage = new GameObject("ThumbnailStage").transform;
            _stage.SetParent(parent, false);
            _stage.position = new Vector3(0f, -500f, 0f);

            var cameraObject = new GameObject("ThumbnailCamera", typeof(Camera));
            cameraObject.transform.SetParent(_stage, false);
            cameraObject.transform.localRotation = Quaternion.Euler(IsoCamera.Pitch, 45f, 0f);
            _camera = cameraObject.GetComponent<Camera>();
            _camera.enabled = false;
            _camera.orthographic = true;
            _camera.cullingMask = 1 << ThumbnailLayer;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 60f;
        }

        public Texture For(BuildingDefinition definition)
        {
            if (_textures.TryGetValue(definition.id, out var existing) && existing != null) return existing;

            var builder = BuildingMeshes.For(definition, null);
            var mesh = builder.Build("Thumbnail");
            var subject = new GameObject("Subject") { layer = ThumbnailLayer };
            subject.transform.SetParent(_stage, false);
            subject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = subject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = builder.LitMaterials();

            var center = mesh.bounds.center;
            var extent = mesh.bounds.extents.magnitude;
            _camera.orthographicSize = extent * 1.02f;
            _camera.transform.localPosition = center - _camera.transform.forward * 30f;

            var texture = new RenderTexture(256, 256, 24);
            _camera.targetTexture = texture;
            _camera.Render();
            _camera.targetTexture = null;
            _textures[definition.id] = texture;

            // Destroy waits for end of frame, so hide it now or the next thumbnail would include it.
            subject.SetActive(false);
            Object.Destroy(subject);
            Object.Destroy(mesh);
            return texture;
        }

        public void Dispose()
        {
            foreach (var texture in _textures.Values)
                if (texture != null) texture.Release();
            _textures.Clear();
        }
    }

    /// <summary>Keeps world objects in sync with the Village model, and resolves taps to buildings.</summary>
    public sealed class VillageView
    {
        private sealed class Entry
        {
            public GameObject Go;
            public Bounds Bounds;
            public string Signature;
            public GameObject Marker;
            public IsoStand Stand;
            public Renderer[] Hero;
        }

        private readonly Village _village;
        private readonly Transform _root;
        private readonly Dictionary<int, Entry> _views = new Dictionary<int, Entry>();
        private readonly WallView _walls;
        private readonly Dictionary<string, Mesh> _meshCache = new Dictionary<string, Mesh>();
        private readonly Dictionary<string, Material[]> _materialCache = new Dictionary<string, Material[]>();
        private readonly System.Func<string, HeroLook> _heroLook;
        private int _hiddenId;
        private readonly HashSet<int> _gone = new HashSet<int>();

        public int ViewCount => _views.Count;

        public VillageView(Village village, Transform parent, System.Func<string, HeroLook> heroLook)
        {
            _village = village;
            _heroLook = heroLook;
            _root = new GameObject("Buildings").transform;
            _root.SetParent(parent, false);
            _walls = new WallView(parent);
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
        }

        /// <summary>Removes a destroyed building from view. The village data itself is untouched.</summary>
        public void SetGone(int instanceId)
        {
            _gone.Add(instanceId);
            Sync();
        }

        public void Dispose()
        {
            _walls.Dispose();
            foreach (var mesh in _meshCache.Values)
                if (mesh != null) Object.Destroy(mesh);
            _meshCache.Clear();
        }

        public void Sync()
        {
            var alive = new HashSet<int>();
            foreach (var building in _village.Buildings)
            {
                if (building.Definition.IsWall || _gone.Contains(building.Id)) continue;
                alive.Add(building.Id);
                var look = building.AssignedHeroInstanceId != null ? _heroLook(building.AssignedHeroInstanceId) : null;
                var art = BuildingSprites.Has(building.Definition.id)
                    ? "art" + BuildingSprites.ArtLevel(building.Level)
                    : "tier" + BuildingSkin.TierForLevel(building.Level);
                var signature = $"{building.Definition.id}|{art}|{look?.Key}";
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
                if (entry.Stand != null)
                {
                    entry.Stand.Place();
                    var footprint = building.Footprint;
                    var height = Mathf.Clamp(entry.Stand.WorldHeight * 0.75f, 0.6f, 4f);
                    entry.Bounds = new Bounds(transform.position + Vector3.up * (height * 0.5f),
                        new Vector3(footprint.Width * GridSpace.CellSize, height, footprint.Depth * GridSpace.CellSize));
                    if (entry.Hero != null)
                        foreach (var renderer in entry.Hero)
                            if (renderer != null) renderer.sortingOrder = BuildingSprites.Order(transform.position) + 1;
                }
                else entry.Bounds = entry.Go.GetComponent<MeshRenderer>().bounds;
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
        }

        private Entry Create(BuildingInstance building, HeroLook look, string signature)
        {
            var go = new GameObject($"{building.Definition.id}_{building.Id}");
            go.transform.SetParent(_root, false);
            go.transform.localPosition = GridSpace.FootprintCenter(building.Footprint);
            go.transform.localRotation = GridSpace.ToQuaternion(building.Rotation);
            // The owner's painted picture when there is one; the code-drawn mesh otherwise.
            var stand = BuildingSprites.Attach(go.transform, building.Definition, building.Level, Color.white);
            if (stand == null)
            {
                GetMesh(building.Definition, look, building.Level, signature, out var mesh, out var materials);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            }
            AddEffects(go.transform, building.Definition);
            Renderer[] hero = null;
            if (building.Definition.function == BuildingFunction.HeroBase)
            {
                var actor = HeroSprites.Spawn(look, go.transform);
                if (actor != null)
                {
                    // The painted base is a low platform; the mesh had a taller pedestal.
                    actor.transform.localPosition = Vector3.up * (stand != null ? 0.12f : BuildingMeshes.PedestalTop);
                    // Heroes stand large on their bases, so they read clearly from the village camera.
                    actor.transform.localScale = Vector3.one * 1.35f;
                    hero = actor.GetComponentsInChildren<Renderer>(true);
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

        private void GetMesh(BuildingDefinition definition, HeroLook hero, int level, string key, out Mesh mesh, out Material[] materials)
        {
            if (_meshCache.TryGetValue(key, out mesh))
            {
                materials = _materialCache[key];
                return;
            }
            var builder = BuildingMeshes.For(definition, hero, level);
            mesh = builder.Build(definition.id);
            materials = builder.LitMaterials();
            _meshCache[key] = mesh;
            _materialCache[key] = materials;
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

        private static GameObject CreateMarker(Transform parent, ResourceKind kind)
        {
            var color = kind == ResourceKind.Crystals ? Palette.Crystal : Palette.Amber;
            var builder = new MeshBuilder();
            builder.ContactShade = false;
            builder.Blob(new Vector3(0f, 2.75f, 0f), new Vector3(0.24f, 0.24f, 0.24f), color, ArtSurface.Plain, 0.6f, 0.65f, 8, 5);
            builder.Frustum(new Vector3(0f, 2.3f, 0f), 0f, 0f, 0.26f, 0.26f, 0.22f, color, ArtSurface.Plain, 0.6f, 0.65f);
            var marker = new GameObject("CollectMarker");
            marker.transform.SetParent(parent, false);
            marker.AddComponent<MeshFilter>().sharedMesh = builder.Build("CollectMarker");
            var renderer = marker.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = builder.LitMaterials();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return marker;
        }

        /// <summary>A ghost for placement: the same silhouette in a see-through tint.</summary>
        public GameObject CreateGhost(BuildingDefinition definition, Color tint)
        {
            var go = new GameObject("PlacementGhost");
            go.transform.SetParent(_root, false);
            var stand = BuildingSprites.Attach(go.transform, definition, 1, new Color(tint.r, tint.g, tint.b, 0.7f));
            if (stand != null)
            {
                // Above everything, so the ghost is never hidden while it is dragged.
                stand.OnTop = true;
                stand.Place();
                return go;
            }

            var key = $"{definition.id}|0|";
            GetMesh(definition, null, 1, key, out var mesh, out var materials);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            var ghostMaterials = new Material[materials.Length];
            for (var i = 0; i < ghostMaterials.Length; i++) ghostMaterials[i] = Mats.Overlay(tint);
            renderer.sharedMaterials = ghostMaterials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
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

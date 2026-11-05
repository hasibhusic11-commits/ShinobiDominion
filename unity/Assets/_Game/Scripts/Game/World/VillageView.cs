using System.Collections.Generic;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShinobiDominion.Game
{
    /// <summary>The ground, boundary and decoration outside the buildable area. One merged mesh.</summary>
    public static class VillageScenery
    {
        public static GameObject Create(Transform parent)
        {
            float size = VillageGrid.Size * GridSpace.CellSize;
            const float outer = 45f;
            const float border = 0.35f;

            var mesh = new MeshBuilder();
            mesh.Quad(new Vector3(-outer, -0.02f, -outer), new Vector3(size + outer, -0.02f, -outer),
                new Vector3(size + outer, -0.02f, size + outer), new Vector3(-outer, -0.02f, size + outer), Vector3.up, Palette.OuterGrass);
            mesh.Quad(new Vector3(0, 0, 0), new Vector3(size, 0, 0), new Vector3(size, 0, size), new Vector3(0, 0, size), Vector3.up, Palette.Grass);

            // Subtle worn patches so the courtyard reads as packed earth rather than a flat sheet.
            var patches = new System.Random(2718);
            for (var i = 0; i < 70; i++)
            {
                var px = (float)patches.NextDouble() * (size - 5f);
                var pz = (float)patches.NextDouble() * (size - 5f);
                var pw = 1.5f + (float)patches.NextDouble() * 3.2f;
                var pd = 1.5f + (float)patches.NextDouble() * 3.2f;
                var tone = i % 2 == 0 ? Palette.GrassLight : Palette.GrassDark;
                const float y = 0.004f;
                mesh.Quad(new Vector3(px, y, pz), new Vector3(px + pw, y, pz), new Vector3(px + pw, y, pz + pd), new Vector3(px, y, pz + pd), Vector3.up, tone);
            }

            // Low stone kerb just outside the map, so the buildable boundary reads without a grid.
            var mid = size * 0.5f;
            var kerbLength = size + border * 2f;
            mesh.Box(new Vector3(mid, 0, -border * 0.5f), new Vector3(kerbLength, 0.12f, border), Palette.Stone);
            mesh.Box(new Vector3(mid, 0, size + border * 0.5f), new Vector3(kerbLength, 0.12f, border), Palette.Stone);
            mesh.Box(new Vector3(-border * 0.5f, 0, mid), new Vector3(border, 0.12f, size), Palette.Stone);
            mesh.Box(new Vector3(size + border * 0.5f, 0, mid), new Vector3(border, 0.12f, size), Palette.Stone);

            // Pond in the north-east, entirely outside the map.
            mesh.Quad(new Vector3(size + 3, 0.0f, size - 12), new Vector3(size + 16, 0.0f, size - 12),
                new Vector3(size + 16, 0.0f, size + 6), new Vector3(size + 3, 0.0f, size + 6), Vector3.up, Palette.Water);

            // Fixed seed: the same forest every launch.
            var random = new System.Random(5150);
            for (var i = 0; i < 380; i++)
            {
                var x = (float)(random.NextDouble() * (size + outer * 2f) - outer);
                var z = (float)(random.NextDouble() * (size + outer * 2f) - outer);
                const float margin = 1.5f;
                if (x > -margin && x < size + margin && z > -margin && z < size + margin) continue;
                if (x > size + 2 && x < size + 17 && z > size - 13 && z < size + 7) continue;
                Tree(mesh, new Vector3(x, 0, z), 0.8f + (float)random.NextDouble() * 0.9f, random.Next(6) == 0);
            }

            var built = mesh.Build("VillageScenery");
            var go = new GameObject("Scenery");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = built;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = mesh.LitMaterials();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        private static void Tree(MeshBuilder mesh, Vector3 at, float scale, bool sakura)
        {
            mesh.Box(at, new Vector3(0.25f, 0.8f, 0.25f) * scale, Palette.Wood);
            var crown = sakura ? Palette.Sakura : Palette.Leaf;
            mesh.Frustum(at + Vector3.up * 0.7f * scale, 1.9f * scale, 1.9f * scale, 0f, 0f, 1.6f * scale, crown);
            mesh.Frustum(at + Vector3.up * 1.5f * scale, 1.4f * scale, 1.4f * scale, 0f, 0f, 1.4f * scale, crown);
        }
    }

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

        public void Show(IEnumerable<KeyValuePair<GridCoord, bool>> cells)
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
    /// Draws all walls as a single rebuilt mesh: a post per cell plus an arm toward each
    /// connected neighbour, so straights, corners, ends and junctions all join correctly.
    /// </summary>
    public sealed class WallView
    {
        private readonly Mesh _mesh;
        private readonly MeshRenderer _renderer;
        private readonly MeshFilter _filter;

        public WallView(Transform parent)
        {
            var go = new GameObject("Walls");
            go.transform.SetParent(parent, false);
            _filter = go.AddComponent<MeshFilter>();
            _renderer = go.AddComponent<MeshRenderer>();
            _mesh = new Mesh { name = "Walls", indexFormat = IndexFormat.UInt32 };
            _mesh.MarkDynamic();
            _filter.sharedMesh = _mesh;
        }

        public void Rebuild(Village village, int hiddenWallId)
        {
            var builder = new MeshBuilder();
            foreach (var building in village.Buildings)
            {
                if (!building.Definition.IsWall || building.Id == hiddenWallId) continue;
                AddSegment(builder, village, building, hiddenWallId);
            }

            var built = builder.Build("WallsTemp");
            _mesh.Clear();
            _mesh.subMeshCount = built.subMeshCount;
            _mesh.SetVertices(built.vertices);
            _mesh.SetNormals(built.normals);
            for (var i = 0; i < built.subMeshCount; i++)
                _mesh.SetTriangles(built.GetTriangles(i), i);
            _mesh.RecalculateBounds();
            Object.Destroy(built);
            _renderer.sharedMaterials = builder.LitMaterials();
        }

        private static void AddSegment(MeshBuilder builder, Village village, BuildingInstance wall, int hiddenWallId)
        {
            var center = GridSpace.CellCenter(wall.Origin);
            var mask = ConnectionsExcluding(village, wall.Origin, hiddenWallId);
            var body = 0.75f + 0.05f * (wall.Level - 1);
            var stone = wall.Level >= 5 ? Palette.DarkStone : Palette.Stone;
            const float thickness = 0.56f;
            const float arm = 0.25f;
            const float capHeight = 0.2f;

            // Castle-style wall: stone footing, pale plaster, and a dark tiled cap with brass
            // finials on the posts. A straight run has no post, so it reads as continuous.
            var straight = mask == (Village.WallNorth | Village.WallSouth) || mask == (Village.WallEast | Village.WallWest);
            var postHeight = straight ? body : body + 0.2f;
            var postWidth = straight ? thickness : thickness + 0.16f;
            builder.Box(center, new Vector3(postWidth, postHeight * 0.45f, postWidth), stone);
            builder.Box(center + Vector3.up * postHeight * 0.45f, new Vector3(postWidth - 0.06f, postHeight * 0.55f, postWidth - 0.06f), Palette.Plaster);
            builder.Frustum(center + Vector3.up * postHeight, postWidth + 0.2f, postWidth + 0.2f, postWidth - 0.2f, postWidth - 0.2f, capHeight, Palette.Slate);
            if (!straight)
                builder.Frustum(center + Vector3.up * (postHeight + capHeight), 0.18f, 0.18f, 0f, 0f, 0.26f, Palette.Amber);

            var offset = 0.25f + 0.25f * 0.5f;
            if ((mask & Village.WallNorth) != 0) Arm(builder, center + new Vector3(0, 0, offset), new Vector2(thickness, arm), body, capHeight, stone);
            if ((mask & Village.WallSouth) != 0) Arm(builder, center + new Vector3(0, 0, -offset), new Vector2(thickness, arm), body, capHeight, stone);
            if ((mask & Village.WallEast) != 0) Arm(builder, center + new Vector3(offset, 0, 0), new Vector2(arm, thickness), body, capHeight, stone);
            if ((mask & Village.WallWest) != 0) Arm(builder, center + new Vector3(-offset, 0, 0), new Vector2(arm, thickness), body, capHeight, stone);
        }

        private static void Arm(MeshBuilder builder, Vector3 at, Vector2 size, float body, float capHeight, Color stone)
        {
            builder.Box(at, new Vector3(size.x, body * 0.45f, size.y), stone);
            builder.Box(at + Vector3.up * body * 0.45f, new Vector3(size.x - 0.04f, body * 0.55f, size.y - 0.04f), Palette.Plaster);
            var wide = size.x > size.y;
            var capX = wide ? size.x + 0.2f : size.x;
            var capZ = wide ? size.y : size.y + 0.2f;
            builder.Frustum(at + Vector3.up * body, capX, capZ, wide ? size.x - 0.2f : size.x, wide ? size.y : size.y - 0.2f, capHeight, Palette.Slate);
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
            cameraObject.transform.localRotation = Quaternion.Euler(35.264f, 45f, 0f);
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
        }

        private readonly Village _village;
        private readonly Transform _root;
        private readonly Dictionary<int, Entry> _views = new Dictionary<int, Entry>();
        private readonly WallView _walls;
        private readonly Dictionary<string, Mesh> _meshCache = new Dictionary<string, Mesh>();
        private readonly Dictionary<string, Material[]> _materialCache = new Dictionary<string, Material[]>();
        private readonly System.Func<string, HeroLook> _heroLook;
        private int _hiddenId;

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

        public void Sync()
        {
            var alive = new HashSet<int>();
            foreach (var building in _village.Buildings)
            {
                if (building.Definition.IsWall) continue;
                alive.Add(building.Id);
                var look = building.AssignedHeroInstanceId != null ? _heroLook(building.AssignedHeroInstanceId) : null;
                var signature = $"{building.Definition.id}|{look?.Key}";
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
                entry.Bounds = entry.Go.GetComponent<MeshRenderer>().bounds;
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

            _walls.Rebuild(_village, _hiddenId);
        }

        private Entry Create(BuildingInstance building, HeroLook look, string signature)
        {
            var go = new GameObject($"{building.Definition.id}_{building.Id}");
            go.transform.SetParent(_root, false);
            GetMesh(building.Definition, look, signature, out var mesh, out var materials);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return new Entry { Go = go, Signature = signature };
        }

        private void GetMesh(BuildingDefinition definition, HeroLook hero, string key, out Mesh mesh, out Material[] materials)
        {
            if (_meshCache.TryGetValue(key, out mesh))
            {
                materials = _materialCache[key];
                return;
            }
            var builder = BuildingMeshes.For(definition, hero);
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
            builder.Box(new Vector3(0f, 2.2f, 0f), new Vector3(0.08f, 0.5f, 0.08f), Palette.Plaster);
            builder.Frustum(new Vector3(0f, 2.7f, 0f), 0.8f, 0.8f, 0.6f, 0.6f, 0.28f, color);
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
            var key = $"{definition.id}|";
            GetMesh(definition, null, key, out var mesh, out var materials);
            var go = new GameObject("PlacementGhost");
            go.transform.SetParent(_root, false);
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

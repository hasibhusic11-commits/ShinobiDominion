using System.Collections.Generic;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShinobiDominion.Game
{
    /// <summary>Converts grid cells to world space. Cell (0,0) spans world (0,0)..(CellSize,CellSize) at y = 0.</summary>
    public static class GridSpace
    {
        public const float CellSize = 1f;

        public static GridCoord WorldToCell(Vector3 world) =>
            new GridCoord(Mathf.FloorToInt(world.x / CellSize), Mathf.FloorToInt(world.z / CellSize));

        public static Vector3 CellCenter(GridCoord cell) =>
            new Vector3((cell.X + 0.5f) * CellSize, 0f, (cell.Z + 0.5f) * CellSize);

        public static Vector3 FootprintCenter(Footprint footprint) =>
            new Vector3(
                (footprint.Origin.X + footprint.Width * 0.5f) * CellSize,
                0f,
                (footprint.Origin.Z + footprint.Depth * 0.5f) * CellSize);

        public static Quaternion ToQuaternion(Rotation rotation) => Quaternion.Euler(0f, 90f * (int)rotation, 0f);
    }

    /// <summary>Shared placeholder materials, cached by colour so buildings batch.</summary>
    public static class Mats
    {
        private static readonly Dictionary<Color, Material> LitCache = new Dictionary<Color, Material>();
        private static readonly Dictionary<Color, Material> OverlayCache = new Dictionary<Color, Material>();
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public static Material Lit(Color color)
        {
            if (LitCache.TryGetValue(color, out var cached) && cached != null) return cached;
            var template = Resources.Load<Material>("Materials/PlaceholderLit");
            var material = template != null
                ? new Material(template)
                : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor(BaseColor, color);
            material.enableInstancing = true;
            LitCache[color] = material;
            return material;
        }

        public static Material Overlay(Color color)
        {
            if (OverlayCache.TryGetValue(color, out var cached) && cached != null) return cached;
            var template = Resources.Load<Material>("Materials/PlaceholderOverlay");
            Material material;
            if (template != null)
            {
                material = new Material(template);
            }
            else
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                MakeTransparent(material);
            }
            material.SetColor(BaseColor, color);
            OverlayCache[color] = material;
            return material;
        }

        public static void MakeTransparent(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        public static void ClearCaches()
        {
            LitCache.Clear();
            OverlayCache.Clear();
        }
    }

    /// <summary>Builds flat-shaded placeholder meshes, one submesh per colour.</summary>
    public sealed class MeshBuilder
    {
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<List<int>> _triangles = new List<List<int>>();

        private List<int> TrianglesFor(Color color)
        {
            var index = _colors.IndexOf(color);
            if (index >= 0) return _triangles[index];
            _colors.Add(color);
            var list = new List<int>();
            _triangles.Add(list);
            return list;
        }

        /// <summary>Adds a quad, flipping winding if needed so it faces <paramref name="outward"/>.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Color color)
        {
            var normal = Vector3.Cross(b - a, c - a).normalized;
            if (Vector3.Dot(normal, outward) < 0f)
            {
                (b, d) = (d, b);
                normal = -normal;
            }

            var start = _vertices.Count;
            _vertices.Add(a);
            _vertices.Add(b);
            _vertices.Add(c);
            _vertices.Add(d);
            for (var i = 0; i < 4; i++) _normals.Add(normal);

            var triangles = TrianglesFor(color);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        /// <summary>Axis-aligned box. <paramref name="baseCenter"/> is the centre of its bottom face. The bottom is not drawn.</summary>
        public void Box(Vector3 baseCenter, Vector3 size, Color color) =>
            Frustum(baseCenter, size.x, size.z, size.x, size.z, size.y, color);

        /// <summary>A tapered block: roofs, plinths and pillars. Top size 0 makes a pyramid.</summary>
        public void Frustum(Vector3 baseCenter, float bottomX, float bottomZ, float topX, float topZ, float height, Color color)
        {
            var bx = bottomX * 0.5f;
            var bz = bottomZ * 0.5f;
            var tx = topX * 0.5f;
            var tz = topZ * 0.5f;
            var up = Vector3.up * height;

            var b0 = baseCenter + new Vector3(-bx, 0f, -bz);
            var b1 = baseCenter + new Vector3(bx, 0f, -bz);
            var b2 = baseCenter + new Vector3(bx, 0f, bz);
            var b3 = baseCenter + new Vector3(-bx, 0f, bz);
            var t0 = baseCenter + up + new Vector3(-tx, 0f, -tz);
            var t1 = baseCenter + up + new Vector3(tx, 0f, -tz);
            var t2 = baseCenter + up + new Vector3(tx, 0f, tz);
            var t3 = baseCenter + up + new Vector3(-tx, 0f, tz);

            Quad(b0, b1, t1, t0, Vector3.back, color);
            Quad(b1, b2, t2, t1, Vector3.right, color);
            Quad(b2, b3, t3, t2, Vector3.forward, color);
            Quad(b3, b0, t0, t3, Vector3.left, color);
            if (topX > 0f && topZ > 0f) Quad(t0, t1, t2, t3, Vector3.up, color);
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.subMeshCount = _triangles.Count;
            for (var i = 0; i < _triangles.Count; i++)
                mesh.SetTriangles(_triangles[i], i);
            mesh.RecalculateBounds();
            return mesh;
        }

        public Material[] LitMaterials()
        {
            var materials = new Material[_colors.Count];
            for (var i = 0; i < materials.Length; i++) materials[i] = Mats.Lit(_colors[i]);
            return materials;
        }
    }

    /// <summary>Screen-space neutral colours for the world, per the brief's quiet palette.</summary>
    public static class Palette
    {
        public static readonly Color Stone = new Color(0.55f, 0.56f, 0.58f);
        public static readonly Color DarkStone = new Color(0.36f, 0.38f, 0.42f);
        public static readonly Color Slate = new Color(0.25f, 0.30f, 0.37f);
        public static readonly Color Wood = new Color(0.45f, 0.32f, 0.22f);
        public static readonly Color Plaster = new Color(0.82f, 0.78f, 0.68f);
        public static readonly Color Amber = new Color(0.86f, 0.64f, 0.24f);
        public static readonly Color AccentRed = new Color(0.62f, 0.20f, 0.18f);
        public static readonly Color AccentBlue = new Color(0.25f, 0.47f, 0.72f);
        public static readonly Color Crystal = new Color(0.45f, 0.82f, 0.90f);
        public static readonly Color Grass = new Color(0.40f, 0.52f, 0.33f);
        public static readonly Color OuterGrass = new Color(0.29f, 0.41f, 0.27f);
        public static readonly Color Leaf = new Color(0.20f, 0.36f, 0.24f);
        public static readonly Color Sakura = new Color(0.86f, 0.62f, 0.70f);
        public static readonly Color Water = new Color(0.28f, 0.50f, 0.60f);
        public static readonly Color Sky = new Color(0.62f, 0.72f, 0.74f);
    }

    /// <summary>
    /// Honest primitive placeholder art. Each function gets a distinct silhouette so buildings
    /// read at isometric distance without labels. All of it is replaced by real art in Phase 5.
    /// </summary>
    public static class BuildingMeshes
    {
        public static MeshBuilder For(BuildingDefinition definition, bool hasHero)
        {
            var mesh = new MeshBuilder();
            var w = definition.width * GridSpace.CellSize;
            var d = definition.depth * GridSpace.CellSize;
            var plinth = 0.15f;

            mesh.Frustum(Vector3.zero, w - 0.1f, d - 0.1f, w - 0.3f, d - 0.3f, plinth, Palette.DarkStone);
            var floor = Vector3.up * plinth;

            switch (definition.function)
            {
                case BuildingFunction.Headquarters: Pagoda(mesh, floor, w, d); break;
                case BuildingFunction.BuilderHall: Workshop(mesh, floor, w, d); break;
                case BuildingFunction.HeroAltar: Altar(mesh, floor, w, d); break;
                case BuildingFunction.HeroBase: Pedestal(mesh, floor, w, hasHero); break;
                case BuildingFunction.SummoningGate: Gate(mesh, floor, w, d); break;
                case BuildingFunction.Training: TrainingYard(mesh, floor, w, d); break;
                case BuildingFunction.Research: Hut(mesh, floor, w, d); break;
                case BuildingFunction.Collector: Mine(mesh, floor, w, definition.produces); break;
                case BuildingFunction.Storage: Vault(mesh, floor, w, d); break;
                case BuildingFunction.Defense: Tower(mesh, floor, w, definition.id == "spirit_turret"); break;
                default: mesh.Box(floor, new Vector3(w - 0.4f, 1f, d - 0.4f), Palette.Stone); break;
            }
            return mesh;
        }

        private static void Pagoda(MeshBuilder mesh, Vector3 floor, float w, float d)
        {
            var size = Mathf.Min(w, d);
            var y = floor.y;
            mesh.Frustum(new Vector3(0, y, 0), size - 0.3f, size - 0.3f, size - 0.7f, size - 0.7f, 0.35f, Palette.Stone);
            y += 0.35f;
            var scales = new[] { 0.62f, 0.46f, 0.30f };
            foreach (var scale in scales)
            {
                var body = size * scale;
                mesh.Box(new Vector3(0, y, 0), new Vector3(body, 0.85f, body), Palette.Plaster);
                y += 0.85f;
                mesh.Frustum(new Vector3(0, y, 0), body + 1.1f, body + 1.1f, body * 0.45f, body * 0.45f, 0.5f, Palette.Slate);
                y += 0.5f;
            }
            mesh.Frustum(new Vector3(0, y, 0), 0.3f, 0.3f, 0f, 0f, 0.8f, Palette.Amber);
        }

        private static void Workshop(MeshBuilder mesh, Vector3 floor, float w, float d)
        {
            mesh.Box(floor + new Vector3(0, 0, -0.2f), new Vector3(w - 0.9f, 1.0f, d - 1.2f), Palette.Wood);
            mesh.Frustum(floor + new Vector3(0, 1.0f, -0.2f), w - 0.5f, d - 0.8f, 0.3f, d - 1.3f, 0.7f, Palette.Slate);
            mesh.Box(floor + new Vector3(w * 0.32f, 0, d * 0.32f), new Vector3(0.18f, 2.2f, 0.18f), Palette.Wood);
            mesh.Box(floor + new Vector3(w * 0.12f, 2.2f, d * 0.32f), new Vector3(1.2f, 0.14f, 0.14f), Palette.Amber);
        }

        private static void Altar(MeshBuilder mesh, Vector3 floor, float w, float d)
        {
            mesh.Frustum(floor, w - 0.4f, d - 0.4f, w - 1.0f, d - 1.0f, 0.3f, Palette.Stone);
            mesh.Frustum(floor + Vector3.up * 0.3f, w - 1.0f, d - 1.0f, w - 1.7f, d - 1.7f, 0.3f, Palette.Plaster);
            var top = floor + Vector3.up * 0.6f;
            var reach = w * 0.5f - 0.55f;
            foreach (var corner in new[] { new Vector3(reach, 0, reach), new Vector3(-reach, 0, reach), new Vector3(reach, 0, -reach), new Vector3(-reach, 0, -reach) })
                mesh.Box(floor + corner, new Vector3(0.28f, 2.0f, 0.28f), Palette.DarkStone);
            mesh.Box(top, new Vector3(0.7f, 0.5f, 0.7f), Palette.Slate);
            mesh.Frustum(top + Vector3.up * 0.5f, 0.45f, 0.45f, 0f, 0f, 0.8f, Palette.AccentBlue);
        }

        private static void Pedestal(MeshBuilder mesh, Vector3 floor, float w, bool hasHero)
        {
            mesh.Frustum(floor, w - 0.5f, w - 0.5f, w - 0.9f, w - 0.9f, 0.3f, Palette.Stone);
            var top = floor + Vector3.up * 0.3f;
            if (hasHero)
            {
                // Tiny hero placeholder: legs, torso and head blocks.
                mesh.Box(top, new Vector3(0.28f, 0.45f, 0.2f), Palette.Slate);
                mesh.Box(top + Vector3.up * 0.45f, new Vector3(0.4f, 0.5f, 0.26f), Palette.AccentRed);
                mesh.Box(top + Vector3.up * 0.95f, new Vector3(0.24f, 0.24f, 0.24f), Palette.Plaster);
            }
            else
            {
                // Neutral empty marker: a small, unmistakably vacant stone token.
                mesh.Frustum(top, 0.4f, 0.4f, 0.22f, 0.22f, 0.18f, Palette.DarkStone);
            }
        }

        private static void Gate(MeshBuilder mesh, Vector3 floor, float w, float d)
        {
            mesh.Frustum(floor, w - 0.5f, d - 0.5f, w - 0.9f, d - 0.9f, 0.2f, Palette.Stone);
            var ground = floor + Vector3.up * 0.2f;
            var span = w * 0.34f;
            mesh.Box(ground + new Vector3(-span, 0, 0), new Vector3(0.3f, 2.5f, 0.3f), Palette.AccentRed);
            mesh.Box(ground + new Vector3(span, 0, 0), new Vector3(0.3f, 2.5f, 0.3f), Palette.AccentRed);
            mesh.Box(ground + new Vector3(0, 1.9f, 0), new Vector3(span * 2f + 0.3f, 0.18f, 0.2f), Palette.AccentRed);
            mesh.Frustum(ground + new Vector3(0, 2.5f, 0), w - 0.5f, 0.55f, w - 0.2f, 0.4f, 0.26f, Palette.Slate);
        }

        private static void TrainingYard(MeshBuilder mesh, Vector3 floor, float w, float d)
        {
            mesh.Box(floor, new Vector3(w - 0.5f, 0.06f, d - 0.5f), Palette.Plaster);
            var x = w * 0.5f - 0.3f;
            var z = d * 0.5f - 0.3f;
            mesh.Box(floor + new Vector3(0, 0, z), new Vector3(w - 0.5f, 0.45f, 0.1f), Palette.Wood);
            mesh.Box(floor + new Vector3(0, 0, -z), new Vector3(w - 0.5f, 0.45f, 0.1f), Palette.Wood);
            mesh.Box(floor + new Vector3(x, 0, 0), new Vector3(0.1f, 0.45f, d - 0.5f), Palette.Wood);
            mesh.Box(floor + new Vector3(-x, 0, 0), new Vector3(0.1f, 0.45f, d - 0.5f), Palette.Wood);
            foreach (var dummy in new[] { -0.9f, 0f, 0.9f })
            {
                mesh.Box(floor + new Vector3(dummy, 0, 0), new Vector3(0.12f, 1.0f, 0.12f), Palette.Wood);
                mesh.Box(floor + new Vector3(dummy, 0.7f, 0), new Vector3(0.6f, 0.1f, 0.1f), Palette.Wood);
            }
        }

        private static void Hut(MeshBuilder mesh, Vector3 floor, float w, float d)
        {
            mesh.Box(floor, new Vector3(w - 1.0f, 0.9f, d - 1.0f), Palette.Plaster);
            mesh.Frustum(floor + Vector3.up * 0.9f, w - 0.4f, d - 0.4f, 0f, 0f, 1.1f, Palette.Wood);
        }

        private static void Mine(MeshBuilder mesh, Vector3 floor, float w, ResourceKind produces)
        {
            var ore = produces == ResourceKind.Crystals ? Palette.Crystal : Palette.Amber;
            mesh.Frustum(floor, w - 0.5f, w - 0.5f, 0.9f, 0.9f, 0.9f, Palette.Stone);
            mesh.Box(floor + new Vector3(0, 0.9f, 0), new Vector3(0.7f, 0.1f, 0.7f), Palette.Wood);
            mesh.Frustum(floor + new Vector3(0.2f, 1.0f, 0.1f), 0.4f, 0.4f, 0f, 0f, 0.6f, ore);
            mesh.Frustum(floor + new Vector3(-0.2f, 1.0f, -0.15f), 0.3f, 0.3f, 0f, 0f, 0.42f, ore);
        }

        private static void Vault(MeshBuilder mesh, Vector3 floor, float w, float d)
        {
            mesh.Box(floor, new Vector3(w - 0.6f, 1.1f, d - 0.6f), Palette.DarkStone);
            mesh.Frustum(floor + Vector3.up * 1.1f, w - 0.4f, d - 0.4f, w - 1.4f, d - 1.4f, 0.3f, Palette.Slate);
            mesh.Box(floor + new Vector3(0, 0.25f, -(d * 0.5f - 0.3f)), new Vector3(0.7f, 0.6f, 0.05f), Palette.Amber);
        }

        private static void Tower(MeshBuilder mesh, Vector3 floor, float w, bool spirit)
        {
            mesh.Frustum(floor, w - 0.5f, w - 0.5f, w - 0.9f, w - 0.9f, 2.0f, Palette.Stone);
            var top = floor + Vector3.up * 2.0f;
            mesh.Box(top, new Vector3(w - 0.65f, 0.25f, w - 0.65f), Palette.DarkStone);
            if (spirit)
                mesh.Frustum(top + Vector3.up * 0.25f, 0.5f, 0.5f, 0f, 0f, 0.9f, Palette.AccentBlue);
            else
                mesh.Frustum(top + Vector3.up * 0.25f, w - 0.4f, w - 0.4f, 0f, 0f, 0.8f, Palette.AccentRed);
        }
    }
}

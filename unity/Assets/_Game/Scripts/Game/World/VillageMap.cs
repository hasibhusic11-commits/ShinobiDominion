using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// The owner's painted village map (2026-11-09), laid flat on the ground under the village. The 50 x 50
    /// grid runs corner to corner between the four corner torches of the painting, so the whole grass field
    /// and the dirt path around it are the buildable square; the cliffs, waterfalls and forest are scenery.
    ///
    /// The four torches are not quite a parallelogram (the front half of the painting is a little taller than
    /// the back), so the painting is fitted with a bilinear map: each torch lands exactly on its grid corner
    /// and the picture bends smoothly between them. The mesh is built over the picture, and each of its points
    /// is placed on the ground by inverting that map, so the picture's own edges are the mesh's edges.
    /// </summary>
    public static class VillageMap
    {
        public const string TexturePath = "Map/village_map";

        private const float MapSize = VillageGrid.Size * GridSpace.CellSize;
        private const float Root2 = 0.70710678f;
        private const int Columns = 48;
        private const int Rows = 32;

        // The torches' centres in the painting's pixels (y down), measured on the owner's 1536 x 1024 image.
        // Grid (0,0) is nearest the camera, the bottom of the picture; (50,0) is on the right; (0,50) on the left.
        private static readonly Vector2 Near = new Vector2(768f, 858f);
        private static readonly Vector2 Right = new Vector2(1487f, 460f);
        private static readonly Vector2 Far = new Vector2(767f, 100f);
        private static readonly Vector2 Left = new Vector2(47f, 460f);

        /// <summary>The colour of the cloud at the painting's edges, for the camera's background.</summary>
        public static readonly Color EdgeColor = new Color(0.88f, 0.9f, 0.92f);

        /// <summary>Whether the painting is in use, so the camera keeps the view on it.</summary>
        public static bool Loaded { get; private set; }

        /// <summary>The rectangle of the view plane that the painting covers (see <see cref="ToView"/>).</summary>
        public static Vector2 ViewMin { get; private set; }
        public static Vector2 ViewMax { get; private set; }

        /// <summary>
        /// A ground point in the view plane: across the screen and up it, in world units, for the fixed
        /// 45 degree turn and <see cref="IsoCamera.Pitch"/> tilt of the camera.
        /// </summary>
        public static Vector2 ToView(Vector3 ground) =>
            new Vector2(Root2 * (ground.x - ground.z), UpScale * (ground.x + ground.z));

        public static Vector3 FromView(Vector2 view)
        {
            var across = view.x / Root2;
            var along = view.y / UpScale;
            return new Vector3((along + across) * 0.5f, 0f, (along - across) * 0.5f);
        }

        private static float UpScale => Mathf.Sin(IsoCamera.Pitch * Mathf.Deg2Rad) * Root2;

        /// <summary>The largest orthographic size whose view still fits inside the painting.</summary>
        public static float MaxViewSize(float aspect) =>
            Mathf.Min((ViewMax.y - ViewMin.y) * 0.5f, (ViewMax.x - ViewMin.x) * 0.5f / Mathf.Max(aspect, 0.1f));

        public static GameObject Create(Transform parent)
        {
            var texture = Resources.Load<Texture2D>(TexturePath);
            if (texture == null)
            {
                Loaded = false;
                return null;
            }
            texture.wrapMode = TextureWrapMode.Clamp;

            var vertices = new Vector3[(Columns + 1) * (Rows + 1)];
            var uvs = new Vector2[vertices.Length];
            var min = new Vector2(float.MinValue, float.MinValue);
            var max = new Vector2(float.MaxValue, float.MaxValue);
            for (var j = 0; j <= Rows; j++)
            for (var i = 0; i <= Columns; i++)
            {
                var pixel = new Vector2(i / (float)Columns * texture.width, j / (float)Rows * texture.height);
                var grid = GridAt(pixel);
                var ground = new Vector3(grid.x * MapSize, -0.03f, grid.y * MapSize);
                var index = j * (Columns + 1) + i;
                vertices[index] = ground;
                uvs[index] = new Vector2(i / (float)Columns, 1f - j / (float)Rows);

                // The view may go no further than the painting's edges: the tightest of each edge's points.
                var view = ToView(ground);
                if (i == 0) min.x = Mathf.Max(min.x, view.x);
                if (i == Columns) max.x = Mathf.Min(max.x, view.x);
                if (j == 0) max.y = Mathf.Min(max.y, view.y);
                if (j == Rows) min.y = Mathf.Max(min.y, view.y);
            }
            ViewMin = min;
            ViewMax = max;

            var triangles = new int[Columns * Rows * 6];
            var t = 0;
            for (var j = 0; j < Rows; j++)
            for (var i = 0; i < Columns; i++)
            {
                var a = j * (Columns + 1) + i;
                var b = a + 1;
                var c = a + Columns + 1;
                var d = c + 1;
                // Wound to face up: picture rows run toward the camera, so this order keeps the top side visible.
                triangles[t++] = a; triangles[t++] = b; triangles[t++] = c;
                triangles[t++] = b; triangles[t++] = d; triangles[t++] = c;
            }
            var mesh = new Mesh { name = "VillageMap" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            // A face pointing down would be culled; flip once if the winding came out that way.
            if (mesh.normals[0].y < 0f)
            {
                for (var k = 0; k < triangles.Length; k += 3) (triangles[k + 1], triangles[k + 2]) = (triangles[k + 2], triangles[k + 1]);
                mesh.triangles = triangles;
                mesh.RecalculateNormals();
            }

            var go = new GameObject("VillageMap");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "VillageMap" };
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Loaded = true;
            return go;
        }

        /// <summary>The painting's pixel for a grid point (0 to 1 across the map; outside that, the scenery).</summary>
        public static Vector2 PixelAt(float u, float v) =>
            Near + u * (Right - Near) + v * (Left - Near) + u * v * (Near - Right + Far - Left);

        /// <summary>
        /// The grid point (0 to 1 across the map) under a pixel of the painting: the inverse of
        /// <see cref="PixelAt"/>, solved as a quadratic (it also works past the corners, for the scenery).
        /// </summary>
        public static Vector2 GridAt(Vector2 pixel)
        {
            var e = Right - Near;
            var f = Left - Near;
            var g = Near - Right + Far - Left;
            var h = pixel - Near;
            var k2 = Cross(g, f);
            var k1 = Cross(e, f) + Cross(h, g);
            var k0 = Cross(h, e);
            if (Mathf.Abs(k2) < 1e-4f)
            {
                var v = -k0 / k1;
                return new Vector2(SolveU(h, e, f, g, v), v);
            }
            var root = Mathf.Sqrt(Mathf.Max(0f, k1 * k1 - 4f * k0 * k2));
            var v1 = (-k1 - root) / (2f * k2);
            var v2 = (-k1 + root) / (2f * k2);
            // Of the two roots, the one nearer the map is the real one; the other lies far off the picture.
            var v0 = Mathf.Abs(v1 - 0.5f) < Mathf.Abs(v2 - 0.5f) ? v1 : v2;
            return new Vector2(SolveU(h, e, f, g, v0), v0);
        }

        private static float SolveU(Vector2 h, Vector2 e, Vector2 f, Vector2 g, float v)
        {
            var dx = e.x + g.x * v;
            var dy = e.y + g.y * v;
            return Mathf.Abs(dx) > Mathf.Abs(dy) ? (h.x - f.x * v) / dx : (h.y - f.y * v) / dy;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}

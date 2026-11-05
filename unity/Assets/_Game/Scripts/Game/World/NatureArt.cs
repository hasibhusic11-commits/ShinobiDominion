using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// The ground and everything outside the 50 x 50 map. All decoration sits outside the buildable
    /// area, except flat tufts that a building simply covers. Fixed seeds keep the world the same.
    /// </summary>
    public static class VillageScenery
    {
        private const float MapSize = VillageGrid.Size * GridSpace.CellSize;
        private const float GroundExtent = 110f;

        public static GameObject Create(Transform parent)
        {
            var root = new GameObject("Scenery");
            root.transform.SetParent(parent, false);

            Attach(root.transform, "Ground", BuildGround(), Mats.Ground, false, true);
            Attach(root.transform, "Nature", BuildNature().Build("Nature"), Mats.World, true, true);
            Attach(root.transform, "Mountains", BuildMountains().Build("Mountains"), Mats.World, false, false);
            Attach(root.transform, "Water", BuildWater(), Mats.Water, false, false);
            return root.gameObject;
        }

        private static void Attach(Transform parent, string name, Mesh mesh, Material material, bool castShadows, bool receiveShadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = receiveShadows;
        }

        // ---------- Ground ----------

        private static float Smooth(float edge0, float edge1, float x)
        {
            var t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static Mesh BuildGround()
        {
            const int step = 1;
            var min = -(int)GroundExtent;
            var max = (int)(MapSize + GroundExtent);
            var coarse = 4;
            var size = (max - min) / coarse + 1;
            var vertices = new Vector3[size * size];
            var colors = new Color[size * size];
            var normals = new Vector3[size * size];

            for (var iz = 0; iz < size; iz++)
            for (var ix = 0; ix < size; ix++)
            {
                var x = min + ix * coarse;
                var z = min + iz * coarse;
                var index = iz * size + ix;
                vertices[index] = new Vector3(x, x >= 0 && x <= MapSize && z >= 0 && z <= MapSize ? -0.02f : -0.02f, z);
                normals[index] = Vector3.up;
                colors[index] = GroundWeights(x, z);
            }

            var triangles = new System.Collections.Generic.List<int>((size - 1) * (size - 1) * 6);
            for (var iz = 0; iz < size - 1; iz++)
            for (var ix = 0; ix < size - 1; ix++)
            {
                var a = iz * size + ix;
                var b = a + 1;
                var c = a + size + 1;
                var d = a + size;
                triangles.Add(a); triangles.Add(d); triangles.Add(c);
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
            }

            // A second, finer sheet over the map only, so paths have crisp edges where they matter.
            var fine = (int)MapSize + 1;
            var offset = vertices.Length;
            var allVertices = new Vector3[offset + fine * fine];
            var allColors = new Color[offset + fine * fine];
            var allNormals = new Vector3[offset + fine * fine];
            System.Array.Copy(vertices, allVertices, offset);
            System.Array.Copy(colors, allColors, offset);
            System.Array.Copy(normals, allNormals, offset);
            for (var iz = 0; iz < fine; iz++)
            for (var ix = 0; ix < fine; ix++)
            {
                var index = offset + iz * fine + ix;
                allVertices[index] = new Vector3(ix * step, 0f, iz * step);
                allNormals[index] = Vector3.up;
                allColors[index] = GroundWeights(ix, iz);
            }
            for (var iz = 0; iz < fine - 1; iz++)
            for (var ix = 0; ix < fine - 1; ix++)
            {
                var a = offset + iz * fine + ix;
                var b = a + 1;
                var c = a + fine + 1;
                var d = a + fine;
                triangles.Add(a); triangles.Add(d); triangles.Add(c);
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
            }

            var mesh = new Mesh { name = "Ground", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(allVertices);
            mesh.SetNormals(allNormals);
            mesh.SetColors(allColors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Red = dirt, green = stone, blue = moss. Paths are decoration: they never affect placement.</summary>
        private static Color GroundWeights(float x, float z)
        {
            var c = new Vector2(x - MapSize * 0.5f, z - MapSize * 0.5f);
            var noise = Mathf.PerlinNoise(x * 0.09f + 5.3f, z * 0.09f + 1.7f);
            var fineNoise = Mathf.PerlinNoise(x * 0.27f + 9.1f, z * 0.27f + 3.3f);

            // A worn courtyard around the centre and packed paths leading out to the map edge.
            var courtyard = 1f - Smooth(7f, 11.5f, c.magnitude + (noise - 0.5f) * 6f);
            var pathA = 1f - Smooth(0.5f, 1.5f, Mathf.Abs(c.x + Mathf.Sin(z * 0.12f) * 2.5f) + (fineNoise - 0.5f) * 0.8f);
            var pathB = 1f - Smooth(0.5f, 1.5f, Mathf.Abs(c.y + Mathf.Sin(x * 0.1f) * 2.5f) + (fineNoise - 0.5f) * 0.8f);
            var inMap = x >= 0f && x <= MapSize && z >= 0f && z <= MapSize;
            var dirt = inMap ? Mathf.Max(courtyard * 0.85f, Mathf.Max(pathA, pathB) * 0.72f) : 0f;
            dirt = Mathf.Max(dirt, Smooth(0.76f, 0.9f, Mathf.PerlinNoise(x * 0.13f + 40f, z * 0.13f + 21f)) * 0.55f);

            var distance = Mathf.Max(Mathf.Max(-x, x - MapSize), Mathf.Max(-z, z - MapSize));
            var moss = Mathf.Clamp01(Smooth(0.5f, 7f, distance) * 0.8f + Smooth(0.62f, 0.85f, noise) * 0.2f);
            return new Color(dirt, 0f, moss, 1f);
        }

        // ---------- Plants, rocks and the map's edge ----------

        private static MeshBuilder BuildNature()
        {
            var mesh = new MeshBuilder { ContactShade = true };
            var random = new System.Random(5150);

            BoundaryKerb(mesh);
            AddWaterRocks(mesh);

            // Trees grow in dense clumps, with room to see through them.
            var density = GameQuality.Current == VisualQuality.High ? 1f : 0.5f;
            for (var i = 0; i < (int)(1700 * density); i++)
            {
                var x = (float)(random.NextDouble() * (MapSize + 76f) - 38f);
                var z = (float)(random.NextDouble() * (MapSize + 76f) - 38f);
                var distance = Mathf.Max(Mathf.Max(-x, x - MapSize), Mathf.Max(-z, z - MapSize));
                if (distance < (x < 0f || z < 0f ? 4f : 1.8f)) continue;
                if (InWaterArea(x, z)) continue;
                // Thick forest near the map, thinning out only in occasional clearings.
                var clump = Mathf.PerlinNoise(x * 0.07f + 11f, z * 0.07f + 4f);
                if (clump < 0.36f) continue;

                // Tall trees on the camera's side would hide the village, so they are held back.
                var inFront = x < 0f || z < 0f;
                var scale = (0.9f + (float)random.NextDouble() * 0.9f) * (inFront ? Mathf.Lerp(0.4f, 0.95f, Smooth(4f, 18f, distance)) : 1.25f);
                var at = new Vector3(x, 0f, z);
                var seed = random.Next();
                var kind = random.NextDouble();
                var region = Mathf.PerlinNoise(x * 0.03f + 2f, z * 0.03f + 9f);
                if (kind < 0.48f) PineTree(mesh, at, scale, seed);
                else if (kind < 0.72f) BroadTree(mesh, at, scale, seed, region > 0.56f ? Palette.Autumn : Palette.Leaf, region > 0.56f ? new Color(1f, 0.72f, 0.28f) : Palette.LeafLight);
                else if (kind < 0.88f) BroadTree(mesh, at, scale * 0.9f, seed, Palette.Sakura, Palette.SakuraLight);
                else Bamboo(mesh, at, scale, seed);
            }

            // Rock formations along the map's edge.
            for (var i = 0; i < 90; i++)
            {
                var x = (float)(random.NextDouble() * (MapSize + 70f) - 35f);
                var z = (float)(random.NextDouble() * (MapSize + 70f) - 35f);
                var distance = Mathf.Max(Mathf.Max(-x, x - MapSize), Mathf.Max(-z, z - MapSize));
                if (distance < 1.3f || InWaterArea(x, z)) continue;
                Boulder(mesh, new Vector3(x, 0f, z), 0.35f + (float)random.NextDouble() * 0.85f, random.Next());
            }

            // Flowers and tufts. Inside the map they are flat enough for any building to cover.
            for (var i = 0; i < (int)(2200 * density); i++)
            {
                var x = (float)(random.NextDouble() * (MapSize + 40f) - 20f);
                var z = (float)(random.NextDouble() * (MapSize + 40f) - 20f);
                if (InWaterArea(x, z)) continue;
                var weights = GroundWeights(x, z);
                if (weights.r > 0.35f) continue;
                var at = new Vector3(x, 0f, z);
                if (random.NextDouble() < 0.72f) Tuft(mesh, at, random);
                else Flower(mesh, at, random);
            }
            return mesh;
        }

        private static void BoundaryKerb(MeshBuilder mesh)
        {
            // A low stone kerb with lantern posts marks the buildable edge without a visible grid.
            const float thick = 0.34f;
            var mid = MapSize * 0.5f;
            var length = MapSize + thick * 2f;
            mesh.Box(new Vector3(mid, 0, -thick * 0.5f), new Vector3(length, 0.16f, thick), Palette.Stone, ArtSurface.Stone);
            mesh.Box(new Vector3(mid, 0, MapSize + thick * 0.5f), new Vector3(length, 0.16f, thick), Palette.Stone, ArtSurface.Stone);
            mesh.Box(new Vector3(-thick * 0.5f, 0, mid), new Vector3(thick, 0.16f, MapSize), Palette.Stone, ArtSurface.Stone);
            mesh.Box(new Vector3(MapSize + thick * 0.5f, 0, mid), new Vector3(thick, 0.16f, MapSize), Palette.Stone, ArtSurface.Stone);
            for (var i = 0; i <= 5; i++)
            {
                var t = i * MapSize / 5f;
                EdgePost(mesh, new Vector3(t, 0f, -thick * 0.5f));
                EdgePost(mesh, new Vector3(t, 0f, MapSize + thick * 0.5f));
                if (i != 0 && i != 5)
                {
                    EdgePost(mesh, new Vector3(-thick * 0.5f, 0f, t));
                    EdgePost(mesh, new Vector3(MapSize + thick * 0.5f, 0f, t));
                }
            }
        }

        private static void EdgePost(MeshBuilder mesh, Vector3 at)
        {
            mesh.Box(at, new Vector3(0.42f, 0.5f, 0.42f), Palette.DarkStone, ArtSurface.Stone);
            mesh.Box(at + Vector3.up * 0.5f, new Vector3(0.26f, 0.2f, 0.26f), Palette.WindowGlow, ArtSurface.Plain, 0f, 0.9f);
            mesh.Frustum(at + Vector3.up * 0.7f, 0.52f, 0.52f, 0.06f, 0.06f, 0.18f, Palette.Slate, ArtSurface.Tile);
        }

        private static void PineTree(MeshBuilder mesh, Vector3 at, float scale, int seed)
        {
            var random = new System.Random(seed);
            var height = (2.8f + (float)random.NextDouble() * 1.4f) * scale;
            mesh.Cylinder(at, 0.17f * scale, 0.09f * scale, height * 0.42f, 4, Palette.DarkWood, ArtSurface.Wood, 0.05f, 0f, false);
            const int tiers = 3;
            for (var tier = 0; tier < tiers; tier++)
            {
                var t = (float)tier / tiers;
                var radius = Mathf.Lerp(1.05f, 0.42f, t) * scale;
                var baseY = height * (0.24f + t * 0.62f);
                var tierHeight = height * 0.44f;
                var tint = Color.Lerp(Palette.Pine, Palette.PineLight, t + (float)random.NextDouble() * 0.2f);
                mesh.Cylinder(at + Vector3.up * baseY, radius, 0f, tierHeight, 6, tint, ArtSurface.Grass, 0.3f + t * 0.7f);
            }
        }

        private static void BroadTree(MeshBuilder mesh, Vector3 at, float scale, int seed, Color color, Color top)
        {
            var random = new System.Random(seed);
            var height = (1.25f + (float)random.NextDouble() * 0.5f) * scale;
            mesh.Cylinder(at, 0.2f * scale, 0.11f * scale, height, 4, Palette.DarkWood, ArtSurface.Wood, 0.05f, 0f, false);
            var blobs = 3;
            mesh.Blob(at + Vector3.up * (height + 0.5f * scale), new Vector3(1.05f, 0.85f, 1.05f) * scale, color, ArtSurface.Grass, 0.6f, 0f, 7, 4, 0.1f, seed, top);
            for (var i = 0; i < blobs; i++)
            {
                var a = (float)random.NextDouble() * Mathf.PI * 2f;
                var offset = new Vector3(Mathf.Cos(a), (float)random.NextDouble() * 0.5f - 0.1f, Mathf.Sin(a)) * 0.72f * scale;
                var radius = (0.46f + (float)random.NextDouble() * 0.3f) * scale;
                mesh.Blob(at + Vector3.up * (height + 0.5f * scale) + offset, Vector3.one * radius, color, ArtSurface.Grass, 0.8f, 0f, 6, 3, 0.1f, seed + i + 1, top);
            }
        }

        private static void Bamboo(MeshBuilder mesh, Vector3 at, float scale, int seed)
        {
            var random = new System.Random(seed);
            var stalks = 4 + random.Next(3);
            for (var i = 0; i < stalks; i++)
            {
                var offset = new Vector3((float)random.NextDouble() - 0.5f, 0f, (float)random.NextDouble() - 0.5f) * 1.1f * scale;
                var height = (2.0f + (float)random.NextDouble() * 1.8f) * scale;
                mesh.Cylinder(at + offset, 0.05f * scale, 0.035f * scale, height, 3, Palette.Bamboo, ArtSurface.Plain, 0.5f, 0f, false);
                mesh.Blob(at + offset + Vector3.up * height, new Vector3(0.32f, 0.16f, 0.32f) * scale, Palette.LeafLight, ArtSurface.Grass, 1f, 0f, 4, 2);
            }
        }

        private static void Boulder(MeshBuilder mesh, Vector3 at, float scale, int seed)
        {
            var random = new System.Random(seed);
            var tone = Color.Lerp(new Color(0.52f, 0.52f, 0.54f), new Color(0.66f, 0.62f, 0.56f), (float)random.NextDouble());
            mesh.Blob(at + Vector3.up * 0.2f * scale, new Vector3(0.62f, 0.5f, 0.54f) * scale, tone, ArtSurface.Stone, 0f, 0f, 7, 4, 0.16f, seed, Color.Lerp(tone, Color.white, 0.2f));
            if (random.NextDouble() < 0.6)
                mesh.Blob(at + new Vector3(0.42f, 0.08f, 0.2f) * scale, new Vector3(0.34f, 0.26f, 0.3f) * scale, tone, ArtSurface.Stone, 0f, 0f, 6, 3, 0.16f, seed + 1);
            if (random.NextDouble() < 0.5)
                mesh.Blob(at + new Vector3(0f, 0.5f, 0f) * scale, new Vector3(0.36f, 0.14f, 0.32f) * scale, Palette.Leaf, ArtSurface.Grass, 0f, 0f, 5, 3);
        }

        private static void Tuft(MeshBuilder mesh, Vector3 at, System.Random random)
        {
            var tint = Color.Lerp(new Color(0.36f, 0.58f, 0.26f), new Color(0.62f, 0.74f, 0.32f), (float)random.NextDouble());
            for (var i = 0; i < 2; i++)
            {
                var offset = new Vector3((float)random.NextDouble() - 0.5f, 0f, (float)random.NextDouble() - 0.5f) * 0.16f;
                mesh.Cylinder(at + offset, 0.05f, 0f, 0.15f + (float)random.NextDouble() * 0.1f, 3, tint, ArtSurface.Plain, 0.7f, 0f, false);
            }
        }

        private static readonly Color[] FlowerColors =
        {
            new Color(1f, 0.82f, 0.28f), new Color(1f, 0.56f, 0.70f), new Color(0.98f, 0.96f, 0.92f), new Color(0.66f, 0.56f, 1f)
        };

        private static void Flower(MeshBuilder mesh, Vector3 at, System.Random random)
        {
            var color = FlowerColors[random.Next(FlowerColors.Length)];
            mesh.Cylinder(at, 0.016f, 0.016f, 0.14f, 3, Palette.Leaf, ArtSurface.Plain, 0.5f, 0f, false);
            mesh.Blob(at + Vector3.up * 0.15f, new Vector3(0.08f, 0.04f, 0.08f), color, ArtSurface.Plain, 0.5f, 0f, 5, 2);
        }

        // ---------- Mountains ----------

        private static MeshBuilder BuildMountains()
        {
            var mesh = new MeshBuilder { ContactShade = false };
            var random = new System.Random(808);

            // Distant ridges behind the map, fading into the haze.
            for (var i = 0; i < 60; i++)
            {
                var along = (float)random.NextDouble();
                var depth = (float)random.NextDouble();
                var back = random.NextDouble() < 0.5;
                var x = back ? Mathf.Lerp(-70f, MapSize + 70f, along) : MapSize + 38f + depth * 36f;
                var z = back ? MapSize + 38f + depth * 36f : Mathf.Lerp(-70f, MapSize + 70f, along);
                Peak(mesh, new Vector3(x, -0.1f, z), 10f + (float)random.NextDouble() * 12f, 14f + (float)random.NextDouble() * 16f + depth * 8f, depth, random.Next());
            }
            return mesh;
        }

        private static void Peak(MeshBuilder mesh, Vector3 at, float radius, float height, float haze, int seed)
        {
            var random = new System.Random(seed);
            const int sides = 9;
            const int rows = 4;
            var offsets = new float[sides, rows + 1];
            for (var s = 0; s < sides; s++)
            for (var r = 0; r <= rows; r++)
                offsets[s, r] = 1f + (float)(random.NextDouble() - 0.5) * 0.3f;

            var lowColor = Color.Lerp(new Color(0.26f, 0.48f, 0.38f), Palette.Sky, 0.12f + haze * 0.5f);
            var midColor = Color.Lerp(new Color(0.40f, 0.46f, 0.62f), Palette.Sky, 0.12f + haze * 0.5f);
            var snow = Color.Lerp(new Color(0.96f, 0.97f, 1f), Palette.Sky, haze * 0.25f);

            Vector3 Point(int s, int r)
            {
                var t = (float)r / rows;
                var a = (float)s / sides * Mathf.PI * 2f;
                var ring = radius * (1f - t) * offsets[s % sides, r];
                return at + new Vector3(Mathf.Cos(a) * ring, height * t * (r == rows ? 1f : offsets[s % sides, r]), Mathf.Sin(a) * ring);
            }

            for (var r = 0; r < rows; r++)
            for (var s = 0; s < sides; s++)
            {
                var t = (float)r / rows;
                var color = t < 0.3f ? lowColor : t < 0.7f ? midColor : snow;
                var a = Point(s, r);
                var b = Point(s + 1, r);
                var c = Point(s + 1, r + 1);
                var d = Point(s, r + 1);
                var outward = (a + b) * 0.5f - at;
                outward.y = 0f;
                mesh.Quad(a, b, c, d, outward + Vector3.up, color, r < rows - 1 ? ArtSurface.Stone : ArtSurface.Plain);
            }
        }

        // ---------- Water: a stream, a pond and a waterfall behind the village ----------

        private static readonly Vector3 PondCenter = new Vector3(13f, 0f, MapSize + 12f);
        private const float PondRadius = 8.5f;

        private static bool InWaterArea(float x, float z)
        {
            var dx = x - PondCenter.x;
            var dz = z - PondCenter.z;
            if (dx * dx + dz * dz < (PondRadius + 2.5f) * (PondRadius + 2.5f)) return true;
            // The stream running from the pond along the map's back edge.
            if (z > MapSize + 2f && z < MapSize + 8f && x > PondCenter.x - 2f && x < MapSize + 40f) return true;
            // The waterfall and the cliff it falls from.
            return x > PondCenter.x - 7f && x < PondCenter.x + 7f && z > PondCenter.z + 5f && z < PondCenter.z + 22f;
        }

        private static Mesh BuildWater()
        {
            var vertices = new System.Collections.Generic.List<Vector3>();
            var normals = new System.Collections.Generic.List<Vector3>();
            var colors = new System.Collections.Generic.List<Color>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var flows = new System.Collections.Generic.List<Vector2>();
            var triangles = new System.Collections.Generic.List<int>();

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uvA, Vector2 uvC, float speed, float shoreA, float shoreC)
            {
                var start = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                normals.AddRange(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
                colors.AddRange(new[] { new Color(shoreA, 0, 0), new Color(shoreA, 0, 0), new Color(shoreC, 0, 0), new Color(shoreC, 0, 0) });
                if (speed >= 7f) uvs.AddRange(new[] { uvA, new Vector2(uvC.x, uvA.y), uvC, new Vector2(uvA.x, uvC.y) });
                else uvs.AddRange(new[] { new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(c.x, c.z), new Vector2(d.x, d.z) });
                flows.AddRange(new[] { new Vector2(speed, 0), new Vector2(speed, 0), new Vector2(speed, 0), new Vector2(speed, 0) });
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
                triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
            }

            // Pond, as rings so the shore can fade to foam.
            const int points = 28;
            for (var i = 0; i < points; i++)
            {
                var a0 = (float)i / points * Mathf.PI * 2f;
                var a1 = (float)(i + 1) / points * Mathf.PI * 2f;
                var wobble0 = 1f + Mathf.Sin(a0 * 3f) * 0.08f;
                var wobble1 = 1f + Mathf.Sin(a1 * 3f) * 0.08f;
                var d0 = new Vector3(Mathf.Cos(a0) * wobble0, 0f, Mathf.Sin(a0) * wobble0);
                var d1 = new Vector3(Mathf.Cos(a1) * wobble1, 0f, Mathf.Sin(a1) * wobble1);
                var c = PondCenter + Vector3.up * 0.015f;
                var inner0 = c + d0 * PondRadius * 0.82f;
                var inner1 = c + d1 * PondRadius * 0.82f;
                var outer0 = c + d0 * PondRadius;
                var outer1 = c + d1 * PondRadius;
                Quad(c, c, inner1, inner0, new Vector2(c.x, c.z), new Vector2(inner1.x, inner1.z), 0.5f, 0f, 0f);
                Quad(inner0, inner1, outer1, outer0, new Vector2(inner0.x, inner0.z), new Vector2(outer1.x, outer1.z), 0.5f, 0f, 1f);
            }

            // The stream, running along the back of the map into the pond.
            for (var i = 0; i < 28; i++)
            {
                float X(int k) => PondCenter.x + 2f + k * ((MapSize + 40f - PondCenter.x - 2f) / 28f);
                float Z(float x) => MapSize + 5f + Mathf.Sin(x * 0.11f) * 1.3f;
                var x0 = X(i);
                var x1 = X(i + 1);
                var z0 = Z(x0);
                var z1 = Z(x1);
                const float half = 1.5f;
                const float y = 0.012f;
                Quad(new Vector3(x0, y, z0 - half), new Vector3(x1, y, z1 - half), new Vector3(x1, y, z1), new Vector3(x0, y, z0),
                    new Vector2(x0, z0 - half), new Vector2(x1, z1), 1.8f, 0.9f, 0f);
                Quad(new Vector3(x0, y, z0), new Vector3(x1, y, z1), new Vector3(x1, y, z1 + half), new Vector3(x0, y, z0 + half),
                    new Vector2(x0, z0), new Vector2(x1, z1 + half), 1.8f, 0f, 0.9f);
            }

            // The waterfall: a steep strip with fast flow, and a churned patch where it lands.
            var fallBottom = PondCenter + new Vector3(0f, 0.03f, 7.3f);
            var fallTop = fallBottom + new Vector3(0f, 11.6f, 3.6f);
            Quad(fallBottom + Vector3.left * 3f, fallBottom + Vector3.right * 3f, fallTop + Vector3.right * 2.2f, fallTop + Vector3.left * 2.2f,
                new Vector2(0f, 0f), new Vector2(30f, 12f), 7f, 0.75f, 0.45f);
            Quad(fallBottom + new Vector3(-3.6f, 0f, -3f), fallBottom + new Vector3(3.6f, 0f, -3f), fallBottom + new Vector3(3f, 0f, 0.1f), fallBottom + new Vector3(-3f, 0f, 0.1f),
                new Vector2(0f, 0f), new Vector2(2f, 1f), 3f, 1f, 1f);

            var mesh = new Mesh { name = "Water", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, flows);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The cliff behind the waterfall and rocks around the pond.</summary>
        public static void AddWaterRocks(MeshBuilder mesh)
        {
            var random = new System.Random(77);
            var cliff = PondCenter + new Vector3(0f, -0.1f, 15f);
            var rock = new Color(0.64f, 0.60f, 0.54f);
            mesh.Frustum(cliff, 22f, 15f, 12f, 7f, 12.5f, rock, ArtSurface.Stone);
            mesh.Frustum(cliff + Vector3.up * 12.5f, 13f, 8f, 10f, 6f, 0.6f, Palette.Leaf, ArtSurface.Grass);
            // Layered rock shelves and foliage break up the slab either side of the falls.
            foreach (var side in new[] { -1f, 1f })
            {
                mesh.Frustum(cliff + new Vector3(side * 7.5f, 0f, -6f), 7f, 6f, 4f, 4f, 7.5f, rock * 0.92f + new Color(0f, 0f, 0f, 0.08f), ArtSurface.Stone);
                mesh.Frustum(cliff + new Vector3(side * 7.5f, 7.5f, -6f), 4.6f, 4.6f, 3.4f, 3.4f, 0.5f, Palette.Leaf, ArtSurface.Grass);
                PineTree(mesh, cliff + new Vector3(side * 7.5f, 8f, -6f), 1.1f, 40 + (int)side);
                BroadTree(mesh, cliff + new Vector3(side * 5f, 13f, 0f), 1.1f, 50 + (int)side, Palette.Sakura, Palette.SakuraLight);
                PineTree(mesh, cliff + new Vector3(side * 3.2f, 13f, 2f), 1.2f, 60 + (int)side);
            }
            for (var i = 0; i < 30; i++)
            {
                var a = (float)random.NextDouble() * Mathf.PI * 2f;
                var at = PondCenter + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (PondRadius + (float)random.NextDouble() * 1.5f);
                Boulder(mesh, at, 0.5f + (float)random.NextDouble() * 1.1f, random.Next());
            }
        }
    }
}

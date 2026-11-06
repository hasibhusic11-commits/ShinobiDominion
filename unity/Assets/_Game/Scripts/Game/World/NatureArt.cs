using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// The mountain the village is built on. Height is 0 across the whole map, so it never affects
    /// placement. Behind the map the ground climbs through forest and bare rock to snow. In front,
    /// on the camera's side, it falls away as a cliff, so nothing out there can hide the village.
    /// </summary>
    public static class VillageTerrain
    {
        private const float MapSize = VillageGrid.Size * GridSpace.CellSize;
        public const float TreeLine = 10f;

        public static readonly Vector3 PondCenter = new Vector3(13f, 0f, MapSize + 12f);
        public const float PondRadius = 8.5f;

        public static float Smooth(float edge0, float edge1, float x)
        {
            var t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static float Noise(float x, float z, float scale, float seed) =>
            Mathf.PerlinNoise(x * scale + 200f + seed, z * scale + 200f + seed * 1.7f);

        private static float Ridged(float x, float z)
        {
            var a = 1f - Mathf.Abs(2f * Noise(x, z, 0.045f, 17.3f) - 1f);
            var b = 1f - Mathf.Abs(2f * Noise(x, z, 0.11f, 3.7f) - 1f);
            return a * a * 0.62f + b * b * 0.28f + Noise(x, z, 0.27f, 41.1f) * 0.1f;
        }

        public static float Height(float x, float z)
        {
            var behind = Mathf.Max(x - MapSize, z - MapSize);
            var front = Mathf.Max(-x, -z);
            var warp = Noise(x, z, 0.035f, 3.1f) - 0.5f;

            // The climb. Warp is at most 3 here, so the ground stays flat for 8 units behind the map.
            var climb = behind + warp * 6f;
            var rise = 30f * Smooth(10f, 28f, climb) + 26f * Smooth(26f, 60f, climb);
            rise = Mathf.Max(0f, rise + (Ridged(x, z) - 0.38f) * 30f * Smooth(12f, 30f, climb));

            // A level basin for the pond, opening up the slope to the waterfall's cliff.
            var alongFall = Mathf.Clamp(z, PondCenter.z, PondCenter.z + 14f);
            var toFall = new Vector2(x - PondCenter.x, z - alongFall).magnitude;
            rise *= Smooth(PondRadius + 3f, PondRadius + 9f, toFall);

            // The drop, in ledges. It fades out to the sides, where the slopes wrap around the map.
            var edge = front + warp * 3f;
            var drop = 13f * Smooth(4.5f, 10f, edge) + 7f * Smooth(16f, 22f, edge) + 4f * Smooth(30f, 50f, edge);
            drop += (Noise(x, z, 0.09f, 11.9f) - 0.5f) * 3.5f * Smooth(5f, 11f, edge);
            drop *= 1f - Smooth(2f, 28f, behind);

            return rise - drop;
        }

        /// <summary>Rise over run: 1 is a 45 degree slope.</summary>
        public static float Slope(float x, float z)
        {
            var dx = Height(x + 1f, z) - Height(x - 1f, z);
            var dz = Height(x, z + 1f) - Height(x, z - 1f);
            return new Vector2(dx, dz).magnitude * 0.5f;
        }

        public static Vector3 Normal(float x, float z)
        {
            var dx = Height(x + 1f, z) - Height(x - 1f, z);
            var dz = Height(x, z + 1f) - Height(x, z - 1f);
            return new Vector3(-dx, 2f, -dz).normalized;
        }
    }

    /// <summary>
    /// The ground and everything outside the 50 x 50 map. All decoration sits outside the buildable
    /// area, except flat tufts that a building simply covers. Fixed seeds keep the world the same.
    /// </summary>
    public static class VillageScenery
    {
        private const float MapSize = VillageGrid.Size * GridSpace.CellSize;

        public static GameObject Create(Transform parent)
        {
            var root = new GameObject("Scenery");
            root.transform.SetParent(parent, false);

            Attach(root.transform, "Ground", BuildGround(), Mats.Ground, false, true);
            Attach(root.transform, "Nature", BuildNature().Build("Nature"), Mats.World, true, true);
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

        private static float Smooth(float edge0, float edge1, float x) => VillageTerrain.Smooth(edge0, edge1, x);

        private static Mesh BuildGround()
        {
            const int step = 1;
            const int coarse = 2;
            const int min = -70;
            var max = (int)MapSize + 110;
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
                vertices[index] = new Vector3(x, VillageTerrain.Height(x, z) - 0.02f, z);
                normals[index] = VillageTerrain.Normal(x, z);
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

        /// <summary>
        /// Red = dirt, green = bare rock, blue = moss, alpha = snow. Paths are decoration: they never
        /// affect placement.
        /// </summary>
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
            var height = VillageTerrain.Height(x, z);
            var slope = VillageTerrain.Slope(x, z);
            var rock = Mathf.Max(Smooth(1.05f, 1.7f, slope), Smooth(6f, 11f, height + noise * 4f));
            var snow = Smooth(14f, 19f, height + noise * 5f) * (1f - Smooth(1.6f, 2.4f, slope) * 0.7f);
            return new Color(dirt, rock, moss, snow);
        }

        // ---------- Plants, rocks and the map's edge ----------

        private static MeshBuilder BuildNature()
        {
            var mesh = new MeshBuilder { ContactShade = true };
            var random = new System.Random(5150);

            BoundaryKerb(mesh);
            AddWaterRocks(mesh);

            // Trees grow in dense clumps and thin out towards the bare rock.
            var density = GameQuality.Current == VisualQuality.High ? 1f : 0.5f;
            for (var i = 0; i < (int)(2600 * density); i++)
            {
                var x = (float)(random.NextDouble() * (MapSize + 120f) - 60f);
                var z = (float)(random.NextDouble() * (MapSize + 120f) - 60f);
                var distance = Mathf.Max(Mathf.Max(-x, x - MapSize), Mathf.Max(-z, z - MapSize));
                if (distance < (x < 0f || z < 0f ? 4f : 1.8f)) continue;
                if (InWaterArea(x, z)) continue;
                var clump = Mathf.PerlinNoise(x * 0.07f + 111f, z * 0.07f + 104f);
                if (clump < 0.36f) continue;
                var height = VillageTerrain.Height(x, z);
                if (height > VillageTerrain.TreeLine || VillageTerrain.Slope(x, z) > 1.5f) continue;
                if (height > 5f && random.NextDouble() < (height - 5f) / 5f) continue;

                // In front of the map, only trees whose tops stay below the map's level are allowed,
                // down in the forest under the cliff. Those can never hide the village.
                var inFront = (x < 0f || z < 0f) && Mathf.Max(x, z) - MapSize < 6f;
                if (inFront && height > -7f) continue;

                var scale = (0.9f + (float)random.NextDouble() * 0.9f) * (inFront ? 0.95f : 1.25f);
                var at = new Vector3(x, height, z);
                var seed = random.Next();
                var kind = height > 3f ? 0.0 : random.NextDouble();
                var region = Mathf.PerlinNoise(x * 0.03f + 102f, z * 0.03f + 109f);
                if (kind < 0.48f) PineTree(mesh, at, scale, seed);
                else if (kind < 0.72f) BroadTree(mesh, at, scale, seed, region > 0.56f ? Palette.Autumn : Palette.Leaf, region > 0.56f ? new Color(1f, 0.72f, 0.28f) : Palette.LeafLight);
                else if (kind < 0.88f) BroadTree(mesh, at, scale * 0.9f, seed, Palette.Sakura, Palette.SakuraLight);
                else Bamboo(mesh, at, scale, seed);
            }

            // Rock: scattered across the slopes, larger the higher it sits.
            for (var i = 0; i < 300; i++)
            {
                var x = (float)(random.NextDouble() * (MapSize + 120f) - 60f);
                var z = (float)(random.NextDouble() * (MapSize + 120f) - 60f);
                var distance = Mathf.Max(Mathf.Max(-x, x - MapSize), Mathf.Max(-z, z - MapSize));
                if (distance < 2.5f || InWaterArea(x, z)) continue;
                var height = VillageTerrain.Height(x, z);
                var scale = (0.35f + (float)random.NextDouble() * 0.85f) * (1f + Mathf.Clamp01(height / 10f) * 2.5f);
                var inFront = (x < 0f || z < 0f) && Mathf.Max(x, z) - MapSize < 6f;
                if (inFront && height > -0.5f) scale = Mathf.Min(scale, 0.5f);
                else if (inFront && height + scale * 0.9f > -0.2f) continue;
                Boulder(mesh, new Vector3(x, height, z), scale, random.Next());
            }

            // Crags jutting from the cliff face below the map's front edges.
            for (var i = 0; i < 160; i++)
            {
                var along = (float)random.NextDouble() * (MapSize + 12f) - 6f;
                var out_ = 5.5f + (float)random.NextDouble() * 7f;
                var onLeft = random.NextDouble() < 0.5;
                var x = onLeft ? -out_ : along;
                var z = onLeft ? along : -out_;
                var height = VillageTerrain.Height(x, z);
                var scale = 1.2f + (float)random.NextDouble() * 1.5f;
                if (height + scale * 0.8f > -0.2f) continue;
                Crag(mesh, new Vector3(x, height, z), scale, random.Next());
            }

            // Flowers and tufts. Inside the map they are flat enough for any building to cover.
            for (var i = 0; i < (int)(2200 * density); i++)
            {
                var x = (float)(random.NextDouble() * (MapSize + 40f) - 20f);
                var z = (float)(random.NextDouble() * (MapSize + 40f) - 20f);
                if (InWaterArea(x, z)) continue;
                var weights = GroundWeights(x, z);
                if (weights.r > 0.35f || weights.g > 0.3f) continue;
                var at = new Vector3(x, VillageTerrain.Height(x, z), z);
                if (random.NextDouble() < 0.72f) Tuft(mesh, at, random);
                else Flower(mesh, at, random);
            }

            // Boulders where the stream comes out of the mountainside.
            for (var i = 0; i < 12; i++)
            {
                var x = MapSize + 7f + (float)random.NextDouble() * 4f;
                var z = MapSize + 2f + (float)random.NextDouble() * 6f;
                Boulder(mesh, new Vector3(x, VillageTerrain.Height(x, z), z), 0.8f + (float)random.NextDouble() * 1.4f, random.Next());
            }
            return mesh;
        }

        private static void Crag(MeshBuilder mesh, Vector3 at, float scale, int seed)
        {
            var tone = new Color(0.50f, 0.51f, 0.56f);
            mesh.Blob(at, new Vector3(0.9f, 0.8f, 0.9f) * scale, tone * 0.82f + new Color(0f, 0f, 0f, 0.18f), ArtSurface.Rock, 0f, 0f, 6, 4, 0.22f, seed, tone);
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
            var tone = Color.Lerp(new Color(0.46f, 0.47f, 0.52f), new Color(0.58f, 0.58f, 0.60f), (float)random.NextDouble());
            mesh.Blob(at + Vector3.up * 0.2f * scale, new Vector3(0.62f, 0.5f, 0.54f) * scale, tone, ArtSurface.Rock, 0f, 0f, 7, 4, 0.16f, seed, Color.Lerp(tone, Color.white, 0.2f));
            if (random.NextDouble() < 0.6)
                mesh.Blob(at + new Vector3(0.42f, 0.08f, 0.2f) * scale, new Vector3(0.34f, 0.26f, 0.3f) * scale, tone, ArtSurface.Rock, 0f, 0f, 6, 3, 0.16f, seed + 1);
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

        // ---------- Water: a stream, a pond and a waterfall behind the village ----------

        private static readonly Vector3 PondCenter = VillageTerrain.PondCenter;
        private const float PondRadius = VillageTerrain.PondRadius;

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
            var rock = new Color(0.50f, 0.51f, 0.56f);
            mesh.Frustum(cliff, 22f, 15f, 12f, 7f, 12.5f, rock, ArtSurface.Rock);
            mesh.Frustum(cliff + Vector3.up * 12.5f, 13f, 8f, 10f, 6f, 0.6f, Palette.Leaf, ArtSurface.Grass);
            // Layered rock shelves and foliage break up the slab either side of the falls.
            foreach (var side in new[] { -1f, 1f })
            {
                mesh.Frustum(cliff + new Vector3(side * 7.5f, 0f, -6f), 7f, 6f, 4f, 4f, 7.5f, rock * 0.92f + new Color(0f, 0f, 0f, 0.08f), ArtSurface.Rock);
                mesh.Frustum(cliff + new Vector3(side * 7.5f, 7.5f, -6f), 4.6f, 4.6f, 3.4f, 3.4f, 0.5f, Palette.Leaf, ArtSurface.Grass);
                PineTree(mesh, cliff + new Vector3(side * 7.5f, 8f, -6f), 1.1f, 40 + (int)side);
                BroadTree(mesh, cliff + new Vector3(side * 5f, 13f, 0f), 1.1f, 50 + (int)side, Palette.Sakura, Palette.SakuraLight);
                PineTree(mesh, cliff + new Vector3(side * 3.2f, 13f, 2f), 1.2f, 60 + (int)side);
            }
            // Crags break up the block so the falls come down a real rock face, leaving the water clear.
            for (var i = 0; i < 46; i++)
            {
                var side = random.NextDouble() < 0.5 ? -1f : 1f;
                var across = side * (4.2f + (float)random.NextDouble() * 9f);
                var depth = 6f + (float)random.NextDouble() * 12f;
                var rise = (float)random.NextDouble() * 12f;
                Crag(mesh, PondCenter + new Vector3(across, rise, depth), 1.6f + (float)random.NextDouble() * 1.8f, random.Next());
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

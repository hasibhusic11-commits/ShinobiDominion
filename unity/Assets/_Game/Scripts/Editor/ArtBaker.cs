using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShinobiDominion.EditorTools
{
    /// <summary>
    /// Generates the world's surface textures and shared materials. All original, with no imported
    /// art. Layers are seamless grayscale detail: colour comes from the meshes.
    /// </summary>
    public static class ArtBaker
    {
        private const string Folder = "Assets/_Game/Resources/Art";
        private const string ArrayPath = Folder + "/DetailArray.asset";
        private const int Size = 256;
        private const int Layers = 12;

        public static bool IsBaked =>
            File.Exists(ArrayPath) && AssetDatabase.LoadAssetAtPath<Texture2DArray>(ArrayPath)?.depth == Layers && File.Exists(Folder + "/WorldMat.mat") && File.Exists(Folder + "/GroundMat.mat") &&
            File.Exists(Folder + "/WaterMat.mat") && File.Exists(Folder + "/ParticleMat.mat");

        public static void Bake()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();

            var array = new Texture2DArray(Size, Size, Layers, TextureFormat.R8, true, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 2
            };
            var pixels = new byte[Size * Size];
            for (var layer = 0; layer < Layers; layer++)
            {
                for (var y = 0; y < Size; y++)
                for (var x = 0; x < Size; x++)
                    pixels[y * Size + x] = (byte)(Mathf.Clamp01(Sample(layer, (float)x / Size, (float)y / Size)) * 255f);
                array.SetPixelData(pixels, 0, layer);
            }
            array.Apply(true, false);

            var existing = AssetDatabase.LoadAssetAtPath<Texture2DArray>(ArrayPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(array, existing);
                array = existing;
                EditorUtility.SetDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(array, ArrayPath);
            }

            Material("WorldMat", "ShinobiDominion/Toon", array);
            Material("GroundMat", "ShinobiDominion/Ground", array);
            Material("WaterMat", "ShinobiDominion/Water", array);
            ParticleMaterial();
            Shader.SetGlobalFloat("_SD_AnimStrength", 1f);
            AssetDatabase.SaveAssets();
        }

        private static void Material(string name, string shaderName, Texture2DArray array)
        {
            var path = $"{Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new System.InvalidOperationException($"Shader {shaderName} did not compile.");
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetTexture("_DetailArray", array);
            material.enableInstancing = false;
            EditorUtility.SetDirty(material);
        }

        private static void ParticleMaterial()
        {
            var path = $"{Folder}/ParticleMat.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return;
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
        }

        // ---------- Seamless patterns ----------

        private static float Sample(int layer, float u, float v)
        {
            switch (layer)
            {
                case 1: return Stone(u, v);
                case 2: return Wood(u, v);
                case 3: return Tile(u, v);
                case 4: return 0.92f + (Noise(u, v, 16, 3) - 0.5f) * 0.16f;
                case 5: return Grass(u, v);
                case 6: return Dirt(u, v);
                case 7: return WaterPattern(u, v);
                case 8: return Marble(u, v);
                case 9: return Metal(u, v);
                case 10: return Thatch(u, v);
                case 11: return Rock(u, v);
                default: return 1f;
            }
        }

        /// <summary>Value noise that tiles because lattice coordinates wrap at <paramref name="cells"/>.</summary>
        private static float Noise(float u, float v, int cells, int seed)
        {
            var x = u * cells;
            var y = v * cells;
            var x0 = Mathf.FloorToInt(x);
            var y0 = Mathf.FloorToInt(y);
            var fx = x - x0;
            var fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            var a = Lattice(x0, y0, cells, seed);
            var b = Lattice(x0 + 1, y0, cells, seed);
            var c = Lattice(x0, y0 + 1, cells, seed);
            var d = Lattice(x0 + 1, y0 + 1, cells, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Lattice(int x, int y, int cells, int seed)
        {
            x = ((x % cells) + cells) % cells;
            y = ((y % cells) + cells) % cells;
            unchecked
            {
                var hash = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647 + 1013);
                hash = (hash ^ (hash >> 13)) * 1274126177u;
                hash = (hash ^ (hash >> 16)) * 2246822519u;
                return ((hash ^ (hash >> 13)) & 0xFFFF) / 65535f;
            }
        }

        private static float Fractal(float u, float v, int cells, int seed)
        {
            return Noise(u, v, cells, seed) * 0.55f + Noise(u, v, cells * 2, seed + 1) * 0.3f + Noise(u, v, cells * 4, seed + 2) * 0.15f;
        }

        private static float Stone(float u, float v)
        {
            // Offset courses of cut stone, with a dark mortar line between blocks.
            const int rows = 4;
            const int columns = 3;
            var row = Mathf.FloorToInt(v * rows);
            var offset = row % 2 == 0 ? 0f : 0.5f;
            var cellU = Mathf.Repeat(u * columns + offset, 1f);
            var cellV = Mathf.Repeat(v * rows, 1f);
            var edge = Mathf.Min(Mathf.Min(cellU, 1f - cellU) * 2.2f, Mathf.Min(cellV, 1f - cellV) * 1.6f);
            var mortar = Mathf.SmoothStep(0f, 0.09f, edge);
            var block = Mathf.Floor(u * columns + offset) + row * 7f;
            var tone = 0.8f + Lattice((int)block, row, 16, 41) * 0.2f;
            var grain = 0.92f + (Fractal(u, v, 8, 5) - 0.5f) * 0.24f;
            return Mathf.Lerp(0.5f, tone, mortar) * grain;
        }

        private static float Wood(float u, float v)
        {
            // Long grain with plank seams.
            const int planks = 4;
            var plank = Mathf.FloorToInt(u * planks);
            var cell = Mathf.Repeat(u * planks, 1f);
            var seam = Mathf.SmoothStep(0f, 0.06f, Mathf.Min(cell, 1f - cell));
            var grain = Noise(u, v, 32, 7) * 0.6f + Noise(u, v, 8, 8) * 0.4f;
            var bands = 0.5f + 0.5f * Mathf.Sin((u * 48f + Noise(u, v, 4, 9) * 7f) * Mathf.PI);
            var tone = 0.82f + Lattice(plank, 0, planks, 13) * 0.18f;
            return Mathf.Lerp(0.55f, tone * (0.82f + grain * 0.1f + bands * 0.08f), seam);
        }

        private static float Tile(float u, float v)
        {
            // Overlapping rounded roof tiles, darker where each row tucks under the next.
            const int rows = 6;
            const int columns = 6;
            var row = Mathf.FloorToInt(v * rows);
            var offset = row % 2 == 0 ? 0f : 0.5f;
            var cellU = Mathf.Repeat(u * columns + offset, 1f) * 2f - 1f;
            var cellV = Mathf.Repeat(v * rows, 1f);
            var round = Mathf.Sqrt(Mathf.Max(0f, 1f - cellU * cellU * 0.9f));
            var shade = Mathf.Lerp(0.55f, 1f, Mathf.SmoothStep(0f, 0.5f, cellV * round + 0.05f));
            var edge = Mathf.SmoothStep(0f, 0.08f, 1f - Mathf.Abs(cellU));
            var block = Mathf.Floor(u * columns + offset) + row * 5f;
            var tone = 0.86f + Lattice((int)block, row, 16, 23) * 0.14f;
            return shade * Mathf.Lerp(0.72f, 1f, edge) * tone;
        }

        private static float Grass(float u, float v)
        {
            var fine = Noise(u, v, 64, 17);
            var blades = Noise(u * 1f, v * 1f, 128, 18);
            var clump = Fractal(u, v, 8, 19);
            return 0.78f + clump * 0.18f + (fine - 0.5f) * 0.14f + (blades - 0.5f) * 0.12f;
        }

        private static float Dirt(float u, float v)
        {
            var body = Fractal(u, v, 10, 27);
            var pebbles = Mathf.SmoothStep(0.68f, 0.76f, Noise(u, v, 48, 28));
            return 0.74f + body * 0.24f - pebbles * 0.1f + Noise(u, v, 96, 29) * 0.05f;
        }

        private static float Marble(float u, float v)
        {
            // Large polished slabs with soft, thin veins.
            const int slabs = 2;
            var cellU = Mathf.Repeat(u * slabs, 1f);
            var cellV = Mathf.Repeat(v * slabs, 1f);
            var seam = Mathf.SmoothStep(0f, 0.012f, Mathf.Min(Mathf.Min(cellU, 1f - cellU), Mathf.Min(cellV, 1f - cellV)));
            var vein = Mathf.SmoothStep(0f, 0.035f, Mathf.Abs(Fractal(u, v, 5, 51) - 0.5f));
            var body = 0.93f + Fractal(u, v, 4, 53) * 0.07f;
            return body * Mathf.Lerp(0.76f, 1f, vein) * Mathf.Lerp(0.7f, 1f, seam);
        }

        private static float Metal(float u, float v)
        {
            // Riveted plates with a faint hammered dimple.
            const int plates = 2;
            var cellU = Mathf.Repeat(u * plates, 1f);
            var cellV = Mathf.Repeat(v * plates, 1f);
            var seam = Mathf.SmoothStep(0f, 0.02f, Mathf.Min(Mathf.Min(cellU, 1f - cellU), Mathf.Min(cellV, 1f - cellV)));
            var rivetX = Mathf.Min(cellU, 1f - cellU) - 0.07f;
            var rivetY = Mathf.Min(cellV, 1f - cellV) - 0.07f;
            var rivet = 1f - Mathf.SmoothStep(0.02f, 0.035f, Mathf.Sqrt(rivetX * rivetX + rivetY * rivetY));
            var hammered = Noise(u, v, 16, 57);
            return Mathf.Clamp01((0.8f + hammered * 0.14f + Noise(u, v, 64, 58) * 0.06f + rivet * 0.25f) * Mathf.Lerp(0.55f, 1f, seam));
        }

        private static float Thatch(float u, float v)
        {
            // Bundled straw in rows, each row overhanging the one below.
            const int rows = 5;
            const int strands = 48;
            var row = Mathf.FloorToInt(v * rows);
            var cellV = Mathf.Repeat(v * rows, 1f);
            var strand = Mathf.FloorToInt(u * strands) + row * 7;
            var tone = 0.62f + Lattice(strand, row, strands, 61) * 0.38f;
            return tone * Mathf.Lerp(0.66f, 1f, Mathf.SmoothStep(0f, 0.4f, cellV));
        }

        private static float Rock(float u, float v)
        {
            // Soft mottling with faint, wide cracks, so it stays calm when zoomed out.
            var body = Noise(u, v, 4, 72) * 0.6f + Noise(u, v, 8, 75) * 0.3f + Noise(u, v, 16, 74) * 0.1f;
            var crack = 1f - Mathf.SmoothStep(0f, 0.05f, Mathf.Abs(Noise(u, v, 5, 73) - 0.5f));
            return Mathf.Clamp01(0.72f + body * 0.32f - crack * 0.12f);
        }

        private static float WaterPattern(float u, float v)
        {
            var a = Noise(u, v, 12, 37);
            var b = Noise(u, v, 24, 38);
            var ridges = 1f - Mathf.Abs(a * 2f - 1f);
            return Mathf.Clamp01(ridges * 0.75f + b * 0.3f);
        }
    }
}

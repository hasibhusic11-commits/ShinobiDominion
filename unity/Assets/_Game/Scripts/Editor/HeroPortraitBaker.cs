using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShinobiDominion.Editor
{
    /// <summary>
    /// Captures the game's own authored Hero3D prefabs into static portrait textures.
    /// Avoids showing a mismatched procedural character in the Hero Altar when a
    /// unique 3D model exists but no finished 2D concept painting has been supplied.
    /// Run once after the 3D prefabs have imported; do not run in a player build.
    /// </summary>
    public static class HeroPortraitBaker
    {
        private const int PortraitWidth = 512;
        private const int PortraitHeight = 640;
        private const int PortraitLayer = 30;
        private const string OutputDirectory = "Assets/_Game/Resources/HeroPortraits";

        [Serializable]
        private sealed class Registry
        {
            public Entry[] sprites;
        }

        [Serializable]
        private sealed class Entry
        {
            public string heroId;
            public int form;
            public string path;
            public bool enabled;
        }

        [MenuItem("Tools/Shinobi Dominion/Art/Bake Missing Hero Portraits")]
        public static void BakeMissing()
        {
            Bake(false);
        }

        [MenuItem("Tools/Shinobi Dominion/Art/Rebake 3D Hero Portraits")]
        public static void Rebake()
        {
            if (EditorUtility.DisplayDialog("Rebake 3D Portraits",
                    "Overwrite previously baked 3D portraits? Original concept art in HeroConcepts is never touched.",
                    "Rebake", "Cancel"))
                Bake(true);
        }

        private static void Bake(bool overwrite)
        {
            var json = Resources.Load<TextAsset>("Config/hero_sprites");
            if (json == null)
            {
                Debug.LogError("Hero portrait baker: Config/hero_sprites.json is missing.");
                return;
            }

            var registry = JsonUtility.FromJson<Registry>(json.text);
            if (registry == null || registry.sprites == null)
            {
                Debug.LogError("Hero portrait baker: hero_sprites.json could not be read.");
                return;
            }

            Directory.CreateDirectory(OutputDirectory);
            var processed = new HashSet<string>();
            int created = 0, skipped = 0, missing = 0;
            foreach (var entry in registry.sprites)
            {
                if (entry == null || !entry.enabled || entry.form != -1 ||
                    string.IsNullOrEmpty(entry.heroId) ||
                    string.IsNullOrEmpty(entry.path) ||
                    !entry.path.StartsWith("Heroes3D/", StringComparison.Ordinal))
                    continue;
                if (!processed.Add(entry.heroId)) continue;

                // An actual artist painting always takes priority. Never overwrite it.
                if (Resources.Load<Texture2D>("HeroConcepts/" + entry.heroId) != null)
                {
                    skipped++;
                    continue;
                }

                var output = OutputDirectory + "/" + entry.heroId + ".png";
                if (!overwrite && File.Exists(output))
                {
                    skipped++;
                    continue;
                }

                var prefab = Resources.Load<GameObject>(entry.path);
                if (prefab == null)
                {
                    Debug.LogWarning("Hero portrait baker: missing prefab " + entry.path);
                    missing++;
                    continue;
                }

                try
                {
                    var bytes = Capture(prefab);
                    File.WriteAllBytes(output, bytes);
                    AssetDatabase.ImportAsset(output, ImportAssetOptions.ForceUpdate);
                    var importer = AssetImporter.GetAtPath(output) as TextureImporter;
                    if (importer != null)
                    {
                        importer.textureType = TextureImporterType.Default;
                        importer.alphaIsTransparency = true;
                        importer.maxTextureSize = 1024;
                        importer.mipmapEnabled = false;
                        importer.SaveAndReimport();
                    }
                    created++;
                }
                catch (Exception ex)
                {
                    Debug.LogError("Hero portrait baker: " + entry.heroId + ": " + ex.Message);
                    missing++;
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"Hero portrait bake complete: {created} rendered, {skipped} skipped, {missing} missing/failed. " +
                      "Open the Heroes Altar and inspect the results before approving the art.");
        }

        private static byte[] Capture(GameObject prefab)
        {
            GameObject subject = null;
            GameObject cameraObject = null;
            GameObject lightObject = null;
            RenderTexture target = null;
            Texture2D output = null;
            var previousActive = RenderTexture.active;
            try
            {
                subject = UnityEngine.Object.Instantiate(prefab);
                subject.name = "PortraitBake_Subject";
                SetLayer(subject.transform, PortraitLayer);

                var renderers = subject.GetComponentsInChildren<Renderer>(true);
                var hasBounds = false;
                var bounds = new Bounds(Vector3.zero, Vector3.one);
                foreach (var renderer in renderers)
                {
                    if (!renderer.enabled || renderer is ParticleSystemRenderer) continue;
                    if (!hasBounds)
                    {
                        bounds = renderer.bounds;
                        hasBounds = true;
                    }
                    else bounds.Encapsulate(renderer.bounds);
                }
                if (!hasBounds) throw new InvalidOperationException("Prefab has no visible mesh renderers.");

                cameraObject = new GameObject("PortraitBake_Camera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.cullingMask = 1 << PortraitLayer;
                camera.orthographic = true;
                camera.allowHDR = false;
                camera.allowMSAA = true;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 200f;

                // Cover the entire character, framed identically regardless of model size.
                var extents = bounds.extents;
                camera.orthographicSize = Mathf.Max(0.5f,
                    Mathf.Max(extents.y * 1.15f, extents.x * PortraitHeight / PortraitWidth * 1.3f));
                var focus = bounds.center + Vector3.up * (extents.y * 0.06f);
                var distance = Mathf.Max(5f, bounds.size.magnitude * 3f);
                camera.transform.position = focus + new Vector3(0f, extents.y * 0.1f, distance);
                camera.transform.LookAt(focus);

                lightObject = new GameObject("PortraitBake_KeyLight");
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.94f, 0.83f);
                light.intensity = 1.7f;
                light.shadows = LightShadows.None;
                light.cullingMask = 1 << PortraitLayer;
                light.transform.rotation = Quaternion.Euler(38f, -28f, 0f);

                target = new RenderTexture(PortraitWidth, PortraitHeight, 24, RenderTextureFormat.ARGB32);
                target.antiAliasing = 4;
                target.Create();
                camera.targetTexture = target;
                camera.Render();

                RenderTexture.active = target;
                output = new Texture2D(PortraitWidth, PortraitHeight, TextureFormat.RGBA32, false);
                output.ReadPixels(new Rect(0, 0, PortraitWidth, PortraitHeight), 0, 0);
                output.Apply();
                return output.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (output != null) UnityEngine.Object.DestroyImmediate(output);
                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (lightObject != null) UnityEngine.Object.DestroyImmediate(lightObject);
                if (subject != null) UnityEngine.Object.DestroyImmediate(subject);
            }
        }

        private static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root) SetLayer(child, layer);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// Renders each hero's real model once to a texture, for the roster and evolution screens. Screens
    /// then show the same character the player sees on the map, in the same form.
    /// </summary>
    public sealed class HeroPortraits : System.IDisposable
    {
        private const int PortraitLayer = 31;
        private readonly Dictionary<string, RenderTexture> _textures = new Dictionary<string, RenderTexture>();
        private readonly Camera _camera;
        private readonly Transform _stage;

        public HeroPortraits(Transform parent)
        {
            _stage = new GameObject("HeroPortraitStage").transform;
            _stage.SetParent(parent, false);
            _stage.position = new Vector3(0f, -600f, 0f);

            var cameraObject = new GameObject("HeroPortraitCamera", typeof(Camera));
            cameraObject.transform.SetParent(_stage, false);
            // Three-quarters from the front and a little above, like the map but closer.
            cameraObject.transform.localRotation = Quaternion.Euler(14f, 28f, 0f);
            _camera = cameraObject.GetComponent<Camera>();
            _camera.enabled = false;
            _camera.orthographic = true;
            _camera.cullingMask = 1 << PortraitLayer;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 40f;
        }

        public RenderTexture For(HeroLook look, int pixels = 384)
        {
            var key = $"{look.Key}|{pixels}";
            if (_textures.TryGetValue(key, out var existing) && existing != null) return existing;

            var builder = BuildingMeshes.Figure(look);
            var mesh = builder.Build("Portrait");
            var subject = new GameObject("Subject") { layer = PortraitLayer };
            subject.transform.SetParent(_stage, false);
            subject.AddComponent<MeshFilter>().sharedMesh = mesh;
            subject.AddComponent<MeshRenderer>().sharedMaterials = builder.LitMaterials();

            var center = mesh.bounds.center;
            _camera.orthographicSize = Mathf.Max(mesh.bounds.extents.y, mesh.bounds.extents.x * 1.05f, 0.75f) * 1.16f;
            _camera.transform.localPosition = center - _camera.transform.localRotation * Vector3.forward * 20f;

            var texture = new RenderTexture(pixels, pixels, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            _camera.targetTexture = texture;
            _camera.Render();
            _camera.targetTexture = null;
            _textures[key] = texture;

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
}

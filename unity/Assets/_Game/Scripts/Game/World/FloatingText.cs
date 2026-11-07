using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>A number that rises and fades above something in the world. Cheap, and capped by its caller.</summary>
    public sealed class FloatingText : MonoBehaviour
    {
        private const float Life = 0.9f;
        private const float RiseSpeed = 1.3f;

        private TextMesh _mesh;
        private Color _color;
        private float _age;
        private Camera _camera;

        public static FloatingText Create(Transform parent, Camera viewer, Vector3 at, string text, Color color, float size = 0.09f)
        {
            var go = new GameObject("FloatingText");
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = Ui.Font;
            mesh.GetComponent<MeshRenderer>().sharedMaterial = Ui.Font.material;
            mesh.text = text;
            mesh.fontSize = 48;
            // Same apparent size at any zoom, and big enough to read on a phone.
            mesh.characterSize = size * 1.5f * (viewer != null && viewer.orthographic ? viewer.orthographicSize / 8f : 1f);
            mesh.fontStyle = FontStyle.Bold;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            var floating = go.AddComponent<FloatingText>();
            floating._mesh = mesh;
            floating._color = color;
            floating._camera = viewer;
            return floating;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            transform.position += Vector3.up * (RiseSpeed * Time.deltaTime);
            if (_camera != null) transform.rotation = _camera.transform.rotation;
            var color = _color;
            color.a = Mathf.Clamp01(1f - _age / Life) * _color.a;
            _mesh.color = color;
            if (_age >= Life) Destroy(gameObject);
        }
    }
}

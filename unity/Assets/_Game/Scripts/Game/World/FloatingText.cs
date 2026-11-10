using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// A number that pops in, rises and fades above something in the world. It carries its own dark shadow so it
    /// reads over grass, stone and fire alike, and it draws over every battle effect. Cheap, and capped by its caller.
    /// </summary>
    public sealed class FloatingText : MonoBehaviour
    {
        /// <summary>Above every effect picture and health bar in the world.</summary>
        public const int Order = 26000;

        private const float Life = 0.9f;
        private const float RiseSpeed = 1.3f;
        private const float PopTime = 0.12f;

        private TextMesh _mesh;
        private TextMesh _shadow;
        private Color _color;
        private float _age;
        private Camera _camera;

        public static FloatingText Create(Transform parent, Camera viewer, Vector3 at, string text, Color color, float size = 0.09f)
        {
            var go = new GameObject("FloatingText");
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            if (viewer != null) go.transform.rotation = viewer.transform.rotation;
            // Same apparent size at any zoom, and big enough to read on a phone.
            var characterSize = size * 1.5f * (viewer != null && viewer.orthographic ? viewer.orthographicSize / 8f : 1f);
            var mesh = Write(go, text, characterSize, color, Order);

            var shade = new GameObject("Shadow");
            shade.transform.SetParent(go.transform, false);
            shade.transform.localPosition = new Vector3(characterSize * 0.28f, -characterSize * 0.28f, 0f);

            var floating = go.AddComponent<FloatingText>();
            floating._mesh = mesh;
            floating._shadow = Write(shade, text, characterSize, new Color(0.05f, 0.03f, 0.03f, 0.7f * color.a), Order - 1);
            floating._color = color;
            floating._camera = viewer;
            floating.Show();
            return floating;
        }

        private static TextMesh Write(GameObject go, string text, float characterSize, Color color, int order)
        {
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = Ui.Font;
            var renderer = mesh.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Ui.Font.material;
            renderer.sortingOrder = order;
            mesh.text = text;
            mesh.fontSize = 48;
            mesh.characterSize = characterSize;
            mesh.fontStyle = FontStyle.Bold;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            return mesh;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            transform.position += Vector3.up * (RiseSpeed * Time.deltaTime);
            if (_camera != null) transform.rotation = _camera.transform.rotation;
            Show();
            if (_age >= Life) Destroy(gameObject);
        }

        private void Show()
        {
            // It lands big and settles, so the eye catches it, then fades over the second half of its life.
            var pop = 1f - Mathf.Clamp01(_age / PopTime);
            transform.localScale = Vector3.one * (1f + 0.5f * pop * pop);
            var fade = Mathf.Clamp01((1f - _age / Life) * 2f);
            var color = _color;
            color.a = fade * _color.a;
            _mesh.color = color;
            if (_shadow != null) _shadow.color = new Color(0.05f, 0.03f, 0.03f, 0.7f * color.a);
        }
    }
}

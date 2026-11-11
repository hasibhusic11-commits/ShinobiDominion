using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// A number that pops in, rises and fades above something in the world. Its dark edge and shadow are part of the
    /// letters (ADR-054), so it reads over grass, stone and fire alike, and it draws over every battle effect.
    /// A finished number is kept and used again, so a long fight makes a handful of these and no more.
    /// </summary>
    public sealed class FloatingText : MonoBehaviour
    {
        /// <summary>Above every effect picture and health bar in the world.</summary>
        public const int Order = 26000;

        private const float Life = 0.9f;
        private const float RiseSpeed = 1.3f;
        private const float PopTime = 0.12f;
        private static readonly Color Edge = new Color(0.05f, 0.03f, 0.03f, 1f);

        /// <summary>Finished numbers waiting to be used again. One that went with its parent is skipped.</summary>
        private static readonly Stack<FloatingText> Spare = new Stack<FloatingText>();

        /// <summary>How many were made new since the game started, for the tests: a fight reuses, it does not pile up.</summary>
        public static int Made { get; private set; }

        private TextMeshPro _text;
        private Color _color;
        private float _age;
        private Camera _camera;

        public static FloatingText Create(Transform parent, Camera viewer, Vector3 at, string text, Color color, float size = 0.09f)
        {
            FloatingText floating = null;
            while (Spare.Count > 0 && floating == null) floating = Spare.Pop();
            if (floating == null)
            {
                var go = new GameObject("FloatingText");
                floating = go.AddComponent<FloatingText>();
                floating._text = go.AddComponent<TextMeshPro>();
                var asset = GameFonts.For(HudArt.Numbers);
                if (asset != null)
                {
                    floating._text.font = asset;
                    floating._text.fontSharedMaterial = GameFonts.WorldMaterial(asset, Edge);
                }
                floating._text.alignment = TextAlignmentOptions.Center;
                floating._text.textWrappingMode = TextWrappingModes.NoWrap;
                floating._text.overflowMode = TextOverflowModes.Overflow;
                floating._text.raycastTarget = false;
                floating._text.rectTransform.sizeDelta = new Vector2(8f, 2f);
                floating._text.sortingOrder = Order;
                Made++;
            }
            var transform = floating.transform;
            transform.SetParent(parent, false);
            transform.position = at;
            if (viewer != null) transform.rotation = viewer.transform.rotation;
            // Same apparent size at any zoom, and big enough to read on a phone.
            var characterSize = size * 1.5f * (viewer != null && viewer.orthographic ? viewer.orthographicSize / 8f : 1f);
            floating._text.fontSize = characterSize * 48f;
            floating._text.text = text;
            floating._color = color;
            floating._camera = viewer;
            floating._age = 0f;
            floating.gameObject.SetActive(true);
            floating.Show();
            return floating;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            transform.position += Vector3.up * (RiseSpeed * Time.deltaTime);
            if (_camera != null) transform.rotation = _camera.transform.rotation;
            Show();
            if (_age < Life) return;
            gameObject.SetActive(false);
            Spare.Push(this);
        }

        private void Show()
        {
            // It lands big and settles, so the eye catches it, then fades over the second half of its life.
            var pop = 1f - Mathf.Clamp01(_age / PopTime);
            transform.localScale = Vector3.one * (1f + 0.5f * pop * pop);
            var fade = Mathf.Clamp01((1f - _age / Life) * 2f);
            var color = _color;
            color.a = fade * _color.a;
            _text.color = color;
        }
    }
}

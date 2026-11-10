using UnityEngine;
using UnityEngine.Rendering;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// Health and charge bars over the battle. A health bar drops at once and leaves a pale strip behind that
    /// drains after a short hold, so the size of each hit can be read.
    /// </summary>
    public sealed partial class BattleStage
    {
        /// <summary>Bars draw over every effect picture and under damage numbers.</summary>
        private const int BarOrder = 24000;

        private sealed class Bar
        {
            public GameObject Root;
            public Transform Fill;
            /// <summary>The pale strip showing health just lost; null for charge bars.</summary>
            public Transform Lag;
            public float Width;
            public float Height = BarHeight;
            public MeshRenderer FillRenderer;
            public Material Normal;
            public Material Full;
            public float Shown = 1f;
            public float Target = 1f;
            public float Hold;

            public void Set(float fraction)
            {
                fraction = Mathf.Clamp01(fraction);
                if (fraction < Target) Hold = 0.2f;
                Target = fraction;
                Fill.localScale = new Vector3(Width * fraction, Height * 0.7f, 1f);
                if (Lag != null && Shown < fraction)
                {
                    Shown = fraction;
                    ShowLag();
                }
                // A full charge bar turns bright: the special fires on the hero's next chance.
                if (Full != null) FillRenderer.sharedMaterial = fraction >= 1f ? Full : Normal;
            }

            public void Tick(float dt)
            {
                if (Lag == null || Shown <= Target) return;
                if (Hold > 0f)
                {
                    Hold -= dt;
                    return;
                }
                Shown = Mathf.MoveTowards(Shown, Target, dt * 1.2f);
                ShowLag();
            }

            private void ShowLag() => Lag.localScale = new Vector3(Width * Shown, Height * 0.7f, 1f);
        }

        private readonly System.Collections.Generic.List<Bar> _bars = new System.Collections.Generic.List<Bar>();

        private Bar CreateBar(Vector3 at, float width, Color color, float heightScale = 1f, bool lag = true)
        {
            var root = new GameObject("HealthBar");
            root.transform.SetParent(transform, false);
            root.transform.position = at;
            if (_camera != null) root.transform.rotation = _camera.transform.rotation;

            Transform Piece(Transform parent, string name, Color tint, Vector3 position, Vector3 scale, int order)
            {
                var piece = new GameObject(name);
                piece.transform.SetParent(parent, false);
                piece.transform.localPosition = position;
                piece.transform.localScale = scale;
                piece.AddComponent<MeshFilter>().sharedMesh = _quad;
                var renderer = piece.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = Mats.OnTop(tint);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.sortingOrder = order;
                return piece.transform;
            }

            var height = BarHeight * heightScale;
            var left = -width * 0.5f;
            Piece(root.transform, "Back", new Color(0.08f, 0.07f, 0.08f, 0.9f), new Vector3(left - 0.03f, 0f, 0.01f), new Vector3(width + 0.06f, height, 1f), BarOrder);
            var bar = new Bar { Root = root, Width = width, Height = height };
            if (lag) bar.Lag = Piece(root.transform, "Lag", new Color(1f, 0.93f, 0.8f), new Vector3(left, 0f, 0.005f), new Vector3(width, height * 0.7f, 1f), BarOrder + 1);
            bar.Fill = Piece(root.transform, "Fill", color, new Vector3(left, 0f, 0f), new Vector3(width, height * 0.7f, 1f), BarOrder + 2);
            bar.FillRenderer = bar.Fill.GetComponent<MeshRenderer>();
            // A light band along the top of the fill, so the bar reads as rounded instead of flat.
            Piece(bar.Fill, "Shine", new Color(1f, 1f, 1f, 0.28f), new Vector3(0f, 0.3f, -0.001f), new Vector3(1f, 0.4f, 1f), BarOrder + 3);
            _bars.Add(bar);
            return bar;
        }

        private void RemoveBar(Bar bar)
        {
            if (bar == null) return;
            _bars.Remove(bar);
            if (bar.Root != null) Destroy(bar.Root);
        }

        /// <summary>Keeps every bar facing the view and drains the pale strips.</summary>
        private void TickBars()
        {
            var dt = Time.deltaTime;
            for (var i = _bars.Count - 1; i >= 0; i--)
            {
                var bar = _bars[i];
                if (bar.Root == null)
                {
                    _bars.RemoveAt(i);
                    continue;
                }
                if (!bar.Root.activeInHierarchy) continue;
                if (_camera != null) bar.Root.transform.rotation = _camera.transform.rotation;
                bar.Tick(dt);
            }
        }
    }
}

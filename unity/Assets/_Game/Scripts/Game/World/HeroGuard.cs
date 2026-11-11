using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// The hero on a Hero Base (owner's request, 2026-11-08): it appears on the small pedestal, hops down beside it
    /// after a moment, turns to face out and stands guard over a softly glowing ring in its own colour. Positions are
    /// world offsets from the base, like the pedestal picture, so they stay put however the base is turned.
    /// Visual only: nothing here touches hero rules or assignments.
    /// </summary>
    public sealed class HeroGuard : MonoBehaviour
    {
        public const float HopDelay = 1f;
        public const float HopTime = 0.45f;
        private const float HopHeight = 0.45f;
        private static readonly Vector3 Outward = new Vector3(-1f, 0f, -1f);

        private Transform _base;
        private Vector3 _from;
        private Vector3 _to;
        private HeroSpriteActor _actor;
        private SpriteRenderer _ring;
        private SpriteRenderer _halo;
        private Color _ringColor;
        private float _clock;
        private int _stage; // 0 on the pedestal, 1 hopping, 2 standing guard

        /// <summary>True once the hero has landed beside the pedestal.</summary>
        public bool Guarding => _clock >= HopDelay + HopTime;

        public Vector3 GuardOffset => _to;

        public static HeroGuard Attach(HeroSpriteActor actor, Transform baseRoot, Vector3 pedestal, Vector3 guard, Color color)
            => Attach(actor.transform, actor, baseRoot, pedestal, guard, color);

        /// <summary>Also supports development fallback meshes when an authored actor prefab is absent.</summary>
        public static HeroGuard Attach(Transform visual, HeroSpriteActor actor, Transform baseRoot, Vector3 pedestal, Vector3 guard, Color color)
        {
            var guardian = visual.gameObject.AddComponent<HeroGuard>();
            guardian._actor = actor;
            guardian._base = baseRoot;
            guardian._from = pedestal;
            guardian._to = guard;
            Color.RGBToHSV(color, out var hue, out var saturation, out _);
            guardian._ringColor = Color.HSVToRGB(hue, Mathf.Max(saturation, 0.75f), 1f);
            guardian._ring = Ring(visual, guardian._ringColor);
            guardian._halo = Halo(guardian._ring.transform);
            // Standing where it appears means no hop: it is on guard from the start.
            if ((pedestal - guard).sqrMagnitude < 0.0001f) guardian._clock = HopDelay + HopTime;
            guardian.Apply();
            return guardian;
        }

        private static SpriteRenderer Ring(Transform parent, Color color)
        {
            var go = new GameObject("GuardRing");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * 0.03f;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = UiSkin.Disc(new Color(1f, 1f, 1f, 0.22f), new Color(1f, 1f, 1f, 0.5f), Color.white, 0.32f);
            var width = Mathf.Max(renderer.sprite.bounds.size.x, 0.001f);
            go.transform.localScale = Vector3.one * (1.1f / width) / Mathf.Max(parent.lossyScale.x, 0.001f);
            renderer.color = new Color(color.r, color.g, color.b, 0f);
            // Lies on the ground: over ground shadows, under every standing picture and the hero.
            renderer.sortingOrder = BuildingSprites.ShadowOrder + 10;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        /// <summary>A soft light pooled around the ring, so it reads as a glow rather than a line.</summary>
        private static SpriteRenderer Halo(Transform ring)
        {
            var go = new GameObject("GuardHalo");
            go.transform.SetParent(ring, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = HeroArt.Glow;
            var ringWidth = ring.GetComponent<SpriteRenderer>().sprite.bounds.size.x;
            go.transform.localScale = Vector3.one * (ringWidth * 1.7f / Mathf.Max(renderer.sprite.bounds.size.x, 0.001f));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private void LateUpdate()
        {
            _clock += Time.deltaTime;
            Apply();
        }

        private void Apply()
        {
            if (_base == null) return;
            var t = Mathf.Clamp01((_clock - HopDelay) / HopTime);
            var along = Vector3.Lerp(_from, _to, t);
            along.y = Mathf.Lerp(_from.y, _to.y, t) + Mathf.Sin(t * Mathf.PI) * HopHeight;
            transform.position = _base.position + along;

            if (_stage == 0 && t > 0f)
            {
                _stage = 1;
                _actor?.Face(_to - _from);
            }
            if (_stage == 1 && t >= 1f)
            {
                _stage = 2;
                _actor?.Face(Outward);
            }

            if (_ring == null) return;
            // One step under the hero, who sorts one step above its base (VillageView), so the ring never covers the feet.
            _ring.sortingOrder = BuildingSprites.Order(_base.position);
            // The ring fades in as the hero lands, then breathes slowly.
            var landed = Mathf.Clamp01((_clock - HopDelay - HopTime) / 0.4f);
            var breathe = 0.8f + 0.2f * Mathf.Sin(Time.time * 2.2f);
            _ring.color = new Color(_ringColor.r, _ringColor.g, _ringColor.b, landed * breathe);
            if (_halo != null)
            {
                _halo.sortingOrder = _ring.sortingOrder - 1;
                _halo.color = new Color(_ringColor.r, _ringColor.g, _ringColor.b, landed * breathe * 0.45f);
            }
        }
    }
}

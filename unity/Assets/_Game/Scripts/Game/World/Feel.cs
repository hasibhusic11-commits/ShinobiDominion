using System.Collections;
using LitMotion;
using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// Game feel ("juice") for world events, following the game-feel skill: each event fires a few small effects
    /// together, scaled by how much it matters, and everything returns to rest. Visual only: nothing here
    /// touches a rule, a timer or the server.
    /// </summary>
    public static class Feel
    {
        private static IMotionScheduler Clock => MotionScheduler.UpdateIgnoreTimeScale;

        /// <summary>How hard an event lands. Placing is small, upgrading medium, a building falling large.</summary>
        public enum Tier
        {
            Small,
            Medium,
            Large
        }

        /// <summary>
        /// Squash and stretch: the picture flattens on impact, then springs back past its size and settles.
        /// Works through IsoStand.Juice, so the stand keeps its placement and scale rules.
        /// </summary>
        public static void Pop(IsoStand stand, Tier tier)
        {
            if (stand == null) return;
            var squash = tier == Tier.Small ? 0.16f : tier == Tier.Medium ? 0.26f : 0.34f;
            var from = new Vector2(1f + squash, 1f - squash);
            stand.Juice = from;
            LMotion.Create(from, Vector2.one, tier == Tier.Small ? 0.32f : 0.45f).WithEase(Ease.OutElastic).WithScheduler(Clock)
                .WithOnComplete(() => { if (stand != null) stand.Juice = Vector2.one; })
                .Bind(v => { if (stand != null) stand.Juice = v; }).AddTo(stand.gameObject);
        }

        /// <summary>A puff of dust round a footprint's edge, for a building landing on the ground.</summary>
        public static void Dust(Transform parent, Vector3 center, Vector2 footprint)
        {
            var sprite = Resources.Load<Sprite>("Props/dust_cloud");
            if (sprite == null) return;
            var points = new[]
            {
                new Vector3(-footprint.x * 0.5f, 0f, -footprint.y * 0.5f), new Vector3(footprint.x * 0.5f, 0f, -footprint.y * 0.5f),
                new Vector3(-footprint.x * 0.5f, 0f, footprint.y * 0.5f), new Vector3(0f, 0f, -footprint.y * 0.55f), new Vector3(-footprint.x * 0.55f, 0f, 0f)
            };
            foreach (var p in points)
                Puff(parent, sprite, center + p, Mathf.Max(footprint.x, footprint.y) * 0.4f, 2.2f, 0.55f, new Color(0.9f, 0.82f, 0.68f, 0.9f));
        }

        /// <summary>A golden ring of rays over a building, for an upgrade.</summary>
        public static void LevelBurst(Transform parent, Vector3 at, float size)
        {
            var sprite = Resources.Load<Sprite>("Props/level_up_burst");
            if (sprite == null) return;
            Puff(parent, sprite, at, size * 0.5f, 2.4f, 0.7f, Color.white);
        }

        /// <summary>An upright picture that grows from <paramref name="start"/> by <paramref name="grow"/> times and fades out.</summary>
        private static void Puff(Transform parent, Sprite sprite, Vector3 at, float start, float grow, float duration, Color color)
        {
            var renderer = BuildingSprites.Stand(parent, "Puff", sprite, at, start, BuildingSprites.Order(at) + 60, color);
            var t = renderer.transform;
            var baseScale = t.localScale;
            LMotion.Create(0f, 1f, duration).WithEase(Ease.OutCubic).WithScheduler(Clock)
                .WithOnComplete(() => { if (renderer != null) Object.Destroy(renderer.gameObject); })
                .Bind(k =>
                {
                    if (renderer == null) return;
                    t.localScale = baseScale * Mathf.Lerp(1f, grow, k);
                    renderer.color = new Color(color.r, color.g, color.b, color.a * (1f - k * k));
                }).AddTo(renderer.gameObject);
        }

        /// <summary>A building flashes red for a moment when it is struck, then returns to its normal colours.</summary>
        public static void HitFlash(SpriteRenderer renderer)
        {
            if (renderer == null) return;
            renderer.color = new Color(1f, 0.55f, 0.5f, renderer.color.a);
            LMotion.Create(0f, 1f, 0.16f).WithScheduler(Clock)
                .Bind(k => { if (renderer != null) renderer.color = Color.Lerp(new Color(1f, 0.55f, 0.5f), Color.white, k); })
                .AddTo(renderer.gameObject);
        }

        private sealed class FeelRunner : MonoBehaviour
        {
        }

        private static FeelRunner _runner;

        /// <summary>A hidden object that lives for the whole game, so a freeze always finishes and restores the speed.</summary>
        private static FeelRunner Runner
        {
            get
            {
                if (_runner != null) return _runner;
                var go = new GameObject("FeelRunner") { hideFlags = HideFlags.HideInHierarchy };
                Object.DontDestroyOnLoad(go);
                _runner = go.AddComponent<FeelRunner>();
                return _runner;
            }
        }

        private static int _hitStop;
        private static float _restoreScale = 1f;

        /// <summary>
        /// A freeze frame: time nearly stops for a few hundredths of a second to sell a big impact. Waits in real
        /// time, and only the newest freeze restores the speed, so overlapping hits never cut each other short.
        /// </summary>
        public static void HitStop(Tier tier)
        {
            var host = Runner;
            var duration = tier == Tier.Small ? 0.03f : tier == Tier.Medium ? 0.05f : 0.11f;
            if (_hitStop == 0) _restoreScale = Time.timeScale;
            host.StartCoroutine(Freeze(++_hitStop, duration));
        }

        private static IEnumerator Freeze(int id, float duration)
        {
            Time.timeScale = _restoreScale * 0.05f;
            yield return new WaitForSecondsRealtime(duration);
            if (id != _hitStop) yield break;
            Time.timeScale = _restoreScale;
            _hitStop = 0;
        }
    }
}

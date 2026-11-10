using System.Collections.Generic;
using ShinobiDominion.Core;
using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// How the attacker's spells look. The battle log says where each one landed, when it pulsed and what it
    /// reached; this draws the ring on the ground, the falling fire, the mist, the frost and the drum.
    /// </summary>
    public sealed partial class BattleStage
    {
        /// <summary>Where each cast landed and what kind it is, by cast number.</summary>
        private readonly Dictionary<int, Vector3> _castAt = new Dictionary<int, Vector3>();
        private readonly Dictionary<int, int> _castEffect = new Dictionary<int, int>();

        private static readonly Color Frost = new Color(0.7f, 0.9f, 1f);
        private static readonly Color Mist = new Color(0.55f, 1f, 0.65f);
        private static readonly Color Gold = new Color(1f, 0.78f, 0.3f);

        /// <summary>The falling fire of a damage spell reaches the ground this long after its pulse.</summary>
        private const float EmberFall = 0.22f;

        private void ShowSpellCast(int cast, int effect)
        {
            if (!_castAt.TryGetValue(cast, out var at)) return;
            GameSounds.Play(GameSound.Cast, 0.7f);
            var tint = effect == BattleSim.EffectHeal ? Mist : effect == BattleSim.EffectFreeze ? Frost : effect == BattleSim.EffectBoost ? Gold : Ember;
            Burst("fx_hit_magic", at + Vector3.up * 0.6f, 0.35f, 0.6f, 2f, tint, spin: 160f);
        }

        /// <summary>One application of a spell over its ring; <paramref name="radius"/> is in cells.</summary>
        private void ShowSpellPulse(int cast, int application, int total, float radius)
        {
            if (!_castAt.TryGetValue(cast, out var at)) return;
            _castEffect.TryGetValue(cast, out var effect);
            var wide = radius * 2f;
            var first = application == 1;
            switch (effect)
            {
                case BattleSim.EffectDamage:
                    if (first) TargetRing(at, wide, Seconds((total - 1) * 10f) + 0.6f);
                    for (var i = 0; i < 5; i++)
                    {
                        var spot = at + new Vector3(Jitter(radius * 0.7f), 0.3f, Jitter(radius * 0.7f));
                        var loud = i == 0;
                        Shot("fx_fireball", spot + new Vector3(-1.5f, 5.5f, 1.5f), spot, 5.9f / EmberFall, 1.5f, 0f, Color.white, null, Vector3.zero, landed =>
                        {
                            Burst("fx_explosion", landed + Vector3.up * 0.3f, 0.36f, 0.7f, 2.1f, Color.white);
                            Scatter(landed, 3, Ember, 0.13f, 3f, 0.6f);
                            if (!loud) return;
                            GameSounds.Play(GameSound.Blast, 0.6f);
                            Shake(0.12f);
                        }, i * 0.04f);
                    }
                    break;
                case BattleSim.EffectHeal:
                    GroundRing(at, Seconds(application < total ? 10f : 4f) + 0.25f, wide * 0.92f, wide, Mist, onTop: true, fadeFrom: 0.75f);
                    for (var i = 0; i < 3; i++)
                        Burst("fx_heal_glow", at + new Vector3(Jitter(radius * 0.6f), 0.8f, Jitter(radius * 0.6f)), 0.7f, 0.8f, 1.5f, Color.white, delay: i * 0.1f, rise: 0.9f);
                    GameSounds.Play(GameSound.Mend, first ? 0.6f : 0.35f);
                    break;
                case BattleSim.EffectFreeze:
                    GroundRing(at, 0.6f, 0.6f, wide, Frost, onTop: true);
                    Burst("fx_hit_magic", at + Vector3.up * 0.8f, 0.5f, 1f, 3.4f, Frost, spin: 90f);
                    GameSounds.Play(GameSound.Zap, 0.7f);
                    break;
                default:
                    GroundRing(at, 0.7f, 0.8f, wide, Gold, onTop: true);
                    Burst("fx_hit_spark", at + Vector3.up * 1.2f, 0.5f, 1.2f, 3.6f, Gold);
                    FloatingText.Create(transform, _camera, at + Vector3.up * 2.1f, "POWER UP!", Gold);
                    GameSounds.Play(GameSound.Drum, 0.7f);
                    break;
            }
        }

        /// <summary>A building caught under falling fire.</summary>
        private void ShowSpellHit(BattleBuildingInput building, long damage)
        {
            var center = CenterOf(building);
            var id = building.id;
            Later(EmberFall, () =>
            {
                Burst("fx_hit_spark", center + Vector3.up * (building.isWall ? 0.6f : 1f), 0.22f, 0.5f, 1.4f, Ember, angle: Jitter(180f));
                _view.HitFlash(id);
                ShowNumber(center + Vector3.up * 1.6f, damage, Ember);
            });
        }

        /// <summary>A guard caught under falling fire.</summary>
        private void ShowSpellHitDefender(Figure defender, long damage)
        {
            var target = defender.Go.transform;
            var body = BodyOf(defender);
            Later(EmberFall, () =>
            {
                if (target == null) return;
                Burst("fx_hit_spark", target.position + body, 0.22f, 0.4f, 1.2f, Ember, angle: Jitter(180f));
                ShowNumber(target.position + Vector3.up * 2.2f, damage, Ivory);
            });
        }

        /// <summary>Ice over a tower for as long as it is held still.</summary>
        private void ShowFreeze(BattleBuildingInput building, float seconds)
        {
            var size = 1.4f + building.width * 0.8f;
            Burst("fx_shield_bubble", CenterOf(building) + Vector3.up * (0.6f + building.width * 0.3f), Mathf.Max(0.4f, seconds), size, size * 1.05f, Frost, fadeFrom: 0.85f);
        }

        /// <summary>Ice over a guard for as long as it is held still.</summary>
        private void ShowFreeze(Figure defender, float seconds)
        {
            var body = BodyOf(defender);
            Follow(Burst("fx_shield_bubble", defender.Go.transform.position + body, Mathf.Max(0.4f, seconds), 2f, 2.1f, Frost, fadeFrom: 0.85f), defender, body);
        }

        private SpriteRenderer _aim;

        /// <summary>
        /// The ring showing where the picked spell would land, before it is cast. <paramref name="radius"/> is in
        /// cells; zero hides the ring.
        /// </summary>
        public void Aim(Vector3 at, float radius)
        {
            if (radius <= 0f)
            {
                if (_aim != null) _aim.gameObject.SetActive(false);
                return;
            }
            if (_aim == null)
            {
                var sprite = Fx("fx_target_ring");
                if (sprite == null) return;
                var go = new GameObject("SpellAim");
                go.transform.SetParent(transform, false);
                _aim = go.AddComponent<SpriteRenderer>();
                _aim.sprite = sprite;
                _aim.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _aim.receiveShadows = false;
                var material = FxMaterial();
                if (material != null) _aim.sharedMaterial = material;
                _aim.sortingOrder = FxOrder - 1;
            }
            _aim.gameObject.SetActive(true);
            // The ring fills 413 of the picture's 512 pixels, and is painted as seen from this camera.
            var wide = radius * 2f * 512f / 413f;
            var pulse = 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 6f);
            var item = _aim.transform;
            item.position = new Vector3(at.x, 0.05f, at.z);
            item.localScale = Vector3.one * (wide * pulse / Mathf.Max(0.01f, _aim.sprite.bounds.size.x));
            if (_camera != null) item.rotation = _camera.transform.rotation;
            _aim.color = new Color(1f, 1f, 1f, 0.8f);
        }

        /// <summary>A unit taking the War Drum's strength.</summary>
        private void ShowBoost(Figure unit)
        {
            var at = unit.Go.transform.position;
            GroundRing(at, 0.5f, 0.4f, unit.Troop ? 1.4f : 2.2f, Gold);
            Burst("fx_hit_spark", at + BodyOf(unit), 0.35f, 0.5f, 1.6f, Gold, spin: 120f);
        }
    }
}

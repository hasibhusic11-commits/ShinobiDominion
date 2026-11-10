using System;
using System.Collections.Generic;
using ShinobiDominion.Core;
using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// What each battle event looks and sounds like. Every attack has its own shot and its own landing, scaled by
    /// its weight: an arrow ticks, a cannon ball bursts and nudges the view, a fallen Town Hall shakes it hard.
    /// The battle log decides everything; this only draws it.
    /// </summary>
    public sealed partial class BattleStage
    {
        /// <summary>The tick of each tower's last shot and how long it flies, so a splash shows one shot, not five.</summary>
        private readonly Dictionary<int, long> _shotTick = new Dictionary<int, long>();
        private readonly Dictionary<int, float> _shotFlight = new Dictionary<int, float>();

        private static readonly Color Ivory = new Color(1f, 0.95f, 0.8f);
        private static readonly Color Hurt = new Color(1f, 0.45f, 0.35f);
        private static readonly Color Smoke = new Color(1f, 1f, 1f, 0.85f);
        private static readonly Color Ember = new Color(1f, 0.7f, 0.25f);
        private static readonly Color Stone = new Color(0.6f, 0.56f, 0.5f);

        // ---------- Helpers ----------

        /// <summary>Where a shot lands on a fighter: the middle of its body.</summary>
        private static Vector3 BodyOf(Figure figure) => Vector3.up * (figure.Troop ? 0.75f : 1.3f);

        private static bool Ranged(Figure figure) => figure.Role >= 3;

        private static string HitSprite(Figure from) => from.Troop || from.Role < 3 ? "fx_hit_spark" : "fx_hit_magic";

        private static void Follow(Flash flash, Figure figure, Vector3 offset)
        {
            if (flash == null) return;
            flash.Target = figure.Go.transform;
            flash.TargetOffset = offset;
            flash.Stick = true;
        }

        /// <summary>Faces the target; a melee fighter steps into the blow and a ranged one rocks back from its shot.</summary>
        private static void Lunge(Figure unit, Vector3 direction, bool ranged)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f) return;
            var aim = direction.normalized;
            unit.Go.transform.rotation = Quaternion.LookRotation(-aim);
            unit.KickDirection = ranged ? -aim : aim;
            unit.Kick = ranged ? 0.12f : 0.22f;
            if (unit.Actor == null) return;
            unit.Actor.Face(direction);
            unit.Actor.Attack();
        }

        /// <summary>A ranged fighter's shot: a thrown blade for troops, fire for a Mage, a spirit bolt for the rest.</summary>
        private float Launch(Figure from, Vector3 to, Transform target, Vector3 offset, Action<Vector3> landed)
        {
            var start = from.Go.transform.position + Vector3.up * (from.Troop ? 0.8f : 1.3f);
            if (from.Troop)
            {
                GameSounds.Play(GameSound.Arrow, 0.35f);
                return Shot("fx_kunai", start, to, 20f, 1.1f, 0.35f, Color.white, target, offset, landed);
            }
            GameSounds.Play(GameSound.Shot, 0.4f);
            var tint = from.Role == 5 ? new Color(0.75f, 1f, 0.85f) : Color.white;
            return Shot(from.Role == 3 ? "fx_fireball" : "fx_spirit_bolt", start, to, 16f, 1.4f, 0.15f, tint, target, offset, landed);
        }

        /// <summary>Where a tower's shot leaves it: near the top of its picture.</summary>
        private Vector3 MuzzleOf(BattleBuildingInput building)
        {
            var center = CenterOf(building);
            if (building.isWall) return center + Vector3.up * 0.6f;
            var stand = _view.StandOf(building.id);
            if (stand != null && stand.Renderer != null && stand.Renderer.sprite != null)
                return center + Vector3.up * Mathf.Max(1f, stand.WorldHeight * 0.8f - building.width * 0.33f);
            return center + Vector3.up * (1.2f + building.width * 0.5f);
        }

        // ---------- Arriving and falling ----------

        /// <summary>A dropped unit falls in from above and lands with dust and a thud.</summary>
        private void ShowLanding(Figure figure)
        {
            var troop = figure.Troop;
            figure.Drop = troop ? 1.1f : 1.6f;
            var at = figure.Go.transform.position;
            at.y = 0f;
            // MoveUnit lowers the drop by 0.6 a tick.
            Later(figure.Drop / (0.6f * BattleSim.TicksPerSecond), () =>
            {
                GroundRing(at, 0.35f, 0.3f, troop ? 1.2f : 2f, new Color(1f, 0.96f, 0.85f, 0.7f));
                Burst("fx_smoke_puff", at + Vector3.up * 0.25f, 0.4f, troop ? 0.5f : 0.8f, troop ? 1f : 1.6f, Smoke, rise: 0.3f);
                GameSounds.Play(GameSound.Land, troop ? 0.35f : 0.7f);
            });
        }

        private void ShowFall(Figure figure)
        {
            Burst("fx_smoke_puff", figure.Go.transform.position + Vector3.up * 0.7f, 0.5f, 0.8f, 1.9f, Smoke, rise: 0.5f);
            GameSounds.Play(GameSound.Fall);
        }

        // ---------- Attacks ----------

        /// <summary>An attacker hitting a building: a blow at its near edge, or a shot that flies in first.</summary>
        private void ShowStrike(Figure unit, BattleBuildingInput building, long damage)
        {
            var center = CenterOf(building);
            var position = unit.Go.transform.position;
            var near = new Vector3(
                Mathf.Clamp(position.x, building.x, building.x + building.width), 0f,
                Mathf.Clamp(position.z, building.z, building.z + building.depth));
            var ranged = Ranged(unit);
            Lunge(unit, center - position, ranged);
            var number = center + Vector3.up * 1.6f;
            var id = building.id;
            if (ranged)
            {
                var picture = HitSprite(unit);
                var hit = Vector3.Lerp(near, center, 0.5f) + Vector3.up * (building.isWall ? 0.6f : 1f);
                Launch(unit, hit, null, Vector3.zero, at =>
                {
                    Burst(picture, at, 0.25f, 0.5f, 1.3f, Color.white, angle: Jitter(180f));
                    GameSounds.Play(GameSound.Hit, 0.5f);
                    _view.HitFlash(id);
                    ShowNumber(number, damage, Ivory);
                });
                return;
            }
            Burst("fx_hit_spark", near + Vector3.up * 0.7f, 0.2f, 0.5f, 1.4f, Color.white, angle: Jitter(180f));
            GameSounds.Play(GameSound.Hit, 0.55f);
            _view.HitFlash(id);
            ShowNumber(number, damage, Ivory);
        }

        /// <summary>One fighter striking another: it faces the target, then lunges or shoots, and a burst lands.</summary>
        private void ShowBlow(Figure from, Figure to, long damage, Color color)
        {
            var start = from.Go.transform.position;
            var end = to.Go.transform.position;
            var body = BodyOf(to);
            var ranged = Ranged(from) && Vector3.Distance(start, end) > 1.5f;
            Lunge(from, end - start, ranged);
            var picture = HitSprite(from);
            if (ranged)
            {
                Launch(from, end + body, to.Go.transform, body, at =>
                {
                    Burst(picture, at, 0.22f, 0.4f, 1.2f, Color.white, angle: Jitter(180f));
                    GameSounds.Play(GameSound.Hit, 0.5f);
                    ShowNumber(at + Vector3.up * 0.9f, damage, color);
                });
                return;
            }
            Burst(picture, end + body, 0.22f, 0.4f, 1.2f, Color.white, angle: Jitter(180f));
            GameSounds.Play(GameSound.Hit, 0.6f);
            ShowNumber(end + Vector3.up * 2.2f, damage, color);
        }

        /// <summary>A tower's shot: an arrow, a lobbed cannon ball that bursts, or a straight bolt of magic.</summary>
        private void ShowTowerShot(BattleBuildingInput tower, Figure unit, long damage, long tick)
        {
            var body = BodyOf(unit);
            var target = unit.Go.transform;
            var magic = tower.definitionId == "spirit_turret";
            if (_shotTick.TryGetValue(tower.id, out var last) && last == tick)
            {
                // The same shot's splash: it reaches this unit when the shot lands.
                _shotFlight.TryGetValue(tower.id, out var wait);
                Later(wait, () =>
                {
                    if (target == null) return;
                    var at = target.position + body;
                    Burst(magic ? "fx_hit_magic" : "fx_hit_spark", at, 0.2f, 0.4f, 1.1f, Color.white, angle: Jitter(180f));
                    ShowNumber(at + Vector3.up * 1.2f, damage, Hurt);
                });
                return;
            }
            _shotTick[tower.id] = tick;
            var from = MuzzleOf(tower);
            var to = target.position + body;
            if (tower.definitionId == "cannon_tower")
            {
                GameSounds.Play(GameSound.Shot, 0.5f);
                _shotFlight[tower.id] = Shot("fx_fireball", from, to, 13f, 1.6f, 1.6f, Color.white, target, body, at =>
                {
                    Burst("fx_explosion", at, 0.38f, 0.8f, 2.4f, Color.white);
                    Burst("fx_smoke_puff", at, 0.5f, 0.8f, 1.6f, Smoke, delay: 0.08f, rise: 0.7f);
                    Scatter(at, 5, Ember, 0.13f, 3f, 0.6f);
                    GameSounds.Play(GameSound.Blast, 0.55f);
                    Shake(0.08f);
                    ShowNumber(at + Vector3.up * 1.2f, damage, Hurt);
                });
            }
            else if (magic)
            {
                GameSounds.Play(GameSound.Shot, 0.35f);
                _shotFlight[tower.id] = Shot("fx_spirit_bolt", from, to, 20f, 1.4f, 0f, Color.white, target, body, at =>
                {
                    Burst("fx_hit_magic", at, 0.3f, 0.5f, 1.3f, Color.white, spin: 120f);
                    GameSounds.Play(GameSound.Zap, 0.45f);
                    ShowNumber(at + Vector3.up * 1.2f, damage, Hurt);
                });
            }
            else
            {
                GameSounds.Play(GameSound.Arrow, 0.45f);
                _shotFlight[tower.id] = Shot("fx_arrow", from, to, 22f, 1.3f, 0.5f, Color.white, target, body, at =>
                {
                    Burst("fx_hit_spark", at, 0.18f, 0.3f, 0.9f, Color.white, angle: Jitter(180f));
                    ShowNumber(at + Vector3.up * 1.2f, damage, Hurt);
                });
            }
        }

        // ---------- Buildings and traps ----------

        /// <summary>
        /// A building going down. The rubble is always left, so a skipped battle ends on the same field; the fall
        /// itself (the picture sinking, the burst, smoke and flying stone) only plays when the battle is watched.
        /// Call it before the building's picture is removed.
        /// </summary>
        private void ShowCollapse(BattleBuildingInput building, bool show)
        {
            var center = CenterOf(building);
            if (building.isWall)
            {
                if (!show) return;
                GameSounds.Play(GameSound.Hit, 0.6f);
                Impact(building);
                Burst("fx_smoke_puff", center + Vector3.up * 0.5f, 0.45f, 0.7f, 1.5f, Smoke, rise: 0.5f);
                Scatter(center + Vector3.up * 0.5f, 4, Stone, 0.16f, 2.2f, 0.7f);
                return;
            }
            AddRubble(building);
            if (!show) return;
            var w = building.width;
            GameSounds.Play(GameSound.Crumble);
            Impact(building);
            Topple(_view.StandOf(building.id));
            Burst("fx_explosion", center + Vector3.up * (0.5f + w * 0.3f), 0.5f, w * 0.8f, w * 1.9f + 0.6f, Color.white);
            for (var i = 0; i < 3; i++)
                Burst("fx_smoke_puff", center + new Vector3(Jitter(w * 0.3f), 0.6f + i * 0.3f, Jitter(w * 0.3f)),
                    0.7f, w * 0.6f, w * 1.3f, Smoke, delay: 0.1f + i * 0.12f, rise: 1.2f);
            Scatter(center + Vector3.up * 0.6f, 6 + w * 2, Stone, 0.2f, 3.2f, 0.9f);
            Scatter(center + Vector3.up * 0.6f, 4 + w, Ember, 0.14f, 3.8f, 0.7f);
            var smoke = WorldEffects.Create(transform, "Smoke", center, new Color(0.35f, 0.33f, 0.3f, 0.55f), new Color(0.5f, 0.5f, 0.5f, 0f), 22f, 1.4f, 1.1f, 1.5f, w * 0.4f);
            _expiring.Add(new KeyValuePair<float, GameObject>(Time.time + 1.2f, smoke.gameObject));
            // A thin column keeps rising from the ruin for a while.
            var smoulder = WorldEffects.Create(transform, "Smoulder", center + Vector3.up * 0.2f, new Color(0.25f, 0.24f, 0.23f, 0.4f), new Color(0.5f, 0.5f, 0.5f, 0f), 3f, 2.2f, 0.7f, 0.9f, w * 0.25f);
            _expiring.Add(new KeyValuePair<float, GameObject>(Time.time + 6f, smoulder.gameObject));
        }

        /// <summary>A hidden trap goes off where it lies: a Bomb Trap bursts, a Hero Trap snaps shut.</summary>
        private void ShowTrap(BattleBuildingInput trap, bool bomb)
        {
            var at = CenterOf(trap);
            if (!bomb)
            {
                GameSounds.Play(GameSound.Hit, 0.9f);
                TargetRing(at, 2.2f, 0.6f);
                Burst("fx_hit_spark", at + Vector3.up * 0.5f, 0.3f, 0.6f, 1.8f, Color.white);
                Shake(0.25f);
                return;
            }
            GameSounds.Play(GameSound.Blast, 0.9f);
            Burst("fx_explosion", at + Vector3.up * 0.6f, 0.5f, 1.2f, 3.6f, Color.white);
            for (var i = 0; i < 2; i++)
                Burst("fx_smoke_puff", at + new Vector3(Jitter(0.5f), 0.7f + i * 0.4f, Jitter(0.5f)), 0.7f, 1f, 2.2f, Smoke, delay: 0.12f + i * 0.12f, rise: 1.1f);
            GroundRing(at, 0.4f, 0.5f, 3.4f, new Color(1f, 0.6f, 0.2f));
            Scatter(at + Vector3.up * 0.4f, 10, Ember, 0.16f, 4f, 0.8f);
            var smoke = WorldEffects.Create(transform, "TrapSmoke", at, new Color(0.3f, 0.28f, 0.25f, 0.6f), new Color(0.5f, 0.5f, 0.5f, 0f), 24f, 1.3f, 1.4f, 1.4f, 0.8f);
            _expiring.Add(new KeyValuePair<float, GameObject>(Time.time + 1.2f, smoke.gameObject));
            Shake(0.45f);
            Feel.HitStop(Feel.Tier.Medium);
        }

        /// <summary>The mark over a hero a Hero Trap is holding still.</summary>
        private void ShowSnare(Figure unit)
        {
            var offset = Vector3.up * (unit.Troop ? 1.6f : 2.3f);
            Follow(Burst("fx_hit_magic", unit.Go.transform.position + offset, 1f, 0.7f, 0.9f, Color.white, spin: 200f, fadeFrom: 0.8f), unit, offset);
        }

        /// <summary>A tower struck still by a Control hero.</summary>
        private void ShowStun(BattleBuildingInput building)
        {
            var center = CenterOf(building);
            Burst("fx_lightning_bolt", center + Vector3.up * 1.8f, 0.4f, 2.4f, 2.8f, Color.white);
            Burst("fx_hit_magic", center + Vector3.up * 0.8f, 0.35f, 0.8f, 2f, Color.white, spin: 120f);
            GameSounds.Play(GameSound.Zap, 0.5f);
        }

        // ---------- Abilities ----------

        /// <summary>A special going off: each role has its own burst, over a ring in the role's colour.</summary>
        private void ShowAbility(Figure figure, int role, float volume)
        {
            GameSounds.Play(GameSound.Ability, volume);
            var body = Vector3.up * 1.1f;
            var at = figure.Go.transform.position + body;
            switch (role)
            {
                case 0:
                    Follow(Burst("fx_shield_bubble", at, 0.6f, 1.6f, 2.6f, Color.white, fadeFrom: 0.6f), figure, body);
                    break;
                case 1:
                    Burst("fx_hit_spark", at, 0.45f, 0.8f, 2.6f, Color.white, spin: 90f);
                    break;
                case 2:
                    Burst("fx_hit_spark", at, 0.4f, 0.8f, 2.4f, new Color(1f, 0.6f, 0.75f), spin: -140f);
                    break;
                case 3:
                    Burst("fx_explosion", at, 0.45f, 0.8f, 2.6f, Color.white);
                    break;
                case 4:
                    Burst("fx_hit_magic", at, 0.5f, 0.8f, 2.6f, Color.white, spin: 90f);
                    break;
                default:
                    Burst("fx_heal_glow", at, 0.6f, 1f, 2.2f, Color.white, rise: 0.8f);
                    break;
            }
            GroundRing(figure.Go.transform.position, 0.5f, 0.6f, 3.2f, AbilityColor(role));
        }

        private void ShowHeal(Figure figure)
        {
            Burst("fx_heal_glow", figure.Go.transform.position + Vector3.up, 0.6f, 0.9f, 1.7f, Color.white, rise: 0.9f);
            GameSounds.Play(GameSound.Mend, 0.4f);
        }

        /// <summary>A rally: a wide golden ring, a burst and a callout over the hero, on a drum.</summary>
        private void ShowRally(Figure figure)
        {
            var at = figure.Go.transform.position;
            var gold = new Color(1f, 0.78f, 0.3f);
            GroundRing(at, 0.7f, 0.8f, BattleSim.RallyRange / (float)BattleSim.CellUnits * 2f, gold, onTop: true);
            Burst("fx_hit_spark", at + Vector3.up * 1.2f, 0.5f, 1f, 3.2f, gold);
            FloatingText.Create(transform, _camera, at + Vector3.up * 1.9f, "ATTACK SPEED UP!", gold);
            GameSounds.Play(GameSound.Drum, 0.5f);
        }
    }
}

using System;
using System.Collections.Generic;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// Shows a raid: the defender's base, placed heroes, then a playback of the battle log. It decides
    /// nothing. Every hit, death and result comes from the log, so what happens here can't change them.
    /// </summary>
    public sealed class BattleStage : MonoBehaviour
    {
        private const float HeroScale = 1.9f;
        private const float TroopScale = 1.45f;
        private const float BarHeight = 0.2f;

        private sealed class Figure
        {
            public GameObject Go;
            public Mesh Mesh;
            public HeroSpriteActor Actor;
            public bool OnBase;
            public float HomeX;
            public float HomeZ;
            public Bar Bar;
            /// <summary>The ability charge bar under the health bar (heroes and defenders only; troops have no special).</summary>
            public Bar Charge;
            public string InstanceId;
            public float Px;
            public float Pz;
            public float Tx;
            public float Tz;
            public float Speed;
            public long MaxHp;
            public float Kick;
            public Vector3 KickDirection;
            public Vector3 Offset;
        }

        private sealed class Bar
        {
            public GameObject Root;
            public Transform Fill;
            public float Width;
            public float Height = BarHeight;
            public MeshRenderer FillRenderer;
            public Material Normal;
            public Material Full;

            public void Set(float fraction)
            {
                Fill.localScale = new Vector3(Width * Mathf.Clamp01(fraction), Height * 0.7f, 1f);
                // A full charge bar turns bright: the special fires on the hero's next chance.
                if (Full != null) FillRenderer.sharedMaterial = fraction >= 1f ? Full : Normal;
            }
        }

        private sealed class Flash
        {
            public Transform Item;
            public Vector3 From;
            public Vector3 To;
            public float Age;
            public float Duration;
            public float StartSize;
            public float EndSize;
        }

        private RaidSession _session;
        private Camera _camera;
        private Func<string, HeroLook> _lookFor;
        private Village _village;
        private VillageView _view;
        private readonly Dictionary<string, Figure> _placed = new Dictionary<string, Figure>();
        private readonly Dictionary<RaidTroopStack, GameObject> _stacks = new Dictionary<RaidTroopStack, GameObject>();
        private readonly Dictionary<string, Mesh> _troopMeshes = new Dictionary<string, Mesh>();
        private TroopCatalog _troops;
        private readonly List<Figure> _units = new List<Figure>();
        private readonly List<Figure> _defenders = new List<Figure>();
        private readonly Dictionary<int, BattleBuildingInput> _buildings = new Dictionary<int, BattleBuildingInput>();
        private readonly Dictionary<int, Bar> _buildingBars = new Dictionary<int, Bar>();
        private readonly List<Flash> _flashes = new List<Flash>();
        private readonly List<KeyValuePair<float, GameObject>> _expiring = new List<KeyValuePair<float, GameObject>>();
        private readonly List<Mesh> _ownedMeshes = new List<Mesh>();
        private Mesh _graveMesh;
        private float _nextNumberAt;
        private Mesh _quad;
        private Mesh _orb;
        private Material[] _orbMaterials;

        private BattleResult _result;
        private int _nextEvent;
        private float _ticks;
        private long _totalHp;
        private long _dealtHp;
        private bool _finished;

        public bool DojoDown { get; private set; }
        public int DamagePercent => _totalHp == 0 ? 100 : (int)(_dealtHp * 100 / _totalHp);
        public int SecondsLeft => Mathf.Max(0, Mathf.CeilToInt((BattleSim.TickLimit - _ticks) / BattleSim.TicksPerSecond));
        public event Action Finished;

        public void Initialize(RaidSession session, BuildingCatalog catalog, TroopCatalog troops, Camera worldCamera, Func<string, HeroLook> lookFor, Func<RaidDefender, HeroLook> defenderLook)
        {
            _session = session;
            _troops = troops;
            _camera = worldCamera;
            _lookFor = lookFor;
            foreach (var building in session.buildings)
            {
                _buildings[building.id] = building;
                if (!building.isWall) _totalHp += building.hp;
            }
            _village = Village.FromSave(catalog, RaidDefinition.ToSave(session.buildings));
            _view = new VillageView(_village, transform, _ => null);

            _quad = new Mesh { name = "BarQuad" };
            _quad.SetVertices(new[] { new Vector3(0f, -0.5f, 0f), new Vector3(1f, -0.5f, 0f), new Vector3(1f, 0.5f, 0f), new Vector3(0f, 0.5f, 0f) });
            _quad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            _quad.RecalculateBounds();
            _ownedMeshes.Add(_quad);

            var orb = new MeshBuilder { ContactShade = false };
            orb.Blob(Vector3.zero, Vector3.one, Color.white, ArtSurface.Plain, 0f, 1f, 8, 5);
            _orb = orb.Build("Orb");
            _orbMaterials = orb.LitMaterials();
            _ownedMeshes.Add(_orb);

            foreach (var defender in session.defenders)
            {
                var figure = CreateFigureFrom(defenderLook(defender), defender.id, defender.x, defender.z);
                figure.Go.name = "BattleDefender";
                figure.Speed = BattleSim.SpeedOf(defender.role);
                figure.MaxHp = defender.hp;
                figure.OnBase = true;
                figure.HomeX = figure.Px;
                figure.HomeZ = figure.Pz;
                PlaceFigure(figure);
                figure.Bar = CreateBar(figure.Go.transform.position + Vector3.up * (figure.Actor != null ? 3.5f : 3.1f), 1.4f, new Color(0.9f, 0.25f, 0.2f));
                figure.Bar.Root.transform.SetParent(figure.Go.transform, true);
                figure.Charge = CreateChargeBar(figure);
                _defenders.Add(figure);
            }
        }

        // ---------- Placing heroes ----------

        public void ShowPlacement(RaidPlacement placement)
        {
            HidePlacement(placement.instanceId);
            _placed[placement.instanceId] = CreateFigure(placement.instanceId, placement.x, placement.z);
        }

        public void HidePlacement(string instanceId)
        {
            if (!_placed.TryGetValue(instanceId, out var figure)) return;
            Destroy(figure.Go);
            _placed.Remove(instanceId);
        }

        public void ShowStack(RaidTroopStack stack)
        {
            var at = new Vector3(stack.x + 0.5f, 0f, stack.z + 0.5f);
            _stacks[stack] = CreateTroop(stack.troopId, at);
        }

        public void HideStack(RaidTroopStack stack)
        {
            if (!_stacks.TryGetValue(stack, out var go)) return;
            Destroy(go);
            _stacks.Remove(stack);
        }

        private GameObject CreateTroop(string troopId, Vector3 at)
        {
            if (!_troopMeshes.TryGetValue(troopId, out var mesh))
            {
                mesh = BuildingMeshes.Troop(troopId).Build(troopId);
                _troopMeshes[troopId] = mesh;
                _ownedMeshes.Add(mesh);
            }
            var go = new GameObject("BattleTroop");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = at;
            go.transform.localScale = Vector3.one * TroopScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Mats.World;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        private Figure CreateFigure(string instanceId, int x, int z) => CreateFigureFrom(_lookFor(instanceId), instanceId, x, z);

        private Figure CreateFigureFrom(HeroLook heroLook, string instanceId, int x, int z)
        {
            var look = heroLook ?? new HeroLook { Color = new Color(0.6f, 0.6f, 0.6f) };
            var go = new GameObject("BattleHero");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * HeroScale;
            var actor = HeroSprites.Spawn(look, go.transform);
            Mesh mesh = null;
            if (actor == null)
            {
                // Development fallback for heroes with no sprite art yet.
                var builder = BuildingMeshes.Figure(look);
                mesh = builder.Build("Hero");
                _ownedMeshes.Add(mesh);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = builder.LitMaterials();
            }
            var figure = new Figure
            {
                Go = go,
                Actor = actor,
                Mesh = mesh,
                InstanceId = instanceId,
                Px = x * BattleSim.CellUnits + BattleSim.CellUnits / 2f,
                Pz = z * BattleSim.CellUnits + BattleSim.CellUnits / 2f
            };
            figure.Tx = figure.Px;
            figure.Tz = figure.Pz;
            PlaceFigure(figure);
            return figure;
        }

        private static void PlaceFigure(Figure figure)
        {
            // A defender stands up on its Hero Base until it steps off.
            var raised = figure.OnBase && Mathf.Abs(figure.Px - figure.HomeX) < BattleSim.CellUnits && Mathf.Abs(figure.Pz - figure.HomeZ) < BattleSim.CellUnits;
            var at = new Vector3(figure.Px / BattleSim.CellUnits, raised ? BuildingMeshes.PedestalTop : 0f, figure.Pz / BattleSim.CellUnits);
            figure.Go.transform.localPosition = at + figure.Offset + figure.KickDirection * figure.Kick;
        }

        // ---------- Playing the log back ----------

        public void Play(BattleResult result, IList<RaidPlacement> placements, IList<RaidTroopStack> stacks)
        {
            _result = result;
            foreach (var placement in placements)
            {
                var figure = _placed[placement.instanceId];
                var member = _session.team.Find(m => m.instanceId == placement.instanceId);
                figure.Speed = member.speed;
                figure.MaxHp = member.hp;
                figure.Bar = CreateBar(figure.Go.transform.position + Vector3.up * (figure.Actor != null ? 3.5f : 3.1f), 1.4f, new Color(0.35f, 0.85f, 0.35f));
                figure.Bar.Root.transform.SetParent(figure.Go.transform, true);
                figure.Charge = CreateChargeBar(figure);
                _units.Add(figure);
            }

            // Same order as the battle: heroes first, then each stack.
            foreach (var stack in stacks)
            {
                HideStack(stack);
                var troop = _troops.Find(stack.troopId);
                for (var i = 0; i < stack.count; i++)
                {
                    var angle = i * 2.4f;
                    var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (0.12f + 0.09f * i);
                    var px = stack.x * BattleSim.CellUnits + BattleSim.CellUnits / 2f;
                    var pz = stack.z * BattleSim.CellUnits + BattleSim.CellUnits / 2f;
                    _units.Add(new Figure
                    {
                        Go = CreateTroop(stack.troopId, new Vector3(px / BattleSim.CellUnits, 0f, pz / BattleSim.CellUnits) + offset),
                        Px = px,
                        Pz = pz,
                        Tx = px,
                        Tz = pz,
                        Speed = BattleSim.SpeedOf(troop.role),
                        MaxHp = troop.hp,
                        Offset = offset
                    });
                }
            }
        }

        /// <summary>Jumps straight to the result. It changes nothing, as the result is already decided.</summary>
        public void Skip()
        {
            if (_result == null) return;
            while (_nextEvent < _result.events.Count) Apply(_result.events[_nextEvent++], false);
            _ticks = _result.endTick;
            Finish();
        }

        private void Update()
        {
            if (_camera != null)
                foreach (var pair in _buildingBars)
                    pair.Value.Root.transform.rotation = _camera.transform.rotation;
            foreach (var unit in _units)
            {
                if (unit.Bar != null && _camera != null) unit.Bar.Root.transform.rotation = _camera.transform.rotation;
                if (unit.Charge != null && _camera != null) unit.Charge.Root.transform.rotation = _camera.transform.rotation;
            }
            foreach (var unit in _defenders)
            {
                if (unit.Bar != null && _camera != null) unit.Bar.Root.transform.rotation = _camera.transform.rotation;
                if (unit.Charge != null && _camera != null) unit.Charge.Root.transform.rotation = _camera.transform.rotation;
            }
            UpdateFlashes();

            if (_result == null || _finished) return;
            var delta = Time.deltaTime * BattleSim.TicksPerSecond;
            _ticks += delta;
            while (_nextEvent < _result.events.Count && _result.events[_nextEvent][0] <= _ticks)
                Apply(_result.events[_nextEvent++], true);
            foreach (var unit in _units) MoveUnit(unit, delta);
            foreach (var unit in _defenders) MoveUnit(unit, delta);
            if (_nextEvent >= _result.events.Count && _ticks >= _result.endTick) Finish();
        }

        private void Finish()
        {
            if (_finished) return;
            _finished = true;
            Finished?.Invoke();
        }

        private static void MoveUnit(Figure unit, float ticks)
        {
            if (!unit.Go.activeSelf) return;
            var budget = unit.Speed * ticks;
            var moving = !Mathf.Approximately(unit.Px, unit.Tx) || !Mathf.Approximately(unit.Pz, unit.Tz);
            if (unit.Actor != null)
            {
                unit.Actor.SetMoving(moving);
                if (moving) unit.Actor.Face(new Vector3(unit.Tx - unit.Px, 0f, Mathf.Approximately(unit.Px, unit.Tx) ? unit.Tz - unit.Pz : 0f));
            }
            // The same walk as the battle itself: along x first, then along z.
            if (!Mathf.Approximately(unit.Px, unit.Tx))
            {
                var step = Mathf.Min(Mathf.Abs(unit.Tx - unit.Px), budget);
                unit.Px += Mathf.Sign(unit.Tx - unit.Px) * step;
                budget -= step;
                unit.Go.transform.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sign(unit.Tx - unit.Px + 0.001f), 0f, 0f));
            }
            if (budget > 0f && !Mathf.Approximately(unit.Pz, unit.Tz))
            {
                var step = Mathf.Min(Mathf.Abs(unit.Tz - unit.Pz), budget);
                unit.Pz += Mathf.Sign(unit.Tz - unit.Pz) * step;
                unit.Go.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, -Mathf.Sign(unit.Tz - unit.Pz + 0.001f)));
            }
            unit.Kick = Mathf.MoveTowards(unit.Kick, 0f, ticks * 0.05f);
            PlaceFigure(unit);
        }

        private void Apply(long[] entry, bool show)
        {
            var kind = (BattleEventKind)entry[1];
            switch (kind)
            {
                case BattleEventKind.Move:
                {
                    var unit = _units[(int)entry[2]];
                    unit.Tx = entry[3] * BattleSim.CellUnits + BattleSim.CellUnits / 2f;
                    unit.Tz = entry[4] * BattleSim.CellUnits + BattleSim.CellUnits / 2f;
                    break;
                }
                case BattleEventKind.HeroHit:
                {
                    var unit = _units[(int)entry[2]];
                    var building = _buildings[(int)entry[3]];
                    if (!building.isWall) _dealtHp += entry[4];
                    if (show)
                    {
                        var center = CenterOf(building);
                        var from = unit.Go.transform.position + Vector3.up;
                        var direction = center - unit.Go.transform.position;
                        direction.y = 0f;
                        if (direction.sqrMagnitude > 0.001f)
                        {
                            unit.Go.transform.rotation = Quaternion.LookRotation(-direction.normalized);
                            unit.KickDirection = direction.normalized;
                            unit.Kick = 0.22f;
                            if (unit.Actor != null)
                            {
                                unit.Actor.Face(direction);
                                unit.Actor.Attack();
                            }
                        }
                        ShowNumber(center + Vector3.up * 1.6f, entry[4], new Color(1f, 0.95f, 0.8f));
                        GameSounds.Play(GameSound.Hit, 0.55f);
                        _view.HitFlash(building.id);
                        var ranged = direction.magnitude > 2.5f;
                        if (ranged) AddFlash(from, center + Vector3.up * 0.8f, 0.2f, 0.1f, 0.1f, new Color(1f, 0.85f, 0.45f));
                        else AddFlash(center + Vector3.up * 0.6f, center + Vector3.up * 0.6f, 0.12f, 0.05f, 0.25f, new Color(1f, 0.9f, 0.6f));
                        BarFor(building).Set((float)entry[5] / building.hp);
                    }
                    break;
                }
                case BattleEventKind.BuildingDown:
                {
                    var building = _buildings[(int)entry[2]];
                    if (building.definitionId == "dojo_hq") DojoDown = true;
                    if (show) GameSounds.Play(building.isWall ? GameSound.Hit : GameSound.Crumble, building.isWall ? 0.6f : 1f);
                    if (show) Impact(building);
                    _view.SetGone(building.id);
                    if (_buildingBars.TryGetValue(building.id, out var bar))
                    {
                        Destroy(bar.Root);
                        _buildingBars.Remove(building.id);
                    }
                    if (!building.isWall) AddRubble(building);
                    if (show)
                    {
                        var smoke = WorldEffects.Create(transform, "Smoke", CenterOf(building), new Color(0.35f, 0.33f, 0.3f, 0.55f), new Color(0.5f, 0.5f, 0.5f, 0f), building.isWall ? 8f : 22f, 1.4f, 1.1f, building.isWall ? 0.7f : 1.5f, building.width * 0.4f);
                        _expiring.Add(new KeyValuePair<float, GameObject>(Time.time + 1.2f, smoke.gameObject));
                    }
                    break;
                }
                case BattleEventKind.TowerHit:
                {
                    var unit = _units[(int)entry[3]];
                    unit.Bar?.Set((float)entry[5] / unit.MaxHp);
                    if (show)
                    {
                        GameSounds.Play(GameSound.Shot, 0.4f);
                        ShowNumber(unit.Go.transform.position + Vector3.up * 2.2f, entry[4], new Color(1f, 0.45f, 0.35f));
                        var building = _buildings[(int)entry[2]];
                        AddFlash(CenterOf(building) + Vector3.up * 2f, unit.Go.transform.position + Vector3.up, 0.18f, 0.09f, 0.09f, new Color(1f, 0.55f, 0.25f));
                    }
                    break;
                }
                case BattleEventKind.HeroDown:
                {
                    var unit = _units[(int)entry[2]];
                    AddGrave(unit.Go.transform.position);
                    if (show) GameSounds.Play(GameSound.Fall);
                    if (show) AddFlash(unit.Go.transform.position + Vector3.up * 0.6f, unit.Go.transform.position + Vector3.up * 0.8f, 0.4f, 0.1f, 0.7f, new Color(0.9f, 0.9f, 1f));
                    unit.Go.SetActive(false);
                    break;
                }
                case BattleEventKind.Ability:
                {
                    if (!show) break;
                    var unit = _units[(int)entry[2]];
                    GameSounds.Play(GameSound.Ability, 0.5f);
                    var at = unit.Go.transform.position + Vector3.up * 0.9f;
                    AddFlash(at, at, 0.35f, 0.15f, 0.55f, AbilityColor((int)entry[3]));
                    break;
                }
                case BattleEventKind.Heal:
                {
                    var unit = _units[(int)entry[2]];
                    unit.Bar?.Set((float)entry[4] / unit.MaxHp);
                    if (show)
                    {
                        var at = unit.Go.transform.position + Vector3.up;
                        AddFlash(at, at + Vector3.up * 0.8f, 0.5f, 0.15f, 0.05f, new Color(0.5f, 1f, 0.55f));
                    }
                    break;
                }
                case BattleEventKind.DefenderMove:
                {
                    var unit = _defenders[(int)entry[2]];
                    unit.Tx = entry[3] * BattleSim.CellUnits + BattleSim.CellUnits / 2f;
                    unit.Tz = entry[4] * BattleSim.CellUnits + BattleSim.CellUnits / 2f;
                    break;
                }
                case BattleEventKind.DefenderHit:
                {
                    var target = _units[(int)entry[3]];
                    target.Bar?.Set((float)entry[5] / target.MaxHp);
                    if (show)
                    {
                        ShowBlow(_defenders[(int)entry[2]], target);
                        ShowNumber(target.Go.transform.position + Vector3.up * 2.2f, entry[4], new Color(1f, 0.45f, 0.35f));
                    }
                    break;
                }
                case BattleEventKind.DefenderDamaged:
                {
                    var target = _defenders[(int)entry[3]];
                    target.Bar?.Set((float)entry[5] / target.MaxHp);
                    if (show)
                    {
                        ShowBlow(_units[(int)entry[2]], target);
                        ShowNumber(target.Go.transform.position + Vector3.up * 2.2f, entry[4], new Color(1f, 0.95f, 0.8f));
                    }
                    break;
                }
                case BattleEventKind.DefenderDown:
                {
                    var unit = _defenders[(int)entry[2]];
                    AddGrave(unit.Go.transform.position);
                    if (show) GameSounds.Play(GameSound.Fall);
                    if (show) AddFlash(unit.Go.transform.position + Vector3.up * 0.6f, unit.Go.transform.position + Vector3.up * 0.8f, 0.4f, 0.4f, 0.05f, new Color(0.9f, 0.3f, 0.25f));
                    unit.Go.SetActive(false);
                    break;
                }
                case BattleEventKind.DefenderAbility:
                {
                    if (!show) break;
                    var at = _defenders[(int)entry[2]].Go.transform.position + Vector3.up * 0.9f;
                    AddFlash(at, at, 0.35f, 0.15f, 0.55f, AbilityColor((int)entry[3]));
                    break;
                }
                case BattleEventKind.Charge:
                {
                    var list = entry[4] == 0 ? _units : _defenders;
                    var index = (int)entry[2];
                    if (index >= 0 && index < list.Count) list[index].Charge?.Set((float)entry[3] / BattleSim.ChargeFull);
                    break;
                }
                case BattleEventKind.DefenderHeal:
                {
                    var unit = _defenders[(int)entry[2]];
                    unit.Bar?.Set((float)entry[4] / unit.MaxHp);
                    if (show)
                    {
                        var at = unit.Go.transform.position + Vector3.up;
                        AddFlash(at, at + Vector3.up * 0.8f, 0.5f, 0.15f, 0.05f, new Color(0.5f, 1f, 0.55f));
                    }
                    break;
                }
                case BattleEventKind.Stun:
                {
                    if (!show) break;
                    var at = CenterOf(_buildings[(int)entry[2]]) + Vector3.up * 2.2f;
                    AddFlash(at, at, 0.8f, 0.5f, 0.8f, new Color(0.7f, 0.6f, 1f));
                    break;
                }
            }
        }

        /// <summary>A damage number, at most one every short moment so a big fight stays readable.</summary>
        private void ShowNumber(Vector3 at, long amount, Color color)
        {
            if (Time.time < _nextNumberAt) return;
            _nextNumberAt = Time.time + 0.08f;
            FloatingText.Create(transform, _camera, at, amount.ToString("N0"), color);
        }

        /// <summary>A small stone where a fighter fell, so the battle's cost stays visible.</summary>
        private void AddGrave(Vector3 worldPosition)
        {
            if (_graveMesh == null)
            {
                var builder = new MeshBuilder { ContactShade = false };
                var stone = new Color(0.46f, 0.45f, 0.44f);
                builder.Box(Vector3.zero, new Vector3(0.34f, 0.06f, 0.24f), new Color(0.3f, 0.26f, 0.22f), ArtSurface.Rock);
                builder.Box(Vector3.up * 0.06f, new Vector3(0.2f, 0.3f, 0.09f), stone, ArtSurface.Rock);
                builder.Box(Vector3.up * 0.22f, new Vector3(0.3f, 0.07f, 0.09f), stone, ArtSurface.Rock);
                _graveMesh = builder.Build("Grave");
                _ownedMeshes.Add(_graveMesh);
            }
            var go = new GameObject("Grave");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(worldPosition.x, 0f, worldPosition.z);
            go.AddComponent<MeshFilter>().sharedMesh = _graveMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Mats.World;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>One hero striking another: it faces the target, lunges, and a small burst lands.</summary>
        private void ShowBlow(Figure from, Figure to)
        {
            GameSounds.Play(GameSound.Hit, 0.6f);
            var direction = to.Go.transform.position - from.Go.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                from.Go.transform.rotation = Quaternion.LookRotation(-direction.normalized);
                from.KickDirection = direction.normalized;
                from.Kick = 0.22f;
                if (from.Actor != null)
                {
                    from.Actor.Face(direction);
                    from.Actor.Attack();
                }
            }
            var impact = to.Go.transform.position + Vector3.up;
            if (direction.magnitude > 2.5f) AddFlash(from.Go.transform.position + Vector3.up, impact, 0.2f, 0.1f, 0.1f, new Color(1f, 0.85f, 0.45f));
            else AddFlash(impact, impact, 0.12f, 0.05f, 0.25f, new Color(1f, 0.9f, 0.6f));
        }

        private static Color AbilityColor(int role)
        {
            switch (role)
            {
                case 0: return new Color(0.75f, 0.8f, 0.9f);
                case 1: return new Color(1f, 0.6f, 0.2f);
                case 2: return new Color(0.9f, 0.3f, 0.5f);
                case 3: return new Color(0.5f, 0.6f, 1f);
                case 4: return new Color(0.7f, 0.5f, 1f);
                default: return new Color(0.5f, 1f, 0.6f);
            }
        }

        private static Vector3 CenterOf(BattleBuildingInput building) =>
            new Vector3(building.x + building.width * 0.5f, 0f, building.z + building.depth * 0.5f);

        // ---------- Bars, flashes and rubble ----------

        private Bar BarFor(BattleBuildingInput building)
        {
            if (_buildingBars.TryGetValue(building.id, out var bar)) return bar;
            var height = building.isWall ? 1.4f : 1.5f + building.width * 0.45f;
            bar = CreateBar(CenterOf(building) + Vector3.up * height, building.isWall ? 0.7f : 0.6f + building.width * 0.3f, new Color(0.95f, 0.72f, 0.2f));
            _buildingBars[building.id] = bar;
            return bar;
        }

        /// <summary>
        /// The gold ability bar just under a hero's health bar. It fills one step per landed hit
        /// (BattleSim.ChargeFull steps) and glows when full; the special fires and it empties.
        /// </summary>
        private Bar CreateChargeBar(Figure figure)
        {
            var at = figure.Go.transform.position + Vector3.up * ((figure.Actor != null ? 3.5f : 3.1f) - BarHeight * 1.25f);
            var bar = CreateBar(at, 1.4f, new Color(0.98f, 0.76f, 0.22f), 0.55f);
            bar.Root.name = "ChargeBar";
            bar.Root.transform.SetParent(figure.Go.transform, true);
            bar.Normal = bar.FillRenderer.sharedMaterial;
            bar.Full = Mats.OnTop(new Color(0.75f, 0.98f, 1f));
            bar.Set(0f);
            return bar;
        }

        private Bar CreateBar(Vector3 at, float width, Color color, float heightScale = 1f)
        {
            var root = new GameObject("HealthBar");
            root.transform.SetParent(transform, false);
            root.transform.position = at;
            if (_camera != null) root.transform.rotation = _camera.transform.rotation;

            Transform Piece(string name, Color tint, float x, float y, float w, float h, float z)
            {
                var piece = new GameObject(name);
                piece.transform.SetParent(root.transform, false);
                piece.transform.localPosition = new Vector3(x, y, z);
                piece.transform.localScale = new Vector3(w, h, 1f);
                piece.AddComponent<MeshFilter>().sharedMesh = _quad;
                var renderer = piece.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = Mats.OnTop(tint);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                return piece.transform;
            }

            var height = BarHeight * heightScale;
            Piece("Back", new Color(0.08f, 0.07f, 0.08f, 0.9f), -width * 0.5f - 0.03f, 0f, width + 0.06f, height, 0.01f);
            var bar = new Bar { Root = root, Width = width, Height = height };
            bar.Fill = Piece("Fill", color, -width * 0.5f, 0f, width, height * 0.7f, 0f);
            bar.FillRenderer = bar.Fill.GetComponent<MeshRenderer>();
            return bar;
        }

        /// <summary>
        /// Game feel when a building falls: the view shakes and time freezes for an instant, more for bigger targets
        /// and most for the Dojo. Walls only nudge the view. Visual only; the battle result is already decided.
        /// </summary>
        private void Impact(BattleBuildingInput building)
        {
            var view = _camera != null ? _camera.GetComponent<IsoCamera>() : null;
            if (building.isWall)
            {
                if (view != null) view.AddTrauma(0.12f);
                return;
            }
            var dojo = building.definitionId == "dojo_hq";
            if (view != null) view.AddTrauma(dojo ? 0.85f : 0.4f);
            Feel.HitStop(dojo ? Feel.Tier.Large : Feel.Tier.Medium);
        }

        private void AddFlash(Vector3 from, Vector3 to, float duration, float startSize, float endSize, Color color)
        {
            var go = new GameObject("Flash");
            go.transform.SetParent(transform, false);
            go.transform.position = from;
            go.transform.localScale = Vector3.one * startSize;
            go.AddComponent<MeshFilter>().sharedMesh = _orb;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = _orbMaterials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            TintOrb(go, color);
            _flashes.Add(new Flash { Item = go.transform, From = from, To = to, Duration = duration, StartSize = startSize, EndSize = endSize });
        }

        /// <summary>Colour lives in the mesh, so each colour of flash gets its own small mesh.</summary>
        private readonly Dictionary<Color, Mesh> _orbByColor = new Dictionary<Color, Mesh>();

        private void TintOrb(GameObject go, Color color)
        {
            if (!_orbByColor.TryGetValue(color, out var mesh))
            {
                var builder = new MeshBuilder { ContactShade = false };
                builder.Blob(Vector3.zero, Vector3.one, color, ArtSurface.Plain, 0f, 1f, 8, 5);
                mesh = builder.Build("Orb");
                _orbByColor[color] = mesh;
                _ownedMeshes.Add(mesh);
            }
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        private void UpdateFlashes()
        {
            for (var i = _flashes.Count - 1; i >= 0; i--)
            {
                var flash = _flashes[i];
                flash.Age += Time.deltaTime;
                var t = Mathf.Clamp01(flash.Age / flash.Duration);
                flash.Item.position = Vector3.Lerp(flash.From, flash.To, t);
                flash.Item.localScale = Vector3.one * Mathf.Lerp(flash.StartSize, flash.EndSize, t);
                if (t >= 1f)
                {
                    Destroy(flash.Item.gameObject);
                    _flashes.RemoveAt(i);
                }
            }
            for (var i = _expiring.Count - 1; i >= 0; i--)
            {
                if (Time.time < _expiring[i].Key) continue;
                var system = _expiring[i].Value.GetComponent<ParticleSystem>();
                if (system != null) system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(_expiring[i].Value, 2f);
                _expiring.RemoveAt(i);
            }
        }

        private void AddRubble(BattleBuildingInput building)
        {
            var builder = new MeshBuilder { ContactShade = false };
            var dark = new Color(0.22f, 0.20f, 0.19f);
            var light = new Color(0.42f, 0.39f, 0.36f);
            var random = new System.Random(building.id * 31);
            var size = new Vector3(building.width * 0.42f, 0.18f, building.depth * 0.42f);
            builder.Blob(Vector3.up * 0.03f, size, dark, ArtSurface.Rock, 0f, 0f, 9, 4, 0.14f, building.id, light);
            for (var i = 0; i < building.width * 3; i++)
            {
                var offset = new Vector3(((float)random.NextDouble() - 0.5f) * building.width * 0.8f, 0.08f, ((float)random.NextDouble() - 0.5f) * building.depth * 0.8f);
                builder.Blob(offset, Vector3.one * (0.08f + (float)random.NextDouble() * 0.1f), light, ArtSurface.Rock, 0f, 0f, 5, 3, 0.15f, i);
            }
            var go = new GameObject("Rubble");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = CenterOf(building);
            var mesh = builder.Build("Rubble");
            _ownedMeshes.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = builder.LitMaterials();
        }

        private void OnDestroy()
        {
            foreach (var mesh in _ownedMeshes)
                if (mesh != null) Destroy(mesh);
        }
    }
}

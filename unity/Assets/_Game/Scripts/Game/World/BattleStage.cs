using System;
using System.Collections.Generic;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// Shows a raid: the defender's base, then the battle from its log. A live raid (ADR-050) steps this device's own
    /// copy of the battle in real time while cards are dropped; a fought one (Siege, Arena, Trials) plays the server's
    /// log back. Either way it decides nothing: every hit, death and result comes from the log, and for a live raid the
    /// server fights the same drops again and its result is the one that counts.
    /// </summary>
    public sealed partial class BattleStage : MonoBehaviour
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
            /// <summary>The fighter's role number (BattleSim.RoleNumber): 3 and up fight at range.</summary>
            public int Role;
            public bool Troop;
            /// <summary>How far above the ground a unit that was just dropped still is.</summary>
            public float Drop;
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
        /// <summary>A live raid's battle, stepped here as time passes; null when playing a finished log back.</summary>
        private BattleSim _live;
        /// <summary>Troops landed so far on each cell, so a group spreads out instead of stacking.</summary>
        private readonly Dictionary<long, int> _troopsOnCell = new Dictionary<long, int>();

        /// <summary>How fast the log plays: 1, 2 or 3 times. The result is already decided, so this changes nothing in it.</summary>
        public float PlaybackSpeed = 1f;

        /// <summary>The middle of the attackers still standing, or of the guards once none are; null when nobody is.</summary>
        public Vector3? ActionCenter
        {
            get
            {
                foreach (var group in new[] { _units, _defenders })
                {
                    var sum = Vector3.zero;
                    var count = 0;
                    foreach (var unit in group)
                    {
                        if (unit.Go == null || !unit.Go.activeSelf) continue;
                        sum += unit.Go.transform.position;
                        count++;
                    }
                    if (count > 0) return sum / count;
                }
                return null;
            }
        }
        private long _totalHp;
        private long _dealtHp;
        private bool _finished;

        public bool DojoDown { get; private set; }
        public int DamagePercent => _totalHp == 0 ? 100 : (int)(_dealtHp * 100 / _totalHp);
        public int SecondsLeft => Mathf.Max(0, Mathf.CeilToInt((BattleSim.TickLimit - (_live != null ? _live.Tick : _ticks)) / BattleSim.TicksPerSecond));
        /// <summary>True while a live battle is running here (ADR-050).</summary>
        public bool IsLive => _live != null;
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
                // Like the battle itself: walls and traps count for nothing in the damage score.
                if (!building.isWall && !building.isTrap) _totalHp += building.hp;
            }
            _village = Village.FromSave(catalog, RaidDefinition.ToSave(session.buildings));
            _view = new VillageView(_village, transform, _ => null);
            // Traps are hidden from the attacker until they go off.
            foreach (var building in session.buildings)
                if (building.isTrap) _view.SetGone(building.id);

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
                figure.Role = BattleSim.RoleNumber(defender.role);
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
            figure.Go.transform.localPosition = at + figure.Offset + figure.KickDirection * figure.Kick + Vector3.up * figure.Drop;
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
                figure.Role = BattleSim.RoleNumber(member.role);
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
                        Role = BattleSim.RoleNumber(troop.role),
                        Troop = true,
                        MaxHp = troop.hp,
                        Offset = offset
                    });
                }
            }
        }

        /// <summary>
        /// Starts a live battle (ADR-050) on an empty field. Time runs at the battle's own pace, never faster, so the
        /// server can tell the fight took as long as it says. Units appear as the battle lands them.
        /// </summary>
        public void BeginLive(BattleSim sim)
        {
            _live = sim;
            _ticks = sim.Tick;
        }

        private void UpdateLive()
        {
            if (_finished) return;
            var delta = Time.deltaTime * BattleSim.TicksPerSecond;
            _ticks += delta;
            while (!_live.Done && _live.Tick + 1 <= _ticks) _live.Step();
            if (_live.Done) _ticks = _live.Tick;
            var events = _live.Events;
            while (_nextEvent < events.Count) Apply(events[_nextEvent++], true);
            foreach (var unit in _units) MoveUnit(unit, delta);
            foreach (var unit in _defenders) MoveUnit(unit, delta);
            if (_live.Done) Finish();
        }

        /// <summary>Shows at once whatever the live battle logged and this has not shown yet, so the field matches its result.</summary>
        public void CatchUp()
        {
            if (_live == null) return;
            var events = _live.Events;
            while (_nextEvent < events.Count) Apply(events[_nextEvent++], false);
        }

        /// <summary>A unit the live battle just landed: a hero figure with its bars, or a troop beside its group.</summary>
        private Figure AddLanded(int index, int x, int z)
        {
            if (index != _units.Count) throw new InvalidOperationException($"Unit {index} landed out of order.");
            var input = _live.LandedInput(index);
            var px = x * BattleSim.CellUnits + BattleSim.CellUnits / 2f;
            var pz = z * BattleSim.CellUnits + BattleSim.CellUnits / 2f;
            Figure figure;
            if (!input.isTroop)
            {
                figure = CreateFigure(input.id, x, z);
                figure.Speed = BattleSim.SpeedOf(input.role);
                figure.Role = BattleSim.RoleNumber(input.role);
                figure.MaxHp = input.hp;
                figure.Bar = CreateBar(figure.Go.transform.position + Vector3.up * (figure.Actor != null ? 3.5f : 3.1f), 1.4f, new Color(0.35f, 0.85f, 0.35f));
                figure.Bar.Root.transform.SetParent(figure.Go.transform, true);
                figure.Charge = CreateChargeBar(figure);
            }
            else
            {
                var key = (long)z * VillageGrid.Size + x;
                _troopsOnCell.TryGetValue(key, out var i);
                _troopsOnCell[key] = i + 1;
                var angle = i * 2.4f;
                var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (0.12f + 0.09f * (i % 8));
                figure = new Figure
                {
                    Go = CreateTroop(input.id, new Vector3(px / BattleSim.CellUnits, 0f, pz / BattleSim.CellUnits) + offset),
                    Px = px,
                    Pz = pz,
                    Tx = px,
                    Tz = pz,
                    Speed = BattleSim.SpeedOf(input.role),
                    Role = BattleSim.RoleNumber(input.role),
                    Troop = true,
                    MaxHp = input.hp,
                    Offset = offset
                };
            }
            _units.Add(figure);
            return figure;
        }

        /// <summary>Jumps straight to the result. It changes nothing, as the result is already decided.</summary>
        public void Skip()
        {
            if (_result == null || _live != null) return;
            while (_nextEvent < _result.events.Count) Apply(_result.events[_nextEvent++], false);
            _ticks = _result.endTick;
            Finish();
        }

        private void Update()
        {
            TickBars();
            UpdateFlashes();

            if (_live != null)
            {
                UpdateLive();
                return;
            }
            if (_result == null || _finished) return;
            var delta = Time.deltaTime * BattleSim.TicksPerSecond * PlaybackSpeed;
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
            unit.Drop = Mathf.MoveTowards(unit.Drop, 0f, ticks * 0.6f);
            PlaceFigure(unit);
        }

        /// <summary>
        /// Applies one line of the battle log. What the line changes (health, who is standing, what has fallen) is
        /// always applied; how it looks and sounds is only shown when the battle is being watched.
        /// </summary>
        private void Apply(long[] entry, bool show)
        {
            var kind = (BattleEventKind)entry[1];
            switch (kind)
            {
                case BattleEventKind.Deploy:
                {
                    var figure = AddLanded((int)entry[2], (int)entry[3], (int)entry[4]);
                    if (show) ShowLanding(figure);
                    break;
                }
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
                    SetBuildingBar(building, entry[5], show);
                    if (show) ShowStrike(unit, building, entry[4]);
                    break;
                }
                case BattleEventKind.BuildingDown:
                {
                    var building = _buildings[(int)entry[2]];
                    if (building.definitionId == "dojo_hq") DojoDown = true;
                    ShowCollapse(building, show);
                    _view.SetGone(building.id);
                    if (_buildingBars.TryGetValue(building.id, out var bar))
                    {
                        RemoveBar(bar);
                        _buildingBars.Remove(building.id);
                    }
                    break;
                }
                case BattleEventKind.TowerHit:
                {
                    var unit = _units[(int)entry[3]];
                    unit.Bar?.Set((float)entry[5] / unit.MaxHp);
                    if (show) ShowTowerShot(_buildings[(int)entry[2]], unit, entry[4], entry[0]);
                    break;
                }
                case BattleEventKind.HeroDown:
                {
                    var unit = _units[(int)entry[2]];
                    AddGrave(unit.Go.transform.position);
                    if (show) ShowFall(unit);
                    unit.Go.SetActive(false);
                    break;
                }
                case BattleEventKind.Ability:
                    if (show) ShowAbility(_units[(int)entry[2]], (int)entry[3], 0.5f);
                    break;
                case BattleEventKind.Heal:
                {
                    var unit = _units[(int)entry[2]];
                    unit.Bar?.Set((float)entry[4] / unit.MaxHp);
                    if (show) ShowHeal(unit);
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
                    if (show) ShowBlow(_defenders[(int)entry[2]], target, entry[4], Hurt);
                    break;
                }
                case BattleEventKind.DefenderDamaged:
                {
                    var target = _defenders[(int)entry[3]];
                    target.Bar?.Set((float)entry[5] / target.MaxHp);
                    if (show) ShowBlow(_units[(int)entry[2]], target, entry[4], Ivory);
                    break;
                }
                case BattleEventKind.DefenderDown:
                {
                    var unit = _defenders[(int)entry[2]];
                    AddGrave(unit.Go.transform.position);
                    if (show) ShowFall(unit);
                    unit.Go.SetActive(false);
                    break;
                }
                case BattleEventKind.DefenderAbility:
                    if (show) ShowAbility(_defenders[(int)entry[2]], (int)entry[3], 0.4f);
                    break;
                case BattleEventKind.Charge:
                {
                    var list = entry[4] == 0 ? _units : _defenders;
                    var index = (int)entry[2];
                    if (index >= 0 && index < list.Count) list[index].Charge?.Set((float)entry[3] / BattleSim.ChargeFull);
                    break;
                }
                case BattleEventKind.Rally:
                {
                    if (!show) break;
                    var list = entry[4] == 0 ? _units : _defenders;
                    var index = (int)entry[2];
                    if (index >= 0 && index < list.Count) ShowRally(list[index]);
                    break;
                }
                case BattleEventKind.DefenderHeal:
                {
                    var unit = _defenders[(int)entry[2]];
                    unit.Bar?.Set((float)entry[4] / unit.MaxHp);
                    if (show) ShowHeal(unit);
                    break;
                }
                case BattleEventKind.Stun:
                    if (show) ShowStun(_buildings[(int)entry[2]]);
                    break;
                case BattleEventKind.TrapFired:
                    if (show) ShowTrap(_buildings[(int)entry[2]], entry[3] == 0);
                    break;
                case BattleEventKind.TrapHit:
                {
                    // b is the unit caught. A Hero Trap also holds the hero still for a moment (the sim's stun).
                    var unit = _units[(int)entry[3]];
                    unit.Bar?.Set((float)entry[5] / unit.MaxHp);
                    if (!show) break;
                    ShowNumber(unit.Go.transform.position + Vector3.up * 2.2f, entry[4], new Color(1f, 0.45f, 0.2f));
                    if (_buildings.TryGetValue((int)entry[2], out var trap) && trap.definitionId == "hero_trap") ShowSnare(unit);
                    break;
                }
                case BattleEventKind.SpellCast:
                {
                    var cast = (int)entry[2];
                    _castAt[cast] = new Vector3(entry[3] + 0.5f, 0f, entry[4] + 0.5f);
                    _castEffect[cast] = (int)entry[5];
                    if (show) ShowSpellCast(cast, (int)entry[5]);
                    break;
                }
                case BattleEventKind.SpellPulse:
                    if (show) ShowSpellPulse((int)entry[2], (int)entry[3], (int)entry[4], entry[5] / (float)BattleSim.CellUnits);
                    break;
                case BattleEventKind.SpellHit:
                {
                    var building = _buildings[(int)entry[3]];
                    if (!building.isWall) _dealtHp += entry[4];
                    SetBuildingBar(building, entry[5], show);
                    if (show) ShowSpellHit(building, entry[4]);
                    break;
                }
                case BattleEventKind.SpellHitDefender:
                {
                    var target = _defenders[(int)entry[3]];
                    target.Bar?.Set((float)entry[5] / target.MaxHp);
                    if (show) ShowSpellHitDefender(target, entry[4]);
                    break;
                }
                case BattleEventKind.SpellHeal:
                {
                    var unit = _units[(int)entry[3]];
                    unit.Bar?.Set((float)entry[5] / unit.MaxHp);
                    if (show) ShowHeal(unit);
                    break;
                }
                case BattleEventKind.SpellFreeze:
                {
                    if (!show) break;
                    var seconds = Seconds(entry[5]);
                    if (entry[3] == 0)
                    {
                        if (_buildings.TryGetValue((int)entry[4], out var tower)) ShowFreeze(tower, seconds);
                    }
                    else if (entry[4] >= 0 && entry[4] < _defenders.Count) ShowFreeze(_defenders[(int)entry[4]], seconds);
                    break;
                }
                case BattleEventKind.SpellBoost:
                    if (show) ShowBoost(_units[(int)entry[3]]);
                    break;
            }
        }

        /// <summary>
        /// A building's health bar. It appears on the first hit that is watched; a skipped battle only updates the
        /// bars already up.
        /// </summary>
        private void SetBuildingBar(BattleBuildingInput building, long hpLeft, bool show)
        {
            if (show || _buildingBars.ContainsKey(building.id)) BarFor(building).Set((float)hpLeft / building.hp);
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
            var bar = CreateBar(at, 1.4f, new Color(0.98f, 0.76f, 0.22f), 0.55f, lag: false);
            bar.Root.name = "ChargeBar";
            bar.Root.transform.SetParent(figure.Go.transform, true);
            bar.Normal = bar.FillRenderer.sharedMaterial;
            bar.Full = Mats.OnTop(new Color(0.75f, 0.98f, 1f));
            bar.Set(0f);
            return bar;
        }

        /// <summary>
        /// Game feel when a building falls: the view shakes and time freezes for an instant, more for bigger targets
        /// and most for the Dojo. Walls only nudge the view. Visual only; the battle result is already decided.
        /// </summary>
        private void Impact(BattleBuildingInput building)
        {
            if (building.isWall)
            {
                Shake(0.12f);
                return;
            }
            var dojo = building.definitionId == "dojo_hq";
            Shake(dojo ? 0.85f : 0.4f);
            if (dojo) ZoomPunch(0.07f);
            Feel.HitStop(dojo ? Feel.Tier.Large : Feel.Tier.Medium);
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

        private void OnDestroy()
        {
            foreach (var mesh in _ownedMeshes)
                if (mesh != null) Destroy(mesh);
            DestroyFx();
        }
    }
}

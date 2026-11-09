using System;
using System.Collections.Generic;
using System.Linq;

namespace ShinobiDominion.Core
{
    public sealed class BattleHeroInput
    {
        public string id;
        public string role;
        public long hp;
        public long attack;
        public long defense;
        public int x;
        public int z;
        public bool isTroop;
        /// <summary>The hero's own special in place of its role's, from heroes.json: "" or "rally".</summary>
        public string special;
        /// <summary>Evolutions the hero has had (0 to 3); the rally lasts longer from Evo II.</summary>
        public int evolution;
        /// <summary>How strong the special is, in percent of a Legendary's: by rarity, set by the server. 0 means 100.</summary>
        public int power;
    }

    /// <summary>A hero guarding the village, standing at a cell on or by its Hero Base.</summary>
    public sealed class BattleDefenderInput
    {
        public string id;
        public string role;
        public long hp;
        public long attack;
        public long defense;
        public int x;
        public int z;
        /// <summary>The hero's own special in place of its role's, from heroes.json: "" or "rally".</summary>
        public string special;
        /// <summary>Evolutions the hero has had (0 to 3); the rally lasts longer from Evo II.</summary>
        public int evolution;
        /// <summary>How strong the special is, in percent of a Legendary's: by rarity, set by the server. 0 means 100.</summary>
        public int power;
    }

    public sealed class BattleBuildingInput
    {
        public int id;
        public string definitionId;
        public int x;
        public int z;
        public int width;
        public int depth;
        public int level;
        public long hp;
        public long attack;
        public int range;
        public bool isWall;
        public bool isDefense;
    }

    /// <summary>What each entry in the battle log means. Numbers are shared with the server.</summary>
    public enum BattleEventKind
    {
        /// <summary>a = hero, b = cell x, c = cell z. The hero starts walking towards that cell.</summary>
        Move = 1,
        /// <summary>a = hero, b = building id, c = damage, d = building HP left.</summary>
        HeroHit = 2,
        /// <summary>a = building id.</summary>
        BuildingDown = 3,
        /// <summary>a = building id, b = hero, c = damage, d = hero HP left.</summary>
        TowerHit = 4,
        /// <summary>a = hero.</summary>
        HeroDown = 5,
        /// <summary>a = hero, b = role number.</summary>
        Ability = 6,
        /// <summary>a = hero healed, b = amount, c = hero HP now.</summary>
        Heal = 7,
        /// <summary>a = building id.</summary>
        Stun = 8,
        /// <summary>a = defender, b = cell x, c = cell z. The defender starts walking towards that cell.</summary>
        DefenderMove = 9,
        /// <summary>a = defender, b = attacking unit hit, c = damage, d = unit HP left.</summary>
        DefenderHit = 10,
        /// <summary>a = attacking unit, b = defender hit, c = damage, d = defender HP left.</summary>
        DefenderDamaged = 11,
        /// <summary>a = defender.</summary>
        DefenderDown = 12,
        /// <summary>a = defender, b = role number.</summary>
        DefenderAbility = 13,
        /// <summary>a = defender healed, b = amount, c = defender HP now.</summary>
        DefenderHeal = 14,
        /// <summary>a = unit (c = 0) or defender (c = 1), b = ability charge now, 0 to BattleSim.ChargeFull.</summary>
        Charge = 15,
        /// <summary>a = unit (c = 0) or defender (c = 1) who roared, b = allies (itself included) now attacking faster.</summary>
        Rally = 16
    }

    public sealed class BattleResult
    {
        public int stars;
        public int damagePercent;
        public bool dojoDestroyed;
        public int endTick;
        public int heroesAlive;
        public int defendersAlive;
        public string checksum;
        public List<long[]> events = new List<long[]>();
    }

    /// <summary>
    /// The canonical battle. It uses only whole numbers and has no randomness, so the same input gives
    /// the same log every time. The server's Go copy runs the same scenarios, so the two can't drift
    /// apart unnoticed. All numbers here are unapproved test values.
    /// </summary>
    public sealed class BattleSim
    {
        public const int TicksPerSecond = 10;
        public const int TickLimit = 180 * TicksPerSecond;
        public const int CellUnits = 100;
        public const int DeployClearance = 2;

        private const int RoleTank = 0;
        private const int RoleFighter = 1;
        private const int RoleAssassin = 2;
        private const int RoleMage = 3;
        private const int RoleControl = 4;
        private const int RoleSupport = 5;

        // Routes are priced in ticks. Attackers, heroes and troops alike, break a wall in the way (the
        // hits it takes) or walk around it, whichever is quicker. Only guards hop walls, in twice the
        // time of a step.
        private const int HopSteps = 2;
        private const int MaxPathCost = 1_000_000;
        private const int GridSize = VillageGrid.Size;
        private const string HeroBaseId = "hero_base";

        // How far a defender notices attackers, and how far from its post it will chase them.
        private const int DefenderAggro = 800;
        private const int DefenderLeash = 1400;

        /// <summary>
        /// Landed hits that fill a hero's ability bar (owner's rule, 2026-11-08). At full, the special fires and the
        /// bar empties: a power strike for fighters, assassins and mages on their next hit; guard, mend or stun for
        /// tanks, supports and controls as soon as it can do something. Troops have no special.
        /// </summary>
        public const int ChargeFull = 5;

        /// <summary>
        /// Rally (Solar Beast Sage's Radiant Roar, owner's design 2026-11-08): at full charge the hero roars at once, and
        /// every ally within RallyRange, itself and troops included, attacks HastePercent faster for HasteTicks.
        /// </summary>
        public const string SpecialRally = "rally";
        public const int RallyRange = 600;
        public const int HasteTicks = 60;
        /// <summary>Roaring Sun (Evo II and up): the haste lasts this long instead.</summary>
        public const int HasteTicksEvolved = 90;
        public const int HastePercent = 30;

        /// <summary>
        /// Special power by rarity (owner's rule, 2026-11-08): Common specials still fire but are weak, Elite a little
        /// stronger, Legendary full. It scales a special's extra effect: bonus damage, guard and stun time, heals.
        /// </summary>
        public const int PowerCommon = 50;
        public const int PowerElite = 75;
        public const int PowerLegendary = 100;

        public static int PowerFor(string rarity) => rarity == "Common" ? PowerCommon : rarity == "Elite" ? PowerElite : PowerLegendary;

        private static int PowerOf(int power) => power <= 0 ? PowerLegendary : power;

        /// <summary>A burst percent with only its extra part scaled: 300 at power 50 is 200.</summary>
        private static long Boost(long percent, int power) => 100 + (percent - 100) * power / 100;

        // Per role: attack range, walking speed per tick, ticks between attacks, and percent of attack dealt to buildings.
        private static readonly int[] Ranges = { 120, 120, 120, 500, 400, 400 };
        private static readonly int[] Speeds = { 9, 11, 16, 10, 10, 10 };
        private static readonly int[] Intervals = { 15, 12, 8, 20, 18, 18 };
        private static readonly int[] DamagePercents = { 70, 100, 100, 100, 80, 60 };

        public static int RoleNumber(string role)
        {
            switch (role)
            {
                case "Tank": return RoleTank;
                case "Fighter": return RoleFighter;
                case "Assassin": return RoleAssassin;
                case "Mage": return RoleMage;
                case "Control": return RoleControl;
                case "Support": return RoleSupport;
                default: throw new FormatException("Unknown role " + role);
            }
        }

        public static int SpeedOf(string role) => Speeds[RoleNumber(role)];

        private sealed class Unit
        {
            public int Index;
            public int Role;
            public long Hp;
            public long MaxHp;
            public long Attack;
            public long Defense;
            public int Px;
            public int Pz;
            public int Target = -1;
            public int Blocker = -1;
            public int[] Path = new int[0];
            public int PathAt;
            public bool NeedsPlan = true;
            public int Idle;
            public int AttackTimer;
            public int Charge;
            public int GuardTimer;
            public int HasteTimer;
            public bool Rally;
            public int Evolution;
            public int Power = 100;
            public bool IsTroop;
        }

        private sealed class Defender
        {
            public int Index;
            public int Role;
            public long Hp;
            public long MaxHp;
            public long Attack;
            public long Defense;
            public int Px;
            public int Pz;
            public int Tx;
            public int Tz;
            public int HomeX;
            public int HomeZ;
            public int AttackTimer;
            public int Charge;
            public int GuardTimer;
            public int HasteTimer;
            public bool Rally;
            public int Evolution;
            public int Power = 100;
        }

        private sealed class Structure
        {
            public BattleBuildingInput Input;
            public long Hp;
            public bool Alive = true;
            public int ShotTimer;
            public int StunTimer;
        }

        private readonly Unit[] _units;
        private readonly Defender[] _defenders;
        private readonly Structure[] _structures;
        private readonly int[] _grid = new int[GridSize * GridSize];
        private readonly int[] _cost = new int[GridSize * GridSize];
        private readonly int[] _from = new int[GridSize * GridSize];
        private readonly List<long> _heap = new List<long>();
        private int _pathStep;
        private long _pathPerHit;
        private int _pathInterval;
        private readonly BattleResult _result = new BattleResult();
        private long _totalHp;
        private long _dealtHp;
        private int _tick;

        private BattleSim(IEnumerable<BattleBuildingInput> buildings, IEnumerable<BattleHeroInput> heroes, IEnumerable<BattleDefenderInput> defenders)
        {
            _structures = buildings.Select(b => new Structure { Input = b, Hp = b.hp }).ToArray();
            for (var i = 0; i < _structures.Length; i++)
            {
                var b = _structures[i].Input;
                if (!b.isWall) _totalHp += b.hp;
                for (var z = b.z; z < b.z + b.depth; z++)
                for (var x = b.x; x < b.x + b.width; x++)
                    _grid[z * GridSize + x] = i + 1;
            }
            _units = heroes.Select((h, i) =>
            {
                var role = RoleNumber(h.role);
                return new Unit
                {
                    Index = i,
                    Role = role,
                    Hp = h.hp,
                    MaxHp = h.hp,
                    Attack = h.attack,
                    Defense = h.defense,
                    Px = h.x * CellUnits + CellUnits / 2,
                    Pz = h.z * CellUnits + CellUnits / 2,
                    IsTroop = h.isTroop,
                    Rally = !h.isTroop && h.special == SpecialRally,
                    Evolution = h.evolution,
                    Power = PowerOf(h.power)
                };
            }).ToArray();
            _defenders = (defenders ?? Enumerable.Empty<BattleDefenderInput>()).Select((d, i) =>
            {
                var role = RoleNumber(d.role);
                var px = d.x * CellUnits + CellUnits / 2;
                var pz = d.z * CellUnits + CellUnits / 2;
                return new Defender
                {
                    Index = i,
                    Role = role,
                    Hp = d.hp,
                    MaxHp = d.hp,
                    Attack = d.attack,
                    Defense = d.defense,
                    Px = px,
                    Pz = pz,
                    Tx = px,
                    Tz = pz,
                    HomeX = d.x,
                    HomeZ = d.z,
                    Rally = d.special == SpecialRally,
                    Evolution = d.evolution,
                    Power = PowerOf(d.power)
                };
            }).ToArray();
        }

        public static BattleResult Run(IEnumerable<BattleBuildingInput> buildings, IEnumerable<BattleHeroInput> heroes, IEnumerable<BattleDefenderInput> defenders = null)
        {
            var sim = new BattleSim(buildings, heroes, defenders);
            sim.Play();
            return sim._result;
        }

        /// <summary>Empty when the placement is legal, otherwise the reason. The server uses the same names.</summary>
        public static string DeployError(IList<BattleBuildingInput> buildings, IList<BattleHeroInput> heroes)
        {
            if (heroes.Count < 1) return "NO_TEAM";
            if (heroes.Count(h => !h.isTroop) > 6) return "TOO_MANY_HEROES";
            for (var i = 0; i < heroes.Count; i++)
            {
                var h = heroes[i];
                if (h.x < 0 || h.x >= GridSize || h.z < 0 || h.z >= GridSize) return "BAD_DEPLOY_CELL";
                if (!CanDeployAt(buildings, h.x, h.z)) return "DEPLOY_TOO_CLOSE";
                if (h.isTroop) continue;
                for (var j = 0; j < i; j++)
                {
                    if (heroes[j].isTroop) continue;
                    if (heroes[j].id == h.id) return "DUPLICATE_HERO";
                    if (heroes[j].x == h.x && heroes[j].z == h.z) return "DEPLOY_CELL_TAKEN";
                }
            }
            return "";
        }

        public static bool CanDeployAt(IEnumerable<BattleBuildingInput> buildings, int x, int z)
        {
            foreach (var b in buildings)
                if (x >= b.x - DeployClearance && x < b.x + b.width + DeployClearance && z >= b.z - DeployClearance && z < b.z + b.depth + DeployClearance)
                    return false;
            return true;
        }

        private void Play()
        {
            for (_tick = 1; _tick <= TickLimit; _tick++)
            {
                foreach (var unit in _units)
                    if (unit.Hp > 0) StepUnit(unit);
                foreach (var defender in _defenders)
                    if (defender.Hp > 0) StepDefender(defender);
                foreach (var structure in _structures)
                    if (structure.Alive && structure.Input.isDefense) StepTower(structure);
                if (Finished()) break;
            }
            var endTick = Math.Min(_tick, TickLimit);

            _result.endTick = endTick;
            _result.damagePercent = _totalHp == 0 ? 100 : (int)(_dealtHp * 100 / _totalHp);
            _result.heroesAlive = _units.Count(u => u.Hp > 0);
            _result.defendersAlive = _defenders.Count(d => d.Hp > 0);
            foreach (var structure in _structures)
                if (structure.Input.definitionId == "dojo_hq" && !structure.Alive) _result.dojoDestroyed = true;
            if (_result.damagePercent >= 50) _result.stars++;
            if (_result.dojoDestroyed) _result.stars++;
            if (_result.damagePercent >= 100) _result.stars++;
            _result.checksum = Checksum(_result.events);
        }

        private bool Finished()
        {
            var anyHero = false;
            foreach (var unit in _units) if (unit.Hp > 0) anyHero = true;
            if (!anyHero) return true;
            foreach (var structure in _structures) if (structure.Alive && !structure.Input.isWall) return false;
            return true;
        }

        public static string Checksum(IEnumerable<long[]> events)
        {
            var hash = 14695981039346656037UL;
            unchecked
            {
                foreach (var entry in events)
                foreach (var value in entry)
                {
                    hash ^= (ulong)value;
                    hash *= 1099511628211UL;
                }
            }
            return hash.ToString("x16");
        }

        private void Log(BattleEventKind kind, long a, long b = 0, long c = 0, long d = 0) =>
            _result.events.Add(new[] { _tick, (long)kind, a, b, c, d });

        // ---------- Ability charge ----------

        /// <summary>A landed hit adds one charge, up to full. Troops have no special.</summary>
        private void Gain(Unit unit)
        {
            if (unit.IsTroop || unit.Charge >= ChargeFull) return;
            unit.Charge++;
            Log(BattleEventKind.Charge, unit.Index, unit.Charge, 0);
        }

        private void Spend(Unit unit)
        {
            unit.Charge = 0;
            Log(BattleEventKind.Charge, unit.Index, 0, 0);
        }

        private void Gain(Defender defender)
        {
            if (defender.Charge >= ChargeFull) return;
            defender.Charge++;
            Log(BattleEventKind.Charge, defender.Index, defender.Charge, 1);
        }

        private void Spend(Defender defender)
        {
            defender.Charge = 0;
            Log(BattleEventKind.Charge, defender.Index, 0, 1);
        }

        // ---------- Rally ----------

        /// <summary>Ticks between attacks for a role, shortened while hasted.</summary>
        private static int Interval(int role, int hasteTimer) =>
            hasteTimer > 0 ? Intervals[role] * 100 / (100 + HastePercent) : Intervals[role];

        public static int HasteFor(int evolution) => evolution >= 2 ? HasteTicksEvolved : HasteTicks;

        private void RallyUnits(Unit roarer)
        {
            Spend(roarer);
            Log(BattleEventKind.Ability, roarer.Index, roarer.Role);
            var count = 0;
            foreach (var other in _units)
            {
                if (other.Hp <= 0 || SquaredPoints(other.Px, other.Pz, roarer.Px, roarer.Pz) > (long)RallyRange * RallyRange) continue;
                other.HasteTimer = HasteFor(roarer.Evolution);
                count++;
            }
            Log(BattleEventKind.Rally, roarer.Index, count, 0);
        }

        private void RallyDefenders(Defender roarer)
        {
            Spend(roarer);
            Log(BattleEventKind.DefenderAbility, roarer.Index, roarer.Role);
            var count = 0;
            foreach (var other in _defenders)
            {
                if (other.Hp <= 0 || SquaredPoints(other.Px, other.Pz, roarer.Px, roarer.Pz) > (long)RallyRange * RallyRange) continue;
                other.HasteTimer = HasteFor(roarer.Evolution);
                count++;
            }
            Log(BattleEventKind.Rally, roarer.Index, count, 1);
        }

        // ---------- Heroes ----------

        private void StepUnit(Unit unit)
        {
            if (unit.AttackTimer > 0) unit.AttackTimer--;
            if (unit.GuardTimer > 0) unit.GuardTimer--;
            if (unit.HasteTimer > 0) unit.HasteTimer--;
            if (unit.Idle > 0) unit.Idle--;
            SelfAbility(unit);

            // A defender in reach comes first. The attacker stands and fights, then carries on.
            var foe = ClosestDefender(unit);
            if (foe != null)
            {
                if (unit.AttackTimer == 0)
                {
                    StrikeDefender(unit, foe);
                    unit.AttackTimer = Interval(unit.Role, unit.HasteTimer);
                }
                return;
            }

            if (unit.NeedsPlan || unit.Target < 0 || !_structures[unit.Target].Alive)
            {
                if (unit.Idle > 0) return;
                Plan(unit);
            }
            if (unit.Target < 0) return;

            var focus = unit.Blocker >= 0 && _structures[unit.Blocker].Alive ? unit.Blocker : unit.Target;
            if (InRange(unit.Px, unit.Pz, _structures[focus].Input, Ranges[unit.Role]))
            {
                if (unit.AttackTimer == 0)
                {
                    Strike(unit, focus);
                    unit.AttackTimer = Interval(unit.Role, unit.HasteTimer);
                }
                return;
            }

            if (unit.PathAt >= unit.Path.Length)
            {
                unit.NeedsPlan = true;
                return;
            }
            var next = unit.Path[unit.PathAt];
            var tx = (next % GridSize) * CellUnits + CellUnits / 2;
            var tz = (next / GridSize) * CellUnits + CellUnits / 2;
            var speed = Speeds[unit.Role];
            if (unit.Px != tx)
            {
                var step = Math.Min(Math.Abs(tx - unit.Px), speed);
                unit.Px += tx > unit.Px ? step : -step;
            }
            else
            {
                var step = Math.Min(Math.Abs(tz - unit.Pz), speed);
                unit.Pz += tz > unit.Pz ? step : -step;
            }
            if (unit.Px == tx && unit.Pz == tz)
            {
                unit.PathAt++;
                if (unit.PathAt < unit.Path.Length) LogMove(unit);
            }
        }

        private void LogMove(Unit unit)
        {
            var next = unit.Path[unit.PathAt];
            Log(BattleEventKind.Move, unit.Index, next % GridSize, next / GridSize);
        }

        private void SelfAbility(Unit unit)
        {
            if (unit.Charge < ChargeFull || unit.IsTroop) return;
            if (unit.Rally)
            {
                RallyUnits(unit);
                return;
            }
            switch (unit.Role)
            {
                case RoleTank:
                    // Guard: towers in range aim at this hero, and it takes half damage.
                    unit.GuardTimer = 40 * unit.Power / 100;
                    Spend(unit);
                    Log(BattleEventKind.Ability, unit.Index, RoleTank);
                    break;
                case RoleSupport:
                    // Mend: heals everyone nearby who is hurt.
                    var healed = false;
                    foreach (var other in _units)
                    {
                        if (other.Hp <= 0 || other.Hp >= other.MaxHp) continue;
                        var dx = (long)(other.Px - unit.Px);
                        var dz = (long)(other.Pz - unit.Pz);
                        if (dx * dx + dz * dz > 600L * 600L) continue;
                        var amount = Math.Min(unit.Attack * 2 * unit.Power / 100, other.MaxHp - other.Hp);
                        other.Hp += amount;
                        if (!healed)
                        {
                            Log(BattleEventKind.Ability, unit.Index, RoleSupport);
                            healed = true;
                        }
                        Log(BattleEventKind.Heal, other.Index, amount, other.Hp);
                    }
                    if (healed) Spend(unit);
                    break;
                case RoleControl:
                    // Stun: silences the closest working tower within reach.
                    var best = -1;
                    var bestDistance = long.MaxValue;
                    for (var i = 0; i < _structures.Length; i++)
                    {
                        var s = _structures[i];
                        if (!s.Alive || !s.Input.isDefense || s.StunTimer > 0) continue;
                        var distance = SquaredDistance(unit.Px, unit.Pz, s.Input);
                        var reach = (long)Ranges[RoleControl] + 200;
                        if (distance <= reach * reach && distance < bestDistance)
                        {
                            best = i;
                            bestDistance = distance;
                        }
                    }
                    if (best >= 0)
                    {
                        _structures[best].StunTimer = 30 * unit.Power / 100;
                        Spend(unit);
                        Log(BattleEventKind.Ability, unit.Index, RoleControl);
                        Log(BattleEventKind.Stun, _structures[best].Input.id);
                    }
                    break;
            }
        }

        private void Strike(Unit unit, int focus)
        {
            var base_ = unit.Attack * DamagePercents[unit.Role] / 100;
            var percent = 100L;
            var splash = 0;
            if (unit.Charge >= ChargeFull && !unit.IsTroop && !unit.Rally)
            {
                switch (unit.Role)
                {
                    case RoleFighter:
                        percent = 150;
                        splash = 150;
                        break;
                    case RoleAssassin:
                        percent = 300;
                        break;
                    case RoleMage:
                        percent = 200;
                        splash = 250;
                        break;
                }
                if (percent != 100)
                {
                    percent = Boost(percent, unit.Power);
                    Spend(unit);
                    Log(BattleEventKind.Ability, unit.Index, unit.Role);
                }
            }
            if (percent == 100) Gain(unit);

            var input = _structures[focus].Input;
            var cx = input.x * CellUnits + input.width * CellUnits / 2;
            var cz = input.z * CellUnits + input.depth * CellUnits / 2;
            Damage(unit, focus, base_ * percent / 100);
            if (splash == 0) return;
            for (var i = 0; i < _structures.Length; i++)
            {
                if (i == focus || !_structures[i].Alive) continue;
                if (SquaredDistance(cx, cz, _structures[i].Input) <= (long)splash * splash)
                    Damage(unit, i, base_);
            }
        }

        private void Damage(Unit unit, int index, long amount)
        {
            var structure = _structures[index];
            amount = Math.Min(Math.Max(amount, 1), structure.Hp);
            structure.Hp -= amount;
            if (!structure.Input.isWall) _dealtHp += amount;
            Log(BattleEventKind.HeroHit, unit.Index, structure.Input.id, amount, structure.Hp);
            if (structure.Hp > 0) return;

            structure.Alive = false;
            var b = structure.Input;
            for (var z = b.z; z < b.z + b.depth; z++)
            for (var x = b.x; x < b.x + b.width; x++)
                _grid[z * GridSize + x] = 0;
            Log(BattleEventKind.BuildingDown, b.id);
            foreach (var other in _units)
                if (other.Target == index || other.Blocker == index)
                    other.NeedsPlan = true;
        }

        // ---------- Defenders ----------

        private static long SquaredPoints(int ax, int az, int bx, int bz)
        {
            var dx = (long)(ax - bx);
            var dz = (long)(az - bz);
            return dx * dx + dz * dz;
        }

        private Defender ClosestDefender(Unit unit)
        {
            Defender best = null;
            var bestDistance = long.MaxValue;
            var reach = (long)Ranges[unit.Role] * Ranges[unit.Role];
            foreach (var defender in _defenders)
            {
                if (defender.Hp <= 0) continue;
                var distance = SquaredPoints(unit.Px, unit.Pz, defender.Px, defender.Pz);
                if (distance <= reach && distance < bestDistance)
                {
                    best = defender;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private static long BurstPercent(int role)
        {
            switch (role)
            {
                case RoleFighter: return 150;
                case RoleAssassin: return 300;
                case RoleMage: return 200;
                default: return 100;
            }
        }

        private void StrikeDefender(Unit unit, Defender defender)
        {
            var percent = 100L;
            if (unit.Charge >= ChargeFull && !unit.IsTroop && !unit.Rally)
            {
                percent = Boost(BurstPercent(unit.Role), unit.Power);
                if (percent != 100)
                {
                    Spend(unit);
                    Log(BattleEventKind.Ability, unit.Index, unit.Role);
                }
            }
            if (percent == 100) Gain(unit);
            var amount = unit.Attack * percent / 100 * 100 / (100 + defender.Defense);
            if (defender.GuardTimer > 0) amount /= 2;
            amount = Math.Min(Math.Max(amount, 1), defender.Hp);
            defender.Hp -= amount;
            Log(BattleEventKind.DefenderDamaged, unit.Index, defender.Index, amount, defender.Hp);
            if (defender.Hp == 0) Log(BattleEventKind.DefenderDown, defender.Index);
        }

        private void StepDefender(Defender defender)
        {
            if (defender.AttackTimer > 0) defender.AttackTimer--;
            if (defender.GuardTimer > 0) defender.GuardTimer--;
            if (defender.HasteTimer > 0) defender.HasteTimer--;

            var target = ClosestAttacker(defender);
            if (defender.Charge >= ChargeFull && defender.Rally)
            {
                RallyDefenders(defender);
            }
            else if (defender.Charge >= ChargeFull)
            {
                if (defender.Role == RoleTank && target != null)
                {
                    defender.GuardTimer = 40 * defender.Power / 100;
                    Spend(defender);
                    Log(BattleEventKind.DefenderAbility, defender.Index, RoleTank);
                }
                else if (defender.Role == RoleSupport)
                {
                    MendDefenders(defender);
                }
            }

            if (target == null)
            {
                WalkDefender(defender, defender.HomeX, defender.HomeZ);
                return;
            }
            var reach = (long)Ranges[defender.Role] * Ranges[defender.Role];
            if (SquaredPoints(defender.Px, defender.Pz, target.Px, target.Pz) <= reach)
            {
                if (defender.AttackTimer == 0)
                {
                    DefenderStrike(defender, target);
                    defender.AttackTimer = Interval(defender.Role, defender.HasteTimer);
                }
                return;
            }
            WalkDefender(defender, target.Px / CellUnits, target.Pz / CellUnits);
        }

        private Unit ClosestAttacker(Defender defender)
        {
            Unit best = null;
            var bestDistance = long.MaxValue;
            var homeX = defender.HomeX * CellUnits + CellUnits / 2;
            var homeZ = defender.HomeZ * CellUnits + CellUnits / 2;
            foreach (var unit in _units)
            {
                if (unit.Hp <= 0) continue;
                var distance = SquaredPoints(defender.Px, defender.Pz, unit.Px, unit.Pz);
                if (distance > (long)DefenderAggro * DefenderAggro) continue;
                if (SquaredPoints(homeX, homeZ, unit.Px, unit.Pz) > (long)DefenderLeash * DefenderLeash) continue;
                if (distance < bestDistance)
                {
                    best = unit;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private void MendDefenders(Defender healer)
        {
            var healed = false;
            foreach (var other in _defenders)
            {
                if (other.Hp <= 0 || other.Hp >= other.MaxHp) continue;
                if (SquaredPoints(other.Px, other.Pz, healer.Px, healer.Pz) > 600L * 600L) continue;
                var amount = Math.Min(healer.Attack * 2 * healer.Power / 100, other.MaxHp - other.Hp);
                other.Hp += amount;
                if (!healed)
                {
                    Log(BattleEventKind.DefenderAbility, healer.Index, RoleSupport);
                    healed = true;
                }
                Log(BattleEventKind.DefenderHeal, other.Index, amount, other.Hp);
            }
            if (healed) Spend(healer);
        }

        private void DefenderStrike(Defender defender, Unit unit)
        {
            var percent = 100L;
            if (defender.Charge >= ChargeFull && !defender.Rally)
            {
                percent = Boost(BurstPercent(defender.Role), defender.Power);
                if (percent != 100)
                {
                    Spend(defender);
                    Log(BattleEventKind.DefenderAbility, defender.Index, defender.Role);
                }
            }
            if (percent == 100) Gain(defender);
            var amount = defender.Attack * percent / 100 * 100 / (100 + unit.Defense);
            if (unit.GuardTimer > 0) amount /= 2;
            amount = Math.Min(Math.Max(amount, 1), unit.Hp);
            unit.Hp -= amount;
            Log(BattleEventKind.DefenderHit, defender.Index, unit.Index, amount, unit.Hp);
            if (unit.Hp == 0) Log(BattleEventKind.HeroDown, unit.Index);
        }

        /// <summary>Guards are heroes, so they hop walls; only buildings stop them.</summary>
        private bool DefenderCanEnter(int x, int z)
        {
            var occupant = _grid[z * GridSize + x];
            return occupant == 0 || _structures[occupant - 1].Input.definitionId == HeroBaseId || _structures[occupant - 1].Input.isWall;
        }

        private bool IsWallCell(int cell)
        {
            var occupant = _grid[cell];
            return occupant != 0 && _structures[occupant - 1].Input.isWall;
        }

        /// <summary>One cell at a time towards a goal, along x and then z. Buildings stop it; walls slow it while it hops.</summary>
        private void WalkDefender(Defender defender, int goalX, int goalZ)
        {
            if (defender.Px == defender.Tx && defender.Pz == defender.Tz)
            {
                var x = defender.Px / CellUnits;
                var z = defender.Pz / CellUnits;
                if (goalX != x && DefenderCanEnter(x + Math.Sign(goalX - x), z)) x += Math.Sign(goalX - x);
                else if (goalZ != z && DefenderCanEnter(x, z + Math.Sign(goalZ - z))) z += Math.Sign(goalZ - z);
                else return;
                defender.Tx = x * CellUnits + CellUnits / 2;
                defender.Tz = z * CellUnits + CellUnits / 2;
                Log(BattleEventKind.DefenderMove, defender.Index, x, z);
            }
            var speed = Speeds[defender.Role];
            if (IsWallCell(defender.Tz / CellUnits * GridSize + defender.Tx / CellUnits)) speed = Math.Max(1, speed / HopSteps);
            if (defender.Px != defender.Tx)
            {
                var step = Math.Min(Math.Abs(defender.Tx - defender.Px), speed);
                defender.Px += defender.Tx > defender.Px ? step : -step;
            }
            else
            {
                var step = Math.Min(Math.Abs(defender.Tz - defender.Pz), speed);
                defender.Pz += defender.Tz > defender.Pz ? step : -step;
            }
        }

        // ---------- Choosing targets and paths ----------

        private void Plan(Unit unit)
        {
            unit.NeedsPlan = false;
            unit.Blocker = -1;
            unit.Path = new int[0];
            unit.PathAt = 0;
            unit.Target = ChooseTarget(unit);
            if (unit.Target < 0) return;

            var path = FindPath(unit);
            if (path == null)
            {
                // Sealed in by buildings. Wait and try again rather than searching every tick.
                unit.Target = -1;
                unit.Idle = 20;
                return;
            }

            // Walk up to the first wall in the way, and break it.
            var length = path.Count;
            for (var i = 0; i < path.Count; i++)
            {
                var occupant = _grid[path[i]];
                if (occupant != 0 && _structures[occupant - 1].Input.isWall)
                {
                    unit.Blocker = occupant - 1;
                    length = i;
                    break;
                }
            }
            unit.Path = path.Take(length).ToArray();
            if (unit.Path.Length > 0) LogMove(unit);
        }

        private int ChooseTarget(Unit unit)
        {
            var wantDefense = unit.Role == RoleAssassin && _structures.Any(s => s.Alive && s.Input.isDefense);
            var best = -1;
            var bestDistance = long.MaxValue;
            for (var i = 0; i < _structures.Length; i++)
            {
                var s = _structures[i];
                if (!s.Alive || s.Input.isWall) continue;
                if (wantDefense && !s.Input.isDefense) continue;
                var distance = SquaredDistance(unit.Px, unit.Pz, s.Input);
                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private List<int> FindPath(Unit unit)
        {
            var target = _structures[unit.Target].Input;
            var range = Ranges[unit.Role];
            var start = Math.Min(unit.Pz / CellUnits, GridSize - 1) * GridSize + Math.Min(unit.Px / CellUnits, GridSize - 1);
            var speed = Speeds[unit.Role];
            _pathStep = (CellUnits + speed - 1) / speed;
            _pathPerHit = Math.Max(1, unit.Attack * DamagePercents[unit.Role] / 100);
            _pathInterval = Intervals[unit.Role];
            for (var i = 0; i < _cost.Length; i++) _cost[i] = int.MaxValue;
            _heap.Clear();
            _cost[start] = 0;
            _from[start] = -1;
            HeapPush(start);

            while (_heap.Count > 0)
            {
                var entry = HeapPop();
                var cell = (int)(entry % 4096);
                var cost = (int)(entry / 4096);
                if (cost != _cost[cell]) continue;
                var cx = cell % GridSize;
                var cz = cell / GridSize;
                if (InRange(cx * CellUnits + CellUnits / 2, cz * CellUnits + CellUnits / 2, target, range))
                {
                    var path = new List<int>();
                    for (var at = cell; at != start; at = _from[at]) path.Add(at);
                    path.Reverse();
                    return path;
                }
                if (cx + 1 < GridSize) Relax(cell, cell + 1, cost);
                if (cx > 0) Relax(cell, cell - 1, cost);
                if (cz + 1 < GridSize) Relax(cell, cell + GridSize, cost);
                if (cz > 0) Relax(cell, cell - GridSize, cost);
            }
            return null;
        }

        private void Relax(int from, int to, int cost)
        {
            var occupant = _grid[to];
            var step = _pathStep;
            if (occupant != 0)
            {
                var structure = _structures[occupant - 1];
                if (!structure.Input.isWall) return;
                step = WallBreakTicks(structure.Hp);
            }
            if (cost + step >= _cost[to]) return;
            _cost[to] = cost + step;
            _from[to] = from;
            HeapPush(to);
        }

        /// <summary>The ticks an attacker spends breaking a wall with this much health, then stepping through.</summary>
        private int WallBreakTicks(long hp)
        {
            var hits = (hp + _pathPerHit - 1) / _pathPerHit;
            var ticks = hits * _pathInterval + _pathStep;
            return ticks > MaxPathCost ? MaxPathCost : (int)ticks;
        }

        private void HeapPush(int cell)
        {
            _heap.Add((long)_cost[cell] * 4096 + cell);
            var i = _heap.Count - 1;
            while (i > 0)
            {
                var parent = (i - 1) / 2;
                if (_heap[parent] <= _heap[i]) break;
                (_heap[parent], _heap[i]) = (_heap[i], _heap[parent]);
                i = parent;
            }
        }

        private long HeapPop()
        {
            var top = _heap[0];
            var last = _heap.Count - 1;
            _heap[0] = _heap[last];
            _heap.RemoveAt(last);
            var i = 0;
            while (true)
            {
                var left = i * 2 + 1;
                var right = left + 1;
                var least = i;
                if (left < _heap.Count && _heap[left] < _heap[least]) least = left;
                if (right < _heap.Count && _heap[right] < _heap[least]) least = right;
                if (least == i) break;
                (_heap[least], _heap[i]) = (_heap[i], _heap[least]);
                i = least;
            }
            return top;
        }

        private static long SquaredDistance(int px, int pz, BattleBuildingInput b)
        {
            var minX = b.x * CellUnits;
            var maxX = (b.x + b.width) * CellUnits;
            var minZ = b.z * CellUnits;
            var maxZ = (b.z + b.depth) * CellUnits;
            long dx = px < minX ? minX - px : px > maxX ? px - maxX : 0;
            long dz = pz < minZ ? minZ - pz : pz > maxZ ? pz - maxZ : 0;
            return dx * dx + dz * dz;
        }

        private static bool InRange(int px, int pz, BattleBuildingInput b, int range) =>
            SquaredDistance(px, pz, b) <= (long)range * range;

        // ---------- Towers ----------

        private void StepTower(Structure tower)
        {
            if (tower.ShotTimer > 0) tower.ShotTimer--;
            if (tower.StunTimer > 0)
            {
                tower.StunTimer--;
                return;
            }
            if (tower.ShotTimer > 0) return;

            var input = tower.Input;
            var splashing = input.definitionId == "spirit_turret";
            var reach = (long)input.range * CellUnits;
            Unit chosen = null;
            var chosenDistance = long.MaxValue;
            var chosenGuard = false;
            foreach (var unit in _units)
            {
                if (unit.Hp <= 0) continue;
                var distance = SquaredDistance(unit.Px, unit.Pz, input);
                if (distance > reach * reach) continue;
                var guarding = unit.GuardTimer > 0;
                if (chosen == null || (guarding && !chosenGuard) || (guarding == chosenGuard && distance < chosenDistance))
                {
                    chosen = unit;
                    chosenDistance = distance;
                    chosenGuard = guarding;
                }
            }
            if (chosen == null) return;

            tower.ShotTimer = splashing ? 15 : 10;
            Shoot(tower, chosen, 100);
            if (!splashing) return;
            foreach (var unit in _units)
            {
                if (unit == chosen || unit.Hp <= 0) continue;
                var dx = (long)(unit.Px - chosen.Px);
                var dz = (long)(unit.Pz - chosen.Pz);
                if (dx * dx + dz * dz <= 150L * 150L) Shoot(tower, unit, 50);
            }
        }

        private void Shoot(Structure tower, Unit unit, long percent)
        {
            var amount = tower.Input.attack * percent / 100 * 100 / (100 + unit.Defense);
            if (unit.GuardTimer > 0) amount /= 2;
            amount = Math.Min(Math.Max(amount, 1), unit.Hp);
            unit.Hp -= amount;
            Log(BattleEventKind.TowerHit, tower.Input.id, unit.Index, amount, unit.Hp);
            if (unit.Hp == 0) Log(BattleEventKind.HeroDown, unit.Index);
        }
    }
}

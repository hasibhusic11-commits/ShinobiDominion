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
        /// <summary>
        /// When the card was dropped (ADR-050): the unit lands after this tick and acts from the next one. 0 lands it
        /// before the first tick. Only a timed battle (one with an end tick) uses it; elsewhere it must be 0.
        /// </summary>
        public int deployTick;
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
        /// <summary>A hidden trap: walked over, never targeted, and counts for nothing in the score (ADR-046).</summary>
        public bool isTrap;
    }

    /// <summary>
    /// One cast of a battle spell (ADR-052), already worked out from spells.json for the caster's spell level, so the
    /// sim needs no catalog. SpellDefinition.ToInput builds it on the client; the server builds its own from the same file.
    /// </summary>
    public sealed class BattleSpellInput
    {
        /// <summary>The spell's id in spells.json. For the caller's use; the sim does not read it.</summary>
        public string id;
        /// <summary>"damage", "heal", "freeze" or "boost".</summary>
        public string effect;
        /// <summary>The cell the spell is centred on: any cell of the grid, buildings included.</summary>
        public int x;
        public int z;
        /// <summary>Reach from the middle of that cell, in battle units (BattleSim.CellUnits to a cell).</summary>
        public int radius;
        /// <summary>How many times it applies. Freeze and boost apply once.</summary>
        public int pulses;
        /// <summary>Ticks from one application to the next.</summary>
        public int pulseTicks;
        /// <summary>Damage or heal per pulse in HP, or the percent more damage of a boost. 0 for a freeze.</summary>
        public long power;
        /// <summary>How long a freeze or a boost lasts, in ticks. 0 for damage and heal.</summary>
        public int ticks;
        /// <summary>When it was cast: it lands after this tick and first applies on the next one.</summary>
        public int tick;
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
        Rally = 16,
        /// <summary>a = trap building id, b = 0 for a Bomb Trap or 1 for a Hero Trap. The trap is spent.</summary>
        TrapFired = 17,
        /// <summary>a = trap building id, b = unit hit, c = damage, d = unit HP left.</summary>
        TrapHit = 18,
        /// <summary>
        /// Timed battles only (ADR-050): a = unit, b = cell x, c = cell z. The unit lands there; it acts from this tick.
        /// Units are numbered in landing order.
        /// </summary>
        Deploy = 19,
        /// <summary>
        /// Timed battles only (ADR-052): a = cast number, b = cell x, c = cell z, d = effect number (BattleSim.EffectDamage
        /// and the rest). A spell lands, centred on the middle of that cell. Casts are numbered in the order they land,
        /// which is their order in the sent list. Its first SpellPulse follows on the same tick.
        /// </summary>
        SpellCast = 20,
        /// <summary>
        /// a = cast number, b = which application this is (1 for the first), c = how many it has in all, d = radius in
        /// battle units. One per application, freeze and boost included; the entries for each target it changed follow.
        /// </summary>
        SpellPulse = 21,
        /// <summary>a = cast number, b = building id, c = damage, d = building HP left. BuildingDown follows if it fell.</summary>
        SpellHit = 22,
        /// <summary>a = cast number, b = defender, c = damage, d = defender HP left. DefenderDown follows if it fell.</summary>
        SpellHitDefender = 23,
        /// <summary>a = cast number, b = unit healed, c = amount, d = unit HP now.</summary>
        SpellHeal = 24,
        /// <summary>
        /// a = cast number, b = 0 for a tower or 1 for a defender, c = building id or defender, d = ticks it cannot act,
        /// this tick included.
        /// </summary>
        SpellFreeze = 25,
        /// <summary>a = cast number, b = unit, c = ticks the boost lasts, this tick included, d = percent more damage.</summary>
        SpellBoost = 26
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

        /// <summary>What a battle spell does (ADR-052), as the number the log uses for it.</summary>
        public const int EffectDamage = 0;
        public const int EffectHeal = 1;
        public const int EffectFreeze = 2;
        public const int EffectBoost = 3;

        public static int EffectNumber(string effect)
        {
            switch (effect)
            {
                case "damage": return EffectDamage;
                case "heal": return EffectHeal;
                case "freeze": return EffectFreeze;
                case "boost": return EffectBoost;
                default: throw new FormatException("Unknown spell effect " + effect);
            }
        }

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
            /// <summary>Holds the unit in place after a Hero Trap: it does nothing until it runs out.</summary>
            public int StunTimer;
            /// <summary>A boost spell (ADR-052): the unit deals BoostPercent more damage on every tick before BoostUntil.</summary>
            public int BoostUntil;
            public long BoostPercent;
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
            /// <summary>Holds the defender in place under a freeze spell (ADR-052): it does nothing until it runs out.</summary>
            public int StunTimer;
        }

        /// <summary>A spell that has landed (ADR-052).</summary>
        private sealed class Spell
        {
            public int Index;
            public BattleSpellInput Input;
            public int Effect;
            public int Cx;
            public int Cz;
            public int PulsesLeft;
            public int NextPulse;
        }

        private sealed class Structure
        {
            public BattleBuildingInput Input;
            public long Hp;
            public bool Alive = true;
            public int ShotTimer;
            public int StunTimer;
        }

        private readonly List<Unit> _units = new List<Unit>();
        /// <summary>Each landed unit's input, by unit number.</summary>
        private readonly List<BattleHeroInput> _landed = new List<BattleHeroInput>();
        /// <summary>Cards dropped but not landed yet, in landing order.</summary>
        private readonly List<BattleHeroInput> _pending = new List<BattleHeroInput>();
        /// <summary>Spells cast but not landed yet, in the order cast (ADR-052).</summary>
        private readonly List<BattleSpellInput> _pendingCasts = new List<BattleSpellInput>();
        /// <summary>Landed spells, by cast number.</summary>
        private readonly List<Spell> _spells = new List<Spell>();
        /// <summary>A timed battle (ADR-050) ends at its end tick or when every building is down, never because the field is empty.</summary>
        private readonly bool _timed;
        private readonly int _endTick;
        private bool _done;
        private bool _resolved;
        private readonly Defender[] _defenders;
        private readonly Structure[] _structures;
        private readonly Structure[] _traps;
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

        private BattleSim(IEnumerable<BattleBuildingInput> buildings, IEnumerable<BattleDefenderInput> defenders, int endTick)
        {
            _timed = endTick > 0;
            _endTick = Math.Min(endTick, TickLimit);
            var all = buildings.ToList();
            _structures = all.Where(b => !b.isTrap).Select(b => new Structure { Input = b, Hp = b.hp }).ToArray();
            _traps = all.Where(b => b.isTrap).Select(b => new Structure { Input = b, Hp = b.hp }).ToArray();
            for (var i = 0; i < _structures.Length; i++)
            {
                var b = _structures[i].Input;
                if (!b.isWall) _totalHp += b.hp;
                for (var z = b.z; z < b.z + b.depth; z++)
                for (var x = b.x; x < b.x + b.width; x++)
                    _grid[z * GridSize + x] = i + 1;
            }
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

        /// <summary>The whole battle at once, everyone landed before the first tick, until one side is gone or time is up.</summary>
        public static BattleResult Run(IEnumerable<BattleBuildingInput> buildings, IEnumerable<BattleHeroInput> heroes, IEnumerable<BattleDefenderInput> defenders = null) =>
            Run(buildings, heroes, defenders, 0);

        /// <summary>
        /// The whole battle at once. With an end tick above 0 it is a timed battle (ADR-050): each unit lands at its
        /// deployTick, in LandingOrder, and the battle stops at the end tick (when the attacker ended it) or when
        /// every building is down. This is how the server re-fights a battle the player fought live. Spells (ADR-052)
        /// land at their tick, in the order listed; only a timed battle may have any.
        /// </summary>
        public static BattleResult Run(IEnumerable<BattleBuildingInput> buildings, IEnumerable<BattleHeroInput> heroes, IEnumerable<BattleDefenderInput> defenders, int endTick, IEnumerable<BattleSpellInput> casts = null)
        {
            var sim = new BattleSim(buildings, defenders, endTick);
            if (sim._timed) sim._pending.AddRange(LandingOrder(heroes));
            else
                foreach (var hero in heroes) sim.Land(hero);
            if (casts != null)
            {
                sim._pendingCasts.AddRange(casts.Select((c, i) => (c, i)).OrderBy(p => p.c.tick).ThenBy(p => p.i).Select(p => p.c));
                if (sim._pendingCasts.Count > 0 && !sim._timed) throw new FormatException("Only a timed battle can have spells.");
                foreach (var cast in sim._pendingCasts) CheckSpell(cast);
            }
            while (sim.Step()) { }
            return sim.Finish();
        }

        /// <summary>
        /// A live timed battle (ADR-050): the field starts empty and the clock is at tick 0. Drop cards with Deploy,
        /// advance with Step, and read Events as they come. Finish ends it at the current tick. Run with the same
        /// drops and that end tick gives the same result, which is how the server checks it.
        /// </summary>
        public static BattleSim Start(IEnumerable<BattleBuildingInput> buildings, IEnumerable<BattleDefenderInput> defenders = null) =>
            new BattleSim(buildings, defenders, TickLimit);

        /// <summary>The last tick played; 0 before the first.</summary>
        public int Tick => _tick;
        /// <summary>True once every building is down, time is up, or Finish was called.</summary>
        public bool Done => _done;
        public IReadOnlyList<long[]> Events => _result.events;
        /// <summary>Landed units still standing.</summary>
        public int UnitsStanding => _units.Count(u => u.Hp > 0);
        /// <summary>Dropped cards that land on the next Step.</summary>
        public int UnitsPending => _pending.Count;
        /// <summary>Spells cast that land on the next Step (ADR-052).</summary>
        public int SpellsPending => _pendingCasts.Count;
        /// <summary>Spells on the field that still have a pulse to come.</summary>
        public int SpellsActive => _spells.Count(s => s.PulsesLeft > 0);
        /// <summary>The input of a cast, landed or not, by the cast number the log uses for it.</summary>
        public BattleSpellInput CastInput(int index) => index < _spells.Count ? _spells[index].Input : _pendingCasts[index - _spells.Count];
        public int DamagePercent => _totalHp == 0 ? 100 : (int)(_dealtHp * 100 / _totalHp);
        /// <summary>The input of a landed unit, by the number the log uses for it.</summary>
        public BattleHeroInput LandedInput(int index) => _landed[index];

        /// <summary>
        /// Drops a card now: it lands after the current tick, so it acts from the next one. Sets its deployTick.
        /// Check DeployError first; this does not.
        /// </summary>
        public void Deploy(BattleHeroInput unit)
        {
            if (!_timed || _done) throw new InvalidOperationException("Cards can only be dropped into a running live battle.");
            unit.deployTick = _tick;
            // The same order LandingOrder gives a whole list: by tick, heroes before troops, then as dropped.
            var at = _pending.Count;
            while (at > 0 && LandsAfter(_pending[at - 1], unit)) at--;
            _pending.Insert(at, unit);
        }

        /// <summary>
        /// Casts a spell now (ADR-052): it lands after the current tick, so it first applies on the next one. Sets its
        /// tick and returns its cast number, the one the log uses. Check SpellCatalog.CastError first; this does not.
        /// </summary>
        public int CastSpell(BattleSpellInput spell)
        {
            if (!_timed || _done) throw new InvalidOperationException("Spells can only be cast in a running live battle.");
            CheckSpell(spell);
            spell.tick = _tick;
            _pendingCasts.Add(spell);
            return _spells.Count + _pendingCasts.Count - 1;
        }

        /// <summary>Throws when a spell's numbers could not have come from a valid spells.json.</summary>
        private static void CheckSpell(BattleSpellInput spell)
        {
            EffectNumber(spell.effect);
            if (spell.radius < 0 || spell.pulses < 1 || spell.pulseTicks < 1 || spell.power < 0 || spell.ticks < 0)
                throw new FormatException("Spell " + spell.id + " has invalid values.");
        }

        private static bool LandsAfter(BattleHeroInput a, BattleHeroInput b) =>
            a.deployTick > b.deployTick || (a.deployTick == b.deployTick && a.isTroop && !b.isTroop);

        /// <summary>
        /// The order a timed battle numbers its units: by deployTick, heroes before troops on the same tick, otherwise
        /// as listed. The server lists heroes then troops, so this is the same on both sides however the cards fell.
        /// </summary>
        public static List<BattleHeroInput> LandingOrder(IEnumerable<BattleHeroInput> heroes) =>
            heroes.Select((h, i) => (h, i)).OrderBy(p => p.h.deployTick).ThenBy(p => p.h.isTroop ? 1 : 0).ThenBy(p => p.i).Select(p => p.h).ToList();

        /// <summary>
        /// Empty when the placement is legal, otherwise the reason. The server uses the same names. With an end tick
        /// (a timed battle, ADR-050) every deployTick must be from 0 to before the end tick, rising among heroes and
        /// among troops; two heroes may share a cell only on different ticks. Without one, every deployTick is 0.
        /// </summary>
        public static string DeployError(IList<BattleBuildingInput> buildings, IList<BattleHeroInput> heroes, int endTick = 0)
        {
            if (heroes.Count < 1) return "NO_TEAM";
            if (heroes.Count(h => !h.isTroop) > 6) return "TOO_MANY_HEROES";
            if (endTick < 0 || endTick > TickLimit) return "BAD_DEPLOY_TICK";
            var lastHero = 0;
            var lastTroop = 0;
            for (var i = 0; i < heroes.Count; i++)
            {
                var h = heroes[i];
                if (h.x < 0 || h.x >= GridSize || h.z < 0 || h.z >= GridSize) return "BAD_DEPLOY_CELL";
                if (!CanDeployAt(buildings, h.x, h.z)) return "DEPLOY_TOO_CLOSE";
                if (endTick == 0 ? h.deployTick != 0 : h.deployTick < 0 || h.deployTick >= endTick) return "BAD_DEPLOY_TICK";
                if (h.deployTick < (h.isTroop ? lastTroop : lastHero)) return "DEPLOY_TICK_ORDER";
                if (h.isTroop)
                {
                    lastTroop = h.deployTick;
                    continue;
                }
                lastHero = h.deployTick;
                for (var j = 0; j < i; j++)
                {
                    if (heroes[j].isTroop) continue;
                    if (heroes[j].id == h.id) return "DUPLICATE_HERO";
                    if (heroes[j].x == h.x && heroes[j].z == h.z && heroes[j].deployTick == h.deployTick) return "DEPLOY_CELL_TAKEN";
                }
            }
            return "";
        }

        public static bool CanDeployAt(IEnumerable<BattleBuildingInput> buildings, int x, int z)
        {
            foreach (var b in buildings)
            {
                // A trap must not give itself away, so landing beside one is allowed.
                if (b.isTrap) continue;
                if (x >= b.x - DeployClearance && x < b.x + b.width + DeployClearance && z >= b.z - DeployClearance && z < b.z + b.depth + DeployClearance)
                    return false;
            }
            return true;
        }

        private void Land(BattleHeroInput h)
        {
            _units.Add(new Unit
            {
                Index = _units.Count,
                Role = RoleNumber(h.role),
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
            });
            _landed.Add(h);
        }

        /// <summary>Plays one tick. False once the battle is over (then Finish gives the result).</summary>
        public bool Step()
        {
            if (_done) return false;
            _tick++;
            // Cards dropped after the last tick land now, before anyone moves.
            while (_pending.Count > 0 && _pending[0].deployTick < _tick)
            {
                var input = _pending[0];
                _pending.RemoveAt(0);
                Land(input);
                Log(BattleEventKind.Deploy, _units.Count - 1, input.x, input.z);
            }
            // Spells cast after the last tick land now, after the cards, and every spell due applies before anyone moves.
            while (_pendingCasts.Count > 0 && _pendingCasts[0].tick < _tick)
            {
                var input = _pendingCasts[0];
                _pendingCasts.RemoveAt(0);
                LandSpell(input);
            }
            foreach (var spell in _spells)
                if (spell.PulsesLeft > 0 && spell.NextPulse <= _tick) Pulse(spell);
            foreach (var unit in _units)
                if (unit.Hp > 0) StepUnit(unit);
            foreach (var trap in _traps)
                if (trap.Alive) CheckTrap(trap);
            foreach (var defender in _defenders)
                if (defender.Hp > 0) StepDefender(defender);
            foreach (var structure in _structures)
                if (structure.Alive && structure.Input.isDefense) StepTower(structure);
            if (Finished() || _tick >= (_timed ? _endTick : TickLimit)) _done = true;
            return !_done;
        }

        /// <summary>Ends the battle at the current tick, if it has not ended already, and scores it.</summary>
        public BattleResult Finish()
        {
            if (_resolved) return _result;
            _resolved = true;
            _done = true;
            _result.endTick = _tick;
            _result.damagePercent = _totalHp == 0 ? 100 : (int)(_dealtHp * 100 / _totalHp);
            _result.heroesAlive = _units.Count(u => u.Hp > 0);
            _result.defendersAlive = _defenders.Count(d => d.Hp > 0);
            foreach (var structure in _structures)
                if (structure.Input.definitionId == "dojo_hq" && !structure.Alive) _result.dojoDestroyed = true;
            if (_result.damagePercent >= 50) _result.stars++;
            if (_result.dojoDestroyed) _result.stars++;
            if (_result.damagePercent >= 100) _result.stars++;
            _result.checksum = Checksum(_result.events);
            return _result;
        }

        private bool Finished()
        {
            // A timed battle goes on with an empty field: more cards may still fall until the attacker ends it.
            if (!_timed)
            {
                var anyHero = false;
                foreach (var unit in _units) if (unit.Hp > 0) anyHero = true;
                if (!anyHero) return true;
            }
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

        // ---------- Spells (ADR-052) ----------

        private void LandSpell(BattleSpellInput input)
        {
            var spell = new Spell
            {
                Index = _spells.Count,
                Input = input,
                Effect = EffectNumber(input.effect),
                Cx = input.x * CellUnits + CellUnits / 2,
                Cz = input.z * CellUnits + CellUnits / 2,
                PulsesLeft = input.pulses,
                NextPulse = _tick
            };
            _spells.Add(spell);
            Log(BattleEventKind.SpellCast, spell.Index, input.x, input.z, spell.Effect);
        }

        /// <summary>One application of a spell to everything in its ring right now.</summary>
        private void Pulse(Spell spell)
        {
            var input = spell.Input;
            spell.PulsesLeft--;
            spell.NextPulse = _tick + input.pulseTicks;
            Log(BattleEventKind.SpellPulse, spell.Index, input.pulses - spell.PulsesLeft, input.pulses, input.radius);
            var reach = (long)input.radius * input.radius;
            switch (spell.Effect)
            {
                case EffectDamage:
                    // Buildings take it in full, walls included; hidden traps are not touched. Guards take it less their defense.
                    for (var i = 0; i < _structures.Length; i++)
                    {
                        var structure = _structures[i];
                        if (!structure.Alive || SquaredDistance(spell.Cx, spell.Cz, structure.Input) > reach) continue;
                        var amount = Math.Min(Math.Max(input.power, 1), structure.Hp);
                        structure.Hp -= amount;
                        if (!structure.Input.isWall) _dealtHp += amount;
                        Log(BattleEventKind.SpellHit, spell.Index, structure.Input.id, amount, structure.Hp);
                        if (structure.Hp == 0) Collapse(i);
                    }
                    foreach (var defender in _defenders)
                    {
                        if (defender.Hp <= 0 || SquaredPoints(defender.Px, defender.Pz, spell.Cx, spell.Cz) > reach) continue;
                        var amount = input.power * 100 / (100 + defender.Defense);
                        if (defender.GuardTimer > 0) amount /= 2;
                        amount = Math.Min(Math.Max(amount, 1), defender.Hp);
                        defender.Hp -= amount;
                        Log(BattleEventKind.SpellHitDefender, spell.Index, defender.Index, amount, defender.Hp);
                        if (defender.Hp == 0) Log(BattleEventKind.DefenderDown, defender.Index);
                    }
                    break;
                case EffectHeal:
                    foreach (var unit in _units)
                    {
                        if (unit.Hp <= 0 || unit.Hp >= unit.MaxHp || SquaredPoints(unit.Px, unit.Pz, spell.Cx, spell.Cz) > reach) continue;
                        var amount = Math.Min(input.power, unit.MaxHp - unit.Hp);
                        if (amount < 1) continue;
                        unit.Hp += amount;
                        Log(BattleEventKind.SpellHeal, spell.Index, unit.Index, amount, unit.Hp);
                    }
                    break;
                case EffectFreeze:
                    // Towers and guards step after spells, so a freeze of N ticks stops them on this tick and the N - 1 after.
                    foreach (var structure in _structures)
                    {
                        if (!structure.Alive || !structure.Input.isDefense || SquaredDistance(spell.Cx, spell.Cz, structure.Input) > reach) continue;
                        structure.StunTimer = Math.Max(structure.StunTimer, input.ticks);
                        Log(BattleEventKind.SpellFreeze, spell.Index, 0, structure.Input.id, structure.StunTimer);
                    }
                    foreach (var defender in _defenders)
                    {
                        if (defender.Hp <= 0 || SquaredPoints(defender.Px, defender.Pz, spell.Cx, spell.Cz) > reach) continue;
                        defender.StunTimer = Math.Max(defender.StunTimer, input.ticks);
                        Log(BattleEventKind.SpellFreeze, spell.Index, 1, defender.Index, defender.StunTimer);
                    }
                    break;
                case EffectBoost:
                    foreach (var unit in _units)
                    {
                        if (unit.Hp <= 0 || SquaredPoints(unit.Px, unit.Pz, spell.Cx, spell.Cz) > reach) continue;
                        unit.BoostUntil = _tick + input.ticks;
                        unit.BoostPercent = input.power;
                        Log(BattleEventKind.SpellBoost, spell.Index, unit.Index, input.ticks, input.power);
                    }
                    break;
            }
        }

        /// <summary>A unit's attack right now: more while a boost spell is on it.</summary>
        private long AttackOf(Unit unit) =>
            _tick < unit.BoostUntil ? unit.Attack * (100 + unit.BoostPercent) / 100 : unit.Attack;

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
            if (unit.StunTimer > 0)
            {
                unit.StunTimer--;
                return;
            }
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
            var base_ = AttackOf(unit) * DamagePercents[unit.Role] / 100;
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
            if (structure.Hp == 0) Collapse(index);
        }

        /// <summary>A building at 0 HP falls: its cells open up, and anyone heading for it plans again.</summary>
        private void Collapse(int index)
        {
            var structure = _structures[index];
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
            var amount = AttackOf(unit) * percent / 100 * 100 / (100 + defender.Defense);
            if (defender.GuardTimer > 0) amount /= 2;
            amount = Math.Min(Math.Max(amount, 1), defender.Hp);
            defender.Hp -= amount;
            Log(BattleEventKind.DefenderDamaged, unit.Index, defender.Index, amount, defender.Hp);
            if (defender.Hp == 0) Log(BattleEventKind.DefenderDown, defender.Index);
        }

        private void StepDefender(Defender defender)
        {
            if (defender.StunTimer > 0)
            {
                defender.StunTimer--;
                return;
            }
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

        // ---------- Traps (ADR-046) ----------

        private const int TrapTrigger = 120;
        private const int HeroTrapStun = 30;

        private void TrapHit(Structure trap, Unit unit)
        {
            var amount = trap.Input.attack * 100 / (100 + unit.Defense);
            if (amount < 1) amount = 1;
            if (amount > unit.Hp) amount = unit.Hp;
            unit.Hp -= amount;
            Log(BattleEventKind.TrapHit, trap.Input.id, unit.Index, amount, unit.Hp);
            if (unit.Hp == 0) Log(BattleEventKind.HeroDown, unit.Index);
        }

        /// <summary>Fires a trap once something it catches comes close: troops for a Bomb Trap, a hero for a Hero Trap.</summary>
        private void CheckTrap(Structure trap)
        {
            var b = trap.Input;
            var cx = b.x * CellUnits + b.width * CellUnits / 2;
            var cz = b.z * CellUnits + b.depth * CellUnits / 2;
            var heroTrap = b.definitionId == "hero_trap";
            Unit caught = null;
            foreach (var unit in _units)
            {
                if (unit.Hp <= 0 || unit.IsTroop == heroTrap) continue;
                if (SquaredPoints(unit.Px, unit.Pz, cx, cz) <= (long)TrapTrigger * TrapTrigger)
                {
                    caught = unit;
                    break;
                }
            }
            if (caught == null) return;
            trap.Alive = false;
            if (heroTrap)
            {
                Log(BattleEventKind.TrapFired, b.id, 1);
                TrapHit(trap, caught);
                if (caught.Hp > 0) caught.StunTimer = HeroTrapStun;
                return;
            }
            Log(BattleEventKind.TrapFired, b.id, 0);
            var reach = (long)b.range * CellUnits;
            foreach (var unit in _units)
            {
                if (unit.Hp <= 0 || !unit.IsTroop) continue;
                if (SquaredPoints(unit.Px, unit.Pz, cx, cz) <= reach * reach) TrapHit(trap, unit);
            }
        }

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
            // Each tower fires its own way (owner, 2026-11-09: Arrow, Cannon and Magic are one switchable building):
            // the Arrow Tower shoots fast, single shots; the Cannon Tower fires slow, heavy balls whose blast hits everyone close by in full;
            // the Magic Tower's bolts splash wider for less. Mirrors the server's stepTower.
            var cannon = input.definitionId == "cannon_tower";
            var splashing = cannon || input.definitionId == "spirit_turret";
            var blast = cannon ? 125L : 150L;
            var blastPercent = cannon ? 100L : 40L;
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

            tower.ShotTimer = cannon ? 25 : splashing ? 15 : 10;
            Shoot(tower, chosen, 100);
            if (!splashing) return;
            foreach (var unit in _units)
            {
                if (unit == chosen || unit.Hp <= 0) continue;
                var dx = (long)(unit.Px - chosen.Px);
                var dz = (long)(unit.Pz - chosen.Pz);
                if (dx * dx + dz * dz <= blast * blast) Shoot(tower, unit, blastPercent);
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

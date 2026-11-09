using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ShinobiDominion.Core
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum BuildingCategory
    {
        Structures,
        Defense,
        Resources,
        HeroBases,
        Walls,
        Traps
    }

    /// <summary>Which panel and actions a building offers when it is tapped.</summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum BuildingFunction
    {
        Headquarters,
        BuilderHall,
        HeroAltar,
        HeroBase,
        SummoningGate,
        Training,
        TroopCamp,
        Research,
        Collector,
        Storage,
        Defense,
        Wall,
        Guild,
        Arena,
        Trap
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ResourceKind
    {
        None,
        Gold,
        Crystals,
        Mana
    }

    /// <summary>
    /// Data-driven building rules, loaded from Resources/Config/buildings.json.
    /// Every number in that file is a proposed test value, not an approved balance value.
    /// </summary>
    /// <summary>One row of a building's level table. Fields that don't apply to a building stay 0.</summary>
    [Serializable]
    public sealed class BuildingLevel
    {
        public long hp;
        public long upgradeCost;
        public int requiredHq = 1;
        public long production;
        public long storedCap;
        public long goldCapacity;
        public long manaCapacity;
        public long attack;
        public int range;
        public int bonusPercent;
        public long troopCapacity;
        public int might;
    }

    [Serializable]
    public sealed class BuildingDefinition
    {
        public string id;
        public string displayName;
        public string description;
        public BuildingCategory category;
        public BuildingFunction function;
        public int width = 1;
        public int depth = 1;
        public bool rotatable;
        public bool removable = true;
        public long costGold;
        public long costCrystals;
        public int[] maxCountByHqLevel = Array.Empty<int>();
        /// <summary>
        /// Buildings that are one building to the player: the Arrow, Cannon and Magic Towers (owner, 2026-11-09).
        /// They share one count limit, and a placed one can switch between them. Empty for everything else.
        /// </summary>
        public string group = "";
        /// <summary>Reached only by switching another building of its group; never in the build catalog.</summary>
        public bool switchOnly;
        public ResourceKind produces = ResourceKind.None;
        public List<BuildingLevel> levels = new List<BuildingLevel>();

        /// <summary>Every building has this many levels, set by its table.</summary>
        public int maxLevel => levels.Count;

        public bool IsWall => function == BuildingFunction.Wall;
        public bool IsTrap => function == BuildingFunction.Trap;

        /// <summary>The limit for an HQ level. Levels past the table use its last entry.</summary>
        public int MaxCountAt(int hqLevel)
        {
            if (maxCountByHqLevel == null || maxCountByHqLevel.Length == 0) return 0;
            var index = Math.Max(0, Math.Min(hqLevel - 1, maxCountByHqLevel.Length - 1));
            return maxCountByHqLevel[index];
        }

        public BuildingLevel At(int level) => levels[Math.Max(1, Math.Min(level, levels.Count)) - 1];

        /// <summary>The cost to go from this level to the next.</summary>
        public long UpgradeCost(int currentLevel) => At(currentLevel).upgradeCost;
        public long HpAtLevel(int level) => At(level).hp;
        public long ProductionAt(int level) => At(level).production;
        public long StoredCapAt(int level) => At(level).storedCap;
    }

    [Serializable]
    public sealed class BuildingCatalogFile
    {
        public int schemaVersion;
        public string balanceStatus;
        public long startingGold;
        public long startingCrystals;
        public long startingMana;
        public long baseGoldCapacity;
        public long baseManaCapacity;
        public long[] upgradeSeconds;
        public List<BuildingDefinition> buildings = new List<BuildingDefinition>();
    }

    public sealed class BuildingCatalog
    {
        public const int SupportedSchemaVersion = 2;
        public const int LevelsPerBuilding = 15;

        private readonly Dictionary<string, BuildingDefinition> _byId;

        public IReadOnlyList<BuildingDefinition> All { get; }
        public long StartingGold { get; }
        public long StartingCrystals { get; }
        public long StartingMana { get; }
        public long BaseGoldCapacity { get; }
        public long BaseManaCapacity { get; }
        private readonly long[] _upgradeSeconds;
        public string BalanceStatus { get; }

        private BuildingCatalog(BuildingCatalogFile file)
        {
            All = file.buildings;
            StartingGold = file.startingGold;
            StartingCrystals = file.startingCrystals;
            StartingMana = file.startingMana;
            BaseGoldCapacity = file.baseGoldCapacity;
            BaseManaCapacity = file.baseManaCapacity;
            _upgradeSeconds = file.upgradeSeconds ?? new long[0];
            BalanceStatus = file.balanceStatus;
            _byId = file.buildings.ToDictionary(definition => definition.id);
        }

        /// <summary>How long upgrading to a level takes, in seconds. Walls are always instant (ADR-040).</summary>
        public long UpgradeSeconds(BuildingDefinition definition, int level) =>
            definition.IsWall || level < 0 || level >= _upgradeSeconds.Length ? 0 : _upgradeSeconds[level];

        public static BuildingCatalog FromJson(string json)
        {
            var file = JsonConvert.DeserializeObject<BuildingCatalogFile>(json);
            if (file == null) throw new FormatException("Building catalog is empty.");
            if (file.schemaVersion != SupportedSchemaVersion)
                throw new FormatException($"Unsupported catalog schema {file.schemaVersion}.");

            var seen = new HashSet<string>();
            foreach (var definition in file.buildings)
            {
                if (string.IsNullOrEmpty(definition.id))
                    throw new FormatException("A building definition has no id.");
                if (!seen.Add(definition.id))
                    throw new FormatException($"Duplicate building id '{definition.id}'.");
                if (definition.width <= 0 || definition.depth <= 0)
                    throw new FormatException($"'{definition.id}' has an invalid footprint.");
                if (definition.costGold < 0 || definition.costCrystals < 0)
                    throw new FormatException($"'{definition.id}' has a negative cost.");
                if (definition.levels.Count != LevelsPerBuilding)
                    throw new FormatException($"'{definition.id}' needs exactly {LevelsPerBuilding} levels.");
                for (var i = 0; i < definition.levels.Count; i++)
                {
                    var row = definition.levels[i];
                    if (row.upgradeCost < 0 || row.hp <= 0 || row.requiredHq < 1)
                        throw new FormatException($"'{definition.id}' level {i + 1} has invalid values.");
                    if (i > 0 && row.hp < definition.levels[i - 1].hp)
                        throw new FormatException($"'{definition.id}' loses HP at level {i + 1}.");
                }
            }
            return new BuildingCatalog(file);
        }

        public bool TryGet(string id, out BuildingDefinition definition)
        {
            if (id == null)
            {
                definition = null;
                return false;
            }
            return _byId.TryGetValue(id, out definition);
        }

        public BuildingDefinition Get(string id) =>
            TryGet(id, out var definition) ? definition : throw new KeyNotFoundException($"Unknown building '{id}'.");

        public IEnumerable<BuildingDefinition> InCategory(BuildingCategory category) =>
            All.Where(definition => definition.category == category);
    }

    /// <summary>A placed building or wall segment.</summary>
    public sealed class BuildingInstance
    {
        public int Id;
        public BuildingDefinition Definition;
        public GridCoord Origin;
        public Rotation Rotation;
        public int Level = 1;
        public string AssignedHeroInstanceId;
        public long LastCollectedUtcTicks;
        /// <summary>When a running upgrade finishes; 0 when none is running (ADR-040).</summary>
        public long UpgradeDoneUtcTicks;
        public bool Upgrading => UpgradeDoneUtcTicks > 0;

        public Footprint Footprint => Footprint.Of(Definition, Origin, Rotation);
        public long MaxHp => Definition.HpAtLevel(Level);
    }
}

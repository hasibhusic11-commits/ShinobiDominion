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
        Walls
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
        Research,
        Collector,
        Storage,
        Defense,
        Wall
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ResourceKind
    {
        None,
        Gold,
        Crystals
    }

    /// <summary>
    /// Data-driven building rules, loaded from Resources/Config/buildings.json.
    /// Every number in that file is a proposed test value, not an approved balance value.
    /// </summary>
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
        public int maxHp = 100;
        public int maxLevel = 1;
        public long costGold;
        public long costCrystals;
        public long upgradeCostGoldPerLevel;
        public int[] maxCountByHqLevel = Array.Empty<int>();
        public ResourceKind produces = ResourceKind.None;
        public long productionPerHour;
        public long storedProductionCap;
        public long goldCapacityBonus;

        public bool IsWall => function == BuildingFunction.Wall;

        /// <summary>The limit for an HQ level. Levels past the table use its last entry.</summary>
        public int MaxCountAt(int hqLevel)
        {
            if (maxCountByHqLevel == null || maxCountByHqLevel.Length == 0) return 0;
            var index = Math.Max(0, Math.Min(hqLevel - 1, maxCountByHqLevel.Length - 1));
            return maxCountByHqLevel[index];
        }

        public long UpgradeCost(int currentLevel) => upgradeCostGoldPerLevel * currentLevel;

        /// <summary>Hit points grow linearly: level 1 has maxHp, each further level adds half of it.</summary>
        public int HpAtLevel(int level) => maxHp + maxHp * (level - 1) / 2;
    }

    [Serializable]
    public sealed class BuildingCatalogFile
    {
        public int schemaVersion;
        public string balanceStatus;
        public long startingGold;
        public long startingCrystals;
        public long baseGoldCapacity;
        public List<BuildingDefinition> buildings = new List<BuildingDefinition>();
    }

    public sealed class BuildingCatalog
    {
        public const int SupportedSchemaVersion = 1;

        private readonly Dictionary<string, BuildingDefinition> _byId;

        public IReadOnlyList<BuildingDefinition> All { get; }
        public long StartingGold { get; }
        public long StartingCrystals { get; }
        public long BaseGoldCapacity { get; }
        public string BalanceStatus { get; }

        private BuildingCatalog(BuildingCatalogFile file)
        {
            All = file.buildings;
            StartingGold = file.startingGold;
            StartingCrystals = file.startingCrystals;
            BaseGoldCapacity = file.baseGoldCapacity;
            BalanceStatus = file.balanceStatus;
            _byId = file.buildings.ToDictionary(definition => definition.id);
        }

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
                if (definition.costGold < 0 || definition.costCrystals < 0 || definition.upgradeCostGoldPerLevel < 0)
                    throw new FormatException($"'{definition.id}' has a negative cost.");
                if (definition.maxLevel < 1)
                    throw new FormatException($"'{definition.id}' has an invalid max level.");
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

        public Footprint Footprint => Footprint.Of(Definition, Origin, Rotation);
        public int MaxHp => Definition.HpAtLevel(Level);
    }
}

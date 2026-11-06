using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace ShinobiDominion.Core
{
    /// <summary>A kind of troop. Stronger troops take up more room in the camps.</summary>
    public sealed class TroopDefinition
    {
        public string id;
        public string displayName;
        public string description;
        public int space;
        public long goldCost;
        public int requiredHall = 1;
        public string role;
        public long hp;
        public long attack;
        public long defense;
    }

    public sealed class TroopCatalog
    {
        public const int SupportedSchemaVersion = 1;

        private sealed class Document
        {
            public int schemaVersion;
            public List<TroopDefinition> troops = new List<TroopDefinition>();
        }

        public IReadOnlyList<TroopDefinition> All { get; private set; }

        public TroopDefinition Find(string id) => All.FirstOrDefault(t => t.id == id);

        public static TroopCatalog FromJson(string json)
        {
            var document = JsonConvert.DeserializeObject<Document>(json);
            if (document == null || document.schemaVersion != SupportedSchemaVersion)
                throw new FormatException("Unsupported troop file.");
            foreach (var troop in document.troops)
            {
                if (troop.space < 1 || troop.goldCost < 0 || troop.hp < 1 || troop.requiredHall < 1)
                    throw new FormatException($"Troop {troop.id} has invalid values.");
                BattleSim.RoleNumber(troop.role);
            }
            return new TroopCatalog { All = document.troops };
        }
    }

    /// <summary>The troops a player has trained, and the rules for training more.</summary>
    public sealed class Army
    {
        public const int MaxTrainAtOnce = 100;

        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();

        public IReadOnlyDictionary<string, int> Counts => _counts;

        public int CountOf(string troopId) => _counts.TryGetValue(troopId, out var count) ? count : 0;

        public void Replace(IEnumerable<KeyValuePair<string, int>> counts)
        {
            _counts.Clear();
            foreach (var pair in counts)
                if (pair.Value > 0) _counts[pair.Key] = pair.Value;
        }

        public void Remove(string troopId, int count)
        {
            var left = CountOf(troopId) - count;
            if (left > 0) _counts[troopId] = left;
            else _counts.Remove(troopId);
        }

        public int UsedSpace(TroopCatalog catalog) =>
            _counts.Sum(pair => (catalog.Find(pair.Key)?.space ?? 0) * pair.Value);

        public static int Capacity(Village village) =>
            (int)village.Buildings.Where(b => b.Definition.function == BuildingFunction.TroopCamp).Sum(b => b.Definition.At(b.Level).troopCapacity);

        public static int HallLevel(Village village) =>
            village.Buildings.Where(b => b.Definition.function == BuildingFunction.Training).Select(b => b.Level).DefaultIfEmpty(0).Max();

        public VillageError CheckTrain(Village village, TroopCatalog catalog, string troopId, int count)
        {
            var troop = catalog.Find(troopId);
            if (troop == null || count < 1 || count > MaxTrainAtOnce) return VillageError.BadTroop;
            var hall = HallLevel(village);
            if (hall == 0) return VillageError.NoTrainingHall;
            if (hall < troop.requiredHall) return VillageError.TroopLocked;
            if (UsedSpace(catalog) + troop.space * count > Capacity(village)) return VillageError.CampsFull;
            if (!village.CanAfford(troop.goldCost * count, 0)) return VillageError.InsufficientFunds;
            return VillageError.None;
        }

        public VillageError TryTrain(Village village, TroopCatalog catalog, string troopId, int count)
        {
            var error = CheckTrain(village, catalog, troopId, count);
            if (error != VillageError.None) return error;
            village.PayGold(catalog.Find(troopId).goldCost * count);
            _counts[troopId] = CountOf(troopId) + count;
            return VillageError.None;
        }
    }

    /// <summary>A group of the same troop, landing together on one cell.</summary>
    public sealed class RaidTroopStack
    {
        public string troopId;
        public int x;
        public int z;
        public int count;
    }
}

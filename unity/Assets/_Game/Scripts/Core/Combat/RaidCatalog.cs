using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace ShinobiDominion.Core
{
    public sealed class RaidBuildingEntry
    {
        public string def;
        public int level = 1;
        public int x;
        public int z;
    }

    public sealed class RaidWallRun
    {
        public int level = 1;
        public int x1;
        public int z1;
        public int x2;
        public int z2;
    }

    public sealed class RaidDefinition
    {
        public string id;
        public string displayName;
        public string description;
        public int version = 1;
        public long goldPerStar;
        public List<RaidBuildingEntry> buildings = new List<RaidBuildingEntry>();
        public List<RaidWallRun> walls = new List<RaidWallRun>();

        /// <summary>Every building in battle order: buildings first, then walls, numbered from 1.</summary>
        public List<BattleBuildingInput> Expand(BuildingCatalog catalog)
        {
            var result = new List<BattleBuildingInput>();
            void Add(string definitionId, int level, int x, int z)
            {
                var definition = catalog.Get(definitionId);
                var row = definition.At(level);
                result.Add(new BattleBuildingInput
                {
                    id = result.Count + 1,
                    definitionId = definitionId,
                    x = x,
                    z = z,
                    width = definition.width,
                    depth = definition.depth,
                    level = level,
                    hp = row.hp,
                    attack = row.attack,
                    range = row.range,
                    isWall = definition.IsWall,
                    isDefense = definition.function == BuildingFunction.Defense
                });
            }

            foreach (var entry in buildings) Add(entry.def, entry.level, entry.x, entry.z);
            var taken = new HashSet<int>();
            foreach (var building in result)
                for (var z = building.z; z < building.z + building.depth; z++)
                for (var x = building.x; x < building.x + building.width; x++)
                    if (!VillageGrid.InBounds(x, z) || !taken.Add(z * VillageGrid.Size + x))
                        throw new FormatException($"Raid {id}: {building.definitionId} overlaps or leaves the map.");

            foreach (var run in walls)
            {
                if (run.x1 != run.x2 && run.z1 != run.z2) throw new FormatException($"Raid {id}: diagonal wall.");
                var dx = Math.Sign(run.x2 - run.x1);
                var dz = Math.Sign(run.z2 - run.z1);
                var length = Math.Max(Math.Abs(run.x2 - run.x1), Math.Abs(run.z2 - run.z1));
                for (var i = 0; i <= length; i++)
                {
                    var x = run.x1 + dx * i;
                    var z = run.z1 + dz * i;
                    if (!VillageGrid.InBounds(x, z)) throw new FormatException($"Raid {id}: wall leaves the map.");
                    // Runs may share a corner. Anything else on the cell is a mistake in the data.
                    if (!taken.Add(z * VillageGrid.Size + x))
                    {
                        if (result.Any(b => b.isWall && b.x == x && b.z == z)) continue;
                        throw new FormatException($"Raid {id}: wall overlaps a building.");
                    }
                    Add(Village.WallId, run.level, x, z);
                }
            }
            return result;
        }

        /// <summary>The same layout in the shape the village renderer already understands.</summary>
        public static VillageSave ToSave(IEnumerable<BattleBuildingInput> buildings)
        {
            var save = new VillageSave { schemaVersion = VillageSave.CurrentSchemaVersion, mapVersion = VillageSave.CurrentMapVersion, buildings = new List<BuildingSave>() };
            var highest = 0;
            foreach (var b in buildings)
            {
                save.buildings.Add(new BuildingSave { id = b.id, definitionId = b.definitionId, x = b.x, z = b.z, level = b.level });
                highest = Math.Max(highest, b.id);
            }
            save.nextInstanceId = highest + 1;
            return save;
        }
    }

    public sealed class RaidCatalog
    {
        public const int SupportedSchemaVersion = 1;

        private sealed class Document
        {
            public int schemaVersion;
            public List<RaidDefinition> raids = new List<RaidDefinition>();
        }

        public IReadOnlyList<RaidDefinition> All { get; private set; }

        public RaidDefinition Find(string id) => All.FirstOrDefault(r => r.id == id);

        public static RaidCatalog FromJson(string json, BuildingCatalog buildings)
        {
            var document = JsonConvert.DeserializeObject<Document>(json);
            if (document == null || document.schemaVersion != SupportedSchemaVersion)
                throw new FormatException("Unsupported raid file.");
            foreach (var raid in document.raids)
                if (raid.Expand(buildings).Count == 0)
                    throw new FormatException($"Raid {raid.id} has no buildings.");
            return new RaidCatalog { All = document.raids };
        }
    }
}

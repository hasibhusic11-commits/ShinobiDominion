using System;
using System.Collections.Generic;
using System.Linq;

namespace ShinobiDominion.Core
{
    public enum VillageError
    {
        None,
        UnknownBuilding,
        OutOfBounds,
        Overlap,
        BlockedTerrain,
        LimitReached,
        InsufficientFunds,
        NotFound,
        NotRotatable,
        MaxLevel,
        NotRemovable,
        NotAHeroBase,
        HeroAlreadyAssigned,
        InvalidHeroId,
        NotACollector,
        NothingToCollect,
        StorageFull,
        NotAWall,
        DojoTooLow,
        BadTroop,
        NoTrainingHall,
        TroopLocked,
        CampsFull,
        OneHeroOnly
    }

    public static class VillageErrorText
    {
        public static string Describe(VillageError error)
        {
            switch (error)
            {
                case VillageError.None: return "Ready";
                case VillageError.UnknownBuilding: return "Unknown building";
                case VillageError.OutOfBounds: return "Outside the 50 x 50 village";
                case VillageError.Overlap: return "Overlaps another building";
                case VillageError.BlockedTerrain: return "This ground cannot be built on";
                case VillageError.LimitReached: return "Limit reached for this Dojo level";
                case VillageError.InsufficientFunds: return "Not enough resources";
                case VillageError.NotFound: return "Building no longer exists";
                case VillageError.NotRotatable: return "This building cannot rotate";
                case VillageError.MaxLevel: return "Already at maximum level";
                case VillageError.NotRemovable: return "This building cannot be removed";
                case VillageError.NotAHeroBase: return "Only a Hero Base can hold a hero";
                case VillageError.HeroAlreadyAssigned: return "That hero is already on another base";
                case VillageError.OneHeroOnly: return "Only one hero can be used at a time. Take your hero off its base first.";
                case VillageError.InvalidHeroId: return "Invalid hero";
                case VillageError.NotACollector: return "This building produces nothing";
                case VillageError.NothingToCollect: return "Nothing to collect yet";
                case VillageError.StorageFull: return "Storage is full";
                case VillageError.NotAWall: return "Not a wall";
                case VillageError.DojoTooLow: return "Upgrade the Dojo first";
                case VillageError.BadTroop: return "That troop can't be trained";
                case VillageError.NoTrainingHall: return "Build a Training Hall first";
                case VillageError.TroopLocked: return "Upgrade the Training Hall first";
                case VillageError.CampsFull: return "Your Troop Camps are full";
                default: return error.ToString();
            }
        }
    }

    /// <summary>
    /// The village rules. Every mutation validates first and applies only on success, so a
    /// failed operation leaves grid, buildings and wallet untouched.
    /// LOCAL PROTOTYPE: until Phase 3 this runs on the client. The server must enforce these
    /// same rules, because a client-side check alone cannot be trusted.
    /// </summary>
    public sealed class Village
    {
        public const string HeadquartersId = "dojo_hq";
        public const string WallId = "wall";

        private readonly Dictionary<int, BuildingInstance> _buildings = new Dictionary<int, BuildingInstance>();

        public BuildingCatalog Catalog { get; }
        public VillageGrid Grid { get; } = new VillageGrid();
        public long Gold { get; private set; }
        public long Crystals { get; private set; }
        public int NextInstanceId { get; private set; } = 1;

        public IEnumerable<BuildingInstance> Buildings => _buildings.Values;
        public int BuildingCount => _buildings.Count;

        /// <summary>Raised after any successful change, including a restore.</summary>
        public event Action Changed;

        public Village(BuildingCatalog catalog)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Gold = catalog.StartingGold;
            Crystals = catalog.StartingCrystals;
        }

        public int HqLevel
        {
            get
            {
                foreach (var building in _buildings.Values)
                    if (building.Definition.id == HeadquartersId)
                        return building.Level;
                return 1;
            }
        }

        public long GoldCapacity
        {
            get
            {
                var capacity = Catalog.BaseGoldCapacity;
                foreach (var building in _buildings.Values)
                    capacity += building.Definition.At(building.Level).goldCapacity;
                return capacity;
            }
        }

        public BuildingInstance Find(int id) => _buildings.TryGetValue(id, out var building) ? building : null;

        public BuildingInstance BuildingAt(GridCoord cell)
        {
            if (!VillageGrid.InBounds(cell)) return null;
            var id = Grid.OccupantAt(cell);
            return id == VillageGrid.NoOccupant ? null : Find(id);
        }

        public int CountOf(string definitionId) => _buildings.Values.Count(b => b.Definition.id == definitionId);
        public int LimitOf(BuildingDefinition definition) => definition.MaxCountAt(HqLevel);
        public bool CanAfford(long gold, long crystals) => Gold >= gold && Crystals >= crystals;

        /// <summary>Takes gold the caller has already checked is available.</summary>
        public void PayGold(long gold)
        {
            Gold -= gold;
            Changed?.Invoke();
        }

        // ---------- Placement ----------

        public VillageError CheckFootprint(BuildingDefinition definition, GridCoord origin, Rotation rotation, int ignoreInstanceId = 0)
        {
            if (definition == null) return VillageError.UnknownBuilding;
            var footprint = Footprint.Of(definition, origin, rotation);
            if (!footprint.InBounds) return VillageError.OutOfBounds;
            foreach (var cell in footprint.Cells())
            {
                if (Grid.IsBlocked(cell)) return VillageError.BlockedTerrain;
                var occupant = Grid.OccupantAt(cell);
                if (occupant != VillageGrid.NoOccupant && occupant != ignoreInstanceId)
                    return VillageError.Overlap;
            }
            return VillageError.None;
        }

        /// <summary>Full check for building something new: limits, funds and footprint.</summary>
        public VillageError CheckNewPlacement(BuildingDefinition definition, GridCoord origin, Rotation rotation)
        {
            if (definition == null) return VillageError.UnknownBuilding;
            if (rotation != Rotation.R0 && !definition.rotatable) return VillageError.NotRotatable;
            if (CountOf(definition.id) >= LimitOf(definition)) return VillageError.LimitReached;
            if (!CanAfford(definition.costGold, definition.costCrystals)) return VillageError.InsufficientFunds;
            return CheckFootprint(definition, origin, rotation);
        }

        public VillageError TryPlace(string definitionId, GridCoord origin, Rotation rotation, long nowUtcTicks, out int instanceId)
        {
            instanceId = 0;
            if (!Catalog.TryGet(definitionId, out var definition)) return VillageError.UnknownBuilding;
            var error = CheckNewPlacement(definition, origin, rotation);
            if (error != VillageError.None) return error;

            Gold -= definition.costGold;
            Crystals -= definition.costCrystals;
            instanceId = AddUnchecked(definition, origin, rotation, 1, null, nowUtcTicks).Id;
            Changed?.Invoke();
            return VillageError.None;
        }

        public VillageError TryMove(int id, GridCoord newOrigin, Rotation newRotation)
        {
            var building = Find(id);
            if (building == null) return VillageError.NotFound;
            if (newRotation != building.Rotation && !building.Definition.rotatable) return VillageError.NotRotatable;
            var error = CheckFootprint(building.Definition, newOrigin, newRotation, id);
            if (error != VillageError.None) return error;

            Grid.Clear(building.Footprint);
            building.Origin = newOrigin;
            building.Rotation = newRotation;
            Grid.Fill(building.Footprint, id);
            Changed?.Invoke();
            return VillageError.None;
        }

        public VillageError TryRemove(int id)
        {
            var building = Find(id);
            if (building == null) return VillageError.NotFound;
            if (!building.Definition.removable) return VillageError.NotRemovable;
            Grid.Clear(building.Footprint);
            _buildings.Remove(id);
            Changed?.Invoke();
            return VillageError.None;
        }

        public VillageError TryUpgrade(int id)
        {
            var building = Find(id);
            if (building == null) return VillageError.NotFound;
            if (building.Level >= building.Definition.maxLevel) return VillageError.MaxLevel;
            if (HqLevel < building.Definition.At(building.Level + 1).requiredHq) return VillageError.DojoTooLow;
            var cost = building.Definition.UpgradeCost(building.Level);
            if (!CanAfford(cost, 0)) return VillageError.InsufficientFunds;
            Gold -= cost;
            building.Level++;
            Changed?.Invoke();
            return VillageError.None;
        }

        // ---------- Walls ----------

        /// <summary>
        /// The cells of a wall run. Runs are always orthogonal: a diagonal drag locks to the
        /// longer axis, and a tie goes to X. Never produces a diagonal or an out-of-order cell.
        /// </summary>
        public static List<GridCoord> WallRunCells(GridCoord start, GridCoord end)
        {
            var dx = end.X - start.X;
            var dz = end.Z - start.Z;
            var cells = new List<GridCoord>();
            if (Math.Abs(dx) >= Math.Abs(dz))
            {
                var step = dx >= 0 ? 1 : -1;
                for (var i = 0; i <= Math.Abs(dx); i++)
                    cells.Add(new GridCoord(start.X + i * step, start.Z));
            }
            else
            {
                var step = dz >= 0 ? 1 : -1;
                for (var i = 0; i <= Math.Abs(dz); i++)
                    cells.Add(new GridCoord(start.X, start.Z + i * step));
            }
            return cells;
        }

        public WallRunPreview PreviewWallRun(GridCoord start, GridCoord end)
        {
            var wall = Catalog.Get(WallId);
            var preview = new WallRunPreview();
            foreach (var cell in WallRunCells(start, end))
            {
                var cellError = CheckFootprint(wall, cell, Rotation.R0);
                preview.Cells.Add(new WallCellPreview(cell, cellError));
                if (preview.Error == VillageError.None) preview.Error = cellError;
            }

            preview.TotalGold = wall.costGold * preview.Cells.Count;
            preview.TotalCrystals = wall.costCrystals * preview.Cells.Count;
            if (preview.Error == VillageError.None && CountOf(WallId) + preview.Cells.Count > LimitOf(wall))
                preview.Error = VillageError.LimitReached;
            if (preview.Error == VillageError.None && !CanAfford(preview.TotalGold, preview.TotalCrystals))
                preview.Error = VillageError.InsufficientFunds;
            return preview;
        }

        /// <summary>All-or-nothing: if any cell or the total cost fails, nothing is placed or spent.</summary>
        public VillageError TryPlaceWallRun(GridCoord start, GridCoord end, long nowUtcTicks)
        {
            var preview = PreviewWallRun(start, end);
            if (preview.Error != VillageError.None) return preview.Error;

            var wall = Catalog.Get(WallId);
            Gold -= preview.TotalGold;
            Crystals -= preview.TotalCrystals;
            foreach (var cell in preview.Cells)
                AddUnchecked(wall, cell.Coord, Rotation.R0, 1, null, nowUtcTicks);
            Changed?.Invoke();
            return VillageError.None;
        }

        public const int WallNorth = 1, WallEast = 2, WallSouth = 4, WallWest = 8;

        /// <summary>Which orthogonal neighbours are walls, as a bitmask. Drives the joined wall mesh.</summary>
        public int WallConnectionMask(GridCoord cell)
        {
            var mask = 0;
            if (IsWallAt(cell.X, cell.Z + 1)) mask |= WallNorth;
            if (IsWallAt(cell.X + 1, cell.Z)) mask |= WallEast;
            if (IsWallAt(cell.X, cell.Z - 1)) mask |= WallSouth;
            if (IsWallAt(cell.X - 1, cell.Z)) mask |= WallWest;
            return mask;
        }

        private bool IsWallAt(int x, int z)
        {
            var building = BuildingAt(new GridCoord(x, z));
            return building != null && building.Definition.IsWall;
        }

        /// <summary>Every wall orthogonally connected to the given wall, including itself.</summary>
        public List<BuildingInstance> ConnectedWalls(int wallId)
        {
            var result = new List<BuildingInstance>();
            var first = Find(wallId);
            if (first == null || !first.Definition.IsWall) return result;

            var visited = new HashSet<int> { wallId };
            var frontier = new Queue<BuildingInstance>();
            frontier.Enqueue(first);
            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                result.Add(current);
                var c = current.Origin;
                var neighbours = new[]
                {
                    new GridCoord(c.X, c.Z + 1), new GridCoord(c.X + 1, c.Z),
                    new GridCoord(c.X, c.Z - 1), new GridCoord(c.X - 1, c.Z)
                };
                foreach (var neighbour in neighbours)
                {
                    var building = BuildingAt(neighbour);
                    if (building != null && building.Definition.IsWall && visited.Add(building.Id))
                        frontier.Enqueue(building);
                }
            }
            return result;
        }

        /// <summary>Upgrades a connected wall group by one level, all-or-nothing. Maxed segments are skipped.</summary>
        public VillageError TryUpgradeWallGroup(int wallId)
        {
            var first = Find(wallId);
            if (first == null) return VillageError.NotFound;
            if (!first.Definition.IsWall) return VillageError.NotAWall;

            var below = ConnectedWalls(wallId).Where(w => w.Level < w.Definition.maxLevel).ToList();
            if (below.Count == 0) return VillageError.MaxLevel;
            // Segments the Dojo can't support yet are left out. If that's all of them, say why.
            var upgradable = below.Where(w => HqLevel >= w.Definition.At(w.Level + 1).requiredHq).ToList();
            if (upgradable.Count == 0) return VillageError.DojoTooLow;
            var total = upgradable.Sum(w => w.Definition.UpgradeCost(w.Level));
            if (!CanAfford(total, 0)) return VillageError.InsufficientFunds;

            Gold -= total;
            foreach (var wall in upgradable) wall.Level++;
            Changed?.Invoke();
            return VillageError.None;
        }

        // ---------- Hero bases ----------

        /// <summary>Only one hero is used at a time: it leads raids and guards the village (ADR-030).</summary>
        public const int MaxAssignedHeroes = 1;

        public VillageError TryAssignHero(int baseId, string heroInstanceId)
        {
            var building = Find(baseId);
            if (building == null) return VillageError.NotFound;
            if (building.Definition.function != BuildingFunction.HeroBase) return VillageError.NotAHeroBase;
            if (string.IsNullOrWhiteSpace(heroInstanceId)) return VillageError.InvalidHeroId;
            foreach (var other in _buildings.Values)
                if (other.Id != baseId && other.AssignedHeroInstanceId == heroInstanceId)
                    return VillageError.HeroAlreadyAssigned;
            if (_buildings.Values.Count(b => b.Id != baseId && b.AssignedHeroInstanceId != null) >= MaxAssignedHeroes)
                return VillageError.OneHeroOnly;

            building.AssignedHeroInstanceId = heroInstanceId;
            Changed?.Invoke();
            return VillageError.None;
        }

        public bool IsHeroAssigned(string heroInstanceId) =>
            _buildings.Values.Any(b => b.AssignedHeroInstanceId == heroInstanceId);

        public VillageError TryUnassignHero(int baseId)
        {
            var building = Find(baseId);
            if (building == null) return VillageError.NotFound;
            if (building.Definition.function != BuildingFunction.HeroBase) return VillageError.NotAHeroBase;
            building.AssignedHeroInstanceId = null;
            Changed?.Invoke();
            return VillageError.None;
        }

        // ---------- Collectors ----------

        public long PendingCollection(BuildingInstance building, long nowUtcTicks)
        {
            var definition = building.Definition;
            if (definition.produces == ResourceKind.None) return 0;
            var row = definition.At(building.Level);
            if (row.production <= 0) return 0;
            var elapsed = Math.Max(0, nowUtcTicks - building.LastCollectedUtcTicks);
            var hours = (decimal)elapsed / TimeSpan.TicksPerHour;
            var cap = row.storedCap;
            var produced = hours * row.production;
            return (long)Math.Min(produced, cap);
        }

        public VillageError TryCollect(int id, long nowUtcTicks, out long collected)
        {
            collected = 0;
            var building = Find(id);
            if (building == null) return VillageError.NotFound;
            if (building.Definition.produces == ResourceKind.None) return VillageError.NotACollector;
            var pending = PendingCollection(building, nowUtcTicks);
            if (pending <= 0) return VillageError.NothingToCollect;

            if (building.Definition.produces == ResourceKind.Gold)
            {
                var room = GoldCapacity - Gold;
                if (room <= 0) return VillageError.StorageFull;
                collected = Math.Min(pending, room);
                Gold += collected;
            }
            else
            {
                collected = pending;
                Crystals += collected;
            }

            // Anything that did not fit stays in the building by moving the timer back.
            var leftover = pending - collected;
            var rate = building.Definition.ProductionAt(building.Level);
            building.LastCollectedUtcTicks = nowUtcTicks - leftover * TimeSpan.TicksPerHour / rate;
            Changed?.Invoke();
            return VillageError.None;
        }

        // ---------- Save and restore ----------

        public VillageSave ToSave()
        {
            var save = new VillageSave
            {
                schemaVersion = VillageSave.CurrentSchemaVersion,
                mapVersion = VillageSave.CurrentMapVersion,
                nextInstanceId = NextInstanceId,
                gold = Gold,
                crystals = Crystals
            };
            foreach (var building in _buildings.Values.OrderBy(b => b.Id))
            {
                save.buildings.Add(new BuildingSave
                {
                    id = building.Id,
                    definitionId = building.Definition.id,
                    x = building.Origin.X,
                    z = building.Origin.Z,
                    rotation = (int)building.Rotation,
                    level = building.Level,
                    assignedHeroInstanceId = building.AssignedHeroInstanceId,
                    lastCollectedUtcTicks = building.LastCollectedUtcTicks
                });
            }
            return save;
        }

        /// <summary>
        /// Replaces this village with a save. The save is fully validated on a scratch village
        /// first, so a corrupt save throws <see cref="VillageSaveException"/> and changes nothing.
        /// </summary>
        public void Restore(VillageSave save)
        {
            var scratch = new Village(Catalog);
            scratch.ApplySave(save);
            ApplySave(save);
            Changed?.Invoke();
        }

        public static Village FromSave(BuildingCatalog catalog, VillageSave save)
        {
            var village = new Village(catalog);
            village.ApplySave(save);
            return village;
        }

        private void ApplySave(VillageSave save)
        {
            if (save == null) throw new VillageSaveException("Save is empty.");
            if (save.schemaVersion != VillageSave.CurrentSchemaVersion)
                throw new VillageSaveException($"Unsupported save schema {save.schemaVersion}.");
            if (save.mapVersion != VillageSave.CurrentMapVersion)
                throw new VillageSaveException($"Unsupported map version {save.mapVersion}.");
            if (save.gold < 0 || save.crystals < 0)
                throw new VillageSaveException("Negative currency in save.");

            _buildings.Clear();
            Grid.ClearAll();
            Gold = save.gold;
            Crystals = save.crystals;

            var heroes = new HashSet<string>();
            var maxId = 0;
            foreach (var entry in save.buildings)
            {
                if (!Catalog.TryGet(entry.definitionId, out var definition))
                    throw new VillageSaveException($"Unknown building '{entry.definitionId}'.");
                if (entry.id <= 0 || _buildings.ContainsKey(entry.id))
                    throw new VillageSaveException($"Invalid or duplicate building id {entry.id}.");
                if (entry.rotation < 0 || entry.rotation > 3)
                    throw new VillageSaveException($"Invalid rotation on building {entry.id}.");
                var rotation = (Rotation)entry.rotation;
                if (rotation != Rotation.R0 && !definition.rotatable)
                    throw new VillageSaveException($"Building {entry.id} cannot be rotated.");
                if (entry.level < 1 || entry.level > definition.maxLevel)
                    throw new VillageSaveException($"Invalid level on building {entry.id}.");
                var origin = new GridCoord(entry.x, entry.z);
                var footprintError = CheckFootprint(definition, origin, rotation);
                if (footprintError != VillageError.None)
                    throw new VillageSaveException($"Building {entry.id} at {origin}: {footprintError}.");
                if (!string.IsNullOrEmpty(entry.assignedHeroInstanceId))
                {
                    if (definition.function != BuildingFunction.HeroBase)
                        throw new VillageSaveException($"Building {entry.id} cannot hold a hero.");
                    if (!heroes.Add(entry.assignedHeroInstanceId))
                        throw new VillageSaveException("One hero is assigned to two bases.");
                }

                var building = new BuildingInstance
                {
                    Id = entry.id,
                    Definition = definition,
                    Origin = origin,
                    Rotation = rotation,
                    Level = entry.level,
                    AssignedHeroInstanceId = string.IsNullOrEmpty(entry.assignedHeroInstanceId) ? null : entry.assignedHeroInstanceId,
                    LastCollectedUtcTicks = entry.lastCollectedUtcTicks
                };
                _buildings.Add(building.Id, building);
                Grid.Fill(building.Footprint, building.Id);
                maxId = Math.Max(maxId, entry.id);
            }

            foreach (var definition in Catalog.All)
            {
                // Villages from before the one-hero rule may keep their extra bases, empty.
                if (definition.function == BuildingFunction.HeroBase) continue;
                if (CountOf(definition.id) > LimitOf(definition))
                    throw new VillageSaveException($"Too many '{definition.id}' for the Dojo level.");
            }
            foreach (var extra in _buildings.Values.Where(b => b.AssignedHeroInstanceId != null).OrderBy(b => b.Id).Skip(MaxAssignedHeroes).ToList())
                extra.AssignedHeroInstanceId = null;

            if (save.nextInstanceId <= maxId)
                throw new VillageSaveException("Save's next id collides with an existing building.");
            NextInstanceId = save.nextInstanceId;
        }

        private BuildingInstance AddUnchecked(BuildingDefinition definition, GridCoord origin, Rotation rotation, int level, string hero, long nowUtcTicks)
        {
            var building = new BuildingInstance
            {
                Id = NextInstanceId++,
                Definition = definition,
                Origin = origin,
                Rotation = rotation,
                Level = level,
                AssignedHeroInstanceId = hero,
                LastCollectedUtcTicks = nowUtcTicks
            };
            _buildings.Add(building.Id, building);
            Grid.Fill(building.Footprint, building.Id);
            return building;
        }
    }

    public readonly struct WallCellPreview
    {
        public readonly GridCoord Coord;
        public readonly VillageError Error;
        public WallCellPreview(GridCoord coord, VillageError error)
        {
            Coord = coord;
            Error = error;
        }
        public bool IsValid => Error == VillageError.None;
    }

    public sealed class WallRunPreview
    {
        public readonly List<WallCellPreview> Cells = new List<WallCellPreview>();
        public long TotalGold;
        public long TotalCrystals;
        public VillageError Error = VillageError.None;
        public bool CanCommit => Error == VillageError.None;
    }
}

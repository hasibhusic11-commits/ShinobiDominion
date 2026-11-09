using System;
using System.Collections.Generic;
using System.Linq;

namespace ShinobiDominion.Core
{
    /// <summary>Research settings from troops.json (ADR-041). Test values.</summary>
    public sealed class ResearchSettings
    {
        public int maxLevel;
        public long percentPerLevel;
        public long manaPerLevel;
        public long[] seconds;
    }

    public sealed class ResearchEntry
    {
        public int Level;
        /// <summary>When a running study finishes; 0 when none runs.</summary>
        public long DoneUtcTicks;
        public bool Running => DoneUtcTicks > 0;
    }

    /// <summary>
    /// Troop research at the Research Hut (ADR-041). A study raises one kind of troop's HP and attack, costs
    /// Mana, takes time, and only one runs at a time; the hut's level caps the level a study can reach.
    /// Mirrors research.go on the server. Online, the server's answer replaces this.
    /// </summary>
    public sealed class TroopResearch
    {
        private readonly Dictionary<string, ResearchEntry> _entries = new Dictionary<string, ResearchEntry>();

        public IReadOnlyDictionary<string, ResearchEntry> Entries => _entries;

        public int LevelOf(string troopId) => _entries.TryGetValue(troopId, out var entry) ? entry.Level : 0;

        public ResearchEntry Running => _entries.Values.FirstOrDefault(e => e.Running);

        public string RunningTroop => _entries.FirstOrDefault(p => p.Value.Running).Key;

        public void Replace(IEnumerable<KeyValuePair<string, ResearchEntry>> entries)
        {
            _entries.Clear();
            foreach (var pair in entries)
                _entries[pair.Key] = new ResearchEntry { Level = pair.Value.Level, DoneUtcTicks = pair.Value.DoneUtcTicks };
        }

        public static long Cost(TroopCatalog catalog, TroopDefinition troop, int level) =>
            troop.manaCost * catalog.Research.manaPerLevel * level * level;

        public static long Seconds(TroopCatalog catalog, int level) =>
            level < 0 || level >= catalog.Research.seconds.Length ? 0 : catalog.Research.seconds[level];

        /// <summary>A troop's battle stats after research: HP and attack grow, defense does not.</summary>
        public static (long hp, long attack, long defense) Boosted(TroopCatalog catalog, TroopDefinition troop, int level)
        {
            var percent = 100 + catalog.Research.percentPerLevel * level;
            return (troop.hp * percent / 100, troop.attack * percent / 100, troop.defense);
        }

        public static int HutLevel(Village village) =>
            village.Buildings.Where(b => b.Definition.function == BuildingFunction.Research).Select(b => b.Level).DefaultIfEmpty(0).Max();

        /// <summary>Finishes a study whose time has come. True if one did.</summary>
        public bool Settle(long nowUtcTicks)
        {
            var changed = false;
            foreach (var entry in _entries.Values)
            {
                if (!entry.Running || entry.DoneUtcTicks > nowUtcTicks) continue;
                entry.Level++;
                entry.DoneUtcTicks = 0;
                changed = true;
            }
            return changed;
        }

        public VillageError Check(Village village, TroopCatalog catalog, string troopId)
        {
            var troop = catalog.Find(troopId);
            if (troop == null) return VillageError.BadTroop;
            var hut = HutLevel(village);
            if (hut == 0) return VillageError.NoResearchHut;
            if (Running != null) return VillageError.ResearchBusy;
            var next = LevelOf(troopId) + 1;
            if (next > catalog.Research.maxLevel) return VillageError.MaxLevel;
            if (hut < next) return VillageError.ResearchHutTooLow;
            if (village.Mana < Cost(catalog, troop, next)) return VillageError.InsufficientMana;
            return VillageError.None;
        }

        public VillageError TryStart(Village village, TroopCatalog catalog, string troopId, long nowUtcTicks)
        {
            Settle(nowUtcTicks);
            var error = Check(village, catalog, troopId);
            if (error != VillageError.None) return error;
            var next = LevelOf(troopId) + 1;
            village.TrySpendMana(Cost(catalog, catalog.Find(troopId), next));
            if (!_entries.TryGetValue(troopId, out var entry)) _entries[troopId] = entry = new ResearchEntry();
            var seconds = Seconds(catalog, next);
            if (seconds > 0) entry.DoneUtcTicks = nowUtcTicks + seconds * TimeSpan.TicksPerSecond;
            else entry.Level = next;
            return VillageError.None;
        }

        public VillageError TryFinish(Village village, string troopId, long nowUtcTicks)
        {
            Settle(nowUtcTicks);
            if (!_entries.TryGetValue(troopId, out var entry) || !entry.Running) return VillageError.NotUpgrading;
            var cost = Village.FinishCost(entry.DoneUtcTicks - nowUtcTicks);
            if (!village.TrySpendCrystals(cost)) return VillageError.InsufficientFunds;
            entry.Level++;
            entry.DoneUtcTicks = 0;
            return VillageError.None;
        }
    }
}

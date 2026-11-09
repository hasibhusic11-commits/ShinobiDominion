using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ShinobiDominion.Core
{
    public sealed class VillageSaveException : Exception
    {
        public VillageSaveException(string message) : base(message) { }
    }

    /// <summary>Versioned save record. Stores stable ids only, never engine objects.</summary>
    [Serializable]
    public sealed class VillageSave
    {
        public const int CurrentSchemaVersion = 1;
        public const int CurrentMapVersion = 1;

        public int schemaVersion;
        public int mapVersion;
        public int nextInstanceId;
        public long gold;
        public long crystals;
        public long mana;
        public List<BuildingSave> buildings = new List<BuildingSave>();

        /// <summary>
        /// A battle layout being shown, not a player's village: dungeons may hold more towers and guards than any
        /// Town Hall allows (ADR-047), so the count limits are not checked. Never set for a player's own save.
        /// </summary>
        public bool battleLayout;

        public string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented);

        public static VillageSave FromJson(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<VillageSave>(json)
                       ?? throw new VillageSaveException("Save is empty.");
            }
            catch (JsonException exception)
            {
                throw new VillageSaveException("Save is not valid JSON: " + exception.Message);
            }
        }
    }

    [Serializable]
    public sealed class BuildingSave
    {
        public int id;
        public string definitionId;
        public int x;
        public int z;
        public int rotation;
        public int level;
        public string assignedHeroInstanceId;
        public long lastCollectedUtcTicks;
        /// <summary>When a running upgrade finishes; 0 when none is running (ADR-040).</summary>
        public long upgradeDoneUtcTicks;
    }

    /// <summary>Snapshot undo stack. Restoring also restores the wallet, so undo refunds exactly.</summary>
    public sealed class VillageHistory
    {
        private readonly Stack<string> _snapshots = new Stack<string>();

        public int Count => _snapshots.Count;
        public void Capture(Village village) => _snapshots.Push(village.ToSave().ToJson());
        public void Clear() => _snapshots.Clear();

        public bool Undo(Village village)
        {
            if (_snapshots.Count == 0) return false;
            village.Restore(VillageSave.FromJson(_snapshots.Pop()));
            return true;
        }
    }

    /// <summary>The village a brand-new player starts with.</summary>
    public static class StarterVillage
    {
        public static Village Create(BuildingCatalog catalog, long nowUtcTicks)
        {
            var village = new Village(catalog);
            Grant(village, "dojo_hq", 22, 22, nowUtcTicks);
            Grant(village, "builder_hall", 16, 23, nowUtcTicks);
            Grant(village, "hero_altar", 29, 22, nowUtcTicks);
            Grant(village, "summoning_gate", 23, 30, nowUtcTicks);
            Grant(village, "gold_mine", 18, 16, nowUtcTicks);
            Grant(village, "hero_base", 19, 29, nowUtcTicks);
            return village;
        }

        private static void Grant(Village village, string definitionId, int x, int z, long nowUtcTicks)
        {
            // Starter buildings are free: pay for the placement, then refund through the save record.
            var gold = village.Gold;
            var crystals = village.Crystals;
            var error = village.TryPlace(definitionId, new GridCoord(x, z), Rotation.R0, nowUtcTicks, out _);
            if (error != VillageError.None)
                throw new InvalidOperationException($"Starter layout is invalid: {definitionId} at ({x},{z}): {error}");

            var save = village.ToSave();
            save.gold = gold;
            save.crystals = crystals;
            village.Restore(save);
        }
    }
}

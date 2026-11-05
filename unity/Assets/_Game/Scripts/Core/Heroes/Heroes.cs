using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ShinobiDominion.Core
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum HeroRarity { Common, Elite, Legendary }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum HeroRole { Tank, Fighter, Assassin, Mage, Control, Support }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum HeroAffinity { Solar, Lightning, Earth, Lunar, Void, Fire, Storm, Water, Steel }

    /// <summary>One of a Legendary's four forms: data for its portrait, model and effects budget.</summary>
    [Serializable]
    public sealed class HeroFormVisual
    {
        public string formName;
        public string summary;
        public string primaryColor;
        public int vfxBudget;
        public string portraitAssetId;
        public string modelAssetId;
    }

    [Serializable]
    public sealed class HeroSkill
    {
        public string id;
        public string displayName;
        public string description;
        public int unlockEvolution;
    }

    /// <summary>A hero identity. Rarity, role and affinity describe it but do not identify it; only id does.</summary>
    [Serializable]
    public sealed class HeroArchetype
    {
        public string id;
        public string displayName;
        public HeroRarity rarity;
        public HeroRole role;
        public HeroAffinity affinity;
        public bool contentComplete;
        public long baseHp;
        public long baseAttack;
        public long baseDefense;
        public long growthHp;
        public long growthAttack;
        public long growthDefense;
        public string primaryColor;
        public List<HeroSkill> skills = new List<HeroSkill>();
        public List<HeroFormVisual> forms = new List<HeroFormVisual>();

        public bool CanEvolve => rarity == HeroRarity.Legendary;
    }

    [Serializable]
    public sealed class HeroCatalogFile
    {
        public int schemaVersion;
        public string balanceStatus;
        public List<string> catalystByTransition = new List<string>();
        public long xpPerLevelBase;
        public long xpPerLevelStep;
        public List<HeroArchetype> heroes = new List<HeroArchetype>();
    }

    public sealed class HeroCatalog
    {
        public const int SupportedSchemaVersion = 1;
        private readonly Dictionary<string, HeroArchetype> _byId;

        public IReadOnlyList<HeroArchetype> All { get; }
        public IReadOnlyList<string> CatalystByTransition { get; }
        public long XpPerLevelBase { get; }
        public long XpPerLevelStep { get; }
        public string BalanceStatus { get; }

        private HeroCatalog(HeroCatalogFile file)
        {
            All = file.heroes;
            CatalystByTransition = file.catalystByTransition;
            XpPerLevelBase = file.xpPerLevelBase;
            XpPerLevelStep = file.xpPerLevelStep;
            BalanceStatus = file.balanceStatus;
            _byId = file.heroes.ToDictionary(h => h.id);
        }

        public static HeroCatalog FromJson(string json)
        {
            var file = JsonConvert.DeserializeObject<HeroCatalogFile>(json);
            if (file == null) throw new FormatException("Hero catalog is empty.");
            if (file.schemaVersion != SupportedSchemaVersion)
                throw new FormatException($"Unsupported hero catalog schema {file.schemaVersion}.");
            if (file.catalystByTransition.Count != HeroMath.MaxEvolution)
                throw new FormatException("There must be exactly one catalyst per evolution transition.");
            if (file.catalystByTransition.Distinct().Count() != HeroMath.MaxEvolution)
                throw new FormatException("Each evolution transition needs its own catalyst.");

            var seen = new HashSet<string>();
            foreach (var hero in file.heroes)
            {
                if (string.IsNullOrEmpty(hero.id)) throw new FormatException("A hero has no id.");
                if (!seen.Add(hero.id)) throw new FormatException($"Duplicate hero id '{hero.id}'.");
                if (hero.baseHp <= 0 || hero.baseAttack < 0 || hero.baseDefense < 0)
                    throw new FormatException($"'{hero.id}' has invalid base stats.");
                if (hero.growthHp < 0 || hero.growthAttack < 0 || hero.growthDefense < 0)
                    throw new FormatException($"'{hero.id}' has negative growth, which would make power fall as it levels.");
                if (hero.contentComplete && hero.CanEvolve && hero.forms.Count != HeroMath.FormCount)
                    throw new FormatException($"Legendary '{hero.id}' needs exactly {HeroMath.FormCount} forms.");
            }
            return new HeroCatalog(file);
        }

        public bool TryGet(string id, out HeroArchetype archetype)
        {
            if (id == null)
            {
                archetype = null;
                return false;
            }
            return _byId.TryGetValue(id, out archetype);
        }

        public HeroArchetype Get(string id) =>
            TryGet(id, out var hero) ? hero : throw new KeyNotFoundException($"Unknown hero '{id}'.");
    }

    public readonly struct HeroStats
    {
        public readonly long Hp;
        public readonly long Attack;
        public readonly long Defense;
        public HeroStats(long hp, long attack, long defense)
        {
            Hp = hp;
            Attack = attack;
            Defense = defense;
        }
    }

    public static class HeroMath
    {
        public const int MaxLevel = 200;
        public const int MaxEvolution = 3;
        public const int FormCount = MaxEvolution + 1;

        /// <summary>
        /// Monotonic progression across forms. Evo I Lv1 is 201 and Base Lv200 is 200, so
        /// evolving can never lower combat power even though the displayed level resets.
        /// </summary>
        public static int Cumulative(int evolutionIndex, int level)
        {
            if (evolutionIndex < 0 || evolutionIndex > MaxEvolution)
                throw new ArgumentOutOfRangeException(nameof(evolutionIndex));
            if (level < 1 || level > MaxLevel)
                throw new ArgumentOutOfRangeException(nameof(level));
            return MaxLevel * evolutionIndex + level;
        }

        public static HeroStats Stats(HeroArchetype hero, int evolutionIndex, int level)
        {
            long steps = Cumulative(evolutionIndex, level) - 1;
            checked
            {
                return new HeroStats(
                    hero.baseHp + hero.growthHp * steps,
                    hero.baseAttack + hero.growthAttack * steps,
                    hero.baseDefense + hero.growthDefense * steps);
            }
        }
    }

    /// <summary>A hero the player owns. Its UUID is its identity; many instances may share one archetype.</summary>
    public sealed class HeroInstance
    {
        public string InstanceId;
        public string ArchetypeId;
        public int EvolutionIndex;
        public int Level = 1;
        public long Experience;
        public bool Locked;
    }

    public enum HeroError
    {
        None,
        HeroNotFound,
        DuplicateNotFound,
        NotEvolvable,
        AtMaxEvolution,
        LevelTooLow,
        SameInstance,
        WrongArchetype,
        DuplicateLocked,
        DuplicateAssigned,
        MissingCatalyst,
        IdempotencyKeyRequired,
        IdempotencyKeyReused,
        InvalidAmount,
        AtMaxLevel
    }

    public static class HeroErrorText
    {
        public static string Describe(HeroError error)
        {
            switch (error)
            {
                case HeroError.None: return "Ready";
                case HeroError.HeroNotFound: return "Hero not found";
                case HeroError.DuplicateNotFound: return "Duplicate not found";
                case HeroError.NotEvolvable: return "Only Legendary heroes evolve";
                case HeroError.AtMaxEvolution: return "Already in final form";
                case HeroError.LevelTooLow: return "Must be level 200 in this form";
                case HeroError.SameInstance: return "A hero cannot be its own duplicate";
                case HeroError.WrongArchetype: return "Duplicate must be the exact same hero";
                case HeroError.DuplicateLocked: return "That copy is locked";
                case HeroError.DuplicateAssigned: return "That copy is on a Hero Base";
                case HeroError.MissingCatalyst: return "Missing the required catalyst";
                case HeroError.IdempotencyKeyRequired: return "Missing request id";
                case HeroError.IdempotencyKeyReused: return "Request id was already used for something else";
                case HeroError.InvalidAmount: return "Invalid amount";
                case HeroError.AtMaxLevel: return "Already at level 200";
                default: return error.ToString();
            }
        }
    }

    public sealed class EvolutionRecord
    {
        public string key;
        public string mainInstanceId;
        public string consumedInstanceId;
        public string catalystId;
        public int fromEvolution;
        public int toEvolution;
    }

    /// <summary>
    /// Owned heroes and items, and the rules that change them. LOCAL PROTOTYPE: Phase 3 moves
    /// these transactions to the server, where the same rules become authoritative.
    /// </summary>
    public sealed class HeroRoster
    {
        private readonly Dictionary<string, HeroInstance> _heroes = new Dictionary<string, HeroInstance>();
        private readonly Dictionary<string, long> _items = new Dictionary<string, long>();
        private readonly Dictionary<string, EvolutionRecord> _evolutions = new Dictionary<string, EvolutionRecord>();

        public HeroCatalog Catalog { get; }
        public IEnumerable<HeroInstance> Heroes => _heroes.Values;
        public int HeroCount => _heroes.Count;
        public IReadOnlyCollection<EvolutionRecord> EvolutionLog => _evolutions.Values;

        public event Action Changed;

        public HeroRoster(HeroCatalog catalog)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public HeroInstance Find(string instanceId) =>
            instanceId != null && _heroes.TryGetValue(instanceId, out var hero) ? hero : null;

        public bool Owns(string instanceId) => Find(instanceId) != null;
        public long ItemCount(string itemId) => _items.TryGetValue(itemId, out var count) ? count : 0;
        public HeroArchetype ArchetypeOf(HeroInstance hero) => Catalog.Get(hero.ArchetypeId);

        public HeroStats StatsOf(HeroInstance hero) =>
            HeroMath.Stats(ArchetypeOf(hero), hero.EvolutionIndex, hero.Level);

        /// <summary>Creates an owned instance. Phase 3 restricts minting to the server.</summary>
        public HeroInstance AddHero(string archetypeId, int evolutionIndex = 0, int level = 1)
        {
            HeroMath.Cumulative(evolutionIndex, level);
            Catalog.Get(archetypeId);
            var hero = new HeroInstance
            {
                InstanceId = Guid.NewGuid().ToString("N"),
                ArchetypeId = archetypeId,
                EvolutionIndex = evolutionIndex,
                Level = level
            };
            _heroes.Add(hero.InstanceId, hero);
            Changed?.Invoke();
            return hero;
        }

        public void AddItem(string itemId, long amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            _items[itemId] = checked(ItemCount(itemId) + amount);
            Changed?.Invoke();
        }

        public void SetLocked(string instanceId, bool locked)
        {
            var hero = Find(instanceId);
            if (hero == null) return;
            hero.Locked = locked;
            Changed?.Invoke();
        }

        public long XpToNextLevel(int level) => Catalog.XpPerLevelBase + Catalog.XpPerLevelStep * (level - 1);

        public HeroError GrantXp(string instanceId, long amount)
        {
            var hero = Find(instanceId);
            if (hero == null) return HeroError.HeroNotFound;
            if (amount <= 0) return HeroError.InvalidAmount;
            if (hero.Level >= HeroMath.MaxLevel) return HeroError.AtMaxLevel;

            hero.Experience = checked(hero.Experience + amount);
            while (hero.Level < HeroMath.MaxLevel && hero.Experience >= XpToNextLevel(hero.Level))
            {
                hero.Experience -= XpToNextLevel(hero.Level);
                hero.Level++;
            }
            if (hero.Level >= HeroMath.MaxLevel) hero.Experience = 0;
            Changed?.Invoke();
            return HeroError.None;
        }

        /// <summary>Every copy that could be consumed to evolve this hero: same archetype, different UUID, unprotected.</summary>
        public List<HeroInstance> EligibleDuplicates(string mainInstanceId, Func<string, bool> isAssigned)
        {
            var main = Find(mainInstanceId);
            if (main == null) return new List<HeroInstance>();
            return _heroes.Values
                .Where(h => h.InstanceId != mainInstanceId && h.ArchetypeId == main.ArchetypeId && !h.Locked && !isAssigned(h.InstanceId))
                .OrderBy(h => h.EvolutionIndex).ThenBy(h => h.Level)
                .ToList();
        }

        public string CatalystFor(int evolutionIndex) =>
            evolutionIndex >= 0 && evolutionIndex < Catalog.CatalystByTransition.Count ? Catalog.CatalystByTransition[evolutionIndex] : null;

        /// <summary>Checks the main hero alone: form, level and catalyst. The duplicate is checked separately.</summary>
        public HeroError CheckEvolutionMain(string mainInstanceId)
        {
            var main = Find(mainInstanceId);
            if (main == null) return HeroError.HeroNotFound;
            if (!ArchetypeOf(main).CanEvolve) return HeroError.NotEvolvable;
            if (main.EvolutionIndex >= HeroMath.MaxEvolution) return HeroError.AtMaxEvolution;
            if (main.Level != HeroMath.MaxLevel) return HeroError.LevelTooLow;
            if (ItemCount(CatalystFor(main.EvolutionIndex)) < 1) return HeroError.MissingCatalyst;
            return HeroError.None;
        }

        public HeroError CheckEvolution(string mainInstanceId, string duplicateInstanceId, Func<string, bool> isAssigned)
        {
            var error = CheckEvolutionMain(mainInstanceId);
            if (error != HeroError.None) return error;
            if (mainInstanceId == duplicateInstanceId) return HeroError.SameInstance;
            var duplicate = Find(duplicateInstanceId);
            if (duplicate == null) return HeroError.DuplicateNotFound;
            if (duplicate.ArchetypeId != Find(mainInstanceId).ArchetypeId) return HeroError.WrongArchetype;
            if (duplicate.Locked) return HeroError.DuplicateLocked;
            if (isAssigned(duplicateInstanceId)) return HeroError.DuplicateAssigned;
            return HeroError.None;
        }

        /// <summary>
        /// All-or-nothing: consumes exactly one duplicate and one catalyst, keeps the main hero's
        /// identity, raises its form by one and resets its displayed level to 1. Retrying a key
        /// returns success without doing it again.
        /// </summary>
        public HeroError TryEvolve(string idempotencyKey, string mainInstanceId, string duplicateInstanceId, Func<string, bool> isAssigned)
        {
            if (string.IsNullOrEmpty(idempotencyKey)) return HeroError.IdempotencyKeyRequired;
            if (_evolutions.TryGetValue(idempotencyKey, out var earlier))
            {
                return earlier.mainInstanceId == mainInstanceId && earlier.consumedInstanceId == duplicateInstanceId
                    ? HeroError.None
                    : HeroError.IdempotencyKeyReused;
            }

            var error = CheckEvolution(mainInstanceId, duplicateInstanceId, isAssigned);
            if (error != HeroError.None) return error;

            var main = Find(mainInstanceId);
            var catalyst = CatalystFor(main.EvolutionIndex);
            _items[catalyst] -= 1;
            _heroes.Remove(duplicateInstanceId);
            _evolutions[idempotencyKey] = new EvolutionRecord
            {
                key = idempotencyKey,
                mainInstanceId = mainInstanceId,
                consumedInstanceId = duplicateInstanceId,
                catalystId = catalyst,
                fromEvolution = main.EvolutionIndex,
                toEvolution = main.EvolutionIndex + 1
            };
            main.EvolutionIndex++;
            main.Level = 1;
            main.Experience = 0;
            Changed?.Invoke();
            return HeroError.None;
        }

        // ---------- Save and load ----------

        public string ToJson()
        {
            var save = new RosterSave { schemaVersion = RosterSave.CurrentSchemaVersion };
            foreach (var hero in _heroes.Values)
            {
                save.heroes.Add(new HeroSave
                {
                    instanceId = hero.InstanceId,
                    archetypeId = hero.ArchetypeId,
                    evolutionIndex = hero.EvolutionIndex,
                    level = hero.Level,
                    experience = hero.Experience,
                    locked = hero.Locked
                });
            }
            foreach (var pair in _items) save.items[pair.Key] = pair.Value;
            save.evolutions.AddRange(_evolutions.Values);
            return JsonConvert.SerializeObject(save, Formatting.Indented);
        }

        public static HeroRoster FromJson(HeroCatalog catalog, string json)
        {
            RosterSave save;
            try
            {
                save = JsonConvert.DeserializeObject<RosterSave>(json);
            }
            catch (JsonException exception)
            {
                throw new VillageSaveException("Roster is not valid JSON: " + exception.Message);
            }
            if (save == null) throw new VillageSaveException("Roster is empty.");
            if (save.schemaVersion != RosterSave.CurrentSchemaVersion)
                throw new VillageSaveException($"Unsupported roster schema {save.schemaVersion}.");

            var roster = new HeroRoster(catalog);
            foreach (var entry in save.heroes)
            {
                if (string.IsNullOrEmpty(entry.instanceId) || roster._heroes.ContainsKey(entry.instanceId))
                    throw new VillageSaveException("Missing or duplicate hero instance id.");
                if (!catalog.TryGet(entry.archetypeId, out var archetype))
                    throw new VillageSaveException($"Unknown hero '{entry.archetypeId}'.");
                if (entry.evolutionIndex < 0 || entry.evolutionIndex > HeroMath.MaxEvolution)
                    throw new VillageSaveException("Evolution out of range.");
                if (entry.evolutionIndex > 0 && !archetype.CanEvolve)
                    throw new VillageSaveException($"'{entry.archetypeId}' cannot have evolved.");
                if (entry.level < 1 || entry.level > HeroMath.MaxLevel)
                    throw new VillageSaveException("Level out of range.");
                if (entry.experience < 0)
                    throw new VillageSaveException("Negative experience.");
                roster._heroes.Add(entry.instanceId, new HeroInstance
                {
                    InstanceId = entry.instanceId,
                    ArchetypeId = entry.archetypeId,
                    EvolutionIndex = entry.evolutionIndex,
                    Level = entry.level,
                    Experience = entry.experience,
                    Locked = entry.locked
                });
            }
            foreach (var pair in save.items)
            {
                if (pair.Value < 0) throw new VillageSaveException("Negative item count.");
                roster._items[pair.Key] = pair.Value;
            }
            foreach (var record in save.evolutions) roster._evolutions[record.key] = record;
            return roster;
        }
    }

    [Serializable]
    public sealed class RosterSave
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion;
        public List<HeroSave> heroes = new List<HeroSave>();
        public Dictionary<string, long> items = new Dictionary<string, long>();
        public List<EvolutionRecord> evolutions = new List<EvolutionRecord>();
    }

    [Serializable]
    public sealed class HeroSave
    {
        public string instanceId;
        public string archetypeId;
        public int evolutionIndex;
        public int level;
        public long experience;
        public bool locked;
    }

    /// <summary>
    /// DEVELOPMENT ONLY, and never a summon: gives testers heroes to work with before the server
    /// exists. The game compiles the call to this out of release builds.
    /// </summary>
    public static class DevInventory
    {
        public static HeroRoster Create(HeroCatalog catalog)
        {
            var roster = new HeroRoster(catalog);
            foreach (var hero in catalog.All.Where(h => h.contentComplete))
                roster.AddHero(hero.id);

            // A Legendary ready to evolve, with its duplicate, and extras to allow an all-Legendary team.
            roster.AddHero("solar_beast_sage", 0, HeroMath.MaxLevel);
            roster.AddHero("thunder_empress");
            roster.AddHero("stone_titan");
            foreach (var catalyst in catalog.CatalystByTransition)
                roster.AddItem(catalyst, 1);
            return roster;
        }
    }
}

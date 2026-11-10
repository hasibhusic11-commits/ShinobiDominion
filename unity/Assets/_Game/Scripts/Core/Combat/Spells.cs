using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace ShinobiDominion.Core
{
    /// <summary>
    /// A battle spell from spells.json (ADR-052). The attacker casts it once in a live raid, on any cell. It opens
    /// when the Research Hut reaches unlockHutLevel, which is spell level 1; each hut level after that is one spell
    /// level more. All values are unapproved test values.
    /// </summary>
    public sealed class SpellDefinition
    {
        public string id;
        public string displayName;
        public string description;
        /// <summary>"damage", "heal", "freeze" or "boost".</summary>
        public string effect;
        public int unlockHutLevel = 1;
        /// <summary>Reach from the middle of the cast cell, in battle units (BattleSim.CellUnits to a cell).</summary>
        public int radius;
        /// <summary>Battle ticks (BattleSim.TicksPerSecond to a second) the spell is spread over, or lasts.</summary>
        public int durationTicks;
        /// <summary>How many times it applies over durationTicks. Freeze and boost apply once.</summary>
        public int pulses = 1;
        /// <summary>
        /// By spell level: damage and heal, HP per pulse; freeze, percent of durationTicks the targets stay frozen;
        /// boost, percent more damage for durationTicks.
        /// </summary>
        public long[] power = new long[0];

        public int MaxLevel => power.Length;

        /// <summary>The spell's level at a Research Hut level: 0 while locked, then 1 at unlockHutLevel and up from there.</summary>
        public int LevelAt(int hutLevel) => hutLevel < unlockHutLevel ? 0 : Math.Min(hutLevel - unlockHutLevel + 1, power.Length);

        /// <summary>The power at a spell level; a level outside the table counts as the nearest one in it.</summary>
        public long PowerAt(int level) => power[Math.Min(Math.Max(level, 1), power.Length) - 1];

        /// <summary>The radius in cells, for drawing the target ring around the middle of the cast cell.</summary>
        public float RadiusCells => radius / (float)BattleSim.CellUnits;

        /// <summary>Ticks from one application to the next.</summary>
        public int PulseTicks => Math.Max(1, durationTicks / pulses);

        /// <summary>
        /// How long the spell goes on at a level, in ticks: the frozen time of a freeze, the length of a boost, and
        /// for damage and heal the time its pulses are spread over.
        /// </summary>
        public int ActiveTicks(int level) =>
            BattleSim.EffectNumber(effect) == BattleSim.EffectFreeze ? (int)(durationTicks * PowerAt(level) / 100) : durationTicks;

        /// <summary>The cast the sim takes: this spell at a level, on a cell. The server works out the same numbers.</summary>
        public BattleSpellInput ToInput(int level, int x, int z, int tick = 0)
        {
            var kind = BattleSim.EffectNumber(effect);
            return new BattleSpellInput
            {
                id = id,
                effect = effect,
                x = x,
                z = z,
                radius = radius,
                pulses = pulses,
                pulseTicks = PulseTicks,
                power = kind == BattleSim.EffectFreeze ? 0 : PowerAt(level),
                ticks = kind == BattleSim.EffectFreeze ? ActiveTicks(level) : kind == BattleSim.EffectBoost ? durationTicks : 0,
                tick = tick
            };
        }
    }

    /// <summary>A spell a raid may cast once, and at what spell level. The server decides the list (ADR-052).</summary>
    public sealed class RaidSpell
    {
        public string id;
        public int level;
    }

    /// <summary>One cast in a fought battle, as sent to the server next to placements and troop stacks.</summary>
    public sealed class RaidSpellCast
    {
        public string spellId;
        /// <summary>The cell the spell is centred on: any cell of the grid, buildings included.</summary>
        public int x;
        public int z;
        /// <summary>The battle tick it was cast on; it lands after this tick, like a dropped card.</summary>
        public int tick;
    }

    public sealed class SpellCatalog
    {
        public const int SupportedSchemaVersion = 1;

        private sealed class Document
        {
            public int schemaVersion;
            public List<SpellDefinition> spells = new List<SpellDefinition>();
        }

        public IReadOnlyList<SpellDefinition> All { get; private set; }

        public SpellDefinition Find(string id) => All.FirstOrDefault(s => s.id == id);

        public static SpellCatalog FromJson(string json)
        {
            var document = JsonConvert.DeserializeObject<Document>(json);
            if (document == null || document.schemaVersion != SupportedSchemaVersion || document.spells == null)
                throw new FormatException("Unsupported spell file.");
            var seen = new HashSet<string>();
            foreach (var spell in document.spells)
            {
                if (spell == null || string.IsNullOrEmpty(spell.id) || !seen.Add(spell.id))
                    throw new FormatException("A spell has no id, or its id is used twice.");
                var kind = BattleSim.EffectNumber(spell.effect);
                var once = kind == BattleSim.EffectFreeze || kind == BattleSim.EffectBoost;
                if (spell.unlockHutLevel < 1 || spell.radius < 1 || spell.durationTicks < 1 || spell.pulses < 1 || spell.pulses > spell.durationTicks
                    || (once && spell.pulses != 1) || spell.power == null || spell.power.Length < 1 || spell.power.Any(p => p < 1)
                    || (kind == BattleSim.EffectFreeze && spell.power.Any(p => spell.durationTicks * p / 100 < 1)))
                    throw new FormatException($"Spell {spell.id} has invalid values.");
            }
            return new SpellCatalog { All = document.spells };
        }

        /// <summary>The spells a Research Hut of this level opens, in file order, each at its level. Mirrors the server.</summary>
        public List<RaidSpell> Available(int hutLevel) =>
            All.Where(s => s.LevelAt(hutLevel) > 0).Select(s => new RaidSpell { id = s.id, level = s.LevelAt(hutLevel) }).ToList();

        /// <summary>
        /// The spells this village's raids may cast. Online the server's list in RaidSession.spells is the one that
        /// counts; this is for the offline practice path.
        /// </summary>
        public List<RaidSpell> Available(Village village) => Available(TroopResearch.HutLevel(village));

        /// <summary>
        /// Empty when the casts are legal, otherwise the reason. The server uses the same names, checked in the same
        /// order. Only a timed battle (one with an end tick) may have casts. Each must be a spell of the session, cast
        /// once, on a cell of the grid, at a tick from 0 to before the end tick, and the ticks must not go down.
        /// </summary>
        public static string CastError(IList<RaidSpell> available, IList<RaidSpellCast> casts, int endTick)
        {
            if (casts == null || casts.Count == 0) return "";
            if (endTick <= 0 || endTick > BattleSim.TickLimit) return "BAD_DEPLOY_TICK";
            var last = 0;
            for (var i = 0; i < casts.Count; i++)
            {
                var cast = casts[i];
                if (available == null || !available.Any(s => s.id == cast.spellId)) return "SPELL_LOCKED";
                for (var j = 0; j < i; j++)
                    if (casts[j].spellId == cast.spellId) return "SPELL_USED";
                if (cast.x < 0 || cast.x >= VillageGrid.Size || cast.z < 0 || cast.z >= VillageGrid.Size) return "BAD_SPELL_CELL";
                if (cast.tick < 0 || cast.tick >= endTick) return "BAD_DEPLOY_TICK";
                if (cast.tick < last) return "DEPLOY_TICK_ORDER";
                last = cast.tick;
            }
            return "";
        }

        /// <summary>One cast as the sim takes it, at the level the session gives the spell. Check CastError first.</summary>
        public BattleSpellInput ToInput(IList<RaidSpell> available, RaidSpellCast cast)
        {
            var spell = Find(cast.spellId);
            var granted = available?.FirstOrDefault(s => s.id == cast.spellId);
            if (spell == null || granted == null) throw new FormatException("Unknown spell " + cast.spellId);
            return spell.ToInput(granted.level, cast.x, cast.z, cast.tick);
        }

        /// <summary>The casts as the sim takes them, in the order sent, for BattleSim.Run.</summary>
        public List<BattleSpellInput> ToInputs(IList<RaidSpell> available, IEnumerable<RaidSpellCast> casts) =>
            (casts ?? Enumerable.Empty<RaidSpellCast>()).Select(c => ToInput(available, c)).ToList();
    }
}

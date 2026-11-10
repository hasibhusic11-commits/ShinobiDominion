using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ShinobiDominion.Core
{
    public sealed class RaidSummary
    {
        public string id;
        public string displayName;
        public string description;
        public long goldPerStar;
        public int bestStars;
        /// <summary>Mana paid per star, like gold (owner, 2026-11-09: loot is gold and Mana).</summary>
        public long manaPerStar;
        /// <summary>A rival raid offers a share of what the rival has (ADR-049): the most it can pay, at 100% damage.</summary>
        public long availableGold;
        public long availableMana;
    }

    public sealed class RaidTeamMember
    {
        public string instanceId;
        public string archetypeId;
        public int evolutionIndex;
        public int level;
        public string role;
        public long hp;
        public long attack;
        public long defense;
        public int speed;
        public string special;
        public int power;
    }

    /// <summary>A hero guarding the target, with the stats fixed when the raid started.</summary>
    public sealed class RaidDefender
    {
        public string id;
        public string archetypeId;
        public int evolutionIndex;
        public int level;
        public string role;
        public long hp;
        public long attack;
        public long defense;
        public int x;
        public int z;
        public string special;
        public int power;
    }

    /// <summary>One raid on this player's village. It never says who attacked.</summary>
    public sealed class DefenseRecord
    {
        public string attackId;
        /// <summary>The attacker's game name; never their account id.</summary>
        public string attackerName;
        public long goldLost;
        public long manaLost;
        public bool canRevenge;
        public int stars;
        public int damagePercent;
        public int defendersAlive;
        public int defendersTotal;
        public long atMs;

        public static List<DefenseRecord> ParseList(string json)
        {
            var token = ServerState.ParseObject(json);
            return token["defenses"] is JArray list ? list.ToObject<List<DefenseRecord>>() : new List<DefenseRecord>();
        }
    }

    public sealed class RaidPlacement
    {
        public string instanceId;
        public int x;
        public int z;
        /// <summary>When the card was dropped in a live battle (ADR-050); 0 is before the first tick.</summary>
        public int tick;
    }

    /// <summary>A raid being prepared: the defender and team are fixed at this moment.</summary>
    public sealed class RaidSession
    {
        public string attackId;
        public RaidSummary raid;
        public List<BattleBuildingInput> buildings = new List<BattleBuildingInput>();
        public List<RaidTeamMember> team = new List<RaidTeamMember>();
        public List<RaidDefender> defenders = new List<RaidDefender>();
        /// <summary>
        /// The spells this raid may cast, each once, and their levels (ADR-052). The server decides it from the
        /// attacker's Research Hut. Empty from a server that has no spells.
        /// </summary>
        public List<RaidSpell> spells = new List<RaidSpell>();

        /// <summary>Empty when these casts are legal for this raid. See SpellCatalog.CastError.</summary>
        public string CastError(IList<RaidSpellCast> casts, int endTick) => SpellCatalog.CastError(spells, casts, endTick);

        /// <summary>The casts as the sim takes them, in the order sent. The server works out the same numbers.</summary>
        public List<BattleSpellInput> ToCasts(IEnumerable<RaidSpellCast> casts, SpellCatalog catalog) => catalog.ToInputs(spells, casts);

        public List<BattleDefenderInput> ToDefenderInputs() =>
            defenders.Select(d => new BattleDefenderInput { id = d.id, role = d.role, hp = d.hp, attack = d.attack, defense = d.defense, x = d.x, z = d.z, special = d.special, evolution = d.evolutionIndex, power = d.power }).ToList();

        /// <summary>
        /// Everything that lands, as sent: heroes, then each stack of troops, each with its drop tick. The server
        /// builds its list the same way; a timed battle then numbers units by BattleSim.LandingOrder.
        /// </summary>
        public List<BattleHeroInput> ToUnits(IEnumerable<RaidPlacement> placements, IEnumerable<RaidTroopStack> stacks, TroopCatalog troops, TroopResearch research = null)
        {
            var units = ToHeroes(placements);
            foreach (var stack in stacks)
            {
                var troop = troops.Find(stack.troopId);
                // Research makes landed troops stronger (ADR-041), the same way the server builds them.
                var (hp, attack, defense) = TroopResearch.Boosted(troops, troop, research?.LevelOf(troop.id) ?? 0);
                for (var i = 0; i < stack.count; i++)
                    units.Add(new BattleHeroInput { id = troop.id, role = troop.role, hp = hp, attack = attack, defense = defense, x = stack.x, z = stack.z, isTroop = true, deployTick = stack.tick });
            }
            return units;
        }

        public static RaidSession Parse(string json) => ServerState.ParseObject(json).ToObject<RaidSession>();

        public List<BattleHeroInput> ToHeroes(IEnumerable<RaidPlacement> placements) =>
            placements.Select(placement =>
            {
                var member = team.First(m => m.instanceId == placement.instanceId);
                return new BattleHeroInput { id = member.instanceId, role = member.role, hp = member.hp, attack = member.attack, defense = member.defense, x = placement.x, z = placement.z, special = member.special, evolution = member.evolutionIndex, power = member.power, deployTick = placement.tick };
            }).ToList();
    }

    /// <summary>What the server decided. The client only plays it back.</summary>
    public sealed class RaidOutcome
    {
        public BattleResult Result;
        public long GoldEarned;
        public long ManaEarned;
        public int BestStars;
        public long XpEarned;
        public int LevelsGained;
        public long CrystalsEarned;
        public string ItemEarned;
        public ServerState State;

        public static RaidOutcome Parse(string json)
        {
            var token = ServerState.ParseObject(json);
            if (!(token["result"] is JObject result)) throw new FormatException("Server sent a raid with no result.");
            var outcome = new RaidOutcome
            {
                Result = result.ToObject<BattleResult>(),
                GoldEarned = token.Value<long>("goldEarned"),
                ManaEarned = token.Value<long?>("manaEarned") ?? 0,
                BestStars = token.Value<int>("bestStars"),
                XpEarned = token.Value<long?>("xpEarned") ?? 0,
                LevelsGained = token.Value<int?>("levelsGained") ?? 0,
                CrystalsEarned = token.Value<long?>("crystalsEarned") ?? 0,
                ItemEarned = token.Value<string>("itemEarned") ?? ""
            };
            if (outcome.XpEarned < 0 || outcome.CrystalsEarned < 0) throw new FormatException("Server sent a negative reward.");
            if (outcome.GoldEarned < 0 || outcome.ManaEarned < 0) throw new FormatException("Server sent a negative reward.");
            if (token["state"] is JObject state) outcome.State = ServerState.FromToken(state);
            return outcome;
        }
    }

    public static class RaidListing
    {
        public static List<RaidSummary> Parse(string json)
        {
            var token = ServerState.ParseObject(json);
            return token["raids"] is JArray raids ? raids.ToObject<List<RaidSummary>>() : new List<RaidSummary>();
        }
    }
}

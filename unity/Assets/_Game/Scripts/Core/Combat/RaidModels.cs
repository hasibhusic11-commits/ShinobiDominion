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
    }

    /// <summary>One raid on this player's village. It never says who attacked.</summary>
    public sealed class DefenseRecord
    {
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
    }

    /// <summary>A raid being prepared: the defender and team are fixed at this moment.</summary>
    public sealed class RaidSession
    {
        public string attackId;
        public RaidSummary raid;
        public List<BattleBuildingInput> buildings = new List<BattleBuildingInput>();
        public List<RaidTeamMember> team = new List<RaidTeamMember>();
        public List<RaidDefender> defenders = new List<RaidDefender>();

        public List<BattleDefenderInput> ToDefenderInputs() =>
            defenders.Select(d => new BattleDefenderInput { id = d.id, role = d.role, hp = d.hp, attack = d.attack, defense = d.defense, x = d.x, z = d.z }).ToList();

        /// <summary>
        /// Everything that lands, in battle order: heroes, then each stack of troops. The server builds
        /// its list the same way, so the log's unit numbers mean the same thing on both sides.
        /// </summary>
        public List<BattleHeroInput> ToUnits(IEnumerable<RaidPlacement> placements, IEnumerable<RaidTroopStack> stacks, TroopCatalog troops)
        {
            var units = ToHeroes(placements);
            foreach (var stack in stacks)
            {
                var troop = troops.Find(stack.troopId);
                for (var i = 0; i < stack.count; i++)
                    units.Add(new BattleHeroInput { id = troop.id, role = troop.role, hp = troop.hp, attack = troop.attack, defense = troop.defense, x = stack.x, z = stack.z, isTroop = true });
            }
            return units;
        }

        public static RaidSession Parse(string json) => ServerState.ParseObject(json).ToObject<RaidSession>();

        public List<BattleHeroInput> ToHeroes(IEnumerable<RaidPlacement> placements) =>
            placements.Select(placement =>
            {
                var member = team.First(m => m.instanceId == placement.instanceId);
                return new BattleHeroInput { id = member.instanceId, role = member.role, hp = member.hp, attack = member.attack, defense = member.defense, x = placement.x, z = placement.z };
            }).ToList();
    }

    /// <summary>What the server decided. The client only plays it back.</summary>
    public sealed class RaidOutcome
    {
        public BattleResult Result;
        public long GoldEarned;
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
                BestStars = token.Value<int>("bestStars"),
                XpEarned = token.Value<long?>("xpEarned") ?? 0,
                LevelsGained = token.Value<int?>("levelsGained") ?? 0,
                CrystalsEarned = token.Value<long?>("crystalsEarned") ?? 0,
                ItemEarned = token.Value<string>("itemEarned") ?? ""
            };
            if (outcome.XpEarned < 0 || outcome.CrystalsEarned < 0) throw new FormatException("Server sent a negative reward.");
            if (outcome.GoldEarned < 0) throw new FormatException("Server sent a negative reward.");
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

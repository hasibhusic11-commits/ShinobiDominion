using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ShinobiDominion.Core
{
    /// <summary>A snapshot of what the server says the player owns. The client only ever displays it.</summary>
    public sealed class ServerState
    {
        public long Crystals;
        public string RosterJson;
        public Dictionary<int, string> HeroBySlot = new Dictionary<int, string>();

        public static ServerState Parse(string json) => FromToken(ParseObject(json));

        internal static ServerState FromToken(JObject token)
        {
            var state = new ServerState
            {
                Crystals = token.Value<long>("crystals"),
                RosterJson = token.ToString(Formatting.None)
            };
            if (state.Crystals < 0) throw new FormatException("Server returned negative crystals.");
            if (token["heroes"] is JArray heroes)
            {
                foreach (var hero in heroes)
                {
                    var slot = hero["baseSlot"];
                    if (slot != null && slot.Type == JTokenType.Integer)
                        state.HeroBySlot[slot.Value<int>()] = hero.Value<string>("instanceId");
                }
            }
            return state;
        }

        internal static JObject ParseObject(string json)
        {
            try
            {
                return JObject.Parse(json);
            }
            catch (JsonException exception)
            {
                throw new FormatException("Server sent invalid data: " + exception.Message);
            }
        }
    }

    public sealed class ServerOddsItem
    {
        public string archetypeId;
        public string displayName;
        public string rarity;
        public long weight;
        public string chance;
    }

    /// <summary>The drop table exactly as the server reports it. The game shows these values unchanged.</summary>
    public sealed class ServerOdds
    {
        public int poolVersion;
        public string status;
        public string bannerName;
        public long priceCrystalsPerRoll;
        public long totalWeight;
        public List<ServerOddsItem> items = new List<ServerOddsItem>();
        public Dictionary<string, string> rarityChance = new Dictionary<string, string>();

        public static ServerOdds Parse(string json) =>
            JsonConvert.DeserializeObject<ServerOdds>(json) ?? throw new FormatException("Empty odds.");
    }

    public sealed class ServerRoll
    {
        public int rollIndex;
        public string instanceId;
        public string archetypeId;
        public string rarity;
        public int copiesOwned;
    }

    public sealed class ServerSummonResult
    {
        public int PoolVersion;
        public List<ServerRoll> Rolls = new List<ServerRoll>();
        public ServerState State;

        public static ServerSummonResult Parse(string json)
        {
            var token = ServerState.ParseObject(json);
            if (!(token["state"] is JObject state)) throw new FormatException("Summon result has no state.");
            return new ServerSummonResult
            {
                PoolVersion = token.Value<int>("poolVersion"),
                Rolls = token["rolls"]?.ToObject<List<ServerRoll>>() ?? new List<ServerRoll>(),
                State = ServerState.FromToken(state)
            };
        }
    }

    public static class ServerErrorText
    {
        /// <summary>Turns a server error code into words for the player.</summary>
        public static string Describe(string code)
        {
            switch (code)
            {
                case "INSUFFICIENT_FUNDS": return "Not enough crystals";
                case "POOL_VERSION_MISMATCH": return "The rates have changed. Please check them again.";
                case "POOL_NOT_ACTIVE": return "This gate is closed right now";
                case "RATE_LIMITED": return "Too many requests. Please wait a moment.";
                case "HERO_NOT_FOUND": return "Hero not found";
                case "DUPLICATE_NOT_FOUND": return "That copy no longer exists";
                case "NOT_EVOLVABLE": return "Only Legendary heroes evolve";
                case "AT_MAX_EVOLUTION": return "Already in final form";
                case "LEVEL_TOO_LOW": return "Must be level 200 in this form";
                case "SAME_INSTANCE": return "A hero cannot be its own duplicate";
                case "WRONG_ARCHETYPE": return "Duplicate must be the exact same hero";
                case "DUPLICATE_LOCKED": return "That copy is locked";
                case "DUPLICATE_ASSIGNED": return "That copy is on a Hero Base";
                case "MISSING_CATALYST": return "Missing the required catalyst";
                case "HERO_ALREADY_ASSIGNED": return "That hero is already on another base";
                case "ALL_BASES_FULL": return "All six bases already have heroes";
                default: return "Could not reach the server. Your request is safe to retry.";
            }
        }
    }
}

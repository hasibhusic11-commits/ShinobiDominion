using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ShinobiDominion.Core
{
    /// <summary>
    /// A battle the server fought in one call: an Arena duel or a Village Siege (ADR-039). Everything here
    /// comes from the server; the client only plays the log back and shows the result.
    /// </summary>
    public sealed class ModeFight
    {
        public string Mode;
        public int Level;
        public string Title;
        public string Description;
        public RaidSession Session;
        public List<RaidPlacement> Placements = new List<RaidPlacement>();
        public List<RaidTroopStack> Troops = new List<RaidTroopStack>();
        public BattleResult Result;
        public bool Won;
        public int Stars;
        public int BestStars;
        public long GoldEarned;
        public long ManaEarned;
        public long CrystalsEarned;
        public int PointsDelta;
        public int Points;
        public int Rank;
        public string OpponentName;
        public int OpponentPoints;
        public ServerState State;
        /// <summary>How strong the Siege attackers are, in percent of a trained troop. 100 for the Arena.</summary>
        public long TroopPercent = 100;

        public bool IsArena => Mode == "arena";
        public bool IsSiege => Mode == "siege";
        public bool IsTrial => Mode == "trial";

        /// <summary>Everyone who attacked, in battle order, exactly as the server built them. Used to check a fight.</summary>
        public List<BattleHeroInput> ToUnits(TroopCatalog troops)
        {
            var units = Session.ToHeroes(Placements);
            foreach (var stack in Troops)
            {
                var troop = troops.Find(stack.troopId);
                for (var i = 0; i < stack.count; i++)
                    units.Add(new BattleHeroInput { id = troop.id, role = troop.role, hp = Scaled(troop.hp), attack = Scaled(troop.attack), defense = Scaled(troop.defense), x = stack.x, z = stack.z, isTroop = true });
            }
            return units;
        }

        private long Scaled(long value)
        {
            var result = value * TroopPercent / 100;
            return result < 1 && value > 0 ? 1 : result;
        }

        public static ModeFight Parse(string json)
        {
            var token = ServerState.ParseObject(json);
            if (!(token["result"] is JObject result)) throw new FormatException("Server sent a battle with no result.");
            var fight = new ModeFight
            {
                Mode = token.Value<string>("mode") ?? "",
                Level = token.Value<int?>("level") ?? 0,
                Title = token.Value<string>("title") ?? "",
                Description = token.Value<string>("description") ?? "",
                Result = result.ToObject<BattleResult>(),
                Won = token.Value<bool?>("won") ?? false,
                Stars = token.Value<int?>("stars") ?? 0,
                BestStars = token.Value<int?>("bestStars") ?? 0,
                GoldEarned = token.Value<long?>("goldEarned") ?? 0,
                ManaEarned = token.Value<long?>("manaEarned") ?? 0,
                CrystalsEarned = token.Value<long?>("crystalsEarned") ?? 0,
                PointsDelta = token.Value<int?>("pointsDelta") ?? 0,
                Points = token.Value<int?>("points") ?? 0,
                Rank = token.Value<int?>("rank") ?? 0,
                OpponentName = token.Value<string>("opponentName") ?? "",
                OpponentPoints = token.Value<int?>("opponentPoints") ?? 0,
                TroopPercent = token.Value<long?>("troopPercent") ?? 100
            };
            if (fight.TroopPercent < 1) throw new FormatException("Server sent impossible attackers.");
            if (fight.Mode != "arena" && fight.Mode != "siege" && fight.Mode != "trial") throw new FormatException("Server sent an unknown mode.");
            if (fight.GoldEarned < 0 || fight.ManaEarned < 0 || fight.CrystalsEarned < 0) throw new FormatException("Server sent a negative reward.");
            if (fight.Stars < 0 || fight.Stars > 3) throw new FormatException("Server sent impossible stars.");
            fight.Session = new RaidSession
            {
                attackId = "mode",
                raid = new RaidSummary { id = fight.Mode, displayName = fight.Title, description = fight.Description },
                buildings = token["buildings"]?.ToObject<List<BattleBuildingInput>>() ?? new List<BattleBuildingInput>(),
                team = token["team"]?.ToObject<List<RaidTeamMember>>() ?? new List<RaidTeamMember>(),
                defenders = token["defenders"]?.ToObject<List<RaidDefender>>() ?? new List<RaidDefender>()
            };
            fight.Placements = token["placements"]?.ToObject<List<RaidPlacement>>() ?? new List<RaidPlacement>();
            fight.Troops = token["troops"]?.ToObject<List<RaidTroopStack>>() ?? new List<RaidTroopStack>();
            if (token["state"] is JObject state) fight.State = ServerState.FromToken(state);
            return fight;
        }
    }

    public sealed class SiegeSummary
    {
        public int level;
        public string name;
        public int attackers;
        public long goldPerStar;
        public long manaPerStar;
        public long firstClearCrystals;
        public int bestStars;
        public bool open;

        public static List<SiegeSummary> ParseList(string json)
        {
            var token = ServerState.ParseObject(json);
            return token["levels"] is JArray levels ? levels.ToObject<List<SiegeSummary>>() : new List<SiegeSummary>();
        }
    }

    /// <summary>A floor of the Trial Tower (ADR-045).</summary>
    public sealed class TrialFloor
    {
        public int floor;
        public string archetypeId;
        public int evolution;
        public int level;
        public long shrineHp;
        public long crystals;
        public long mana;
        public bool cleared;
        public bool open;

        public static List<TrialFloor> ParseList(string json)
        {
            var token = ServerState.ParseObject(json);
            return token["floors"] is JArray floors ? floors.ToObject<List<TrialFloor>>() : new List<TrialFloor>();
        }
    }

    public sealed class ArenaEntry
    {
        public int rank;
        public string name;
        public int points;
        public string archetypeId;
        public int level;
        public int evolution;
        public bool isMe;
    }

    public sealed class ArenaBoard
    {
        public int points;
        public int wins;
        public int losses;
        public int rank;
        public int paidWinsLeft;
        public long winGold;
        public long winMana;
        public List<ArenaEntry> top = new List<ArenaEntry>();

        public static ArenaBoard Parse(string json) => ServerState.ParseObject(json).ToObject<ArenaBoard>() ?? new ArenaBoard();
    }

    /// <summary>A fixed swap of gems for gold or Mana (ADR-043). Nothing random.</summary>
    public sealed class ShopOffer
    {
        public string id;
        public string name;
        public long crystals;
        public long gold;
        public long mana;
    }

    /// <summary>The free Treasure Chest and the shop, as the server sends them.</summary>
    public sealed class ChestView
    {
        public long readyAtMs;
        public long nowMs;
        public long gold;
        public long mana;
        public List<ShopOffer> offers = new List<ShopOffer>();
        public ServerState State;

        public bool Ready => readyAtMs <= nowMs;

        public static ChestView Parse(string json)
        {
            var token = ServerState.ParseObject(json);
            var view = token.ToObject<ChestView>() ?? new ChestView();
            if (view.gold < 0 || view.mana < 0) throw new FormatException("Server sent a negative chest.");
            view.offers = view.offers ?? new List<ShopOffer>();
            if (token["state"] is JObject state) view.State = ServerState.FromToken(state);
            return view;
        }
    }

    public sealed class QuestStatus
    {
        public string id;
        public string title;
        public int target;
        public int progress;
        public long crystals;
        public bool claimed;

        public bool Done => progress >= target;
    }

    public sealed class QuestBoard
    {
        public List<QuestStatus> Quests = new List<QuestStatus>();
        public bool GiftReady;
        public long GiftCrystals;
        public long GiftGold;
        public long GiftMana;
        public ServerState State;

        public static QuestBoard Parse(string json)
        {
            var token = ServerState.ParseObject(json);
            var board = new QuestBoard
            {
                Quests = token["quests"]?.ToObject<List<QuestStatus>>() ?? new List<QuestStatus>(),
                GiftReady = token.Value<bool?>("giftReady") ?? false,
                GiftCrystals = token.Value<long?>("giftCrystals") ?? 0,
                GiftGold = token.Value<long?>("giftGold") ?? 0,
                GiftMana = token.Value<long?>("giftMana") ?? 0
            };
            if (token["state"] is JObject state) board.State = ServerState.FromToken(state);
            return board;
        }
    }
}

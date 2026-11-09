using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using Nakama;
using Newtonsoft.Json;
using ShinobiDominion.Core;
using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>What the game asks of the backend. Results come only from the server.</summary>
    public interface IGameServer
    {
        Task<ServerState> ConnectAsync();
        Task<ServerState> GetStateAsync();
        Task<ServerOdds> GetOddsAsync();
        Task<ServerSummonResult> SummonAsync(int poolVersion, int count, string key);
        Task<ServerState> EvolveAsync(string key, string mainId, string duplicateId);
        Task<ServerState> SetLockAsync(string instanceId, bool locked);
        Task<ServerState> AssignAsync(int slot, string instanceId);
        Task<ServerState> DevMaxLevelAsync(string instanceId);
        Task<ServerState> DevResetVillageAsync();
        Task<ServerState> PlaceAsync(string key, string definitionId, int x, int z, int rotation);
        Task<ServerState> PlaceWallsAsync(string key, int x1, int z1, int x2, int z2);
        Task<ServerState> MoveAsync(string key, int id, int x, int z, int rotation);
        Task<ServerState> RemoveAsync(string key, int id);
        Task<ServerState> SwitchAsync(string key, int id, string to);
        Task<ServerState> UpgradeAsync(string key, int id, bool group);
        Task<ServerState> CollectAsync(string key, int id);
        Task<List<RaidSummary>> RaidListAsync();
        Task<RaidSession> RaidStartAsync(string key, string raidId);
        Task<RaidSession> RivalStartAsync(string key);
        Task<RaidSession> RevengeAsync(string key, string attackId);
        Task<List<DefenseRecord>> DefenseLogAsync();
        Task<RaidOutcome> RaidDeployAsync(string key, string attackId, IList<RaidPlacement> placements, IList<RaidTroopStack> troops);
        Task<ServerState> TrainTroopsAsync(string key, string troopId, int count);
        Task<ServerState> TrainHeroAsync(string key, string instanceId);
    }

    /// <summary>The game modes beyond raids (ADR-039). Kept apart so test servers need not implement them.</summary>
    public interface IModeServer
    {
        Task<List<SiegeSummary>> SiegeListAsync();
        Task<ModeFight> SiegeFightAsync(string key, int level);
        Task<ModeFight> ArenaFightAsync(string key);
        Task<ArenaBoard> ArenaBoardAsync();
        Task<QuestBoard> QuestBoardAsync();
        Task<QuestBoard> QuestClaimAsync(string key, string questId);
        Task<QuestBoard> GiftOpenAsync(string key);
        Task<ServerState> FinishUpgradeAsync(string key, int id);
        Task<ServerState> ResearchStartAsync(string key, string troopId);
        Task<ServerState> ResearchFinishAsync(string key, string troopId);
        Task<string> SetNameAsync(string name);
        Task<GuildView> GuildViewAsync();
        Task<GuildView> GuildCreateAsync(string key, string name, string description);
        Task<GuildView> GuildJoinAsync(string key, string guildId);
        Task<GuildView> GuildLeaveAsync(string key);
        Task<GuildView> GuildChatAsync(string key, string text);
        Task<ChestView> ChestViewAsync();
        Task<ChestView> ChestOpenAsync(string key);
        Task<ChestView> ShopBuyAsync(string key, string offerId);
        Task<ServerState> GearUpgradeAsync(string key, string instanceId, int slot);
        Task<List<TrialFloor>> TrialListAsync();
        Task<ModeFight> TrialFightAsync(string key, int floor);
    }

    public sealed class GameServerException : Exception
    {
        public readonly string Code;
        public GameServerException(string code) : base(code)
        {
            Code = code;
        }
    }

    [Serializable]
    public sealed class ServerConfig
    {
        public string scheme = "http";
        public string host = "127.0.0.1";
        public int port = 7350;
        public string serverKey;

        /// <summary>Written locally by backend/write_client_config.ps1 and gitignored. Null means no server.</summary>
        public static ServerConfig Load()
        {
            var asset = Resources.Load<TextAsset>("Config/server.local");
            if (asset == null) return null;
            var config = JsonUtility.FromJson<ServerConfig>(asset.text);
            return string.IsNullOrEmpty(config?.serverKey) ? null : config;
        }
    }

    public sealed class NakamaGameServer : IGameServer, IModeServer
    {
        private const string DeviceIdKey = "sd_device_id";

        private readonly IClient _client;
        private readonly string _deviceId;
        private ISession _session;

        public NakamaGameServer(ServerConfig config, string deviceId = null)
        {
            _client = new Client(config.scheme, config.host, config.port, config.serverKey, UnityWebRequestAdapter.Instance)
            {
                Timeout = 10
            };
            _deviceId = deviceId ?? LoadDeviceId();
        }

        /// <summary>A random id kept on this device, rather than any real hardware identifier.</summary>
        private static string LoadDeviceId()
        {
            var id = PlayerPrefs.GetString(DeviceIdKey, null);
            if (string.IsNullOrEmpty(id))
            {
                id = "sd-" + Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(DeviceIdKey, id);
                PlayerPrefs.Save();
            }
            return id;
        }

        private async Task EnsureSessionAsync()
        {
            if (_session != null && !_session.HasExpired(DateTime.UtcNow.AddMinutes(1))) return;
            _session = await _client.AuthenticateDeviceAsync(_deviceId);
        }

        private async Task<string> CallAsync(string rpc, object payload)
        {
            try
            {
                await EnsureSessionAsync();
                var response = await _client.RpcAsync(_session, rpc, JsonConvert.SerializeObject(payload ?? new { }));
                return response.Payload;
            }
            catch (ApiResponseException exception)
            {
                throw new GameServerException(ExtractCode(exception.Message));
            }
            catch (Exception exception) when (!(exception is GameServerException))
            {
                Debug.LogWarning($"Server call {rpc} failed: {exception.Message}");
                throw new GameServerException("UNREACHABLE");
            }
        }

        private static string ExtractCode(string message)
        {
            if (string.IsNullOrEmpty(message)) return "UNKNOWN";
            var trimmed = message.Trim();
            return trimmed.IndexOf(' ') < 0 ? trimmed : "UNKNOWN";
        }

        public Task<ServerState> ConnectAsync() => GetStateAsync();

        public async Task<ServerState> GetStateAsync() => ServerState.Parse(await CallAsync("sd_get_state", null));

        public async Task<List<RaidSummary>> RaidListAsync() => RaidListing.Parse(await CallAsync("sd_raid_list", null));

        public async Task<RaidSession> RaidStartAsync(string key, string raidId) =>
            RaidSession.Parse(await CallAsync("sd_raid_start", new { key, raidId }));

        public async Task<RaidSession> RevengeAsync(string key, string attackId) =>
            RaidSession.Parse(await CallAsync("sd_raid_revenge", new { key, attackId }));

        public async Task<RaidSession> RivalStartAsync(string key) =>
            RaidSession.Parse(await CallAsync("sd_rival_start", new { key }));

        public async Task<List<DefenseRecord>> DefenseLogAsync() => DefenseRecord.ParseList(await CallAsync("sd_defense_log", null));

        public async Task<RaidOutcome> RaidDeployAsync(string key, string attackId, IList<RaidPlacement> placements, IList<RaidTroopStack> troops) =>
            RaidOutcome.Parse(await CallAsync("sd_raid_deploy", new { key, attackId, placements, troops }));

        public async Task<List<SiegeSummary>> SiegeListAsync() => SiegeSummary.ParseList(await CallAsync("sd_siege_list", null));

        public async Task<ModeFight> SiegeFightAsync(string key, int level) => ModeFight.Parse(await CallAsync("sd_siege_fight", new { key, level }));

        public async Task<ModeFight> ArenaFightAsync(string key) => ModeFight.Parse(await CallAsync("sd_arena_fight", new { key }));

        public async Task<ArenaBoard> ArenaBoardAsync() => ArenaBoard.Parse(await CallAsync("sd_arena_board", null));

        public async Task<QuestBoard> QuestBoardAsync() => QuestBoard.Parse(await CallAsync("sd_quest_board", null));

        public async Task<QuestBoard> QuestClaimAsync(string key, string questId) => QuestBoard.Parse(await CallAsync("sd_quest_claim", new { key, questId }));

        public async Task<QuestBoard> GiftOpenAsync(string key) => QuestBoard.Parse(await CallAsync("sd_gift_open", new { key }));

        public async Task<ServerState> FinishUpgradeAsync(string key, int id) => ServerState.Parse(await CallAsync("sd_village_finish", new { key, id }));

        public async Task<ServerState> ResearchStartAsync(string key, string troopId) => ServerState.Parse(await CallAsync("sd_research_start", new { key, troopId }));

        public async Task<ServerState> ResearchFinishAsync(string key, string troopId) => ServerState.Parse(await CallAsync("sd_research_finish", new { key, troopId }));

        public async Task<string> SetNameAsync(string name) =>
            Newtonsoft.Json.Linq.JObject.Parse(await CallAsync("sd_set_name", new { name })).Value<string>("name");

        public async Task<GuildView> GuildViewAsync() => GuildView.Parse(await CallAsync("sd_guild_view", null));

        public async Task<GuildView> GuildCreateAsync(string key, string name, string description) => GuildView.Parse(await CallAsync("sd_guild_create", new { key, name, description }));

        public async Task<GuildView> GuildJoinAsync(string key, string guildId) => GuildView.Parse(await CallAsync("sd_guild_join", new { key, guildId }));

        public async Task<GuildView> GuildLeaveAsync(string key) => GuildView.Parse(await CallAsync("sd_guild_leave", new { key }));

        public async Task<GuildView> GuildChatAsync(string key, string text) => GuildView.Parse(await CallAsync("sd_guild_chat", new { key, text }));

        public async Task<ChestView> ChestViewAsync() => ChestView.Parse(await CallAsync("sd_chest_view", null));

        public async Task<ChestView> ChestOpenAsync(string key) => ChestView.Parse(await CallAsync("sd_chest_open", new { key }));

        public async Task<ChestView> ShopBuyAsync(string key, string offerId) => ChestView.Parse(await CallAsync("sd_shop_buy", new { key, offerId }));

        public async Task<ServerState> GearUpgradeAsync(string key, string instanceId, int slot) => ServerState.Parse(await CallAsync("sd_gear_upgrade", new { key, instanceId, slot }));

        public async Task<List<TrialFloor>> TrialListAsync() => TrialFloor.ParseList(await CallAsync("sd_trial_list", null));

        public async Task<ModeFight> TrialFightAsync(string key, int floor) => ModeFight.Parse(await CallAsync("sd_trial_fight", new { key, floor }));

        public async Task<ServerState> TrainTroopsAsync(string key, string troopId, int count) =>
            ServerState.Parse(await CallAsync("sd_troop_train", new { key, troopId, count }));

        public async Task<ServerState> TrainHeroAsync(string key, string instanceId) =>
            ServerState.Parse(await CallAsync("sd_hero_train", new { key, instanceId }));

        public async Task<ServerOdds> GetOddsAsync() => ServerOdds.Parse(await CallAsync("sd_summon_odds", null));

        public async Task<ServerSummonResult> SummonAsync(int poolVersion, int count, string key) =>
            ServerSummonResult.Parse(await CallAsync("sd_summon", new { poolVersion, count, key }));

        public async Task<ServerState> EvolveAsync(string key, string mainId, string duplicateId) =>
            ServerState.Parse(await CallAsync("sd_evolve", new { key, mainInstanceId = mainId, duplicateInstanceId = duplicateId }));

        public async Task<ServerState> SetLockAsync(string instanceId, bool locked) =>
            ServerState.Parse(await CallAsync("sd_set_lock", new { instanceId, locked }));

        public async Task<ServerState> AssignAsync(int slot, string instanceId) =>
            ServerState.Parse(await CallAsync("sd_assign_hero", new { slot, instanceId }));

        public async Task<ServerState> DevMaxLevelAsync(string instanceId) =>
            ServerState.Parse(await CallAsync("sd_dev_grant", new { maxLevelFor = instanceId }));

        public async Task<ServerState> DevResetVillageAsync() =>
            ServerState.Parse(await CallAsync("sd_dev_grant", new { resetVillage = true }));

        public async Task<ServerState> PlaceAsync(string key, string definitionId, int x, int z, int rotation) =>
            ServerState.Parse(await CallAsync("sd_village_place", new { key, definitionId, x, z, rotation }));

        public async Task<ServerState> PlaceWallsAsync(string key, int x1, int z1, int x2, int z2) =>
            ServerState.Parse(await CallAsync("sd_village_walls", new { key, x1, z1, x2, z2 }));

        public async Task<ServerState> MoveAsync(string key, int id, int x, int z, int rotation) =>
            ServerState.Parse(await CallAsync("sd_village_move", new { key, id, x, z, rotation }));

        public async Task<ServerState> SwitchAsync(string key, int id, string to) =>
            ServerState.Parse(await CallAsync("sd_village_switch", new { key, id, to }));

        public async Task<ServerState> RemoveAsync(string key, int id) =>
            ServerState.Parse(await CallAsync("sd_village_remove", new { key, id }));

        public async Task<ServerState> UpgradeAsync(string key, int id, bool group) =>
            ServerState.Parse(await CallAsync("sd_village_upgrade", new { key, id, group }));

        public async Task<ServerState> CollectAsync(string key, int id) =>
            ServerState.Parse(await CallAsync("sd_village_collect", new { key, id }));
    }
}

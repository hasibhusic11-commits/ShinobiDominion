using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ShinobiDominion.Core;
using ShinobiDominion.Game;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShinobiDominion.Tests
{
    /// <summary>
    /// Runs the game as a release build behaves: no offline play. Uses a server that is always
    /// unreachable, because that is the situation a tampering player would create on purpose.
    /// </summary>
    public class ReleaseModeTests
    {
        private sealed class DeadServer : IGameServer
        {
            private static Task<ServerState> Fail() => Task.FromException<ServerState>(new GameServerException("UNREACHABLE"));
            public Task<ServerState> ConnectAsync() => Fail();
            public Task<ServerState> GetStateAsync() => Fail();
            public Task<ServerOdds> GetOddsAsync() => Task.FromException<ServerOdds>(new GameServerException("UNREACHABLE"));
            public Task<ServerSummonResult> SummonAsync(int poolVersion, int count, string key) => Task.FromException<ServerSummonResult>(new GameServerException("UNREACHABLE"));
            public Task<ServerState> EvolveAsync(string key, string mainId, string duplicateId) => Fail();
            public Task<ServerState> SetLockAsync(string instanceId, bool locked) => Fail();
            public Task<ServerState> AssignAsync(int slot, string instanceId) => Fail();
            public Task<ServerState> DevMaxLevelAsync(string instanceId) => Fail();
            public Task<ServerState> DevResetVillageAsync() => Fail();
            public Task<ServerState> PlaceAsync(string key, string definitionId, int x, int z, int rotation) => Fail();
            public Task<ServerState> PlaceWallsAsync(string key, int x1, int z1, int x2, int z2) => Fail();
            public Task<ServerState> MoveAsync(string key, int id, int x, int z, int rotation) => Fail();
            public Task<ServerState> RemoveAsync(string key, int id) => Fail();
            public Task<ServerState> UpgradeAsync(string key, int id, bool group) => Fail();
            public Task<ServerState> CollectAsync(string key, int id) => Fail();
        }

        private string _savePath;
        private GameObject _root;
        private VillageController Controller;
        private bool _previousOffline;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _previousOffline = VillageBootstrap.AllowOfflinePlay;
            VillageBootstrap.AllowOfflinePlay = false;
            VillageBootstrap.ServerOverride = () => new DeadServer();
            _savePath = Path.Combine(Application.temporaryCachePath, "release_village_" + Guid.NewGuid().ToString("N") + ".json");
            VillageBootstrap.SavePathOverride = _savePath;

            // A forged save, sitting exactly where a tampering player would put it.
            var forged = new VillageSave
            {
                schemaVersion = VillageSave.CurrentSchemaVersion,
                mapVersion = VillageSave.CurrentMapVersion,
                nextInstanceId = 100,
                gold = 999999999,
                crystals = 999999999
            };
            forged.buildings.Add(new BuildingSave { id = 50, definitionId = "defense_tower", x = 5, z = 5, level = 1 });
            File.WriteAllText(_savePath, forged.ToJson());
            File.WriteAllText(_savePath + ".heroes", "{\"schemaVersion\":1,\"heroes\":[{\"instanceId\":\"stolen\",\"archetypeId\":\"stone_titan\",\"evolutionIndex\":3,\"level\":200}]}");

            _root = new GameObject("VillageBootstrapUnderTest");
            Controller = _root.AddComponent<VillageBootstrap>().Controller;
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_root);
            VillageBootstrap.AllowOfflinePlay = _previousOffline;
            VillageBootstrap.ServerOverride = null;
            VillageBootstrap.SavePathOverride = null;
            yield return null;
            foreach (var file in new[] { _savePath, _savePath + ".heroes" })
                if (File.Exists(file)) File.Delete(file);
        }

        private BuildingInstance First(string id) => Controller.Village.Buildings.First(b => b.Definition.id == id);

        [Test]
        public void ForgedSavesAreNeverLoaded()
        {
            Assert.Less(Controller.Village.Gold, 999999999);
            Assert.AreEqual(0, Controller.Village.CountOf("defense_tower"));
            Assert.AreEqual(0, Controller.Roster.HeroCount, "Forged heroes must not load");
            Assert.IsNull(Controller.Roster.Find("stolen"));
        }

        [UnityTest]
        public IEnumerator NothingCanChange_WithoutTheServer()
        {
            var gold = Controller.Village.Gold;
            var count = Controller.Village.BuildingCount;
            Assert.IsFalse(Controller.IsOnline);
            Assert.IsFalse(Controller.CanChangeVillage);

            Controller.Select(First("builder_hall").Id);
            yield return null;
            Controller.OpenCatalog();
            Assert.IsFalse(Controller.BeginPlacement("defense_tower"), "Cannot start building");
            Assert.IsNotNull(Controller.LastMessage);
            Controller.CloseAll();

            var mine = First("gold_mine");
            Controller.Select(mine.Id);
            yield return null;
            Assert.IsFalse(Controller.BeginMove(mine.Id), "Cannot move");

            Assert.AreEqual(gold, Controller.Village.Gold);
            Assert.AreEqual(count, Controller.Village.BuildingCount);
        }

        [UnityTest]
        public IEnumerator TappingAMine_WithoutTheServer_PaysNothing()
        {
            var gold = Controller.Village.Gold;
            var mine = First("gold_mine");
            mine.LastCollectedUtcTicks = DateTime.UtcNow.Ticks - TimeSpan.TicksPerHour;
            Controller.CollectOrSelect(mine.Id);
            yield return null;
            Assert.AreEqual(gold, Controller.Village.Gold);
        }

        [UnityTest]
        public IEnumerator NothingIsWrittenToTheDevice()
        {
            var before = File.ReadAllText(_savePath);
            Controller.CloseAll();
            yield return null;
            yield return null;
            Assert.AreEqual(before, File.ReadAllText(_savePath), "A release build must not touch the local file");
        }
    }
}

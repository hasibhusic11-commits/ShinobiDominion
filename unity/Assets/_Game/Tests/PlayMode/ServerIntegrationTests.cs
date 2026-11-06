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
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShinobiDominion.Tests
{
    /// <summary>
    /// Runs the real game against the real local server, through the real Nakama SDK.
    /// Skipped unless SD_SERVER_KEY is set, because it needs the server running in dev mode.
    /// </summary>
    public class ServerIntegrationTests
    {
        private string _savePath;
        private GameObject _root;
        private VillageController Controller;
        private NakamaGameServer _direct;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var key = Environment.GetEnvironmentVariable("SD_SERVER_KEY");
            if (string.IsNullOrEmpty(key)) Assert.Ignore("SD_SERVER_KEY not set, so no server to test against.");

            var config = new ServerConfig { serverKey = key };
            var deviceId = "sd-test-" + Guid.NewGuid().ToString("N");
            _direct = new NakamaGameServer(config, deviceId);
            VillageBootstrap.ServerOverride = () => new NakamaGameServer(config, deviceId);
            _savePath = Path.Combine(Application.temporaryCachePath, "server_village_" + Guid.NewGuid().ToString("N") + ".json");
            VillageBootstrap.SavePathOverride = _savePath;

            _root = new GameObject("VillageBootstrapUnderTest");
            Controller = _root.AddComponent<VillageBootstrap>().Controller;
            yield return WaitFor(() => Controller.ConnectTask.IsCompleted);
            Assert.IsTrue(Controller.IsOnline, "Could not connect to the local server");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_root != null) Object.Destroy(_root);
            VillageBootstrap.ServerOverride = null;
            VillageBootstrap.SavePathOverride = null;
            yield return null;
            if (_savePath == null) yield break;
            foreach (var file in new[] { _savePath, _savePath + ".heroes" })
                if (File.Exists(file)) File.Delete(file);
        }

        private static IEnumerator WaitFor(Func<bool> condition, float seconds = 15f)
        {
            var deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(condition(), "Timed out waiting for the server");
        }

        private BuildingInstance First(string id) => Controller.Village.Buildings.First(b => b.Definition.id == id);
        private Button Find(string name) => _root.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.name == name);

        private IEnumerator ClickWhenReady(string name)
        {
            yield return WaitFor(() => Find(name) != null);
            Find(name).onClick.Invoke();
        }

        [UnityTest]
        public IEnumerator Connecting_ShowsTheServersHeroesAndCrystals()
        {
            var stateTask = _direct.GetStateAsync();
            yield return WaitFor(() => stateTask.IsCompleted);
            Assert.AreEqual(stateTask.Result.Crystals, Controller.ServerCrystals);
            StringAssert.Contains($"Crystals {stateTask.Result.Crystals:N0}", string.Join(" ", _root.GetComponentsInChildren<Text>().Select(t => t.text)));
            Assert.Greater(Controller.Roster.HeroCount, 0, "Dev-mode server seeds heroes");
        }

        [UnityTest]
        public IEnumerator SummoningGate_ShowsTheServersExactOdds()
        {
            Controller.Select(First("summoning_gate").Id);
            yield return WaitFor(() => Find("SummonOneButton") != null);

            var oddsTask = _direct.GetOddsAsync();
            yield return WaitFor(() => oddsTask.IsCompleted);
            var text = string.Join("\n", _root.GetComponentsInChildren<Text>().Select(t => t.text));
            foreach (var item in oddsTask.Result.items)
                StringAssert.Contains($"{item.displayName}  ({item.rarity}):  {item.chance}", text);
            StringAssert.Contains("NOT APPROVED", text);
            StringAssert.DoesNotContain("pity", text.ToLowerInvariant());
        }

        [UnityTest]
        public IEnumerator SummonTen_ThroughTheScreen_ChargesAndAddsExactlyTen()
        {
            var before = Controller.ServerCrystals.Value;
            var heroes = Controller.Roster.HeroCount;

            Controller.Select(First("summoning_gate").Id);
            yield return ClickWhenReady("SummonTenButton");
            yield return WaitFor(() => Find("SummonDoneButton") != null);

            Assert.AreEqual(heroes + 10, Controller.Roster.HeroCount);
            Assert.AreEqual(before - 1000, Controller.ServerCrystals);
            Assert.AreEqual(10, _root.GetComponentsInChildren<Button>(false).Count(b => b.name.StartsWith("Result_")));

            var stateTask = _direct.GetStateAsync();
            yield return WaitFor(() => stateTask.IsCompleted);
            Assert.AreEqual(stateTask.Result.Crystals, Controller.ServerCrystals, "Client and server must agree");
        }

        [UnityTest]
        public IEnumerator DoubleTappingSummon_OnlyChargesOnce()
        {
            var before = Controller.ServerCrystals.Value;
            Controller.Select(First("summoning_gate").Id);
            yield return WaitFor(() => Find("SummonOneButton") != null);
            var button = Find("SummonOneButton");
            button.onClick.Invoke();
            button.onClick.Invoke();
            yield return WaitFor(() => Find("SummonDoneButton") != null);
            yield return new WaitForSeconds(0.5f);

            var stateTask = _direct.GetStateAsync();
            yield return WaitFor(() => stateTask.IsCompleted);
            Assert.AreEqual(before - 100, stateTask.Result.Crystals);
        }

        [UnityTest]
        public IEnumerator Evolution_RunsOnTheServer_AndClientFollows()
        {
            var roster = Controller.Roster;
            var main = roster.Heroes.First(h => h.ArchetypeId == "solar_beast_sage" && h.Level == HeroMath.MaxLevel);
            var duplicate = roster.Heroes.First(h => h.ArchetypeId == "solar_beast_sage" && h.InstanceId != main.InstanceId);
            var mainId = main.InstanceId;
            var duplicateId = duplicate.InstanceId;

            Controller.Select(First("hero_altar").Id);
            yield return null;
            Controller.ShowEvolution(mainId);
            yield return ClickWhenReady("Sacrifice_" + duplicateId);
            yield return ClickWhenReady("ConfirmEvolveButton");
            yield return WaitFor(() => roster.Find(duplicateId) == null);

            Assert.AreEqual(1, roster.Find(mainId).EvolutionIndex);
            Assert.AreEqual(1, roster.Find(mainId).Level);

            var stateTask = _direct.GetStateAsync();
            yield return WaitFor(() => stateTask.IsCompleted);
            StringAssert.DoesNotContain(duplicateId, stateTask.Result.RosterJson, "Server must have consumed it too");
        }

        [UnityTest]
        public IEnumerator Assignment_RoundTripsThroughTheServer_AndSurvivesReconnect()
        {
            var heroBase = First("hero_base");
            var baseId = heroBase.Id;
            var heroId = Controller.Roster.Heroes.First().InstanceId;

            Controller.Select(baseId);
            yield return null;
            Assert.AreEqual(VillageError.None, Controller.AssignHero(baseId, heroId));
            yield return WaitFor(() => Controller.Village.Find(baseId).AssignedHeroInstanceId == heroId);

            var stateTask = _direct.GetStateAsync();
            yield return WaitFor(() => stateTask.IsCompleted);
            Assert.AreEqual(heroId, stateTask.Result.HeroBySlot[baseId]);
        }

        [UnityTest]
        public IEnumerator CaptureSummoningScreens()
        {
            var dir = Environment.GetEnvironmentVariable("SD_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("SD_CAPTURE_DIR not set.");
            Directory.CreateDirectory(dir);

            var camera = _root.GetComponentInChildren<IsoCamera>().Camera;
            var canvas = _root.GetComponentInChildren<Canvas>();
            var texture = new RenderTexture(1600, 900, 24);
            camera.targetTexture = texture;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;

            Controller.Select(First("summoning_gate").Id);
            yield return WaitFor(() => Find("SummonOneButton") != null);
            yield return ScreenCaptureTests.Capture(camera, texture, Path.Combine(dir, "13_summoning_gate.png"));

            Find("SummonTenButton").onClick.Invoke();
            yield return WaitFor(() => Find("SummonDoneButton") != null);
            yield return ScreenCaptureTests.Capture(camera, texture, Path.Combine(dir, "14_summon_results.png"));

            camera.targetTexture = null;
            Object.Destroy(texture);
        }

        [UnityTest]
        public IEnumerator Online_NoHeroFileIsWrittenToTheDevice()
        {
            Controller.Roster.SetLocked(Controller.Roster.Heroes.First().InstanceId, false);
            yield return null;
            yield return null;
            Assert.IsFalse(File.Exists(_savePath + ".heroes"), "Online, heroes must not be saved locally");
        }

        // ---------- The village on the server ----------

        private IEnumerator ServerState(Action<ServerState> use)
        {
            var task = _direct.GetStateAsync();
            yield return WaitFor(() => task.IsCompleted);
            use(task.Result);
        }

        [UnityTest]
        public IEnumerator Village_ComesFromTheServer()
        {
            yield return ServerState(state =>
            {
                Assert.AreEqual(state.Village.gold, Controller.Village.Gold);
                Assert.AreEqual(state.Village.buildings.Count, Controller.Village.BuildingCount);
            });
            StringAssert.Contains($"Gold {Controller.Village.Gold:N0}", string.Join(" ", _root.GetComponentsInChildren<Text>().Select(t => t.text)));
        }

        [UnityTest]
        public IEnumerator PlacingABuilding_IsChargedAndStoredByTheServer()
        {
            var gold = Controller.Village.Gold;
            Controller.Select(First("builder_hall").Id);
            yield return null;
            Assert.IsTrue(Controller.OpenCatalog());
            Assert.IsTrue(Controller.BeginPlacement("defense_tower"));
            Controller.SetGhostOrigin(new GridCoord(40, 40));
            Assert.AreEqual(VillageError.None, Controller.ConfirmPlacementAction());
            yield return WaitFor(() => Controller.Village.CountOf("defense_tower") == 1);

            Assert.AreEqual(gold - 600, Controller.Village.Gold);
            Assert.IsNull(Controller.Toolbar, "Placement closes once the server accepts it");
            yield return ServerState(state =>
            {
                Assert.AreEqual(gold - 600, state.Village.gold);
                Assert.IsTrue(state.Village.buildings.Any(b => b.definitionId == "defense_tower" && b.x == 40 && b.z == 40));
            });
        }

        [UnityTest]
        public IEnumerator WallRun_MoveUpgradeAndRemove_AllGoThroughTheServer()
        {
            Controller.Select(First("builder_hall").Id);
            yield return null;
            Controller.OpenCatalog();
            Controller.BeginPlacement(Village.WallId);
            Controller.SetWallRun(new GridCoord(35, 40), new GridCoord(39, 40));
            Assert.AreEqual(VillageError.None, Controller.ConfirmWallRun());
            yield return WaitFor(() => Controller.Village.CountOf(Village.WallId) == 5);
            Controller.ExitPlacement();
            Controller.CloseAll();

            var heroBase = First("hero_base").Id;
            Controller.Select(heroBase);
            yield return null;
            Assert.IsTrue(Controller.BeginMove(heroBase));
            Controller.SetGhostOrigin(new GridCoord(42, 42));
            Controller.ConfirmMove();
            yield return WaitFor(() => Controller.Village.Find(heroBase).Origin == new GridCoord(42, 42));
            Assert.AreEqual(VillageScreen.Home, Controller.UiState.Screen);

            Controller.Select(heroBase);
            yield return ClickWhenReady("UpgradeButton");
            yield return WaitFor(() => Controller.Village.Find(heroBase).Level == 2);

            yield return ClickWhenReady("RemoveButton");
            yield return WaitFor(() => Controller.Village.Find(heroBase) == null);
            yield return ServerState(state =>
            {
                Assert.IsFalse(state.Village.buildings.Any(b => b.id == heroBase), "The server must have removed it too");
                Assert.AreEqual(5, state.Village.buildings.Count(b => b.definitionId == Village.WallId));
            });
        }

        /// <summary>The whole raid through the real server, whose battle must match this device's rules.</summary>
        [UnityTest]
        public IEnumerator Raid_ThroughTheServer_MatchesTheLocalRules()
        {
            var heroBase = First("hero_base").Id;
            var hero = Controller.Roster.Heroes.First();
            Controller.AssignHero(heroBase, hero.InstanceId);
            yield return WaitFor(() => Controller.Village.Find(heroBase).AssignedHeroInstanceId == hero.InstanceId);

            Controller.OpenAttack();
            yield return ClickWhenReady("ScoutButton");
            yield return WaitFor(() => Controller.CurrentRaidPhase == RaidPhase.Prepare);
            var session = Controller.CurrentRaidSession;
            Assert.AreEqual(1, session.team.Count, "The team comes from the server, from the bases");
            Assert.AreEqual(hero.InstanceId, session.team[0].instanceId);

            Controller.AutoPlaceHeroes();
            var placements = Controller.RaidPlacements.ToList();
            Controller.StartBattle();
            yield return WaitFor(() => Controller.CurrentRaidPhase == RaidPhase.Battle);

            var server = Controller.LastRaidOutcome.Result;
            var local = BattleSim.Run(session.buildings, session.ToHeroes(placements));
            Assert.AreEqual(local.checksum, server.checksum, "Server and client battles must be identical");
            Assert.AreEqual(local.stars, server.stars);

            Controller.SkipBattle();
            Assert.AreEqual(RaidPhase.Resolved, Controller.CurrentRaidPhase);
            var gold = Controller.Village.Gold;
            yield return ServerState(state => Assert.AreEqual(gold, state.Village.gold, "Gold must match the server after a raid"));
            Controller.FinishRaid();
            yield return null;
            Assert.AreEqual(VillageScreen.Home, Controller.UiState.Screen);
        }

        [UnityTest]
        public IEnumerator ServerRefusal_IsShown_AndChangesNothing()
        {
            // The client's copy is made stale on purpose, so only the server can catch the conflict.
            var placeTask = _direct.PlaceAsync("direct-" + Guid.NewGuid().ToString("N"), "defense_tower", 40, 40, 0);
            yield return WaitFor(() => placeTask.IsCompleted);

            Controller.Select(First("builder_hall").Id);
            yield return null;
            Controller.OpenCatalog();
            Controller.BeginPlacement("defense_tower");
            Controller.SetGhostOrigin(new GridCoord(40, 40));
            Assert.AreEqual(VillageError.None, Controller.ConfirmPlacementAction(), "The stale client sees free ground");
            yield return WaitFor(() => Controller.LastMessage == VillageErrorText.Describe(VillageError.Overlap));
            Assert.IsNotNull(Controller.Toolbar, "Stays in placement so the player can adjust");
            yield return ServerState(state => Assert.AreEqual(1, state.Village.buildings.Count(b => b.definitionId == "defense_tower")));
        }

        [UnityTest]
        public IEnumerator TamperedLocalSave_IsIgnored()
        {
            // A forged save with a rich player and a building placed for free.
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

            Object.Destroy(_root);
            yield return null;
            _root = new GameObject("VillageBootstrapUnderTest");
            Controller = _root.AddComponent<VillageBootstrap>().Controller;
            yield return WaitFor(() => Controller.ConnectTask.IsCompleted);

            Assert.IsTrue(Controller.IsOnline);
            Assert.Less(Controller.Village.Gold, 999999999);
            Assert.AreEqual(0, Controller.Village.CountOf("defense_tower"));
            yield return ServerState(state => Assert.Less(state.Village.gold, 999999999));
        }

        [UnityTest]
        public IEnumerator Online_NoVillageFileIsWrittenToTheDevice()
        {
            var mine = First("gold_mine").Id;
            Controller.Select(mine);
            yield return null;
            Controller.BeginMove(mine);
            Controller.SetGhostOrigin(new GridCoord(42, 42));
            Controller.ConfirmMove();
            yield return WaitFor(() => Controller.Village.Find(mine).Origin == new GridCoord(42, 42));
            yield return null;
            yield return null;
            Assert.IsFalse(File.Exists(_savePath), "Online, the village must not be saved locally");
        }
    }
}

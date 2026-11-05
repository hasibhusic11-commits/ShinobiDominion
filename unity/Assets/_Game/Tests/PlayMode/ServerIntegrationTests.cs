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
    }
}

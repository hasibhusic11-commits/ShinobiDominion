using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShinobiDominion.Core;
using ShinobiDominion.Game;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShinobiDominion.Tests
{
    /// <summary>Upgrade timers and builders on screen (ADR-040), offline against a temp save.</summary>
    public class UpgradeTimerTests
    {
        private string _savePath;
        private GameObject _root;
        private VillageBootstrap _game;

        private VillageController Controller => _game.Controller;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _savePath = Path.Combine(Application.temporaryCachePath, "timer_village_" + Guid.NewGuid().ToString("N") + ".json");
            VillageBootstrap.SavePathOverride = _savePath;
            _root = new GameObject("VillageBootstrapUnderTest");
            _game = _root.AddComponent<VillageBootstrap>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_root);
            VillageBootstrap.SavePathOverride = null;
            yield return null;
            if (File.Exists(_savePath)) File.Delete(_savePath);
        }

        private Button Find(string name) => _root.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.name == name);

        private int MineAtLevelTwo()
        {
            var village = Controller.Village;
            var mine = village.Buildings.First(b => b.Definition.id == "gold_mine");
            var save = village.ToSave();
            save.gold = 50000;
            save.crystals = 500;
            save.buildings.Find(b => b.id == mine.Id).level = 2;
            village.Restore(save);
            return mine.Id;
        }

        [UnityTest]
        public IEnumerator TimedUpgrade_ShowsATimer_AndFinishesForGems()
        {
            var id = MineAtLevelTwo();
            var village = Controller.Village;
            Assert.AreEqual(VillageError.None, village.TryUpgrade(id, DateTime.UtcNow.Ticks));
            Assert.IsTrue(village.Find(id).Upgrading);
            Assert.AreEqual(2, village.Find(id).Level, "The level waits for the timer");
            yield return null;
            yield return null;
            var pill = _root.GetComponentsInChildren<RectTransform>(false).FirstOrDefault(r => r.name == "UpgradeTimer" + id);
            Assert.IsNotNull(pill, "A timer floats over the building");
            StringAssert.Contains("s", pill.GetComponentInChildren<Text>().text);

            Controller.Select(id);
            yield return null;
            Assert.IsNotNull(Find("FinishUpgradeButton"), "The action row offers to finish it with gems");
            Assert.IsNull(Find("UpgradeButton"), "No second upgrade while one runs");
            var gems = village.Crystals;
            Find("FinishUpgradeButton").onClick.Invoke();
            yield return null;
            Assert.AreEqual(3, village.Find(id).Level);
            Assert.IsFalse(village.Find(id).Upgrading);
            Assert.AreEqual(gems - 1, village.Crystals, "A one-minute upgrade costs one gem");
            yield return null;
            Assert.IsFalse(_root.GetComponentsInChildren<RectTransform>(false).Any(r => r.name == "UpgradeTimer" + id), "The timer goes away");
        }

        [UnityTest]
        public IEnumerator Upgrades_FinishOnTheirOwn_WhenTheTimeIsUp()
        {
            var id = MineAtLevelTwo();
            var village = Controller.Village;
            // Started a minute ago on this clock, so it is already due.
            Assert.AreEqual(VillageError.None, village.TryUpgrade(id, DateTime.UtcNow.Ticks - TimeSpan.TicksPerMinute * 2));
            Assert.IsTrue(village.Find(id).Upgrading);
            var deadline = Time.realtimeSinceStartup + 3f;
            while (village.Find(id).Upgrading && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(3, village.Find(id).Level);
        }

        [UnityTest]
        public IEnumerator Details_ShowBuildersAndTime()
        {
            var id = MineAtLevelTwo();
            Controller.ShowBuildingDetails(id);
            yield return null;
            var text = string.Join(" ", _root.GetComponentsInChildren<Text>().Select(t => t.text));
            StringAssert.Contains("Builders free: 2 of 2", text);
            StringAssert.Contains("Takes 1m 00s", text);
        }

        [UnityTest]
        public IEnumerator CaptureTimer()
        {
            var dir = Environment.GetEnvironmentVariable("SD_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("SD_CAPTURE_DIR not set.");
            Directory.CreateDirectory(dir);
            LogAssert.ignoreFailingMessages = true;
            var camera = _game.IsoCamera.Camera;
            var canvas = _root.GetComponentInChildren<Canvas>();
            var texture = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            camera.targetTexture = texture;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;
            var id = MineAtLevelTwo();
            Controller.Village.TryUpgrade(id, DateTime.UtcNow.Ticks);
            Controller.Select(id);
            yield return null;
            yield return ScreenCaptureTests.Capture(camera, texture, Path.Combine(dir, "60_upgrade_timer.png"));
            camera.targetTexture = null;
        }
        private int ResearchHut()
        {
            var village = Controller.Village;
            var save = village.ToSave();
            save.gold = 50000;
            save.mana = 50000;
            save.crystals = 500;
            save.buildings.Find(b => b.definitionId == "dojo_hq").level = 2;
            village.Restore(save);
            Assert.AreEqual(VillageError.None, village.TryPlace("research_hut", new GridCoord(40, 40), Rotation.R0, DateTime.UtcNow.Ticks, out var hut));
            return hut;
        }

        [UnityTest]
        public IEnumerator Research_FromTheHut_StudiesAndFinishesForGems()
        {
            var hut = ResearchHut();
            Controller.Select(hut);
            yield return null;
            Assert.IsNotNull(Find("ResearchButton"), "The hut's medallion opens research");
            Controller.ShowBuildingDetails(hut);
            yield return null;
            var mana = Controller.Village.Mana;
            Find("Research_militia").onClick.Invoke();
            yield return null;
            Assert.AreEqual(mana - 200, Controller.Village.Mana, "The first militia study costs 200 Mana");
            Assert.AreEqual("militia", Controller.Research.RunningTroop);
            Controller.ShowBuildingDetails(hut);
            yield return null;
            Assert.IsFalse(Find("Research_kunai_thrower").interactable, "One study at a time");
            var gems = Controller.Village.Crystals;
            Find("FinishResearchButton").onClick.Invoke();
            yield return null;
            Assert.AreEqual(1, Controller.Research.LevelOf("militia"));
            Assert.AreEqual(gems - 3, Controller.Village.Crystals, "A five-minute study costs 3 gems");
        }

        [UnityTest]
        public IEnumerator CaptureResearch()
        {
            var dir = Environment.GetEnvironmentVariable("SD_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("SD_CAPTURE_DIR not set.");
            Directory.CreateDirectory(dir);
            LogAssert.ignoreFailingMessages = true;
            var camera = _game.IsoCamera.Camera;
            var canvas = _root.GetComponentInChildren<Canvas>();
            var texture = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            camera.targetTexture = texture;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;
            var hut = ResearchHut();
            Controller.Research.TryStart(Controller.Village, Controller.TroopData, "militia", DateTime.UtcNow.Ticks);
            Controller.ShowBuildingDetails(hut);
            yield return null;
            yield return ScreenCaptureTests.Capture(camera, texture, Path.Combine(dir, "61_research.png"));
            camera.targetTexture = null;
        }
    }
}

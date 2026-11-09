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
    /// <summary>
    /// The Arrow, Cannon and Magic Towers are one building the player switches between (owner, 2026-11-09).
    /// Offline, against a temp save.
    /// </summary>
    public class TowerSwitchTests
    {
        private string _savePath;
        private GameObject _root;
        private VillageBootstrap _game;

        private VillageController Controller => _game.Controller;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _savePath = Path.Combine(Application.temporaryCachePath, "tower_village_" + Guid.NewGuid().ToString("N") + ".json");
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

        [UnityTest]
        public IEnumerator SwitchButton_TurnsAnArrowTowerIntoAMagicTower_KeepingItsLevel()
        {
            var village = Controller.Village;
            var save = village.ToSave();
            save.gold = 50000;
            village.Restore(save);
            Assert.AreEqual(VillageError.None, village.TryPlace("defense_tower", new GridCoord(2, 2), Rotation.R0, DateTime.UtcNow.Ticks, out var id));
            save = village.ToSave();
            save.buildings.Find(b => b.id == id).level = 4;
            village.Restore(save);

            Controller.Select(id);
            yield return null;
            Assert.IsNotNull(Find("SwitchButton"), "A tower offers to switch");
            Find("SwitchButton").onClick.Invoke();
            yield return null;
            Assert.IsNotNull(Find("Switch_cannon_tower"), "The Cannon Tower is offered");
            Assert.IsNotNull(Find("Switch_spirit_turret"), "The Magic Tower is offered");
            Assert.IsNull(Find("Switch_defense_tower"), "Not the tower it already is");
            Find("Switch_spirit_turret").onClick.Invoke();
            yield return null;

            var tower = village.Find(id);
            Assert.AreEqual("spirit_turret", tower.Definition.id);
            Assert.AreEqual(4, tower.Level, "Switching keeps the level");
            Assert.AreEqual(50000 - village.Catalog.Get("defense_tower").costGold, village.Gold, "Switching is free");

            // It saves as a Magic Tower and loads back as one.
            var reloaded = village.ToSave();
            Assert.AreEqual("spirit_turret", reloaded.buildings.Find(b => b.id == id).definitionId);
        }

        [Test]
        public void Catalog_ShowsOnlyTheArrowTower_AndTheTowersShareOneLimit()
        {
            var village = Controller.Village;
            var catalog = village.Catalog;
            Assert.IsFalse(catalog.Get("defense_tower").switchOnly);
            Assert.IsTrue(catalog.Get("cannon_tower").switchOnly);
            Assert.IsTrue(catalog.Get("spirit_turret").switchOnly);
            Assert.AreEqual(VillageError.UnknownBuilding, village.TryPlace("cannon_tower", new GridCoord(2, 2), Rotation.R0, DateTime.UtcNow.Ticks, out _));
        }
    }
}

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
    /// <summary>The first-time guide, offline against a temp save.</summary>
    public class GuideTests
    {
        private string _savePath;
        private GameObject _root;
        private VillageBootstrap _game;

        private VillageController Controller => _game.Controller;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _savePath = Path.Combine(Application.temporaryCachePath, "guide_village_" + Guid.NewGuid().ToString("N") + ".json");
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
        public IEnumerator Guide_PointsTheWay_AndMovesOnWhenTheStepIsDone()
        {
            yield return null;
            Assert.IsTrue(Controller.GuideShowing, "A new village gets the guide");
            var arrow = _root.GetComponentsInChildren<RectTransform>(false).FirstOrDefault(r => r.name == "GuideArrow");
            Assert.IsNotNull(arrow, "An arrow points at what to tap");
            var first = Controller.GuideStepIndex;

            // Collecting from the mine is the first step for a fresh village.
            var mine = Controller.Village.Buildings.First(b => b.Definition.id == "gold_mine");
            var save = Controller.Village.ToSave();
            save.buildings.Find(b => b.id == mine.Id).lastCollectedUtcTicks = DateTime.UtcNow.Ticks - TimeSpan.TicksPerHour;
            Controller.Village.Restore(save);
            Controller.CollectOrSelect(mine.Id);
            yield return null;
            Assert.Greater(Controller.GuideStepIndex, first, "Collecting moves the guide on");
        }

        [UnityTest]
        public IEnumerator Guide_NeverBlocksTaps_AndCanBeSkipped()
        {
            yield return null;
            var graphics = _root.GetComponentsInChildren<Graphic>(false).Where(g => g.transform.IsChildOf(_root.GetComponentsInChildren<RectTransform>().First(r => r.name == "Guide")));
            Assert.IsTrue(graphics.Where(g => g.raycastTarget).All(g => g.GetComponentInParent<Button>()?.name == "SkipGuideButton"), "Only Skip takes taps");
            Find("SkipGuideButton").onClick.Invoke();
            yield return null;
            Assert.IsFalse(Controller.GuideShowing);
            Object.Destroy(_root);
            yield return null;
            _root = new GameObject("VillageBootstrapUnderTest");
            _game = _root.AddComponent<VillageBootstrap>();
            yield return null;
            yield return null;
            Assert.IsFalse(Controller.GuideShowing, "Skipping is remembered");
        }
    }
}

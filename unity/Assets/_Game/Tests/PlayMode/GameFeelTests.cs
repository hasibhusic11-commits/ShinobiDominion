using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShinobiDominion.Game;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShinobiDominion.Tests
{
    /// <summary>Game feel always returns to rest: pops settle, freezes restore the speed, flying coins clean up.</summary>
    public class GameFeelTests
    {
        private string _savePath;
        private GameObject _root;
        private VillageController Controller;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _savePath = Path.Combine(Application.temporaryCachePath, "feel_" + System.Guid.NewGuid().ToString("N") + ".json");
            VillageBootstrap.SavePathOverride = _savePath;
            _root = new GameObject("VillageBootstrapUnderTest");
            Controller = _root.AddComponent<VillageBootstrap>().Controller;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            Object.Destroy(_root);
            VillageBootstrap.SavePathOverride = null;
            yield return null;
            foreach (var file in new[] { _savePath, _savePath + ".heroes" })
                if (File.Exists(file)) File.Delete(file);
        }

        [UnityTest]
        public IEnumerator Pop_SquashesTheBuilding_ThenSettlesAtItsNormalSize()
        {
            var stand = _root.GetComponentsInChildren<IsoStand>().First();
            var restScale = stand.transform.localScale;
            Feel.Pop(stand, Feel.Tier.Medium);
            yield return null;
            Assert.AreNotEqual(Vector2.one, stand.Juice, "The pop squashes the picture");
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.AreEqual(Vector2.one, stand.Juice, "and it comes back to rest");
            Assert.That(Vector3.Distance(restScale, stand.transform.localScale), Is.LessThan(0.001f));
        }

        [UnityTest]
        public IEnumerator HitStop_FreezesBriefly_AndAlwaysRestoresTheSpeed()
        {
            Time.timeScale = 1f;
            Feel.HitStop(Feel.Tier.Large);
            Feel.HitStop(Feel.Tier.Medium);
            yield return null;
            Assert.Less(Time.timeScale, 0.2f, "Time nearly stops on impact");
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(1f, Time.timeScale, "Overlapping freezes still end at full speed");
        }

        [UnityTest]
        public IEnumerator CollectCoins_FlyIntoTheCounter_AndCleanUp()
        {
            Controller.Hud.FlyCoins(new Vector2(Screen.width * 0.5f, Screen.height * 0.4f), false);
            yield return null;
            Assert.Greater(Controller.Hud.Root.GetComponentsInChildren<RectTransform>().Count(t => t.name == "FlyingCoin"), 0);
            yield return new WaitForSecondsRealtime(1.3f);
            Assert.AreEqual(0, Controller.Hud.Root.GetComponentsInChildren<RectTransform>().Count(t => t.name == "FlyingCoin"), "Every coin is gone once it lands");
        }

        [UnityTest]
        public IEnumerator ScreenShake_DecaysBackToTheSameView()
        {
            var camera = Object.FindAnyObjectByType<IsoCamera>();
            var rest = camera.transform.position;
            camera.AddTrauma(0.8f);
            yield return null;
            yield return null;
            Assert.Greater(Vector3.Distance(rest, camera.transform.position), 0.0001f, "The view shakes");
            yield return new WaitForSecondsRealtime(1f);
            Assert.Less(Vector3.Distance(rest, camera.transform.position), 0.001f, "and settles exactly where it was");
        }
    }
}

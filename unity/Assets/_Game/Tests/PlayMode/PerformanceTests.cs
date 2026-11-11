using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShinobiDominion.Core;
using ShinobiDominion.Game;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShinobiDominion.Tests
{
    /// <summary>
    /// What a frame costs on the screens a player spends the most time on. The numbers are from the editor on a
    /// desktop, so they say nothing about a phone's speed; what they do catch is the game's own code starting to
    /// allocate memory every frame again, which is the thing that makes a phone stutter. Each reading is written to
    /// the log, and to perf.txt when SD_CAPTURE_DIR is set.
    /// </summary>
    public class PerformanceTests
    {
        /// <summary>Seconds measured, after the seconds given to let the screen settle.</summary>
        private const float Measured = 3f;

        private string _savePath;
        private GameObject _root;
        private VillageBootstrap _game;

        private VillageController Controller => _game.Controller;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _savePath = Path.Combine(Application.temporaryCachePath, "perf_village_" + Guid.NewGuid().ToString("N") + ".json");
            VillageBootstrap.SavePathOverride = _savePath;
            _root = new GameObject("VillageBootstrapUnderTest");
            _game = _root.AddComponent<VillageBootstrap>();
            yield return null;
            // The editor's own 60-a-second limiter allocates about 160 KB a frame while it waits, which would drown the
            // game's numbers. Frames run as fast as they can while measuring.
            Application.targetFrameRate = -1;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_root);
            VillageBootstrap.SavePathOverride = null;
            Application.targetFrameRate = 60;
            yield return null;
            foreach (var file in new[] { _savePath, _savePath + ".heroes" })
                if (File.Exists(file)) File.Delete(file);
        }

        private static IEnumerator Measure(string what, float settle, Action<FrameMeter.Reading> then)
        {
            var until = Time.realtimeSinceStartup + settle;
            while (Time.realtimeSinceStartup < until) yield return null;
            var meter = new FrameMeter();
            meter.Start();
            until = Time.realtimeSinceStartup + Measured;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                meter.Tick();
            }
            var reading = meter.Stop();
            var line = $"[perf] {what}: {reading}";
            Debug.Log(line);
            var dir = Environment.GetEnvironmentVariable("SD_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "perf.txt"), line + Environment.NewLine);
            }
            then(reading);
        }

        /// <summary>The village with nothing happening: the screen a player looks at most.</summary>
        [UnityTest]
        public IEnumerator Village_AtRest_AllocatesAlmostNothingPerFrame()
        {
            yield return Measure("village at rest", 0.5f, reading =>
                // Measured at about 1.3 KB a frame in the editor (0.9 KB of it the editor's own); 3 KB leaves room and still
                // fails if a screen goes back to rebuilding lists and strings every frame.
                Assert.Less(reading.AllocatedPerFrame, 3000, reading.ToString()));
        }

        /// <summary>The village with three upgrades running: three timers placed and counted down every frame.</summary>
        [UnityTest]
        public IEnumerator Village_WithUpgradeTimers_AllocatesAlmostNothingPerFrame()
        {
            var village = Controller.Village;
            var save = village.ToSave();
            save.gold = 500000;
            save.crystals = 500;
            foreach (var building in save.buildings.Where(b => b.definitionId == "gold_mine" || b.definitionId == "mana_mill" || b.definitionId == "hero_altar"))
                building.level = Math.Max(building.level, 2);
            village.Restore(save);
            var started = 0;
            foreach (var building in village.Buildings.ToList())
            {
                if (started >= village.Builders) break;
                if (village.TryUpgrade(building.Id, DateTime.UtcNow.Ticks) == VillageError.None && village.Find(building.Id).Upgrading) started++;
            }
            Assert.Greater(started, 0, "At least one upgrade is running");
            yield return Measure($"village with {started} upgrade timers", 0.5f, reading =>
                Assert.Less(reading.AllocatedPerFrame, 3000, reading.ToString()));
        }

        /// <summary>A practice raid in full swing: units, shots, damage numbers and the battle HUD.</summary>
        [UnityTest]
        public IEnumerator Raid_InBattle_StaysUnderItsAllocationBudget()
        {
            var village = Controller.Village;
            if (!village.Buildings.Any(b => b.Definition.function == BuildingFunction.HeroBase))
                Assert.AreEqual(VillageError.None, village.TryPlace("hero_base", new GridCoord(0, 40), Rotation.R0, 0, out _));
            var home = village.Buildings.First(b => b.Definition.function == BuildingFunction.HeroBase);
            Controller.AssignHero(home.Id, Controller.Roster.AddHero("solar_beast_sage", 0, HeroMath.MaxLevel).InstanceId);
            Controller.OpenAttack();
            Controller.ScoutRaid("ridge_outpost");
            yield return null;
            Controller.AutoPlaceHeroes();
            Assert.Greater(Controller.RaidPlacements.Count, 0, Controller.LastMessage);
            yield return Measure("raid in battle", 4f, reading =>
                // Measured at about 0.9 KB a frame with frames running free. The bound is that with room on top: it catches
                // a big regression and does not prove the fight is free.
                Assert.Less(reading.AllocatedPerFrame, 6000, reading.ToString()));
            Controller.FinishRaid();
            yield return null;
        }

        /// <summary>Damage numbers are used again when they finish: a burst of them makes a few, not one each.</summary>
        [UnityTest]
        public IEnumerator DamageNumbers_AreReused()
        {
            var camera = Camera.main;
            var first = FloatingText.Create(_root.transform, camera, Vector3.zero, "120", Color.white);
            Assert.IsNotNull(first.GetComponent<TMPro.TextMeshPro>(), "A number is distance-field text");
            Assert.AreEqual("120", first.GetComponent<TMPro.TextMeshPro>().text);
            var made = FloatingText.Made;
            // Each number lives under a second. Forty of them, one every tenth of a second, need about ten at once.
            for (var i = 0; i < 40; i++)
            {
                FloatingText.Create(_root.transform, camera, Vector3.zero, i.ToString(), Color.white);
                yield return new WaitForSeconds(0.1f);
            }
            Assert.LessOrEqual(FloatingText.Made - made, 14, "Forty numbers over four seconds reuse about ten objects");
            Assert.LessOrEqual(_root.GetComponentsInChildren<FloatingText>(true).Length, 15);
        }

        [Test]
        public void RenderProfile_ADesktopKeepsEverything_APhoneGetsALighterSet()
        {
            Assert.AreEqual(RenderProfile.Desktop.ToString(), RenderProfile.For(false, 2000, 2160).ToString(), "A desktop is never lowered, whatever its memory");

            var phone = RenderProfile.For(true, 8000, 1080);
            Assert.AreEqual(1f, phone.RenderScale, "A 1080 screen is drawn at full size");
            Assert.AreEqual(2, phone.Msaa);
            Assert.AreEqual(2048, phone.ShadowMap);
            Assert.IsFalse(phone.Hdr);

            var sharp = RenderProfile.For(true, 8000, 1440);
            Assert.AreEqual(0.75f, sharp.RenderScale, 0.001f, "A 1440 screen draws the world at 1080 and scales it up");

            var weak = RenderProfile.For(true, 3000, 1080);
            Assert.AreEqual(0.75f, weak.RenderScale, 0.001f, "A weak phone draws the world at 810");
            Assert.AreEqual(1, weak.Msaa);
            Assert.AreEqual(1024, weak.ShadowMap);
            Assert.AreEqual(1, weak.ExtraLights);

            Assert.AreEqual(0.6f, RenderProfile.For(true, 3000, 2160).RenderScale, 0.001f, "Never below 60%");
            Assert.AreEqual(1f, RenderProfile.For(true, 0, 720).RenderScale, "Unknown memory counts as enough; a small screen is never scaled up");
        }
    }
}

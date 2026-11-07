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
    /// <summary>Heroes guarding a raid target, seen through the real attack flow. Offline, so it pays nothing.</summary>
    public class RaidDefenderTests
    {
        private const string Garrison = "cinder_garrison";
        private GameObject _root;
        private VillageController _controller;
        private VillageBootstrap _game;
        private string _savePath;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _savePath = Path.Combine(Application.temporaryCachePath, "defenders_" + Guid.NewGuid().ToString("N") + ".json");
            VillageBootstrap.SavePathOverride = _savePath;
            _root = new GameObject("VillageBootstrapUnderTest");
            _game = _root.AddComponent<VillageBootstrap>();
            _controller = _game.Controller;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_root);
            VillageBootstrap.SavePathOverride = null;
            yield return null;
            foreach (var file in new[] { _savePath, _savePath + ".heroes" })
                if (File.Exists(file)) File.Delete(file);
        }

        private void AssignTeam(int count)
        {
            var village = _controller.Village;
            var bases = village.Buildings.Where(b => b.Definition.function == BuildingFunction.HeroBase).ToList();
            for (var x = 0; bases.Count < count; x += 3)
            {
                Assert.AreEqual(VillageError.None, village.TryPlace("hero_base", new GridCoord(x, 40), Rotation.R0, 0, out _));
                bases = village.Buildings.Where(b => b.Definition.function == BuildingFunction.HeroBase).ToList();
            }
            for (var i = 0; i < count; i++)
            {
                _controller.Roster.AddHero("solar_beast_sage", 0, HeroMath.MaxLevel);
                var hero = _controller.Roster.Heroes.Last();
                _controller.AssignHero(bases[i].Id, hero.InstanceId);
            }
        }

        private static GameObject[] Guards() =>
            Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name == "BattleDefender").Select(t => t.gameObject).ToArray();

        [UnityTest]
        public IEnumerator Garrison_ShowsItsGuards_BeforeTheBattleStarts()
        {
            AssignTeam(1);
            yield return null;
            _controller.ScoutRaid(Garrison);
            yield return null;
            Assert.AreEqual(RaidPhase.Prepare, _controller.CurrentRaidPhase);
            Assert.AreEqual(3, _controller.CurrentRaidSession.defenders.Count);
            Assert.AreEqual(3, Guards().Length);
            Assert.IsTrue(Guards().Any(g => g.GetComponentInChildren<HeroSpriteActor>() != null), "The Sage guard should be a sprite");
            Assert.IsTrue(Guards().Any(g => g.GetComponent<MeshRenderer>() != null), "Guards with no art yet should use the fallback");
            _controller.RetreatRaid();
            yield return null;
            Assert.AreEqual(0, Guards().Length, "Retreating should clear the guards");
        }

        [UnityTest]
        public IEnumerator Garrison_StrongTeam_BeatsTheGuards_AndTheResultNamesThem()
        {
            AssignTeam(6);
            yield return null;
            _controller.ScoutRaid(Garrison);
            _controller.AutoPlaceHeroes();
            _controller.StartBattle();
            Assert.AreEqual(RaidPhase.Battle, _controller.CurrentRaidPhase);
            var result = _controller.LastRaidOutcome.Result;
            Assert.IsTrue(result.events.Any(e => e[1] == (long)BattleEventKind.DefenderDamaged), "The team should hit a guard");
            yield return new WaitForSeconds(0.5f);
            _controller.SkipBattle();
            Assert.AreEqual(RaidPhase.Resolved, _controller.CurrentRaidPhase);
            Assert.AreEqual(result.defendersAlive, Guards().Count(g => g.activeSelf), "Skipping should leave only the living guards");
            _controller.FinishRaid();
            yield return null;
            Assert.IsNull(_controller.CurrentRaidPhase);
        }

        [UnityTest]
        public IEnumerator Garrison_PlaysToTheEnd_WithoutErrors()
        {
            AssignTeam(6);
            yield return null;
            _controller.ScoutRaid(Garrison);
            _controller.AutoPlaceHeroes();
            _controller.StartBattle();
            var scale = Time.timeScale;
            Time.timeScale = 20f;
            var limit = Time.realtimeSinceStartup + 60f;
            while (_controller.CurrentRaidPhase == RaidPhase.Battle && Time.realtimeSinceStartup < limit) yield return null;
            Time.timeScale = scale;
            Assert.AreEqual(RaidPhase.Resolved, _controller.CurrentRaidPhase);
            _controller.FinishRaid();
            yield return null;
        }

        [Test]
        public void SessionFromTheServer_ReadsItsGuards()
        {
            var session = RaidSession.Parse("{\"attackId\":\"a\",\"raid\":{\"id\":\"rival\"},\"buildings\":[],\"team\":[],\"defenders\":[{\"id\":\"d1\",\"archetypeId\":\"river_scout\",\"evolutionIndex\":0,\"level\":5,\"role\":\"Assassin\",\"hp\":10,\"attack\":3,\"defense\":1,\"x\":4,\"z\":6}]}");
            var inputs = session.ToDefenderInputs();
            Assert.AreEqual(1, inputs.Count);
            Assert.AreEqual(4, inputs[0].x);
            Assert.AreEqual("Assassin", inputs[0].role);
        }

        [Test]
        public void DefenseLog_ReadsRecords()
        {
            var list = DefenseRecord.ParseList("{\"defenses\":[{\"stars\":2,\"damagePercent\":60,\"defendersAlive\":1,\"defendersTotal\":3,\"atMs\":5}]}");
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(3, list[0].defendersTotal);
        }

        [UnityTest]
        public IEnumerator Capture_ForReview()
        {
            var dir = Environment.GetEnvironmentVariable("SD_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("SD_CAPTURE_DIR not set.");
            Directory.CreateDirectory(dir);
            LogAssert.ignoreFailingMessages = true;

            AssignTeam(6);
            yield return null;
            var iso = _game.IsoCamera;
            var camera = iso.Camera;
            var canvas = _root.GetComponentInChildren<Canvas>();
            var texture = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            camera.targetTexture = texture;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;
            yield return null;

            _controller.OpenAttack();
            yield return null;
            yield return ScreenCaptureTests.Capture(camera, texture, Path.Combine(dir, "defenders_attack_panel.png"));

            _controller.ScoutRaid(Garrison);
            yield return null;
            yield return ScreenCaptureTests.Capture(camera, texture, Path.Combine(dir, "defenders_prepare.png"));

            _controller.AutoPlaceHeroes();
            _controller.StartBattle();
            yield return null;
            canvas.gameObject.SetActive(false);
            iso.enabled = false;

            var sage = Guards().First(g => g.GetComponentInChildren<HeroSpriteActor>() != null);
            camera.orthographicSize = 6f;
            var shots = 0;
            var limit = Time.time + 45f;
            while (_controller.CurrentRaidPhase == RaidPhase.Battle && Time.time < limit && shots < 8 && sage != null && sage.activeInHierarchy)
            {
                camera.transform.position = sage.transform.position + Vector3.up * 0.8f - camera.transform.forward * 80f;
                var fighting = sage.GetComponentInChildren<HeroSpriteActor>().Animator.GetCurrentAnimatorStateInfo(0).IsName(HeroSpriteActor.AttackState);
                if (fighting)
                {
                    yield return ScreenCaptureTests.Capture(camera, texture, Path.Combine(dir, $"defenders_fight_{shots}.png"));
                    shots++;
                }
                yield return new WaitForSeconds(0.1f);
            }

            camera.orthographicSize = 14f;
            camera.transform.position = new Vector3(25f, 0f, 25f) - camera.transform.forward * 80f;
            yield return ScreenCaptureTests.Capture(camera, texture, Path.Combine(dir, "defenders_battle_wide.png"));

            canvas.gameObject.SetActive(true);
            _controller.SkipBattle();
            yield return null;
            yield return ScreenCaptureTests.Capture(camera, texture, Path.Combine(dir, "defenders_result.png"));

            iso.enabled = true;
            camera.targetTexture = null;
            Object.Destroy(texture);
        }
    }
}

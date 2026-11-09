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
            Assert.AreEqual(RaidPhase.Battle, _controller.CurrentRaidPhase, "A raid is a live battle from the start (ADR-050)");
            Assert.AreEqual(1, _controller.CurrentRaidSession.defenders.Count);
            Assert.AreEqual(1, Guards().Length);
            Assert.IsTrue(Guards().Any(g => g.GetComponentInChildren<HeroSpriteActor>() != null), "The Sage guard should be a sprite");
            _controller.RetreatRaid();
            yield return null;
            Assert.AreEqual(0, Guards().Length, "Retreating should clear the guards");
        }

        [UnityTest]
        public IEnumerator Garrison_StrongTeam_BeatsTheGuards_AndTheResultNamesThem()
        {
            AssignTeam(1);
            yield return null;
            _controller.ScoutRaid(Garrison);
            _controller.AutoPlaceHeroes();
            Assert.AreEqual(RaidPhase.Battle, _controller.CurrentRaidPhase);
            var scale = Time.timeScale;
            Time.timeScale = 20f;
            var limit = Time.realtimeSinceStartup + 30f;
            while (_controller.CurrentRaidPhase == RaidPhase.Battle && _controller.LiveTick < 500 && Time.realtimeSinceStartup < limit) yield return null;
            Time.timeScale = scale;
            _controller.SkipBattle();
            Assert.AreEqual(RaidPhase.Resolved, _controller.CurrentRaidPhase);
            var result = _controller.LastRaidOutcome.Result;
            Assert.IsTrue(result.events.Any(e => e[1] == (long)BattleEventKind.DefenderDamaged), "The team should hit a guard");
            yield return null;
            Assert.AreEqual(result.defendersAlive, Guards().Count(g => g.activeSelf), "Skipping should leave only the living guards");
            _controller.FinishRaid();
            yield return null;
            Assert.IsNull(_controller.CurrentRaidPhase);
        }

        [UnityTest]
        public IEnumerator Garrison_PlaysToTheEnd_WithoutErrors()
        {
            AssignTeam(1);
            yield return null;
            _controller.ScoutRaid(Garrison);
            _controller.AutoPlaceHeroes();
            var scale = Time.timeScale;
            Time.timeScale = 20f;
            var limit = Time.realtimeSinceStartup + 60f;
            while (_controller.CurrentRaidPhase == RaidPhase.Battle && Time.realtimeSinceStartup < limit) yield return null;
            Time.timeScale = scale;
            Assert.AreEqual(RaidPhase.Resolved, _controller.CurrentRaidPhase);
            yield return null;
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
        public IEnumerator DungeonMap_ShowsChapters_AndScoutsTheChosenStage()
        {
            AssignTeam(1);
            yield return null;
            _controller.OpenAttack();
            yield return null;
            _controller.OpenCampaign();
            yield return null;
            Button Find(string name) => _root.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.name == name);
            Assert.IsNotNull(Find("StageNode1"));
            Assert.IsNotNull(Find("StageNode5"));
            Assert.IsNull(Find("StageNode6"), "Stage 6 belongs to the next chapter");
            Find("ChapterTab2").onClick.Invoke();
            yield return null;
            Assert.IsNotNull(Find("StageNode6"));
            Find("StageNode7").onClick.Invoke();
            yield return null;
            Find("ScoutButton").onClick.Invoke();
            yield return null;
            Assert.AreEqual(RaidPhase.Battle, _controller.CurrentRaidPhase);
            Assert.AreEqual("stage_07", _controller.CurrentRaidSession.raid.id);
            _controller.RetreatRaid();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Battle_ShowsHud_GravesAndSound_WithoutErrors()
        {
            AssignTeam(1);
            yield return null;
            _controller.ScoutRaid("stage_05");
            _controller.AutoPlaceHeroes();
            yield return null;
            Assert.IsNotNull(GameObject.Find("BattleHud"), "The battle screen should show its panel");
            var scale = Time.timeScale;
            Time.timeScale = 20f;
            var limit = Time.realtimeSinceStartup + 60f;
            while (_controller.CurrentRaidPhase == RaidPhase.Battle && Time.realtimeSinceStartup < limit) yield return null;
            Time.timeScale = scale;
            Assert.AreEqual(RaidPhase.Resolved, _controller.CurrentRaidPhase);
            yield return null;
            Assert.IsNull(GameObject.Find("BattleHud"), "The panel should go when the battle ends");
            var result = _controller.LastRaidOutcome.Result;
            var falls = result.events.Count(e => e[1] == (long)BattleEventKind.HeroDown || e[1] == (long)BattleEventKind.DefenderDown);
            var graves = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.name == "Grave");
            Assert.AreEqual(falls, graves, "Every fallen fighter should leave a stone");
            _controller.FinishRaid();
            yield return null;
        }

        [Test]
        public void EverySound_CanBeMade()
        {
            foreach (GameSound sound in Enum.GetValues(typeof(GameSound)))
            {
                var clip = GameSounds.ClipFor(sound);
                Assert.IsNotNull(clip, sound.ToString());
                Assert.Greater(clip.length, 0f, sound.ToString());
                var samples = new float[clip.samples];
                clip.GetData(samples, 0);
                Assert.Greater(samples.Max(Mathf.Abs), 0.05f, sound + " should not be silent");
            }
        }

        [UnityTest]
        public IEnumerator Village_HasWalkers_ThatMove_AndStayOutOfBuildings()
        {
            yield return null;
            var life = Object.FindAnyObjectByType<VillageLife>();
            Assert.IsNotNull(life);
            Assert.Greater(life.WalkerCount, 0);
            var walkers = life.GetComponentsInChildren<Transform>().Where(t => t.name == "VillageWalker").ToList();
            var start = walkers.Select(w => w.position).ToList();
            var scale = Time.timeScale;
            Time.timeScale = 10f;
            yield return new WaitForSeconds(30f);
            Time.timeScale = scale;
            Assert.IsTrue(walkers.Select((w, i) => (w.position - start[i]).sqrMagnitude > 0.01f).Any(moved => moved), "Someone should have walked");
            foreach (var walker in walkers)
            {
                var cell = new GridCoord(Mathf.FloorToInt(walker.position.x), Mathf.FloorToInt(walker.position.z));
                if (VillageGrid.InBounds(cell.X, cell.Z)) Assert.AreEqual(VillageGrid.NoOccupant, _controller.Village.Grid.OccupantAt(cell), $"A walker is inside a building at {cell.X},{cell.Z}");
            }
        }

        [UnityTest]
        public IEnumerator Capture_ForReview()
        {
            var dir = Environment.GetEnvironmentVariable("SD_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("SD_CAPTURE_DIR not set.");
            Directory.CreateDirectory(dir);
            LogAssert.ignoreFailingMessages = true;

            AssignTeam(1);
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

            _controller.OpenCampaign();
            yield return null;
            yield return ScreenCaptureTests.Capture(camera, texture, Path.Combine(dir, "campaign_map.png"));
            _controller.CloseAll();
            yield return null;
            _controller.OpenAttack();
            yield return null;

            _controller.ScoutRaid(Garrison);
            yield return null;
            yield return ScreenCaptureTests.Capture(camera, texture, Path.Combine(dir, "defenders_prepare.png"));

            _controller.AutoPlaceHeroes();
            yield return new WaitUntil(() => _controller.LiveTick > 1);
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
            canvas.gameObject.SetActive(true);
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

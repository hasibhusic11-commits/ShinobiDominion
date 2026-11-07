using System;
using System.Collections;
using System.Collections.Generic;
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
    /// <summary>Checks the sprite hero in the real village and battle. Set SD_CAPTURE_DIR to also save images.</summary>
    public class HeroSpriteTests
    {
        private const string Hero = "solar_beast_sage";
        private GameObject _root;
        private string _savePath;

        private VillageController Controller => _root.GetComponent<VillageBootstrap>().Controller;
        private VillageBootstrap Game => _root.GetComponent<VillageBootstrap>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _savePath = Path.Combine(Application.temporaryCachePath, "sprites_" + Guid.NewGuid().ToString("N") + ".json");
            VillageBootstrap.SavePathOverride = _savePath;
            _root = new GameObject("VillageBootstrapUnderTest");
            _root.AddComponent<VillageBootstrap>();
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

        private BuildingInstance AssignSage()
        {
            var village = Controller.Village;
            var baseAt = village.Buildings.FirstOrDefault(b => b.Definition.function == BuildingFunction.HeroBase);
            if (baseAt == null)
            {
                Assert.AreEqual(VillageError.None, village.TryPlace("hero_base", new GridCoord(0, 40), Rotation.R0, 0, out _));
                baseAt = village.Buildings.First(b => b.Definition.function == BuildingFunction.HeroBase);
            }
            Controller.Roster.AddHero(Hero, 0, HeroMath.MaxLevel);
            var hero = Controller.Roster.Heroes.Last(h => h.ArchetypeId == Hero);
            Controller.AssignHero(baseAt.Id, hero.InstanceId);
            return baseAt;
        }

        private HeroSpriteActor FindActor() => Object.FindObjectsByType<HeroSpriteActor>(FindObjectsSortMode.None).FirstOrDefault();

        private static float BoneAngle(Transform actor, string bone)
        {
            return actor.GetComponentsInChildren<Transform>().First(t => t.name == bone).localEulerAngles.z;
        }

        [UnityTest]
        public IEnumerator SpriteHero_AppearsOnHeroBase_AndPlaysAllThreeAnimations()
        {
            AssignSage();
            yield return null;
            yield return null;
            var actor = FindActor();
            Assert.IsNotNull(actor, "The Sage should be a sprite hero on the Hero Base");
            Assert.IsTrue(actor.GetComponentsInChildren<SpriteRenderer>().Length >= 16, "All layers should render");
            // The test's base may be off screen, where Unity stops animating to save time.
            actor.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Assert.IsTrue(actor.Animator.GetCurrentAnimatorStateInfo(0).IsName(HeroSpriteActor.IdleState), "Should start idle");

            var before = BoneAngle(actor.transform, "upper_l");
            yield return new WaitForSeconds(0.45f);
            Assert.AreNotEqual(before, BoneAngle(actor.transform, "upper_l"), "Idle should move the arms");

            actor.SetMoving(true);
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(actor.Animator.GetCurrentAnimatorStateInfo(0).IsName(HeroSpriteActor.RunState), "Should run");
            var first = BoneAngle(actor.transform, "thigh_r");
            yield return new WaitForSeconds(0.15f);
            Assert.AreNotEqual(first, BoneAngle(actor.transform, "thigh_r"), "Run should move the legs");

            actor.SetMoving(false);
            actor.Attack();
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(actor.Animator.GetCurrentAnimatorStateInfo(0).IsName(HeroSpriteActor.AttackState), "Should attack");
            yield return new WaitForSeconds(1f);
            Assert.IsTrue(actor.Animator.GetCurrentAnimatorStateInfo(0).IsName(HeroSpriteActor.IdleState), "Should return to idle");
        }

        [UnityTest]
        public IEnumerator EvolutionWithNoSpriteEntry_UsesTheFallback()
        {
            Assert.IsTrue(HeroSprites.Has(new HeroLook { DesignId = Hero, Evolution = 0 }));
            Assert.IsFalse(HeroSprites.Has(new HeroLook { DesignId = Hero, Evolution = 1 }));
            Assert.IsFalse(HeroSprites.Has(new HeroLook { DesignId = "thunder_empress", Evolution = 0 }));
            Assert.IsNull(HeroSprites.Spawn(new HeroLook { DesignId = "thunder_empress", Evolution = 0 }, _root.transform));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SpriteHero_FightsInABattle()
        {
            AssignSage();
            yield return null;
            Controller.ScoutRaid("ridge_outpost");
            Controller.AutoPlaceHeroes();
            yield return null;
            Assert.IsTrue(Object.FindObjectsByType<HeroSpriteActor>(FindObjectsSortMode.None).Any(a => a.GetComponentInParent<BattleStage>() != null), "Sage should be a sprite in the raid");
            Controller.StartBattle();
            var stageActor = Object.FindObjectsByType<HeroSpriteActor>(FindObjectsSortMode.None).First(a => a.GetComponentInParent<BattleStage>() != null);
            var ran = false;
            var attacked = false;
            var limit = Time.time + 40f;
            while (Controller.CurrentRaidPhase == RaidPhase.Battle && Time.time < limit && !(ran && attacked))
            {
                if (stageActor == null) break;
                var state = stageActor.Animator.GetCurrentAnimatorStateInfo(0);
                ran |= state.IsName(HeroSpriteActor.RunState);
                attacked |= state.IsName(HeroSpriteActor.AttackState);
                yield return null;
            }
            Assert.IsTrue(ran, "Should have run in battle");
            Assert.IsTrue(attacked, "Should have attacked in battle");
        }

        [UnityTest]
        public IEnumerator Capture_ForReview()
        {
            var dir = Environment.GetEnvironmentVariable("SD_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("SD_CAPTURE_DIR not set.");
            Directory.CreateDirectory(dir);
            LogAssert.ignoreFailingMessages = true;

            var building = AssignSage();
            yield return null;
            yield return null;
            var actor = FindActor();
            var iso = Game.IsoCamera;
            var camera = iso.Camera;
            var canvas = _root.GetComponentInChildren<Canvas>();
            var texture = new RenderTexture(1000, 1000, 24) { antiAliasing = 4 };
            camera.targetTexture = texture;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;
            canvas.gameObject.SetActive(false);
            iso.enabled = false;
            var startSize = camera.orthographicSize;
            var at = actor.transform.position + Vector3.up * 0.8f;
            camera.transform.position = at - camera.transform.forward * 80f;

            IEnumerator Shot(string name) => ScreenCaptureTests.Capture(camera, texture, Path.Combine(dir, name + ".png"));

            camera.orthographicSize = 1.6f;
            for (var i = 0; i < 4; i++)
            {
                yield return new WaitForSeconds(0.45f);
                yield return Shot($"close_idle_{i}");
            }
            actor.Face(camera.transform.right);
            actor.SetMoving(true);
            yield return new WaitForSeconds(0.2f);
            for (var i = 0; i < 8; i++)
            {
                yield return new WaitForSeconds(0.07f);
                yield return Shot($"close_run_{i}");
            }
            actor.SetMoving(false);
            yield return new WaitForSeconds(0.4f);
            actor.Attack();
            for (var i = 0; i < 8; i++)
            {
                yield return new WaitForSeconds(0.05f);
                yield return Shot($"close_attack_{i}");
            }
            yield return new WaitForSeconds(0.5f);

            // The gameplay camera, on the same wide frame the player sees.
            var wide = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            camera.targetTexture = wide;
            camera.orthographicSize = startSize;
            yield return null;
            yield return ScreenCaptureTests.Capture(camera, wide, Path.Combine(dir, "village_gameplay_zoom.png"));
            camera.orthographicSize = 8f;
            yield return null;
            yield return ScreenCaptureTests.Capture(camera, wide, Path.Combine(dir, "village_mid_zoom.png"));

            // Heroes all around a tall building, to see occlusion and clipping from the real camera.
            var hall = Controller.Village.Buildings.First(b => b.Definition.id == "builder_hall");
            var center = new Vector3(hall.Origin.X + hall.Definition.width * 0.5f, 0f, hall.Origin.Z + hall.Definition.depth * 0.5f);
            var ring = new GameObject("Ring");
            for (var i = 0; i < 12; i++)
            {
                var angle = i * Mathf.PI / 6f;
                var one = HeroSprites.Spawn(new HeroLook { DesignId = Hero, Evolution = 0 }, ring.transform);
                one.transform.position = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (hall.Definition.width * 0.5f + 0.35f);
            }
            camera.orthographicSize = 6f;
            camera.transform.position = center + Vector3.up - camera.transform.forward * 80f;
            yield return null;
            yield return ScreenCaptureTests.Capture(camera, wide, Path.Combine(dir, "occlusion_around_hall.png"));
            Object.Destroy(ring);
            canvas.gameObject.SetActive(true);

            // Battle, at gameplay scale and close enough to read.
            iso.enabled = true;
            yield return null;
            Controller.ScoutRaid("ridge_outpost");
            Controller.AutoPlaceHeroes();
            yield return null;
            Controller.StartBattle();
            var stageActor = Object.FindObjectsByType<HeroSpriteActor>(FindObjectsSortMode.None).First(a => a.GetComponentInParent<BattleStage>() != null);
            iso.enabled = false;
            canvas.gameObject.SetActive(false);
            var seen = new HashSet<string>();
            var limit = Time.time + 60f;
            var log = new List<string>();
            var shots = 0;
            var attackShots = 0;
            while (Controller.CurrentRaidPhase == RaidPhase.Battle && Time.time < limit && attackShots < 6 && stageActor != null && stageActor.gameObject.activeInHierarchy)
            {
                var state = stageActor.Animator.GetCurrentAnimatorStateInfo(0);
                var label = state.IsName(HeroSpriteActor.RunState) ? "run" : state.IsName(HeroSpriteActor.AttackState) ? "attack" : "idle";
                if (seen.Add(label)) log.Add($"{Time.time:F1}s {label}");
                camera.orthographicSize = 4f;
                camera.transform.position = stageActor.transform.position + Vector3.up * 0.8f - camera.transform.forward * 80f;
                if (label == "attack" || shots % 8 == 0)
                {
                    yield return ScreenCaptureTests.Capture(camera, wide, Path.Combine(dir, $"battle_{shots:000}_{label}.png"));
                    if (label == "attack") attackShots++;
                }
                shots++;
                yield return null;
            }
            if (stageActor != null)
            {
                camera.orthographicSize = startSize;
                yield return ScreenCaptureTests.Capture(camera, wide, Path.Combine(dir, "battle_gameplay_zoom.png"));
            }

            // Many at once, for the frame cost on this machine only.
            var crowd = new GameObject("Crowd");
            for (var i = 0; i < 30; i++)
            {
                var one = HeroSprites.Spawn(new HeroLook { DesignId = Hero, Evolution = 0 }, crowd.transform);
                one.transform.position = camera.transform.position + camera.transform.forward * 80f + new Vector3((i % 6) * 1.1f - 3f, 0f, (i / 6) * 1.1f - 2f);
                if (i % 2 == 0) one.SetMoving(true);
            }
            yield return null;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            for (var i = 0; i < 60; i++) yield return null;
            clock.Stop();
            camera.orthographicSize = 5f;
            yield return ScreenCaptureTests.Capture(camera, wide, Path.Combine(dir, "crowd_30.png"));
            var renderers = crowd.GetComponentsInChildren<SpriteRenderer>();
            var atlases = renderers.Where(r => r.sprite != null && r.sprite.name != "shadow").Select(r => r.sprite.texture).Distinct().Count();
            log.Add($"crowd 30 heroes: {renderers.Length} sprites, {atlases} texture(s), {clock.ElapsedMilliseconds / 60f:F1} ms a frame in the editor");
            log.Add($"sprite packed: {renderers.First(r => r.sprite.name != "shadow").sprite.packed}");
            File.WriteAllLines(Path.Combine(dir, "sprite_report.txt"), log);

            camera.targetTexture = null;
            Object.Destroy(crowd);
            Object.Destroy(texture);
            Object.Destroy(wide);
        }
    }
}

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
    /// <summary>Checks the 3D heroes (ADR-037) in the real village and battle. Set SD_CAPTURE_DIR to also save images.</summary>
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

        private static Transform Bone(Component actor, string bone) =>
            actor.GetComponentsInChildren<Transform>().First(t => t.name == bone);

        private static readonly string[] Legendaries =
        {
            "solar_beast_sage", "thunder_empress", "stone_titan", "moonfang_oracle",
            "crimson_eye_prodigy", "void_sovereign", "ashen_flame_lord", "storm_crow_shinobi"
        };

        private static readonly string[] Commons =
        {
            "ember_recruit", "reed_archer", "stone_apprentice", "talisman_novice", "bell_guard",
            "mist_courier", "spark_tinkerer", "ash_monk", "moth_herbalist", "gale_runner"
        };

        private static readonly string[] AllHeroes = Legendaries.Concat(Commons).ToArray();

        [UnityTest]
        public IEnumerator EliteWithout3DArt_StillAppearsOnHeroBase()
        {
            // No authored Elite prefab yet; this is a visibility regression test, not an art approval.
            var baseAt = AssignSage();
            var elite = Controller.Roster.AddHero("frost_lake_monk", 0, 1);
            Assert.AreEqual(VillageError.None, Controller.AssignHero(baseAt.Id, elite.InstanceId));
            yield return null;
            yield return null;
            var fallback = Object.FindObjectsByType<HeroFallbackMeshLifetime>(FindObjectsSortMode.None).FirstOrDefault();
            Assert.IsNotNull(fallback, "An assigned Elite must not disappear simply because its art is unfinished.");
            Assert.IsNotNull(fallback.GetComponent<HeroGuard>());
            Assert.IsNotEmpty(fallback.GetComponentsInChildren<MeshRenderer>(),
                "The temporary Elite representation must actually render.");
        }

        [UnityTest]
        public IEnumerator Hero3D_AppearsOnHeroBase_AndPlaysAllThreeAnimations()
        {
            AssignSage();
            yield return null;
            yield return null;
            var actor = FindActor();
            Assert.IsNotNull(actor, "The Sage should stand on the Hero Base");
            Assert.IsTrue(actor.Solid, "The Sage is a 3D model now");
            Assert.IsNotEmpty(actor.GetComponentsInChildren<SkinnedMeshRenderer>(), "A skinned 3D body renders");
            Assert.AreEqual(VillageView.BaseHeroScale3D, actor.transform.lossyScale.y, 0.001f, "A 3D hero stands big beside its small pedestal");
            // The test's base may be off screen, where Unity stops animating to save time.
            actor.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Assert.IsTrue(actor.Animator.GetCurrentAnimatorStateInfo(0).IsName(HeroSpriteActor.IdleState), "Should start idle");

            var arm = Bone(actor, "LeftArm");
            var before = arm.localRotation;
            yield return new WaitForSeconds(0.45f);
            Assert.Greater(Quaternion.Angle(before, arm.localRotation), 0.01f, "Idle should move the arms");

            actor.SetMoving(true);
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(actor.Animator.GetCurrentAnimatorStateInfo(0).IsName(HeroSpriteActor.RunState), "Should run");
            var thigh = Bone(actor, "RightUpLeg");
            var first = thigh.localRotation;
            yield return new WaitForSeconds(0.15f);
            Assert.Greater(Quaternion.Angle(first, thigh.localRotation), 1f, "Run should move the legs");

            actor.SetMoving(false);
            actor.Attack();
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(actor.Animator.GetCurrentAnimatorStateInfo(0).IsName(HeroSpriteActor.AttackState), "Should attack");
            yield return new WaitForSeconds(1.6f);
            Assert.IsTrue(actor.Animator.GetCurrentAnimatorStateInfo(0).IsName(HeroSpriteActor.IdleState), "Should return to idle");
        }

        [UnityTest]
        public IEnumerator Hero3D_HopsOffThePedestal_ToStandGuard_OverAGlowingRing()
        {
            AssignSage();
            yield return null;
            yield return null;
            var actor = FindActor();
            var guard = actor.GetComponent<HeroGuard>();
            Assert.IsNotNull(guard, "The base hero stands guard");
            var baseRoot = guard.transform.parent;
            Assert.Less(Vector3.Distance(actor.transform.position, baseRoot.position + VillageView.PedestalOffset + Vector3.up * VillageView.PedestalTop), 0.05f, "Starts on the pedestal");
            var stand = baseRoot.GetComponentInChildren<IsoStand>();
            Assert.AreEqual(VillageView.PedestalSize, stand.ArtSize, "The pedestal is drawn small");

            yield return new WaitForSeconds(HeroGuard.HopDelay + HeroGuard.HopTime + 0.6f);
            Assert.IsTrue(guard.Guarding);
            Assert.Less(Vector3.Distance(actor.transform.position, baseRoot.position + VillageView.GuardOffset), 0.05f, "Stands beside the pedestal");
            Assert.Greater(Vector3.Dot(actor.Visual.forward, new Vector3(-1f, 0f, -1f).normalized), 0.9f, "Faces out, towards the viewer");
            var ring = actor.GetComponentsInChildren<SpriteRenderer>().First(r => r.name == "GuardRing");
            Assert.Greater(ring.color.a, 0.2f, "The ring glows once the hero has landed");
        }

        [UnityTest]
        public IEnumerator Hero3D_TurnsToFaceWhereItGoes()
        {
            var actor = HeroSprites.Spawn(new HeroLook { DesignId = Hero, Evolution = 0 }, _root.transform);
            yield return null;
            actor.Face(Vector3.right);
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(Vector3.Dot(actor.Visual.forward, Vector3.right), 0.99f, "Turns towards +X");
            Assert.Greater(actor.Visual.localScale.x, 0f, "A 3D hero is never mirrored");
            var head = Bone(actor, "Head").position;
            var front = Bone(actor, "headfront").position;
            Assert.Greater(Vector3.Dot((front - head).normalized, Vector3.right), 0.5f, "Its face looks the way it faces");
        }

        [UnityTest]
        public IEnumerator EveryHero_HasA3DModel_AtEveryForm_WithAllClips()
        {
            foreach (var id in AllHeroes)
            {
                for (var form = 0; form <= 3; form++)
                    Assert.IsTrue(HeroSprites.Has(new HeroLook { DesignId = id, Evolution = form }), $"{id} form {form}");
                var actor = HeroSprites.Spawn(new HeroLook { DesignId = id, Evolution = 0 }, _root.transform);
                Assert.IsNotNull(actor, id);
                Assert.IsTrue(actor.Solid, id);
                var height = actor.GetComponentsInChildren<SkinnedMeshRenderer>().Select(r => r.bounds.max.y).Max() - actor.transform.position.y;
                Assert.That(height, Is.InRange(1.2f, 1.9f), $"{id} stands about the sprite heroes' height");
                var clips = actor.Animator.runtimeAnimatorController.animationClips.Select(c => c.name).ToList();
                CollectionAssert.IsSubsetOf(new[] { "idle", "run", "attack" }, clips, id);
                Object.Destroy(actor.gameObject);
            }
            Assert.IsFalse(HeroSprites.Has(new HeroLook { DesignId = "no_such_hero", Evolution = 0 }));
            Assert.IsNull(HeroSprites.Spawn(new HeroLook { DesignId = "no_such_hero", Evolution = 0 }, _root.transform));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Hero3D_FightsInABattle()
        {
            AssignSage();
            yield return null;
            Controller.ScoutRaid("ridge_outpost");
            Controller.AutoPlaceHeroes();
            yield return new WaitUntil(() => Controller.LiveTick > 1);
            Assert.IsTrue(Object.FindObjectsByType<HeroSpriteActor>(FindObjectsSortMode.None).Any(a => a.Solid && a.GetComponentInParent<BattleStage>() != null), "Sage should be a 3D model in the raid");
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

            // How the hero and its base are drawn, for checking the hero is never hidden by the painted base.
            var body = actor.GetComponentInChildren<SkinnedMeshRenderer>();
            var group = actor.GetComponentInChildren<UnityEngine.Rendering.SortingGroup>();
            var baseSprites = actor.transform.parent.GetComponentsInChildren<SpriteRenderer>().Where(r => r.GetComponentInParent<HeroSpriteActor>() == null);
            File.WriteAllLines(Path.Combine(dir, "hero_base_diag.txt"), new[]
            {
                $"hero root {actor.transform.position}, body bounds {body.bounds.min} to {body.bounds.max}",
                $"body order {body.sortingOrder}, group order {(group != null ? group.sortingOrder.ToString() : "none")}, queue {body.sharedMaterial.renderQueue}, shader {body.sharedMaterial.shader.name}",
                "base sprites: " + string.Join("; ", baseSprites.Select(r => $"{r.name} order {r.sortingOrder} queue {r.sharedMaterial.renderQueue} at {r.transform.position}")),
                $"camera at {camera.transform.position}, looking {camera.transform.forward}"
            });
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
            yield return new WaitUntil(() => Controller.LiveTick > 1);
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
            var bodies = crowd.GetComponentsInChildren<SkinnedMeshRenderer>();
            var triangles = bodies.Sum(r => r.sharedMesh.triangles.Length / 3);
            log.Add($"crowd 30 heroes: {bodies.Length} skinned bodies, {triangles:N0} triangles, {clock.ElapsedMilliseconds / 60f:F1} ms a frame in the editor");

            // Each rarity side by side, for review.
            Object.Destroy(crowd);
            foreach (var (row, file) in new[] { (Legendaries, "all_heroes_lineup.png"), (Commons, "commons_lineup.png") })
            {
                var lineup = new GameObject("Lineup");
                for (var i = 0; i < row.Length; i++)
                {
                    var one = HeroSprites.Spawn(new HeroLook { DesignId = row[i], Evolution = 0 }, lineup.transform);
                    one.transform.position = camera.transform.position + camera.transform.forward * 80f + camera.transform.right * ((i - (row.Length - 1) * 0.5f) * 1.25f) - Vector3.up * 0.6f;
                }
                camera.orthographicSize = row.Length > 8 ? 3.6f : 3f;
                for (var i = 0; i < 20; i++) yield return null;
                yield return ScreenCaptureTests.Capture(camera, wide, Path.Combine(dir, file));
                Object.Destroy(lineup);
                yield return null;
            }
            File.WriteAllLines(Path.Combine(dir, "hero_report.txt"), log);

            camera.targetTexture = null;
            Object.Destroy(texture);
            Object.Destroy(wide);
        }
    }
}

using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShinobiDominion.Core;
using ShinobiDominion.Game;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShinobiDominion.Tests
{
    /// <summary>Checks the art layer's promises: it stays separate from rules, and stays cheap.</summary>
    public class VisualSystemTests
    {
        private string _savePath;
        private GameObject _root;
        private VillageController Controller;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _savePath = Path.Combine(Application.temporaryCachePath, "visual_" + System.Guid.NewGuid().ToString("N") + ".json");
            VillageBootstrap.SavePathOverride = _savePath;
            _root = new GameObject("VillageBootstrapUnderTest");
            Controller = _root.AddComponent<VillageBootstrap>().Controller;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameQuality.Set(VisualQuality.High);
            Object.Destroy(_root);
            VillageBootstrap.SavePathOverride = null;
            yield return null;
            foreach (var file in new[] { _savePath, _savePath + ".heroes" })
                if (File.Exists(file)) File.Delete(file);
        }

        [Test]
        public void EveryBuilding_HasPaintedArt_AtEveryArtLevel()
        {
            // Buildings are the owner's painted pictures (ADR-033); the code-drawn meshes are gone, so a missing
            // picture would leave a building invisible.
            foreach (var definition in Controller.Village.Catalog.All)
            {
                if (definition.IsWall)
                {
                    for (var level = 1; level <= BuildingSprites.ArtLevels; level++)
                    {
                        Assert.IsNotNull(BuildingSprites.Load($"wall_seg_x_{level:00}"), $"wall segment x, art level {level}");
                        Assert.IsNotNull(BuildingSprites.Load($"wall_seg_z_{level:00}"), $"wall segment z, art level {level}");
                        Assert.IsNotNull(BuildingSprites.Load($"wall_post_v2_{level:00}"), $"wall post, art level {level}");
                    }
                    continue;
                }
                for (var level = 1; level <= BuildingSprites.GameLevels; level++)
                    Assert.IsNotNull(BuildingSprites.For(definition.id, level), $"{definition.id} level {level}");
            }
        }

        [Test]
        public void Outside_IsMountain_WithNoHouses()
        {
            for (var x = 0; x <= 50; x += 5)
            for (var z = 0; z <= 50; z += 5)
                Assert.AreEqual(0f, VillageTerrain.Height(x, z), "The map must stay flat");

            // The ground just outside is level too, so the pond, stream and paths have somewhere to be.
            Assert.AreEqual(0f, VillageTerrain.Height(25f, 57f));
            Assert.AreEqual(0f, VillageTerrain.Height(-2f, 25f));

            // Behind the map it climbs. In front it drops, so it can never hide the village.
            Assert.Greater(VillageTerrain.Height(25f, 90f), 15f);
            Assert.Greater(VillageTerrain.Height(90f, 25f), 15f);
            Assert.Less(VillageTerrain.Height(25f, -14f), -8f);
            Assert.Less(VillageTerrain.Height(-14f, 25f), -8f);

            var scenery = _root.GetComponentsInChildren<MeshFilter>(true).Where(f => f.transform.parent != null && f.transform.parent.name == "Scenery").Select(f => f.name).OrderBy(n => n);
            CollectionAssert.AreEqual(new[] { "Ground", "Nature", "Water" }, scenery.ToArray());
        }

        [Test]
        public void NothingOutside_RisesInFrontOfTheMap()
        {
            var nature = _root.GetComponentsInChildren<MeshFilter>(true).First(f => f.name == "Nature").sharedMesh;
            foreach (var vertex in nature.vertices)
            {
                var inFront = (vertex.x < -3f || vertex.z < -3f) && Mathf.Abs(vertex.x - vertex.z) <= 50f;
                if (inFront) Assert.Less(vertex.y, 1.2f, $"Something at {vertex} would hide part of the village");
            }
        }

        [Test]
        public void AllVillageArt_SharesOneMaterial()
        {
            // Buildings, walls and scenery are sprites; one shared material lets them batch. Heroes have their own.
            var roots = new[] { "Buildings", "Walls", "Props" };
            var renderers = _root.GetComponentsInChildren<SpriteRenderer>()
                .Where(r => r.GetComponentInParent<HeroSpriteActor>() == null && r.transform.GetComponentsInParent<Transform>().Any(t => roots.Contains(t.name)))
                .ToArray();
            Assert.Greater(renderers.Length, 0, "The village shows painted sprites");
            var materials = renderers.Select(r => r.sharedMaterial).Distinct().ToArray();
            Assert.AreEqual(1, materials.Length, "Buildings, walls and scenery must batch on a single material");
            Assert.IsFalse(_root.GetComponentsInChildren<MeshRenderer>().Any(r => r.transform.GetComponentsInParent<Transform>().Any(t => t.name == "Buildings")),
                "No building is drawn as a mesh any more");
        }

        [Test]
        public void GridOverlay_IsHiddenUntilBuilding()
        {
            Assert.IsFalse(Controller.Grid.Visible);
        }

        [UnityTest]
        public IEnumerator LowQuality_StopsParticlesAndShadows_AndHighRestoresThem()
        {
            GameQuality.Set(VisualQuality.High);
            yield return null;
            var systems = _root.GetComponentsInChildren<ParticleSystem>();
            Assert.Greater(systems.Length, 0, "Key buildings should have effects at high quality");
            Assert.AreEqual(LightShadows.Soft, GameQuality.Sun.shadows);

            GameQuality.Set(VisualQuality.Low);
            yield return null;
            Assert.IsTrue(systems.All(s => !s.isPlaying));
            Assert.AreEqual(LightShadows.None, GameQuality.Sun.shadows);

            GameQuality.Set(VisualQuality.High);
            yield return null;
            Assert.IsTrue(systems.All(s => s.isPlaying));
            Assert.AreEqual(LightShadows.Soft, GameQuality.Sun.shadows);
        }

        [UnityTest]
        public IEnumerator Quality_DoesNotAffectTheVillage()
        {
            var before = Controller.Village.ToSave().ToJson();
            GameQuality.Set(VisualQuality.Low);
            yield return null;
            Assert.AreEqual(before, Controller.Village.ToSave().ToJson());
        }

        [UnityTest]
        public IEnumerator Decorations_NeverBlockThe50x50Grid()
        {
            // Art must not change what can be built: every free cell stays free.
            yield return null;
            var free = 0;
            for (var x = 0; x < VillageGrid.Size; x++)
            for (var z = 0; z < VillageGrid.Size; z++)
                if (Controller.Village.Grid.OccupantAt(new GridCoord(x, z)) == VillageGrid.NoOccupant && !Controller.Village.Grid.IsBlocked(new GridCoord(x, z)))
                    free++;
            var occupied = Controller.Village.Buildings.Sum(b => b.Footprint.Width * b.Footprint.Depth);
            Assert.AreEqual(VillageGrid.CellCount - occupied, free);
        }

        [Test]
        public void Camera_ZoomsOutFarEnoughToSeeTheWholeMap_AndNoFurther()
        {
            var iso = _root.GetComponentInChildren<IsoCamera>();
            iso.ZoomAt(100f, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            var size = iso.Camera.orthographicSize;
            Assert.LessOrEqual(size, 30f);
            // The map's diamond is about 41 world units tall on screen, so it must fit in the view.
            Assert.Greater(size * 2f, 41f);
            iso.ZoomAt(0.001f, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            Assert.GreaterOrEqual(iso.Camera.orthographicSize, 3.5f, "Zooming in stops at a close view of a few buildings (ADR-034)");
        }
    }
}

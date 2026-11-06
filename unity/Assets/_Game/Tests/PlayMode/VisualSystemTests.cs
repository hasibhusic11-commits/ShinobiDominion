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
        public void EveryBuilding_HasItsOwnModel_ThatIsNotJustABox()
        {
            var catalog = Controller.Village.Catalog;
            var sizes = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var definition in catalog.All)
            {
                var builder = BuildingMeshes.For(definition, null);
                Assert.Greater(builder.VertexCount, 40, definition.id + " should be real geometry");
                sizes[definition.id] = builder.VertexCount;
            }
            // Different vertex counts are a simple signal that models are not recoloured copies.
            Assert.Greater(sizes.Values.Distinct().Count(), catalog.All.Count - 2);
        }

        private static string ColourKey(Mesh mesh) =>
            string.Join(",", mesh.colors.Select(c => ColorUtility.ToHtmlStringRGB(c)).Distinct().OrderBy(s => s));

        [Test]
        public void EveryBuilding_LooksDifferentInEachOfItsFiveTiers()
        {
            foreach (var definition in Controller.Village.Catalog.All.Where(d => !d.IsWall))
            {
                var looks = new[] { 1, 4, 7, 10, 13 }.Select(level => ColourKey(BuildingMeshes.For(definition, null, level).Build("t"))).Distinct().Count();
                Assert.AreEqual(5, looks, definition.id);
                Assert.AreEqual(ColourKey(BuildingMeshes.For(definition, null, 13).Build("t")), ColourKey(BuildingMeshes.For(definition, null, 15).Build("t")), "Levels in one tier share a look");
            }
        }

        [Test]
        public void TwoBuildings_NeverShareATexturePack()
        {
            var ids = Controller.Village.Catalog.All.Select(d => d.id).ToList();
            for (var level = 1; level <= 13; level += 3)
            {
                var level0 = level;
                var roofs = ids.Select(id => ColorUtility.ToHtmlStringRGB(BuildingSkin.For(id, level0).Slate)).ToList();
                Assert.GreaterOrEqual(roofs.Distinct().Count(), ids.Count - 1, "level " + level);
            }
        }

        [Test]
        public void TierDressing_StaysInsideTheFootprint()
        {
            foreach (var definition in Controller.Village.Catalog.All.Where(d => !d.IsWall))
            {
                var plain = BuildingMeshes.For(definition, null, 1).Build("t").bounds;
                var dressed = BuildingMeshes.For(definition, null, 15).Build("t").bounds;
                // Some models already reach past their cells, such as the Dojo's front steps. Dressing must add nothing beyond that.
                Assert.GreaterOrEqual(dressed.min.x, Mathf.Min(plain.min.x, -definition.width * 0.5f) - 0.02f, definition.id);
                Assert.LessOrEqual(dressed.max.x, Mathf.Max(plain.max.x, definition.width * 0.5f) + 0.02f, definition.id);
                Assert.GreaterOrEqual(dressed.min.z, Mathf.Min(plain.min.z, -definition.depth * 0.5f) - 0.02f, definition.id);
                Assert.LessOrEqual(dressed.max.z, Mathf.Max(plain.max.z, definition.depth * 0.5f) + 0.02f, definition.id);
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
            var materials = _root.GetComponentsInChildren<MeshRenderer>()
                .Where(r => r.gameObject.name.Contains("_") || r.gameObject.name == "Walls")
                .SelectMany(r => r.sharedMaterials)
                .Distinct()
                .ToArray();
            Assert.AreEqual(1, materials.Length, "Buildings and walls must batch on a single material");
            Assert.AreEqual("ShinobiDominion/Toon", materials[0].shader.name);
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
            Assert.GreaterOrEqual(iso.Camera.orthographicSize, 5f);
        }
    }
}

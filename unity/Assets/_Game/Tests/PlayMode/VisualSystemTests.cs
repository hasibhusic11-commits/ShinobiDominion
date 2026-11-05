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

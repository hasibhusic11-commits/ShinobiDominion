using System.Linq;
using NUnit.Framework;
using ShinobiDominion.Core;

namespace ShinobiDominion.Tests
{
    public class SaveTests
    {
        private const long Now = 123456;

        private static Village BuildSample()
        {
            var village = TestCatalog.EmptyVillage();
            village.TryPlace("dojo_hq", new GridCoord(22, 22), Rotation.R0, Now, out _);
            village.TryPlace("training_grounds", new GridCoord(5, 5), Rotation.R90, Now, out _);
            village.TryPlace("hero_base", new GridCoord(30, 30), Rotation.R0, Now, out var baseId);
            village.TryAssignHero(baseId, "hero-uuid-1");
            village.TryUpgrade(baseId);
            village.TryPlaceWallRun(new GridCoord(0, 49), new GridCoord(10, 49), Now);
            return village;
        }

        [Test]
        public void SaveLoad_PreservesExactLayout()
        {
            var original = BuildSample();
            var json = original.ToSave().ToJson();
            var loaded = Village.FromSave(original.Catalog, VillageSave.FromJson(json));

            Assert.AreEqual(json, loaded.ToSave().ToJson(), "A second save must be byte-identical");
            Assert.AreEqual(original.Gold, loaded.Gold);
            for (var x = 0; x < VillageGrid.Size; x++)
            for (var z = 0; z < VillageGrid.Size; z++)
            {
                var cell = new GridCoord(x, z);
                Assert.AreEqual(original.Grid.OccupantAt(cell), loaded.Grid.OccupantAt(cell), $"Cell {cell}");
            }
        }

        [Test]
        public void SaveLoad_NewBuildingsDoNotReuseIds()
        {
            var original = BuildSample();
            var loaded = Village.FromSave(original.Catalog, VillageSave.FromJson(original.ToSave().ToJson()));
            loaded.TryPlace("defense_tower", new GridCoord(40, 5), Rotation.R0, Now, out var newId);
            Assert.AreEqual(1, loaded.Buildings.Count(b => b.Id == newId));
            Assert.Greater(newId, original.Buildings.Max(b => b.Id));
        }

        [Test]
        public void Undo_RestoresLayoutAndRefunds()
        {
            var village = BuildSample();
            var before = village.ToSave().ToJson();
            var history = new VillageHistory();

            history.Capture(village);
            village.TryPlaceWallRun(new GridCoord(0, 0), new GridCoord(5, 0), Now);
            Assert.AreNotEqual(before, village.ToSave().ToJson());

            Assert.IsTrue(history.Undo(village));
            Assert.AreEqual(before, village.ToSave().ToJson());
            Assert.IsFalse(history.Undo(village), "Nothing left to undo");
        }

        private static VillageSave ValidSave() => BuildSample().ToSave();

        [Test]
        public void Load_RejectsOutOfBounds()
        {
            var save = ValidSave();
            save.buildings[0].x = 50;
            Assert.Throws<VillageSaveException>(() => Village.FromSave(TestCatalog.Load(), save));
        }

        [Test]
        public void Load_RejectsOverlap()
        {
            var save = ValidSave();
            save.buildings[1].x = save.buildings[0].x;
            save.buildings[1].z = save.buildings[0].z;
            Assert.Throws<VillageSaveException>(() => Village.FromSave(TestCatalog.Load(), save));
        }

        [Test]
        public void Load_KeepsOldExtraHeroBases_ButOnlyTheFirstHero()
        {
            var save = ValidSave();
            for (var i = 0; i < 6; i++)
                save.buildings.Add(new BuildingSave { id = 1000 + i, definitionId = "hero_base", x = i * 3, z = 40, level = 1, assignedHeroInstanceId = "hero-" + i });
            save.nextInstanceId = 2000;
            var village = Village.FromSave(TestCatalog.Load(), save);
            var assigned = village.Buildings.Where(b => b.AssignedHeroInstanceId != null).ToList();
            Assert.AreEqual(1, assigned.Count, "Only one hero stays on a base");
            Assert.AreEqual(VillageError.LimitReached, village.TryPlace("hero_base", new GridCoord(30, 30), Rotation.R0, 0, out _), "No new bases past the limit");
        }

        [Test]
        public void Load_RejectsSameHeroOnTwoBases()
        {
            var save = ValidSave();
            save.buildings.Add(new BuildingSave { id = 1000, definitionId = "hero_base", x = 0, z = 40, level = 1, assignedHeroInstanceId = "hero-uuid-1" });
            save.nextInstanceId = 2000;
            Assert.Throws<VillageSaveException>(() => Village.FromSave(TestCatalog.Load(), save));
        }

        [TestCase(-1L)]
        [TestCase(long.MinValue)]
        public void Load_RejectsNegativeCurrency(long gold)
        {
            var save = ValidSave();
            save.gold = gold;
            Assert.Throws<VillageSaveException>(() => Village.FromSave(TestCatalog.Load(), save));
        }

        [Test]
        public void Load_RejectsWrongSchema_UnknownBuilding_AndIdCollision()
        {
            var wrongSchema = ValidSave();
            wrongSchema.schemaVersion = 99;
            Assert.Throws<VillageSaveException>(() => Village.FromSave(TestCatalog.Load(), wrongSchema));

            var unknown = ValidSave();
            unknown.buildings[0].definitionId = "not_a_building";
            Assert.Throws<VillageSaveException>(() => Village.FromSave(TestCatalog.Load(), unknown));

            var collision = ValidSave();
            collision.nextInstanceId = 1;
            Assert.Throws<VillageSaveException>(() => Village.FromSave(TestCatalog.Load(), collision));
        }

        [Test]
        public void Load_RejectsGarbageJson()
        {
            Assert.Throws<VillageSaveException>(() => VillageSave.FromJson("{ not json"));
        }

        [Test]
        public void Restore_WithCorruptSave_LeavesVillageUntouched()
        {
            var village = BuildSample();
            var before = village.ToSave().ToJson();
            var bad = village.ToSave();
            bad.buildings[0].x = 999;
            Assert.Throws<VillageSaveException>(() => village.Restore(bad));
            Assert.AreEqual(before, village.ToSave().ToJson());
        }

        [Test]
        public void StarterVillage_IsValid_AndFree()
        {
            var catalog = TestCatalog.Load();
            var village = StarterVillage.Create(catalog, Now);
            Assert.AreEqual(catalog.StartingGold, village.Gold);
            Assert.AreEqual(1, village.CountOf("dojo_hq"));
            Assert.AreEqual(1, village.CountOf("builder_hall"));
            Assert.AreEqual(1, village.CountOf("hero_altar"));
            Assert.LessOrEqual(village.CountOf("hero_base"), 6);
            Assert.DoesNotThrow(() => Village.FromSave(catalog, village.ToSave()));
        }
    }

    public class CatalogTests
    {
        [Test]
        public void ShippedCatalog_MatchesBriefFootprints()
        {
            var catalog = TestCatalog.Load();
            AssertSize(catalog, "dojo_hq", 4, 4);
            AssertSize(catalog, "builder_hall", 3, 3);
            AssertSize(catalog, "hero_altar", 3, 3);
            AssertSize(catalog, "hero_base", 2, 2);
            AssertSize(catalog, "defense_tower", 3, 3);
            AssertSize(catalog, "gold_mine", 3, 3);
            AssertSize(catalog, "gold_vault", 3, 3);
            AssertSize(catalog, "summoning_gate", 3, 3);
            // The owner's list (2026-11-09): Town Hall 4x4, Hero Base 2x2, Wall 1x1, every other building 3x3; traps are 1x1.
            foreach (var definition in catalog.All)
            {
                if (definition.id == "dojo_hq" || definition.id == "hero_base" || definition.IsWall || definition.IsTrap) continue;
                AssertSize(catalog, definition.id, 3, 3);
            }
            AssertSize(catalog, "bomb_trap", 1, 1);
            AssertSize(catalog, "hero_trap", 1, 1);
            AssertSize(catalog, "wall", 1, 1);
        }

        private static void AssertSize(BuildingCatalog catalog, string id, int width, int depth)
        {
            var definition = catalog.Get(id);
            Assert.AreEqual(width, definition.width, id);
            Assert.AreEqual(depth, definition.depth, id);
        }

        [Test]
        public void EveryBuilding_HasFifteenLevels_WithHpThatNeverFalls()
        {
            foreach (var definition in TestCatalog.Load().All)
            {
                Assert.AreEqual(15, definition.maxLevel, definition.id);
                for (var level = 2; level <= 15; level++)
                    Assert.GreaterOrEqual(definition.HpAtLevel(level), definition.HpAtLevel(level - 1), definition.id);
            }
        }

        [Test]
        public void Stats_FollowTheirCurves()
        {
            var catalog = TestCatalog.Load();
            var tower = catalog.Get("defense_tower");
            Assert.AreEqual(20, tower.At(2).attack - tower.At(1).attack, "Attack rises by a fixed amount");
            Assert.AreEqual(tower.At(2).attack - tower.At(1).attack, tower.At(15).attack - tower.At(14).attack);
            Assert.Greater(tower.At(15).range, tower.At(1).range);
            Assert.AreEqual(tower.At(1).range, tower.At(2).range, "Range rises in steps");
            Assert.Greater(tower.UpgradeCost(14) - tower.UpgradeCost(13), tower.UpgradeCost(2) - tower.UpgradeCost(1), "Cost accelerates");
            Assert.Greater(catalog.Get("wall").At(15).hp / catalog.Get("wall").At(1).hp, 100, "Walls grow fastest");
            Assert.Greater(catalog.Get("gold_mine").ProductionAt(15), catalog.Get("gold_mine").ProductionAt(1));
            Assert.Greater(catalog.Get("gold_vault").At(15).goldCapacity, catalog.Get("gold_vault").At(1).goldCapacity);
            Assert.AreEqual(2, catalog.Get("hero_base").At(1).bonusPercent);
            Assert.AreEqual(30, catalog.Get("hero_base").At(15).bonusPercent);
        }

        [Test]
        public void Catalog_RejectsWrongLevelCount()
        {
            Assert.Throws<System.FormatException>(() => BuildingCatalog.FromJson("{\"schemaVersion\":2,\"buildings\":[{\"id\":\"a\",\"levels\":[{\"hp\":1}]}]}"));
        }

        [Test]
        public void HeroBaseLimit_IsOneAtEveryHqLevel()
        {
            var heroBase = TestCatalog.Load().Get("hero_base");
            for (var level = 1; level <= 15; level++)
                Assert.AreEqual(1, heroBase.MaxCountAt(level));
        }

        [Test]
        public void ShippedCatalog_IsMarkedAsNotApproved()
        {
            StringAssert.Contains("NOT APPROVED", TestCatalog.Load().BalanceStatus);
        }

        [Test]
        public void NoCatalogEntry_MentionsEnergyOrStamina()
        {
            var text = TestCatalog.Json.ToLowerInvariant();
            StringAssert.DoesNotContain("energy", text);
            StringAssert.DoesNotContain("stamina", text);
        }

        [Test]
        public void Catalog_RejectsDuplicateIdsAndBadFootprints()
        {
            Assert.Throws<System.FormatException>(() => BuildingCatalog.FromJson(
                "{\"schemaVersion\":2,\"buildings\":[{\"id\":\"a\"},{\"id\":\"a\"}]}"));
            Assert.Throws<System.FormatException>(() => BuildingCatalog.FromJson(
                "{\"schemaVersion\":2,\"buildings\":[{\"id\":\"a\",\"width\":0}]}"));
        }
    }

    public class NavigationTests
    {
        private static BuildingInstance Instance(BuildingFunction function) => new BuildingInstance
        {
            Id = 7,
            Definition = new BuildingDefinition { id = "x", function = function, width = 1, depth = 1 }
        };

        [Test]
        public void Home_ShowsAttack_NoCatalog_NoGrid()
        {
            var ui = new VillageUiState();
            Assert.IsTrue(ui.IsAttackVisible);
            Assert.IsFalse(ui.IsCatalogVisible);
            Assert.IsFalse(ui.IsGridOverlayVisible);
        }

        [Test]
        public void Catalog_CannotOpenFromHome()
        {
            var ui = new VillageUiState();
            Assert.IsFalse(ui.TryOpenCatalog());
            Assert.AreEqual(VillageScreen.Home, ui.Screen);
        }

        [TestCase(BuildingFunction.Headquarters)]
        [TestCase(BuildingFunction.HeroAltar)]
        [TestCase(BuildingFunction.HeroBase)]
        [TestCase(BuildingFunction.SummoningGate)]
        [TestCase(BuildingFunction.Wall)]
        public void Catalog_CannotOpenFromOtherBuildings(BuildingFunction function)
        {
            var ui = new VillageUiState();
            ui.SelectBuilding(Instance(function));
            Assert.IsFalse(ui.TryOpenCatalog());
            Assert.IsFalse(ui.IsCatalogVisible);
        }

        [Test]
        public void Catalog_OpensOnlyFromBuilderHall_AndDisappearsOnExit()
        {
            var ui = new VillageUiState();
            ui.SelectBuilding(Instance(BuildingFunction.BuilderHall));
            Assert.IsTrue(ui.TryOpenCatalog());
            Assert.IsTrue(ui.IsCatalogVisible);
            ui.CloseToHome();
            Assert.IsFalse(ui.IsCatalogVisible);
            Assert.IsFalse(ui.TryOpenCatalog(), "Exit must drop the Builder Hall context");
        }

        [Test]
        public void GridOverlay_VisibleOnlyDuringPlacement()
        {
            var ui = new VillageUiState();
            ui.SelectBuilding(Instance(BuildingFunction.BuilderHall));
            ui.TryOpenCatalog();
            Assert.IsFalse(ui.IsGridOverlayVisible);
            Assert.IsTrue(ui.TryBeginPlacement(false));
            Assert.IsTrue(ui.IsGridOverlayVisible);
            Assert.IsFalse(ui.IsAttackVisible);
            ui.EndPlacement();
            Assert.IsFalse(ui.IsGridOverlayVisible);
            Assert.IsTrue(ui.IsCatalogVisible);
        }

        [Test]
        public void Placement_CannotStartWithoutCatalog()
        {
            var ui = new VillageUiState();
            Assert.IsFalse(ui.TryBeginPlacement(false));
            Assert.IsFalse(ui.TryBeginPlacement(true));
        }

        [Test]
        public void Move_EndsBackAtHome_WithGridHidden()
        {
            var ui = new VillageUiState();
            ui.SelectBuilding(Instance(BuildingFunction.HeroBase));
            Assert.IsTrue(ui.TryBeginMove());
            Assert.IsTrue(ui.IsGridOverlayVisible);
            ui.EndMove();
            Assert.AreEqual(VillageScreen.Home, ui.Screen);
            Assert.IsFalse(ui.IsGridOverlayVisible);
        }

        [Test]
        public void TapsDuringPlacement_CannotOpenBuildingPanels()
        {
            var ui = new VillageUiState();
            ui.SelectBuilding(Instance(BuildingFunction.BuilderHall));
            ui.TryOpenCatalog();
            ui.TryBeginPlacement(true);
            ui.SelectBuilding(Instance(BuildingFunction.HeroBase));
            Assert.AreEqual(VillageScreen.PaintingWalls, ui.Screen);
        }
    }
}

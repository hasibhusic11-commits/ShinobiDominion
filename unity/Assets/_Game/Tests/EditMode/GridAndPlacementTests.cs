using System.Linq;
using NUnit.Framework;
using ShinobiDominion.Core;

namespace ShinobiDominion.Tests
{
    public class GridTests
    {
        [Test]
        public void Grid_HasExactly2500ValidCells()
        {
            var count = 0;
            for (var x = -5; x < 60; x++)
            for (var z = -5; z < 60; z++)
                if (VillageGrid.InBounds(x, z)) count++;
            Assert.AreEqual(2500, count);
            Assert.AreEqual(2500, VillageGrid.CellCount);
        }

        [TestCase(0, 0, true)]
        [TestCase(49, 49, true)]
        [TestCase(50, 0, false)]
        [TestCase(0, 50, false)]
        [TestCase(-1, 0, false)]
        [TestCase(0, -1, false)]
        public void Grid_BoundaryCells(int x, int z, bool expected)
        {
            Assert.AreEqual(expected, VillageGrid.InBounds(x, z));
        }

        [Test]
        public void Footprint_RotationSwapsWidthAndDepth()
        {
            var training = TestCatalog.Load().Get("training_grounds");
            var flat = Footprint.Of(training, new GridCoord(0, 0), Rotation.R0);
            var turned = Footprint.Of(training, new GridCoord(0, 0), Rotation.R90);
            Assert.AreEqual(4, flat.Width);
            Assert.AreEqual(3, flat.Depth);
            Assert.AreEqual(3, turned.Width);
            Assert.AreEqual(4, turned.Depth);
        }
    }

    public class PlacementTests
    {
        private const long Now = 1000;

        [Test]
        public void Footprint_CannotOverrunMapEdge()
        {
            var village = TestCatalog.EmptyVillage();
            Assert.AreEqual(VillageError.None, village.TryPlace("hero_base", new GridCoord(48, 48), Rotation.R0, Now, out _));
            Assert.AreEqual(VillageError.OutOfBounds, village.TryPlace("hero_base", new GridCoord(49, 49), Rotation.R0, Now, out _));
            Assert.AreEqual(VillageError.OutOfBounds, village.TryPlace("hero_base", new GridCoord(50, 0), Rotation.R0, Now, out _));
            Assert.AreEqual(VillageError.OutOfBounds, village.TryPlace("hero_base", new GridCoord(-1, 0), Rotation.R0, Now, out _));
        }

        [Test]
        public void OneByOneWall_FitsInLastCell()
        {
            var village = TestCatalog.EmptyVillage();
            Assert.AreEqual(VillageError.None, village.TryPlaceWallRun(new GridCoord(49, 49), new GridCoord(49, 49), Now));
            Assert.AreEqual(VillageError.OutOfBounds, village.TryPlaceWallRun(new GridCoord(50, 0), new GridCoord(50, 0), Now));
        }

        [Test]
        public void Tower_CannotOverlapWallOrBuilding()
        {
            var village = TestCatalog.EmptyVillage();
            village.TryPlaceWallRun(new GridCoord(10, 10), new GridCoord(10, 10), Now);
            Assert.AreEqual(VillageError.Overlap, village.TryPlace("defense_tower", new GridCoord(9, 9), Rotation.R0, Now, out _));
            Assert.AreEqual(VillageError.None, village.TryPlace("defense_tower", new GridCoord(20, 20), Rotation.R0, Now, out _));
            Assert.AreEqual(VillageError.Overlap, village.TryPlace("defense_tower", new GridCoord(21, 21), Rotation.R0, Now, out _));
        }

        [Test]
        public void FailedPlacement_SpendsNothing()
        {
            var village = TestCatalog.EmptyVillage();
            village.TryPlace("defense_tower", new GridCoord(20, 20), Rotation.R0, Now, out _);
            var gold = village.Gold;
            var count = village.BuildingCount;
            village.TryPlace("defense_tower", new GridCoord(20, 20), Rotation.R0, Now, out _);
            Assert.AreEqual(gold, village.Gold);
            Assert.AreEqual(count, village.BuildingCount);
        }

        [Test]
        public void Placement_RejectsWhenTooPoor()
        {
            var village = TestCatalog.EmptyVillage();
            var save = village.ToSave();
            save.nextInstanceId = 1;
            save.gold = 0;
            village.Restore(save);
            Assert.AreEqual(VillageError.InsufficientFunds, village.TryPlace("defense_tower", new GridCoord(1, 1), Rotation.R0, Now, out _));
            Assert.AreEqual(0, village.BuildingCount);
        }

        [Test]
        public void HeroBases_ExactlySixAllowed_EachIndependentlyPlaced()
        {
            var village = TestCatalog.EmptyVillage();
            for (var i = 0; i < 6; i++)
                Assert.AreEqual(VillageError.None, village.TryPlace("hero_base", new GridCoord(i * 3, 0), Rotation.R0, Now, out _));
            Assert.AreEqual(VillageError.LimitReached, village.TryPlace("hero_base", new GridCoord(30, 30), Rotation.R0, Now, out _));
            Assert.AreEqual(6, village.CountOf("hero_base"));
        }

        [Test]
        public void Move_ValidatesAndFreesOldCells()
        {
            var village = TestCatalog.EmptyVillage();
            village.TryPlace("hero_base", new GridCoord(5, 5), Rotation.R0, Now, out var id);
            village.TryPlace("defense_tower", new GridCoord(10, 10), Rotation.R0, Now, out _);

            Assert.AreEqual(VillageError.Overlap, village.TryMove(id, new GridCoord(10, 10), Rotation.R0));
            Assert.AreEqual(new GridCoord(5, 5), village.Find(id).Origin);

            Assert.AreEqual(VillageError.None, village.TryMove(id, new GridCoord(6, 6), Rotation.R0));
            Assert.IsNull(village.BuildingAt(new GridCoord(5, 5)));
            Assert.AreEqual(id, village.BuildingAt(new GridCoord(7, 7)).Id);
        }

        [Test]
        public void Rotate_RevalidatesFootprint_AndRespectsRotatableFlag()
        {
            var village = TestCatalog.EmptyVillage();
            village.TryPlace("training_grounds", new GridCoord(0, 47), Rotation.R0, Now, out var id);
            Assert.AreEqual(VillageError.OutOfBounds, village.TryMove(id, new GridCoord(0, 47), Rotation.R90));
            Assert.AreEqual(VillageError.None, village.TryMove(id, new GridCoord(0, 46), Rotation.R90));
            Assert.AreEqual(new GridCoord(2, 49), village.Find(id).Footprint.Cells().Last());

            village.TryPlace("hero_base", new GridCoord(20, 20), Rotation.R0, Now, out var baseId);
            Assert.AreEqual(VillageError.NotRotatable, village.TryMove(baseId, new GridCoord(20, 20), Rotation.R90));
        }

        [Test]
        public void EssentialBuildings_CannotBeRemoved()
        {
            var village = TestCatalog.EmptyVillage();
            village.TryPlace("builder_hall", new GridCoord(1, 1), Rotation.R0, Now, out var id);
            Assert.AreEqual(VillageError.NotRemovable, village.TryRemove(id));
        }

        [Test]
        public void Upgrade_ChargesAndStopsAtMax()
        {
            var village = TestCatalog.EmptyVillage();
            village.TryPlace("hero_base", new GridCoord(1, 1), Rotation.R0, Now, out var id);
            var before = village.Gold;
            var cost = village.Find(id).Definition.UpgradeCost(1);
            Assert.AreEqual(VillageError.None, village.TryUpgrade(id));
            Assert.AreEqual(2, village.Find(id).Level);
            Assert.AreEqual(before - cost, village.Gold);

            var save = village.ToSave();
            save.gold = 10_000_000;
            save.buildings[0].level = village.Find(id).Definition.maxLevel;
            village.Restore(save);
            Assert.AreEqual(VillageError.MaxLevel, village.TryUpgrade(id));
        }

        [Test]
        public void HeroBase_OneHeroCannotOccupyTwoBases_SixLegendaryIdsAllowed()
        {
            var village = TestCatalog.EmptyVillage();
            var ids = new int[6];
            for (var i = 0; i < 6; i++)
            {
                village.TryPlace("hero_base", new GridCoord(i * 3, 0), Rotation.R0, Now, out ids[i]);
                Assert.AreEqual(VillageError.None, village.TryAssignHero(ids[i], "legendary-" + i));
            }
            Assert.AreEqual(VillageError.HeroAlreadyAssigned, village.TryAssignHero(ids[1], "legendary-0"));
            Assert.AreEqual(VillageError.None, village.TryUnassignHero(ids[0]));
            Assert.AreEqual(VillageError.None, village.TryAssignHero(ids[1], "legendary-0"));
        }

        [Test]
        public void HeroCannotBeAssignedToNonBase()
        {
            var village = TestCatalog.EmptyVillage();
            village.TryPlace("defense_tower", new GridCoord(1, 1), Rotation.R0, Now, out var id);
            Assert.AreEqual(VillageError.NotAHeroBase, village.TryAssignHero(id, "hero"));
        }

        [Test]
        public void Collector_AccruesToCap_AndRespectsStorage()
        {
            var village = TestCatalog.EmptyVillage();
            village.TryPlace("gold_mine", new GridCoord(1, 1), Rotation.R0, Now, out var id);
            var mine = village.Find(id);
            Assert.AreEqual(VillageError.NothingToCollect, village.TryCollect(id, Now, out _));

            var oneHour = Now + System.TimeSpan.TicksPerHour;
            Assert.AreEqual(mine.Definition.ProductionAt(1), village.PendingCollection(mine, oneHour));

            var longTime = Now + 1000 * System.TimeSpan.TicksPerHour;
            Assert.AreEqual(mine.Definition.StoredCapAt(1), village.PendingCollection(mine, longTime));

            var goldBefore = village.Gold;
            Assert.AreEqual(VillageError.None, village.TryCollect(id, longTime, out var collected));
            Assert.AreEqual(mine.Definition.StoredCapAt(1), collected);
            Assert.AreEqual(goldBefore + collected, village.Gold);
            Assert.AreEqual(0, village.PendingCollection(mine, longTime));
        }

        [Test]
        public void Collector_KeepsWhatDoesNotFitInStorage()
        {
            var village = TestCatalog.EmptyVillage();
            village.TryPlace("gold_mine", new GridCoord(1, 1), Rotation.R0, Now, out var id);
            var save = village.ToSave();
            save.gold = village.GoldCapacity - 100;
            village.Restore(save);

            var later = Now + 1000 * System.TimeSpan.TicksPerHour;
            var mine = village.Find(id);
            var pending = village.PendingCollection(mine, later);
            Assert.AreEqual(VillageError.None, village.TryCollect(id, later, out var collected));
            Assert.AreEqual(100, collected);
            Assert.AreEqual(village.GoldCapacity, village.Gold);
            Assert.AreEqual(pending - 100, village.PendingCollection(mine, later));
            Assert.AreEqual(VillageError.StorageFull, village.TryCollect(id, later, out _));
        }
    }
}

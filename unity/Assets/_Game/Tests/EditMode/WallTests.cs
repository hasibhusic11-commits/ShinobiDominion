using System.Linq;
using NUnit.Framework;
using ShinobiDominion.Core;

namespace ShinobiDominion.Tests
{
    public class WallTests
    {
        private const long Now = 1000;

        [Test]
        public void DiagonalDrag_NeverProducesDiagonalCells()
        {
            for (var x = 0; x < 12; x++)
            for (var z = 0; z < 12; z++)
            {
                var cells = Village.WallRunCells(new GridCoord(5, 5), new GridCoord(x, z));
                Assert.That(cells.Select(c => c.X).Distinct().Count() == 1 || cells.Select(c => c.Z).Distinct().Count() == 1,
                    $"Run to ({x},{z}) is not a straight line");
                for (var i = 1; i < cells.Count; i++)
                {
                    var step = System.Math.Abs(cells[i].X - cells[i - 1].X) + System.Math.Abs(cells[i].Z - cells[i - 1].Z);
                    Assert.AreEqual(1, step, "Cells in a run must be orthogonally adjacent");
                }
            }
        }

        [Test]
        public void Run_LocksToLongerAxis()
        {
            var cells = Village.WallRunCells(new GridCoord(0, 0), new GridCoord(5, 2));
            Assert.AreEqual(6, cells.Count);
            Assert.That(cells.All(c => c.Z == 0));

            cells = Village.WallRunCells(new GridCoord(10, 10), new GridCoord(8, 3));
            Assert.AreEqual(8, cells.Count);
            Assert.That(cells.All(c => c.X == 10));
            Assert.AreEqual(new GridCoord(10, 3), cells.Last());
        }

        [Test]
        public void Run_CostIsSumOfSegments_AndChargedOnce()
        {
            var village = TestCatalog.EmptyVillage();
            var wall = village.Catalog.Get(Village.WallId);
            var preview = village.PreviewWallRun(new GridCoord(0, 0), new GridCoord(9, 0));
            Assert.AreEqual(10 * wall.costGold, preview.TotalGold);

            var before = village.Gold;
            Assert.AreEqual(VillageError.None, village.TryPlaceWallRun(new GridCoord(0, 0), new GridCoord(9, 0), Now));
            Assert.AreEqual(before - preview.TotalGold, village.Gold);
            Assert.AreEqual(10, village.CountOf(Village.WallId));
        }

        [Test]
        public void Run_IsAtomic_OneBlockedCellPlacesNothing()
        {
            var village = TestCatalog.EmptyVillage();
            village.TryPlace("defense_tower", new GridCoord(5, 0), Rotation.R0, Now, out _);
            var gold = village.Gold;
            var preview = village.PreviewWallRun(new GridCoord(0, 0), new GridCoord(9, 0));

            Assert.AreEqual(VillageError.Overlap, preview.Error);
            Assert.AreEqual(3, preview.Cells.Count(c => !c.IsValid), "Only the three tower cells on this row are red");
            Assert.AreEqual(VillageError.Overlap, village.TryPlaceWallRun(new GridCoord(0, 0), new GridCoord(9, 0), Now));
            Assert.AreEqual(gold, village.Gold);
            Assert.AreEqual(0, village.CountOf(Village.WallId));
        }

        [Test]
        public void Run_IsAtomic_PastMapEdgePlacesNothing()
        {
            var village = TestCatalog.EmptyVillage();
            Assert.AreEqual(VillageError.OutOfBounds, village.TryPlaceWallRun(new GridCoord(45, 0), new GridCoord(50, 0), Now));
            Assert.AreEqual(0, village.CountOf(Village.WallId));
        }

        [Test]
        public void Run_RollsBack_WhenTooExpensive()
        {
            var village = TestCatalog.EmptyVillage();
            var save = village.ToSave();
            save.gold = village.Catalog.Get(Village.WallId).costGold * 3;
            save.nextInstanceId = 1;
            village.Restore(save);

            Assert.AreEqual(VillageError.InsufficientFunds, village.TryPlaceWallRun(new GridCoord(0, 0), new GridCoord(9, 0), Now));
            Assert.AreEqual(save.gold, village.Gold);
            Assert.AreEqual(0, village.CountOf(Village.WallId));
        }

        [Test]
        public void Run_RespectsWallLimit()
        {
            var village = TestCatalog.EmptyVillage();
            var limit = village.LimitOf(village.Catalog.Get(Village.WallId));
            Assert.Less(limit, 2500);
            var placed = 0;
            for (var z = 0; placed + 50 <= limit; z++, placed += 50)
                Assert.AreEqual(VillageError.None, village.TryPlaceWallRun(new GridCoord(0, z), new GridCoord(49, z), Now));
            var remaining = limit - placed;
            var row = placed / 50 + 1;
            Assert.AreEqual(VillageError.LimitReached,
                village.PreviewWallRun(new GridCoord(0, row), new GridCoord(remaining, row)).Error);
        }

        [Test]
        public void Walls_JoinOrthogonally_WithCorrectMasks()
        {
            var village = TestCatalog.EmptyVillage();
            village.TryPlaceWallRun(new GridCoord(10, 10), new GridCoord(12, 10), Now);
            village.TryPlaceWallRun(new GridCoord(12, 11), new GridCoord(12, 12), Now);

            Assert.AreEqual(Village.WallEast, village.WallConnectionMask(new GridCoord(10, 10)), "West end");
            Assert.AreEqual(Village.WallEast | Village.WallWest, village.WallConnectionMask(new GridCoord(11, 10)), "Straight");
            Assert.AreEqual(Village.WallWest | Village.WallNorth, village.WallConnectionMask(new GridCoord(12, 10)), "Corner");
            Assert.AreEqual(Village.WallSouth, village.WallConnectionMask(new GridCoord(12, 12)), "North end");
        }

        [Test]
        public void Walls_DoNotConnectDiagonally_OrToBuildings()
        {
            var village = TestCatalog.EmptyVillage();
            village.TryPlaceWallRun(new GridCoord(10, 10), new GridCoord(10, 10), Now);
            village.TryPlaceWallRun(new GridCoord(11, 11), new GridCoord(11, 11), Now);
            village.TryPlace("hero_base", new GridCoord(8, 10), Rotation.R0, Now, out _);
            Assert.AreEqual(0, village.WallConnectionMask(new GridCoord(10, 10)));
        }

        [Test]
        public void WallAtMapCorner_HasNoOutOfBoundsNeighbours()
        {
            var village = TestCatalog.EmptyVillage();
            village.TryPlaceWallRun(new GridCoord(49, 49), new GridCoord(49, 49), Now);
            Assert.AreEqual(0, village.WallConnectionMask(new GridCoord(49, 49)));
        }

        [Test]
        public void ConnectedGroup_FollowsCorners_UpgradesAtomically()
        {
            var village = TestCatalog.EmptyVillage();
            village.TryPlaceWallRun(new GridCoord(0, 0), new GridCoord(4, 0), Now);
            village.TryPlaceWallRun(new GridCoord(4, 1), new GridCoord(4, 3), Now);
            village.TryPlaceWallRun(new GridCoord(20, 20), new GridCoord(21, 20), Now);

            var first = village.BuildingAt(new GridCoord(0, 0));
            Assert.AreEqual(8, village.ConnectedWalls(first.Id).Count);

            var cost = first.Definition.UpgradeCost(1) * 8;
            var gold = village.Gold;
            Assert.AreEqual(VillageError.None, village.TryUpgradeWallGroup(first.Id));
            Assert.AreEqual(gold - cost, village.Gold);
            Assert.AreEqual(2, village.BuildingAt(new GridCoord(4, 3)).Level);
            Assert.AreEqual(1, village.BuildingAt(new GridCoord(20, 20)).Level, "Separate group untouched");
        }

        [Test]
        public void GroupUpgrade_TooPoor_ChangesNothing()
        {
            var village = TestCatalog.EmptyVillage();
            village.TryPlaceWallRun(new GridCoord(0, 0), new GridCoord(9, 0), Now);
            var save = village.ToSave();
            save.gold = 1;
            village.Restore(save);

            var id = village.BuildingAt(new GridCoord(0, 0)).Id;
            Assert.AreEqual(VillageError.InsufficientFunds, village.TryUpgradeWallGroup(id));
            Assert.AreEqual(1, village.Gold);
            Assert.That(village.ConnectedWalls(id).All(w => w.Level == 1));
        }

        [Test]
        public void Wall_CannotBeRemoved_OnlyMoved()
        {
            var village = TestCatalog.EmptyVillage();
            village.TryPlaceWallRun(new GridCoord(3, 3), new GridCoord(3, 3), Now);
            Assert.AreEqual(VillageError.NotRemovable, village.TryRemove(village.BuildingAt(new GridCoord(3, 3)).Id), "ADR-051");
            Assert.IsNotNull(village.BuildingAt(new GridCoord(3, 3)));
        }
    }
}

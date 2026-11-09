using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShinobiDominion.Core;

namespace ShinobiDominion.Tests
{
    /// <summary>Hero gear maths (ADR-044). The same numbers are checked in Go, so client and server agree.</summary>
    public class GearTests
    {
        private static HeroCatalog Catalog()
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Config", "heroes.json"),
                Path.Combine("Assets", "_Game", "Resources", "Config", "heroes.json")
            };
            return HeroCatalog.FromJson(File.ReadAllText(candidates.First(File.Exists)));
        }

        [Test]
        public void Gear_RaisesTheRightStat()
        {
            var catalog = Catalog();
            var stats = HeroMath.WithGear(new HeroStats(1000, 200, 50), new[] { 2, 4, 1 }, catalog.Gear.percentPerLevel);
            Assert.AreEqual(1200, stats.Hp, "Armor raises HP");
            Assert.AreEqual(220, stats.Attack, "Weapon raises attack");
            Assert.AreEqual(52, stats.Defense, "Charm raises defense, rounding down");
        }

        [Test]
        public void Gear_CostsAndNeeds_MatchTheServer()
        {
            var gear = Catalog().Gear;
            Assert.AreEqual(4500, gear.Gold(3));
            Assert.AreEqual(1350, gear.Mana(3));
            Assert.AreEqual(41, gear.HeroLevel(3));
        }

        [Test]
        public void Roster_RefusesGearOutOfRange()
        {
            var catalog = Catalog();
            var json = "{\"schemaVersion\":1,\"heroes\":[{\"instanceId\":\"a\",\"archetypeId\":\"ember_recruit\",\"evolutionIndex\":0,\"level\":1,\"gear\":[99,0,0]}]}";
            Assert.Throws<VillageSaveException>(() => HeroRoster.FromJson(catalog, json));
        }
    }
}

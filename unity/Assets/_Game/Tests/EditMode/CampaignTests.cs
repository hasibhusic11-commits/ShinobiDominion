using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShinobiDominion.Core;

namespace ShinobiDominion.Tests
{
    public class CampaignTests
    {
        private static string FindFile(string name, string assetsFolder)
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Config", name),
                Path.Combine("Assets", "_Game", assetsFolder, name)
            };
            return File.ReadAllText(candidates.First(File.Exists));
        }

        private static RaidCatalog Raids() => RaidCatalog.FromJson(FindFile("raids.json", "Resources/Config"), TestCatalog.Load());
        private static HeroCatalog Heroes() => HeroCatalog.FromJson(FindFile("heroes.json", "Resources/Config"));

        /// <summary>The army a player would have by this stage: camps grow with the Dojo.</summary>
        public static int ArmyFor(int stage) => Math.Min(160, 10 + 10 * stage);

        /// <summary>One hero at a level fitting the stage, landing with a militia army, as a player would.</summary>
        private static List<BattleHeroInput> Team(HeroCatalog heroes, int level, int army, int evolution = 0, string heroId = "solar_beast_sage")
        {
            var hero = heroes.Get(heroId);
            var stats = HeroMath.Stats(hero, evolution, level);
            var team = new List<BattleHeroInput> { new BattleHeroInput { id = "h0", role = hero.role.ToString(), hp = stats.Hp, attack = stats.Attack, defense = stats.Defense, x = 3, z = 3 } };
            for (var i = 0; i < army; i++)
                team.Add(new BattleHeroInput { id = "t" + i, role = "Fighter", hp = 110, attack = 14, defense = 5, isTroop = true, x = 2 + i % 10, z = 2 + i / 10 % 3 });
            return team;
        }

        private static BattleResult Fight(RaidDefinition raid, HeroCatalog heroes, int level, int evolution = 0) => Fight(raid, heroes, level, ArmyFor(raid.stage), evolution);

        private static BattleResult Fight(RaidDefinition raid, HeroCatalog heroes, int level, int army, int evolution, string heroId = "solar_beast_sage")
        {
            var catalog = TestCatalog.Load();
            var guards = raid.ExpandDefenders(catalog, heroes).Select(d => new BattleDefenderInput { id = d.id, role = d.role, hp = d.hp, attack = d.attack, defense = d.defense, x = d.x, z = d.z });
            return BattleSim.Run(raid.Expand(catalog), Team(heroes, level, army, evolution, heroId), guards);
        }

        [Test]
        public void Campaign_HasFifteenStages_InOrder_EachRequiringTheOneBefore()
        {
            var raids = Raids().All;
            Assert.AreEqual(15, raids.Count);
            for (var i = 0; i < raids.Count; i++)
                Assert.AreEqual(i + 1, raids[i].stage, raids[i].id);
            Assert.AreEqual("", raids[0].requires);
            Assert.AreEqual("", raids[1].requires);
            for (var i = 2; i < raids.Count; i++)
                Assert.AreEqual(raids[i - 1].id, raids[i].requires, raids[i].id);
        }

        [Test]
        public void EveryStage_HasGuardsOnRealHeroBases_AndAReward()
        {
            var catalog = TestCatalog.Load();
            var heroes = Heroes();
            foreach (var raid in Raids().All.Where(r => r.stage >= 2))
            {
                var buildings = raid.Expand(catalog);
                var guards = raid.ExpandDefenders(catalog, heroes);
                Assert.AreEqual(1, guards.Count, raid.id + " should have exactly one guard, like any village");
                Assert.Greater(raid.xpPerWin, 0, raid.id);
                Assert.Greater(raid.goldPerStar, 0, raid.id);
                foreach (var guard in guards)
                    Assert.IsTrue(buildings.Any(b => b.definitionId == "hero_base" && guard.x >= b.x && guard.x < b.x + b.width && guard.z >= b.z && guard.z < b.z + b.depth), $"{raid.id} {guard.id}");
            }
        }

        [Test]
        public void ChapterBosses_GiveTheThreeCatalysts_InEvolutionOrder()
        {
            var raids = Raids().All;
            var catalysts = Heroes().CatalystByTransition;
            Assert.AreEqual(catalysts[0], raids[4].firstClearItem);
            Assert.AreEqual(catalysts[1], raids[9].firstClearItem);
            Assert.AreEqual(catalysts[2], raids[14].firstClearItem);
            Assert.AreEqual(3, raids.Count(r => !string.IsNullOrEmpty(r.firstClearItem)));
        }

        [Test]
        public void Difficulty_RisesAcrossTheCampaign()
        {
            var catalog = TestCatalog.Load();
            var heroes = Heroes();
            var raids = Raids().All;
            long Power(RaidDefinition raid) =>
                raid.Expand(catalog).Where(b => !b.isWall).Sum(b => b.hp + b.attack * 20) + raid.ExpandDefenders(catalog, heroes).Sum(d => d.hp + d.attack * 20);
            Assert.Greater(Power(raids[9]), Power(raids[4]));
            Assert.Greater(Power(raids[14]), Power(raids[9]));
        }

        [TestCase("ridge_outpost", 1)]
        [TestCase("cinder_garrison", 10)]
        [TestCase("stage_03", 25)]
        [TestCase("stage_05", 50)]
        [TestCase("stage_10", 110)]
        [TestCase("stage_15", 200)]
        public void TeamAtTheExpectedLevel_CanClearTheStage(string id, int level)
        {
            var result = Fight(Raids().Find(id), Heroes(), level);
            Assert.GreaterOrEqual(result.stars, 1, $"{id} at level {level}: damage {result.damagePercent}, alive {result.heroesAlive}");
        }

        [TestCase("stage_05")]
        [TestCase("stage_10")]
        [TestCase("stage_15")]
        public void LevelOneTeam_CannotClearALaterStage(string id)
        {
            Assert.Less(Fight(Raids().Find(id), Heroes(), 1).stars, 3, id);
        }

        [TestCase("stage_05")]
        [TestCase("stage_10")]
        [TestCase("stage_15")]
        public void ArmyAlone_WithAWeakHero_DoesNotThreeStarTheStage(string id)
        {
            var raid = Raids().Find(id);
            Assert.Less(Fight(raid, Heroes(), 1, ArmyFor(raid.stage), 0, "river_scout").stars, 3, id);
        }
    }
}

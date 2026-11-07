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

        /// <summary>Six level-appropriate legends landing together, as a player would have them.</summary>
        private static List<BattleHeroInput> Team(HeroCatalog heroes, int level, int evolution = 0)
        {
            var ids = new[] { "stone_titan", "solar_beast_sage", "solar_beast_sage", "thunder_empress", "moonfang_oracle", "lantern_warden" };
            return ids.Select((id, i) =>
            {
                var hero = heroes.Get(id);
                var stats = HeroMath.Stats(hero, evolution, level);
                return new BattleHeroInput { id = "h" + i, role = hero.role.ToString(), hp = stats.Hp, attack = stats.Attack, defense = stats.Defense, x = 3 + i, z = 3 };
            }).ToList();
        }

        private static BattleResult Fight(RaidDefinition raid, HeroCatalog heroes, int level, int evolution = 0)
        {
            var catalog = TestCatalog.Load();
            var guards = raid.ExpandDefenders(catalog, heroes).Select(d => new BattleDefenderInput { id = d.id, role = d.role, hp = d.hp, attack = d.attack, defense = d.defense, x = d.x, z = d.z });
            return BattleSim.Run(raid.Expand(catalog), Team(heroes, level, evolution), guards);
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
                Assert.Greater(guards.Count, 0, raid.id);
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

        [Test]
        public void SixSameRoleLegends_DoNotTriviallyBeatTheFinalStage()
        {
            var catalog = TestCatalog.Load();
            var heroes = Heroes();
            var raid = Raids().Find("stage_15");
            var guards = raid.ExpandDefenders(catalog, heroes).Select(d => new BattleDefenderInput { id = d.id, role = d.role, hp = d.hp, attack = d.attack, defense = d.defense, x = d.x, z = d.z }).ToList();
            var assassin = heroes.Get("river_scout");
            var stats = HeroMath.Stats(assassin, 0, 100);
            var team = Enumerable.Range(0, 6).Select(i => new BattleHeroInput { id = "a" + i, role = "Assassin", hp = stats.Hp, attack = stats.Attack, defense = stats.Defense, x = 3 + i, z = 3 }).ToList();
            Assert.Less(BattleSim.Run(raid.Expand(catalog), team, guards).stars, 3);
        }
    }
}

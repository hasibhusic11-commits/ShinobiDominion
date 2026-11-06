using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using ShinobiDominion.Core;

namespace ShinobiDominion.Tests
{
    public class BattleTests
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

        private static List<BattleBuildingInput> Outpost()
        {
            var raids = RaidCatalog.FromJson(FindFile("raids.json", "Resources/Config"), TestCatalog.Load());
            return raids.Find("ridge_outpost").Expand(TestCatalog.Load());
        }

        private static BattleHeroInput Hero(string id, string role, long hp, long attack, long defense, int x, int z) =>
            new BattleHeroInput { id = id, role = role, hp = hp, attack = attack, defense = defense, x = x, z = z };

        private static List<BattleHeroInput> MixedTeam(long power) => new List<BattleHeroInput>
        {
            Hero("a", "Tank", 1500 * power, 60 * power, 60 * power, 10, 10),
            Hero("b", "Fighter", 1200 * power, 90 * power, 40 * power, 11, 10),
            Hero("c", "Assassin", 800 * power, 110 * power, 25 * power, 12, 10),
            Hero("d", "Mage", 800 * power, 100 * power, 25 * power, 13, 10),
            Hero("e", "Control", 900 * power, 80 * power, 30 * power, 14, 10),
            Hero("f", "Support", 900 * power, 60 * power, 30 * power, 15, 10)
        };

        private static List<BattleHeroInput> SameRole(string role, long power) =>
            Enumerable.Range(0, 6).Select(i => Hero("h" + i, role, 1000 * power, 80 * power, 30 * power, 10 + i, 10)).ToList();

        [Test]
        public void SameInput_GivesTheSameLog()
        {
            var first = BattleSim.Run(Outpost(), MixedTeam(1));
            var second = BattleSim.Run(Outpost(), MixedTeam(1));
            Assert.Greater(first.events.Count, 50);
            Assert.AreEqual(first.checksum, second.checksum);
            Assert.AreEqual(first.events.Count, second.events.Count);
        }

        [Test]
        public void StrongTeam_BreaksTheWalls_AndClearsTheBase()
        {
            var result = BattleSim.Run(Outpost(), MixedTeam(4));
            Assert.AreEqual(3, result.stars);
            Assert.AreEqual(100, result.damagePercent);
            Assert.IsTrue(result.dojoDestroyed);
            Assert.Less(result.endTick, BattleSim.TickLimit);
            var walls = new HashSet<long>(Outpost().Where(b => b.isWall).Select(b => (long)b.id));
            Assert.IsTrue(result.events.Any(e => e[1] == (long)BattleEventKind.BuildingDown && walls.Contains(e[2])), "The base is fully walled in, so a wall must fall");
        }

        [Test]
        public void WeakTeam_Loses_AndEarnsNoStars()
        {
            var team = SameRole("Support", 1);
            foreach (var hero in team)
            {
                hero.hp = 60;
                hero.attack = 5;
                hero.defense = 0;
            }
            var result = BattleSim.Run(Outpost(), team);
            Assert.AreEqual(0, result.stars);
            Assert.AreEqual(0, result.heroesAlive);
        }

        [Test]
        public void Progression_Matters()
        {
            // Six level 1 commons, against the same six roles much further along.
            var commons = SameRole("Fighter", 1);
            var roles = new[] { "Tank", "Fighter", "Assassin", "Mage", "Control", "Support" };
            for (var i = 0; i < commons.Count; i++)
            {
                commons[i].role = roles[i];
                commons[i].hp = 420;
                commons[i].attack = 55;
                commons[i].defense = 20;
            }
            var weak = BattleSim.Run(Outpost(), commons);
            var strong = BattleSim.Run(Outpost(), MixedTeam(4));
            Assert.Greater(strong.stars, weak.stars);
            Assert.Greater(strong.damagePercent, weak.damagePercent);
        }

        [Test]
        public void Walls_MakeTheRaidSlower()
        {
            var open = Outpost().Where(b => !b.isWall).ToList();
            var walled = Outpost();
            Assert.Greater(BattleSim.Run(walled, MixedTeam(4)).endTick, BattleSim.Run(open, MixedTeam(4)).endTick);
        }

        [TestCase("Tank")]
        [TestCase("Fighter")]
        [TestCase("Assassin")]
        [TestCase("Mage")]
        [TestCase("Control")]
        [TestCase("Support")]
        public void EveryRole_UsesItsAbility(string role)
        {
            var result = BattleSim.Run(Outpost(), SameRole(role, 3));
            Assert.IsTrue(result.events.Any(e => e[1] == (long)BattleEventKind.Ability && e[3] == BattleSim.RoleNumber(role)), role);
        }

        [Test]
        public void Abilities_DoWhatTheySay()
        {
            var support = BattleSim.Run(Outpost(), SameRole("Support", 3));
            Assert.IsTrue(support.events.Any(e => e[1] == (long)BattleEventKind.Heal), "Supports must heal");
            var control = BattleSim.Run(Outpost(), SameRole("Control", 3));
            Assert.IsTrue(control.events.Any(e => e[1] == (long)BattleEventKind.Stun), "Control must stun towers");
        }

        /// <summary>The brief asks that six of one role isn't simply best: mixing roles must pay off.</summary>
        [Test]
        public void MixedTeam_BeatsSixOfMostSingleRoles()
        {
            var mixed = BattleSim.Run(Outpost(), MixedTeam(2));
            var beaten = new[] { "Tank", "Fighter", "Assassin", "Mage", "Control", "Support" }
                .Count(role => Score(BattleSim.Run(Outpost(), SameRole(role, 2))) <= Score(mixed));
            Assert.GreaterOrEqual(beaten, 4, "A mixed team of equal power should beat most single-role teams");
        }

        private static long Score(BattleResult result) =>
            result.stars * 100000L + result.damagePercent * 1000L + (BattleSim.TickLimit - result.endTick);

        [Test]
        public void Deploy_RefusesEveryIllegalPlacement()
        {
            var buildings = Outpost();
            string Check(params BattleHeroInput[] heroes) => BattleSim.DeployError(buildings, heroes);
            Assert.AreEqual("", Check(Hero("a", "Tank", 1, 1, 0, 2, 2)));
            Assert.AreEqual("NO_TEAM", Check());
            Assert.AreEqual("BAD_DEPLOY_CELL", Check(Hero("a", "Tank", 1, 1, 0, -1, 2)));
            Assert.AreEqual("BAD_DEPLOY_CELL", Check(Hero("a", "Tank", 1, 1, 0, 50, 2)));
            Assert.AreEqual("DEPLOY_TOO_CLOSE", Check(Hero("a", "Tank", 1, 1, 0, 25, 25)));
            Assert.AreEqual("DEPLOY_TOO_CLOSE", Check(Hero("a", "Tank", 1, 1, 0, 15, 20)));
            Assert.AreEqual("DUPLICATE_HERO", Check(Hero("a", "Tank", 1, 1, 0, 2, 2), Hero("a", "Tank", 1, 1, 0, 3, 3)));
            Assert.AreEqual("DEPLOY_CELL_TAKEN", Check(Hero("a", "Tank", 1, 1, 0, 2, 2), Hero("b", "Tank", 1, 1, 0, 2, 2)));
            var seven = Enumerable.Range(0, 7).Select(i => Hero("h" + i, "Tank", 1, 1, 0, i, 0)).ToArray();
            Assert.AreEqual("TOO_MANY_HEROES", Check(seven));
        }

        [Test]
        public void Raid_RefusesOverlappingData()
        {
            const string json = "{\"schemaVersion\":1,\"raids\":[{\"id\":\"x\",\"buildings\":[{\"def\":\"dojo_hq\",\"x\":1,\"z\":1},{\"def\":\"defense_tower\",\"x\":2,\"z\":2}]}]}";
            Assert.Throws<FormatException>(() => RaidCatalog.FromJson(json, TestCatalog.Load()));
        }

        /// <summary>The server's Go copy of the battle must produce these exact logs too.</summary>
        [Test]
        public void SharedScenarios_MatchExpectedLogs()
        {
            var vectors = JObject.Parse(FindFile("battle_vectors.json", "Tests/EditMode"));
            var buildings = Outpost();
            var problems = new List<string>();
            foreach (var scenario in vectors["scenarios"])
            {
                var team = scenario["team"].Select(h => h.ToObject<BattleHeroInput>()).ToList();
                var result = BattleSim.Run(buildings, team);
                var name = scenario.Value<string>("name");
                var actual = $"stars={result.stars} damage={result.damagePercent} endTick={result.endTick} alive={result.heroesAlive} checksum={result.checksum}";
                var expected = $"stars={scenario.Value<int>("stars")} damage={scenario.Value<int>("damagePercent")} endTick={scenario.Value<int>("endTick")} alive={scenario.Value<int>("heroesAlive")} checksum={scenario.Value<string>("checksum")}";
                if (actual != expected) problems.Add($"{name}: got {actual}, expected {expected}");
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }
    }
}

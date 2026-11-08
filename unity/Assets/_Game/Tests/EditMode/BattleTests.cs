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
        public void StrongTeam_ClearsTheWalledBase()
        {
            var result = BattleSim.Run(Outpost(), MixedTeam(4));
            Assert.AreEqual(3, result.stars);
            Assert.AreEqual(100, result.damagePercent);
            Assert.IsTrue(result.dojoDestroyed);
            Assert.Less(result.endTick, BattleSim.TickLimit);
        }

        // ---------- Walls: guards hop them; attackers go around or break them, whichever is quicker ----------

        private const int Treasury = 1;

        /// <summary>A 3x3 building at 20,20 inside a ring of walls from 18 to 24, with an optional gap on the far side.</summary>
        private static List<BattleBuildingInput> WalledBuilding(long wallHp, bool gapOnFarSide)
        {
            var list = new List<BattleBuildingInput>
            {
                new BattleBuildingInput { id = Treasury, definitionId = "gold_vault", x = 20, z = 20, width = 3, depth = 3, level = 1, hp = 400 }
            };
            var id = 100;
            for (var x = 18; x <= 24; x++)
            for (var z = 18; z <= 24; z++)
            {
                if (x != 18 && x != 24 && z != 18 && z != 24) continue;
                if (gapOnFarSide && x == 24 && z == 21) continue;
                list.Add(new BattleBuildingInput { id = id++, definitionId = "wall", x = x, z = z, width = 1, depth = 1, level = 1, hp = wallHp, isWall = true });
            }
            return list;
        }

        private static bool HitAWall(BattleResult result) => result.events.Any(e => e[1] == (long)BattleEventKind.HeroHit && e[3] >= 100);
        private static bool Destroyed(BattleResult result, int id) => result.events.Any(e => e[1] == (long)BattleEventKind.BuildingDown && e[2] == id);
        private static bool OnRing(long x, long z) => (x == 18 || x == 24 || z == 18 || z == 24) && x >= 18 && x <= 24 && z >= 18 && z <= 24;

        [Test]
        public void AttackingHero_CannotHop_SoAStrongWallWithAGapIsWalkedAround()
        {
            var hero = Hero("a", "Assassin", 100000, 60, 50, 10, 21);
            var result = BattleSim.Run(WalledBuilding(10_000_000, true), new List<BattleHeroInput> { hero });
            Assert.IsTrue(Destroyed(result, Treasury), "The hero should get in through the gap");
            Assert.IsFalse(HitAWall(result));
            Assert.IsFalse(result.events.Any(e => e[1] == (long)BattleEventKind.Move && OnRing(e[3], e[4]) && !(e[3] == 24 && e[4] == 21)), "An attacking hero never steps onto a standing wall");
        }

        [Test]
        public void AttackingHero_BreaksAWeakWall_WhenThatIsQuicker()
        {
            var hero = Hero("a", "Assassin", 100000, 60, 50, 10, 21);
            var result = BattleSim.Run(WalledBuilding(10, true), new List<BattleHeroInput> { hero });
            Assert.IsTrue(Destroyed(result, Treasury));
            Assert.IsTrue(HitAWall(result), "Breaking a thin wall beats walking round to the gap");
        }

        [Test]
        public void RangedHero_ShootsOverTheWall_FromOutside()
        {
            var mage = Hero("m", "Mage", 100000, 60, 50, 10, 21);
            var result = BattleSim.Run(WalledBuilding(10_000_000, false), new List<BattleHeroInput> { mage });
            Assert.IsTrue(Destroyed(result, Treasury));
            Assert.IsFalse(result.events.Any(e => e[1] == (long)BattleEventKind.Move && e[3] >= 18 && e[3] <= 24 && e[4] >= 18 && e[4] <= 24), "A mage in range stays outside the walls");
        }

        [Test]
        public void Troop_WalksAroundToTheGap_WhenBreakingWouldTakeLonger()
        {
            var troop = Hero("t", "Fighter", 100000, 14, 5, 10, 21);
            troop.isTroop = true;
            var result = BattleSim.Run(WalledBuilding(5000, true), new List<BattleHeroInput> { troop });
            Assert.IsTrue(Destroyed(result, Treasury));
            Assert.IsFalse(HitAWall(result), "A strong wall with a gap: go around");
        }

        [Test]
        public void Troop_BreaksAWeakWall_WhenThatIsQuickerThanGoingAround()
        {
            var troop = Hero("t", "Fighter", 100000, 14, 5, 10, 21);
            troop.isTroop = true;
            var result = BattleSim.Run(WalledBuilding(10, true), new List<BattleHeroInput> { troop });
            Assert.IsTrue(Destroyed(result, Treasury));
            Assert.IsTrue(HitAWall(result), "A weak wall in the way: break it rather than walk around");
        }

        [Test]
        public void Guard_HopsTheWall_ToReachAnAttackerOutside()
        {
            var mage = Hero("m", "Mage", 100000, 1, 50, 10, 21);
            var guard = new BattleDefenderInput { id = "g", role = "Fighter", hp = 100000, attack = 50, defense = 10, x = 19, z = 21 };
            var result = BattleSim.Run(WalledBuilding(10_000_000, false), new List<BattleHeroInput> { mage }, new List<BattleDefenderInput> { guard });
            Assert.IsTrue(result.events.Any(e => e[1] == (long)BattleEventKind.DefenderMove && OnRing(e[3], e[4])), "The guard should hop the wall");
            Assert.IsTrue(result.events.Any(e => e[1] == (long)BattleEventKind.DefenderHit), "and then reach the mage");
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
            // A mid-campaign fortress, so the gap shows; the first outpost is meant to be easy.
            var fortress = RaidCatalog.FromJson(FindFile("raids.json", "Resources/Config"), TestCatalog.Load()).Find("stage_05").Expand(TestCatalog.Load());
            var weak = BattleSim.Run(fortress, commons);
            var strong = BattleSim.Run(fortress, MixedTeam(4));
            Assert.Greater(strong.stars, weak.stars);
            Assert.Greater(strong.damagePercent, weak.damagePercent);
        }

        [Test]
        public void Walls_MakeTheRaidSlower()
        {
            // The first outpost has only thin walls, so test with a fortress.
            var walled = RaidCatalog.FromJson(FindFile("raids.json", "Resources/Config"), TestCatalog.Load()).Find("stage_05").Expand(TestCatalog.Load());
            var open = walled.Where(b => !b.isWall).ToList();
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

        [Test]
        public void Troops_FightWithoutAbilities_AndCanShareACell()
        {
            var units = MixedTeam(1).Take(1).ToList();
            for (var i = 0; i < 10; i++) units.Add(new BattleHeroInput { id = "militia", role = "Fighter", hp = 110, attack = 14, defense = 5, x = 2, z = 2, isTroop = true });
            Assert.AreEqual("", BattleSim.DeployError(Outpost(), units));
            var result = BattleSim.Run(Outpost(), units);
            Assert.IsFalse(result.events.Any(e => e[1] == (long)BattleEventKind.Ability && e[2] >= 1), "Only heroes use abilities");
            Assert.IsTrue(result.events.Any(e => e[1] == (long)BattleEventKind.HeroHit && e[2] >= 1), "Troops must attack");
        }

        [Test]
        public void Troops_AddToWhatHeroesCanDo()
        {
            var alone = MixedTeam(1).Take(1).ToList();
            alone[0].hp = 300;
            var helped = new List<BattleHeroInput>(alone) { };
            for (var i = 0; i < 40; i++) helped.Add(new BattleHeroInput { id = "militia", role = "Fighter", hp = 110, attack = 14, defense = 5, x = 45, z = 3, isTroop = true });
            var fresh = MixedTeam(1).Take(1).ToList();
            fresh[0].hp = 300;
            Assert.Greater(BattleSim.Run(Outpost(), helped).damagePercent, BattleSim.Run(Outpost(), fresh).damagePercent);
        }

        [Test]
        public void BiggestArmy_RunsQuicklyEnoughForTheServer()
        {
            var units = MixedTeam(3);
            for (var i = 0; i < 200; i++) units.Add(new BattleHeroInput { id = "militia", role = "Fighter", hp = 110, attack = 14, defense = 5, x = 2 + i % 10, z = 2, isTroop = true });
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var result = BattleSim.Run(Outpost(), units);
            clock.Stop();
            TestContext.Out.WriteLine($"200 troops: {clock.ElapsedMilliseconds} ms, {result.events.Count} events");
            Assert.Less(clock.ElapsedMilliseconds, 3000);
            Assert.Less(result.events.Count, 60000);
        }

        [Test]
        public void Army_RulesForTraining()
        {
            var troops = TroopCatalog.FromJson(FindFile("troops.json", "Resources/Config"));
            var village = TestCatalog.EmptyVillage();
            var army = new Army();
            Assert.AreEqual(VillageError.NoTrainingHall, army.TryTrain(village, troops, "militia", 1));
            Assert.AreEqual(VillageError.BadTroop, army.TryTrain(village, troops, "dragon", 1));
            Assert.AreEqual(VillageError.BadTroop, army.TryTrain(village, troops, "militia", 0));
            Assert.AreEqual(VillageError.None, village.TryPlace("training_grounds", new GridCoord(1, 1), Rotation.R0, 0, out _));
            Assert.AreEqual(VillageError.CampsFull, army.TryTrain(village, troops, "militia", 1));
            Assert.AreEqual(VillageError.None, village.TryPlace("troop_camp", new GridCoord(10, 10), Rotation.R0, 0, out _));
            Assert.AreEqual(15, Army.Capacity(village));
            Assert.AreEqual(VillageError.TroopLocked, army.TryTrain(village, troops, "iron_ox_rider", 1));

            var gold = village.Gold;
            Assert.AreEqual(VillageError.None, army.TryTrain(village, troops, "kunai_thrower", 7));
            Assert.AreEqual(gold - 7 * 60, village.Gold);
            Assert.AreEqual(14, army.UsedSpace(troops), "Stronger troops take more room");
            Assert.AreEqual(VillageError.CampsFull, army.TryTrain(village, troops, "kunai_thrower", 1));
            Assert.AreEqual(VillageError.None, army.TryTrain(village, troops, "militia", 1));
        }

        [Test]
        public void Army_SpaceGrowsWithStrength()
        {
            var troops = TroopCatalog.FromJson(FindFile("troops.json", "Resources/Config"));
            var ordered = troops.All.OrderBy(t => t.space).ToList();
            for (var i = 1; i < ordered.Count; i++)
                Assert.Greater(ordered[i].hp * ordered[i].attack, ordered[i - 1].hp * ordered[i - 1].attack, ordered[i].id);
        }

        [Test]
        public void Camps_AreLimitedToFour()
        {
            var camp = TestCatalog.Load().Get("troop_camp");
            for (var level = 1; level <= 15; level++) Assert.LessOrEqual(camp.MaxCountAt(level), 4);
            Assert.AreEqual(4, camp.MaxCountAt(15));
            Assert.Greater(camp.At(15).troopCapacity, camp.At(1).troopCapacity);
        }

        private static BattleDefenderInput Guard(string id, string role, long power, int x, int z) =>
            new BattleDefenderInput { id = id, role = role, hp = 1000 * power, attack = 80 * power, defense = 30 * power, x = x, z = z };

        /// <summary>Three guards just inside the walls, on the side the test teams land.</summary>
        private static List<BattleDefenderInput> Guards(long power) => new List<BattleDefenderInput>
        {
            Guard("g1", "Tank", power, 14, 14),
            Guard("g2", "Mage", power, 17, 17),
            Guard("g3", "Support", power, 20, 17)
        };

        [Test]
        public void Defenders_FightBack_AndCostTheAttackersHeroes()
        {
            var undefended = BattleSim.Run(Outpost(), MixedTeam(2));
            var defended = BattleSim.Run(Outpost(), MixedTeam(2), Guards(4));
            Assert.Greater(undefended.heroesAlive, defended.heroesAlive, "Guards should cost the attackers heroes");
            Assert.IsTrue(defended.events.Any(e => e[1] == (long)BattleEventKind.DefenderHit), "Guards should strike");
            Assert.IsTrue(defended.events.Any(e => e[1] == (long)BattleEventKind.DefenderDamaged), "Attackers should strike back");
        }

        [Test]
        public void Defenders_CanStopARaidThatWouldOtherwiseWin()
        {
            var undefended = BattleSim.Run(Outpost(), MixedTeam(2));
            var defended = BattleSim.Run(Outpost(), MixedTeam(2), Guards(20));
            Assert.Greater(undefended.stars, defended.stars);
            Assert.AreEqual(0, defended.heroesAlive);
            Assert.AreEqual(3, defended.defendersAlive);
        }

        [Test]
        public void StrongerAttackers_BeatTheDefenders()
        {
            var result = BattleSim.Run(Outpost(), MixedTeam(30), Guards(2));
            Assert.AreEqual(0, result.defendersAlive);
            Assert.AreEqual(3, result.stars);
            Assert.IsTrue(result.events.Count(e => e[1] == (long)BattleEventKind.DefenderDown) == 3);
        }

        [Test]
        public void DefendedBattle_IsRepeatable_AndNoEventRefersToAMissingUnit()
        {
            var first = BattleSim.Run(Outpost(), MixedTeam(4), Guards(4));
            var second = BattleSim.Run(Outpost(), MixedTeam(4), Guards(4));
            Assert.AreEqual(first.checksum, second.checksum);
            foreach (var e in first.events)
            {
                var kind = (BattleEventKind)e[1];
                if (kind == BattleEventKind.DefenderHit || kind == BattleEventKind.DefenderMove || kind == BattleEventKind.DefenderDown || kind == BattleEventKind.DefenderAbility || kind == BattleEventKind.DefenderHeal)
                    Assert.Less(e[2], 3);
                if (kind == BattleEventKind.DefenderDamaged)
                    Assert.Less(e[3], 3);
            }
        }

        [Test]
        public void Guard_HopsWalls_ButNeverWalksIntoABuilding()
        {
            var buildings = Outpost();
            var result = BattleSim.Run(buildings, MixedTeam(3), Guards(5));
            foreach (var e in result.events.Where(entry => entry[1] == (long)BattleEventKind.DefenderMove))
            {
                var x = (int)e[3];
                var z = (int)e[4];
                // A wall can have fallen by then, so only standing buildings that never fell are checked.
                var fallen = result.events.Where(entry => entry[0] <= e[0] && entry[1] == (long)BattleEventKind.BuildingDown).Select(entry => (int)entry[2]).ToList();
                Assert.IsFalse(buildings.Any(b => !b.isWall && !fallen.Contains(b.id) && x >= b.x && x < b.x + b.width && z >= b.z && z < b.z + b.depth), $"Guard walked into a building at {x},{z}");
            }
        }

        [Test]
        public void GarrisonRaid_HasOneGuardOnItsBase()
        {
            var raids = RaidCatalog.FromJson(FindFile("raids.json", "Resources/Config"), TestCatalog.Load());
            var raid = raids.Find("cinder_garrison");
            Assert.IsNotNull(raid);
            var buildings = raid.Expand(TestCatalog.Load());
            var guards = raid.ExpandDefenders(TestCatalog.Load(), HeroCatalog.FromJson(FindFile("heroes.json", "Resources/Config")));
            Assert.AreEqual(1, guards.Count, "A village has one guard");
            foreach (var guard in guards)
            {
                Assert.Greater(guard.hp, 0);
                Assert.IsTrue(buildings.Any(b => b.definitionId == "hero_base" && guard.x >= b.x && guard.x < b.x + b.width && guard.z >= b.z && guard.z < b.z + b.depth), guard.id);
            }
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
                var defenders = scenario["defenders"]?.Select(d => d.ToObject<BattleDefenderInput>()).ToList();
                var result = BattleSim.Run(buildings, team, defenders);
                var name = scenario.Value<string>("name");
                var actual = $"stars={result.stars} damage={result.damagePercent} endTick={result.endTick} alive={result.heroesAlive} guards={result.defendersAlive} checksum={result.checksum}";
                var expected = $"stars={scenario.Value<int>("stars")} damage={scenario.Value<int>("damagePercent")} endTick={scenario.Value<int>("endTick")} alive={scenario.Value<int>("heroesAlive")} guards={scenario.Value<int?>("defendersAlive") ?? 0} checksum={scenario.Value<string>("checksum")}";
                if (actual != expected) problems.Add($"{name}: got {actual}, expected {expected}");
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        // ---------- Ability charge (owner's rule, 2026-11-08) ----------

        private static List<long[]> ChargeOf(BattleResult result, int index, int side) =>
            result.events.Where(e => e[1] == (long)BattleEventKind.Charge && e[2] == index && e[4] == side).ToList();

        [Test]
        public void Special_FiresAfterFiveLandedHits_ThenTheBarEmpties()
        {
            var result = BattleSim.Run(Outpost(), new List<BattleHeroInput> { Hero("f", "Fighter", 50000, 60, 20, 3, 3) });
            var charge = ChargeOf(result, 0, 0).Select(e => e[3]).ToList();
            Assert.GreaterOrEqual(charge.Count, 6, "The fighter fights long enough to fill its bar");
            CollectionAssert.AreEqual(new long[] { 1, 2, 3, 4, 5, 0 }, charge.Take(6).ToArray(), "One step per landed hit, then empty when the special fires");
            var fullTick = ChargeOf(result, 0, 0).First(e => e[3] == BattleSim.ChargeFull)[0];
            var firstSpecial = result.events.First(e => e[1] == (long)BattleEventKind.Ability && e[2] == 0)[0];
            Assert.Greater(firstSpecial, fullTick, "The special fires on the hit after the bar fills, never before");
            Assert.IsTrue(charge.All(c => c >= 0 && c <= BattleSim.ChargeFull));
        }

        [Test]
        public void Troops_HaveNoChargeBar()
        {
            var team = new List<BattleHeroInput> { Hero("f", "Fighter", 5000, 60, 20, 3, 3) };
            for (var i = 0; i < 6; i++)
                team.Add(new BattleHeroInput { id = "t" + i, role = "Fighter", hp = 110, attack = 14, defense = 5, isTroop = true, x = 2 + i, z = 2 });
            var result = BattleSim.Run(Outpost(), team);
            Assert.IsFalse(result.events.Any(e => e[1] == (long)BattleEventKind.Charge && e[4] == 0 && e[2] >= 1), "Only the hero charges");
        }

        [Test]
        public void Defenders_ChargeOnLandedHits_Too()
        {
            var result = BattleSim.Run(Outpost(), MixedTeam(4), Guards(4));
            var guardCharge = result.events.Where(e => e[1] == (long)BattleEventKind.Charge && e[4] == 1).ToList();
            Assert.IsNotEmpty(guardCharge, "Guards fill their bars by landing hits");
            Assert.IsTrue(guardCharge.All(e => e[3] >= 0 && e[3] <= BattleSim.ChargeFull));
        }
    }
}

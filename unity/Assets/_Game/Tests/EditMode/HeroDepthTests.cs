using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShinobiDominion.Core;

namespace ShinobiDominion.Tests
{
    /// <summary>
    /// Hero depth (ADR-053): technique ranks and talents. The same numbers are checked in Go by depth_test.go, so the
    /// client's displayed numbers match what the server fights with.
    /// </summary>
    public class HeroDepthTests
    {
        private static string CatalogJson()
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Config", "heroes.json"),
                Path.Combine("Assets", "_Game", "Resources", "Config", "heroes.json")
            };
            return File.ReadAllText(candidates.First(File.Exists));
        }

        private static HeroCatalog Catalog() => HeroCatalog.FromJson(CatalogJson());

        [Test]
        public void Rank_RaisesThePowerOfTheSpecial_AndCostsMatchTheServer()
        {
            var technique = Catalog().Technique;
            Assert.AreEqual(100, technique.Power(BattleSim.PowerLegendary, 1));
            Assert.AreEqual(172, technique.Power(BattleSim.PowerLegendary, 10));
            Assert.AreEqual(74, technique.Power(BattleSim.PowerCommon, 4));
            Assert.AreEqual(75, technique.Power(BattleSim.PowerElite, 0), "A rank below 1 counts as 1");
            Assert.AreEqual(6, technique.Manuals(4));
            Assert.AreEqual(46, technique.HeroLevel(4));
            // 2 + 4 + ... + 18: every rank from 2 to 10.
            Assert.AreEqual(90, Enumerable.Range(2, 9).Sum(rank => technique.Manuals(rank)));
        }

        [Test]
        public void EachTalent_ChangesOnlyItsOwnThing()
        {
            var talents = Catalog().Talents;
            var plain = new HeroStats(1000, 200, 50);
            (long, long, long) With(string id, int level)
            {
                var stats = HeroMath.WithTalent(plain, talents.Find(id), level);
                return (stats.Hp, stats.Attack, stats.Defense);
            }
            Assert.AreEqual((1200L, 200L, 50L), With("iron_body", 5));
            Assert.AreEqual((1000L, 224L, 50L), With("keen_edge", 3));
            Assert.AreEqual((1000L, 200L, 56L), With("stone_skin", 2));
            foreach (var id in new[] { "ready_stance", "siege_breaker", "swift_feet", "no_such_talent", "" })
                Assert.AreEqual((1000L, 200L, 50L), With(id, 5), id + " leaves the three stats alone");
            Assert.AreEqual((1000L, 200L, 50L), With("keen_edge", 0), "Level 0 is no talent");

            Assert.AreEqual(3, HeroMath.TalentAmount(talents.Find("ready_stance"), 3, TalentSettings.Charge));
            Assert.AreEqual(40, HeroMath.TalentAmount(talents.Find("siege_breaker"), 5, TalentSettings.Siege));
            Assert.AreEqual(30, HeroMath.TalentAmount(talents.Find("swift_feet"), 5, TalentSettings.Speed));
            Assert.AreEqual(0, HeroMath.TalentAmount(talents.Find("swift_feet"), 5, TalentSettings.Siege), "Only its own kind");
            Assert.AreEqual(0, HeroMath.TalentAmount(null, 5, TalentSettings.Speed));
        }

        [Test]
        public void Talents_EachHaveTheirOwnKindOrStat_AndAName()
        {
            var talents = Catalog().Talents;
            Assert.AreEqual(talents.list.Count, talents.list.Select(t => t.kind).Distinct().Count(), "No two talents do the same thing");
            foreach (var talent in talents.list)
            {
                Assert.IsNotEmpty(talent.displayName, talent.id);
                Assert.IsNotEmpty(talent.description, talent.id);
            }
            // The strongest Ready Stance fills the bar and no more.
            Assert.LessOrEqual(talents.Find("ready_stance").perLevel * talents.maxLevel, BattleSim.ChargeFull);
        }

        [Test]
        public void Roster_ShowsStatsWithTheTalent_AndKeepsRankAndTalentThroughASave()
        {
            var catalog = Catalog();
            var roster = new HeroRoster(catalog);
            var hero = roster.AddHero("stone_titan");
            var before = roster.StatsOf(hero);
            hero.TalentId = "iron_body";
            hero.TalentLevel = 5;
            hero.TechniqueRank = 4;
            Assert.AreEqual(before.Hp * 120 / 100, roster.StatsOf(hero).Hp);
            Assert.AreEqual(before.Attack, roster.StatsOf(hero).Attack);

            var loaded = HeroRoster.FromJson(catalog, roster.ToJson()).Find(hero.InstanceId);
            Assert.AreEqual(4, loaded.TechniqueRank);
            Assert.AreEqual("iron_body", loaded.TalentId);
            Assert.AreEqual(5, loaded.TalentLevel);
        }

        [Test]
        public void Roster_ReadsASaveFromBeforeRanksAndTalents()
        {
            var json = "{\"schemaVersion\":1,\"heroes\":[{\"instanceId\":\"a\",\"archetypeId\":\"stone_titan\",\"evolutionIndex\":0,\"level\":1}]}";
            var hero = HeroRoster.FromJson(Catalog(), json).Find("a");
            Assert.AreEqual(1, hero.TechniqueRank);
            Assert.AreEqual("", hero.TalentId);
            Assert.AreEqual(0, hero.TalentLevel);
        }

        [Test]
        public void Roster_RefusesRankOrTalentOutOfRange()
        {
            var catalog = Catalog();
            string Save(string extra) => "{\"schemaVersion\":1,\"heroes\":[{\"instanceId\":\"a\",\"archetypeId\":\"stone_titan\",\"evolutionIndex\":0,\"level\":1," + extra + "}]}";
            foreach (var bad in new[]
            {
                "\"techniqueRank\":11", "\"techniqueRank\":-1",
                "\"talentId\":\"no_such_talent\",\"talentLevel\":1",
                "\"talentId\":\"keen_edge\",\"talentLevel\":0",
                "\"talentId\":\"keen_edge\",\"talentLevel\":6",
                "\"talentId\":\"\",\"talentLevel\":3"
            })
                Assert.Throws<VillageSaveException>(() => HeroRoster.FromJson(catalog, Save(bad)), bad);
            Assert.DoesNotThrow(() => HeroRoster.FromJson(catalog, Save("\"techniqueRank\":10,\"talentId\":\"keen_edge\",\"talentLevel\":5")));
        }

        [Test]
        public void Catalog_RefusesBadSettings()
        {
            var good = CatalogJson();
            Assert.Throws<FormatException>(() => HeroCatalog.FromJson(good.Replace("\"kind\": \"siege\"", "\"kind\": \"luck\"")));
            Assert.Throws<FormatException>(() => HeroCatalog.FromJson(good.Replace("\"id\": \"keen_edge\"", "\"id\": \"iron_body\"")));
            Assert.Throws<FormatException>(() => HeroCatalog.FromJson(good.Replace("\"item\": \"technique_manual\"", "\"item\": \"\"")));
            Assert.Throws<FormatException>(() => HeroCatalog.FromJson(good.Replace("\"maxRank\": 10", "\"maxRank\": 0")));
        }

        // ---------- In the battle ----------

        private static List<BattleBuildingInput> Hall() => new List<BattleBuildingInput>
        {
            new BattleBuildingInput { id = 1, definitionId = "dojo_hq", x = 20, z = 20, width = 4, depth = 4, level = 1, hp = 20000 }
        };

        private static BattleHeroInput Fighter(Action<BattleHeroInput> change = null)
        {
            var hero = new BattleHeroInput { id = "a", role = "Fighter", hp = 5000, attack = 300, defense = 100, x = 10, z = 21 };
            change?.Invoke(hero);
            return hero;
        }

        private static BattleResult Fight(BattleHeroInput hero) => BattleSim.Run(Hall(), new List<BattleHeroInput> { hero }, null);

        private static long FirstTick(BattleResult result, BattleEventKind kind) =>
            result.events.First(e => e[1] == (long)kind)[0];

        [Test]
        public void SwiftFeet_ReachesTheTargetSooner_AndChangesNothingElse()
        {
            var plain = Fight(Fighter());
            var swift = Fight(Fighter(h => h.speedPercent = 30));
            Assert.Less(FirstTick(swift, BattleEventKind.HeroHit), FirstTick(plain, BattleEventKind.HeroHit), "The first hit lands sooner");
            Assert.Less(swift.endTick, plain.endTick, "So the battle ends sooner");
            Assert.AreEqual(plain.events.Count(e => e[1] == (long)BattleEventKind.HeroHit), swift.events.Count(e => e[1] == (long)BattleEventKind.HeroHit), "With the same number of hits");
        }

        [Test]
        public void SiegeBreaker_FellsTheBuildingInFewerHits()
        {
            var plain = Fight(Fighter());
            var siege = Fight(Fighter(h => h.siegePercent = 40));
            var plainHits = plain.events.Count(e => e[1] == (long)BattleEventKind.HeroHit);
            var siegeHits = siege.events.Count(e => e[1] == (long)BattleEventKind.HeroHit);
            Assert.AreEqual(3, plain.stars);
            Assert.AreEqual(3, siege.stars);
            Assert.Less(siegeHits, plainHits);
            Assert.Less(siege.endTick, plain.endTick);
        }

        [Test]
        public void ReadyStance_LandsWithCharge_AndTheSpecialFiresSooner()
        {
            var plain = Fight(Fighter());
            var ready = Fight(Fighter(h => h.startCharge = 4));
            var first = ready.events.First(e => e[1] == (long)BattleEventKind.Charge);
            Assert.AreEqual(4, first[3], "The bar shows the charge the hero landed with");
            Assert.Less(first[0], FirstTick(ready, BattleEventKind.HeroHit), "Before its first hit");
            // The bar empties when the special fires: that happens after 1 hit and not after 5.
            long EmptiedAt(BattleResult result) => result.events.First(e => e[1] == (long)BattleEventKind.Charge && e[3] == 0)[0];
            Assert.Less(EmptiedAt(ready), EmptiedAt(plain));
        }

        [Test]
        public void StartCharge_StaysInsideTheBar_AndTroopsTakeNoTalent()
        {
            var over = Fight(Fighter(h => h.startCharge = 99));
            Assert.AreEqual(BattleSim.ChargeFull, over.events.First(e => e[1] == (long)BattleEventKind.Charge)[3]);
            var under = Fight(Fighter(h => { h.startCharge = -3; h.speedPercent = -50; h.siegePercent = -50; }));
            Assert.AreEqual(Fight(Fighter()).checksum, under.checksum, "Negative values count as none");

            BattleHeroInput Troop(Action<BattleHeroInput> change = null) => Fighter(h => { h.isTroop = true; change?.Invoke(h); });
            var troop = Fight(Troop());
            var gifted = Fight(Troop(h => { h.startCharge = 5; h.speedPercent = 30; h.siegePercent = 40; }));
            Assert.AreEqual(troop.checksum, gifted.checksum, "A troop with talent numbers fights like any other troop");
        }

        [Test]
        public void AGuardWithReadyStance_StartsWithItsCharge()
        {
            var guard = new BattleDefenderInput { id = "g", role = "Tank", hp = 4000, attack = 100, defense = 50, x = 16, z = 21, startCharge = 3, speedPercent = 30 };
            var result = BattleSim.Run(Hall(), new List<BattleHeroInput> { Fighter() }, new List<BattleDefenderInput> { guard });
            var first = result.events.First(e => e[1] == (long)BattleEventKind.Charge);
            Assert.AreEqual(3, first[3]);
            Assert.AreEqual(1, first[4], "It is the guard's bar");
        }
    }
}

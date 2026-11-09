using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using ShinobiDominion.Core;

namespace ShinobiDominion.Tests
{
    /// <summary>
    /// Phase 5 balance simulation. Plays the real battle rules over and over and writes a report for the owner:
    /// the campaign for every hero at three levels, the three tower kinds side by side, and the economy in hours
    /// of mine income. Runs only with SD_BALANCE_REPORT=&lt;path to a .md file&gt;; otherwise it is skipped.
    /// </summary>
    public class BalanceReportTests
    {
        private static string FindFile(string name)
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Config", name),
                Path.Combine("Assets", "_Game", "Resources", "Config", name)
            };
            return File.ReadAllText(candidates.First(File.Exists));
        }

        // The level a player is expected to have reached for each stage: the campaign tests' anchors, joined up.
        private static readonly (int Stage, int Level)[] Anchors = { (1, 1), (2, 10), (3, 25), (5, 50), (10, 110), (15, 200) };

        public static int ExpectedLevel(int stage)
        {
            for (var i = 1; i < Anchors.Length; i++)
            {
                if (stage > Anchors[i].Stage) continue;
                var (s0, l0) = Anchors[i - 1];
                var (s1, l1) = Anchors[i];
                return l0 + (l1 - l0) * (stage - s0) / Math.Max(1, s1 - s0);
            }
            return Anchors[Anchors.Length - 1].Level;
        }

        private static int ArmyFor(int stage) => Math.Min(160, 10 + 10 * stage);

        /// <summary>A hero at a total level: only Legendaries evolve past 200 (200 levels per evolution).</summary>
        private static BattleHeroInput HeroInput(HeroArchetype hero, int totalLevel, int x, int z)
        {
            var evolution = 0;
            var level = Math.Max(1, totalLevel);
            if (hero.rarity == HeroRarity.Legendary)
            {
                evolution = Math.Min(3, (level - 1) / 200);
                level -= 200 * evolution;
            }
            level = Math.Min(200, level);
            var stats = HeroMath.Stats(hero, evolution, level);
            return new BattleHeroInput
            {
                id = hero.id, role = hero.role.ToString(), hp = stats.Hp, attack = stats.Attack, defense = stats.Defense,
                x = x, z = z, special = hero.special, evolution = evolution, power = BattleSim.PowerFor(hero.rarity.ToString())
            };
        }

        private static List<BattleHeroInput> Team(HeroArchetype hero, int level, int army)
        {
            var team = new List<BattleHeroInput> { HeroInput(hero, level, 3, 3) };
            for (var i = 0; i < army; i++)
                team.Add(new BattleHeroInput { id = "t" + i, role = "Fighter", hp = 110, attack = 14, defense = 5, isTroop = true, x = 2 + i % 10, z = 2 + i / 10 % 3 });
            return team;
        }

        [Test]
        public void WriteBalanceReport()
        {
            var path = Environment.GetEnvironmentVariable("SD_BALANCE_REPORT");
            if (string.IsNullOrEmpty(path)) Assert.Ignore("Set SD_BALANCE_REPORT to a file path to write the balance report.");

            var catalog = TestCatalog.Load();
            var heroes = HeroCatalog.FromJson(FindFile("heroes.json"));
            var raids = RaidCatalog.FromJson(FindFile("raids.json"), catalog);
            var report = new StringBuilder();
            report.AppendLine("# Balance report");
            report.AppendLine();
            report.AppendLine($"Written by `BalanceReportTests` from the shipped data on {DateTime.UtcNow:yyyy-MM-dd}. Every battle is the real battle rules, the same ones the server runs. All numbers in the data are still test values.");
            report.AppendLine();
            Campaign(report, catalog, heroes, raids);
            Towers(report, catalog, heroes);
            Economy(report, catalog);
            File.WriteAllText(path, report.ToString());
            TestContext.WriteLine("Balance report written to " + path);
        }

        // ---------- Campaign tuning ----------

        /// <summary>
        /// Owner's rule (2026-11-09): Legendaries are rare, so Common and Elite heroes must be able to clear the
        /// campaign well enough to farm it. Dungeons may hold many towers and guards, so each stage has one strength
        /// percent that scales everything in it. For each stage this finds the strongest setting (100% at most) at which
        /// at least 70% of Common heroes win a star at the expected level, and writes the strengths as JSON for
        /// tools/balance/generate_campaign.js. Runs only with SD_CAMPAIGN_TUNE=&lt;path to campaign_strength.json&gt;.
        /// </summary>
        [Test, Timeout(1800000)]
        public void TuneCampaign()
        {
            var path = Environment.GetEnvironmentVariable("SD_CAMPAIGN_TUNE");
            if (string.IsNullOrEmpty(path)) Assert.Ignore("Set SD_CAMPAIGN_TUNE to a file path to tune the campaign.");

            var catalog = TestCatalog.Load();
            var heroes = HeroCatalog.FromJson(FindFile("heroes.json"));
            var raids = RaidCatalog.FromJson(FindFile("raids.json"), catalog);
            var commons = heroes.All.Where(h => h.contentComplete && !h.retired && h.rarity == HeroRarity.Common).ToList();
            var lines = new List<string>();
            foreach (var raid in raids.All)
            {
                var expected = ExpectedLevel(raid.stage);
                var army = ArmyFor(raid.stage);
                bool CommonsWin(int strength)
                {
                    raid.strength = strength;
                    var buildings = raid.Expand(catalog);
                    var guards = Guards(raid, catalog, heroes);
                    var wins = commons.Count(h => BattleSim.Run(buildings, Team(h, expected, army), guards).stars >= 1);
                    return wins * 10 >= commons.Count * 7;
                }
                // Strength in steps of 5, from 5 to 100: the highest step the Commons still beat.
                int low = 1, high = 20;
                if (CommonsWin(100)) low = 20;
                else
                {
                    while (low < high)
                    {
                        var middle = (low + high + 1) / 2;
                        if (CommonsWin(middle * 5)) low = middle;
                        else high = middle - 1;
                    }
                }
                lines.Add($"  \"{raid.id}\": {low * 5}");
            }
            File.WriteAllText(path, "{\n" + string.Join(",\n", lines) + "\n}\n");
            TestContext.WriteLine("Campaign strengths written to " + path);
        }

        /// <summary>A dungeon's guards as battle input, with its strength applied (RaidDefinition.ExpandDefenders).</summary>
        private static List<BattleDefenderInput> Guards(RaidDefinition raid, BuildingCatalog catalog, HeroCatalog heroes) =>
            raid.ExpandDefenders(catalog, heroes)
                .Select(d =>
                {
                    var hero = heroes.Get(d.archetypeId);
                    return new BattleDefenderInput { id = d.id, role = d.role, hp = d.hp, attack = d.attack, defense = d.defense, x = d.x, z = d.z, special = hero.special, evolution = d.evolutionIndex, power = BattleSim.PowerFor(hero.rarity.ToString()) };
                })
                .ToList();

        // ---------- Campaign ----------

        private static void Campaign(StringBuilder report, BuildingCatalog catalog, HeroCatalog heroes, RaidCatalog raids)
        {
            var pool = heroes.All.Where(h => h.contentComplete && !h.retired).ToList();
            var byRarity = new[] { HeroRarity.Common, HeroRarity.Elite, HeroRarity.Legendary }
                .Select(r => (Rarity: r, Heroes: pool.Where(h => h.rarity == r).ToList())).Where(g => g.Heroes.Count > 0).ToList();
            var steps = new[] { (Name: "70%", Scale: 0.7), (Name: "expected", Scale: 1.0), (Name: "130%", Scale: 1.3) };

            report.AppendLine("## 1. Campaign");
            report.AppendLine();
            report.AppendLine($"Each of the {pool.Count} summonable heroes leads the army a player would have by that stage (militia, 10 more per stage, up to 160) at 70%, 100% and 130% of the level expected there. Cells show the average stars, then the share of heroes that win at least one star.");
            report.AppendLine();
            var header = "| Stage | Expected level | " + string.Join(" | ", byRarity.SelectMany(g => steps.Select(s => $"{g.Rarity} {s.Name}"))) + " | Flag |";
            report.AppendLine(header);
            report.AppendLine("|" + string.Concat(Enumerable.Repeat("---|", 3 + byRarity.Count * steps.Length)));
            var flags = new List<string>();
            foreach (var raid in raids.All)
            {
                var expected = ExpectedLevel(raid.stage);
                var buildings = raid.Expand(catalog);
                var guards = Guards(raid, catalog, heroes);
                var cells = new List<string>();
                var averages = new Dictionary<string, double>();
                foreach (var group in byRarity)
                foreach (var step in steps)
                {
                    var level = Math.Max(1, (int)Math.Round(expected * step.Scale));
                    var results = group.Heroes.Select(h => BattleSim.Run(buildings, Team(h, level, ArmyFor(raid.stage)), guards)).ToList();
                    var average = results.Average(r => r.stars);
                    var wins = results.Count(r => r.stars >= 1) * 100 / results.Count;
                    averages[$"{group.Rarity} {step.Name}"] = average;
                    cells.Add($"{average:0.0} ({wins}%)");
                }
                var flag = "";
                if (averages.TryGetValue("Legendary expected", out var legend) && legend < 1.0) flag = "too hard: Legendaries lose at the expected level";
                else if (averages.TryGetValue("Elite expected", out var elite) && elite < 1.0) flag = "hard: Elites lose at the expected level";
                else if (averages.TryGetValue("Common 70%", out var weak) && weak >= 2.9 && raid.stage > 1) flag = "too easy: weak Commons three-star it";
                if (flag != "") flags.Add($"- Stage {raid.stage}, {raid.displayName}: {flag}.");
                report.AppendLine($"| {raid.stage}. {raid.displayName} | {expected} | {string.Join(" | ", cells)} | {flag} |");
            }
            report.AppendLine();
            report.AppendLine(flags.Count == 0 ? "No stage is flagged as too hard or too easy." : "**Flagged stages:**");
            foreach (var flag in flags) report.AppendLine(flag);
            report.AppendLine();
        }

        // ---------- Towers ----------

        private static void Towers(StringBuilder report, BuildingCatalog catalog, HeroCatalog heroes)
        {
            report.AppendLine("## 2. Arrow, Cannon and Magic Tower");
            report.AppendLine();
            report.AppendLine("One tower at a given level, built so it cannot fall, against three attackers on their own: a swarm of 40 militia (110 HP), a pack of 20 armoured troops (700 HP, 30 defense), and one Elite hero at level 100 with no army. Seconds are how long the tower takes to kill the whole group (\"-\" if not within the 3-minute battle); hero damage is over the first 60 seconds.");
            report.AppendLine();
            report.AppendLine("| Tower | Level | Attack | Range | Swarm: seconds | Armoured pack: seconds | Damage to the hero |");
            report.AppendLine("|---|---|---|---|---|---|---|");
            var hero = heroes.All.Where(h => h.contentComplete && !h.retired).FirstOrDefault(h => h.rarity == HeroRarity.Elite)
                       ?? heroes.All.First(h => h.contentComplete && !h.retired);
            foreach (var level in new[] { 1, 8, 15 })
            foreach (var id in new[] { "defense_tower", "cannon_tower", "spirit_turret" })
            {
                var definition = catalog.Get(id);
                var stats = definition.At(level);
                var tower = new BattleBuildingInput { id = 1, definitionId = id, x = 20, z = 20, width = definition.width, depth = definition.depth, level = level, hp = 100000000, attack = stats.attack, range = stats.range, isDefense = true };
                // A far-off Town Hall that nobody reaches in time, so the battle does not end when the tower is the only target.
                var hall = catalog.Get("dojo_hq");
                var townHall = new BattleBuildingInput { id = 2, definitionId = "dojo_hq", x = 44, z = 44, width = hall.width, depth = hall.depth, level = 1, hp = 100000000 };

                var buildings = new List<BattleBuildingInput> { tower, townHall };
                var swarm = SecondsToKill(buildings, 40, 110, 14, 5);
                var armoured = SecondsToKill(buildings, 20, 700, 30, 30);
                var limit = 60 * BattleSim.TicksPerSecond;

                Assert.AreEqual("", BattleSim.DeployError(new List<BattleBuildingInput> { tower, townHall }, new List<BattleHeroInput> { HeroInput(hero, 100, 5, 5) }), "The hero must be allowed to land");
                var soloFight = BattleSim.Run(new List<BattleBuildingInput> { tower, townHall }, new List<BattleHeroInput> { HeroInput(hero, 100, 5, 5) });
                var damage = soloFight.events.Where(e => e[1] == (long)BattleEventKind.TowerHit && e[0] <= limit).Sum(e => e[4]);
                report.AppendLine($"| {definition.displayName} | {level} | {stats.attack} | {stats.range} | {swarm} | {armoured} | {damage:N0} |");
            }
            report.AppendLine();
            report.AppendLine($"The hero is {hero.displayName}.");
            report.AppendLine();
        }

        /// <summary>How long a tower takes to kill a whole group of identical troops, or "-" if not in the battle's time.</summary>
        private static string SecondsToKill(List<BattleBuildingInput> buildings, int count, long hp, long attack, long defense)
        {
            var pack = new List<BattleHeroInput>();
            for (var i = 0; i < count; i++)
                pack.Add(new BattleHeroInput { id = "t" + i, role = "Fighter", hp = hp, attack = attack, defense = defense, isTroop = true, x = 2 + i % 8, z = 2 + i / 8 });
            Assert.AreEqual("", BattleSim.DeployError(buildings, pack), "The pack must be allowed to land");
            var downs = BattleSim.Run(buildings, pack).events.Where(e => e[1] == (long)BattleEventKind.HeroDown).ToList();
            return downs.Count == count ? $"{downs.Max(e => e[0]) / (double)BattleSim.TicksPerSecond:0.0}" : "-";
        }

        // ---------- Economy ----------

        private static void Economy(StringBuilder report, BuildingCatalog catalog)
        {
            report.AppendLine("## 3. Economy in hours of mine income");
            report.AppendLine();
            report.AppendLine("Gold income at each Town Hall level: as many Gold Mines as allowed, each at the highest level that Town Hall allows. Upgrade costs are then shown as hours of that income. Collecting is assumed to be on time; raids and rewards come on top.");
            report.AppendLine();
            report.AppendLine("| Town Hall | Mines | Mine level | Gold per hour | Town Hall's next upgrade | Hours of income | Everything newly unlocked, in hours |");
            report.AppendLine("|---|---|---|---|---|---|---|");
            var mine = catalog.Get("gold_mine");
            var hall = catalog.Get("dojo_hq");
            for (var th = 1; th <= hall.maxLevel; th++)
            {
                var mineLevel = Enumerable.Range(1, mine.maxLevel).Last(l => mine.At(l).requiredHq <= th);
                var mines = mine.MaxCountAt(th);
                var perHour = mines * mine.ProductionAt(mineLevel);
                var hallCost = th < hall.maxLevel ? hall.UpgradeCost(th) : 0;
                // Every upgrade step this Town Hall level newly allows, for every copy of every building.
                long unlocked = 0;
                foreach (var definition in catalog.All.Where(d => !d.IsWall && d.id != "dojo_hq" && !d.switchOnly))
                {
                    var copies = definition.MaxCountAt(th);
                    for (var level = 1; level < definition.maxLevel; level++)
                        if (definition.At(level + 1).requiredHq == th) unlocked += definition.UpgradeCost(level) * copies;
                }
                var hours = perHour == 0 ? "-" : $"{hallCost / (double)perHour:0.0}";
                var unlockedHours = perHour == 0 ? "-" : $"{unlocked / (double)perHour:0.0}";
                report.AppendLine($"| {th} | {mines} | {mineLevel} | {perHour:N0} | {(hallCost == 0 ? "max" : hallCost.ToString("N0"))} | {hours} | {unlockedHours} |");
            }
            report.AppendLine();
            report.AppendLine("Upgrade waiting times by level (builders work on these while income comes in):");
            report.AppendLine();
            var times = new List<string>();
            for (var level = 2; level <= 15; level++)
                times.Add($"level {level}: {Duration(catalog.UpgradeSeconds(mine, level))}");
            report.AppendLine(string.Join(", ", times) + ".");
            report.AppendLine();
        }

        private static string Duration(long seconds)
        {
            if (seconds <= 0) return "instant";
            if (seconds < 3600) return $"{seconds / 60} min";
            return seconds % 3600 == 0 ? $"{seconds / 3600} h" : $"{seconds / 3600.0:0.#} h";
        }
    }
}

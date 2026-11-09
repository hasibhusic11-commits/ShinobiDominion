using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using ShinobiDominion.Core;

namespace ShinobiDominion.Tests
{
    /// <summary>
    /// Runs the shared scenarios through the client's rules. The server's Go tests run the same
    /// file, so a rule changed in only one language fails here or there.
    /// </summary>
    public class RulesVectorTests
    {
        private static string ReadConfig(string name)
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Config", name),
                Path.Combine("Assets", "_Game", "Resources", "Config", name)
            };
            return File.ReadAllText(candidates.First(File.Exists));
        }

        private static JObject Vectors()
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Config", "rules_vectors.json"),
                Path.Combine("Assets", "_Game", "Tests", "EditMode", "rules_vectors.json")
            };
            return JObject.Parse(File.ReadAllText(candidates.First(File.Exists)));
        }

        [Test]
        public void Starter_MatchesTheSharedLayout()
        {
            var village = StarterVillage.Create(TestCatalog.Load(), 0);
            var expected = Vectors()["starter"].Select(b => $"{b.Value<string>("def")}@{b.Value<int>("x")},{b.Value<int>("z")}").OrderBy(s => s);
            var actual = village.Buildings.Select(b => $"{b.Definition.id}@{b.Origin.X},{b.Origin.Z}").OrderBy(s => s);
            CollectionAssert.AreEqual(expected.ToList(), actual.ToList());
        }

        [Test]
        public void Scenarios_MatchExpectedOutcomes()
        {
            foreach (var scenario in Vectors()["scenarios"])
                RunScenario(scenario);
        }

        private static void RunScenario(JToken scenario)
        {
            var name = scenario.Value<string>("name");
            var village = TestCatalog.EmptyVillage();
            var start = village.ToSave();
            start.nextInstanceId = 1;
            start.gold = scenario.Value<long>("startGold");
            start.crystals = scenario.Value<long?>("startCrystals") ?? 0;
            start.mana = scenario.Value<long?>("startMana") ?? 0;
            village.Restore(start);

            var labels = new Dictionary<string, int>();
            var army = new Army();
            var research = new TroopResearch();
            var troops = TroopCatalog.FromJson(ReadConfig("troops.json"));
            var heroes = HeroCatalog.FromJson(ReadConfig("heroes.json"));
            long clock = TimeSpan.TicksPerHour * 1000;
            var step = 0;
            foreach (var op in scenario["ops"])
            {
                step++;
                // The server finishes due upgrades before every change, under the player lock.
                village.Settle(clock);
                research.Settle(clock);
                var where = $"{name}, step {step}";
                var kind = op.Value<string>("op");
                switch (kind)
                {
                    case "setGold":
                        var save = village.ToSave();
                        save.gold = op.Value<long>("gold");
                        village.Restore(save);
                        continue;
                    case "setLevel":
                        var levelSave = village.ToSave();
                        var levelId = TargetOf(village, op, labels);
                        var levelEntry = levelSave.buildings.Find(b => b.id == levelId);
                        levelEntry.level = op.Value<int>("level");
                        levelEntry.upgradeDoneUtcTicks = 0;
                        village.Restore(levelSave);
                        continue;
                    case "train":
                        var trained = army.TryTrain(village, troops, op.Value<string>("troop"), op.Value<int>("count"));
                        Assert.AreEqual(op.Value<string>("expect"), trained.ToString(), where);
                        if (op["gold"] != null) Assert.AreEqual(op.Value<long>("gold"), village.Gold, where + " gold");
                        if (op["mana"] != null) Assert.AreEqual(op.Value<long>("mana"), village.Mana, where + " mana");
                        continue;
                    case "setMana":
                        var manaSave = village.ToSave();
                        manaSave.mana = op.Value<long>("mana");
                        village.Restore(manaSave);
                        continue;
                    case "trainHero":
                        var roster = new HeroRoster(heroes);
                        var hero = roster.AddHero("thunder_empress", 0, op.Value<int>("heroLevel"));
                        hero.Experience = op.Value<long?>("heroXp") ?? 0;
                        var heroResult = roster.TryTrainWithMana(hero.InstanceId, village, out var manaError);
                        var got = heroResult == HeroError.InvalidAmount ? manaError.ToString() : heroResult.ToString();
                        Assert.AreEqual(op.Value<string>("expect"), got, where);
                        if (op["toLevel"] != null) Assert.AreEqual(op.Value<int>("toLevel"), hero.Level, where + " hero level");
                        if (op["toXp"] != null) Assert.AreEqual(op.Value<long>("toXp"), hero.Experience, where + " hero xp");
                        if (op["mana"] != null) Assert.AreEqual(op.Value<long>("mana"), village.Mana, where + " mana");
                        continue;
                    case "advanceHours":
                        clock += TimeSpan.TicksPerHour * op.Value<long>("hours");
                        continue;
                    case "research":
                    case "finishResearch":
                        var studied = kind == "research"
                            ? research.TryStart(village, troops, op.Value<string>("troop"), clock)
                            : research.TryFinish(village, op.Value<string>("troop"), clock);
                        Assert.AreEqual(op.Value<string>("expect"), studied.ToString(), where);
                        if (op["mana"] != null) Assert.AreEqual(op.Value<long>("mana"), village.Mana, where + " mana");
                        if (op["crystals"] != null) Assert.AreEqual(op.Value<long>("crystals"), village.Crystals, where + " crystals");
                        continue;
                    case "researchLevel":
                        Assert.AreEqual(int.Parse(op.Value<string>("expect")), research.LevelOf(op.Value<string>("troop")), where);
                        continue;
                    case "advanceMinutes":
                        clock += TimeSpan.TicksPerMinute * op.Value<long>("minutes");
                        continue;
                    case "upgrading":
                        Assert.AreEqual(op.Value<string>("expect") == "true", village.Find(TargetOf(village, op, labels)).Upgrading, where);
                        continue;
                    case "occupied":
                        var hit = village.BuildingAt(new GridCoord(op.Value<int>("x"), op.Value<int>("z")));
                        Assert.AreEqual(op.Value<string>("expect") == "true", hit != null, where);
                        continue;
                    case "levelAt":
                        var at = village.BuildingAt(new GridCoord(op.Value<int>("x"), op.Value<int>("z")));
                        Assert.AreEqual(int.Parse(op.Value<string>("expect")), at.Level, where);
                        continue;
                    case "defAt":
                        var standing = village.BuildingAt(new GridCoord(op.Value<int>("x"), op.Value<int>("z")));
                        Assert.AreEqual(op.Value<string>("expect"), standing.Definition.id, where);
                        continue;
                }

                var actual = Apply(village, op, kind, labels, clock);
                Assert.AreEqual(op.Value<string>("expect"), actual.ToString(), where);
                if (op["gold"] != null) Assert.AreEqual(op.Value<long>("gold"), village.Gold, where + " gold");
                if (op["crystals"] != null) Assert.AreEqual(op.Value<long>("crystals"), village.Crystals, where + " crystals");
                if (op["mana"] != null) Assert.AreEqual(op.Value<long>("mana"), village.Mana, where + " mana");
                if (op["count"] != null)
                    Assert.AreEqual(op.Value<int>("count"), village.CountOf(op.Value<string>("countDef") ?? op.Value<string>("def")), where + " count");
                if (op["level"] != null)
                    Assert.AreEqual(op.Value<int>("level"), village.Find(TargetOf(village, op, labels)).Level, where + " level");
            }
        }

        private static int TargetOf(Village village, JToken op, Dictionary<string, int> labels)
        {
            if (op["target"] != null) return labels[op.Value<string>("target")];
            if (op["atX"] != null) return village.BuildingAt(new GridCoord(op.Value<int>("atX"), op.Value<int>("atZ")))?.Id ?? 0;
            return op.Value<int>("id");
        }

        private static VillageError Apply(Village village, JToken op, string kind, Dictionary<string, int> labels, long clock)
        {
            var cell = new GridCoord(op.Value<int?>("x") ?? 0, op.Value<int?>("z") ?? 0);
            var rotation = (Rotation)(op.Value<int?>("rotation") ?? 0);
            switch (kind)
            {
                case "place":
                    var placed = village.TryPlace(op.Value<string>("def"), cell, rotation, clock, out var id);
                    if (placed == VillageError.None && op["label"] != null) labels[op.Value<string>("label")] = id;
                    return placed;
                case "walls":
                    return village.TryPlaceWallRun(cell, new GridCoord(op.Value<int>("x2"), op.Value<int>("z2")), clock);
                case "move":
                    return village.TryMove(TargetOf(village, op, labels), cell, rotation);
                case "remove":
                    return village.TryRemove(TargetOf(village, op, labels));
                case "switch":
                    return village.TrySwitch(TargetOf(village, op, labels), op.Value<string>("def"));
                case "upgrade":
                    return village.TryUpgrade(TargetOf(village, op, labels), clock);
                case "finish":
                    return village.TryFinishNow(TargetOf(village, op, labels), clock);
                case "upgradeGroup":
                    return village.TryUpgradeWallGroup(TargetOf(village, op, labels));
                case "collect":
                    return village.TryCollect(TargetOf(village, op, labels), clock, out _);
                default:
                    throw new ArgumentException("Unknown op " + kind);
            }
        }
    }
}

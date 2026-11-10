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
    /// Battle spells (ADR-052): the data file, who may cast what, the cast checks, and what each spell does in the
    /// sim. The shared vectors in BattleTests check that the Go server reaches the same logs.
    /// </summary>
    public class SpellTests
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

        private static SpellCatalog Spells() => SpellCatalog.FromJson(FindFile("spells.json", "Resources/Config"));

        private static List<BattleBuildingInput> Outpost()
        {
            var raids = RaidCatalog.FromJson(FindFile("raids.json", "Resources/Config"), TestCatalog.Load());
            return raids.Find("ridge_outpost").Expand(TestCatalog.Load());
        }

        private static string Levels(IEnumerable<RaidSpell> spells) => string.Join(" ", spells.Select(s => s.id + ":" + s.level));

        private static BattleBuildingInput Building(int id, string definitionId, int x, int z, int size, long hp) =>
            new BattleBuildingInput { id = id, definitionId = definitionId, x = x, z = z, width = size, depth = size, level = 1, hp = hp };

        private static BattleBuildingInput Wall(int id, int x, int z, long hp) =>
            new BattleBuildingInput { id = id, definitionId = "wall", x = x, z = z, width = 1, depth = 1, level = 1, hp = hp, isWall = true };

        private static BattleBuildingInput Tower(int id, int x, int z, long hp, long attack) =>
            new BattleBuildingInput { id = id, definitionId = "defense_tower", x = x, z = z, width = 3, depth = 3, level = 1, hp = hp, attack = attack, range = 13, isDefense = true };

        private static BattleHeroInput Hero(string id, string role, long hp, long attack, long defense, int x, int z) =>
            new BattleHeroInput { id = id, role = role, hp = hp, attack = attack, defense = defense, x = x, z = z };

        private static BattleHeroInput Troop(string role, long hp, long attack, int x, int z, int tick = 0) =>
            new BattleHeroInput { id = "t", role = role, hp = hp, attack = attack, defense = 10, x = x, z = z, isTroop = true, deployTick = tick };

        private static BattleDefenderInput Guard(string id, string role, long hp, long attack, long defense, int x, int z) =>
            new BattleDefenderInput { id = id, role = role, hp = hp, attack = attack, defense = defense, x = x, z = z };

        private static List<BattleHeroInput> MixedTeam(long power) => new List<BattleHeroInput>
        {
            Hero("a", "Tank", 1500 * power, 60 * power, 60 * power, 10, 10),
            Hero("b", "Fighter", 1200 * power, 90 * power, 40 * power, 11, 10),
            Hero("c", "Assassin", 800 * power, 110 * power, 25 * power, 12, 10),
            Hero("d", "Mage", 800 * power, 100 * power, 25 * power, 13, 10),
            Hero("e", "Control", 900 * power, 80 * power, 30 * power, 14, 10),
            Hero("f", "Support", 900 * power, 60 * power, 30 * power, 15, 10)
        };

        private static List<BattleDefenderInput> Guards(long power) => new List<BattleDefenderInput>
        {
            Guard("g1", "Tank", 1000 * power, 80 * power, 30 * power, 14, 14),
            Guard("g2", "Mage", 1000 * power, 80 * power, 30 * power, 17, 17),
            Guard("g3", "Support", 1000 * power, 80 * power, 30 * power, 20, 17)
        };

        private static readonly List<BattleHeroInput> Nobody = new List<BattleHeroInput>();

        private static string Line(long[] e) => string.Join(",", e);
        private static List<long[]> Of(BattleResult result, BattleEventKind kind) => result.events.Where(e => e[1] == (long)kind).ToList();
        private static List<string> SpellLines(BattleResult result) =>
            result.events.Where(e => e[1] >= (long)BattleEventKind.SpellCast || e[1] == (long)BattleEventKind.BuildingDown || e[1] == (long)BattleEventKind.DefenderDown).Select(Line).ToList();

        // ---------- The data file and who may cast ----------

        [Test]
        public void Catalog_LoadsTheShippedSpells_AndOpensThemByHutLevel()
        {
            var spells = Spells();
            Assert.AreEqual("ember_rain mending_mist frost_seal war_drum", string.Join(" ", spells.All.Select(s => s.id)));
            Assert.AreEqual("damage heal freeze boost", string.Join(" ", spells.All.Select(s => s.effect)));
            Assert.AreEqual("", Levels(spells.Available(0)), "No Research Hut, no spells");
            Assert.AreEqual("ember_rain:1", Levels(spells.Available(1)));
            Assert.AreEqual("ember_rain:2 mending_mist:1", Levels(spells.Available(2)));
            Assert.AreEqual("ember_rain:4 mending_mist:3 frost_seal:2 war_drum:1", Levels(spells.Available(4)));
            Assert.AreEqual("ember_rain:15 mending_mist:14 frost_seal:13 war_drum:12", Levels(spells.Available(15)));
            Assert.AreEqual(Levels(spells.Available(15)), Levels(spells.Available(99)), "A spell stops at the end of its table");

            // Every spell has a level for every hut level from its unlock to the hut's last, and each level is stronger.
            var hut = TestCatalog.Load().Get("research_hut");
            foreach (var spell in spells.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(spell.displayName), spell.id);
                Assert.IsFalse(string.IsNullOrEmpty(spell.description), spell.id);
                Assert.AreEqual(hut.maxLevel, spell.unlockHutLevel + spell.MaxLevel - 1, spell.id);
                for (var level = 2; level <= spell.MaxLevel; level++)
                    Assert.Greater(spell.PowerAt(level), spell.PowerAt(level - 1), spell.id);
            }
        }

        [Test]
        public void Available_FollowsTheVillagesResearchHut()
        {
            var spells = Spells();
            var village = TestCatalog.EmptyVillage();
            Assert.AreEqual("", Levels(spells.Available(village)));

            var save = village.ToSave();
            save.buildings.Add(new BuildingSave { id = 901, definitionId = Village.HeadquartersId, x = 22, z = 22, level = 3 });
            save.buildings.Add(new BuildingSave { id = 902, definitionId = "research_hut", x = 2, z = 2, level = 3 });
            save.nextInstanceId = 903;
            var grown = Village.FromSave(village.Catalog, save);
            Assert.AreEqual(3, TroopResearch.HutLevel(grown));
            Assert.AreEqual("ember_rain:3 mending_mist:2 frost_seal:1", Levels(spells.Available(grown)));
        }

        private static string OneSpell(Action<JObject> change, int schemaVersion = 1)
        {
            var spell = new JObject
            {
                ["id"] = "spark",
                ["effect"] = "damage",
                ["unlockHutLevel"] = 1,
                ["radius"] = 300,
                ["durationTicks"] = 30,
                ["pulses"] = 3,
                ["power"] = new JArray(10, 20)
            };
            change?.Invoke(spell);
            return new JObject { ["schemaVersion"] = schemaVersion, ["spells"] = new JArray(spell) }.ToString();
        }

        [Test]
        public void Catalog_RefusesBadFiles()
        {
            Assert.AreEqual(2, SpellCatalog.FromJson(OneSpell(null)).Find("spark").MaxLevel);
            Assert.Throws<FormatException>(() => SpellCatalog.FromJson(OneSpell(null, 2)), "A newer file");
            Assert.Throws<FormatException>(() => SpellCatalog.FromJson(OneSpell(s => s["id"] = "")));
            Assert.Throws<FormatException>(() => SpellCatalog.FromJson(OneSpell(s => s["effect"] = "storm")));
            Assert.Throws<FormatException>(() => SpellCatalog.FromJson(OneSpell(s => s["unlockHutLevel"] = 0)));
            Assert.Throws<FormatException>(() => SpellCatalog.FromJson(OneSpell(s => s["radius"] = 0)));
            Assert.Throws<FormatException>(() => SpellCatalog.FromJson(OneSpell(s => s["durationTicks"] = 0)));
            Assert.Throws<FormatException>(() => SpellCatalog.FromJson(OneSpell(s => s["pulses"] = 0)));
            Assert.Throws<FormatException>(() => SpellCatalog.FromJson(OneSpell(s => s["pulses"] = 31)), "More pulses than ticks");
            Assert.Throws<FormatException>(() => SpellCatalog.FromJson(OneSpell(s => s["power"] = new JArray())));
            Assert.Throws<FormatException>(() => SpellCatalog.FromJson(OneSpell(s => s["power"] = new JArray(10, 0))));
            Assert.Throws<FormatException>(() => SpellCatalog.FromJson(OneSpell(s => s["effect"] = "freeze")), "A freeze applies once");
            Assert.Throws<FormatException>(() => SpellCatalog.FromJson(OneSpell(s => s["effect"] = "boost")), "A boost applies once");
            Assert.Throws<FormatException>(() => SpellCatalog.FromJson(OneSpell(s => { s["effect"] = "freeze"; s["pulses"] = 1; s["durationTicks"] = 5; })),
                "10 percent of 5 ticks is no time at all");
            Assert.AreEqual(1, SpellCatalog.FromJson(OneSpell(s => { s["effect"] = "freeze"; s["pulses"] = 1; s["power"] = new JArray(100); })).All.Count);

            var twice = JObject.Parse(OneSpell(null));
            ((JArray)twice["spells"]).Add(twice["spells"][0].DeepClone());
            Assert.Throws<FormatException>(() => SpellCatalog.FromJson(twice.ToString()), "The same id twice");
        }

        [Test]
        public void Session_ReadsItsSpellsFromTheServer()
        {
            var session = RaidSession.Parse("{\"attackId\":\"a1\",\"spells\":[{\"id\":\"ember_rain\",\"level\":3},{\"id\":\"mending_mist\",\"level\":2}]}");
            Assert.AreEqual("ember_rain:3 mending_mist:2", Levels(session.spells));
            Assert.AreEqual("", session.CastError(new List<RaidSpellCast> { new RaidSpellCast { spellId = "ember_rain", x = 5, z = 5, tick = 10 } }, 300));
            Assert.AreEqual("SPELL_LOCKED", session.CastError(new List<RaidSpellCast> { new RaidSpellCast { spellId = "war_drum", x = 5, z = 5, tick = 10 } }, 300));
            var inputs = session.ToCasts(new[] { new RaidSpellCast { spellId = "ember_rain", x = 5, z = 6, tick = 10 } }, Spells());
            Assert.AreEqual(1, inputs.Count);
            Assert.AreEqual(260, inputs[0].power, "Level 3, as the session says");
            Assert.AreEqual(10, inputs[0].tick);

            var old = RaidSession.Parse("{\"attackId\":\"a1\"}");
            Assert.IsNotNull(old.spells, "A server without spells sends none");
            Assert.AreEqual(0, old.spells.Count);
        }

        // ---------- The cast checks ----------

        [Test]
        public void CastError_NamesEachRefusal()
        {
            var available = Spells().Available(2);
            RaidSpellCast At(string id, int x, int z, int tick) => new RaidSpellCast { spellId = id, x = x, z = z, tick = tick };
            string Check(int end, params RaidSpellCast[] casts) => SpellCatalog.CastError(available, casts, end);

            Assert.AreEqual("", Check(300));
            Assert.AreEqual("", SpellCatalog.CastError(available, null, 0), "No casts is always fine, in the old kind of battle too");
            Assert.AreEqual("", Check(300, At("ember_rain", 0, 0, 0), At("mending_mist", 49, 49, 299)));
            Assert.AreEqual("", Check(300, At("mending_mist", 22, 22, 7), At("ember_rain", 22, 22, 7)), "Any cell, buildings included, and two on one tick");

            Assert.AreEqual("SPELL_LOCKED", Check(300, At("frost_seal", 5, 5, 0)), "The hut is too low");
            Assert.AreEqual("SPELL_LOCKED", Check(300, At("meteor", 5, 5, 0)));
            Assert.AreEqual("SPELL_LOCKED", Check(300, At(null, 5, 5, 0)));
            Assert.AreEqual("SPELL_LOCKED", SpellCatalog.CastError(null, new[] { At("ember_rain", 5, 5, 0) }, 300));
            Assert.AreEqual("SPELL_USED", Check(300, At("ember_rain", 5, 5, 0), At("ember_rain", 6, 6, 50)));
            Assert.AreEqual("BAD_SPELL_CELL", Check(300, At("ember_rain", -1, 5, 0)));
            Assert.AreEqual("BAD_SPELL_CELL", Check(300, At("ember_rain", 5, -1, 0)));
            Assert.AreEqual("BAD_SPELL_CELL", Check(300, At("ember_rain", VillageGrid.Size, 5, 0)));
            Assert.AreEqual("BAD_SPELL_CELL", Check(300, At("ember_rain", 5, VillageGrid.Size, 0)));
            Assert.AreEqual("BAD_DEPLOY_TICK", Check(300, At("ember_rain", 5, 5, -1)));
            Assert.AreEqual("BAD_DEPLOY_TICK", Check(300, At("ember_rain", 5, 5, 300)), "Nothing lands once the battle is over");
            Assert.AreEqual("BAD_DEPLOY_TICK", Check(0, At("ember_rain", 5, 5, 0)), "Only a timed battle has spells");
            Assert.AreEqual("BAD_DEPLOY_TICK", Check(-3, At("ember_rain", 5, 5, 0)));
            Assert.AreEqual("BAD_DEPLOY_TICK", Check(BattleSim.TickLimit + 1, At("ember_rain", 5, 5, 0)));
            Assert.AreEqual("DEPLOY_TICK_ORDER", Check(300, At("ember_rain", 5, 5, 50), At("mending_mist", 5, 5, 49)));

            // The first problem in the list is the one named, and for one cast: locked, used, cell, tick, order.
            Assert.AreEqual("SPELL_LOCKED", Check(300, At("war_drum", -1, 99, 999)));
            Assert.AreEqual("SPELL_USED", Check(300, At("ember_rain", 5, 5, 10), At("ember_rain", -1, 5, 5)));
            Assert.AreEqual("BAD_SPELL_CELL", Check(300, At("ember_rain", 5, 5, 10), At("mending_mist", 50, 5, 5)));
            Assert.AreEqual("BAD_DEPLOY_TICK", Check(300, At("ember_rain", 5, 5, 10), At("mending_mist", 5, 5, -1)));
        }

        [Test]
        public void ToInput_WorksOutTheNumbersTheSimUses()
        {
            var spells = Spells();
            var ember = spells.Find("ember_rain").ToInput(1, 7, 8, 12);
            Assert.AreEqual("ember_rain damage 7 8 300 3 10 120 0 12", $"{ember.id} {ember.effect} {ember.x} {ember.z} {ember.radius} {ember.pulses} {ember.pulseTicks} {ember.power} {ember.ticks} {ember.tick}");
            var mist = spells.Find("mending_mist").ToInput(2, 1, 2);
            Assert.AreEqual("heal 350 5 10 130 0 0", $"{mist.effect} {mist.radius} {mist.pulses} {mist.pulseTicks} {mist.power} {mist.ticks} {mist.tick}");
            var frost = spells.Find("frost_seal").ToInput(1, 1, 2);
            Assert.AreEqual("freeze 350 1 40 0 40", $"{frost.effect} {frost.radius} {frost.pulses} {frost.pulseTicks} {frost.power} {frost.ticks}");
            Assert.AreEqual(64, spells.Find("frost_seal").ToInput(13, 1, 2).ticks, "160 percent of 40 ticks");
            var drum = spells.Find("war_drum").ToInput(1, 1, 2);
            Assert.AreEqual("boost 400 1 50 25 50", $"{drum.effect} {drum.radius} {drum.pulses} {drum.pulseTicks} {drum.power} {drum.ticks}");
            Assert.AreEqual(80, spells.Find("war_drum").ToInput(12, 1, 2).power);

            // A level outside the table counts as the nearest one in it.
            Assert.AreEqual(120, spells.Find("ember_rain").ToInput(0, 1, 2).power);
            Assert.AreEqual(1100, spells.Find("ember_rain").ToInput(40, 1, 2).power);

            Assert.AreEqual(3f, spells.Find("ember_rain").RadiusCells, "For the target ring: cells from the middle of the cast cell");
            Assert.AreEqual(3.5f, spells.Find("mending_mist").RadiusCells);
            Assert.AreEqual(30, spells.Find("ember_rain").ActiveTicks(1));
            Assert.AreEqual(50, spells.Find("mending_mist").ActiveTicks(1));
            Assert.AreEqual(40, spells.Find("frost_seal").ActiveTicks(1));
            Assert.AreEqual(64, spells.Find("frost_seal").ActiveTicks(13));
            Assert.AreEqual(50, spells.Find("war_drum").ActiveTicks(12));

            var available = spells.Available(1);
            Assert.Throws<FormatException>(() => spells.ToInput(available, new RaidSpellCast { spellId = "war_drum" }), "Not in the session");
            Assert.Throws<FormatException>(() => spells.ToInput(available, new RaidSpellCast { spellId = "meteor" }));
            Assert.AreEqual(0, spells.ToInputs(available, null).Count);
        }

        // ---------- What each spell does ----------

        [Test]
        public void EmberRain_HitsBuildingsWallsAndGuardsInTheRing_InThreePulses()
        {
            var buildings = new List<BattleBuildingInput>
            {
                Building(1, "dojo_hq", 30, 30, 4, 5000),
                Building(2, "gold_vault", 10, 10, 3, 1000),
                Wall(3, 11, 14, 300),
                Building(4, "gold_mine", 16, 10, 3, 1000)
            };
            // The first guard stands exactly three cells from the middle of the cast cell, on the edge of the ring.
            var guards = new List<BattleDefenderInput> { Guard("near", "Tank", 1000, 50, 30, 14, 11), Guard("far", "Tank", 1000, 50, 30, 15, 11) };
            var cast = Spells().Find("ember_rain").ToInput(1, 11, 11);
            var result = BattleSim.Run(buildings, Nobody, guards, 40, new[] { cast });
            CollectionAssert.AreEqual(new[]
            {
                "1,20,0,11,11,0",    // the cast lands on the tick after it was made
                "1,21,0,1,3,300",    // first of three pulses, radius 300
                "1,22,0,2,120,880",  // the vault
                "1,22,0,3,120,180",  // the wall
                "1,23,0,0,92,908",   // the guard: 120 * 100 / (100 + 30 defence)
                "11,21,0,2,3,300",
                "11,22,0,2,120,760",
                "11,22,0,3,120,60",
                "11,23,0,0,92,816",
                "21,21,0,3,3,300",
                "21,22,0,2,120,640",
                "21,22,0,3,60,0",    // never more than the HP left
                "21,3,3,0,0,0",      // the wall falls
                "21,23,0,0,92,724"
            }, result.events.Select(Line).ToList());
            Assert.AreEqual(40, result.endTick);
            Assert.AreEqual(5, result.damagePercent, "360 of 7000; the wall counts for nothing");
            Assert.AreEqual(2, result.defendersAlive);
        }

        [Test]
        public void EmberRain_CanFellGuards()
        {
            var buildings = new List<BattleBuildingInput> { Building(1, "dojo_hq", 30, 30, 4, 5000) };
            var guards = new List<BattleDefenderInput> { Guard("g", "Tank", 150, 50, 0, 11, 11) };
            var result = BattleSim.Run(buildings, Nobody, guards, 40, new[] { Spells().Find("ember_rain").ToInput(1, 10, 11) });
            CollectionAssert.AreEqual(new[] { "1,20,0,10,11,0", "1,21,0,1,3,300", "1,23,0,0,120,30", "11,21,0,2,3,300", "11,23,0,0,30,0", "11,12,0,0,0,0", "21,21,0,3,3,300" },
                result.events.Select(Line).ToList());
            Assert.AreEqual(0, result.defendersAlive);
        }

        [Test]
        public void EmberRain_EndsTheBattle_WhenTheLastBuildingFalls()
        {
            List<BattleBuildingInput> Field() => new List<BattleBuildingInput> { Building(1, "dojo_hq", 20, 20, 4, 200), Wall(2, 30, 30, 300) };
            var spell = Spells().Find("ember_rain");
            var result = BattleSim.Run(Field(), Nobody, null, 300, new[] { spell.ToInput(1, 21, 21) });
            Assert.AreEqual(11, result.endTick, "The second pulse brings it down, and a standing wall does not keep the battle going");
            Assert.AreEqual(100, result.damagePercent);
            Assert.AreEqual(3, result.stars);
            Assert.IsTrue(result.dojoDestroyed);
            CollectionAssert.AreEqual(new[] { "1,20,0,21,21,0", "1,21,0,1,3,300", "1,22,0,1,120,80", "11,21,0,2,3,300", "11,22,0,1,80,0", "11,3,1,0,0,0" },
                result.events.Select(Line).ToList());

            // The same thing live: the battle stops on that tick, and nothing more can be cast.
            var live = BattleSim.Start(Field());
            Assert.AreEqual(0, live.CastSpell(spell.ToInput(1, 21, 21)));
            for (var i = 0; i < 10; i++) Assert.IsTrue(live.Step());
            Assert.IsFalse(live.Step());
            Assert.IsTrue(live.Done);
            Assert.AreEqual(result.checksum, live.Finish().checksum);
            Assert.Throws<InvalidOperationException>(() => live.CastSpell(spell.ToInput(1, 21, 21)));
        }

        [Test]
        public void MendingMist_HealsHurtUnitsInTheRing_NeverPastFull()
        {
            // Each troop stands by its own tower, too far from the other one to be shot by it.
            var buildings = new List<BattleBuildingInput> { Tower(1, 10, 10, 1000000, 200), Tower(2, 30, 30, 1000000, 200) };
            var team = new List<BattleHeroInput> { Troop("Fighter", 30000, 10, 15, 11), Troop("Fighter", 30000, 10, 35, 31) };
            var result = BattleSim.Run(buildings, team, null, 60, new[] { Spells().Find("mending_mist").ToInput(1, 14, 11) });

            Assert.AreEqual(5, Of(result, BattleEventKind.SpellPulse).Count);
            CollectionAssert.AreEqual(new long[] { 1, 11, 21, 31, 41 }, Of(result, BattleEventKind.SpellPulse).Select(e => e[0]).ToList());
            var hp = new long[] { 30000, 30000 };
            var heals = new List<long[]>();
            foreach (var e in result.events)
            {
                if (e[1] == (long)BattleEventKind.TowerHit) hp[e[3]] = e[5];
                if (e[1] != (long)BattleEventKind.SpellHeal) continue;
                Assert.AreEqual(0, e[3], "Only the troop in the ring is healed");
                var now = Math.Min(hp[0] + 80, 30000);
                Assert.AreEqual(now - hp[0], e[4]);
                Assert.AreEqual(now, e[5]);
                hp[0] = now;
                heals.Add(e);
            }
            // At full HP on the first pulse there is nothing to heal, so nothing is logged.
            CollectionAssert.AreEqual(new long[] { 11, 21, 31, 41 }, heals.Select(e => e[0]).ToList());
            Assert.Less(hp[1], 30000, "The far troop was hurt too, and got nothing");
        }

        [Test]
        public void MendingMist_StopsAtFullHp()
        {
            // One weak shot before the second pulse: the mist gives back only what is missing.
            var buildings = new List<BattleBuildingInput> { Tower(1, 10, 10, 1000000, 30) };
            var team = new List<BattleHeroInput> { Troop("Fighter", 30000, 10, 15, 11) };
            var result = BattleSim.Run(buildings, team, null, 12, new[] { Spells().Find("mending_mist").ToInput(1, 14, 11) });
            var heal = Of(result, BattleEventKind.SpellHeal).Single();
            Assert.AreEqual(11, heal[0]);
            Assert.Less(heal[4], 80);
            Assert.AreEqual(30000, heal[5]);
            var hurt = Of(result, BattleEventKind.TowerHit).Where(e => e[0] < 11).Sum(e => e[4]);
            Assert.AreEqual(hurt, heal[4]);
        }

        [Test]
        public void FrostSeal_StopsATowerForItsLength()
        {
            List<BattleBuildingInput> Field() => new List<BattleBuildingInput> { Tower(1, 10, 10, 1000000, 60), Building(2, "gold_mine", 10, 14, 3, 1000000), Tower(3, 30, 30, 1000000, 60) };
            List<BattleHeroInput> Team() => new List<BattleHeroInput> { Troop("Tank", 100000, 1, 15, 11) };
            var spell = Spells().Find("frost_seal");
            long FirstShot(BattleResult r, int tower) => Of(r, BattleEventKind.TowerHit).Where(e => e[2] == tower).Select(e => e[0]).DefaultIfEmpty(-1).Min();

            var plain = BattleSim.Run(Field(), Team(), null, 120);
            Assert.AreEqual(1, FirstShot(plain, 1), "Without the spell the tower shoots at once");

            var frozen = BattleSim.Run(Field(), Team(), null, 120, new[] { spell.ToInput(1, 11, 11) });
            // Only the tower in the ring: not the mine beside it, and not the tower far away.
            CollectionAssert.AreEqual(new[] { "1,20,0,11,11,2", "1,21,0,1,1,350", "1,25,0,0,1,40" }, SpellLines(frozen));
            Assert.AreEqual(41, FirstShot(frozen, 1), "Frozen on ticks 1 to 40, shooting again on tick 41");

            var longer = BattleSim.Run(Field(), Team(), null, 120, new[] { spell.ToInput(13, 11, 11) });
            Assert.AreEqual("1,25,0,0,1,64", SpellLines(longer).Last());
            Assert.AreEqual(65, FirstShot(longer, 1));
        }

        [Test]
        public void FrostSeal_StopsGuardsInTheRing()
        {
            List<BattleBuildingInput> Field() => new List<BattleBuildingInput> { Building(1, "dojo_hq", 30, 30, 4, 1000000) };
            List<BattleDefenderInput> TwoGuards() => new List<BattleDefenderInput> { Guard("in", "Fighter", 5000, 50, 30, 13, 13), Guard("out", "Fighter", 5000, 50, 30, 10, 15) };
            List<BattleHeroInput> Team() => new List<BattleHeroInput> { Troop("Tank", 100000, 1, 10, 10) };
            var acts = new[] { (long)BattleEventKind.DefenderMove, (long)BattleEventKind.DefenderHit, (long)BattleEventKind.DefenderAbility, (long)BattleEventKind.DefenderHeal };
            long FirstAct(BattleResult r, int guard) => r.events.Where(e => acts.Contains(e[1]) && e[2] == guard).Select(e => e[0]).DefaultIfEmpty(-1).Min();

            var plain = BattleSim.Run(Field(), Team(), TwoGuards(), 120);
            Assert.AreEqual(1, FirstAct(plain, 0));
            Assert.AreEqual(1, FirstAct(plain, 1));

            var frozen = BattleSim.Run(Field(), Team(), TwoGuards(), 120, new[] { Spells().Find("frost_seal").ToInput(1, 13, 13) });
            CollectionAssert.AreEqual(new[] { "1,20,0,13,13,2", "1,21,0,1,1,350", "1,25,0,1,0,40" }, SpellLines(frozen));
            Assert.AreEqual(41, FirstAct(frozen, 0), "The guard in the ring does nothing on ticks 1 to 40");
            Assert.AreEqual(1, FirstAct(frozen, 1), "The guard outside it is not held");
        }

        [Test]
        public void WarDrum_RaisesDamageForItsLength_ForUnitsInTheRingWhenItLands()
        {
            List<BattleBuildingInput> Field() => new List<BattleBuildingInput> { Building(1, "dojo_hq", 12, 10, 4, 1000000) };
            // The second troop lands after the drum, on the same cell, and gets nothing.
            List<BattleHeroInput> Team() => new List<BattleHeroInput> { Troop("Fighter", 1000, 100, 9, 11), Troop("Fighter", 1000, 100, 9, 11, 5) };
            var spell = Spells().Find("war_drum");
            List<long[]> Hits(BattleResult r, int unit) => Of(r, BattleEventKind.HeroHit).Where(e => e[2] == unit).ToList();

            var plain = BattleSim.Run(Field(), Team(), null, 120);
            Assert.IsTrue(Hits(plain, 0).All(e => e[4] == 100), "A fighter troop with 100 attack hits for 100");

            var drummed = BattleSim.Run(Field(), Team(), null, 120, new[] { spell.ToInput(1, 9, 11) });
            CollectionAssert.AreEqual(new[] { "1,20,0,9,11,3", "1,21,0,1,1,400", "1,26,0,0,50,25" }, SpellLines(drummed));
            var boosted = Hits(drummed, 0).Where(e => e[0] <= 50).ToList();
            var after = Hits(drummed, 0).Where(e => e[0] > 50).ToList();
            Assert.Greater(boosted.Count, 0);
            Assert.Greater(after.Count, 0);
            Assert.IsTrue(boosted.All(e => e[4] == 125), "25 percent more on ticks 1 to 50");
            Assert.IsTrue(after.All(e => e[4] == 100), "and back to normal from tick 51");
            Assert.Greater(Hits(drummed, 1).Count, 0);
            Assert.IsTrue(Hits(drummed, 1).All(e => e[4] == 100));

            var loud = BattleSim.Run(Field(), Team(), null, 120, new[] { spell.ToInput(12, 9, 11) });
            Assert.IsTrue(Hits(loud, 0).Where(e => e[0] <= 50).All(e => e[4] == 180), "80 percent more at the last level");
        }

        // ---------- Live battles ----------

        /// <summary>
        /// A battle fought live with casts gives exactly the log the server gets from the drop list, the cast list
        /// and the end tick.
        /// </summary>
        [Test]
        public void LiveCasts_MatchRunWithTheSameCasts()
        {
            var catalog = Spells();
            var available = catalog.Available(4);
            var team = MixedTeam(4);
            var live = BattleSim.Start(Outpost(), Guards(8));
            foreach (var hero in team) live.Deploy(hero);

            var sent = new List<RaidSpellCast>();
            int Cast(string id, int x, int z)
            {
                var cast = new RaidSpellCast { spellId = id, x = x, z = z, tick = live.Tick };
                Assert.AreEqual("", SpellCatalog.CastError(available, sent.Concat(new[] { cast }).ToList(), BattleSim.TickLimit));
                sent.Add(cast);
                return live.CastSpell(catalog.ToInput(available, cast));
            }

            for (var i = 0; i < 5; i++) live.Step();
            Assert.AreEqual(0, Cast("war_drum", 12, 11), "Casts are numbered from 0, in the order cast");
            Assert.AreEqual(1, live.SpellsPending);
            Assert.AreEqual(5, live.CastInput(0).tick);
            Assert.IsFalse(live.Events.Any(e => e[1] == (long)BattleEventKind.SpellCast), "It lands on the next tick");
            live.Step();
            Assert.AreEqual(0, live.SpellsPending);
            Assert.AreEqual("6,20,0,12,11,3", Line(live.Events.First(e => e[1] == (long)BattleEventKind.SpellCast)));
            Assert.AreEqual("war_drum", live.CastInput(0).id);

            while (live.Tick < 30) live.Step();
            Assert.AreEqual(1, Cast("frost_seal", 16, 15));
            Assert.AreEqual(2, Cast("ember_rain", 17, 17), "Two on one tick land in the order cast");
            Assert.AreEqual("ember_rain", live.CastInput(2).id);
            while (live.Tick < 120) live.Step();
            Assert.AreEqual(3, Cast("mending_mist", 14, 13));
            Assert.AreEqual("SPELL_USED", SpellCatalog.CastError(available, sent.Concat(new[] { new RaidSpellCast { spellId = "war_drum", x = 1, z = 1, tick = live.Tick } }).ToList(), BattleSim.TickLimit));
            while (live.Tick < 400 && live.Step()) { }
            var fought = live.Finish();

            Assert.AreEqual("", SpellCatalog.CastError(available, sent, fought.endTick));
            var server = BattleSim.Run(Outpost(), team, Guards(8), fought.endTick, catalog.ToInputs(available, sent));
            Assert.AreEqual(fought.checksum, server.checksum);
            Assert.AreEqual(fought.events.Count, server.events.Count);
            Assert.AreEqual(fought.stars, server.stars);
            Assert.AreEqual(fought.damagePercent, server.damagePercent);
            Assert.AreEqual(fought.heroesAlive, server.heroesAlive);
            foreach (var kind in new[] { BattleEventKind.SpellCast, BattleEventKind.SpellPulse, BattleEventKind.SpellHit, BattleEventKind.SpellHitDefender, BattleEventKind.SpellHeal, BattleEventKind.SpellFreeze, BattleEventKind.SpellBoost })
                Assert.Greater(Of(fought, kind).Count, 0, kind + " should be in this log");

            var without = BattleSim.Run(Outpost(), team, Guards(8), fought.endTick);
            Assert.AreNotEqual(without.checksum, fought.checksum, "The casts changed the battle");
        }

        [Test]
        public void CastAndCard_OnTheSameTick_LandTogether_TheCardFirst()
        {
            var drum = Spells().Find("war_drum");
            BattleResult Fight(bool castFirst)
            {
                var live = BattleSim.Start(Outpost());
                for (var i = 0; i < 10; i++) live.Step();
                if (castFirst) live.CastSpell(drum.ToInput(1, 10, 10));
                live.Deploy(Troop("Fighter", 600, 40, 10, 10));
                if (!castFirst) live.CastSpell(drum.ToInput(1, 10, 10));
                live.Step();
                // The troop lands first, so the drum that landed with it reaches it.
                CollectionAssert.AreEqual(new[] { "11,19,0,10,10,0", "11,20,0,10,10,3", "11,21,0,1,1,400", "11,26,0,0,50,25" }, live.Events.Take(4).Select(Line).ToList());
                while (live.Tick < 200 && live.Step()) { }
                return live.Finish();
            }

            var one = Fight(true);
            var other = Fight(false);
            Assert.AreEqual(one.checksum, other.checksum, "Whichever was tapped first");
            var server = BattleSim.Run(Outpost(), new[] { Troop("Fighter", 600, 40, 10, 10, 10) }, null, one.endTick, new[] { drum.ToInput(1, 10, 10, 10) });
            Assert.AreEqual(one.checksum, server.checksum);
        }

        [Test]
        public void ACastOnTheLastTick_NeedsOneMoreStepToLand()
        {
            var catalog = Spells();
            var available = catalog.Available(1);
            var cast = new RaidSpellCast { spellId = "ember_rain", x = 19, z = 19, tick = 20 };

            var live = BattleSim.Start(Outpost());
            for (var i = 0; i < 20; i++) live.Step();
            live.CastSpell(catalog.ToInput(available, cast));
            // Ended right here, the cast never landed, and the server would refuse the list: its tick is the end tick.
            Assert.AreEqual("BAD_DEPLOY_TICK", SpellCatalog.CastError(available, new[] { cast }, live.Tick));
            // So whoever ends a live battle plays one more tick first while a cast (or a card) is still waiting.
            Assert.AreEqual(1, live.SpellsPending);
            live.Step();
            var fought = live.Finish();
            Assert.AreEqual(21, fought.endTick);
            Assert.AreEqual(1, Of(fought, BattleEventKind.SpellCast).Count);
            Assert.AreEqual("", SpellCatalog.CastError(available, new[] { cast }, fought.endTick));
            Assert.AreEqual(fought.checksum, BattleSim.Run(Outpost(), Nobody, null, fought.endTick, catalog.ToInputs(available, new[] { cast })).checksum);
        }

        [Test]
        public void SpellsActive_CountsCastsWithPulsesStillToCome()
        {
            var catalog = Spells();
            var available = catalog.Available(1);
            var live = BattleSim.Start(Outpost());
            for (var i = 0; i < 5; i++) live.Step();
            Assert.AreEqual(0, live.SpellsActive);
            live.CastSpell(catalog.ToInput(available, new RaidSpellCast { spellId = "ember_rain", x = 19, z = 19, tick = live.Tick }));
            Assert.AreEqual(1, live.SpellsPending);
            Assert.AreEqual(0, live.SpellsActive, "Cast but not landed: waiting, not on the field");
            live.Step();
            Assert.AreEqual(0, live.SpellsPending);
            Assert.AreEqual(1, live.SpellsActive, "Landed, with two of its three pulses still to come");
            // Whoever ends a battle because nobody is left standing waits for these: fire still falling can still earn a star.
            var steps = 0;
            while (live.SpellsActive > 0 && steps++ < 100) live.Step();
            Assert.AreEqual(20, steps, "The last pulse comes twenty ticks after the first");
            Assert.AreEqual(3, live.Events.Count(e => e[1] == (long)BattleEventKind.SpellPulse));
        }

        [Test]
        public void Spells_NeedATimedBattle_AndSaneNumbers()
        {
            var spell = Spells().Find("ember_rain");
            Assert.Throws<FormatException>(() => BattleSim.Run(Outpost(), MixedTeam(1), null, 0, new[] { spell.ToInput(1, 10, 10) }), "The all-at-once kind has no spells");
            Assert.AreEqual(BattleSim.Run(Outpost(), MixedTeam(1)).checksum, BattleSim.Run(Outpost(), MixedTeam(1), null, 0, new List<BattleSpellInput>()).checksum);

            var timed = BattleSim.Run(Outpost(), MixedTeam(1), null, 300);
            Assert.AreEqual(timed.checksum, BattleSim.Run(Outpost(), MixedTeam(1), null, 300, null).checksum);
            Assert.AreEqual(timed.checksum, BattleSim.Run(Outpost(), MixedTeam(1), null, 300, new List<BattleSpellInput>()).checksum, "No casts, the same log as before spells existed");

            BattleSpellInput Bad(Action<BattleSpellInput> change)
            {
                var input = spell.ToInput(1, 10, 10);
                change(input);
                return input;
            }
            Assert.Throws<FormatException>(() => BattleSim.Run(Outpost(), Nobody, null, 300, new[] { Bad(s => s.effect = "storm") }));
            Assert.Throws<FormatException>(() => BattleSim.Run(Outpost(), Nobody, null, 300, new[] { Bad(s => s.pulses = 0) }));
            Assert.Throws<FormatException>(() => BattleSim.Run(Outpost(), Nobody, null, 300, new[] { Bad(s => s.pulseTicks = 0) }));
            Assert.Throws<FormatException>(() => BattleSim.Run(Outpost(), Nobody, null, 300, new[] { Bad(s => s.radius = -1) }));
            Assert.Throws<FormatException>(() => BattleSim.Run(Outpost(), Nobody, null, 300, new[] { Bad(s => s.power = -1) }));
            Assert.Throws<FormatException>(() => BattleSim.Start(Outpost()).CastSpell(Bad(s => s.pulses = 0)));
            Assert.Throws<FormatException>(() => BattleSim.EffectNumber("storm"));
            Assert.AreEqual(BattleSim.EffectBoost, BattleSim.EffectNumber("boost"));
        }
    }
}

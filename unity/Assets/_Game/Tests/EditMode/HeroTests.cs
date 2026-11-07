using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShinobiDominion.Core;

namespace ShinobiDominion.Tests
{
    public class HeroTests
    {
        private const string Legend = "solar_beast_sage";
        private static readonly Func<string, bool> NoneAssigned = id => false;
        private static string _json;

        private static HeroCatalog Catalog()
        {
            if (_json == null)
            {
                var candidates = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, "Config", "heroes.json"),
                    Path.Combine("Assets", "_Game", "Resources", "Config", "heroes.json")
                };
                _json = File.ReadAllText(candidates.First(File.Exists));
            }
            return HeroCatalog.FromJson(_json);
        }

        /// <summary>A roster holding a level 200 main, one duplicate, and all catalysts.</summary>
        private static HeroRoster Ready(out HeroInstance main, out HeroInstance duplicate)
        {
            var roster = new HeroRoster(Catalog());
            main = roster.AddHero(Legend, 0, 200);
            duplicate = roster.AddHero(Legend);
            foreach (var catalyst in roster.Catalog.CatalystByTransition) roster.AddItem(catalyst, 1);
            return roster;
        }

        // ---------- Content rules ----------

        [Test]
        public void Catalog_HasSixFunctionalHeroes_WithEveryRarity_AndTwoPlusLegendaries()
        {
            var complete = Catalog().All.Where(h => h.contentComplete).ToList();
            Assert.GreaterOrEqual(complete.Count, 6);
            Assert.That(complete.Any(h => h.rarity == HeroRarity.Common));
            Assert.That(complete.Any(h => h.rarity == HeroRarity.Elite));
            Assert.GreaterOrEqual(complete.Count(h => h.rarity == HeroRarity.Legendary), 2);
        }

        [Test]
        public void FunctionalLegendaries_HaveFourDistinctForms_AndNewSkillsAtEachEvolution()
        {
            foreach (var hero in Catalog().All.Where(h => h.contentComplete && h.CanEvolve))
            {
                Assert.AreEqual(4, hero.forms.Count, hero.id);
                Assert.AreEqual(4, hero.forms.Select(f => f.formName).Distinct().Count(), hero.id);
                Assert.AreEqual(4, hero.forms.Select(f => f.primaryColor).Distinct().Count(), hero.id);
                for (var e = 0; e <= 3; e++)
                    Assert.That(hero.skills.Any(s => s.unlockEvolution == e), $"{hero.id} needs a skill at Evo {e}");
            }
        }

        [Test]
        public void CatalystsAreDistinctForEachTransition()
        {
            Assert.AreEqual(3, Catalog().CatalystByTransition.Distinct().Count());
        }

        [Test]
        public void Catalog_RejectsNegativeGrowth()
        {
            var json = "{\"schemaVersion\":1,\"catalystByTransition\":[\"a\",\"b\",\"c\"],\"heroes\":[{\"id\":\"x\",\"baseHp\":1,\"growthHp\":-1}]}";
            Assert.Throws<FormatException>(() => HeroCatalog.FromJson(json));
        }

        [Test]
        public void HeroConfig_HasNoEnergyOrPityConcepts()
        {
            Catalog();
            var text = _json.ToLowerInvariant();
            foreach (var banned in new[] { "stamina", "pity", "guarantee" })
                StringAssert.DoesNotContain(banned, text);
        }

        // ---------- Power continuity ----------

        [Test]
        public void Cumulative_MatchesBriefFormula()
        {
            Assert.AreEqual(1, HeroMath.Cumulative(0, 1));
            Assert.AreEqual(200, HeroMath.Cumulative(0, 200));
            Assert.AreEqual(201, HeroMath.Cumulative(1, 1));
            Assert.AreEqual(800, HeroMath.Cumulative(3, 200));
            Assert.Throws<ArgumentOutOfRangeException>(() => HeroMath.Cumulative(4, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => HeroMath.Cumulative(0, 201));
            Assert.Throws<ArgumentOutOfRangeException>(() => HeroMath.Cumulative(0, 0));
        }

        [Test]
        public void EveryHero_PowerNeverDrops_AcrossAllLevelsAndEvolutions()
        {
            foreach (var hero in Catalog().All.Where(h => h.contentComplete))
            {
                var previous = HeroMath.Stats(hero, 0, 1);
                for (var e = 0; e <= 3; e++)
                for (var level = 1; level <= 200; level++)
                {
                    var stats = HeroMath.Stats(hero, e, level);
                    Assert.GreaterOrEqual(stats.Hp, previous.Hp);
                    Assert.GreaterOrEqual(stats.Attack, previous.Attack);
                    Assert.GreaterOrEqual(stats.Defense, previous.Defense);
                    Assert.Greater(stats.Hp, 0);
                    previous = stats;
                }
            }
        }

        // ---------- The exact evolution transaction ----------

        [Test]
        public void Evolve_ConsumesExactlyOneOfEach_PreservesMain_ResetsDisplayLevel()
        {
            var roster = Ready(out var main, out var duplicate);
            var bystander = roster.AddHero(Legend);
            var id = main.InstanceId;
            var powerBefore = roster.StatsOf(main);

            Assert.AreEqual(HeroError.None, roster.TryEvolve("k1", id, duplicate.InstanceId, NoneAssigned));

            Assert.AreSame(main, roster.Find(id), "Main hero keeps its identity");
            Assert.AreEqual(1, main.EvolutionIndex);
            Assert.AreEqual(1, main.Level);
            Assert.IsNull(roster.Find(duplicate.InstanceId));
            Assert.IsNotNull(roster.Find(bystander.InstanceId));
            Assert.AreEqual(0, roster.ItemCount("awakening_scroll"));
            Assert.AreEqual(1, roster.ItemCount("ascension_stone"), "Only this transition's catalyst is spent");
            Assert.Greater(roster.StatsOf(main).Attack, powerBefore.Attack);
        }

        [Test]
        public void DuplicateDoesNotNeedLevel200_ButMainDoes()
        {
            var roster = Ready(out var main, out var duplicate);
            Assert.AreEqual(1, duplicate.Level);
            Assert.AreEqual(HeroError.None, roster.CheckEvolution(main.InstanceId, duplicate.InstanceId, NoneAssigned));

            roster = new HeroRoster(Catalog());
            var low = roster.AddHero(Legend, 0, 199);
            var other = roster.AddHero(Legend, 0, 200);
            roster.AddItem("awakening_scroll", 1);
            Assert.AreEqual(HeroError.LevelTooLow, roster.TryEvolve("k", low.InstanceId, other.InstanceId, NoneAssigned));
            Assert.AreEqual(199, low.Level);
        }

        [Test]
        public void AllThreeTransitions_RequireTheirOwnCatalyst_AndFourthIsRefused()
        {
            var catalysts = Catalog().CatalystByTransition;
            for (var e = 0; e < 3; e++)
            {
                var roster = new HeroRoster(Catalog());
                var main = roster.AddHero(Legend, e, 200);
                var dup = roster.AddHero(Legend);
                Assert.AreEqual(HeroError.MissingCatalyst, roster.TryEvolve("none", main.InstanceId, dup.InstanceId, NoneAssigned));

                // The other transitions' catalysts must not count.
                foreach (var wrong in catalysts.Where(c => c != catalysts[e])) roster.AddItem(wrong, 1);
                Assert.AreEqual(HeroError.MissingCatalyst, roster.TryEvolve("wrong", main.InstanceId, dup.InstanceId, NoneAssigned));

                roster.AddItem(catalysts[e], 1);
                Assert.AreEqual(HeroError.None, roster.TryEvolve("right", main.InstanceId, dup.InstanceId, NoneAssigned));
                Assert.AreEqual(e + 1, main.EvolutionIndex, "Evolution rises by exactly one");
                Assert.AreEqual(0, roster.ItemCount(catalysts[e]));
            }

            var final = new HeroRoster(Catalog());
            var top = final.AddHero(Legend, 3, 200);
            var extra = final.AddHero(Legend);
            foreach (var catalyst in catalysts) final.AddItem(catalyst, 1);
            Assert.AreEqual(HeroError.AtMaxEvolution, final.TryEvolve("evo4", top.InstanceId, extra.InstanceId, NoneAssigned));
        }

        [Test]
        public void WrongDuplicate_SameRarityDifferentHero_IsRejected()
        {
            var roster = Ready(out var main, out _);
            var other = roster.AddHero("thunder_empress");
            Assert.AreEqual(HeroRarity.Legendary, roster.ArchetypeOf(other).rarity);
            Assert.AreEqual(HeroError.WrongArchetype, roster.TryEvolve("k", main.InstanceId, other.InstanceId, NoneAssigned));
            Assert.IsNotNull(roster.Find(other.InstanceId));
            Assert.AreEqual(1, roster.ItemCount("awakening_scroll"));
        }

        [Test]
        public void CannotSacrificeItself()
        {
            var roster = Ready(out var main, out _);
            Assert.AreEqual(HeroError.SameInstance, roster.TryEvolve("k", main.InstanceId, main.InstanceId, NoneAssigned));
            Assert.IsNotNull(roster.Find(main.InstanceId));
        }

        [Test]
        public void LockedAndAssignedDuplicates_AreProtected()
        {
            var roster = Ready(out var main, out var duplicate);
            roster.SetLocked(duplicate.InstanceId, true);
            Assert.AreEqual(HeroError.DuplicateLocked, roster.TryEvolve("a", main.InstanceId, duplicate.InstanceId, NoneAssigned));
            Assert.IsEmpty(roster.EligibleDuplicates(main.InstanceId, NoneAssigned));

            roster.SetLocked(duplicate.InstanceId, false);
            Func<string, bool> onBase = id => id == duplicate.InstanceId;
            Assert.AreEqual(HeroError.DuplicateAssigned, roster.TryEvolve("b", main.InstanceId, duplicate.InstanceId, onBase));
            Assert.IsEmpty(roster.EligibleDuplicates(main.InstanceId, onBase));
            Assert.IsNotNull(roster.Find(duplicate.InstanceId));
        }

        [Test]
        public void AssignedOrLockedMainHero_MayStillEvolve()
        {
            var roster = Ready(out var main, out var duplicate);
            roster.SetLocked(main.InstanceId, true);
            Func<string, bool> mainOnBase = id => id == main.InstanceId;
            Assert.AreEqual(HeroError.None, roster.TryEvolve("k", main.InstanceId, duplicate.InstanceId, mainOnBase));
        }

        [Test]
        public void NonLegendary_CannotEvolve()
        {
            var roster = new HeroRoster(Catalog());
            var common = roster.AddHero("river_scout", 0, 200);
            var dup = roster.AddHero("river_scout");
            Assert.AreEqual(HeroError.NotEvolvable, roster.TryEvolve("k", common.InstanceId, dup.InstanceId, NoneAssigned));
        }

        [Test]
        public void Retry_SameKey_DoesNotDoubleSpendOrDoubleEvolve()
        {
            var roster = Ready(out var main, out var duplicate);
            var dupId = duplicate.InstanceId;
            roster.AddItem("awakening_scroll", 5);
            Assert.AreEqual(HeroError.None, roster.TryEvolve("key", main.InstanceId, dupId, NoneAssigned));
            var scrolls = roster.ItemCount("awakening_scroll");

            for (var i = 0; i < 3; i++)
                Assert.AreEqual(HeroError.None, roster.TryEvolve("key", main.InstanceId, dupId, NoneAssigned));

            Assert.AreEqual(1, main.EvolutionIndex);
            Assert.AreEqual(scrolls, roster.ItemCount("awakening_scroll"));
            Assert.AreEqual(1, roster.EvolutionLog.Count);
        }

        [Test]
        public void Retry_SameKeyForDifferentRequest_IsRefused()
        {
            var roster = Ready(out var main, out var duplicate);
            var other = roster.AddHero(Legend);
            roster.TryEvolve("key", main.InstanceId, duplicate.InstanceId, NoneAssigned);
            Assert.AreEqual(HeroError.IdempotencyKeyReused, roster.TryEvolve("key", main.InstanceId, other.InstanceId, NoneAssigned));
            Assert.IsNotNull(roster.Find(other.InstanceId));
            Assert.AreEqual(HeroError.IdempotencyKeyRequired, roster.TryEvolve("", main.InstanceId, other.InstanceId, NoneAssigned));
        }

        [Test]
        public void SameDuplicate_CannotBeSpentTwice()
        {
            var roster = Ready(out var main, out var duplicate);
            var second = roster.AddHero(Legend, 0, 200);
            roster.AddItem("awakening_scroll", 1);
            roster.TryEvolve("a", main.InstanceId, duplicate.InstanceId, NoneAssigned);
            Assert.AreEqual(HeroError.DuplicateNotFound, roster.TryEvolve("b", second.InstanceId, duplicate.InstanceId, NoneAssigned));
        }

        // ---------- Levels ----------

        [Test]
        public void Xp_LevelsUp_StopsAt200_AndRejectsBadAmounts()
        {
            var roster = new HeroRoster(Catalog());
            var hero = roster.AddHero("river_scout");
            Assert.AreEqual(HeroError.InvalidAmount, roster.GrantXp(hero.InstanceId, -5));
            Assert.AreEqual(HeroError.InvalidAmount, roster.GrantXp(hero.InstanceId, 0));
            Assert.AreEqual(HeroError.None, roster.GrantXp(hero.InstanceId, roster.XpToNextLevel(1)));
            Assert.AreEqual(2, hero.Level);

            Assert.AreEqual(HeroError.None, roster.GrantXp(hero.InstanceId, 100_000_000));
            Assert.AreEqual(200, hero.Level);
            Assert.AreEqual(HeroError.AtMaxLevel, roster.GrantXp(hero.InstanceId, 1));
        }

        // ---------- Persistence and identity ----------

        [Test]
        public void Instances_HaveUniqueIds_SeparateFromArchetype()
        {
            var roster = new HeroRoster(Catalog());
            var a = roster.AddHero(Legend);
            var b = roster.AddHero(Legend);
            Assert.AreNotEqual(a.InstanceId, b.InstanceId);
            Assert.AreEqual(a.ArchetypeId, b.ArchetypeId);
            Assert.AreEqual(2, roster.HeroCount);
        }

        [Test]
        public void SaveLoad_RoundTrips_IncludingIdempotencyRecords()
        {
            var roster = Ready(out var main, out var duplicate);
            var dupId = duplicate.InstanceId;
            roster.TryEvolve("key", main.InstanceId, dupId, NoneAssigned);
            roster.SetLocked(main.InstanceId, true);

            var loaded = HeroRoster.FromJson(roster.Catalog, roster.ToJson());
            var again = loaded.Find(main.InstanceId);
            Assert.AreEqual(1, again.EvolutionIndex);
            Assert.IsTrue(again.Locked);
            Assert.AreEqual(1, loaded.ItemCount("ascension_stone"));

            Assert.AreEqual(HeroError.None, loaded.TryEvolve("key", main.InstanceId, dupId, NoneAssigned));
            Assert.AreEqual(1, again.EvolutionIndex, "A retry after reload must still not evolve twice");
        }

        [TestCase("{\"schemaVersion\":1,\"heroes\":[{\"instanceId\":\"a\",\"archetypeId\":\"nope\",\"level\":1}]}")]
        [TestCase("{\"schemaVersion\":1,\"heroes\":[{\"instanceId\":\"a\",\"archetypeId\":\"river_scout\",\"level\":201}]}")]
        [TestCase("{\"schemaVersion\":1,\"heroes\":[{\"instanceId\":\"a\",\"archetypeId\":\"river_scout\",\"level\":1,\"evolutionIndex\":1}]}")]
        [TestCase("{\"schemaVersion\":1,\"heroes\":[{\"instanceId\":\"a\",\"archetypeId\":\"solar_beast_sage\",\"level\":1,\"evolutionIndex\":4}]}")]
        [TestCase("{\"schemaVersion\":1,\"items\":{\"awakening_scroll\":-1}}")]
        [TestCase("{\"schemaVersion\":99}")]
        [TestCase("{ not json")]
        public void Load_RejectsForgedOrCorruptRosters(string json)
        {
            Assert.Throws<VillageSaveException>(() => HeroRoster.FromJson(Catalog(), json));
        }

        [Test]
        public void DevInventory_IsNotASummon_AndAllowsSixLegendaries()
        {
            var roster = DevInventory.Create(Catalog());
            Assert.GreaterOrEqual(roster.Heroes.Count(h => roster.ArchetypeOf(h).rarity == HeroRarity.Legendary), 6);
            Assert.That(roster.Heroes.All(h => roster.ArchetypeOf(h).contentComplete), "Incomplete content must not be handed out");
        }

        [Test]
        public void AnyLegendary_CanTakeTheOneBase()
        {
            var roster = DevInventory.Create(Catalog());
            var village = TestCatalog.EmptyVillage();
            village.TryPlace("hero_base", new GridCoord(0, 0), Rotation.R0, 0, out var id);
            foreach (var legend in roster.Heroes.Where(h => roster.ArchetypeOf(h).rarity == HeroRarity.Legendary).Take(6))
                Assert.AreEqual(VillageError.None, village.TryAssignHero(id, legend.InstanceId));
        }
    }
}

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShinobiDominion.Core;

namespace ShinobiDominion.Tests
{
    /// <summary>Mana, the third currency (ADR-038). The shared scenarios cover the rules both sides run.</summary>
    public class ManaTests
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

        [Test]
        public void ManaMill_IsAThreeByThreeManaCollector_InTheCatalogue()
        {
            var mill = TestCatalog.Load().Get("mana_mill");
            Assert.AreEqual(BuildingFunction.Collector, mill.function);
            Assert.AreEqual(ResourceKind.Mana, mill.produces);
            Assert.AreEqual(3, mill.width);
            Assert.AreEqual(3, mill.depth);
            Assert.AreEqual(BuildingCategory.Resources, mill.category);
            Assert.GreaterOrEqual(mill.MaxCountAt(1), 1, "a new player must be able to build one");
            for (var level = 2; level <= mill.maxLevel; level++)
                Assert.Greater(mill.ProductionAt(level), mill.ProductionAt(level - 1));
        }

        [Test]
        public void NewVillage_StartsWithTheCataloguesMana()
        {
            var catalog = TestCatalog.Load();
            Assert.AreEqual(catalog.StartingMana, StarterVillage.Create(catalog, 0).Mana);
            Assert.Greater(catalog.StartingMana, 0);
        }

        [Test]
        public void Save_RoundTripsMana_AndRefusesNegativeMana()
        {
            var village = TestCatalog.EmptyVillage();
            var save = village.ToSave();
            save.mana = 1234;
            village.Restore(save);
            Assert.AreEqual(1234, Village.FromSave(village.Catalog, VillageSave.FromJson(village.ToSave().ToJson())).Mana);

            save.mana = -1;
            Assert.Throws<VillageSaveException>(() => village.Restore(save));
            Assert.AreEqual(1234, village.Mana, "a bad save changes nothing");
        }

        [Test]
        public void ServerState_ReadsManaFromTheVillage()
        {
            var state = ServerState.Parse("{\"crystals\":5,\"heroes\":[],\"items\":{},\"village\":{\"schemaVersion\":1,\"mapVersion\":1,\"nextInstanceId\":1,\"gold\":10,\"crystals\":5,\"mana\":777,\"buildings\":[]}}");
            Assert.AreEqual(777, state.Village.mana);
        }

        [Test]
        public void SpendMana_RefusesMoreThanTheBalance_AndNegativeAmounts()
        {
            var village = TestCatalog.EmptyVillage();
            var start = village.Mana;
            Assert.AreEqual(VillageError.InsufficientMana, village.TrySpendMana(start + 1));
            Assert.AreEqual(VillageError.InsufficientMana, village.TrySpendMana(-5));
            Assert.AreEqual(start, village.Mana);
            Assert.AreEqual(VillageError.None, village.TrySpendMana(start));
            Assert.AreEqual(0, village.Mana);
        }

        [Test]
        public void EveryTroop_CostsMana()
        {
            foreach (var troop in TroopCatalog.FromJson(ReadConfig("troops.json")).All)
                Assert.Greater(troop.manaCost, 0, troop.id);
        }

        [Test]
        public void HeroTraining_UnknownHero_ChargesNothing()
        {
            var village = TestCatalog.EmptyVillage();
            var roster = new HeroRoster(HeroCatalog.FromJson(ReadConfig("heroes.json")));
            var mana = village.Mana;
            Assert.AreEqual(HeroError.HeroNotFound, roster.TryTrainWithMana("nope", village, out _));
            Assert.AreEqual(mana, village.Mana);
        }
    }
}

using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShinobiDominion.Core;
using ShinobiDominion.Game;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShinobiDominion.Tests
{
    public class HeroFlowTests
    {
        private string _savePath;
        private GameObject _root;
        private VillageController Controller;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _savePath = Path.Combine(Application.temporaryCachePath, "hero_village_" + System.Guid.NewGuid().ToString("N") + ".json");
            VillageBootstrap.SavePathOverride = _savePath;
            Boot();
            yield return null;
        }

        private void Boot()
        {
            _root = new GameObject("VillageBootstrapUnderTest");
            Controller = _root.AddComponent<VillageBootstrap>().Controller;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_root);
            VillageBootstrap.SavePathOverride = null;
            yield return null;
            foreach (var file in new[] { _savePath, _savePath + ".heroes" })
                if (File.Exists(file)) File.Delete(file);
        }

        private BuildingInstance First(string id) => Controller.Village.Buildings.First(b => b.Definition.id == id);
        private Button[] Buttons() => _root.GetComponentsInChildren<Button>(false);
        private Button Find(string name) => Buttons().FirstOrDefault(b => b.name == name);

        private static void Click(Button button) => button.onClick.Invoke();

        [UnityTest]
        public IEnumerator Altar_ListsEveryOwnedHero_ThroughTheBuilding()
        {
            Controller.Select(First("hero_altar").Id);
            Controller.ShowAltarRoster();
            yield return null;
            var rows = Buttons().Count(b => b.name.StartsWith("Hero_"));
            Assert.Greater(Controller.Roster.HeroCount, 0);
            Assert.AreEqual(Controller.Roster.HeroCount, rows);
        }

        [UnityTest]
        public IEnumerator Altar_RarityFilter_ShowsOnlyThatRarity()
        {
            Controller.Select(First("hero_altar").Id);
            Controller.ShowAltarRoster();
            yield return null;
            Click(Find("FilterLegendary"));
            yield return null;
            var expected = Controller.Roster.Heroes.Count(h => Controller.Roster.ArchetypeOf(h).rarity == HeroRarity.Legendary);
            Assert.AreEqual(expected, Buttons().Count(b => b.name.StartsWith("Hero_")));
            Click(Find("FilterAll"));
        }

        [UnityTest]
        public IEnumerator HeroBase_AssignThroughPicker_ThenSwapForAnother()
        {
            var bases = Controller.Village.Buildings.Where(b => b.Definition.function == BuildingFunction.HeroBase).ToList();
            Assert.AreEqual(1, bases.Count, "A village has one Hero Base");
            var hero = Controller.Roster.Heroes.First();
            var other = Controller.Roster.Heroes.First(h => h.InstanceId != hero.InstanceId);

            Controller.Select(bases[0].Id);
            yield return null;
            Click(Find("ChangeHeroButton"));
            yield return null;
            Click(Find("Pick_" + hero.InstanceId));
            yield return null;
            Assert.AreEqual(hero.InstanceId, bases[0].AssignedHeroInstanceId);

            Controller.CloseAll();
            Controller.Select(bases[0].Id);
            yield return null;
            Click(Find("ChangeHeroButton"));
            yield return null;
            Assert.IsFalse(Find("Pick_" + hero.InstanceId).interactable, "The hero already there cannot be picked again");
            Click(Find("Pick_" + other.InstanceId));
            yield return null;
            Assert.AreEqual(other.InstanceId, bases[0].AssignedHeroInstanceId, "Picking another hero swaps them");
        }

        [UnityTest]
        public IEnumerator Altar_ShowsConceptPaintings_ForBaseFormHeroesThatHaveThem()
        {
            Controller.Select(First("hero_altar").Id);
            Controller.ShowAltarRoster();
            yield return null;
            var painted = Controller.Roster.Heroes.First(h => h.EvolutionIndex == 0 && HeroConcepts.Has(h.ArchetypeId));
            var card = Find("Hero_" + painted.InstanceId);
            Assert.IsNotNull(card, "The Altar should list the hero");
            var concept = card.GetComponentsInChildren<Image>().FirstOrDefault(i => i.name == "HeroConcept")?.transform;
            Assert.IsNotNull(concept, "A base-form hero with a concept painting should show it");
            Assert.IsNotNull(concept.GetComponent<Image>().sprite);

            var unpainted = Controller.Roster.Heroes.FirstOrDefault(h => !HeroConcepts.Has(h.ArchetypeId));
            if (unpainted != null) Assert.IsNull(Find("Hero_" + unpainted.InstanceId).GetComponentsInChildren<Image>().FirstOrDefault(i => i.name == "HeroConcept"), "Heroes without a painting keep their old picture");

            Controller.ShowHeroDetail(painted.InstanceId);
            yield return null;
            var detail = Object.FindObjectsByType<Image>(FindObjectsSortMode.None).FirstOrDefault(i => i.name == "HeroConcept");
            Assert.IsNotNull(detail, "The hero's own page should show the whole painting");
            Assert.Greater(detail.rectTransform.sizeDelta.y, 250f);
        }

        [Test]
        public void EveryLegendary_HasAConceptPainting()
        {
            foreach (var hero in Controller.Roster.Catalog.All.Where(a => a.rarity == HeroRarity.Legendary))
                Assert.IsTrue(HeroConcepts.Has(hero.id), hero.id);
        }

        [Test]
        public void HeroBase_RejectsHeroThePlayerDoesNotOwn()
        {
            var heroBase = First("hero_base");
            Assert.AreEqual(VillageError.InvalidHeroId, Controller.AssignHero(heroBase.Id, "not-my-hero"));
            Assert.IsNull(heroBase.AssignedHeroInstanceId);
        }

        [UnityTest]
        public IEnumerator OnlyOneHeroBase_CanBeBuilt_AndAnyLegendaryCanStandOnIt()
        {
            Assert.AreEqual(VillageError.LimitReached, Controller.Village.TryPlace("hero_base", new GridCoord(10, 10), Rotation.R0, 0, out _));
            var heroBase = First("hero_base");
            foreach (var legend in Controller.Roster.Heroes.Where(h => Controller.Roster.ArchetypeOf(h).rarity == HeroRarity.Legendary).Take(6))
                Assert.AreEqual(VillageError.None, Controller.AssignHero(heroBase.Id, legend.InstanceId));
            Assert.AreEqual(1, Controller.Village.Buildings.Count(b => b.AssignedHeroInstanceId != null));
            yield return null;
        }

        [UnityTest]
        public IEnumerator EvolutionFlow_FromAltar_ConsumesDuplicate_AndKeepsMain()
        {
            var roster = Controller.Roster;
            var main = roster.Heroes.First(h => h.ArchetypeId == "solar_beast_sage" && h.Level == HeroMath.MaxLevel);
            var duplicate = roster.Heroes.First(h => h.ArchetypeId == "solar_beast_sage" && h.InstanceId != main.InstanceId);
            var mainId = main.InstanceId;
            var duplicateId = duplicate.InstanceId;

            Controller.Select(First("hero_altar").Id);

            Controller.ShowAltarRoster();
            yield return null;
            Click(Find("Hero_" + mainId));
            yield return null;
            Click(Find("EvolveButton"));
            yield return null;
            Click(Find("Sacrifice_" + duplicateId));
            yield return null;

            var confirm = Find("ConfirmEvolveButton");
            Click(confirm);
            Click(confirm); // A double tap must not evolve twice.
            yield return null;

            Assert.AreEqual(1, roster.Find(mainId).EvolutionIndex);
            Assert.AreEqual(1, roster.Find(mainId).Level);
            Assert.IsNull(roster.Find(duplicateId));
            Assert.AreEqual(0, roster.ItemCount("awakening_scroll"));
        }

        [UnityTest]
        public IEnumerator Evolution_RefusesAssignedDuplicate_InTheUi()
        {
            var roster = Controller.Roster;
            var main = roster.Heroes.First(h => h.ArchetypeId == "solar_beast_sage" && h.Level == HeroMath.MaxLevel);
            var duplicate = roster.Heroes.First(h => h.ArchetypeId == "solar_beast_sage" && h.InstanceId != main.InstanceId);
            Controller.AssignHero(First("hero_base").Id, duplicate.InstanceId);

            Controller.Select(First("hero_altar").Id);

            Controller.ShowAltarRoster();
            yield return null;
            Controller.ShowEvolution(main.InstanceId);
            yield return null;
            Assert.IsNull(Find("Sacrifice_" + duplicate.InstanceId), "A copy on a base must not be offered");
        }

        [UnityTest]
        public IEnumerator HeroesAndAssignments_SurviveReload()
        {
            var hero = Controller.Roster.Heroes.First();
            var heroId = hero.InstanceId;
            var baseId = First("hero_base").Id;
            Controller.AssignHero(baseId, heroId);
            Controller.Roster.SetLocked(heroId, true);
            yield return null;

            var count = Controller.Roster.HeroCount;
            Object.Destroy(_root);
            yield return null;
            Boot();
            yield return null;

            Assert.AreEqual(count, Controller.Roster.HeroCount);
            Assert.IsTrue(Controller.Roster.Find(heroId).Locked);
            Assert.AreEqual(heroId, Controller.Village.Find(baseId).AssignedHeroInstanceId);
        }

        [UnityTest]
        public IEnumerator SummoningGate_StillDoesNoClientRolls()
        {
            var heroes = Controller.Roster.HeroCount;
            Controller.Select(First("summoning_gate").Id);
            Controller.ShowSummoningGate();
            yield return null;
            Assert.AreEqual(heroes, Controller.Roster.HeroCount);
            Assert.IsFalse(Buttons().Any(b => b.name.ToLowerInvariant().Contains("summon")));
        }
    }
}

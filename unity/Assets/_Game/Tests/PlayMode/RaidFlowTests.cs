using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShinobiDominion.Core;
using ShinobiDominion.Game;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShinobiDominion.Tests
{
    /// <summary>The whole attack flow through the real controller, offline, as a practice battle.</summary>
    public class RaidFlowTests
    {
        private string _savePath;
        private GameObject _root;
        private VillageController Controller;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _savePath = Path.Combine(Application.temporaryCachePath, "raid_" + System.Guid.NewGuid().ToString("N") + ".json");
            VillageBootstrap.SavePathOverride = _savePath;
            _root = new GameObject("VillageBootstrapUnderTest");
            Controller = _root.AddComponent<VillageBootstrap>().Controller;
            yield return null;
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

        private void AssignTeam(int count)
        {
            var village = Controller.Village;
            var bases = village.Buildings.Where(b => b.Definition.function == BuildingFunction.HeroBase).ToList();
            for (var x = 0; bases.Count < count; x += 3)
            {
                Assert.AreEqual(VillageError.None, village.TryPlace("hero_base", new GridCoord(x, 40), Rotation.R0, 0, out _));
                bases = village.Buildings.Where(b => b.Definition.function == BuildingFunction.HeroBase).ToList();
            }
            var legends = Controller.Roster.Heroes.Where(h => h.ArchetypeId == "solar_beast_sage").ToList();
            while (legends.Count < count)
            {
                Controller.Roster.AddHero("solar_beast_sage", 0, HeroMath.MaxLevel);
                legends = Controller.Roster.Heroes.Where(h => h.ArchetypeId == "solar_beast_sage").ToList();
            }
            for (var i = 0; i < count; i++) Controller.AssignHero(bases[i].Id, legends[i].InstanceId);
        }

        [UnityTest]
        public IEnumerator FullRaid_RunsToResult_AndComesBackHome()
        {
            AssignTeam(6);
            var buildings = Controller.Village.BuildingCount;
            var gold = Controller.Village.Gold;

            Controller.OpenAttack();
            Assert.AreEqual(VillageScreen.AttackFlow, Controller.UiState.Screen);
            Controller.ScoutRaid("ridge_outpost");
            Assert.AreEqual(RaidPhase.Prepare, Controller.CurrentRaidPhase);
            Assert.IsFalse(Controller.Hud.AttackButton.gameObject.activeSelf, "ATTACK stays hidden during a raid");

            Controller.AutoPlaceHeroes();
            Assert.AreEqual(6, Controller.RaidPlacements.Count);
            Controller.StartBattle();
            Assert.AreEqual(RaidPhase.Battle, Controller.CurrentRaidPhase);

            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(RaidPhase.Battle, Controller.CurrentRaidPhase, "Playback should still be running");

            Controller.SkipBattle();
            Assert.AreEqual(RaidPhase.Resolved, Controller.CurrentRaidPhase);
            Assert.AreEqual(3, Controller.LastRaidOutcome.Result.stars, "Six level 200 legends should clear this base");
            Assert.AreEqual(gold, Controller.Village.Gold, "A practice battle pays nothing");

            Controller.FinishRaid();
            yield return null;
            Assert.IsNull(Controller.CurrentRaidPhase);
            Assert.AreEqual(VillageScreen.Home, Controller.UiState.Screen);
            Assert.IsTrue(Controller.Hud.AttackButton.gameObject.activeSelf);
            Assert.AreEqual(buildings, Controller.Village.BuildingCount, "A raid never touches the player's own village");
            Assert.IsNull(GameObject.Find("BattleStage"));
        }

        [UnityTest]
        public IEnumerator Playback_ReachesTheResult_WithoutSkipping()
        {
            AssignTeam(6);
            Controller.OpenAttack();
            Controller.ScoutRaid("ridge_outpost");
            Controller.AutoPlaceHeroes();
            Controller.StartBattle();
            var limit = Time.time + 90f;
            Time.timeScale = 20f;
            while (Controller.CurrentRaidPhase == RaidPhase.Battle && Time.time < limit) yield return null;
            Time.timeScale = 1f;
            Assert.AreEqual(RaidPhase.Resolved, Controller.CurrentRaidPhase);
            Controller.FinishRaid();
            yield return null;
        }

        [Test]
        public void NoHeroes_CannotScout()
        {
            foreach (var building in Controller.Village.Buildings.Where(b => b.AssignedHeroInstanceId != null).ToList())
                Controller.Village.TryUnassignHero(building.Id);
            Controller.OpenAttack();
            Assert.IsNull(Controller.CurrentRaidPhase);
            Controller.CloseAll();
            Assert.AreEqual(VillageScreen.Home, Controller.UiState.Screen);
        }

        [UnityTest]
        public IEnumerator Placement_RefusesCellsNearTheEnemy_AndPicksHeroesBackUp()
        {
            AssignTeam(2);
            Controller.OpenAttack();
            Controller.ScoutRaid("ridge_outpost");
            Controller.TapDeployCell(new GridCoord(25, 25));
            Assert.AreEqual(0, Controller.RaidPlacements.Count, "Inside the enemy base");
            Controller.TapDeployCell(new GridCoord(15, 20));
            Assert.AreEqual(0, Controller.RaidPlacements.Count, "Right beside the wall");
            Controller.TapDeployCell(new GridCoord(3, 3));
            Assert.AreEqual(1, Controller.RaidPlacements.Count);
            Controller.TapDeployCell(new GridCoord(3, 3));
            Assert.AreEqual(0, Controller.RaidPlacements.Count, "Tapping a placed hero picks it up");
            Controller.RetreatRaid();
            yield return null;
            Assert.IsNull(Controller.CurrentRaidPhase);
            Assert.IsTrue(Controller.Hud.AttackButton.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Outcome_FromTheLog_MatchesWhatThePlaybackShows()
        {
            AssignTeam(6);
            Controller.OpenAttack();
            Controller.ScoutRaid("ridge_outpost");
            Controller.AutoPlaceHeroes();
            Controller.StartBattle();
            Controller.SkipBattle();
            var stage = Object.FindAnyObjectByType<BattleStage>();
            Assert.AreEqual(Controller.LastRaidOutcome.Result.damagePercent, stage.DamagePercent);
            Controller.FinishRaid();
            yield return null;
        }
    }
}

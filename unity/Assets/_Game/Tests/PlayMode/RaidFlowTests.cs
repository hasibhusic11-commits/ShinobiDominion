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
            var legends = Controller.Roster.Heroes.Where(h => h.ArchetypeId == "solar_beast_sage" && h.Level == HeroMath.MaxLevel).ToList();
            while (legends.Count < count)
            {
                Controller.Roster.AddHero("solar_beast_sage", 0, HeroMath.MaxLevel);
                legends = Controller.Roster.Heroes.Where(h => h.ArchetypeId == "solar_beast_sage" && h.Level == HeroMath.MaxLevel).ToList();
            }
            for (var i = 0; i < count; i++) Controller.AssignHero(bases[i].Id, legends[i].InstanceId);
        }

        [UnityTest]
        public IEnumerator FullRaid_RunsToResult_AndComesBackHome()
        {
            AssignTeam(1);
            var buildings = Controller.Village.BuildingCount;
            var gold = Controller.Village.Gold;

            Controller.OpenAttack();
            Assert.AreEqual(VillageScreen.AttackFlow, Controller.UiState.Screen);
            Controller.ScoutRaid("ridge_outpost");
            Assert.AreEqual(RaidPhase.Battle, Controller.CurrentRaidPhase, "The clock runs from the start (ADR-050)");
            Assert.IsFalse(Controller.Hud.AttackButton.gameObject.activeSelf, "ATTACK stays hidden during a raid");

            yield return new WaitForSeconds(0.5f);
            Assert.Greater(Controller.LiveTick, 0, "The clock runs on an empty field");
            Assert.AreEqual(RaidPhase.Battle, Controller.CurrentRaidPhase, "An empty field does not end the battle");
            Controller.AutoPlaceHeroes();
            Assert.AreEqual(1, Controller.RaidPlacements.Count);
            Assert.Greater(Controller.RaidPlacements[0].tick, 0, "The hero lands at the moment it was dropped");

            var scale = Time.timeScale;
            Time.timeScale = 20f;
            var limit = Time.realtimeSinceStartup + 60f;
            while (Controller.CurrentRaidPhase == RaidPhase.Battle && Time.realtimeSinceStartup < limit) yield return null;
            Time.timeScale = scale;
            Assert.AreEqual(RaidPhase.Resolved, Controller.CurrentRaidPhase);
            Assert.AreEqual(3, Controller.LastRaidOutcome.Result.stars, "One level 200 legend should clear this base");
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
            AssignTeam(1);
            Controller.OpenAttack();
            Controller.ScoutRaid("ridge_outpost");
            Controller.AutoPlaceHeroes();
            var limit = Time.realtimeSinceStartup + 60f;
            Time.timeScale = 20f;
            while (Controller.CurrentRaidPhase == RaidPhase.Battle && Time.realtimeSinceStartup < limit) yield return null;
            Time.timeScale = 1f;
            Assert.AreEqual(RaidPhase.Resolved, Controller.CurrentRaidPhase);
            Controller.FinishRaid();
            yield return null;
        }

        private void ReadyArmy()
        {
            var save = Controller.Village.ToSave();
            save.gold = 20000;
            Controller.Village.Restore(save);
            Assert.AreEqual(VillageError.None, Controller.Village.TryPlace("training_grounds", new GridCoord(38, 30), Rotation.R0, 0, out _));
            Assert.AreEqual(VillageError.None, Controller.Village.TryPlace("troop_camp", new GridCoord(38, 36), Rotation.R0, 0, out _));
        }

        [UnityTest]
        public IEnumerator Troops_TrainInTheHall_LandInARaid_AndAreUsedUp()
        {
            ReadyArmy();
            AssignTeam(1);
            var gold = Controller.Village.Gold;
            for (var i = 0; i < 8; i++) Controller.TrainTroop("militia", 1);
            Assert.AreEqual(8, Controller.Army.CountOf("militia"));
            Assert.AreEqual(gold - 8 * 25, Controller.Village.Gold);

            Controller.OpenAttack();
            Controller.ScoutRaid("ridge_outpost");
            Controller.CycleTroop();
            Controller.TapDeployCell(new GridCoord(3, 3));
            Controller.CycleTroop();
            Controller.AutoPlaceHeroes();
            Assert.AreEqual(RaidPhase.Battle, Controller.CurrentRaidPhase);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(9, Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(r => r.name == "BattleTroop" || r.name == "BattleHero"));
            Controller.SkipBattle();
            Assert.AreEqual(RaidPhase.Resolved, Controller.CurrentRaidPhase);
            Assert.AreEqual(0, Controller.Army.CountOf("militia"), "Troops that land are used up");
            Controller.FinishRaid();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Troops_NotDropped_AreNotUsed_ButDroppedOnesAre()
        {
            ReadyArmy();
            AssignTeam(1);
            Controller.TrainTroop("militia", 3);
            Controller.OpenAttack();
            Controller.ScoutRaid("ridge_outpost");
            Controller.CycleTroop();
            Controller.RetreatRaid();
            yield return null;
            Assert.IsNull(Controller.CurrentRaidPhase, "Leaving before any drop costs nothing");
            Assert.AreEqual(3, Controller.Army.CountOf("militia"));

            Controller.OpenAttack();
            Controller.ScoutRaid("ridge_outpost");
            Controller.CycleTroop();
            Controller.TapDeployCell(new GridCoord(3, 3));
            Controller.RetreatRaid();
            Assert.AreEqual(RaidPhase.Resolved, Controller.CurrentRaidPhase, "Once a card has fallen, leaving ends the battle");
            Assert.AreEqual(0, Controller.Army.CountOf("militia"), "Dropped troops are spent: there is no picking back up");
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
        public IEnumerator Drops_RefuseCellsNearTheEnemy_AndLandWhenDropped()
        {
            AssignTeam(1);
            Controller.OpenAttack();
            Controller.ScoutRaid("ridge_outpost");
            Controller.TapDeployCell(new GridCoord(25, 25));
            Assert.AreEqual(0, Controller.RaidPlacements.Count, "Inside the enemy base");
            Controller.TapDeployCell(new GridCoord(15, 20));
            Assert.AreEqual(0, Controller.RaidPlacements.Count, "Right beside the wall");
            Assert.IsNotNull(Controller.LastMessage, "A refused drop says why");
            yield return new WaitForSeconds(0.35f);
            var tick = Controller.LiveTick;
            Controller.TapDeployCell(new GridCoord(3, 3));
            Assert.AreEqual(1, Controller.RaidPlacements.Count);
            Assert.AreEqual(tick, Controller.RaidPlacements[0].tick, "A card lands at the tick it was dropped");
            Controller.TapDeployCell(new GridCoord(3, 3));
            Assert.AreEqual(1, Controller.RaidPlacements.Count, "A dropped hero stays: there is no picking back up");
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(1, Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(r => r.name == "BattleHero"), "The hero is on the field");
            Controller.SkipBattle();
            Assert.AreEqual(RaidPhase.Resolved, Controller.CurrentRaidPhase);
            Assert.AreEqual(tick + 1, Controller.LastLiveResult.events.First(e => e[1] == (long)BattleEventKind.Deploy)[0], "It acts from the next tick");
            Controller.FinishRaid();
            yield return null;
            Assert.IsNull(Controller.CurrentRaidPhase);
            Assert.IsTrue(Controller.Hud.AttackButton.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Outcome_FromTheLog_MatchesWhatThePlaybackShows()
        {
            AssignTeam(1);
            Controller.OpenAttack();
            Controller.ScoutRaid("ridge_outpost");
            Controller.AutoPlaceHeroes();
            yield return new WaitForSeconds(1f);
            Controller.SkipBattle();
            var stage = Object.FindAnyObjectByType<BattleStage>();
            Assert.AreEqual(Controller.LastRaidOutcome.Result.damagePercent, stage.DamagePercent);
            Controller.FinishRaid();
            yield return null;
        }
    }
}

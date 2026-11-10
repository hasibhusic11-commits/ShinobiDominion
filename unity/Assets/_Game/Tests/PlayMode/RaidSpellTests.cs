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
    /// <summary>
    /// Spells in a live raid (ADR-052), and what the battle screen shows around them: the round buttons, the ring
    /// that aims, the painted stars, the clock that warns, the lean-in when the Town Hall falls and the ruin a
    /// fallen building leaves.
    /// </summary>
    public class RaidSpellTests
    {
        private string _savePath;
        private GameObject _root;
        private VillageController Controller;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _savePath = Path.Combine(Application.temporaryCachePath, "spell_" + System.Guid.NewGuid().ToString("N") + ".json");
            VillageBootstrap.SavePathOverride = _savePath;
            _root = new GameObject("VillageBootstrapUnderTest");
            Controller = _root.AddComponent<VillageBootstrap>().Controller;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            Object.Destroy(_root);
            VillageBootstrap.SavePathOverride = null;
            yield return null;
            foreach (var file in new[] { _savePath, _savePath + ".heroes" })
                if (File.Exists(file)) File.Delete(file);
        }

        private void AssignTeam()
        {
            var village = Controller.Village;
            if (!village.Buildings.Any(b => b.Definition.function == BuildingFunction.HeroBase))
                Assert.AreEqual(VillageError.None, village.TryPlace("hero_base", new GridCoord(0, 40), Rotation.R0, 0, out _));
            var home = village.Buildings.First(b => b.Definition.function == BuildingFunction.HeroBase);
            var legend = Controller.Roster.AddHero("solar_beast_sage", 0, HeroMath.MaxLevel);
            Controller.AssignHero(home.Id, legend.InstanceId);
        }

        /// <summary>A level 1 Relic Hall: it opens the first spell.</summary>
        private void BuildRelicHall()
        {
            var save = Controller.Village.ToSave();
            save.gold = 20000;
            // The Relic Hall opens at Town Hall level 2.
            save.buildings.Find(b => b.definitionId == "dojo_hq").level = 2;
            Controller.Village.Restore(save);
            Assert.AreEqual(VillageError.None, Controller.Village.TryPlace("research_hut", new GridCoord(38, 30), Rotation.R0, 0, out _));
        }

        private static Text LevelPlate(string spell) =>
            GameObject.Find("SpellButton_" + spell).transform.Find("Level").GetComponentInChildren<Text>();

        [UnityTest]
        public IEnumerator NoRelicHall_NoSpellButtons()
        {
            AssignTeam();
            Controller.OpenAttack();
            Controller.ScoutRaid("ridge_outpost");
            yield return null;
            Assert.IsNull(GameObject.Find("SpellRow"), "No hall, no spells");
            Controller.SelectSpell("ember_rain");
            Assert.IsNull(Controller.SelectedSpell, "Nothing to pick either");
            Controller.SkipBattle();
            Controller.FinishRaid();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Spell_WaitsForAnArmy_IsCastOnce_AndGoesIntoTheBattle()
        {
            BuildRelicHall();
            AssignTeam();
            Controller.OpenAttack();
            Controller.ScoutRaid("ridge_outpost");
            yield return null;

            Assert.IsNotNull(GameObject.Find("SpellRow"));
            Assert.IsNotNull(GameObject.Find("SpellButton_ember_rain"), "The first spell opens with the hall");
            Assert.IsNull(GameObject.Find("SpellButton_mending_mist"), "The second needs a higher hall");
            Assert.AreEqual("Lv 1", LevelPlate("ember_rain").text);

            // The row sits to the right of Auto and above the cards, on the screen.
            var button = (RectTransform)GameObject.Find("SpellButton_ember_rain").transform;
            var auto = (RectTransform)GameObject.Find("AutoPlaceButton").transform;
            var corners = new Vector3[4];
            var autoCorners = new Vector3[4];
            button.GetWorldCorners(corners);
            auto.GetWorldCorners(autoCorners);
            Assert.Greater(corners[0].x, autoCorners[2].x, "Clear of the Auto button");
            Assert.GreaterOrEqual(corners[0].x, 0f);
            Assert.LessOrEqual(corners[2].y, Screen.height);

            Controller.SelectSpell("ember_rain");
            Assert.AreEqual("ember_rain", Controller.SelectedSpell);
            // The screen is rebuilt on a pick; the old one is gone by the next frame.
            yield return null;
            Assert.IsNotNull(GameObject.Find("SpellButton_ember_rain").transform.Find("Glow"), "The picked spell glows");
            Controller.TapDeployCell(new GridCoord(25, 25));
            Assert.AreEqual(0, Controller.SpellsCast, "A spell needs an army on the field first");
            Assert.IsNotNull(Controller.LastMessage, "A refused cast says why");

            Controller.AutoPlaceHeroes();
            Assert.AreEqual(1, Controller.RaidPlacements.Count);
            yield return new WaitForSeconds(0.3f);
            if (Controller.SelectedSpell != "ember_rain") Controller.SelectSpell("ember_rain");
            var tick = Controller.LiveTick;
            Controller.TapDeployCell(new GridCoord(25, 25));
            Assert.AreEqual(1, Controller.SpellsCast, "A spell lands anywhere on the map, the enemy base too");
            Assert.IsNull(Controller.SelectedSpell, "Once cast, it is put down");
            yield return null;
            Assert.AreEqual("Used", LevelPlate("ember_rain").text);
            Assert.IsFalse(GameObject.Find("SpellButton_ember_rain").GetComponent<Button>().interactable);

            Controller.SelectSpell("ember_rain");
            Assert.IsNull(Controller.SelectedSpell, "A spent spell cannot be picked again");
            Assert.IsFalse(Controller.CastSpellAt("ember_rain", new GridCoord(20, 20)), "Once per battle");
            Assert.AreEqual(1, Controller.SpellsCast);

            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(RaidPhase.Battle, Controller.CurrentRaidPhase);
            Controller.SkipBattle();
            Assert.AreEqual(RaidPhase.Resolved, Controller.CurrentRaidPhase);
            var events = Controller.LastLiveResult.events;
            var cast = events.First(e => e[1] == (long)BattleEventKind.SpellCast);
            Assert.AreEqual(tick + 1, cast[0], "It lands on the tick after the tap");
            Assert.AreEqual(25, cast[3]);
            Assert.AreEqual(25, cast[4]);
            Assert.GreaterOrEqual(events.Count(e => e[1] == (long)BattleEventKind.SpellPulse), 1, "and its fire falls");
            yield return null;
            Assert.IsTrue(Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Any(t => t.text == "Spells cast: 1"), "The result counts it");
            Controller.FinishRaid();
            yield return null;
            Assert.IsNull(Controller.CurrentRaidPhase);
        }

        [UnityTest]
        public IEnumerator AimRing_ShowsTheReach_AndHides()
        {
            BuildRelicHall();
            AssignTeam();
            Controller.OpenAttack();
            Controller.ScoutRaid("ridge_outpost");
            yield return null;
            var stage = Object.FindAnyObjectByType<BattleStage>();
            var spell = Controller.SpellData.Find("ember_rain");
            stage.Aim(new Vector3(20.5f, 0f, 20.5f), spell.RadiusCells);
            var ring = stage.transform.Find("SpellAim");
            Assert.IsNotNull(ring, "The ring is drawn");
            Assert.IsTrue(ring.gameObject.activeSelf);
            var picture = ring.GetComponent<SpriteRenderer>();
            // The ring fills 413 of the picture's 512 pixels; it breathes by 4 percent.
            var wide = picture.sprite.bounds.size.x * ring.localScale.x * 413f / 512f;
            Assert.Greater(wide, spell.RadiusCells * 2f * 0.95f, "As wide as the spell reaches");
            Assert.Less(wide, spell.RadiusCells * 2f * 1.05f);
            stage.Aim(Vector3.zero, 0f);
            Assert.IsFalse(ring.gameObject.activeSelf, "No spell picked, no ring");
            Controller.SkipBattle();
            Controller.FinishRaid();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Stars_ArePainted_DarkUntilEarned()
        {
            AssignTeam();
            Controller.OpenAttack();
            Controller.ScoutRaid("ridge_outpost");
            yield return null;
            var target = GameObject.Find("BattleTarget").transform;
            for (var i = 1; i <= 3; i++)
            {
                var star = target.Find("Star" + i).GetComponent<Image>();
                Assert.AreEqual("icon_star", star.sprite.name, "The painted star, not a drawn diamond");
                Assert.Less(star.color.r, 0.5f, "Dark until it is earned");
            }

            Controller.AutoPlaceHeroes();
            Time.timeScale = 20f;
            var limit = Time.realtimeSinceStartup + 60f;
            while (Controller.CurrentRaidPhase == RaidPhase.Battle && Time.realtimeSinceStartup < limit) yield return null;
            Time.timeScale = 1f;
            Assert.AreEqual(RaidPhase.Resolved, Controller.CurrentRaidPhase);
            yield return null;
            var earned = Controller.LastRaidOutcome.Result.stars;
            Assert.AreEqual(3, earned, "One level 200 legend should clear this base");
            var result = Object.FindObjectsByType<Image>(FindObjectsSortMode.None).Where(i => i.name.StartsWith("ResultStar")).ToList();
            Assert.AreEqual(3, result.Count, "Three stars on the result");
            Assert.IsTrue(result.All(i => i.sprite.name == "icon_star" && i.color.r > 0.9f), "All three lit");
            Controller.FinishRaid();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Clock_TurnsRed_InTheLastHalfMinute()
        {
            AssignTeam();
            Controller.OpenAttack();
            Controller.ScoutRaid("ridge_outpost");
            yield return null;
            Text Clock() => GameObject.Find("BattleBar").GetComponentsInChildren<Text>().First(t => t.text.StartsWith("Time left"));
            yield return new WaitForSeconds(0.2f);
            var calm = Clock().color;
            Assert.Greater(calm.g, 0.8f, "Ivory while there is time");

            // Nobody is dropped, so the clock runs all the way down.
            Time.timeScale = 20f;
            var limit = Time.realtimeSinceStartup + 90f;
            while (Controller.CurrentRaidPhase == RaidPhase.Battle && Controller.LiveTick < BattleSim.TickLimit - 290 && Time.realtimeSinceStartup < limit) yield return null;
            Time.timeScale = 1f;
            Assert.AreEqual(RaidPhase.Battle, Controller.CurrentRaidPhase, "Still fighting with under half a minute left");
            yield return null;
            var late = Clock().color;
            Assert.Less(late.g, 0.6f, "Red for the last half minute");
            Assert.Greater(late.r, 0.9f);
            Controller.SkipBattle();
            Controller.FinishRaid();
            yield return null;
        }

        [UnityTest]
        public IEnumerator FallenBuilding_LeavesTheFootOfItsOwnWalls()
        {
            AssignTeam();
            Controller.OpenAttack();
            Controller.ScoutRaid("ridge_outpost");
            yield return null;
            var stage = Object.FindAnyObjectByType<BattleStage>();
            Assert.IsNull(stage.transform.Find("Rubble"), "Nothing has fallen yet");

            Controller.AutoPlaceHeroes();
            Time.timeScale = 20f;
            var limit = Time.realtimeSinceStartup + 60f;
            while (Controller.CurrentRaidPhase == RaidPhase.Battle && Time.realtimeSinceStartup < limit) yield return null;
            Time.timeScale = 1f;
            Assert.AreEqual(RaidPhase.Resolved, Controller.CurrentRaidPhase);
            yield return null;
            Assert.AreEqual(3, Controller.LastRaidOutcome.Result.stars, "One level 200 legend should clear this base");
            var ruins = stage.GetComponentsInChildren<Transform>().Where(r => r.name == "Rubble").ToList();
            Assert.Greater(ruins.Count, 0, "Every fallen building leaves a ruin");
            var walls = 0;
            foreach (var ruin in ruins)
            {
                Assert.IsNotNull(ruin.Find("Scorch"), "Burnt ground under it");
                Assert.IsNotNull(ruin.Find("Stones"), "and loose stones");
                var stump = ruin.Find("Stump");
                if (stump == null) continue;
                walls++;
                var picture = stump.GetComponent<SpriteRenderer>().sprite;
                // Cut from the building's own picture: same texture, same name as the standing sprite.
                Assert.AreEqual(picture.name, picture.texture.name, "Cut from the building's own picture");
                Assert.IsNotNull(Resources.Load<Sprite>("Buildings/" + picture.name), picture.name + " is a building picture");
                var foot = BuildingSprites.BaseWidth(picture);
                var top = picture.vertices.Max(v => v.y);
                // The front edges rise to a quarter of the base at the side corners; the break adds a fifth at most.
                Assert.Less(top, foot * 0.47f, "Only the foot of the walls is left");
                Assert.Greater(top, foot * 0.2f, "but it does stand up from the ground");
                Assert.Less(picture.vertices.Min(v => v.y), foot * 0.01f, "from the front corner");
            }
            Assert.Greater(walls, 0, "Buildings with a picture leave their walls");
            Controller.FinishRaid();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Raid_OpensOnTheEnemyBase_NotTheWholeField()
        {
            var iso = Object.FindAnyObjectByType<IsoCamera>();
            var home = iso.Camera.orthographicSize;
            AssignTeam();
            Controller.OpenAttack();
            Controller.ScoutRaid("ridge_outpost");
            yield return null;
            var buildings = Controller.CurrentRaidSession.buildings;
            var whole = VillageGrid.Size * GridSpace.CellSize * 0.70710678f / iso.Camera.aspect;
            Assert.Less(iso.Camera.orthographicSize, whole * 0.75f, "Closer than a view of the whole field");
            // The heart of the base: its walls and everything inside them. This base also has a mine and a mill
            // alone in two far corners, which the opening view leaves for the player to find.
            var walls = buildings.Where(b => b.isWall).ToList();
            Assert.Greater(walls.Count, 0, "This base is walled");
            int wallMinX = walls.Min(b => b.x), wallMaxX = walls.Max(b => b.x), wallMinZ = walls.Min(b => b.z), wallMaxZ = walls.Max(b => b.z);
            var heart = buildings.Where(b => b.x >= wallMinX && b.x <= wallMaxX && b.z >= wallMinZ && b.z <= wallMaxZ).ToList();
            Assert.IsTrue(heart.Any(b => b.definitionId == "dojo_hq"), "The Town Hall is inside the walls");
            Assert.Less(heart.Count, buildings.Count, "and something stands outside them");
            float left = 1f, right = 0f, low = 1f, high = 0f;
            foreach (var b in heart)
            for (var corner = 0; corner < 4; corner++)
            {
                var world = new Vector3(b.x + (corner % 2) * b.width, 0f, b.z + (corner / 2) * b.depth) * GridSpace.CellSize;
                var seen = iso.Camera.WorldToViewportPoint(world);
                left = Mathf.Min(left, seen.x);
                right = Mathf.Max(right, seen.x);
                low = Mathf.Min(low, seen.y);
                high = Mathf.Max(high, seen.y);
            }
            Assert.Greater(left, 0.04f, "The whole base is on screen, with ground to drop on at its left");
            Assert.Less(right, 0.96f, "and at its right");
            Assert.Greater(low, 0.04f, "and in front");
            Assert.Less(high, 0.96f, "and behind");
            Assert.AreEqual(0.5f, (left + right) * 0.5f, 0.03f, "The base is in the middle of the screen");
            var middle = (low + high) * 0.5f;
            Assert.IsTrue(middle > 0.42f && middle < 0.495f, "a little below it, between the clock and the cards: " + middle);
            Assert.Greater(Mathf.Max(right - left, high - low), 0.5f, "and fills more than half of it");
            // Open ground beyond the walls is in view, where an army can land.
            var drop = new GridCoord(wallMinX - BattleSim.DeployClearance - 1, wallMinZ - BattleSim.DeployClearance - 1);
            Assert.IsTrue(BattleSim.CanDeployAt(buildings, drop.X, drop.Z), "A cell an army may land on");
            var landing = iso.Camera.WorldToViewportPoint(GridSpace.CellCenter(drop));
            Assert.IsTrue(landing.x > 0.02f && landing.x < 0.98f && landing.y > 0.02f && landing.y < 0.98f, "is on screen: " + landing);

            // The fight does not pull the view off the base: the player is aiming at it.
            var hall = buildings.First(x => x.definitionId == "dojo_hq");
            var hallAt = new Vector3(hall.x + hall.width * 0.5f, 0f, hall.z + hall.depth * 0.5f) * GridSpace.CellSize;
            var before = iso.Camera.WorldToViewportPoint(hallAt);
            Controller.AutoPlaceHeroes();
            Assert.Greater(Controller.RaidPlacements.Count, 0, Controller.LastMessage);
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(RaidPhase.Battle, Controller.CurrentRaidPhase);
            var after = iso.Camera.WorldToViewportPoint(hallAt);
            Assert.Less(Vector2.Distance(before, after), 0.04f, "The Town Hall stays where it was on screen");

            Controller.FinishRaid();
            yield return null;
            Assert.AreEqual(home, iso.Camera.orthographicSize, 0.01f, "Home again, at the home view");
        }

        [UnityTest]
        public IEnumerator ZoomPunch_LeansIn_AndComesBackExactly()
        {
            var iso = Object.FindAnyObjectByType<IsoCamera>();
            // Let the first heavy frames pass, so the lean-in is seen frame by frame.
            yield return new WaitForSecondsRealtime(0.5f);
            var rest = iso.Camera.orthographicSize;
            iso.ZoomPunch(0.07f);
            var closest = rest;
            var until = Time.realtimeSinceStartup + 0.45f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                closest = Mathf.Min(closest, iso.Camera.orthographicSize);
            }
            Assert.Less(closest, rest * 0.97f, "The view leans in");
            Assert.Greater(closest, rest * 0.92f, "by no more than it was asked to");
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(rest, iso.Camera.orthographicSize, 0.0005f, "and returns to exactly where it was");
        }
    }
}

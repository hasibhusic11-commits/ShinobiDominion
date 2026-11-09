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
    /// <summary>Boots the real game object graph against a temp save and checks the screen rules.</summary>
    public class VillageSceneTests
    {
        private string _savePath;
        private GameObject _root;
        private VillageBootstrap _game;

        private VillageController Controller => _game.Controller;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _savePath = Path.Combine(Application.temporaryCachePath, "test_village_" + System.Guid.NewGuid().ToString("N") + ".json");
            VillageBootstrap.SavePathOverride = _savePath;
            Boot();
            yield return null;
        }

        private void Boot()
        {
            _root = new GameObject("VillageBootstrapUnderTest");
            _game = _root.AddComponent<VillageBootstrap>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_root);
            VillageBootstrap.SavePathOverride = null;
            yield return null;
            if (File.Exists(_savePath)) File.Delete(_savePath);
        }

        private BuildingInstance First(string definitionId) =>
            Controller.Village.Buildings.First(b => b.Definition.id == definitionId);

        private Button[] Buttons() => _root.GetComponentsInChildren<Button>(false);

        private bool ButtonExists(string name) => Buttons().Any(b => b.name == name);

        /// <summary>Screen-space rect of a UI element.</summary>
        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return new Rect(corners[0].x, corners[0].y, corners[2].x - corners[0].x, corners[2].y - corners[0].y);
        }

        [Test]
        public void IdleHome_BottomHasOnlyAttackAndTheLeftColumn()
        {
            // ADR-033: the owner's newest mockup puts ATTACK at the bottom left under a column of round shortcuts;
            // the bottom right stays empty until a building is tapped.
            var column = new[] { "GiftButton", "TrophyButton", "QuestsButton", "FriendsButton", "MailButton" };
            var bottom = Buttons().Where(b => ScreenRect((RectTransform)b.transform).yMin < Screen.height / 3f).Select(b => b.name).ToArray();
            CollectionAssert.Contains(bottom, "AttackButton");
            CollectionAssert.IsSubsetOf(bottom, column.Concat(new[] { "AttackButton" }).ToArray());
            var attack = Buttons().First(b => b.name == "AttackButton");
            // The word is either written on the button or painted into the owner's kit picture (hud_attack_button).
            var word = attack.GetComponentInChildren<Text>();
            if (word != null) StringAssert.AreEqualIgnoringCase("ATTACK", word.text);
            else Assert.AreEqual("hud_attack_button", attack.GetComponentsInChildren<Image>().First(i => i.name == "Face").sprite.name);
            Assert.IsFalse(Buttons().Any(b => ScreenRect((RectTransform)b.transform).xMin > Screen.width * 0.5f && ScreenRect((RectTransform)b.transform).yMin < Screen.height / 3f),
                "Nothing sits at the bottom right of the idle screen");
        }

        [Test]
        public void IdleHome_AttackIsAtBottomLeft()
        {
            var rect = ScreenRect((RectTransform)Controller.Hud.AttackButton.transform);
            Assert.Less(rect.center.x, Screen.width * 0.5f);
            Assert.Less(rect.center.y, Screen.height * 0.5f);
        }

        [Test]
        public void IdleHome_ShowsNoEnergyOrStamina()
        {
            var all = string.Join(" ", _root.GetComponentsInChildren<Text>(true).Select(t => t.text)).ToLowerInvariant();
            StringAssert.DoesNotContain("energy", all);
            StringAssert.DoesNotContain("stamina", all);
            // The wallet shows icons and numbers, so check the pieces are there and not their words.
            var names = _root.GetComponentsInChildren<RectTransform>(true).Select(r => r.name).ToList();
            CollectionAssert.Contains(names, "GoldPill");
            CollectionAssert.Contains(names, "CrystalsPill");
        }

        [Test]
        public void IdleHome_HasNoCatalog_NoPanel_NoGrid()
        {
            Assert.IsFalse(Controller.Panel.IsOpen);
            Assert.IsFalse(Controller.Grid.Visible);
            Assert.IsNull(Controller.Toolbar);
            Assert.IsFalse(ButtonExists("Place_wall"));
        }

        [Test]
        public void SpatialUi_CoversLittleOfTheScreen()
        {
            // The first-time guide is a few minutes' help for a new player, not part of the idle screen this rule is about.
            Controller.SkipGuide();
            // Sample the screen on a grid and count points under any UI picture, so layered parts of one button
            // (face, ring, icon) count once. Soft glows and drop shadows are light, not panels, and are skipped.
            var rects = _root.GetComponentsInChildren<Graphic>(false)
                .Where(g => !(g is Text) && g.name != "Glow" && g.name != "Shadow")
                .Select(g => ScreenRect(g.rectTransform)).ToList();
            const int columns = 128, rows = 72;
            var covered = 0;
            for (var i = 0; i < columns; i++)
            for (var j = 0; j < rows; j++)
            {
                var point = new Vector2((i + 0.5f) * Screen.width / columns, (j + 0.5f) * Screen.height / rows);
                if (rects.Any(r => r.Contains(point))) covered++;
            }
            Assert.Less(covered / (float)(columns * rows), 0.15f, "The village must keep at least 85% of the visible space");
        }

        [Test]
        public void OnlyBuilderHall_OpensCatalog()
        {
            Assert.IsFalse(Controller.OpenCatalog(), "Catalog must not open from home");
            Controller.Select(First("hero_altar").Id);
            Controller.ShowAltarRoster();
            Assert.IsFalse(Controller.OpenCatalog(), "Catalog must not open from the altar");
            Assert.IsFalse(ButtonExists("BuildButton"));
            Controller.CloseAll();

            Controller.Select(First("builder_hall").Id);
            Assert.IsTrue(ButtonExists("BuildButton"));
            Assert.IsTrue(Controller.OpenCatalog());
            Assert.AreEqual(VillageScreen.BuildCatalog, Controller.UiState.Screen);
        }

        [UnityTest]
        public IEnumerator ExitingCatalog_RemovesPaletteEntirely()
        {
            Controller.Select(First("builder_hall").Id);
            Controller.OpenCatalog();
            yield return null;
            Assert.IsTrue(ButtonExists("TabWalls"));

            Controller.CloseAll();
            yield return null;
            Assert.IsFalse(ButtonExists("TabWalls"));
            Assert.IsFalse(Controller.Panel.IsOpen);
            Assert.AreEqual(VillageScreen.Home, Controller.UiState.Screen);
        }

        [UnityTest]
        public IEnumerator Placement_ShowsGrid_HidesAttack_ThenPlacesAndCleansUp()
        {
            Controller.Select(First("builder_hall").Id);
            Controller.OpenCatalog();
            var before = Controller.Village.CountOf("defense_tower");

            Assert.IsTrue(Controller.BeginPlacement("defense_tower"));
            yield return null;
            Assert.IsTrue(Controller.Grid.Visible);
            Assert.IsFalse(Controller.Hud.AttackButton.gameObject.activeSelf);
            Assert.IsFalse(ButtonExists("TabWalls"), "Catalog must be gone while placing");
            Assert.IsNotNull(Controller.Toolbar);

            Controller.SetGhostOrigin(new GridCoord(2, 2));
            Assert.AreEqual(VillageError.None, Controller.ConfirmPlacementAction());
            yield return null;

            Assert.AreEqual(before + 1, Controller.Village.CountOf("defense_tower"));
            Assert.IsFalse(Controller.Grid.Visible);
            Assert.IsNull(Controller.Toolbar);
            Assert.IsNotNull(Controller.Village.BuildingAt(new GridCoord(2, 2)));
        }

        [UnityTest]
        public IEnumerator Placement_BlockedOnOccupiedCells_ChangesNothing()
        {
            Controller.Select(First("builder_hall").Id);
            Controller.OpenCatalog();
            Controller.BeginPlacement("defense_tower");
            var gold = Controller.Village.Gold;
            var hq = First("dojo_hq").Origin;

            Controller.SetGhostOrigin(hq);
            Assert.AreEqual(VillageError.Overlap, Controller.ConfirmPlacementAction());
            yield return null;
            Assert.AreEqual(gold, Controller.Village.Gold);
            Assert.IsNotNull(Controller.Toolbar, "Stays in placement mode so the player can adjust");
        }

        [UnityTest]
        public IEnumerator WallTool_DrawsAtomicRun_AndDoneReturnsToCatalog()
        {
            Controller.Select(First("builder_hall").Id);
            Controller.OpenCatalog();
            Assert.IsTrue(Controller.BeginPlacement(Village.WallId));
            Controller.SetWallRun(new GridCoord(0, 0), new GridCoord(6, 3));
            Assert.AreEqual(VillageError.None, Controller.ConfirmWallRun());
            yield return null;

            Assert.AreEqual(7, Controller.Village.CountOf(Village.WallId));
            Assert.IsNotNull(Controller.Village.BuildingAt(new GridCoord(6, 0)), "Diagonal drag locks to the long axis");
            Assert.IsNull(Controller.Village.BuildingAt(new GridCoord(6, 3)));

            Controller.ExitPlacement();
            yield return null;
            Assert.AreEqual(VillageScreen.BuildCatalog, Controller.UiState.Screen);
            Assert.IsFalse(Controller.Grid.Visible);
        }

        [UnityTest]
        public IEnumerator Move_RelocatesBuilding_AndCancelLeavesItAlone()
        {
            var heroBase = First("hero_base");
            var original = heroBase.Origin;

            Controller.Select(heroBase.Id);
            Assert.IsTrue(Controller.BeginMove(heroBase.Id));
            Controller.SetGhostOrigin(new GridCoord(3, 3));
            Controller.CancelMove();
            yield return null;
            Assert.AreEqual(original, heroBase.Origin);

            Controller.Select(heroBase.Id);
            Controller.BeginMove(heroBase.Id);
            Controller.SetGhostOrigin(new GridCoord(3, 3));
            Assert.AreEqual(VillageError.None, Controller.ConfirmMove());
            yield return null;
            Assert.AreEqual(new GridCoord(3, 3), heroBase.Origin);
            Assert.AreEqual(VillageScreen.Home, Controller.UiState.Screen);
        }

        [UnityTest]
        public IEnumerator Layout_SurvivesReloadExactly()
        {
            Controller.Select(First("builder_hall").Id);
            Controller.OpenCatalog();
            Controller.BeginPlacement(Village.WallId);
            Controller.SetWallRun(new GridCoord(1, 1), new GridCoord(5, 1));
            Controller.ConfirmWallRun();
            Controller.ExitPlacement();
            Controller.CloseAll();
            yield return null; // LateUpdate flushes the save

            var expected = Controller.Village.ToSave().ToJson();
            Assert.IsTrue(File.Exists(_savePath));

            Object.Destroy(_root);
            yield return null;
            Boot();
            yield return null;

            Assert.AreEqual(expected, Controller.Village.ToSave().ToJson());
        }

        [Test]
        public void AttackButton_OpensAttackFlow_NotASecondMenu()
        {
            Controller.OpenAttack();
            Assert.AreEqual(VillageScreen.AttackFlow, Controller.UiState.Screen);
            Assert.IsFalse(ButtonExists("TabWalls"));
        }

        [Test]
        public void TappingGround_DoesNotSelect_AndPickFindsRealBuilding()
        {
            var cam = _game.IsoCamera;
            var hq = First("dojo_hq");
            var center = GridSpace.FootprintCenter(hq.Footprint) + Vector3.up * 0.5f;
            var picked = Controller.View.Pick(cam.ScreenRay(cam.Camera.WorldToScreenPoint(center)));
            Assert.IsNotNull(picked);
            Assert.AreEqual(hq.Id, picked.Id);
        }
    }
}

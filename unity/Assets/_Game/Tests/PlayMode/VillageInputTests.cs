using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShinobiDominion.Core;
using ShinobiDominion.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace ShinobiDominion.Tests
{
    /// <summary>Drives the game through simulated devices, so the whole path from device to gesture to screen runs.</summary>
    public class VillageInputTests
    {
        private string _savePath;
        private GameObject _root;
        private VillageBootstrap _game;
        private Mouse _mouse;
        private Touchscreen _touch;
        private InputSettings.BackgroundBehavior _background;
#if UNITY_EDITOR
        private InputSettings.EditorInputBehaviorInPlayMode _editorBehavior;
#endif

        private VillageController Controller => _game.Controller;
        private Camera Cam => _game.IsoCamera.Camera;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            // A batch-mode editor has no focused Game view, and would otherwise discard all device input.
            _editorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            _mouse = InputSystem.AddDevice<Mouse>();
            _mouse.MakeCurrent();

            _savePath = Path.Combine(Application.temporaryCachePath, "input_village_" + System.Guid.NewGuid().ToString("N") + ".json");
            VillageBootstrap.SavePathOverride = _savePath;
            _root = new GameObject("VillageBootstrapUnderTest");
            _game = _root.AddComponent<VillageBootstrap>();
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_root);
            VillageBootstrap.SavePathOverride = null;
            InputSystem.RemoveDevice(_mouse);
            if (_touch != null) InputSystem.RemoveDevice(_touch);
            _touch = null;
            InputSystem.settings.backgroundBehavior = _background;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorBehavior;
#endif
            yield return null;
            if (File.Exists(_savePath)) File.Delete(_savePath);
        }

        private IEnumerator MouseState(Vector2 position, bool down)
        {
            var state = new MouseState { position = position };
            if (down) state = state.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(_mouse, state);
            InputSystem.Update();
            yield return null;
        }

        private IEnumerator Click(Vector2 position)
        {
            yield return MouseState(position, false);
            yield return MouseState(position, true);
            yield return MouseState(position, false);
            yield return null;
        }

        private IEnumerator Drag(Vector2 from, Vector2 to)
        {
            yield return MouseState(from, false);
            yield return MouseState(from, true);
            for (var i = 1; i <= 6; i++)
                yield return MouseState(Vector2.Lerp(from, to, i / 6f), true);
            yield return MouseState(to, false);
            yield return null;
        }

        private Vector2 ScreenOf(BuildingInstance building)
        {
            var world = GridSpace.FootprintCenter(building.Footprint) + Vector3.up * 0.3f;
            return Cam.WorldToScreenPoint(world);
        }

        private Vector2 ScreenOf(int x, int z) => Cam.WorldToScreenPoint(GridSpace.CellCenter(new GridCoord(x, z)));

        private static Vector2 ScreenOf(Component ui)
        {
            var corners = new Vector3[4];
            ((RectTransform)ui.transform).GetWorldCorners(corners);
            return (corners[0] + corners[2]) * 0.5f;
        }

        private BuildingInstance First(string id) => Controller.Village.Buildings.First(b => b.Definition.id == id);

        private Button Find(string name) => _root.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.name == name);

        [UnityTest]
        public IEnumerator Tap_OnBuilding_OpensItsPanel()
        {
            var altar = First("hero_altar");
            yield return Click(ScreenOf(altar));
            Assert.AreEqual(VillageScreen.BuildingPanel, Controller.UiState.Screen);
            Assert.AreEqual(altar.Id, Controller.UiState.SelectedBuildingId);
            Assert.IsTrue(Controller.Panel.IsOpen);
        }

        [UnityTest]
        public IEnumerator Tap_OnEmptyGround_ClosesPanel()
        {
            yield return Click(ScreenOf(First("hero_altar")));
            Assert.IsTrue(Controller.Panel.IsOpen);

            yield return Click(ScreenOf(41, 18));
            Assert.AreEqual(VillageScreen.Home, Controller.UiState.Screen);
            Assert.IsFalse(Controller.Panel.IsOpen);
        }

        [UnityTest]
        public IEnumerator Drag_PansCamera_WithoutSelectingOrMovingAnything()
        {
            var altar = First("hero_altar");
            var origin = altar.Origin;
            var cameraBefore = Cam.transform.position;

            var start = ScreenOf(altar);
            yield return Drag(start, start + new Vector2(-90f, -40f));

            Assert.AreNotEqual(cameraBefore, Cam.transform.position, "Camera should have panned");
            Assert.AreEqual(VillageScreen.Home, Controller.UiState.Screen, "A drag must not count as a tap");
            Assert.AreEqual(origin, altar.Origin, "A drag must never relocate a building");
        }

        [UnityTest]
        public IEnumerator Click_OnAttackButton_ReachesUi_NotWorld()
        {
            yield return Click(ScreenOf(Controller.Hud.AttackButton));
            Assert.AreEqual(VillageScreen.AttackFlow, Controller.UiState.Screen);
        }

        [UnityTest]
        public IEnumerator FullBuildFlow_WithRealClicks_PlacesTower()
        {
            var towers = Controller.Village.CountOf("defense_tower");
            yield return Click(ScreenOf(First("builder_hall")));
            yield return Click(ScreenOf(Find("BuildButton")));
            Assert.AreEqual(VillageScreen.BuildCatalog, Controller.UiState.Screen);

            yield return Click(ScreenOf(Find("TabDefense")));
            yield return Click(ScreenOf(Find("Place_defense_tower")));
            Assert.AreEqual(VillageScreen.PlacingBuilding, Controller.UiState.Screen);
            Assert.IsTrue(Controller.Grid.Visible);

            yield return Click(ScreenOf(40, 22));
            yield return Click(ScreenOf(Find("ConfirmButton")));

            Assert.AreEqual(towers + 1, Controller.Village.CountOf("defense_tower"));
            Assert.AreEqual(VillageScreen.BuildCatalog, Controller.UiState.Screen);
            Assert.IsFalse(Controller.Grid.Visible);
        }

        [UnityTest]
        public IEnumerator WallTool_DragPaintsRun_ConfirmPlacesIt()
        {
            yield return Click(ScreenOf(First("builder_hall")));
            yield return Click(ScreenOf(Find("BuildButton")));
            yield return Click(ScreenOf(Find("TabWalls")));
            yield return Click(ScreenOf(Find("Place_wall")));
            Assert.AreEqual(VillageScreen.PaintingWalls, Controller.UiState.Screen);

            var cameraBefore = Cam.transform.position;
            yield return Drag(ScreenOf(32, 14), ScreenOf(38, 14));
            Assert.AreEqual(cameraBefore, Cam.transform.position, "Painting must not pan the camera");

            var confirm = Find("ConfirmButton");
            Assert.IsTrue(confirm.interactable);
            yield return Click(ScreenOf(confirm));
            Assert.AreEqual(7, Controller.Village.CountOf(Village.WallId));
            Assert.IsNotNull(Controller.Village.BuildingAt(new GridCoord(32, 14)));
            Assert.IsNotNull(Controller.Village.BuildingAt(new GridCoord(38, 14)));
        }

        [UnityTest]
        public IEnumerator Pinch_ZoomsCamera_AndNeverTaps()
        {
            _touch = InputSystem.AddDevice<Touchscreen>();
            var sizeBefore = Cam.orthographicSize;
            var center = ScreenOf(First("dojo_hq"));

            yield return Fingers(center, 40f, TouchPhase.Began);
            yield return Fingers(center, 60f, TouchPhase.Moved);
            yield return Fingers(center, 110f, TouchPhase.Moved);
            yield return Fingers(center, 160f, TouchPhase.Moved);
            yield return Fingers(center, 160f, TouchPhase.Ended);
            yield return null;

            Assert.Less(Cam.orthographicSize, sizeBefore, "Spreading two fingers should zoom in");
            Assert.AreEqual(VillageScreen.Home, Controller.UiState.Screen, "A pinch must not select the building under it");
        }

        private IEnumerator Fingers(Vector2 center, float halfSpread, TouchPhase phase)
        {
            InputSystem.QueueStateEvent(_touch, new TouchState { touchId = 1, phase = phase, position = center + new Vector2(halfSpread, 0f) });
            InputSystem.QueueStateEvent(_touch, new TouchState { touchId = 2, phase = phase, position = center - new Vector2(halfSpread, 0f) });
            InputSystem.Update();
            yield return null;
        }
    }
}

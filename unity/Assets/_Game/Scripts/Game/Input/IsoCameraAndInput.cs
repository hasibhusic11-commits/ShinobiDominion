using System.Collections.Generic;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ShinobiDominion.Game
{
    /// <summary>Constrained orthographic isometric camera with pan and zoom.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class IsoCamera : MonoBehaviour
    {
        private const float Distance = 120f;
        private const float MinSize = 5f;
        private const float MaxSize = 26f;
        private const float PanMargin = 4f;

        private static readonly Plane Ground = new Plane(Vector3.up, Vector3.zero);
        private Vector3 _focus;

        public Camera Camera { get; private set; }

        public void Initialize(Vector3 focus, float size)
        {
            Camera = GetComponent<Camera>();
            Camera.orthographic = true;
            Camera.orthographicSize = Mathf.Clamp(size, MinSize, MaxSize);
            Camera.nearClipPlane = 0.1f;
            Camera.farClipPlane = 400f;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = Palette.Sky;
            transform.rotation = Quaternion.Euler(35.264f, 45f, 0f);
            _focus = focus;
            Apply();
        }

        public Vector3 GroundPoint(Vector2 screen)
        {
            var ray = Camera.ScreenPointToRay(screen);
            return Ground.Raycast(ray, out var distance) ? ray.GetPoint(distance) : _focus;
        }

        public Ray ScreenRay(Vector2 screen) => Camera.ScreenPointToRay(screen);

        /// <summary>Keeps the grabbed ground point under the pointer.</summary>
        public void PanTo(Vector3 grabbedWorld, Vector2 screen)
        {
            _focus += grabbedWorld - GroundPoint(screen);
            Apply();
        }

        /// <summary>Factor below 1 zooms in. The ground under the screen point stays put.</summary>
        public void ZoomAt(float factor, Vector2 screen)
        {
            var before = GroundPoint(screen);
            Camera.orthographicSize = Mathf.Clamp(Camera.orthographicSize * factor, MinSize, MaxSize);
            Apply();
            _focus += before - GroundPoint(screen);
            Apply();
        }

        private void Apply()
        {
            float size = VillageGrid.Size * GridSpace.CellSize;
            _focus.x = Mathf.Clamp(_focus.x, -PanMargin, size + PanMargin);
            _focus.z = Mathf.Clamp(_focus.z, -PanMargin, size + PanMargin);
            _focus.y = 0f;
            transform.position = _focus - transform.forward * Distance;
        }
    }

    /// <summary>What the world does with pointer gestures. Implemented by VillageController.</summary>
    public interface IWorldGestureHandler
    {
        /// <summary>Return true to capture this press, for dragging a ghost or painting walls, instead of panning.</summary>
        bool OnWorldPress(Vector2 screen);
        void OnWorldDrag(Vector2 screen);
        void OnWorldRelease(Vector2 screen);
        void OnWorldTap(Vector2 screen);
        void OnBackPressed();
    }

    /// <summary>
    /// Turns raw touch and mouse into tap, drag, pan and pinch. Presses that begin on UI never
    /// reach the world. A press only becomes a tap if it stays within the drag threshold, so a
    /// pan can never select or relocate anything by accident.
    /// </summary>
    public sealed class WorldInput : MonoBehaviour
    {
        private enum Gesture
        {
            None,
            PanCandidate,
            Captured
        }

        private IsoCamera _camera;
        private IWorldGestureHandler _handler;
        private readonly List<RaycastResult> _hits = new List<RaycastResult>();

        private Gesture _gesture;
        private bool _wasPressed;
        private bool _moved;
        private Vector2 _pressPosition;
        private Vector3 _grabbedWorld;
        private bool _pinching;
        private bool _waitForAllUp;
        private float _pinchDistance;
        private Vector3 _pinchGrab;
        private bool _secondaryPanning;
        private Vector3 _secondaryGrab;

        public void Initialize(IsoCamera isoCamera, IWorldGestureHandler handler)
        {
            _camera = isoCamera;
            _handler = handler;
        }

        private float DragThreshold => Mathf.Max(10f, (Screen.dpi > 0f ? Screen.dpi : 96f) * 0.08f);

        private void Update()
        {
            if (_camera == null || _handler == null) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) _handler.OnBackPressed();

            var touch1 = default(Vector2);
            var touch2 = default(Vector2);
            var touchCount = ReadTouches(ref touch1, ref touch2);

            if (touchCount >= 2)
            {
                HandlePinch(touch1, touch2);
                return;
            }
            _pinching = false;

            var mouse = Mouse.current;
            bool pressed;
            Vector2 position;
            if (touchCount == 1)
            {
                pressed = true;
                position = touch1;
            }
            else if (mouse != null)
            {
                pressed = mouse.leftButton.isPressed;
                position = mouse.position.ReadValue();
                HandleMouseExtras(mouse, position);
            }
            else
            {
                pressed = false;
                position = _pressPosition;
            }

            if (_waitForAllUp)
            {
                if (!pressed) _waitForAllUp = false;
                _wasPressed = false;
                return;
            }

            if (pressed && !_wasPressed) Begin(position);
            else if (pressed) Continue(position);
            else if (_wasPressed) End(position);
            _wasPressed = pressed;
        }

        private static int ReadTouches(ref Vector2 first, ref Vector2 second)
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen == null) return 0;
            var count = 0;
            foreach (var touch in touchscreen.touches)
            {
                if (!touch.press.isPressed) continue;
                if (count == 0) first = touch.position.ReadValue();
                else if (count == 1) second = touch.position.ReadValue();
                count++;
            }
            return count;
        }

        private void HandlePinch(Vector2 a, Vector2 b)
        {
            // A second finger cancels any one-finger gesture without committing it.
            _gesture = Gesture.None;
            _wasPressed = false;
            _waitForAllUp = true;

            var distance = Vector2.Distance(a, b);
            var middle = (a + b) * 0.5f;
            if (!_pinching)
            {
                _pinching = true;
                _pinchDistance = distance;
                _pinchGrab = _camera.GroundPoint(middle);
                return;
            }

            if (distance > 1f && _pinchDistance > 1f)
                _camera.ZoomAt(_pinchDistance / distance, middle);
            _pinchDistance = distance;
            _camera.PanTo(_pinchGrab, middle);
        }

        private void HandleMouseExtras(Mouse mouse, Vector2 position)
        {
            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !IsOverUi(position))
                _camera.ZoomAt(scroll > 0f ? 0.9f : 1.1f, position);

            // Right or middle drag always pans, even during the wall tool.
            var panHeld = mouse.rightButton.isPressed || mouse.middleButton.isPressed;
            if (panHeld && !_secondaryPanning && !IsOverUi(position))
            {
                _secondaryPanning = true;
                _secondaryGrab = _camera.GroundPoint(position);
            }
            else if (panHeld && _secondaryPanning)
            {
                _camera.PanTo(_secondaryGrab, position);
            }
            else if (!panHeld)
            {
                _secondaryPanning = false;
            }
        }

        private void Begin(Vector2 position)
        {
            _pressPosition = position;
            _moved = false;
            if (IsOverUi(position))
            {
                _gesture = Gesture.None;
                return;
            }

            if (_handler.OnWorldPress(position))
            {
                _gesture = Gesture.Captured;
            }
            else
            {
                _gesture = Gesture.PanCandidate;
                _grabbedWorld = _camera.GroundPoint(position);
            }
        }

        private void Continue(Vector2 position)
        {
            if (_gesture == Gesture.None) return;
            if (!_moved && Vector2.Distance(position, _pressPosition) > DragThreshold) _moved = true;

            if (_gesture == Gesture.Captured) _handler.OnWorldDrag(position);
            else if (_moved) _camera.PanTo(_grabbedWorld, position);
        }

        private void End(Vector2 position)
        {
            if (_gesture == Gesture.Captured) _handler.OnWorldRelease(position);
            else if (_gesture == Gesture.PanCandidate && !_moved) _handler.OnWorldTap(position);
            _gesture = Gesture.None;
        }

        private bool IsOverUi(Vector2 position)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;
            var data = new PointerEventData(eventSystem) { position = position };
            _hits.Clear();
            eventSystem.RaycastAll(data, _hits);
            return _hits.Count > 0;
        }
    }
}

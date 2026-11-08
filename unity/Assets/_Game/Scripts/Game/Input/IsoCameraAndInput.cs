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
        /// <summary>
        /// 30 degrees down: the classic 2:1 angle the owner's building art is painted for (ADR-034). It was 35.264
        /// (true isometric), which made the painted bases sit wrongly on the grid.
        /// </summary>
        public const float Pitch = 30f;

        private const float Distance = 70f;
        private const float MinSize = 3.5f;
        private const float MaxSize = 30f;
        private const float PanMargin = 4f;

        private static readonly Plane Ground = new Plane(Vector3.up, Vector3.zero);
        private Vector3 _focus;
        private Vector3 _velocity;
        private float _targetSize;
        private Vector2 _zoomAnchor;
        private bool _zoomingSmoothly;
        private Vector3 _focusTarget;
        private float _focusTime = -1f;
        private Vector3 _focusStart;

        public Camera Camera { get; private set; }

        public void Initialize(Vector3 focus, float size)
        {
            Camera = GetComponent<Camera>();
            Camera.orthographic = true;
            Camera.orthographicSize = Mathf.Clamp(size, MinSize, MaxSize);
            _targetSize = Camera.orthographicSize;
            Camera.nearClipPlane = 0.1f;
            Camera.farClipPlane = 400f;
            Camera.allowMSAA = GameQuality.Current == VisualQuality.High;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = Palette.Sky.linear;
            transform.rotation = Quaternion.Euler(IsoCamera.Pitch, 45f, 0f);
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
            _focusTime = -1f;
            _velocity = Vector3.zero;
            _focus += grabbedWorld - GroundPoint(screen);
            Apply();
        }

        /// <summary>Factor below 1 zooms in. The ground under the screen point stays put.</summary>
        public void ZoomAt(float factor, Vector2 screen)
        {
            _zoomingSmoothly = false;
            var before = GroundPoint(screen);
            Camera.orthographicSize = Mathf.Clamp(Camera.orthographicSize * factor, MinSize, MaxSize);
            Apply();
            _focus += before - GroundPoint(screen);
            _targetSize = Camera.orthographicSize;
            Apply();
        }

        /// <summary>Mouse-wheel zoom eases toward its target rather than jumping.</summary>
        public void ZoomSmooth(float factor, Vector2 screen)
        {
            _targetSize = Mathf.Clamp(_targetSize * factor, MinSize, MaxSize);
            _zoomAnchor = screen;
            _zoomingSmoothly = true;
        }

        /// <summary>A released drag keeps gliding briefly, which is what makes panning feel natural.</summary>
        public void Fling(Vector3 worldVelocity)
        {
            _velocity = Vector3.ClampMagnitude(worldVelocity, 45f);
        }

        public void StopMotion()
        {
            _velocity = Vector3.zero;
            _focusTime = -1f;
        }

        /// <summary>Eases the view so a selected building stays visible beside the side panel.</summary>
        public void EnsureVisible(Vector3 world, float minScreenX, float maxScreenX, float minScreenY, float maxScreenY)
        {
            var viewport = Camera.WorldToViewportPoint(world);
            var targetX = Mathf.Clamp(viewport.x, minScreenX, maxScreenX);
            var targetY = Mathf.Clamp(viewport.y, minScreenY, maxScreenY);
            if (Mathf.Approximately(targetX, viewport.x) && Mathf.Approximately(targetY, viewport.y)) return;
            var desired = Camera.ViewportToScreenPoint(new Vector3(targetX, targetY, 0f));
            _focusTarget = _focus + GroundPoint(Camera.WorldToScreenPoint(world)) - GroundPoint(desired);
            _focusStart = _focus;
            _focusTime = 0f;
            _velocity = Vector3.zero;
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            if (_zoomingSmoothly)
            {
                var before = GroundPoint(_zoomAnchor);
                Camera.orthographicSize = Mathf.Lerp(Camera.orthographicSize, _targetSize, 1f - Mathf.Exp(-12f * dt));
                Apply();
                _focus += before - GroundPoint(_zoomAnchor);
                if (Mathf.Abs(Camera.orthographicSize - _targetSize) < 0.01f) _zoomingSmoothly = false;
                Apply();
            }

            if (_focusTime >= 0f)
            {
                _focusTime += dt / 0.35f;
                var t = Mathf.Clamp01(_focusTime);
                _focus = Vector3.Lerp(_focusStart, _focusTarget, t * t * (3f - 2f * t));
                if (t >= 1f) _focusTime = -1f;
                Apply();
            }

            if (_velocity.sqrMagnitude > 0.01f)
            {
                _focus += _velocity * dt;
                _velocity *= Mathf.Exp(-5f * dt);
                Apply();
            }
        }

        private void Apply()
        {
            float size = VillageGrid.Size * GridSpace.CellSize;
            _focus.x = Mathf.Clamp(_focus.x, -PanMargin, size + PanMargin);
            _focus.z = Mathf.Clamp(_focus.z, -PanMargin, size + PanMargin);
            _focus.y = 0f;
            transform.position = _focus - transform.forward * Distance + _shakeOffset;
        }

        // Screen shake (game-feel skill): impacts add "trauma", which decays smoothly; the shake is trauma squared, so
        // small hits barely move the view and big ones punch. It moves only the camera, never the world.
        private float _trauma;
        private float _shakeTime;
        private Vector3 _shakeOffset;

        /// <summary>Adds shake, 0 to 1. Hits add up; they never reset it.</summary>
        public void AddTrauma(float amount) => _trauma = Mathf.Clamp01(_trauma + amount);

        private void LateUpdate()
        {
            if (_trauma <= 0f && _shakeOffset == Vector3.zero) return;
            var dt = Time.unscaledDeltaTime;
            _trauma = Mathf.Max(_trauma - 1.6f * dt, 0f);
            _shakeTime += dt * 32f;
            var shake = _trauma * _trauma;
            var reach = Camera.orthographicSize * 0.035f;
            _shakeOffset = (transform.right * Mathf.Sin(_shakeTime * 1.7f) + transform.up * Mathf.Sin(_shakeTime * 2.3f) * 0.7f) * (reach * shake);
            Apply();
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
        private Vector3 _lastGround;
        private Vector3 _dragVelocity;
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
                _camera.ZoomSmooth(scroll > 0f ? 0.88f : 1.14f, position);

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
                _camera.StopMotion();
                _gesture = Gesture.Captured;
            }
            else
            {
                _gesture = Gesture.PanCandidate;
                _camera.StopMotion();
                _grabbedWorld = _camera.GroundPoint(position);
                _dragVelocity = Vector3.zero;
                _lastGround = _camera.transform.position;
            }
        }

        private void Continue(Vector2 position)
        {
            if (_gesture == Gesture.None) return;
            if (!_moved && Vector2.Distance(position, _pressPosition) > DragThreshold) _moved = true;

            if (_gesture == Gesture.Captured) _handler.OnWorldDrag(position);
            else if (_moved)
            {
                _camera.PanTo(_grabbedWorld, position);
                var dt = Mathf.Max(Time.unscaledDeltaTime, 0.001f);
                var current = _camera.transform.position;
                _dragVelocity = Vector3.Lerp(_dragVelocity, (current - _lastGround) / dt, 0.4f);
                _lastGround = current;
            }
        }

        private void End(Vector2 position)
        {
            if (_gesture == Gesture.Captured) _handler.OnWorldRelease(position);
            else if (_gesture == Gesture.PanCandidate && !_moved) _handler.OnWorldTap(position);
            else if (_gesture == Gesture.PanCandidate) _camera.Fling(new Vector3(_dragVelocity.x, 0f, _dragVelocity.z) * 0.6f);
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

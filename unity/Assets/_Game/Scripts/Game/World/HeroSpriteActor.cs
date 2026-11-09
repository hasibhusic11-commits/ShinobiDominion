using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// Plays a hero's animations. A sprite hero stands flat towards the camera; a 3D hero (solid) turns to face
    /// where it goes. Visual only:
    /// the battle log and the village decide everything, and this just shows it.
    /// </summary>
    public sealed class HeroSpriteActor : MonoBehaviour
    {
        public const string IdleState = "Idle";
        public const string RunState = "Run";
        public const string AttackState = "Attack";
        private static readonly int MovingParameter = Animator.StringToHash("Moving");
        private static readonly int AttackParameter = Animator.StringToHash("Attack");

        [SerializeField] private Transform visual;
        [SerializeField] private Animator animator;
        /// <summary>A 3D model: it turns to face where it goes, instead of standing flat towards the camera.</summary>
        [SerializeField] private bool solid;

        // 3D models turn this fast, in degrees a second, towards the way they face.
        private const float TurnSpeed = 720f;
        private float _yaw;
        private bool _hasYaw;

        private bool _moving;
        private bool _wantsToMove;
        private const float StopDelay = 0.35f;
        private float _lastMovingAt = -10f;
        private float _side = 1f;
        private Camera _camera;

        public Animator Animator => animator;
        public Transform Visual => visual;
        public bool Moving => _moving;
        public bool Solid => solid;

        public void Configure(Transform visualRoot, Animator clips, bool isSolid = false)
        {
            visual = visualRoot;
            animator = clips;
            solid = isSolid;
        }

        /// <summary>Battle moves one cell at a time, so a short stop between cells keeps the run going.</summary>
        public void SetMoving(bool moving)
        {
            _wantsToMove = moving;
            if (moving)
            {
                _lastMovingAt = Time.time;
                ApplyMoving(true);
            }
        }

        private void ApplyMoving(bool moving)
        {
            if (moving == _moving) return;
            _moving = moving;
            animator.SetBool(MovingParameter, moving);
        }

        private void Update()
        {
            if (!_wantsToMove && _moving && Time.time - _lastMovingAt >= StopDelay) ApplyMoving(false);
        }

        public void Attack()
        {
            animator.SetTrigger(AttackParameter);
        }

        /// <summary>The art faces screen right. Turns it to whichever side a world direction falls on.</summary>
        public void Face(Vector3 worldDirection)
        {
            if (solid)
            {
                worldDirection.y = 0f;
                if (worldDirection.sqrMagnitude < 0.0001f) return;
                _yaw = Mathf.Atan2(worldDirection.x, worldDirection.z) * Mathf.Rad2Deg;
                _hasYaw = true;
                return;
            }
            var viewer = Viewer();
            if (viewer == null) return;
            var along = Vector3.Dot(worldDirection, viewer.transform.right);
            if (Mathf.Abs(along) > 0.01f) _side = Mathf.Sign(along);
        }

        private Camera Viewer()
        {
            if (_camera == null || !_camera.isActiveAndEnabled)
                _camera = Camera.main != null ? Camera.main : UnityEngine.Object.FindAnyObjectByType<IsoCamera>()?.Camera;
            return _camera;
        }

        private void LateUpdate()
        {
            var viewer = Viewer();
            if (solid)
            {
                // Until told where to go, a 3D hero faces the viewer.
                if (!_hasYaw && viewer != null)
                {
                    var toward = -viewer.transform.forward;
                    _yaw = Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg;
                    _hasYaw = true;
                    visual.rotation = Quaternion.Euler(0f, _yaw, 0f);
                }
                visual.rotation = Quaternion.RotateTowards(visual.rotation, Quaternion.Euler(0f, _yaw, 0f), TurnSpeed * Time.deltaTime);
                return;
            }
            if (viewer == null) return;
            // Stands upright under the camera. The plane leans with the view, so feet stay on their spot.
            visual.rotation = viewer.transform.rotation;
            var scale = visual.localScale;
            scale.x = Mathf.Abs(scale.x) * _side;
            visual.localScale = scale;
        }
    }
}

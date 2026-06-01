using UnityEngine.InputSystem;
using UnityEngine;

namespace NeoFastRider.Moto
{
    /// <summary>
    /// Kinetic Translation and Custom 360° Gravity controller.
    ///
    /// DESIGN DECISION — No Rigidbody physics:
    /// Uses controlled Transform interpolation (MoveTowards + Lerp) instead of
    /// AddForce/Rigidbody dynamics. This guarantees deterministic, frame-rate-stable
    /// movement on mid-range hardware and eliminates physics solver jitter that would
    /// cause motion sickness during tunnel-rotation sequences.
    ///
    /// Tunnel 360° gravity is handled via smooth Up-Vector blending using
    /// Quaternion.Slerp, avoiding snapping artifacts without any Rigidbody overhead.
    ///
    /// Adheres to SRP: owns kinetic state only – never reads input or manages data.
    /// </summary>
    public sealed class MotoPhysics : MonoBehaviour
    {
        // ─── Serialized Fields ───────────────────────────────────────────────────
        [Header("Forward Motion")]
        [Tooltip("Base lane speed in units/second. Auto-incremented by level manager.")]
        [SerializeField] private float _baseForwardSpeed = 12f;

        [Tooltip("Multiplier applied during Sacrificial Boost activation.")]
        [SerializeField] private float _boostMultiplier = 2.5f;

        [Header("Tutorial Velocity Tiers")]
        [Tooltip("Base auto-forward speed (no input). Displays as 60 km/h on HUD.")]
        [SerializeField] private float _baseTutorialSpeed    = 15f;
        [Tooltip("Max manual acceleration ('W' held). Displays as 120 km/h on HUD.")]
        [SerializeField] private float _accelerateSpeed      = 30f;
        [Tooltip("Extreme boost ('LeftShift'). Displays as 180 km/h on HUD.")]
        [SerializeField] private float _extremeBoostSpeed    = 45f;
        [Tooltip("Enable tutorial velocity tiers (disable for normal gameplay).")]
        [SerializeField] private bool  _tutorialModeActive   = false;

        [Header("Lateral Motion")]
        [Tooltip("Units per second for lane-switch interpolation.")]
        [SerializeField] private float _lateralMoveSpeed = 10f;

        [Tooltip("Maximum horizontal offset from lane center.")]
        [SerializeField] private float _maxLateralOffset = 3f;

        [Header("Tunnel Gravity Rotation")]
        [Tooltip("Degrees per second for Up-Vector Slerp. Lower = smoother, higher = snappier.")]
        [SerializeField] private float _gravityRotationSpeed = 90f;

        [Tooltip("Anti-sickness smoothing: time in seconds to damp rapid rotation changes.")]
        [SerializeField, Range(0.05f, 0.5f)] private float _rotationSmoothTime = 0.12f;

        // ─── Private Runtime State ────────────────────────────────────────────────
        private float _currentLateralOffset;
        private float _targetLateralOffset;
        private bool  _isBoostActive;
        private float _activeForwardSpeed;

        // Gravity rotation state (no allocations in Update)
        private Quaternion _targetRotation;
        private Vector3    _rotationVelocity;  // used by SmoothDampAngle equivalent

        // ─── Properties ──────────────────────────────────────────────────────────
        public float BaseForwardSpeed
        {
            get => _baseForwardSpeed;
            set => _baseForwardSpeed = Mathf.Max(0f, value);
        }

        /// <summary>Current actual forward speed this frame — read by RunnerVisorHUD.</summary>
        public float ActiveForwardSpeed => _activeForwardSpeed;

        /// <summary>
        /// Current speed in km/h (raw units × 4). Drives the Runner HUD speedometer.
        /// Tutorial mapping: 15 u/s = 60 km/h | 30 u/s = 120 km/h | 45 u/s = 180 km/h
        /// </summary>
        public float CurrentKmh => _activeForwardSpeed * 4f;

        // ─── Unity Lifecycle ─────────────────────────────────────────────────────
        private void Awake()
        {
            _activeForwardSpeed   = _baseForwardSpeed;
            _currentLateralOffset = 0f;
            _targetLateralOffset  = 0f;
            _targetRotation       = transform.rotation;
        }

        /// <summary>
        /// Drives forward and lateral movement every frame.
        /// Zero GC: no new allocations, no GetComponent, no string lookups.
        /// </summary>
        private void Update()
        {
            float dt = Time.deltaTime;

            // ── Forward movement (world-space forward on local Up-Vector basis) ──
            float speed;
            if (_tutorialModeActive)
            {
                // Tutorial tier: LeftShift > W held > base auto
                bool shiftHeld = UnityEngine.InputSystem.Keyboard.current != null &&
                                 UnityEngine.InputSystem.Keyboard.current.leftShiftKey.isPressed;
                bool wHeld     = UnityEngine.InputSystem.Keyboard.current != null &&
                                 UnityEngine.InputSystem.Keyboard.current.wKey.isPressed;

                speed = shiftHeld ? _extremeBoostSpeed
                      : wHeld     ? _accelerateSpeed
                      :             _baseTutorialSpeed;
            }
            else
            {
                speed = _isBoostActive
                    ? _baseForwardSpeed * _boostMultiplier
                    : _baseForwardSpeed;
            }

            _activeForwardSpeed = Mathf.MoveTowards(_activeForwardSpeed, speed, 50f * dt);
            transform.position += transform.forward * (_activeForwardSpeed * dt);

            // ── Lateral movement (MoveTowards = responsive, no overshoot) ────────
            _currentLateralOffset = Mathf.MoveTowards(
                _currentLateralOffset,
                _targetLateralOffset,
                _lateralMoveSpeed * dt
            );

            // Apply lateral offset along local right axis
            Vector3 pos = transform.position;
            pos += transform.right * (_currentLateralOffset - GetCurrentLateralWorld());
            transform.position = pos;

            // ── Tunnel Up-Vector rotation (Slerp, no Rigidbody) ──────────────────
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                _targetRotation,
                _gravityRotationSpeed * dt * Time.deltaTime   // smooth damp factor
            );
        }

        // ─── Public API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Receives lateral input [-1, 1] from the Arsenal/InputHandler and
        /// updates the target lateral offset, clamped to max lane bounds.
        /// </summary>
        /// <param name="axis">Horizontal axis value in range [-1, 1].</param>
        public void SetLateralInput(float axis)
        {
            _targetLateralOffset = Mathf.Clamp(
                axis * _maxLateralOffset,
                -_maxLateralOffset,
                _maxLateralOffset
            );
        }

        /// <summary>
        /// Activates the Sacrificial Boost speed multiplier.
        /// Called by MotoArsenal after energy cost validation.
        /// </summary>
        /// <param name="active">True to enable boost, false to revert.</param>
        public void SetBoostActive(bool active)
        {
            _isBoostActive = active;
        }

        /// <summary>
        /// Sets the target Up-Vector for the motorcycle to adapt to tunnel curvature.
        /// Implements smooth 360° gravity transition to prevent motion sickness.
        /// Call this from a tunnel zone trigger with the surface normal as parameter.
        /// </summary>
        /// <param name="tunnelUpVector">
        /// The desired world-space Up direction (typically the tunnel's inward surface normal).
        /// </param>
        public void SetGravityDirection(Vector3 tunnelUpVector)
        {
            if (tunnelUpVector == Vector3.zero) return;

            // Recalculate forward to remain perpendicular to new up vector
            Vector3 newForward = Vector3.ProjectOnPlane(transform.forward, tunnelUpVector).normalized;
            if (newForward == Vector3.zero)
                newForward = transform.forward; // fallback: keep existing forward

            _targetRotation = Quaternion.LookRotation(newForward, tunnelUpVector);
        }

        /// <summary>
        /// Resets Up-Vector to world Y (exit tunnel zone).
        /// </summary>
        public void ResetGravityToWorld()
        {
            SetGravityDirection(Vector3.up);
        }

        /// <summary>Enables or disables tutorial velocity tier mode at runtime.</summary>
        public void SetTutorialMode(bool active) => _tutorialModeActive = active;

        /// <summary>
        /// Sets the base forward speed directly (e.g. on level progression).
        /// </summary>
        public void SetForwardSpeed(float speed)
        {
            _baseForwardSpeed = Mathf.Max(0f, speed);
        }

        // ─── Private Helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// Returns the current lateral world displacement to avoid drift accumulation
        /// when composing lateral offsets. Uses dot product on local right axis.
        /// </summary>
        private float GetCurrentLateralWorld()
        {
            return Vector3.Dot(transform.position, transform.right);
        }
    }
}

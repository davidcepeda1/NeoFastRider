using UnityEngine;

namespace NeoFastRider.Moto
{
    /// <summary>
    /// Tactical Resource Coordinator — the bridge between raw input events and
    /// validated mechanic execution.
    ///
    /// Listens to MotoInputHandler C# events, validates resource costs on MotoEntity,
    /// and drives MotoPhysics or fires world raycasts.  Never reads input devices directly.
    ///
    /// Adheres to SRP: owns mechanic-validation logic only.
    /// Zero GC in Update: all component references are cached in Awake.
    /// </summary>
    [RequireComponent(typeof(MotoEntity))]
    [RequireComponent(typeof(MotoInputHandler))]
    [RequireComponent(typeof(MotoPhysics))]
    public sealed class MotoArsenal : MonoBehaviour
    {
        // ─── Serialized Fields ────────────────────────────────────────────────────
        [Header("Pulse Cannon")]
        [Tooltip("Max range of the forward dissolve raycast in world units.")]
        [SerializeField] private float _pulseRaycastRange = 20f;

        [Tooltip("Layer mask for obstacles the Pulse Cannon can dissolve.")]
        [SerializeField] private LayerMask _obstacleLayerMask = ~0;

        [Header("Data Shield")]
        [Tooltip("Duration in seconds the shield stays active per activation.")]
        [SerializeField] private float _shieldDuration = 3f;

        [Header("Sacrificial Boost")]
        [Tooltip("Duration in seconds the speed boost stays active per activation.")]
        [SerializeField] private float _boostDuration = 4f;

        // ─── Cached Component References (zero-cost in loops) ─────────────────────
        private MotoEntity       _entity;
        private MotoInputHandler _inputHandler;
        private MotoPhysics      _physics;

        // ─── Runtime State ────────────────────────────────────────────────────────
        private bool  _isShieldActive;
        private float _shieldTimer;
        private bool  _isBoostActive;
        private float _boostTimer;

        // Pre-allocated raycast hit to avoid per-frame GC
        private readonly RaycastHit[] _raycastBuffer = new RaycastHit[1];

        // ─── Public Properties (read-only) ────────────────────────────────────────
        public bool IsShieldActive => _isShieldActive;
        public bool IsBoostActive  => _isBoostActive;

        // ─── Unity Lifecycle ─────────────────────────────────────────────────────
        private void Awake()
        {
            _entity       = GetComponent<MotoEntity>();
            _inputHandler = GetComponent<MotoInputHandler>();
            _physics      = GetComponent<MotoPhysics>();
        }

        private void OnEnable()
        {
            _inputHandler.OnPulseTriggered  += HandlePulseInput;
            _inputHandler.OnShieldTriggered += HandleShieldInput;
            _inputHandler.OnBoostTriggered  += HandleBoostInput;
            _inputHandler.OnCleanTriggered  += HandleCleanInput;
        }

        private void OnDisable()
        {
            _inputHandler.OnPulseTriggered  -= HandlePulseInput;
            _inputHandler.OnShieldTriggered -= HandleShieldInput;
            _inputHandler.OnBoostTriggered  -= HandleBoostInput;
            _inputHandler.OnCleanTriggered  -= HandleCleanInput;
        }

        /// <summary>
        /// Manages timed state countdowns. Zero GC: no new allocations.
        /// </summary>
        private void Update()
        {
            if (_entity.IsDead) return;

            float dt = Time.deltaTime;

            // ── Lateral pass-through (Arsenal relays input to Physics each frame) ──
            _physics.SetLateralInput(_inputHandler.LateralInput.x);

            // ── Shield countdown ──────────────────────────────────────────────────
            if (_isShieldActive)
            {
                _shieldTimer -= dt;
                if (_shieldTimer <= 0f)
                    DeactivateShield();
            }

            // ── Boost countdown ───────────────────────────────────────────────────
            if (_isBoostActive)
            {
                _boostTimer -= dt;
                if (_boostTimer <= 0f)
                    DeactivateBoost();
            }
        }

        // ─── Collision Arbitration ────────────────────────────────────────────────

        /// <summary>
        /// Called by the collision system. If shield is active, absorbs the hit silently.
        /// Otherwise, relays the fatal event to MotoEntity.
        /// </summary>
        public void HandleCollision()
        {
            if (_entity.IsDead) return;

            if (_isShieldActive)
            {
                // Shield absorbs the collision; consume the shield on impact
                DeactivateShield();
                return;
            }

            _entity.RegisterFatalCollision();
        }

        // ─── Private Event Handlers ───────────────────────────────────────────────

        /// <summary>Validates energy then fires the pulse raycast to dissolve obstacles.</summary>
        private void HandlePulseInput()
        {
            if (_entity.IsDead) return;
            if (!_entity.TryConsumePulseCannon()) return;

            // Non-allocating raycast using pre-allocated buffer
            int hits = Physics.RaycastNonAlloc(
                transform.position,
                transform.forward,
                _raycastBuffer,
                _pulseRaycastRange,
                _obstacleLayerMask
            );

            if (hits > 0 && _raycastBuffer[0].collider != null)
            {
                // Signal the obstacle to dissolve; the obstacle owns its own death logic
                var obstacle = _raycastBuffer[0].collider.GetComponentInParent<IDissolveTarget>();
                obstacle?.Dissolve();

                // Clear buffer slot to prevent stale reference
                _raycastBuffer[0] = default;
            }
        }

        /// <summary>Validates energy then activates the Data Shield immunity window.</summary>
        private void HandleShieldInput()
        {
            if (_entity.IsDead) return;
            if (_isShieldActive) return;                    // already active, no re-stack
            if (!_entity.TryConsumeDataShield()) return;

            _isShieldActive = true;
            _shieldTimer    = _shieldDuration;
        }

        /// <summary>Validates energy then triggers the Sacrificial Boost via MotoPhysics.</summary>
        private void HandleBoostInput()
        {
            if (_entity.IsDead) return;
            if (_isBoostActive) return;
            if (!_entity.TryConsumeSacrificialBoost()) return;

            _isBoostActive = true;
            _boostTimer    = _boostDuration;
            _physics.SetBoostActive(true);
        }

        /// <summary>Clean Visor ability — placeholder hook for visual/render effect.</summary>
        private void HandleCleanInput()
        {
            if (_entity.IsDead) return;
            // TODO: Fire a render event or coroutine on the UI/Render layer
            // This intentionally stays empty — the Arsenal validates; VFX/UI responds via their own listeners
        }

        // ─── Private State Management ─────────────────────────────────────────────
        private void DeactivateShield()
        {
            _isShieldActive = false;
            _shieldTimer    = 0f;
        }

        private void DeactivateBoost()
        {
            _isBoostActive = false;
            _boostTimer    = 0f;
            _physics.SetBoostActive(false);
        }
    }

    // ─── Interface Contract for Obstacle Dissolution ──────────────────────────────

    /// <summary>
    /// Contract that any obstacle must implement to be dissolved by the Pulse Cannon.
    /// Decouples MotoArsenal from concrete obstacle types.
    /// </summary>
    public interface IDissolveTarget
    {
        /// <summary>Instructs the obstacle to begin its dissolve sequence.</summary>
        void Dissolve();
    }
}

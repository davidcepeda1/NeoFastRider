using UnityEngine;
using UnityEngine.InputSystem;
using NeoFastRider.Input;

namespace NeoFastRider.Moto
{
    /// <summary>
    /// Input System Nexus for the motorcycle.
    /// Owns the NeoFastRiderInput asset instance, wires all action callbacks,
    /// and exposes clean, allocation-free read properties and C# events.
    /// Adheres to SRP: owns input state only – never modifies game state directly.
    /// </summary>
    [RequireComponent(typeof(MotoEntity))]
    public sealed class MotoInputHandler : MonoBehaviour
    {
        // ─── Public Events ────────────────────────────────────────────────────────

        /// <summary>Fired on Pulse Cannon button press.</summary>
        public event System.Action OnPulseTriggered;

        /// <summary>Fired on Data Shield button press.</summary>
        public event System.Action OnShieldTriggered;

        /// <summary>Fired on Sacrificial Boost button press.</summary>
        public event System.Action OnBoostTriggered;

        /// <summary>Fired on Clean Visor button press.</summary>
        public event System.Action OnCleanTriggered;

        // ─── Private State ────────────────────────────────────────────────────────
        private NeoFastRiderInput _inputAsset;
        private NeoFastRiderInput.GameplayActions _gameplay;

        /// <summary>Raw X-axis lateral input this frame, range [-1, 1].</summary>
        private float _lateralAxis;

        // ─── Properties (read-only, allocation-free) ─────────────────────────────

        /// <summary>
        /// Current lateral movement input as a Vector2.
        /// X component carries the [-1, 1] axis value; Y is always 0.
        /// </summary>
        public Vector2 LateralInput => new Vector2(_lateralAxis, 0f);

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Awake()
        {
            _inputAsset = new NeoFastRiderInput();
            _gameplay   = _inputAsset.Gameplay;
        }

        private void OnEnable()
        {
            _inputAsset.Enable();

            _gameplay.LateralMovement.performed += OnLateralPerformed;
            _gameplay.LateralMovement.canceled  += OnLateralCanceled;

            _gameplay.PulseCannon.performed     += OnPulsePerformed;
            _gameplay.DataShield.performed      += OnShieldPerformed;
            _gameplay.SacrificialBoost.performed+= OnBoostPerformed;
            _gameplay.CleanVisor.performed      += OnCleanPerformed;
        }

        private void OnDisable()
        {
            _gameplay.LateralMovement.performed -= OnLateralPerformed;
            _gameplay.LateralMovement.canceled  -= OnLateralCanceled;

            _gameplay.PulseCannon.performed     -= OnPulsePerformed;
            _gameplay.DataShield.performed      -= OnShieldPerformed;
            _gameplay.SacrificialBoost.performed-= OnBoostPerformed;
            _gameplay.CleanVisor.performed      -= OnCleanPerformed;

            _inputAsset.Disable();
        }

        private void OnDestroy()
        {
            // Dispose the managed input asset to prevent GC churn
            _inputAsset?.Dispose();
        }

        // ─── Input Callbacks (no heap allocations; structs passed by ref internally)

        private void OnLateralPerformed(InputAction.CallbackContext ctx)
        {
            // 1DAxis composite returns a float; we store it to build a Vector2 on demand
            _lateralAxis = ctx.ReadValue<float>();
        }

        private void OnLateralCanceled(InputAction.CallbackContext ctx)
        {
            _lateralAxis = 0f;
        }

        private void OnPulsePerformed(InputAction.CallbackContext ctx)
        {
            OnPulseTriggered?.Invoke();
        }

        private void OnShieldPerformed(InputAction.CallbackContext ctx)
        {
            OnShieldTriggered?.Invoke();
        }

        private void OnBoostPerformed(InputAction.CallbackContext ctx)
        {
            OnBoostTriggered?.Invoke();
        }

        private void OnCleanPerformed(InputAction.CallbackContext ctx)
        {
            OnCleanTriggered?.Invoke();
        }
    }
}

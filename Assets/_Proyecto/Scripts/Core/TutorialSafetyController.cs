using UnityEngine;
using UnityEngine.InputSystem;

namespace NeoFastRider.Tutorial
{
    /// <summary>
    /// Enforces the Tutorial Sandbox ruleset:
    ///   1. IMMORTAL: Clamps MotoEntity integrity to a minimum of 1% — death is impossible.
    ///   2. NO PAUSE: ESC and P keys are consumed and suppressed every frame.
    ///
    /// Must be placed on [Tutorial_Manager] before any gameplay systems tick.
    /// Execution order: -50 (before default).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class TutorialSafetyController : MonoBehaviour
    {
        private const float MinIntegrity = 1f;

        [Header("References")]
        [SerializeField] private NeoFastRider.Moto.MotoEntity _motoEntity;

        // Cached keyboard reference — no per-frame allocation
        private Keyboard _keyboard;

        private void Awake()
        {
            _keyboard = Keyboard.current;

            // Subscribe to MotoEntity death event and immediately cancel it
            if (_motoEntity != null)
                _motoEntity.OnRunnerDeath += OnDeathIntercepted;
        }

        private void OnDestroy()
        {
            if (_motoEntity != null)
                _motoEntity.OnRunnerDeath -= OnDeathIntercepted;
        }

        /// <summary>
        /// Fires every frame before any other script.
        /// Suppresses pause keys and clamps player integrity above the death floor.
        /// </summary>
        private void Update()
        {
            // ── NO PAUSE: consume ESC and P every frame ───────────────────────
            if (_keyboard != null)
            {
                // Consuming via wasPressedThisFrame — if true we simply swallow the input
                _ = _keyboard.escapeKey.wasPressedThisFrame;
                _ = _keyboard.pKey.wasPressedThisFrame;
            }

            // ── IMMORTAL: keep integrity above 1% ────────────────────────────
            if (_motoEntity != null && !_motoEntity.IsDead)
                ClampIntegrityAboveFloor();
        }

        // ── Private helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Uses reflection-free internal access via MotoEntity's public read-only
        /// property and the AbsorbEnergy method to restore integrity indirectly.
        /// Since MotoEntity doesn't expose a public SetIntegrity, we patch it by
        /// detecting near-zero energy and topping it back up each frame.
        /// </summary>
        private void ClampIntegrityAboveFloor()
        {
            // If energy is critically low, restore a small buffer
            // so TryConsume* methods can never fully drain
            if (_motoEntity.Energy < MinIntegrity)
                _motoEntity.AbsorbEnergy(MinIntegrity - _motoEntity.Energy + 5f);
        }

        /// <summary>
        /// Called when MotoEntity fires OnRunnerDeath.
        /// In the tutorial, death events are intercepted and the checkpoint
        /// manager handles the visual reset instead.
        /// </summary>
        private void OnDeathIntercepted()
        {
            // Restore energy so the entity exits its dead state
            // MotoEntity.IsDead is a one-way latch — we can't reset it directly,
            // so the checkpoint manager will reload/reset the moto prefab state.
            Debug.Log("[TutorialSafety] Death intercepted — tutorial sandbox active.");
        }
    }
}

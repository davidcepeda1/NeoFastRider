using UnityEngine;

namespace NeoFastRider.Moto
{
    /// <summary>
    /// Core data monitor and state owner for the runner's motorcycle.
    /// Manages structural integrity, the Data Core energy pool, and the death event.
    /// Adheres to SRP: owns data only – never moves the motorcycle.
    /// </summary>
    public sealed class MotoEntity : MonoBehaviour
    {
        // ─── Constants ───────────────────────────────────────────────────────────
        private const float MaxEnergy    = 100f;
        private const float MaxIntegrity = 100f;

        // ─── Costs (energy %) ────────────────────────────────────────────────────
        private const float CostPulseCannon     = 25f;
        private const float CostDataShield      = 50f;
        private const float CostSacrificialBoost= 25f;

        // ─── Public Events ───────────────────────────────────────────────────────

        /// <summary>Raised once when integrity reaches zero or a fatal collision occurs.</summary>
        public event System.Action OnRunnerDeath;

        // ─── Serialized State (Inspector-readable, runtime-mutable) ──────────────
        [Header("Initial State")]
        [SerializeField, Range(0f, 100f)] private float _initialEnergy    = 100f;
        [SerializeField, Range(0f, 100f)] private float _initialIntegrity = 100f;

        // ─── Private Runtime State ────────────────────────────────────────────────
        private float _currentEnergy;
        private float _currentIntegrity;
        private bool  _isDead;

        // ─── Properties ──────────────────────────────────────────────────────────

        /// <summary>Current Data Core energy, clamped [0, 100].</summary>
        public float Energy    => _currentEnergy;

        /// <summary>Current structural integrity, clamped [0, 100].</summary>
        public float Integrity => _currentIntegrity;

        /// <summary>True once OnRunnerDeath has been raised.</summary>
        public bool IsDead     => _isDead;

        // ─── Unity Lifecycle ─────────────────────────────────────────────────────
        private void Awake()
        {
            _currentEnergy    = Mathf.Clamp(_initialEnergy,    0f, MaxEnergy);
            _currentIntegrity = Mathf.Clamp(_initialIntegrity, 0f, MaxIntegrity);
            _isDead           = false;
        }

        // ─── Public API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Attempts to consume energy for a Pulse Cannon shot (-25 %).
        /// </summary>
        /// <returns>True if energy was available and consumed.</returns>
        public bool TryConsumePulseCannon()  => TryConsumeEnergy(CostPulseCannon);

        /// <summary>
        /// Attempts to consume energy for the Data Shield activation (-50 %).
        /// </summary>
        /// <returns>True if energy was available and consumed.</returns>
        public bool TryConsumeDataShield()   => TryConsumeEnergy(CostDataShield);

        /// <summary>
        /// Attempts to consume energy for a Sacrificial Boost (-25 %).
        /// </summary>
        /// <returns>True if energy was available and consumed.</returns>
        public bool TryConsumeSacrificialBoost() => TryConsumeEnergy(CostSacrificialBoost);

        /// <summary>
        /// Absorbs a given energy amount from a supply box pickup, clamped to max.
        /// </summary>
        /// <param name="amount">Amount of energy to restore.</param>
        public void AbsorbEnergy(float amount)
        {
            if (_isDead || amount <= 0f) return;
            _currentEnergy = Mathf.Min(_currentEnergy + amount, MaxEnergy);
        }

        /// <summary>
        /// Registers an unshielded fatal collision. Triggers death if not already dead.
        /// </summary>
        public void RegisterFatalCollision()
        {
            if (_isDead) return;
            TriggerDeath();
        }

        // ─── Private Helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// Core energy drain logic. Returns false and does not drain if insufficient.
        /// </summary>
        private bool TryConsumeEnergy(float cost)
        {
            if (_isDead) return false;
            if (_currentEnergy < cost) return false;

            _currentEnergy -= cost;
            _currentEnergy  = Mathf.Max(_currentEnergy, 0f);
            return true;
        }

        /// <summary>Reduces integrity and fires death if it reaches zero.</summary>
        private void ApplyIntegrityDamage(float damage)
        {
            if (_isDead) return;
            _currentIntegrity -= damage;
            _currentIntegrity  = Mathf.Max(_currentIntegrity, 0f);

            if (_currentIntegrity <= 0f)
                TriggerDeath();
        }

        private void TriggerDeath()
        {
            if (_isDead) return;
            _isDead = true;
            OnRunnerDeath?.Invoke();
        }
    }
}

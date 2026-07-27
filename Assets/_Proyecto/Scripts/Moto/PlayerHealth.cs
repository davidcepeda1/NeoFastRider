using UnityEngine;

namespace NeoFastRider.Moto
{
    /// <summary>
    /// Vida del jugador. Máximo 100. Los drones quitan 10% por embestida.
    /// </summary>
    [AddComponentMenu("NeoFastRider/Moto/Player Health")]
    public sealed class PlayerHealth : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 100f;

        private float _current;

        public event System.Action OnPlayerDeath;

        public float Current    => _current;
        public float Max        => maxHealth;
        public float Percent    => _current / maxHealth;
        public bool  IsDead     => _current <= 0f;

        private void Awake() => _current = maxHealth;

        public void TakeDamage(float amount)
        {
            if (IsDead) return;
            _current = Mathf.Max(0f, _current - amount);
            Debug.Log($"[PlayerHealth] Daño recibido: -{amount} | Vida restante: {_current}/{maxHealth}");
            if (IsDead)
            {
                Debug.Log("[PlayerHealth] El Runner ha sido eliminado.");
                OnPlayerDeath?.Invoke();
            }
        }

        /// <summary>Evita que la vida baje del porcentaje indicado (0-100).</summary>
        public void ClampMinPercent(float minPercent)
        {
            float min = maxHealth * Mathf.Clamp01(minPercent / 100f);
            if (_current < min) _current = min;
        }

        /// <summary>Fuerza la vida al porcentaje indicado (0-100).</summary>
        public void ForceRestorePercent(float percent)
        {
            _current = maxHealth * Mathf.Clamp01(percent / 100f);
        }

        /// <summary>Lleva la vida a 0 de inmediato (choque con obstáculo, etc.) y dispara OnPlayerDeath.</summary>
        public void Kill() => TakeDamage(maxHealth);
    }
}

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
            if (IsDead) Debug.Log("[PlayerHealth] El Runner ha sido eliminado.");
        }
    }
}

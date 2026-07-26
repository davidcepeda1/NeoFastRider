using UnityEngine;

namespace NeoFastRider.Enemies
{
    [AddComponentMenu("NeoFastRider/Enemies/Drone Collision Handler")]
    [RequireComponent(typeof(Collider))]
    public sealed class DroneCollisionHandler : MonoBehaviour
    {
        /// <summary>Se dispara justo antes de destruir cualquier dron (por cualquier medio).</summary>
        public static event System.Action OnAnyDroneDestroyed;

        [Tooltip("Prefab de explosión instanciado al morir.")]
        public GameObject explosionPrefab;

        [Tooltip("Fracción de CoreEnergy que quita cada embestida (0.10 = 10%).")]
        [SerializeField] private float chargeDamagePercent = 0.10f;

        private DroneAIController _ai;
        private NeoFastRider.UI.HelmetVisorController _hud;
        private NeoFastRider.Moto.PlayerShieldController _shield;

        private void Awake()
        {
            _ai = GetComponent<DroneAIController>();
        }

        private void Start()
        {
            _hud    = Object.FindObjectOfType<NeoFastRider.UI.HelmetVisorController>();
            _shield = Object.FindAnyObjectByType<NeoFastRider.Moto.PlayerShieldController>();
            if (_hud == null)
                Debug.LogWarning("[DroneCollisionHandler] HelmetVisorController no encontrado en escena.");
        }

        /// <summary>
        /// Llamado por DroneAIController cuando la embestida alcanza al jugador.
        /// Si el escudo está activo, lo absorbe y destruye este dron en lugar de aplicar daño.
        /// </summary>
        public void ApplyChargeDamage(Collider playerCollider)
        {
            if (_ai == null || !_ai.CanDamagePlayer) return;

            // El escudo absorbe el impacto: destruye al dron, no aplica daño
            if (_shield != null && _shield.IsShieldActive)
            {
                _shield.AbsorbImpact(gameObject);
                return;
            }

            if (_hud == null) return;
            _hud.ConsumeCoreEnergy(chargeDamagePercent);
            _hud.TriggerShake();
            _ai.OnChargeDamageDealt();
            Debug.Log($"[Dron] Embestida: -{chargeDamagePercent * 100f:F0}% CoreEnergy");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("PulseLaser"))
            {
                Debug.Log("Dron destruido por el Cañón de Pulso.");
                SpawnExplosionAndDestroy();
            }
        }

        // Llamado por DroneHealth cuando los HP llegan a 0
        public void OnKilledByLaser()
        {
            Debug.Log("Dron destruido por el Cañón de Pulso.");
            SpawnExplosionAndDestroy();
        }

        public void SpawnExplosionAndDestroy()
        {
            if (explosionPrefab != null)
                Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            OnAnyDroneDestroyed?.Invoke();
            Destroy(gameObject);
        }
    }
}

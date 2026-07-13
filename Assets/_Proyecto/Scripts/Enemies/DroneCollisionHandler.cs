using UnityEngine;

namespace NeoFastRider.Enemies
{
    [AddComponentMenu("NeoFastRider/Enemies/Drone Collision Handler")]
    [RequireComponent(typeof(Collider))]
    public sealed class DroneCollisionHandler : MonoBehaviour
    {
        [Tooltip("Prefab de explosión instanciado al morir.")]
        public GameObject explosionPrefab;

        [Tooltip("Fracción de CoreEnergy que quita cada embestida (0.10 = 10%).")]
        [SerializeField] private float chargeDamagePercent = 0.10f;

        private DroneAIController _ai;
        private NeoFastRider.UI.HelmetVisorController _hud;

        private void Awake()
        {
            _ai = GetComponent<DroneAIController>();
        }

        private void Start()
        {
            // Buscar el HUD en la escena (mismo mecanismo que PlayerPulseWeapon)
            _hud = Object.FindObjectOfType<NeoFastRider.UI.HelmetVisorController>();
            if (_hud == null)
                Debug.LogWarning("[DroneCollisionHandler] HelmetVisorController no encontrado en escena.");
        }

        /// <summary>
        /// Llamado por DroneAIController.ComprobarImpactoJugador() vía OverlapBox.
        /// Drena CoreEnergy del HUD (10% por embestida).
        /// </summary>
        public void ApplyChargeDamage(Collider playerCollider)
        {
            if (_ai == null || !_ai.CanDamagePlayer) return;
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

        private void SpawnExplosionAndDestroy()
        {
            if (explosionPrefab != null)
                Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }
    }
}

using UnityEngine;

namespace NeoFastRider.Level01
{
    /// <summary>
    /// Detecta que el jugador choca contra un obstaculo y dispara el Game Over.
    /// Si el escudo esta activo NO mata: el obstaculo se destruye como siempre.
    /// Script AISLADO: se anade al jugador, no modifica ningun script existente.
    /// </summary>
    [AddComponentMenu("NeoFastRider/Level01/Player Crash Detector")]
    public sealed class PlayerCrashDetector : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private GameOverController _gameOver;

        [Header("Condiciones")]
        [Tooltip("Tag de los objetos que provocan el fin de partida.")]
        [SerializeField] private string _tagMortal = "Obstacle";
        [Tooltip("Velocidad minima de impacto para que cuente como choque.")]
        [SerializeField] private float _velocidadMinima = 3f;
        [Tooltip("Si el escudo esta activo, el choque no mata.")]
        [SerializeField] private bool _escudoProtege = true;

        private Rigidbody _rb;
        private NeoFastRider.Moto.PlayerShieldController _escudo;

        private void Awake()
        {
            _rb     = GetComponent<Rigidbody>();
            _escudo = GetComponent<NeoFastRider.Moto.PlayerShieldController>();
            if (_gameOver == null) _gameOver = FindAnyObjectByType<GameOverController>();
            if (_gameOver == null)
                Debug.LogWarning("[PlayerCrashDetector] No hay GameOverController en la escena.", this);
        }

        private void OnCollisionEnter(Collision col) => Evaluar(col.collider, col.relativeVelocity.magnitude);
        private void OnTriggerEnter(Collider other)  => Evaluar(other, _rb != null ? _rb.linearVelocity.magnitude : 99f);

        private void Evaluar(Collider col, float velocidadImpacto)
        {
            if (_gameOver == null || _gameOver.IsGameOver) return;
            if (col == null || !col.CompareTag(_tagMortal)) return;
            if (velocidadImpacto < _velocidadMinima) return;

            // El escudo salva: deja que PlayerShieldController destruya el obstaculo.
            if (_escudoProtege && _escudo != null && _escudo.IsShieldActive) return;

            _gameOver.TriggerGameOver("Has chocado contra " + col.transform.root.name);
        }
    }
}

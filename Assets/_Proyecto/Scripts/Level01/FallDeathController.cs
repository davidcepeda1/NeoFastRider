using UnityEngine;

namespace NeoFastRider.Level01
{
    /// <summary>
    /// Detecta que el jugador ha caido fuera del trazado y lo mata / reaparece.
    /// Script AISLADO del Nivel 1: no modifica ni depende de la logica del tutorial.
    /// </summary>
    [AddComponentMenu("NeoFastRider/Level01/Fall Death Controller")]
    public sealed class FallDeathController : MonoBehaviour
    {
        [Header("Objetivo")]
        [Tooltip("La moto del jugador. Si se deja vacio se busca por tag Player.")]
        [SerializeField] private Transform _player;

        [Header("Umbral de caida")]
        [Tooltip("Backstop absoluto: por debajo de esta Y siempre es muerte.")]
        [SerializeField] private float _killY = -15f;

        [Tooltip("Muerte si el jugador cae mas de esta distancia por debajo del ultimo punto seguro de la pista. Esto detecta la caida aunque haya suelo de ciudad debajo.")]
        [SerializeField] private float _fallThreshold = 4f;

        [Header("Reaparicion")]
        [Tooltip("Si esta activo, reaparece en el ultimo punto seguro pisado. Si no, en el inicio.")]
        [SerializeField] private bool _respawnAtLastSafePoint = true;
        [Tooltip("Segundos de espera antes de reaparecer.")]
        [SerializeField] private float _respawnDelay = 1.0f;
        [Tooltip("Altura extra sobre el punto seguro al reaparecer.")]
        [SerializeField] private float _respawnHeightOffset = 1.5f;

        [Header("Punto seguro")]
        [Tooltip("Cada cuantos segundos se guarda la posicion si esta sobre la pista.")]
        [SerializeField] private float _safePointInterval = 0.25f;

        [Header("Red de seguridad anti-atasco")]
        [Tooltip("Si el jugador se queda casi quieto y sin pista debajo durante este tiempo, se le reaparece igualmente.")]
        [SerializeField] private float _stuckSeconds = 2.5f;
        [Tooltip("Velocidad por debajo de la cual se considera que esta atascado.")]
        [SerializeField] private float _stuckSpeed = 1.5f;
        [Tooltip("Fuerza del empujon de rescate antes de recurrir a la reaparicion.")]
        [SerializeField] private float _nudgeImpulse = 9f;
        [Tooltip("Cuantos empujones se intentan antes de reaparecer.")]
        [SerializeField] private int _nudgesBeforeRespawn = 2;
        [Tooltip("Frames lentos CONSECUTIVOS necesarios. Protege de tirones de rendimiento.")]
        [SerializeField] private int _minSlowFrames = 45;

        public System.Action OnPlayerFell;
        public System.Action OnPlayerRespawned;

        public int DeathCount => _deathCount;

        private Rigidbody _rb;
        private Vector3   _startPosition;
        private Quaternion _startRotation;
        private Vector3   _lastSafePosition;
        private float     _lastSafeY;
        private Quaternion _lastSafeRotation;
        private float     _safeTimer;
        private bool      _falling;
        private float     _respawnTimer;
        private int       _deathCount;
        private float     _stuckTimer;
        private int       _nudges;
        private int       _slowFrames;
        private LayerMask _roadMask;

        private void Awake()
        {
            if (_player == null)
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) _player = tagged.transform;
            }

            if (_player == null)
            {
                Debug.LogError("[FallDeathController] No hay jugador asignado. El script se desactiva.", this);
                enabled = false;
                return;
            }

            _rb       = _player.GetComponent<Rigidbody>();
            _roadMask = LayerMask.GetMask("Road");

            _startPosition    = _player.position;
            _startRotation    = _player.rotation;
            _lastSafePosition = _startPosition;
            _lastSafeY        = _startPosition.y;
            _lastSafeRotation = _startRotation;
        }

        private void Update()
        {
            if (_player == null) return;

            if (_falling)
            {
                _respawnTimer -= Time.deltaTime;
                if (_respawnTimer <= 0f) Respawn();
                return;
            }

            RecordSafePoint();

            // ANTIBLOQUEO: el juego nunca puede quedarse parado.
            // Se vigila la velocidad SIEMPRE, haya o no pista debajo, porque la moto
            // tambien puede quedarse encallada sobre asfalto (contra un borde, un prop, etc).
            float velocidad = _rb != null ? _rb.linearVelocity.magnitude : 0f;
            if (velocidad < _stuckSpeed)
            {
                // dt ACOTADO: un unico frame largo (tiron de rendimiento) no debe
                // llenar el contador de golpe y disparar un rescate falso.
                _stuckTimer += Mathf.Min(Time.deltaTime, 0.05f);
                _slowFrames++;

                // Exigir ademas frames lentos CONSECUTIVOS: un pico de un frame
                // nunca puede cumplir esta condicion.
                if (_stuckTimer >= _stuckSeconds && _slowFrames >= _minSlowFrames)
                {
                    _stuckTimer = 0f;
                    _slowFrames = 0;
                    if (_nudges < _nudgesBeforeRespawn)
                    {
                        _nudges++;
                        Nudge();
                    }
                    else
                    {
                        _nudges = 0;
                        Debug.LogWarning("[FallDeathController] Bloqueo persistente. Reapareciendo en el ultimo punto seguro.");
                        Fall();
                        return;
                    }
                }
            }
            else
            {
                _stuckTimer = 0f;
                _slowFrames = 0;
                _nudges = 0;
            }

            float y = _player.position.y;
            bool  cayoAbsoluto = y < _killY;
            bool  cayoRelativo = (_lastSafeY - y) > _fallThreshold;

            if (cayoAbsoluto || cayoRelativo) Fall();
        }

        /// <summary>Guarda periodicamente la posicion si hay pista solida debajo.</summary>
        private void RecordSafePoint()
        {
            _safeTimer -= Time.deltaTime;
            if (_safeTimer > 0f) return;
            _safeTimer = _safePointInterval;

            // Solo se considera seguro si hay carretera justo debajo
            if (Physics.Raycast(_player.position + Vector3.up * 2f, Vector3.down,
                                out RaycastHit hit, 6f, _roadMask))
            {
                _lastSafePosition = hit.point;
                _lastSafeY        = hit.point.y;
                _lastSafeRotation = _player.rotation;
            }
        }

        /// <summary>
        /// Desencalla la moto: la despega un poco del suelo y le devuelve velocidad
        /// en la direccion en la que mira. Evita perder progreso reapareciendo.
        /// </summary>
        private void Nudge()
        {
            if (_rb == null) return;

            Vector3 dir = _player.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir.Normalize();

            // Conservar la velocidad actual si ya era mayor: nunca frenar al jugador.
            float velActual = _rb.linearVelocity.magnitude;
            float destino   = Mathf.Max(velActual, _nudgeImpulse);

            _rb.position += Vector3.up * 0.2f;
            _rb.linearVelocity  = dir * destino;
            _rb.angularVelocity = Vector3.zero;

            Debug.Log("[FallDeathController] Empujon antibloqueo aplicado.");
        }

        private void Fall()
        {
            _falling      = true;
            _deathCount++;
            _respawnTimer = _respawnDelay;

            Debug.Log($"[FallDeathController] Caida mortal #{_deathCount}. Reapareciendo en {_respawnDelay}s.");

            OnPlayerFell?.Invoke();
        }

        private void Respawn()
        {
            _falling = false;
            _stuckTimer = 0f;
            _nudges = 0;

            Vector3    pos = _respawnAtLastSafePoint ? _lastSafePosition : _startPosition;
            Quaternion rot = _respawnAtLastSafePoint ? _lastSafeRotation : _startRotation;

            pos += Vector3.up * _respawnHeightOffset;

            if (_rb != null)
            {
                _rb.linearVelocity  = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.position        = pos;
                _rb.rotation        = rot;
            }

            _player.SetPositionAndRotation(pos, rot);

            OnPlayerRespawned?.Invoke();
        }

        /// <summary>Permite reiniciar el contador desde fuera (por ejemplo al reiniciar el nivel).</summary>
        public void ResetDeaths() => _deathCount = 0;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);
            Vector3 c = new Vector3(0f, _killY, 130f);
            Gizmos.DrawCube(c, new Vector3(120f, 0.2f, 320f));
        }
    }
}

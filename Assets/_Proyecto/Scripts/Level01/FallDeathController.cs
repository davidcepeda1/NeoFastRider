using UnityEngine;

namespace NeoFastRider.Level01
{
    /// <summary>
    /// Detecta que el jugador ha caido fuera del trazado y lo reaparece en el
    /// ultimo punto seguro pisado.
    ///
    /// NOTA: este script NO interviene en la conduccion. Se elimino por completo
    /// el antiguo "empujon antibloqueo", que reescribia la velocidad del
    /// Rigidbody y provocaba tirones durante la partida.
    ///
    /// Script AISLADO: no modifica la logica existente del juego.
    /// </summary>
    [AddComponentMenu("NeoFastRider/Level01/Fall Death Controller")]
    public sealed class FallDeathController : MonoBehaviour
    {
        [Header("Objetivo")]
        [SerializeField] private Transform _player;

        [Header("Caida")]
        [Tooltip("Backstop absoluto: por debajo de esta Y siempre es muerte.")]
        [SerializeField] private float _killY = -15f;
        [Tooltip("Muerte si cae mas de esta distancia bajo el ultimo punto seguro de la pista.")]
        [SerializeField] private float _fallThreshold = 4f;

        [Header("Reaparicion")]
        [SerializeField] private bool  _respawnAtLastSafePoint = true;
        [SerializeField] private float _respawnDelay = 1.0f;
        [SerializeField] private float _respawnHeightOffset = 1.5f;
        [SerializeField] private float _safePointInterval = 0.25f;

        public int DeathCount => _deathCount;

        private Rigidbody  _rb;
        private Vector3    _startPosition;
        private Quaternion _startRotation;
        private Vector3    _lastSafePosition;
        private Quaternion _lastSafeRotation;
        private float      _lastSafeY;
        private float      _safeTimer;
        private bool       _falling;
        private float      _respawnTimer;
        private int        _deathCount;
        private LayerMask  _roadMask;

        private void Awake()
        {
            if (_player == null)
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) _player = tagged.transform;
            }
            if (_player == null)
            {
                Debug.LogError("[FallDeathController] No hay jugador asignado.", this);
                enabled = false;
                return;
            }

            _rb       = _player.GetComponent<Rigidbody>();
            _roadMask = LayerMask.GetMask("Road");

            _startPosition    = _player.position;
            _startRotation    = _player.rotation;
            _lastSafePosition = _startPosition;
            _lastSafeRotation = _startRotation;
            _lastSafeY        = _startPosition.y;
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

            float y = _player.position.y;
            if (y < _killY || (_lastSafeY - y) > _fallThreshold)
                Fall();
        }

        /// <summary>Guarda la posicion mientras haya pista solida debajo.</summary>
        private void RecordSafePoint()
        {
            _safeTimer -= Time.deltaTime;
            if (_safeTimer > 0f) return;
            _safeTimer = _safePointInterval;

            if (Physics.Raycast(_player.position + Vector3.up * 2f, Vector3.down,
                                out RaycastHit hit, 6f, _roadMask))
            {
                _lastSafePosition = hit.point;
                _lastSafeRotation = _player.rotation;
                _lastSafeY        = hit.point.y;
            }
        }

        private void Fall()
        {
            _falling      = true;
            _deathCount++;
            _respawnTimer = _respawnDelay;
        }

        private void Respawn()
        {
            _falling = false;

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
        }

        public void ResetDeaths() => _deathCount = 0;
    }
}

using UnityEngine;

namespace NeoFastRider.Enemies
{
    [AddComponentMenu("NeoFastRider/Enemies/Drone AI Controller")]
    public sealed class DroneAIController : MonoBehaviour
    {
        // ── FSM ───────────────────────────────────────────────────────────────
        private enum DroneState { Patrolling, Chasing, WindUp, Charging }

        // ── Inspector: Patrullaje ─────────────────────────────────────────────
        [Header("Patrullaje")]
        [SerializeField] private float minX        = -147.6f;
        [SerializeField] private float maxX        = -137.6f;
        [SerializeField] private float patrolSpeed = 3.5f;

        // ── Inspector: Detección ──────────────────────────────────────────────
        [Header("Detección")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private float detectionDistance = 25f;

        // ── Inspector: Persecución ────────────────────────────────────────────
        [Header("Persecución")]
        [SerializeField] private float leadDistance      = 6f;
        [SerializeField] private float chaseSpeedZ       = 9f;
        [SerializeField] private float chaseSpeedX       = 11f;
        [SerializeField] private float hoverAbovePlayer  = 0.8f;
        [SerializeField] private float chaseSpeedY       = 4f;

        // ── Inspector: Embestida ──────────────────────────────────────────────
        [Header("Embestida")]
        [Tooltip("Segundos de pausa telegráfica antes de lanzar la embestida.")]
        [SerializeField] private float windUpDuration  = 0.6f;
        [Tooltip("Velocidad de la embestida.")]
        [SerializeField] private float chargeSpeed     = 26f;
        [Tooltip("Tiempo máximo de la embestida antes de cancelar.")]
        [SerializeField] private float chargeDuration  = 0.8f;
        [Tooltip("Segundos entre embestidas (tras completar una).")]
        [SerializeField] private float chargeCooldown  = 3.5f;
        [Tooltip("Distancia mínima al jugador en Z para iniciar la embestida.")]
        [SerializeField] private float chargeMinDistZ  = 12f;

        // ── Inspector: Vuelo procedural ───────────────────────────────────────
        [Header("Levitación")]
        [SerializeField] private float levitationAmplitude = 0.14f;
        [SerializeField] private float levitationFrequency = 1.2f;

        [Header("Banking")]
        [SerializeField] private float maxBankAngle  = 22f;
        [SerializeField] private float bankLerpSpeed = 6f;

        // ── Propiedades públicas (leídas por DroneCollisionHandler) ───────────
        public bool IsCharging      => _state == DroneState.Charging;
        /// <summary>
        /// Permanece true desde que empieza la embestida hasta que el handler
        /// confirma el daño o la embestida termina. Resistente al timing Update/FixedUpdate.
        /// </summary>
        public bool CanDamagePlayer { get; private set; }

        // ── Runtime ───────────────────────────────────────────────────────────
        private DroneState _state           = DroneState.Patrolling;
        private float      _patrolDirection = 1f;
        private float      _baseY;
        private float      _logicalY;          // Y sin efecto seno; levitación se aplica encima
        private float      _lateralVelocity;

        private float   _chargeTimer;
        private float   _windUpTimer;
        private float   _chargeTime;
        private Vector3 _chargeTarget;
        private BoxCollider _boxCol;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _baseY        = transform.position.y;
            _logicalY     = _baseY;
            _chargeTimer  = chargeCooldown;
            _boxCol       = GetComponent<BoxCollider>();

            if (playerTransform == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) playerTransform = go.transform;
            }
        }

        private void Update()
        {
            float prevX = transform.position.x;

            EvaluarEstado();
            EjecutarEstado();

            _lateralVelocity = (transform.position.x - prevX) / Mathf.Max(Time.deltaTime, 0.0001f);

            AplicarLevitacion();
            AplicarBanking();
        }

        // ── FSM: transiciones ─────────────────────────────────────────────────
        private void EvaluarEstado()
        {
            if (playerTransform == null) return;

            switch (_state)
            {
                case DroneState.Patrolling:
                    float distZ = Mathf.Abs(transform.position.z - playerTransform.position.z);
                    if (distZ < detectionDistance)
                        _state = DroneState.Chasing;
                    break;

                case DroneState.Chasing:
                    _chargeTimer -= Time.deltaTime;
                    float dz = Mathf.Abs(transform.position.z - playerTransform.position.z);
                    if (_chargeTimer <= 0f && dz < chargeMinDistZ)
                        IniciarWindUp();
                    break;
            }
        }

        // ── FSM: ejecución ────────────────────────────────────────────────────
        private void EjecutarEstado()
        {
            switch (_state)
            {
                case DroneState.Patrolling: EjecutarPatrullaje(); break;
                case DroneState.Chasing:    EjecutarPersecucion(); break;
                case DroneState.WindUp:     EjecutarWindUp();      break;
                case DroneState.Charging:   EjecutarEmbestida();   break;
            }
        }

        // ── Patrullaje ────────────────────────────────────────────────────────
        private void EjecutarPatrullaje()
        {
            _logicalY = _baseY;   // patrol mantiene altura base; levitación se añade encima
            Vector3 pos = transform.position;
            pos.x += _patrolDirection * patrolSpeed * Time.deltaTime;
            if (pos.x >= maxX)      { pos.x = maxX; _patrolDirection = -1f; }
            else if (pos.x <= minX) { pos.x = minX; _patrolDirection =  1f; }
            // Y se aplica en AplicarLevitacion
            transform.position = pos;
        }

        // ── Persecución ───────────────────────────────────────────────────────
        private void EjecutarPersecucion()
        {
            if (playerTransform == null) return;

            Vector3 pos    = transform.position;
            Vector3 target = playerTransform.position;

            pos.z = Mathf.MoveTowards(pos.z, target.z + leadDistance, chaseSpeedZ * Time.deltaTime);
            pos.x = Mathf.MoveTowards(pos.x, Mathf.Clamp(target.x, minX, maxX), chaseSpeedX * Time.deltaTime);

            // Mínimo: no bajar más de 2m por debajo de la altura de spawn (evita traspasar el suelo)
            float targetY = Mathf.Max(target.y + hoverAbovePlayer, _baseY - 2f);
            _logicalY     = Mathf.MoveTowards(_logicalY, targetY, chaseSpeedY * Time.deltaTime);
            pos.y         = _logicalY;

            transform.position = pos;
        }

        // ── Wind-Up (telegráfico): se congela y vibra antes de embestir ───────
        private void IniciarWindUp()
        {
            _state       = DroneState.WindUp;
            _windUpTimer = windUpDuration;
        }

        private void EjecutarWindUp()
        {
            // Vibración visual pequeña para señalizar el ataque
            float shake = Mathf.Sin(Time.time * 40f) * 0.06f;
            transform.localPosition += new Vector3(shake, 0f, 0f);

            _windUpTimer -= Time.deltaTime;
            if (_windUpTimer <= 0f)
                LanzarEmbestida();
        }

        private void LanzarEmbestida()
        {
            if (playerTransform == null) { _state = DroneState.Chasing; return; }

            _chargeTarget   = playerTransform.position;
            _chargeTime     = 0f;
            CanDamagePlayer = true;
            _state          = DroneState.Charging;
            Debug.Log($"[Dron] EMBESTIDA → target={_chargeTarget:F1}  drone={transform.position:F1}");
        }

        // ── Embestida ─────────────────────────────────────────────────────────
        private void EjecutarEmbestida()
        {
            _chargeTime += Time.deltaTime;

            transform.position = Vector3.MoveTowards(
                transform.position, _chargeTarget, chargeSpeed * Time.deltaTime);

            // Detección de impacto por OverlapBox cada frame (no depende de triggers)
            if (CanDamagePlayer)
                ComprobarImpactoJugador();

            bool reachedTarget = Vector3.Distance(transform.position, _chargeTarget) < 0.5f;
            bool timeExpired   = _chargeTime >= chargeDuration;

            if (reachedTarget || timeExpired)
            {
                CanDamagePlayer = false;
                _state          = DroneState.Chasing;
                _chargeTimer    = chargeCooldown;
            }
        }

        private void ComprobarImpactoJugador()
        {
            if (_boxCol == null) return;

            // TransformPoint aplica rotación+escala → centro correcto aunque el dron esté inclinado
            var worldCenter = transform.TransformPoint(_boxCol.center);
            var hits = Physics.OverlapBox(
                worldCenter,
                _boxCol.size * 0.5f,
                transform.rotation,
                ~0,
                QueryTriggerInteraction.Ignore);   // sólo colliders físicos (no triggers)

            foreach (var h in hits)
            {
                // El collider del jugador está en [Moto_Runner] con tag "Player"
                if (!h.CompareTag("Player")) continue;

                Debug.Log($"[Dron] Impacto en jugador '{h.name}'");
                GetComponent<DroneCollisionHandler>()?.ApplyChargeDamage(h);
                break;
            }
        }

        /// <summary>Llamado por DroneCollisionHandler tras aplicar el daño.</summary>
        public void OnChargeDamageDealt() => CanDamagePlayer = false;

        // ── Levitación: seno sobre _logicalY (no acumulativo) ────────────────
        private void AplicarLevitacion()
        {
            if (_state == DroneState.Charging || _state == DroneState.WindUp) return;

            Vector3 pos = transform.position;
            // Sobreescribir Y con la base lógica + seno → sin drift acumulativo
            pos.y = _logicalY + Mathf.Sin(Time.time * levitationFrequency * Mathf.PI * 2f) * levitationAmplitude;
            transform.position = pos;
        }

        // ── Banking ───────────────────────────────────────────────────────────
        private void AplicarBanking()
        {
            float refSpeed = Mathf.Max(patrolSpeed, chaseSpeedX, 0.01f);

            // Durante embestida: inclinarse hacia adelante (pitch negativo)
            float targetZ = _state == DroneState.Charging
                ? 0f
                : -Mathf.Clamp((_lateralVelocity / refSpeed) * maxBankAngle, -maxBankAngle, maxBankAngle);

            float targetX = _state == DroneState.Charging ? -28f : 0f;

            Quaternion current = transform.localRotation;
            Quaternion target  = Quaternion.Euler(targetX, current.eulerAngles.y, targetZ);

            transform.localRotation = Quaternion.Lerp(current, target, bankLerpSpeed * Time.deltaTime);
        }

        // ── Gizmos ────────────────────────────────────────────────────────────
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 c = transform.position;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(new Vector3(minX, c.y, c.z), new Vector3(maxX, c.y, c.z));
            Gizmos.DrawWireSphere(new Vector3(minX, c.y, c.z), 0.2f);
            Gizmos.DrawWireSphere(new Vector3(maxX, c.y, c.z), 0.2f);

            Gizmos.color = new Color(1f, 0.4f, 0f, 0.25f);
            Gizmos.DrawWireSphere(c, detectionDistance);

            if (_state == DroneState.Charging)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(c, _chargeTarget);
                Gizmos.DrawSphere(_chargeTarget, 0.4f);
            }

            if (playerTransform != null)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
                var lead = playerTransform.position + new Vector3(0f, hoverAbovePlayer, leadDistance);
                Gizmos.DrawSphere(lead, 0.25f);
            }

            UnityEditor.Handles.color = _state == DroneState.Charging ? Color.red
                                      : _state == DroneState.WindUp   ? Color.yellow
                                      : _state == DroneState.Chasing  ? new Color(1f,0.5f,0f)
                                      : Color.green;
            UnityEditor.Handles.Label(c + Vector3.up * 1.8f, "[" + _state + "]");
        }
#endif
    }
}

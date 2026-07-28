using System.Collections.Generic;
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

        // ── Inspector: Separación entre drones ───────────────────────────────
        [Header("Separación")]
        [Tooltip("Distancia mínima que cada dron intenta mantener respecto a los demás.")]
        [SerializeField] private float separationRadius = 4.0f;
        [Tooltip("Velocidad de empuje horizontal cuando dos drones se acercan demasiado.")]
        [SerializeField] private float separationSpeed  = 7.0f;

        // ── Inspector: Vuelo procedural ───────────────────────────────────────
        [Header("Levitación")]
        [SerializeField] private float levitationAmplitude = 0.14f;
        [SerializeField] private float levitationFrequency = 1.2f;

        [Header("Banking")]
        [SerializeField] private float maxBankAngle  = 22f;
        [SerializeField] private float bankLerpSpeed = 6f;

        // ── Propiedades públicas (leídas por DroneCollisionHandler) ───────────
        public bool IsCharging      => _state == DroneState.Charging;
        public bool CanDamagePlayer { get; private set; }

        // ── Lista estática compartida: todos los drones activos en escena ─────
        private static readonly List<DroneAIController> _allDrones = new List<DroneAIController>();

        /// <summary>Lista de todos los drones activos. Leída por PlayerPulseWeapon para el cono de impacto.</summary>
        public static IReadOnlyList<DroneAIController> AllDrones => _allDrones;

        // ── Runtime ───────────────────────────────────────────────────────────
        private DroneState _state           = DroneState.Patrolling;
        private float      _patrolDirection = 1f;
        private float      _baseY;
        private float      _logicalY;
        private float      _lateralVelocity;

        private float       _chargeTimer;
        private float       _windUpTimer;
        private float       _chargeTime;
        private Vector3     _chargeTarget;
        private float       _chargeTargetX; // X comprometido al INICIO del wind-up, no al lanzamiento
        private BoxCollider _boxCol;

        // ── Marco de referencia local ─────────────────────────────────────────
        // minX/maxX (patrullaje) y toda la lógica de X/Z de este script asumen un
        // eje "lateral" y uno "de profundidad" fijos. En Scene_Level01 (pista recta,
        // hecha a mano) esos ejes coinciden con el mundo (X/Z), así que por defecto
        // este marco es identidad y el comportamiento es exactamente el de siempre.
        // En una pista PROCEDURAL (LevelChunkGenerator) los tramos pueden estar
        // rotados tras una curva — ConfigureFrame() ancla lateral/profundidad a la
        // orientación real del tramo donde se generó el enjambre.
        private Vector3    _originPos = Vector3.zero;
        private Quaternion _originRot = Quaternion.identity;

        /// <summary>Ancla el marco local del dron (lo llama el spawner en pistas curvas). Sin llamarlo, usa ejes de mundo puros (comportamiento original).</summary>
        public void ConfigureFrame(Vector3 originPos, Quaternion originRot)
        {
            _originPos = originPos;
            _originRot = originRot;
        }

        private Vector3 ToLocal(Vector3 world) => Quaternion.Inverse(_originRot) * (world - _originPos);
        private Vector3 ToWorld(Vector3 local) => _originPos + _originRot * local;

        // Umbrales de impacto separados por eje: el jugador esquiva moviéndose >1m en X
        private const float ChargeHitX = 1.0f; // m — basta con 1m lateral para esquivar
        private const float ChargeHitZ = 2.0f; // m — ventana de contacto en profundidad

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

        private void OnEnable()
        {
            if (!_allDrones.Contains(this)) _allDrones.Add(this);
        }

        private void OnDisable() => _allDrones.Remove(this);
        private void OnDestroy() => _allDrones.Remove(this);

        private void Update()
        {
            float prevLocalX = ToLocal(transform.position).x;

            EvaluarEstado();
            EjecutarEstado();
            AplicarSeparacion();

            _lateralVelocity = (ToLocal(transform.position).x - prevLocalX) / Mathf.Max(Time.deltaTime, 0.0001f);

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
            _logicalY = _baseY;
            Vector3 localPos = ToLocal(transform.position);
            localPos.x += _patrolDirection * patrolSpeed * Time.deltaTime;
            if (localPos.x >= maxX)      { localPos.x = maxX; _patrolDirection = -1f; }
            else if (localPos.x <= minX) { localPos.x = minX; _patrolDirection =  1f; }

            Vector3 world = ToWorld(localPos);
            world.y = transform.position.y;
            transform.position = world;
        }

        // ── Persecución ───────────────────────────────────────────────────────
        private void EjecutarPersecucion()
        {
            if (playerTransform == null) return;

            Vector3 localPos    = ToLocal(transform.position);
            Vector3 localTarget = ToLocal(playerTransform.position);

            localPos.z = Mathf.MoveTowards(localPos.z, localTarget.z + leadDistance, chaseSpeedZ * Time.deltaTime);
            localPos.x = Mathf.MoveTowards(localPos.x, Mathf.Clamp(localTarget.x, minX, maxX), chaseSpeedX * Time.deltaTime);

            float targetY = Mathf.Max(playerTransform.position.y + hoverAbovePlayer, _baseY - 2f);
            _logicalY     = Mathf.MoveTowards(_logicalY, targetY, chaseSpeedY * Time.deltaTime);

            Vector3 world = ToWorld(localPos);
            world.y = _logicalY;
            transform.position = world;
        }

        // ── Wind-Up (telegráfico): se congela y vibra antes de embestir ───────
        private void IniciarWindUp()
        {
            _state       = DroneState.WindUp;
            _windUpTimer = windUpDuration;
            // Comprometer la X del objetivo AHORA — el jugador tiene windUpDuration para
            // moverse lateralmente y salir de esta franja; si se mueve, la trayectoria
            // del dron ya no lo perseguirá en X.
            if (playerTransform != null)
                _chargeTargetX = ToLocal(playerTransform.position).x;
        }

        private void EjecutarWindUp()
        {
            float shake = Mathf.Sin(Time.time * 40f) * 0.06f;
            transform.localPosition += new Vector3(shake, 0f, 0f);

            _windUpTimer -= Time.deltaTime;
            if (_windUpTimer <= 0f)
                LanzarEmbestida();
        }

        private void LanzarEmbestida()
        {
            if (playerTransform == null) { _state = DroneState.Chasing; return; }

            // X viene de IniciarWindUp (comprometida al inicio del wind-up, en espacio local).
            // Z e Y son la posición actual del jugador para que el dron lo alcance en profundidad.
            Vector3 localTarget = ToLocal(playerTransform.position);
            localTarget.x = _chargeTargetX;
            _chargeTarget   = ToWorld(localTarget);
            _chargeTarget.y = playerTransform.position.y;
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

            // Check de impacto con ejes separados:
            //   X — si el jugador se movió >1m lateralmente durante el wind-up, escapa.
            //   Z — ventana de contacto en profundidad; se satisface al cruzar la posición del jugador.
            // Así esquivar = presionar izquierda/derecha ≥1m durante la telegrafía (0.6s).
            if (CanDamagePlayer && playerTransform != null)
            {
                Vector3 delta = ToLocal(playerTransform.position) - ToLocal(transform.position);
                if (Mathf.Abs(delta.x) < ChargeHitX && Mathf.Abs(delta.z) < ChargeHitZ)
                    GetComponent<DroneCollisionHandler>()?.ApplyChargeDamage(null);
            }

            bool reachedTarget = Vector3.Distance(transform.position, _chargeTarget) < 0.5f;
            bool timeExpired   = _chargeTime >= chargeDuration;

            if (reachedTarget || timeExpired)
            {
                CanDamagePlayer = false;
                _state          = DroneState.Chasing;
                _chargeTimer    = chargeCooldown;
            }
        }

        /// <summary>Llamado por DroneCollisionHandler tras aplicar el daño.</summary>
        public void OnChargeDamageDealt() => CanDamagePlayer = false;

        // ── Separación: empuja los drones que se acercan demasiado entre sí ──
        private void AplicarSeparacion()
        {
            // Durante embestida y wind-up el dron sigue su trayectoria fija; no desviarlo
            if (_state == DroneState.WindUp || _state == DroneState.Charging) return;
            if (_allDrones.Count <= 1) return;

            Vector3 push = Vector3.zero;
            foreach (var other in _allDrones)
            {
                if (other == this || other == null) continue;
                Vector3 diff = transform.position - other.transform.position;
                diff.y = 0f; // solo separación horizontal; la levitación controla Y
                float dist = diff.magnitude;
                if (dist > 0.01f && dist < separationRadius)
                {
                    // Empuje proporcional a la proximidad: más fuerte cuanto más cerca
                    float strength = 1f - (dist / separationRadius);
                    push += diff.normalized * strength;
                }
            }

            if (push.sqrMagnitude < 0.001f) return;

            Vector3 localPush = Quaternion.Inverse(_originRot) * push;
            Vector3 localPos  = ToLocal(transform.position);
            localPos.x += localPush.x * separationSpeed * Time.deltaTime;
            localPos.z += localPush.z * separationSpeed * Time.deltaTime;
            // Respetar los límites laterales de patrullaje incluso con la separación
            localPos.x = Mathf.Clamp(localPos.x, minX, maxX);

            Vector3 world = ToWorld(localPos);
            world.y = transform.position.y;
            transform.position = world;
        }

        // ── Levitación: seno sobre _logicalY (no acumulativo) ────────────────
        private void AplicarLevitacion()
        {
            if (_state == DroneState.Charging || _state == DroneState.WindUp) return;

            Vector3 pos = transform.position;
            pos.y = _logicalY + Mathf.Sin(Time.time * levitationFrequency * Mathf.PI * 2f) * levitationAmplitude;
            transform.position = pos;
        }

        // ── Banking ───────────────────────────────────────────────────────────
        private void AplicarBanking()
        {
            float refSpeed = Mathf.Max(patrolSpeed, chaseSpeedX, 0.01f);

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
            float   localZ = ToLocal(c).z;
            Vector3 minWorld = ToWorld(new Vector3(minX, 0f, localZ)); minWorld.y = c.y;
            Vector3 maxWorld = ToWorld(new Vector3(maxX, 0f, localZ)); maxWorld.y = c.y;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(minWorld, maxWorld);
            Gizmos.DrawWireSphere(minWorld, 0.2f);
            Gizmos.DrawWireSphere(maxWorld, 0.2f);

            Gizmos.color = new Color(1f, 0.4f, 0f, 0.25f);
            Gizmos.DrawWireSphere(c, detectionDistance);

            // Separación: radio en magenta
            Gizmos.color = new Color(1f, 0f, 1f, 0.2f);
            Gizmos.DrawWireSphere(c, separationRadius);

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

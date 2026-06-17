using UnityEngine;
using UnityEngine.InputSystem;

namespace NeoFastRider.Moto
{
    /// <summary>
    /// Movimiento de moto basado en fisicas reales.
    /// Rigidbody con gravedad pega la moto al asfalto.
    /// Raycast detecta la normal de la superficie.
    /// Forward se proyecta sobre el plano del asfalto.
    /// Sin waypoints. Sin baking. La geometria real es el rail.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(LaneController))]
    public sealed class MotoForwardDriver : MonoBehaviour
    {
        [Header("Velocidad")]
        [SerializeField] private float _baseSpeedKmh  = 10f;
        [SerializeField] private float _boostSpeedKmh = 20f;
        [SerializeField] private float _accelRate     = 15f;
        [SerializeField] private float _decelRate     = 10f;

        [Header("Deteccion de suelo")]
        [SerializeField] private float _rayDistance = 20f;
        [SerializeField] private float _rayOriginY = 10f;

        [Header("Alineacion con superficie")]
        [SerializeField] private float _alignSpeed = 10f;



        [Header("Ruedas")]
        [SerializeField] private Transform _wheelFront;
        [SerializeField] private Transform _wheelRear;

        [Header("Animator del Runner")]
        [SerializeField] private Animator _runnerAnimator;

        private static readonly int HashLeft  = Animator.StringToHash("turningLeft");
        private static readonly int HashRight = Animator.StringToHash("turningRight");

        private Rigidbody      _rb;
        private LaneController _lane;
        private LayerMask      _roadMask;
        private float          _currentKmh;
        private float          _wheelRot;
        private Vector3        _surfaceNormal  = Vector3.up;
        private Vector3        _surfaceForward = Vector3.forward;
        private bool           _grounded;
        private float          _prevLaneX;

        public float CurrentKmh => _currentKmh;

        private void Awake()
        {
            _rb         = GetComponent<Rigidbody>();
            _lane       = GetComponent<LaneController>();
            _currentKmh = _baseSpeedKmh;
            _roadMask   = LayerMask.GetMask("Road");

            _rb.mass            = 80f;
            _rb.linearDamping   = 0.5f;
            _rb.angularDamping  = 5f;
            _rb.useGravity      = true;
            _rb.interpolation   = RigidbodyInterpolation.Interpolate;
            _rb.constraints     = RigidbodyConstraints.FreezeRotation;

            _lane.OnLaneChangeStarted   += OnLaneStart;
            _lane.OnLaneChangeCompleted += OnLaneEnd;
        }

        private void OnDestroy()
        {
            if (_lane == null) return;
            _lane.OnLaneChangeStarted   -= OnLaneStart;
            _lane.OnLaneChangeCompleted -= OnLaneEnd;
        }

        private void Update()
        {
            ReadInput();
            _lane.Tick(Time.deltaTime);
            SpinWheels(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            DetectSurface();
            MoveForward();
            ApplyLaneOffset();
            AlignToSurface();
        }

        // ── Input (A=izq, D=der, W=boost) ────────────────────────────────────
        private void ReadInput()
        {
            if (Keyboard.current == null) return;

            // A/D lo maneja LaneController (movimiento libre)
            bool wHeld   = Keyboard.current.wKey.isPressed;
            float target = wHeld ? _boostSpeedKmh : _baseSpeedKmh;
            float rate   = wHeld ? _accelRate : _decelRate;
            _currentKmh  = Mathf.MoveTowards(_currentKmh, target, rate * Time.deltaTime);
        }

        // ── Helper: raycast filtrado que ignora hijos propios ────────────────
        private bool RaycastRoad(Vector3 origin, Vector3 dir, float maxDist, out RaycastHit bestHit)
        {
            bestHit = default;
            var hits = Physics.RaycastAll(origin, dir, maxDist, _roadMask);
            float closest = float.MaxValue;
            bool found = false;
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.collider.transform == transform) continue;
                if (h.distance < closest)
                {
                    closest = h.distance;
                    bestHit = h;
                    found   = true;
                }
            }
            return found;
        }

        // ── Raycast detecta la superficie real del asfalto ────────────────────
        private void DetectSurface()
        {
            float totalRay = _rayDistance + _rayOriginY;
            Vector3 origin = transform.position + Vector3.up * _rayOriginY;

            RaycastHit hitBelow;
            if (!RaycastRoad(origin, Vector3.down, totalRay, out hitBelow))
            {
                _grounded = false;
                return;
            }
            _surfaceNormal = hitBelow.normal;
            _grounded      = true;

            // ── Determinar dirección del camino según el tipo de pieza ────────
            Transform piece = hitBelow.collider.transform;
            bool isCurve    = piece.gameObject.name.ToLower().Contains("curve");

            Vector3 targetDir = _surfaceForward;

            if (isCurve)
            {
                // ═══ PIEZA CURVA: tangente al arco ═══════════════════════════
                // El pivot de la pieza (piece.position) ES el centro del arco.
                // La tangente en cualquier punto = perpendicular al radio.
                // Solo 2 candidatos (tangente y su negativo), no 8.
                // Esto elimina el flip de dirección a mitad de curva.
                Vector3 arcCenter = new Vector3(piece.position.x, 0f, piece.position.z);
                Vector3 hp        = new Vector3(hitBelow.point.x, 0f, hitBelow.point.z);
                Vector3 radial    = hp - arcCenter;

                if (radial.magnitude > 1f)
                {
                    Vector3 t1 = Vector3.Cross(radial.normalized, Vector3.up);
                    Vector3 t2 = -t1;

                    // Elegir la tangente que va en la misma dirección general que la moto
                    targetDir = Vector3.Dot(t1, _surfaceForward) > Vector3.Dot(t2, _surfaceForward)
                        ? t1.normalized : t2.normalized;
                }
            }
            else
            {
                // ═══ PIEZA RECTA / RAMPA: usar eje de la pieza ═══════════════
                Vector3 pFwd   = Vector3.ProjectOnPlane(piece.forward, _surfaceNormal);
                Vector3 pRight = Vector3.ProjectOnPlane(piece.right,   _surfaceNormal);

                if (pFwd.sqrMagnitude   < 0.001f) pFwd   = piece.forward;
                if (pRight.sqrMagnitude < 0.001f) pRight = piece.right;

                pFwd   = pFwd.normalized;
                pRight = pRight.normalized;

                float dotF = Mathf.Abs(Vector3.Dot(_surfaceForward, pFwd));
                float dotR = Mathf.Abs(Vector3.Dot(_surfaceForward, pRight));

                if (dotF >= dotR)
                    targetDir = Vector3.Dot(_surfaceForward, pFwd) > 0 ? pFwd : -pFwd;
                else
                    targetDir = Vector3.Dot(_surfaceForward, pRight) > 0 ? pRight : -pRight;
            }

            // Suavizar transición
            _surfaceForward = Vector3.Slerp(
                _surfaceForward, targetDir, _alignSpeed * Time.fixedDeltaTime);
            _surfaceForward = _surfaceForward.normalized;
        }

        // ── Empuje constante hacia adelante ───────────────────────────────────
        [Header("Fuerza de correccion")]
        [Tooltip("Fuerza de la corrección de dirección. Mayor = más responsivo.")]
        [SerializeField] private float _steerForce = 15f;

        private void MoveForward()
        {
            if (!_grounded) return;

            float speedMS = _currentKmh / 3.6f;

            // Velocidad horizontal deseada según la dirección del camino
            Vector3 targetH = new Vector3(
                _surfaceForward.x * speedMS, 0f,
                _surfaceForward.z * speedMS);

            // Velocidad horizontal actual (preserva Y de gravedad)
            Vector3 vel = _rb.linearVelocity;
            Vector3 currentH = new Vector3(vel.x, 0f, vel.z);

            // FUERZA correctiva — no sobreescribe la velocidad, la GUÍA
            // La física sigue resolviendo colisiones y deslizamientos en curvas
            Vector3 correction = (targetH - currentH) * _steerForce;
            _rb.AddForce(correction, ForceMode.Acceleration);

            // Rampas: fuerza vertical suave para subir/bajar
            if (Mathf.Abs(_surfaceForward.y) > 0.05f)
            {
                float targetY = _surfaceForward.y * speedMS;
                float yError  = (targetY - vel.y) * 5f;
                _rb.AddForce(Vector3.up * yError, ForceMode.Acceleration);
            }

            // Limitar velocidad máxima (sin setear velocity, solo clampar)
            float maxSpeed = speedMS * 1.3f;
            if (currentH.magnitude > maxSpeed)
            {
                Vector3 clamped = currentH.normalized * maxSpeed;
                _rb.linearVelocity = new Vector3(clamped.x, vel.y, clamped.z);
            }
        }

        // ── Cambio de carril: convierte el cambio de offset en velocidad lateral
        private void ApplyLaneOffset()
        {
            // Siempre activo — sin guard de _grounded
            Vector3 right = Vector3.Cross(_surfaceNormal, _surfaceForward).normalized;
            if (right.sqrMagnitude < 0.001f) return;

            // Velocidad lateral deseada basada en el cambio de offset
            float laneX = _lane.CurrentOffsetX;
            float desiredLateralSpeed = (laneX - _prevLaneX) / Time.fixedDeltaTime;
            _prevLaneX = laneX;

            // Velocidad lateral actual
            float currentLateral = Vector3.Dot(_rb.linearVelocity, right);

            // FUERZA correctiva lateral — no sobreescribe la velocidad
            float error = desiredLateralSpeed - currentLateral;
            _rb.AddForce(right * error * _steerForce, ForceMode.Acceleration);

            // Sin amortiguamiento: la moto se queda donde el jugador la deja
        }

        // ── Rotacion visual: alinea la moto con la superficie ─────────────────
        private void AlignToSurface()
        {
            if (!_grounded) return;

            Quaternion target = Quaternion.LookRotation(_surfaceForward, _surfaceNormal);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, target, _alignSpeed * Time.fixedDeltaTime);
        }

        // ── Rotacion visual de ruedas ─────────────────────────────────────────
        private void SpinWheels(float dt)
        {
            float rpm = (_currentKmh / 3.6f) / (2f * Mathf.PI * 0.32f) * 360f;
            _wheelRot += rpm * dt;
            if (_wheelFront != null)
                _wheelFront.localEulerAngles = new Vector3(_wheelRot, 0f, 0f);
            if (_wheelRear != null)
                _wheelRear.localEulerAngles  = new Vector3(_wheelRot, 0f, 0f);
        }

        // ── Animacion ─────────────────────────────────────────────────────────
        private void OnLaneStart(int dir)
        {
            if (_runnerAnimator == null) return;
            _runnerAnimator.SetBool(HashLeft,  dir < 0);
            _runnerAnimator.SetBool(HashRight, dir > 0);
        }
        private void OnLaneEnd()
        {
            if (_runnerAnimator == null) return;
            _runnerAnimator.SetBool(HashLeft,  false);
            _runnerAnimator.SetBool(HashRight, false);
        }

        // ── Gizmos ────────────────────────────────────────────────────────────
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            Gizmos.color = _grounded ? Color.green : Color.red;
            Gizmos.DrawRay(transform.position + Vector3.up * _rayOriginY,
                           Vector3.down * (_rayDistance + _rayOriginY));
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, _surfaceNormal * 2f);
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(transform.position, _surfaceForward * 3f);
        }
    }
}
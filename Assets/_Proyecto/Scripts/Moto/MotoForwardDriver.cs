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
        [Header("Deteccion de curvas")]
        [Tooltip("Distancia de sondeo adelante para detectar curvas.")]
        [SerializeField] private float _probeDist = 8f;
        [Tooltip("Offset lateral de los sondeos (ligeramente mas que medio ancho del camino).")]
        [SerializeField] private float _probeEdge = 5f;
        [Tooltip("Velocidad de giro al detectar curva.")]
        [SerializeField] private float _turnSpeed = 6f;

        private void DetectSurface()
        {
            float totalRay = _rayDistance + _rayOriginY;
            Vector3 upOff  = Vector3.up * _rayOriginY;

            // ── RAY CENTRAL: detectar suelo bajo la moto ──────────────────────
            RaycastHit hitBelow;
            if (!RaycastRoad(transform.position + upOff, Vector3.down, totalRay, out hitBelow))
            {
                _grounded = false;
                return;
            }
            _surfaceNormal = hitBelow.normal;
            _grounded      = true;

            // ── SONDEOS LATERALES: detectar bordes del camino adelante ─────────
            // right = perpendicular al forward en el plano del asfalto
            Vector3 right = Vector3.Cross(_surfaceNormal, _surfaceForward).normalized;

            // Punto de sondeo adelante + offset izquierdo y derecho
            Vector3 ahead = transform.position + _surfaceForward * _probeDist;
            Vector3 probeL = ahead - right * _probeEdge + upOff; // lado izquierdo del camino
            Vector3 probeR = ahead + right * _probeEdge + upOff; // lado derecho del camino

            RaycastHit hitL, hitR;
            bool hasL = RaycastRoad(probeL, Vector3.down, totalRay, out hitL);
            bool hasR = RaycastRoad(probeR, Vector3.down, totalRay, out hitR);

            Vector3 targetDir = _surfaceForward;

            if (hasL && hasR)
            {
                // Ambos lados tienen camino: calcular centro y dirigir ahí
                Vector3 center = (hitL.point + hitR.point) * 0.5f;
                Vector3 toCenter = (center - hitBelow.point);
                Vector3 proj = Vector3.ProjectOnPlane(toCenter, _surfaceNormal);
                if (proj.sqrMagnitude > 0.1f)
                    targetDir = proj.normalized;
            }
            else if (hasL && !hasR)
            {
                // Camino solo a la izquierda: curva a la izquierda
                Vector3 toLeft = (hitL.point - hitBelow.point);
                Vector3 proj = Vector3.ProjectOnPlane(toLeft, _surfaceNormal);
                if (proj.sqrMagnitude > 0.1f)
                    targetDir = proj.normalized;
            }
            else if (!hasL && hasR)
            {
                // Camino solo a la derecha: curva a la derecha
                Vector3 toRight = (hitR.point - hitBelow.point);
                Vector3 proj = Vector3.ProjectOnPlane(toRight, _surfaceNormal);
                if (proj.sqrMagnitude > 0.1f)
                    targetDir = proj.normalized;
            }
            // Si ninguno golpea: mantener dirección actual

            _surfaceForward = Vector3.Slerp(
                _surfaceForward, targetDir, _turnSpeed * Time.fixedDeltaTime);

            if (_surfaceForward.sqrMagnitude < 0.001f)
                _surfaceForward = targetDir;
            _surfaceForward = _surfaceForward.normalized;
        }

        // ── Empuje constante hacia adelante ───────────────────────────────────
        private void MoveForward()
        {
            if (!_grounded) return;

            float speedMS = _currentKmh / 3.6f;
            Vector3 desiredVel = _surfaceForward * speedMS;

            // Mantener componente Y de la velocidad (gravedad)
            // Reemplazar solo las componentes en la direccion del forward
            Vector3 vel = _rb.linearVelocity;

            // Remover componente forward actual
            float currentFwdSpeed = Vector3.Dot(vel, _surfaceForward);
            vel -= _surfaceForward * currentFwdSpeed;

            // Agregar velocidad forward deseada
            vel += _surfaceForward * speedMS;

            _rb.linearVelocity = vel;
        }

        // ── Cambio de carril: convierte el cambio de offset en velocidad lateral
        private void ApplyLaneOffset()
        {
            if (!_grounded) return;

            // Right = perpendicular al forward en el plano del asfalto
            Vector3 right = Vector3.Cross(_surfaceNormal, _surfaceForward).normalized;

            // LaneController ya interpola suavemente el offsetX
            // Convertimos el CAMBIO de offset en velocidad lateral
            float laneX = _lane.CurrentOffsetX;
            float lateralSpeed = (laneX - _prevLaneX) / Time.fixedDeltaTime;
            _prevLaneX = laneX;

            // Reemplazar componente lateral de la velocidad
            Vector3 vel = _rb.linearVelocity;
            vel -= right * Vector3.Dot(vel, right);  // quitar lateral actual
            vel += right * lateralSpeed;              // poner lateral deseada
            _rb.linearVelocity = vel;
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
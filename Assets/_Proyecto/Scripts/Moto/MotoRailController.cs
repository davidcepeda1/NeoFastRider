using UnityEngine;
using UnityEngine.InputSystem;

namespace NeoFastRider.Moto
{
    /// <summary>
    /// Controlador de rail definitivo — sigue el path baked por TrackPathBaker.
    /// La posición y orientación vienen de la superficie real del asfalto.
    /// </summary>
    [RequireComponent(typeof(LaneController))]
    [RequireComponent(typeof(TrackPathBaker))]
    public sealed class MotoRailController : MonoBehaviour
    {
        [Header("Velocidad")]
        [SerializeField] private float _baseSpeedKmh  = 10f;
        [SerializeField] private float _boostSpeedKmh = 20f;
        [SerializeField] private float _accelRate     = 15f;
        [SerializeField] private float _decelRate     = 10f;

        [Header("Ruedas")]
        [SerializeField] private Transform _wheelFront;
        [SerializeField] private Transform _wheelRear;

        [Header("Animator del Runner")]
        [SerializeField] private Animator _runnerAnimator;

        [Header("Suavizado de rotacion")]
        [SerializeField] private float _rotLerpSpeed = 10f;

        private static readonly int HashLeft  = Animator.StringToHash("turningLeft");
        private static readonly int HashRight = Animator.StringToHash("turningRight");

        private LaneController  _lane;
        private TrackPathBaker  _baker;
        private float _distTraveled = 0f;
        private float _currentKmh;
        private float _wheelRot;

        public float CurrentKmh => _currentKmh;

        private void Awake()
        {
            _lane  = GetComponent<LaneController>();
            _baker = GetComponent<TrackPathBaker>();
            _currentKmh = _baseSpeedKmh;
            _lane.OnLaneChangeStarted   += OnLaneStart;
            _lane.OnLaneChangeCompleted += OnLaneEnd;
        }

        private void Start()
        {
            _baker.Bake();
            if (_baker.IsBaked)
            {
                // Posicionar en el inicio del path desde el frame 0
                _baker.Sample(0f, out var pos, out var nrm, out var fwd);
                transform.position = pos;
                transform.rotation = Quaternion.LookRotation(fwd, nrm);
            }
        }

        private void OnDestroy()
        {
            if (_lane == null) return;
            _lane.OnLaneChangeStarted   -= OnLaneStart;
            _lane.OnLaneChangeCompleted -= OnLaneEnd;
        }

        private void Update()
        {
            if (!_baker.IsBaked) return;
            ReadInput();
            _lane.Tick(Time.deltaTime);
            Advance(Time.deltaTime);
            SpinWheels(Time.deltaTime);
        }

        private void ReadInput()
        {
            if (Keyboard.current == null) return;
            // A/D manejado por LaneController directamente
            

            bool wHeld   = Keyboard.current.wKey.isPressed;
            float target = wHeld ? _boostSpeedKmh : _baseSpeedKmh;
            float rate   = wHeld ? _accelRate : _decelRate;
            _currentKmh  = Mathf.MoveTowards(_currentKmh, target, rate * Time.deltaTime);
        }

        private void Advance(float dt)
        {
            _distTraveled += (_currentKmh / 4f) * dt;
            _distTraveled  = Mathf.Min(_distTraveled, _baker.TotalLength);

            // Posición, normal y forward EXACTOS de la superficie del asfalto
            _baker.Sample(_distTraveled, out var basePos, out var normal, out var fwd);

            // Offset de carril: perpendicular al forward EN EL PLANO del asfalto
            Vector3 right  = Vector3.Cross(normal, fwd).normalized * -1f;
            float   laneX  = Mathf.Clamp(_lane.CurrentOffsetX, -3.4f, 3.4f);

            // Posición final — directa, sin lerp de posición (el path ya es suave)
            transform.position = basePos + right * laneX;

            // Rotación: forward del path + normal del asfalto como Up
            // → la moto se inclina automáticamente en rampas
            Quaternion targetRot = Quaternion.LookRotation(fwd, normal);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, targetRot, _rotLerpSpeed * dt);
        }

        private void SpinWheels(float dt)
        {
            float rpm = (_currentKmh / 3.6f) / (2f * Mathf.PI * 0.32f) * 360f;
            _wheelRot += rpm * dt;
            if (_wheelFront != null)
                _wheelFront.localEulerAngles = new Vector3(_wheelRot, 0f, 0f);
            if (_wheelRear != null)
                _wheelRear.localEulerAngles  = new Vector3(_wheelRot, 0f, 0f);
        }

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
    }
}
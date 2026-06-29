using System.Collections;
using UnityEngine;

#if NOESIS
using Noesis;
#endif

namespace NeoFastRider.UI
{
    // Setup (Screen Space — sin RenderTexture):
    //   1. Crea GO vacío hijo de [Scene_Setup], ej: "HUD_VisorOverlay"
    //   2. Add Component → Camera  (depth: 1, Clear Flags: Don't Clear, Culling Mask: Nothing)
    //   3. Add Component → NoesisView  (Xaml: HelmetVisorHUD, Texture: None)
    //   4. Add Component → HelmetVisorController  (asigna MotoForwardDriver)
    //   NoesisView renderiza vía OnPostRender de la overlay Camera — sin mallas ni RT.
    [AddComponentMenu("NeoFastRider/UI/Helmet Visor Controller")]
    public sealed class HelmetVisorController : MonoBehaviour
    {
        [Header("Moto")]
        [SerializeField] private NeoFastRider.Moto.MotoForwardDriver _motoPhysics;

        [Header("Nivel")]
        [SerializeField] private float _levelDuration = 120f;

        [Header("CoreEnergy")]
        [Range(0f, 1f)]
        [SerializeField] private float _initialCoreEnergy = 1f;

        [Header("Shake")]
        [SerializeField] private float _shakeMagnitude = 6f;
        [SerializeField] private float _shakeDuration  = 0.35f;
        [SerializeField] private float _shakeDecay     = 8f;

        private HelmetVisorViewModel _vm;
        private float _timeRemaining;
        private float _currentCoreEnergy;
        private float _targetCoreEnergy;
        private float _coreEnergyVelocity;
        private float _rpmSmoothed;
        private float _rpmVelocity;
        private bool  _shaking;

        private const float EnergySmoothTime = 0.25f;
        private const float RPMSmoothTime    = 0.15f;

        private void Awake()
        {
            _vm                = new HelmetVisorViewModel();
            _timeRemaining     = _levelDuration;
            _currentCoreEnergy = _initialCoreEnergy;
            _targetCoreEnergy  = _initialCoreEnergy;
            _vm.CoreEnergy     = _initialCoreEnergy;
            _vm.TimeRemaining  = _levelDuration;
        }

        private void Start()
        {
#if NOESIS
            // NoesisView.Awake() carga el XAML; Start() garantiza que Content ya existe.
            // Solo inyectamos el DataContext — NoesisView gestiona el render internamente.
            var noesisView = GetComponent<NoesisView>();
            if (noesisView != null && noesisView.Content != null)
            {
                noesisView.Content.DataContext = _vm;
                Debug.Log("[HelmetVisorController] DataContext inyectado.", this);
            }
            else
            {
                Debug.LogWarning("[HelmetVisorController] NoesisView o Content no disponibles.", this);
            }
#endif
        }

        private void Update()
        {
            UpdateTimer();
            UpdateSpeed();
            UpdateEnergy();
        }

        private void UpdateTimer()
        {
            if (_timeRemaining <= 0f) return;
            _timeRemaining -= Time.deltaTime;
            _vm.TimeRemaining = _timeRemaining;
        }

        private void UpdateSpeed()
        {
            if (_motoPhysics == null) return;
            float kmh    = _motoPhysics.CurrentKmh;
            _vm.SpeedKmh = kmh;

            float maxKmh  = _motoPhysics.BoostSpeedKmh;
            float rawRPM  = maxKmh > 0f ? Mathf.Clamp01(kmh / maxKmh) : 0f;
            _rpmSmoothed  = Mathf.SmoothDamp(_rpmSmoothed, rawRPM, ref _rpmVelocity, RPMSmoothTime);
            _vm.RPMNormalized = _rpmSmoothed;
        }

        private void UpdateEnergy()
        {
            _currentCoreEnergy = Mathf.SmoothDamp(
                _currentCoreEnergy, _targetCoreEnergy,
                ref _coreEnergyVelocity, EnergySmoothTime);
            _vm.CoreEnergy = _currentCoreEnergy;
        }

        public void SetCoreEnergy(float value)
        {
            _currentCoreEnergy = Mathf.Clamp01(value);
            _targetCoreEnergy  = _currentCoreEnergy;
            _vm.CoreEnergy     = _currentCoreEnergy;
        }

        public void ConsumeCoreEnergy(float fraction)
            => _targetCoreEnergy = Mathf.Clamp01(_targetCoreEnergy - fraction);

        public void OnObstacleHit(float energyDamage = 0.15f)
        {
            ConsumeCoreEnergy(energyDamage);
            TriggerShake();
        }

        public void TriggerShake()
        {
            if (_shaking) StopCoroutine(nameof(ShakeCoroutine));
            StartCoroutine(ShakeCoroutine());
        }

        private IEnumerator ShakeCoroutine()
        {
            _shaking = true;
            float elapsed = 0f;
            while (elapsed < _shakeDuration)
            {
                elapsed += Time.deltaTime;
                float decay      = 1f - Mathf.Clamp01(elapsed * _shakeDecay / _shakeDuration);
                float mag        = _shakeMagnitude * decay;
                _vm.ShakeOffsetX = (Random.value * 2f - 1f) * mag;
                _vm.ShakeOffsetY = (Random.value * 2f - 1f) * mag * 0.6f;
                yield return null;
            }
            _vm.ShakeOffsetX = 0.0;
            _vm.ShakeOffsetY = 0.0;
            _shaking = false;
        }

        public void ResetLevel()
        {
            _timeRemaining     = _levelDuration;
            _currentCoreEnergy = _initialCoreEnergy;
            _targetCoreEnergy  = _initialCoreEnergy;
            _rpmSmoothed       = 0f;
            _vm.TimeRemaining  = _levelDuration;
            _vm.CoreEnergy     = _initialCoreEnergy;
            _vm.SpeedKmh       = 0f;
            _vm.RPMNormalized  = 0.0;
        }
    }
}

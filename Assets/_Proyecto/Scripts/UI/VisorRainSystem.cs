using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NeoFastRider.UI
{
    /// <summary>
    /// Sensory Visor Rain Overlay System.
    /// Rain progressively increases overlay alpha (reducing visor visibility).
    /// Pressing 'C' (CleanVisor action) wipes the visor clean.
    /// Zero GC in Update: no allocations after Awake.
    /// </summary>
    public sealed class VisorRainSystem : MonoBehaviour
    {
        [Header("Visuals")]
        [SerializeField] private ParticleSystem _rainParticleSystem;
        [SerializeField] private CanvasGroup    _rainOverlayGroup;
        [SerializeField] private Image          _dirtyOverlay;

        [Header("Rain Settings")]
        [SerializeField] private float _accumulationRate = 0.04f;
        [SerializeField] private float _maxAlpha         = 0.65f;

        [Header("Clean Settings")]
        [SerializeField] private float _wipeSpeed = 3f;

        private NeoFastRider.Input.NeoFastRiderInput _input;
        private float _currentAlpha = 0f;
        private bool  _isWiping     = false;

        private void Awake()   => _input = new NeoFastRider.Input.NeoFastRiderInput();
        private void OnDestroy() => _input?.Dispose();

        private void OnEnable()
        {
            _input.Enable();
            _input.Gameplay.CleanVisor.performed += OnCleanPerformed;
        }

        private void OnDisable()
        {
            _input.Gameplay.CleanVisor.performed -= OnCleanPerformed;
            _input.Disable();
        }

        private void Update()
        {
            _currentAlpha = _isWiping
                ? Mathf.MoveTowards(_currentAlpha, 0f,       _wipeSpeed        * Time.deltaTime)
                : Mathf.MoveTowards(_currentAlpha, _maxAlpha, _accumulationRate * Time.deltaTime);

            if (_currentAlpha <= 0f) _isWiping = false;

            if (_rainOverlayGroup != null) _rainOverlayGroup.alpha = _currentAlpha;
            if (_dirtyOverlay     != null)
            {
                var c = _dirtyOverlay.color;
                c.a   = _currentAlpha;
                _dirtyOverlay.color = c;
            }
        }

        private void OnCleanPerformed(InputAction.CallbackContext ctx) => _isWiping = true;
    }
}
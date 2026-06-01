using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NeoFastRider.UI
{
    /// <summary>
    /// Screen-space rain particle overlay with progressive visor fouling.
    /// 'C' key (CleanVisor input action) wipes the visor overlay clean.
    /// </summary>
    public sealed class VisorRainOverlay : MonoBehaviour
    {
        [Header("Particle System")]
        [SerializeField] private ParticleSystem _rainParticles;

        [Header("Visor Dirt Overlay")]
        [SerializeField] private UnityEngine.UI.Image _visorDirt;

        [Header("Clean Prompt")]
        [SerializeField] private TMPro.TextMeshProUGUI _cleanPrompt;
        [SerializeField] private string _promptMsg = "PRESIONA 'C' PARA LIMPIAR VISERA";

        [Header("Settings")]
        [SerializeField] private float _fogRate      = 0.04f;
        [SerializeField, Range(0f, 0.6f)] private float _maxDirt = 0.45f;
        [SerializeField] private float _wipeDuration = 0.3f;

        private NeoFastRider.Input.NeoFastRiderInput _input;
        private float _dirt;
        private bool  _wiping;

        private void Awake()
        {
            _input = new NeoFastRider.Input.NeoFastRiderInput();
            SetDirt(0f);
        }

        private void OnEnable()
        {
            _input.Enable();
            _input.Gameplay.CleanVisor.performed += OnClean;
        }

        private void OnDisable()
        {
            _input.Gameplay.CleanVisor.performed -= OnClean;
            _input.Disable();
        }

        private void OnDestroy() => _input?.Dispose();

        private void Update()
        {
            if (_wiping) return;
            _dirt = Mathf.Min(_dirt + _fogRate * Time.deltaTime, _maxDirt);
            SetDirt(_dirt);
            if (_cleanPrompt != null)
                _cleanPrompt.gameObject.SetActive(_dirt > 0.1f);
        }

        private void OnClean(InputAction.CallbackContext ctx)
        {
            if (!_wiping) StartCoroutine(Wipe());
        }

        private IEnumerator Wipe()
        {
            _wiping = true;
            if (_rainParticles != null) { var em = _rainParticles.emission; em.enabled = false; }

            float start = _dirt, e = 0f;
            while (e < _wipeDuration)
            {
                _dirt = Mathf.Lerp(start, 0f, e / _wipeDuration);
                SetDirt(_dirt);
                e += Time.unscaledDeltaTime;
                yield return null;
            }
            _dirt = 0f; SetDirt(0f);
            if (_cleanPrompt != null) _cleanPrompt.gameObject.SetActive(false);

            yield return new WaitForSeconds(1.5f);
            if (_rainParticles != null) { var em = _rainParticles.emission; em.enabled = true; }
            _wiping = false;
        }

        private void SetDirt(float a)
        {
            if (_visorDirt == null) return;
            var c = _visorDirt.color; c.a = a; _visorDirt.color = c;
        }
    }
}

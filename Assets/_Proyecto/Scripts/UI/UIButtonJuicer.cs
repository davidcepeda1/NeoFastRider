using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

namespace NeoFastRider.UI
{
    /// <summary>
    /// Per-button hover juice: neon glow via TMP material instancing, hyphen
    /// text decoration, smooth scale pop, and SFX triggers.
    ///
    /// No Animator overhead. All animation driven by a single Update lerp.
    /// TMP material is instanced once in Awake so per-button glow never bleeds
    /// onto other text sharing the same font asset material.
    ///
    /// Zero heap allocations after Awake.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIButtonJuicer : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler
    {
        // ── Serialized Fields ─────────────────────────────────────────────────
        [Header("Text")]
        [Tooltip("Cached reference to the TextMeshProUGUI child of this button.")]
        [SerializeField] private TextMeshProUGUI _buttonText;

        [Header("Scale Animation")]
        [Tooltip("Uniform scale multiplier on hover (e.g. 1.04 = 4 % bigger).")]
        [SerializeField] private float _scaleMultiplier = 1.04f;

        [Tooltip("Lerp speed for scale animation. Higher = snappier.")]
        [SerializeField] private float _animationSpeed  = 14f;

        [Header("Glow")]
        [Tooltip("Neon glow colour applied to TMP material on hover.")]
        [SerializeField] private Color _glowColor       = new Color(0f, 0.941f, 1f, 1f); // #00F0FF
        [SerializeField, Range(0f, 1f)]  private float _glowOuter   = 0.25f;
        [SerializeField, Range(0f, 1f)]  private float _glowInner   = 0.05f;
        [SerializeField, Range(0.01f, 1f)] private float _glowPower = 0.35f;

        [Header("Audio")]
        [SerializeField] private AudioSource _fxAudioSource;
        [SerializeField] private AudioClip   _hoverSound;
        [SerializeField] private AudioClip   _clickSound;

        // ── Private State ─────────────────────────────────────────────────────
        private string   _originalText;
        private Vector3  _targetScale;
        private Vector3  _normalScale;
        private Vector3  _hoveredScale;
        private bool     _isHovered;

        /// <summary>
        /// Per-instance TMP material. Created once in Awake via <c>fontMaterial</c>
        /// getter (which forces material instantiation), then cached here.
        /// Never call <c>fontMaterial</c> again after Awake — that re-instantiates.
        /// </summary>
        private Material _instancedMaterial;

        // ── Unity Lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            _originalText = _buttonText != null ? _buttonText.text : string.Empty;
            _normalScale  = Vector3.one;
            _hoveredScale = Vector3.one * _scaleMultiplier;
            _targetScale  = _normalScale;

            // Force material instantiation now so every button owns its own copy.
            // fontMaterial getter auto-creates an instance if one doesn't exist.
            if (_buttonText != null)
            {
                _instancedMaterial = _buttonText.fontMaterial;
                // Make sure glow starts disabled on the fresh instance
                _instancedMaterial.DisableKeyword(ShaderUtilities.Keyword_Glow);
                _buttonText.UpdateMeshPadding();
            }
        }

        private void OnEnable()
        {
            // Guard against stale hover state when panels toggle
            if (_buttonText != null)
                _buttonText.text = _originalText;

            transform.localScale = _normalScale;
            _targetScale         = _normalScale;
            _isHovered           = false;

            // Ensure glow is off when re-enabled
            if (_instancedMaterial != null &&
                _instancedMaterial.IsKeywordEnabled(ShaderUtilities.Keyword_Glow))
            {
                _instancedMaterial.DisableKeyword(ShaderUtilities.Keyword_Glow);
                _buttonText?.UpdateMeshPadding();
            }
        }

        /// <summary>Single lerp toward target scale — zero allocations.</summary>
        private void Update()
        {
            transform.localScale = Vector3.Lerp(
                transform.localScale,
                _targetScale,
                _animationSpeed * Time.unscaledDeltaTime);
        }

        // ── Pointer Events ────────────────────────────────────────────────────

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_isHovered) return;
            _isHovered = true;

            // ── Text decoration ───────────────────────────────────────────────
            if (_buttonText != null)
                _buttonText.text = $"-{_originalText}-";

            // ── Scale ─────────────────────────────────────────────────────────
            _targetScale = _hoveredScale;

            // ── Neon glow ─────────────────────────────────────────────────────
            EnableGlow();

            // ── SFX ──────────────────────────────────────────────────────────
            if (_fxAudioSource != null && _hoverSound != null)
                _fxAudioSource.PlayOneShot(_hoverSound);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_isHovered) return;
            _isHovered = false;

            if (_buttonText != null)
                _buttonText.text = _originalText;

            _targetScale = _normalScale;

            DisableGlow();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_fxAudioSource != null && _clickSound != null)
                _fxAudioSource.PlayOneShot(_clickSound);
        }

        // ── Glow Helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Enables TMP GLOW_ON keyword on the per-instance material and writes
        /// all glow properties. Calls UpdateMeshPadding so vertices expand to
        /// accommodate the glow spread without clipping.
        /// </summary>
        private void EnableGlow()
        {
            if (_instancedMaterial == null || _buttonText == null) return;

            _instancedMaterial.EnableKeyword(ShaderUtilities.Keyword_Glow);
            _instancedMaterial.SetColor(ShaderUtilities.ID_GlowColor,  _glowColor);
            _instancedMaterial.SetFloat(ShaderUtilities.ID_GlowOuter,  _glowOuter);
            _instancedMaterial.SetFloat(ShaderUtilities.ID_GlowInner,  _glowInner);
            _instancedMaterial.SetFloat(ShaderUtilities.ID_GlowPower,  _glowPower);
            _instancedMaterial.SetFloat(ShaderUtilities.ID_GlowOffset, 0f);
            _buttonText.UpdateMeshPadding();
        }

        /// <summary>
        /// Disables TMP GLOW_ON keyword and restores mesh padding.
        /// </summary>
        private void DisableGlow()
        {
            if (_instancedMaterial == null || _buttonText == null) return;

            _instancedMaterial.DisableKeyword(ShaderUtilities.Keyword_Glow);
            _buttonText.UpdateMeshPadding();
        }
    }
}

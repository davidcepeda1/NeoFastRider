using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NeoFastRider.UI
{
    /// <summary>
    /// Visor Calibration Options Panel controller.
    ///
    /// Architecture: Transactional — slider/toggle changes are held in a
    /// temporary state buffer and only committed to PlayerPrefs when the
    /// player explicitly clicks ApplyChanges(). Closing without applying
    /// rolls back all temp values.
    ///
    /// Navigation uses smooth CanvasGroup alpha Lerp coroutines instead of
    /// SetActive toggles, preserving layout state across open/close cycles.
    ///
    /// Zero GC in all hot paths: no Update loop, event-driven only.
    /// </summary>
    public sealed class OptionsController : MonoBehaviour
    {
        // ── PlayerPrefs keys ──────────────────────────────────────────────────
        private const string KEY_BGM  = "opt_bgm_volume";
        private const string KEY_SFX  = "opt_sfx_volume";
        private const string KEY_POST = "opt_postprocess";

        // ── Neon colours ──────────────────────────────────────────────────────
        private static readonly Color NeonCyan    = new Color(0f,   1f,   1f,   1f);
        private static readonly Color NeonMagenta = new Color(1f,   0f,   1f,   1f);
        private static readonly Color FlashWhite  = new Color(1f,   1f,   1f,   1f);

        // ── Serialized References ─────────────────────────────────────────────
        [Header("Canvas Groups")]
        [Tooltip("CanvasGroup on UI_Button_Stack (or UI_Left_Panel_Container).")]
        [SerializeField] private CanvasGroup _menuButtonsCG;

        [Tooltip("CanvasGroup on Panel_Options (the overlay).")]
        [SerializeField] private CanvasGroup _optionsPanelCG;

        [Header("Sliders")]
        [SerializeField] private Slider _sliderBGM;
        [SerializeField] private Slider _sliderSFX;

        [Header("Toggle")]
        [SerializeField] private Toggle _togglePostProcess;

        [Header("Audio Sources (live preview on Apply)")]
        [SerializeField] private AudioSource _bgmSource;
        [SerializeField] private AudioSource _sfxSource;

        [Header("Flash Targets (Apply visual feedback)")]
        [Tooltip("Fill Image on Slider_BGM for the neon flash.")]
        [SerializeField] private Image _bgmFill;
        [Tooltip("Fill Image on Slider_SFX for the neon flash.")]
        [SerializeField] private Image _sfxFill;

        [Header("Feedback Text")]
        [SerializeField] private TextMeshProUGUI _applyFeedbackText;

        [Header("Timing")]
        [SerializeField, Range(0.1f, 0.8f)] private float _fadeDuration  = 0.35f;
        [SerializeField, Range(0.05f, 0.3f)] private float _flashDuration = 0.12f;

        // ── Private State ─────────────────────────────────────────────────────
        // Temporary (uncommitted) values — live in UI until Apply is pressed
        private float _tempBGM          = 1f;
        private float _tempSFX          = 1f;
        private bool  _tempPostProcess  = true;

        // Last-saved values — used for rollback on close without apply
        private float _savedBGM         = 1f;
        private float _savedSFX         = 1f;
        private bool  _savedPostProcess = true;

        private bool  _appliedThisSession = false;
        private bool  _transitioning      = false;

        // ── Unity Lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            // Guarantee hidden state on boot regardless of scene serialization
            SetPanelState(_optionsPanelCG, 0f, false);
            HideFeedback();
        }

        // ── Public API — wired to buttons via Inspector ───────────────────────

        /// <summary>
        /// Fades menu buttons out, fades options panel in, loads saved prefs.
        /// Called by Button_Opciones.OnClick().
        /// </summary>
        public void OpenOptionsPanel()
        {
            if (_transitioning) return;
            LoadSavedPrefs();
            PopulateUIFromSaved();
            _appliedThisSession = false;
            HideFeedback();
            StartCoroutine(FadeMenuToOptions());
        }

        /// <summary>
        /// Commits temporary slider/toggle values to PlayerPrefs permanently.
        /// Triggers neon flash feedback. Called by Button_ApplyChanges.OnClick().
        /// </summary>
        public void ApplyChanges()
        {
            // Snapshot current UI state into temp buffers
            _tempBGM         = _sliderBGM        != null ? _sliderBGM.value        : _tempBGM;
            _tempSFX         = _sliderSFX        != null ? _sliderSFX.value        : _tempSFX;
            _tempPostProcess = _togglePostProcess != null ? _togglePostProcess.isOn : _tempPostProcess;

            // Permanent write
            PlayerPrefs.SetFloat(KEY_BGM,  _tempBGM);
            PlayerPrefs.SetFloat(KEY_SFX,  _tempSFX);
            PlayerPrefs.SetInt  (KEY_POST, _tempPostProcess ? 1 : 0);
            PlayerPrefs.Save();

            // Persist to saved state
            _savedBGM         = _tempBGM;
            _savedSFX         = _tempSFX;
            _savedPostProcess = _tempPostProcess;
            _appliedThisSession = true;

            // Apply volume immediately to live audio
            if (_bgmSource != null) _bgmSource.volume = _savedBGM;
            if (_sfxSource != null) _sfxSource.volume = _savedSFX;

            // Neon flash feedback
            StartCoroutine(NeonFlashFeedback());
        }

        /// <summary>
        /// Fades options panel out, restores menu buttons. Rolls back if not applied.
        /// Called by Button_BackToMenu.OnClick().
        /// </summary>
        public void CloseOptionsPanel()
        {
            if (_transitioning) return;

            // Rollback: if player never applied, revert UI to saved state
            if (!_appliedThisSession)
                PopulateUIFromSaved();

            HideFeedback();
            StartCoroutine(FadeOptionsToMenu());
        }

        // ── Slider / Toggle change callbacks (TASK 4 rule: NO PlayerPrefs write) ──

        /// <summary>Stores BGM temp value only — no PlayerPrefs write.</summary>
        public void OnBGMChanged(float value)  => _tempBGM = value;

        /// <summary>Stores SFX temp value only — no PlayerPrefs write.</summary>
        public void OnSFXChanged(float value)  => _tempSFX = value;

        /// <summary>Stores PostProcess temp value only — no PlayerPrefs write.</summary>
        public void OnPostProcessChanged(bool value) => _tempPostProcess = value;

        // ── Private Coroutines ────────────────────────────────────────────────

        private IEnumerator FadeMenuToOptions()
        {
            _transitioning = true;
            // Parallel: menu buttons → 0, options panel → 1
            yield return FadeParallel(_menuButtonsCG, 1f, 0f,
                                      _optionsPanelCG,  0f, 1f,
                                      _fadeDuration);
            SetPanelState(_optionsPanelCG, 1f, true);
            SetGroupInteractable(_menuButtonsCG, false);
            _transitioning = false;
        }

        private IEnumerator FadeOptionsToMenu()
        {
            _transitioning = true;
            SetPanelState(_optionsPanelCG, 1f, false); // disable interaction immediately
            // Parallel: options panel → 0, menu buttons → 1
            yield return FadeParallel(_optionsPanelCG,  1f, 0f,
                                      _menuButtonsCG,   0f, 1f,
                                      _fadeDuration);
            SetGroupInteractable(_menuButtonsCG, true);
            _transitioning = false;
        }

        private IEnumerator FadeParallel(
            CanvasGroup groupA, float fromA, float toA,
            CanvasGroup groupB, float fromB, float toB,
            float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                if (groupA != null) groupA.alpha = Mathf.Lerp(fromA, toA, t);
                if (groupB != null) groupB.alpha = Mathf.Lerp(fromB, toB, t);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            if (groupA != null) groupA.alpha = toA;
            if (groupB != null) groupB.alpha = toB;
        }

        private IEnumerator NeonFlashFeedback()
        {
            // Show "GUARDADO" text
            if (_applyFeedbackText != null)
            {
                _applyFeedbackText.gameObject.SetActive(true);
                _applyFeedbackText.text  = "✓ MODIFICACIONES GRABADAS";
                _applyFeedbackText.color = NeonCyan;
            }

            // Flash the slider fill images white → cyan three times
            for (int i = 0; i < 3; i++)
            {
                SetFillColor(FlashWhite);
                yield return new WaitForSecondsRealtime(_flashDuration);
                SetFillColor(NeonCyan);
                yield return new WaitForSecondsRealtime(_flashDuration);
            }

            // Keep feedback visible briefly then fade
            yield return new WaitForSecondsRealtime(1.2f);
            HideFeedback();
        }

        // ── Private Helpers ───────────────────────────────────────────────────

        private void LoadSavedPrefs()
        {
            _savedBGM         = PlayerPrefs.GetFloat(KEY_BGM,  1f);
            _savedSFX         = PlayerPrefs.GetFloat(KEY_SFX,  1f);
            _savedPostProcess = PlayerPrefs.GetInt  (KEY_POST, 1) == 1;
        }

        private void PopulateUIFromSaved()
        {
            _tempBGM         = _savedBGM;
            _tempSFX         = _savedSFX;
            _tempPostProcess = _savedPostProcess;

            // SetValueWithoutNotify prevents triggering OnChanged callbacks
            _sliderBGM?       .SetValueWithoutNotify(_savedBGM);
            _sliderSFX?       .SetValueWithoutNotify(_savedSFX);
            _togglePostProcess?.SetIsOnWithoutNotify(_savedPostProcess);
        }

        private static void SetPanelState(CanvasGroup cg, float alpha, bool interactive)
        {
            if (cg == null) return;
            cg.alpha          = alpha;
            cg.interactable   = interactive;
            cg.blocksRaycasts = interactive;
        }

        private static void SetGroupInteractable(CanvasGroup cg, bool interactive)
        {
            if (cg == null) return;
            cg.interactable   = interactive;
            cg.blocksRaycasts = interactive;
        }

        private void SetFillColor(Color c)
        {
            if (_bgmFill != null) _bgmFill.color = c;
            if (_sfxFill != null) _sfxFill.color = c;
        }

        private void HideFeedback()
        {
            if (_applyFeedbackText != null)
                _applyFeedbackText.gameObject.SetActive(false);
        }
    }
}

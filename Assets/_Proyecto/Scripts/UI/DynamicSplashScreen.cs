using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace NeoFastRider.UI
{
    /// <summary>
    /// Cinematic Splash Screen controller — single-panel color-interpolation approach.
    ///
    /// Architecture: One full-screen <see cref="Image"/> component whose <c>.color</c>
    /// is lerped directly between <see cref="Color.black"/> and <see cref="Color.white"/>.
    /// No stacked overlay panels, no CanvasGroup alpha compositing for the background.
    /// This eliminates the multi-overlay rendering conflict of the previous implementation.
    ///
    /// Flow: Black → White → [Juz Studios Logo] → [Unity Logo] → Black → MainMenu
    ///
    /// Optimization notes:
    ///   • Single continuous coroutine — no nested StartCoroutine chains.
    ///   • All waits use <see cref="Time.unscaledDeltaTime"/> — pause-safe.
    ///   • Zero GC allocations in any timed loop.
    ///   • Input via New Input System value polling — no delegate subscription overhead.
    /// </summary>
    public sealed class DynamicSplashScreen : MonoBehaviour
    {
        // ── Serialized References ────────────────────────────────────────────────
        [Header("Background")]
        [Tooltip("Single full-screen Image. Its .color is lerped black↔white.")]
        [SerializeField] private Image _backgroundImage;

        [Header("Logos")]
        [Tooltip("CanvasGroup on the Juz Studios logo for alpha control.")]
        [SerializeField] private CanvasGroup _juzStudioCanvasGroup;

        [Tooltip("RectTransform of the Juz Studios logo for scale ease-in.")]
        [SerializeField] private RectTransform _juzStudioRect;

        [Tooltip("CanvasGroup on the Unity logo for alpha control.")]
        [SerializeField] private CanvasGroup _unityCanvasGroup;

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip   _splashStingerClip;

        [Header("Scene Routing")]
        [SerializeField] private string _mainMenuSceneName = "Scene_MainMenu";

        // ── Timing constants (tuned for a 10-second waveform) ────────────────────
        private const float BackgroundFadeDuration = 1.0f;
        private const float LogoFadeDuration       = 0.8f;
        private const float LogoDisplayDuration    = 2.2f;
        private const float CrossfadeBuffer        = 0.3f;
        private const float SkipFadeDuration       = 0.15f;

        private static readonly Vector3 LogoStartScale = new Vector3(1.1f, 1.1f, 1.1f);
        private static readonly Vector3 LogoEndScale   = Vector3.one;

        // ── Runtime state ────────────────────────────────────────────────────────
        private AsyncOperation _menuLoadOp;
        private Coroutine      _mainSequence;
        private bool           _skipRequested;
        private bool           _sequenceDone;

        // ── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Awake()
        {
            // Background starts fully black
            if (_backgroundImage != null)
                _backgroundImage.color = Color.black;

            // Logos hidden
            SetAlpha(_juzStudioCanvasGroup, 0f);
            SetAlpha(_unityCanvasGroup,     0f);

            // Juz Studios logo at start scale
            if (_juzStudioRect != null)
                _juzStudioRect.localScale = LogoStartScale;

            // Configure audio source
            if (_audioSource != null && _splashStingerClip != null)
            {
                _audioSource.clip        = _splashStingerClip;
                _audioSource.loop        = false;
                _audioSource.playOnAwake = false;
                _audioSource.volume      = 1f;
            }
        }

        private void Start()
        {
            // Pre-load main menu asynchronously; hold at 90% until we allow activation
            _menuLoadOp = SceneManager.LoadSceneAsync(_mainMenuSceneName);
            if (_menuLoadOp != null)
                _menuLoadOp.allowSceneActivation = false;

            _audioSource?.Play();
            _mainSequence = StartCoroutine(ExecuteSplashSequence());
        }

        private void Update()
        {
            if (_sequenceDone || _skipRequested) return;

            bool skip =
                (Keyboard.current != null &&
                    (Keyboard.current.spaceKey.wasPressedThisFrame ||
                     Keyboard.current.enterKey.wasPressedThisFrame)) ||
                (Mouse.current != null &&
                     Mouse.current.leftButton.wasPressedThisFrame);

            if (skip)
            {
                _skipRequested = true;
                StartCoroutine(MuteAndSkip());
            }
        }

        // ── Main Sequence ────────────────────────────────────────────────────────

        /// <summary>
        /// Single continuous coroutine driving the full cinematic sequence.
        /// No nested coroutine chains — all phases inline.
        /// </summary>
        private IEnumerator ExecuteSplashSequence()
        {
            // Phase 0 ── Fade background black → white
            yield return LerpBackgroundColor(Color.black, Color.white, BackgroundFadeDuration);
            if (_skipRequested) yield break;

            // Phase 1 ── Juz Studios: fade in with scale ease
            yield return FadeLogoIn(_juzStudioCanvasGroup, _juzStudioRect, LogoFadeDuration);
            if (_skipRequested) yield break;

            yield return WaitUnlessSkipped(LogoDisplayDuration);
            if (_skipRequested) yield break;

            yield return LerpCanvasGroupAlpha(_juzStudioCanvasGroup, 1f, 0f, LogoFadeDuration);
            if (_skipRequested) yield break;

            yield return WaitUnlessSkipped(CrossfadeBuffer);
            if (_skipRequested) yield break;

            // Phase 2 ── Unity logo: simple fade in/out
            yield return LerpCanvasGroupAlpha(_unityCanvasGroup, 0f, 1f, LogoFadeDuration);
            if (_skipRequested) yield break;

            yield return WaitUnlessSkipped(LogoDisplayDuration);
            if (_skipRequested) yield break;

            yield return LerpCanvasGroupAlpha(_unityCanvasGroup, 1f, 0f, LogoFadeDuration);
            if (_skipRequested) yield break;

            // Phase 3 ── Fade background white → black, duck audio simultaneously
            yield return FadeToBlackWithAudio();

            ActivateMainMenu();
        }

        // ── Coroutine Helpers (reusable, allocation-free) ───────────────────────

        /// <summary>Lerps <see cref="Image.color"/> between two colors using SmoothStep.</summary>
        private IEnumerator LerpBackgroundColor(Color from, Color to, float duration)
        {
            if (_backgroundImage == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                _backgroundImage.color = Color.Lerp(from, to, t);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            _backgroundImage.color = to;
        }

        /// <summary>Lerps a CanvasGroup's alpha using SmoothStep.</summary>
        private IEnumerator LerpCanvasGroupAlpha(CanvasGroup group, float from, float to, float duration)
        {
            if (group == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                group.alpha = Mathf.Lerp(from, to, t);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            group.alpha = to;
        }

        /// <summary>
        /// Fades the logo in with simultaneous scale ease (1.1 → 1.0).
        /// </summary>
        private IEnumerator FadeLogoIn(CanvasGroup group, RectTransform rect, float duration)
        {
            if (group == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t     = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                group.alpha = t;
                if (rect != null)
                    rect.localScale = Vector3.Lerp(LogoStartScale, LogoEndScale, t);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            group.alpha = 1f;
            if (rect != null)
                rect.localScale = LogoEndScale;
        }

        /// <summary>
        /// Simultaneously fades background white→black and ducks audio volume to 0.
        /// </summary>
        private IEnumerator FadeToBlackWithAudio()
        {
            float elapsed   = 0f;
            float startVol  = _audioSource != null ? _audioSource.volume : 1f;

            while (elapsed < BackgroundFadeDuration)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / BackgroundFadeDuration);
                if (_backgroundImage != null)
                    _backgroundImage.color = Color.Lerp(Color.white, Color.black, t);
                if (_audioSource != null)
                    _audioSource.volume = Mathf.Lerp(startVol, 0f, t);

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (_backgroundImage != null) _backgroundImage.color = Color.black;
            if (_audioSource != null)     _audioSource.volume    = 0f;
        }

        /// <summary>
        /// Yields every frame for <paramref name="seconds"/> or until skip is requested.
        /// </summary>
        private IEnumerator WaitUnlessSkipped(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds && !_skipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // ── Skip Path ────────────────────────────────────────────────────────────

        /// <summary>
        /// Graceful skip: simultaneously fades audio and background to black in 0.15 s,
        /// then activates the main menu. Never cuts audio abruptly.
        /// </summary>
        private IEnumerator MuteAndSkip()
        {
            if (_mainSequence != null)
            {
                StopCoroutine(_mainSequence);
                _mainSequence = null;
            }

            float elapsed  = 0f;
            float startVol = _audioSource != null ? _audioSource.volume : 0f;
            Color startCol = _backgroundImage != null ? _backgroundImage.color : Color.black;

            while (elapsed < SkipFadeDuration)
            {
                float t = elapsed / SkipFadeDuration;
                if (_audioSource    != null) _audioSource.volume    = Mathf.Lerp(startVol, 0f, t);
                if (_backgroundImage != null) _backgroundImage.color = Color.Lerp(startCol, Color.black, t);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (_audioSource    != null) { _audioSource.volume = 0f; _audioSource.Stop(); }
            if (_backgroundImage != null)  _backgroundImage.color = Color.black;

            ActivateMainMenu();
        }

        // ── Scene Activation ─────────────────────────────────────────────────────

        private void ActivateMainMenu()
        {
            if (_sequenceDone) return;
            _sequenceDone = true;

            if (_menuLoadOp != null)
                _menuLoadOp.allowSceneActivation = true;
            else
                SceneManager.LoadScene(_mainMenuSceneName);
        }

        // ── Utility ──────────────────────────────────────────────────────────────
        private static void SetAlpha(CanvasGroup group, float alpha)
        {
            if (group != null) group.alpha = alpha;
        }
    }
}

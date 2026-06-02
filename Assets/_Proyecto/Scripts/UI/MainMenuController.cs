using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeoFastRider.UI
{
    /// <summary>
    /// Main Menu navigation controller for Neo Fast Rider.
    ///
    /// Responsibilities (SRP):
    ///   • Asynchronous gameplay scene loading with audio duck transition.
    ///   • Sub-panel visibility toggling (Level Select / Options).
    ///   • Application quit.
    ///
    /// Zero GC in all hot paths: coroutine reuse, cached component refs, no per-frame allocs.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        // ── Serialized Fields ─────────────────────────────────────────────────────
        [Header("Audio")]
        [Tooltip("AudioSource playing the main menu BGM to be ducked on transition.")]
        [SerializeField] private AudioSource _menuAudioSource;

        [Header("Scene Routing")]
        [SerializeField] private string _gameplaySceneName = "Scene_Gameplay";

        [Tooltip("Duration in seconds to smoothly duck BGM volume before scene switch.")]
        [SerializeField] private float _sceneTransitionFadeDuration = 0.5f;

        [Header("Sub-Panels")]
        [SerializeField] private GameObject _levelSelectPanel;
        [SerializeField] private GameObject _optionsPanel;

        // ── Private State ─────────────────────────────────────────────────────────
        private AsyncOperation _loadOp;
        private bool           _transitioning;

        // ── Unity Lifecycle ───────────────────────────────────────────────────────
        private void Start()
        {
            // Ensure sub-panels start hidden
            SetPanelActive(_levelSelectPanel, false);
            // Panel_Options visibility is managed exclusively by OptionsController via CanvasGroup.
        }

        // ── Public API (bound to UI buttons via Inspector) ────────────────────────

        /// <summary>
        /// Initiates asynchronous load of the gameplay scene while smoothly ducking
        /// the menu BGM. Safe to call from a UI Button OnClick event.
        /// </summary>
        public void RunGame()
        {
            if (_transitioning) return;
            _transitioning = true;
            StartCoroutine(TransitionToGameplay());
        }

        /// <summary>Opens or closes the Level Selection sub-panel.</summary>
        /// <param name="active">True to show, false to hide.</param>
        public void ToggleLevelSelect(bool active)
        {
            SetPanelActive(_levelSelectPanel, active);
        }

        /// <summary>Opens or closes the Options sub-panel.</summary>
        /// <param name="active">True to show, false to hide.</param>
        public void ToggleOptions(bool active)
        {
            SetPanelActive(_optionsPanel, active);
        }

        /// <summary>Gracefully exits the application.</summary>
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ── Private Coroutines ────────────────────────────────────────────────────

        /// <summary>
        /// Ducks BGM to silence over <see cref="_sceneTransitionFadeDuration"/> seconds,
        /// then allows the pre-loaded scene to activate.
        /// Uses unscaledDeltaTime — immune to Time.timeScale changes.
        /// </summary>
        private IEnumerator TransitionToGameplay()
        {
            // Start async load immediately; hold at 90% until audio fade completes
            _loadOp = SceneManager.LoadSceneAsync(_gameplaySceneName);
            if (_loadOp != null)
                _loadOp.allowSceneActivation = false;

            // Duck BGM
            if (_menuAudioSource != null && _menuAudioSource.isPlaying)
            {
                float elapsed   = 0f;
                float startVol  = _menuAudioSource.volume;

                while (elapsed < _sceneTransitionFadeDuration)
                {
                    float t = Mathf.SmoothStep(0f, 1f, elapsed / _sceneTransitionFadeDuration);
                    _menuAudioSource.volume = Mathf.Lerp(startVol, 0f, t);
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
                _menuAudioSource.volume = 0f;
            }

            // Allow scene activation
            if (_loadOp != null)
                _loadOp.allowSceneActivation = true;
            else
                SceneManager.LoadScene(_gameplaySceneName);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────
        private static void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null) panel.SetActive(active);
        }
    }
}

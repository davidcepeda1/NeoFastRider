using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeoFastRider.UI
{
    /// <summary>
    /// Decoupled navigation controller for Scene_PauseMenu.
    ///
    /// Reads the last active gameplay scene from PlayerPrefs so it can
    /// return the player to exactly where they left off.
    ///
    /// PlayerPrefs key: "last_gameplay_scene" (string — scene name)
    ///                  "last_gameplay_index" (int    — build index fallback)
    ///
    /// Zero GC: no per-frame allocations; all logic is event-driven.
    /// </summary>
    public sealed class PauseSceneController : MonoBehaviour
    {
        // ── PlayerPrefs keys (shared with gameplay scenes) ────────────────────
        public const string KEY_LAST_SCENE = "last_gameplay_scene";
        public const string KEY_LAST_INDEX = "last_gameplay_index";

        // ── Default fallback if no prefs exist ────────────────────────────────
        private const string MAIN_MENU_SCENE = "Scene_MainMenu";
        private const int    MAIN_MENU_INDEX = 0;

        // ── Serialized Fields ─────────────────────────────────────────────────
        [Header("Transition")]
        [Tooltip("Duration in seconds for any optional fade-out before scene load.")]
        [SerializeField] private float _transitionDelay = 0.1f;

        // ── Hover text state (used by button event hooks) ─────────────────────
        [Header("Button Text References")]
        [Tooltip("TextMeshProUGUI on Button_Resume for hover bracket swap.")]
        [SerializeField] private TMPro.TextMeshProUGUI _resumeText;

        [Tooltip("TextMeshProUGUI on Button_Restart for hover warning swap.")]
        [SerializeField] private TMPro.TextMeshProUGUI _restartText;

        [Tooltip("TextMeshProUGUI on Button_Quit for hover tone shift.")]
        [SerializeField] private TMPro.TextMeshProUGUI _quitText;

        // ── Original labels (cached in Awake, never reallocated) ──────────────
        private string _resumeOriginal;
        private string _restartOriginal;
        private string _quitOriginal;

        private void Awake()
        {
            _resumeOriginal  = _resumeText  != null ? _resumeText.text  : string.Empty;
            _restartOriginal = _restartText != null ? _restartText.text : string.Empty;
            _quitOriginal    = _quitText    != null ? _quitText.text    : string.Empty;
        }

        // ═════════════════════════════════════════════════════════════════════
        // ── SECTION 4: Navigation Methods ────────────────────────────────────
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns to the last active gameplay scene stored in PlayerPrefs.
        /// Falls back to Scene_MainMenu if no record exists.
        /// </summary>
        public void ResumeGame()
        {
            string lastScene = PlayerPrefs.GetString(KEY_LAST_SCENE, string.Empty);

            if (!string.IsNullOrEmpty(lastScene))
                StartCoroutine(LoadAsync(lastScene));
            else
            {
                int lastIndex = PlayerPrefs.GetInt(KEY_LAST_INDEX, MAIN_MENU_INDEX);
                StartCoroutine(LoadAsync(lastIndex));
            }
        }

        /// <summary>
        /// Reloads the last active gameplay scene from scratch (restart from checkpoint logic
        /// is handled by TutorialManager / level managers in the destination scene).
        /// </summary>
        public void RestartGameplayLevel()
        {
            string lastScene = PlayerPrefs.GetString(KEY_LAST_SCENE, string.Empty);

            if (!string.IsNullOrEmpty(lastScene))
                StartCoroutine(LoadAsync(lastScene));
            else
                StartCoroutine(LoadAsync(PlayerPrefs.GetInt(KEY_LAST_INDEX, MAIN_MENU_INDEX)));
        }

        /// <summary>
        /// Drops to Scene_MainMenu (build index 0) unconditionally.
        /// </summary>
        public void ExitToMainMenu()
        {
            StartCoroutine(LoadAsync(MAIN_MENU_INDEX));
        }

        // ═════════════════════════════════════════════════════════════════════
        // ── SECTION 3: Hover Event Hooks ─────────────────────────────────────
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>Called by Button_Resume EventTrigger PointerEnter.</summary>
        public void OnResumeHoverEnter()
        {
            if (_resumeText != null)
                _resumeText.text = "[ RECONECTAR ENLACE NEURONAL ]";
        }

        /// <summary>Called by Button_Resume EventTrigger PointerExit.</summary>
        public void OnResumeHoverExit()
        {
            if (_resumeText != null)
                _resumeText.text = _resumeOriginal;
        }

        /// <summary>Called by Button_Restart EventTrigger PointerEnter.</summary>
        public void OnRestartHoverEnter()
        {
            if (_restartText != null)
                _restartText.text = "¿REINTENTAR DESDE EL ÚLTIMO CHECKPOINT?";
        }

        /// <summary>Called by Button_Restart EventTrigger PointerExit.</summary>
        public void OnRestartHoverExit()
        {
            if (_restartText != null)
                _restartText.text = _restartOriginal;
        }

        /// <summary>Called by Button_Quit EventTrigger PointerEnter — subtle tone shift via color.</summary>
        public void OnQuitHoverEnter()
        {
            if (_quitText != null)
                _quitText.color = new Color(1f, 0.4f, 0.4f, 1f); // brighter red on hover
        }

        /// <summary>Called by Button_Quit EventTrigger PointerExit.</summary>
        public void OnQuitHoverExit()
        {
            if (_quitText != null)
                _quitText.color = new Color(1f, 0f, 0f, 1f); // restore hostile red
        }

        // ═════════════════════════════════════════════════════════════════════
        // ── Private Async Loaders ─────────────────────────────────────────────
        // ═════════════════════════════════════════════════════════════════════

        private IEnumerator LoadAsync(string sceneName)
        {
            if (_transitionDelay > 0f)
                yield return new WaitForSecondsRealtime(_transitionDelay);
            yield return SceneManager.LoadSceneAsync(sceneName);
        }

        private IEnumerator LoadAsync(int buildIndex)
        {
            if (_transitionDelay > 0f)
                yield return new WaitForSecondsRealtime(_transitionDelay);
            yield return SceneManager.LoadSceneAsync(buildIndex);
        }
    }
}

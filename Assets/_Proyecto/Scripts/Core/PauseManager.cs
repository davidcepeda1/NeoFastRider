using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using NeoFastRider.UI;

namespace NeoFastRider.Core
{
    /// <summary>
    /// Menú de pausa para niveles con meta (Scene_Level_01 y sucesores — el tutorial usa su
    /// propio TutorialManager, que a propósito NO tiene pausa). ESC alterna el estado; pausar
    /// solo detiene el tiempo (Time.timeScale = 0), así que toda la física/movimiento existente
    /// se congela sola sin tener que tocar MotoForwardDriver ni nada más.
    /// </summary>
    public sealed class PauseManager : MonoBehaviour
    {
        public static PauseManager Instance { get; private set; }

        [Header("UI")]
        [SerializeField] private GameObject _pausePanel;
        [Tooltip("Imagen negra a pantalla completa (alpha 0 en reposo) usada para el fundido antes de cambiar de escena.")]
        [SerializeField] private Image _fadeOverlay;
        [SerializeField] private float _fadeDuration = 0.6f;
        [Tooltip("El HUD se renderiza con NoesisGUI (cámara/pipeline propio), por fuera del Canvas de uGUI — el overlay de fundido NO lo tapa aunque esté 'encima'. Se necesita esta referencia para desvanecer el HUD en paralelo por su propio sistema de opacidad.")]
        [SerializeField] private HelmetVisorController _helmetVisor;

        [Header("Escenas")]
        [SerializeField] private string _mainMenuSceneName = "Scene_MainMenu";

        public bool IsPaused { get; private set; }

        private bool _transitioning;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            SetPausedVisual(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f; // por si el objeto se destruye estando en pausa (cambio de escena, etc.)
        }

        private void Update()
        {
            if (!_transitioning && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                TogglePause();
        }

        public void TogglePause()
        {
            if (IsPaused) Resume();
            else          Pause();
        }

        public void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;
            Time.timeScale = 0f;
            SetPausedVisual(true);
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = 1f;
            SetPausedVisual(false);
        }

        /// <summary>Reinicia el nivel actual desde cero (recarga la escena).</summary>
        public void RestartLevel()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        /// <summary>Vuelve al menú principal, con un fundido a negro antes de cambiar de escena.</summary>
        public void GoToMainMenu()
        {
            if (_transitioning) return;
            StartCoroutine(FadeAndLoadScene(_mainMenuSceneName));
        }

        private IEnumerator FadeAndLoadScene(string sceneName)
        {
            _transitioning = true;
            yield return Fade(0f, 1f, _fadeDuration);

            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        }

        /// <summary>
        /// Anima el alfa de _fadeOverlay con tiempo NO escalado — sigue funcionando aunque
        /// Time.timeScale esté en 0 por la pausa. En paralelo, desvanece la opacidad del HUD
        /// de NoesisGUI (1 - alfa del overlay), ya que ese HUD no queda tapado por el overlay
        /// de uGUI sin importar el orden de dibujado configurado.
        /// </summary>
        private IEnumerator Fade(float from, float to, float duration)
        {
            if (_fadeOverlay == null) yield break;

            _fadeOverlay.gameObject.SetActive(true);
            Color c = _fadeOverlay.color;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float alpha = Mathf.Lerp(from, to, elapsed / duration);
                c.a = alpha;
                _fadeOverlay.color = c;
                if (_helmetVisor != null) _helmetVisor.SetHudOpacity(1f - alpha);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            c.a = to;
            _fadeOverlay.color = c;
            if (_helmetVisor != null) _helmetVisor.SetHudOpacity(1f - to);
        }

        private void SetPausedVisual(bool paused)
        {
            if (_pausePanel != null) _pausePanel.SetActive(paused);
        }
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using NeoFastRider.Moto;
using NeoFastRider.UI;

namespace NeoFastRider.Core
{
    /// <summary>
    /// Pantalla de game over para niveles con meta (Scene_Level_01 y sucesores). Se dispara por
    /// cualquiera de dos causas — <see cref="PlayerHealth.OnPlayerDeath"/> (núcleo destruido) o
    /// <see cref="LevelGoalManager.OnTimeUp"/> (tiempo agotado) — y congela el juego de forma
    /// permanente (a diferencia de PauseManager, acá no hay "Continuar": es un estado terminal).
    /// </summary>
    public sealed class GameOverManager : MonoBehaviour
    {
        [Header("Referencias de gameplay")]
        [SerializeField] private PlayerHealth     _playerHealth;
        [SerializeField] private LevelGoalManager _levelGoal;

        [Header("UI")]
        [SerializeField] private GameObject _gameOverPanel;
        [SerializeField] private TextMeshProUGUI _reasonText;
        [SerializeField] private Image _fadeOverlay;
        [SerializeField] private float _fadeDuration = 0.6f;
        [Tooltip("Igual que en PauseManager: el HUD de NoesisGUI no queda tapado por un overlay de uGUI, hay que desvanecerlo por su propio sistema de opacidad.")]
        [SerializeField] private HelmetVisorController _helmetVisor;

        [Header("Mensajes")]
        [SerializeField] private string _reasonDeath  = "NÚCLEO DESTRUIDO";
        [SerializeField] private string _reasonTimeUp = "TIEMPO AGOTADO";

        [Header("Escenas")]
        [SerializeField] private string _mainMenuSceneName = "Scene_MainMenu";

        public bool HasEnded { get; private set; }

        private void Awake()
        {
            if (_gameOverPanel != null) _gameOverPanel.SetActive(false);

            if (_playerHealth != null) _playerHealth.OnPlayerDeath += HandlePlayerDeath;
            if (_levelGoal    != null) _levelGoal.OnTimeUp         += HandleTimeUp;
        }

        private void OnDestroy()
        {
            if (_playerHealth != null) _playerHealth.OnPlayerDeath -= HandlePlayerDeath;
            if (_levelGoal    != null) _levelGoal.OnTimeUp         -= HandleTimeUp;
            Time.timeScale = 1f; // por si el objeto se destruye en medio del estado de game over
        }

        private void HandlePlayerDeath() => TriggerGameOver(_reasonDeath);
        private void HandleTimeUp()      => TriggerGameOver(_reasonTimeUp);

        private void TriggerGameOver(string reason)
        {
            if (HasEnded) return;
            HasEnded = true;

            Time.timeScale = 0f;
            if (_reasonText != null) _reasonText.text = reason;
            if (_gameOverPanel != null) _gameOverPanel.SetActive(true);
            // El HUD ya no aporta nada en esta pantalla, se oculta directo — SIN tocar
            // _fadeOverlay acá: ese overlay es solo para la transición de salida (GoToMainMenu),
            // llevarlo a alfa=1 en la revelación tapaba el panel recién mostrado y dejaba todo
            // en negro (el bug reportado: "aparece pero desaparece, se queda en negro").
            if (_helmetVisor != null) _helmetVisor.SetHudOpacity(0f);
        }

        /// <summary>Reinicia el nivel actual desde cero.</summary>
        public void RestartLevel()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        /// <summary>Vuelve al menú principal, con un fundido a negro antes de cambiar de escena.</summary>
        public void GoToMainMenu()
        {
            StartCoroutine(FadeAndLoadScene(_mainMenuSceneName));
        }

        private IEnumerator FadeAndLoadScene(string sceneName)
        {
            yield return Fade(_fadeOverlay != null ? _fadeOverlay.color.a : 1f, 1f, _fadeDuration);
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        }

        /// <summary>Anima el alfa de _fadeOverlay y la opacidad del HUD con tiempo NO escalado — funciona aunque timeScale esté en 0.</summary>
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
    }
}

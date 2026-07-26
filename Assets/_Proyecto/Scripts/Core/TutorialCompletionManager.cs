using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using NeoFastRider.Enemies;

namespace NeoFastRider.Core
{
    /// <summary>
    /// Exclusivo de Scene_TutorialLevel: cuando se destruye el último dron,
    /// oscurece la pantalla y muestra "TUTORIAL COMPLETADO" con un botón
    /// para volver al menú principal.
    /// </summary>
    public sealed class TutorialCompletionManager : MonoBehaviour
    {
        [Header("Pantalla de finalización")]
        [SerializeField] private CanvasGroup _screenGroup;
        [SerializeField] private Button      _menuButton;
        [SerializeField] private float       _fadeDuration = 0.6f;

        [Header("Escena destino")]
        [SerializeField] private string _mainMenuSceneName = "Scene_MainMenu";

        private int  _dronesRemaining;
        private bool _completed;

        private void Awake()
        {
            if (_screenGroup != null)
            {
                _screenGroup.alpha          = 0f;
                _screenGroup.interactable   = false;
                _screenGroup.blocksRaycasts = false;
                _screenGroup.gameObject.SetActive(false);
            }
            if (_menuButton != null)
                _menuButton.onClick.AddListener(GoToMainMenu);
        }

        private void Start()
        {
            _dronesRemaining = FindObjectsByType<DroneCollisionHandler>(FindObjectsSortMode.None).Length;
            DroneCollisionHandler.OnAnyDroneDestroyed += HandleDroneDestroyed;
        }

        private void OnDestroy()
        {
            DroneCollisionHandler.OnAnyDroneDestroyed -= HandleDroneDestroyed;
        }

        private void HandleDroneDestroyed()
        {
            if (_completed) return;
            _dronesRemaining--;
            if (_dronesRemaining <= 0) StartCoroutine(ShowCompletionScreen());
        }

        private IEnumerator ShowCompletionScreen()
        {
            _completed = true;

            if (_screenGroup != null)
            {
                _screenGroup.gameObject.SetActive(true);
                float e = 0f;
                while (e < _fadeDuration)
                {
                    e += Time.unscaledDeltaTime;
                    _screenGroup.alpha = Mathf.Clamp01(e / _fadeDuration);
                    yield return null;
                }
                _screenGroup.alpha          = 1f;
                _screenGroup.interactable   = true;
                _screenGroup.blocksRaycasts = true;
            }

            Time.timeScale = 0f;
        }

        private void GoToMainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(_mainMenuSceneName);
        }
    }
}

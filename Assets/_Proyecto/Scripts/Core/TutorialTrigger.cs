using UnityEngine;
using UnityEngine.InputSystem;
using NeoFastRider.UI;

namespace NeoFastRider.Core
{
    /// <summary>
    /// Zona de pista no bloqueante: al entrar el jugador, muestra un mensaje
    /// educativo en el visor (HelmetVisorController) sin pausar el juego ni
    /// el tiempo, respetando la regla NO PAUSE SYSTEM de TutorialManager.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class TutorialTrigger : MonoBehaviour
    {
        [Header("Mensaje")]
        [SerializeField, TextArea] private string _message = string.Empty;

        [Header("Cierre del prompt")]
        [SerializeField] private Key   _dismissKey    = Key.None;
        [SerializeField] private float _autoHideDelay = 4f;
        [SerializeField] private bool  _triggerOnce   = true;

        private bool _fired;
        private bool _active;
        private HelmetVisorController _visor;

        private void Awake()
        {
            var col = GetComponent<BoxCollider>();
            col.isTrigger = true;
            _visor = FindFirstObjectByType<HelmetVisorController>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_fired && _triggerOnce) return;
            if (!other.CompareTag("Player")) return;

            _fired  = true;
            _active = true;
            _visor?.ShowTutorialPrompt(_message);

            CancelInvoke(nameof(HidePrompt));
            if (_autoHideDelay > 0f) Invoke(nameof(HidePrompt), _autoHideDelay);
        }

        private void Update()
        {
            if (!_active || _dismissKey == Key.None || Keyboard.current == null) return;
            if (Keyboard.current[_dismissKey].wasPressedThisFrame) HidePrompt();
        }

        private void HidePrompt()
        {
            if (!_active) return;
            _active = false;
            _visor?.HideTutorialPrompt();
        }
    }
}

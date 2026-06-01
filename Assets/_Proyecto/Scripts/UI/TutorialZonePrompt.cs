using UnityEngine;
using TMPro;

namespace NeoFastRider.UI
{
    public sealed class TutorialZonePrompt : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _promptText;
        [SerializeField] private float _fadeSpeed = 3f;

        private static readonly string[] ZoneMessages = new string[]
        {
            "MOTO SE MUEVE AUTOMATICAMENTE.\nMANTEN 'W' PARA ACELERAR AL MAXIMO (120 km/h)",
            "OBSTACULO ADELANTE.\nUSA 'A/D' PARA CAMBIAR AL CARRIL LIBRE",
            "BARRERA IMPASABLE.\nPRESIONA 'ESPACIO' PARA DISPARAR TU CANON DE PULSO"
        };

        private float _targetAlpha = 0f;
        private int   _currentZone = -1;

        private void Awake() { if (_promptText != null) _promptText.alpha = 0f; }

        private void Update()
        {
            if (_promptText == null) return;
            _promptText.alpha = Mathf.MoveTowards(
                _promptText.alpha, _targetAlpha, _fadeSpeed * Time.deltaTime);
        }

        public void ShowZone(int zone)
        {
            if (zone == _currentZone || _promptText == null) return;
            _currentZone = zone;
            if (zone >= 0 && zone < ZoneMessages.Length)
            {
                _promptText.text = ZoneMessages[zone];
                _targetAlpha     = 1f;
            }
            else _targetAlpha = 0f;
        }

        public void HidePrompt() => _targetAlpha = 0f;
    }
}

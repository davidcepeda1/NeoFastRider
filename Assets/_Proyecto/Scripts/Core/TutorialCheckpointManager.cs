using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NeoFastRider.Tutorial
{
    /// <summary>
    /// Manages the three tutorial zones (A/B/C), detects collision resets,
    /// executes the 0.3-second fade-to-black + "INTÉNTALO DE NUEVO" overlay,
    /// and teleports the player back to the active zone's start Z coordinate.
    /// </summary>
    public sealed class TutorialCheckpointManager : MonoBehaviour
    {
        // ── Zone spawn-Z coordinates ───────────────────────────────────────────
        public const float ZoneA_Start = 0f;
        public const float ZoneB_Start = 200f;
        public const float ZoneC_Start = 400f;

        private const float FadeDuration  = 0.15f; // half of 0.3s (in + out)
        private const float HoldDuration  = 0.3f;
        private const string RetryMessage = "INTÉNTALO DE NUEVO";

        // ── Serialized refs ───────────────────────────────────────────────────
        [Header("Player")]
        [SerializeField] private Transform _playerTransform;

        [Header("Fade Overlay")]
        [SerializeField] private Image          _fadeImage;      // full-screen black Image
        [SerializeField] private TextMeshProUGUI _retryText;     // "INTÉNTALO DE NUEVO"

        [Header("Zone Prompts")]
        [SerializeField] private TextMeshProUGUI _zonePromptText;

        // ── Runtime state ─────────────────────────────────────────────────────
        private int   _currentZone = 0;   // 0=A, 1=B, 2=C
        private bool  _isResetting = false;
        private float _zoneResetZ  = ZoneA_Start;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Called by obstacle colliders when an unshielded impact occurs.</summary>
        public void TriggerReset()
        {
            if (_isResetting) return;
            StartCoroutine(ExecuteReset());
        }

        /// <summary>Called by zone trigger volumes when player crosses a boundary.</summary>
        public void EnterZone(int zone)
        {
            _currentZone = zone;
            _zoneResetZ  = zone == 0 ? ZoneA_Start : zone == 1 ? ZoneB_Start : ZoneC_Start;
            if (_zonePromptText != null)
            {
                string[] prompts = {
                    "MOTO SE MUEVE AUTOMÁTICAMENTE.\nMANTÉN 'W' PARA ACELERAR AL MÁXIMO (120 km/h)",
                    "OBSTÁCULO ADELANTE.\nUSA 'A/D' PARA CAMBIAR AL CARRIL LIBRE",
                    "BARRERA IMPASABLE.\nPRESIONA 'ESPACIO' PARA DISPARAR TU CAÑÓN DE PULSO"
                };
                if (zone >= 0 && zone < prompts.Length)
                    _zonePromptText.text = prompts[zone];
            }
        }

        // ── Private ───────────────────────────────────────────────────────────

        private IEnumerator ExecuteReset()
        {
            _isResetting = true;

            // Fade to black
            yield return FadeCanvas(0f, 1f, FadeDuration);

            // Show retry text
            if (_retryText != null)
            {
                _retryText.text    = RetryMessage;
                _retryText.enabled = true;
            }

            yield return new WaitForSeconds(HoldDuration);

            // Teleport player to zone start
            if (_playerTransform != null)
            {
                var pos = _playerTransform.position;
                pos.z   = _zoneResetZ;
                pos.x   = 0f;
                _playerTransform.position = pos;

                // Reset physics velocity if Rigidbody present (defensive)
                var rb = _playerTransform.GetComponent<Rigidbody>();
                if (rb != null) rb.linearVelocity = UnityEngine.Vector3.zero;
            }

            // Hide retry text
            if (_retryText != null) _retryText.enabled = false;

            // Fade back in
            yield return FadeCanvas(1f, 0f, FadeDuration);

            _isResetting = false;
        }

        private IEnumerator FadeCanvas(float from, float to, float duration)
        {
            if (_fadeImage == null) yield break;
            _fadeImage.gameObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = elapsed / duration;
                var c   = _fadeImage.color;
                c.a     = Mathf.Lerp(from, to, t);
                _fadeImage.color = c;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            var final   = _fadeImage.color;
            final.a     = to;
            _fadeImage.color = final;

            if (to <= 0f) _fadeImage.gameObject.SetActive(false);
        }
    }
}

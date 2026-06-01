using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NeoFastRider.Core
{
    /// <summary>
    /// Scene_Level_Tutorial master controller.
    ///
    /// SYSTEM DESIGN RULES:
    ///   1. IMMORTAL SANDBOX  — integrity clamped to >=1%; OnRunnerDeath suppressed.
    ///   2. NO PAUSE SYSTEM   — ESC and P inputs consumed/swallowed every frame.
    ///   3. FAIL-SAFE LOOP    — collision triggers 0.3s fade-to-black + teleport to
    ///                          active checkpoint Z + "INTÉNTALO DE NUEVO" overlay.
    /// </summary>
    public sealed class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        [Header("Moto References")]
        [SerializeField] private NeoFastRider.Moto.MotoEntity  _motoEntity;
        [SerializeField] private NeoFastRider.Moto.MotoPhysics _motoPhysics;

        [Header("HUD References")]
        [SerializeField] private UnityEngine.UI.Image    _fadeOverlay;
        [SerializeField] private TMPro.TextMeshProUGUI   _retryText;
        [SerializeField] private TMPro.TextMeshProUGUI   _zonePromptText;

        [Header("Zone Z Coordinates")]
        [SerializeField] private float _zoneAStartZ = 0f;
        [SerializeField] private float _zoneBStartZ = 200f;
        [SerializeField] private float _zoneCStartZ = 400f;

        [Header("Zone Prompts")]
        [SerializeField, TextArea] private string _promptZoneA =
            "MOTO SE MUEVE AUTOMÁTICAMENTE.\nMANTÉN 'W' PARA ACELERAR AL MÁXIMO (120 km/h)";
        [SerializeField, TextArea] private string _promptZoneB =
            "OBSTÁCULO ADELANTE.\nUSA 'A/D' PARA CAMBIAR AL CARRIL LIBRE";
        [SerializeField, TextArea] private string _promptZoneC =
            "BARRERA IMPASABLE.\nPRESIONA 'ESPACIO' PARA DISPARAR TU CAÑÓN DE PULSO";

        [Header("Timing")]
        [SerializeField] private float _fadeDuration  = 0.15f;
        [SerializeField] private float _retryHoldTime = 1.0f;

        private int   _currentZone = 0;
        private float _checkpointZ = 0f;
        private bool  _resetting   = false;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (_motoEntity != null)
                _motoEntity.OnRunnerDeath += HandleDeathSuppressed;

            _checkpointZ = _zoneAStartZ;
        }

        private void Start()
        {
            SetFadeAlpha(0f);
            ShowRetryText(false);
            UpdateZonePrompt();
        }

        private void OnDestroy()
        {
            if (_motoEntity != null)
                _motoEntity.OnRunnerDeath -= HandleDeathSuppressed;
        }

        private void Update()
        {
            // ── RULE 2: swallow pause inputs ─────────────────────────────────
            if (Keyboard.current != null &&
               (Keyboard.current.escapeKey.wasPressedThisFrame ||
                Keyboard.current.pKey.wasPressedThisFrame))
            { /* consumed */ }

            // ── RULE 1: keep integrity above 1% ──────────────────────────────
            _motoEntity?.ClampIntegrityMin(1f);

            // ── Zone detection from Z position ────────────────────────────────
            if (_motoPhysics != null)
                EvaluateZone(_motoPhysics.transform.position.z);
        }

        /// <summary>Called by MotoArsenal or obstacles on unshielded collision.</summary>
        public void RegisterCollision()
        {
            if (!_resetting) StartCoroutine(FailSafeReset());
        }

        private void HandleDeathSuppressed()
        {
            _motoEntity?.ForceRestoreIntegrity(100f);
            if (!_resetting) StartCoroutine(FailSafeReset());
        }

        private void EvaluateZone(float z)
        {
            int newZone = z >= _zoneCStartZ ? 2 : z >= _zoneBStartZ ? 1 : 0;
            if (newZone == _currentZone) return;
            _currentZone = newZone;
            _checkpointZ = newZone == 2 ? _zoneCStartZ
                         : newZone == 1 ? _zoneBStartZ : _zoneAStartZ;
            UpdateZonePrompt();
        }

        private void UpdateZonePrompt()
        {
            if (_zonePromptText == null) return;
            _zonePromptText.text = _currentZone == 2 ? _promptZoneC
                                 : _currentZone == 1 ? _promptZoneB
                                 : _promptZoneA;
        }

        private IEnumerator FailSafeReset()
        {
            _resetting = true;
            yield return Fade(0f, 1f, _fadeDuration);
            ShowRetryText(true);

            if (_motoPhysics != null)
            {
                var p = _motoPhysics.transform.position;
                _motoPhysics.transform.SetPositionAndRotation(
                    new Vector3(0f, p.y, _checkpointZ), Quaternion.identity);
            }
            _motoEntity?.ForceRestoreIntegrity(100f);

            yield return new WaitForSeconds(_retryHoldTime);
            ShowRetryText(false);
            yield return Fade(1f, 0f, _fadeDuration);
            _resetting = false;
        }

        private IEnumerator Fade(float from, float to, float dur)
        {
            if (_fadeOverlay == null) yield break;
            float e = 0f;
            while (e < dur)
            {
                SetFadeAlpha(Mathf.Lerp(from, to, e / dur));
                e += Time.unscaledDeltaTime;
                yield return null;
            }
            SetFadeAlpha(to);
        }

        private void SetFadeAlpha(float a)
        {
            if (_fadeOverlay == null) return;
            var c = _fadeOverlay.color; c.a = a; _fadeOverlay.color = c;
        }

        private void ShowRetryText(bool show)
        {
            if (_retryText == null) return;
            _retryText.gameObject.SetActive(show);
            if (show) _retryText.text = "INTÉNTALO DE NUEVO";
        }
    }
}

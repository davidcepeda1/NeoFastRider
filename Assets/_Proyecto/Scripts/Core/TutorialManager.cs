using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using NeoFastRider.UI;

namespace NeoFastRider.Core
{
    /// <summary>
    /// Scene_Level_Tutorial master controller.
    ///
    /// SYSTEM DESIGN RULES:
    ///   1. IMMORTAL SANDBOX  — integrity clamped to >=1%; OnRunnerDeath suppressed.
    ///   2. NO PAUSE SYSTEM   — ESC y P inputs consumed/swallowed every frame.
    ///   3. FAIL-SAFE LOOP    — al chocar con un obstáculo (ver
    ///                          TutorialObstacleCollisionRelay), fade-to-black +
    ///                          teleport al punto inicial del nivel + overlay
    ///                          "INTÉNTALO DE NUEVO", en vez de quedar bloqueado
    ///                          por la física del obstáculo.
    /// </summary>
    public sealed class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        [Header("Moto References")]
        [SerializeField] private NeoFastRider.Moto.PlayerHealth      _playerHealth;
        [SerializeField] private NeoFastRider.Moto.MotoForwardDriver _motoForward;

        [Header("HUD References")]
        [Tooltip("Opcional: solo si la escena usa Canvas+TMP. Este nivel usa NoesisGUI (ver _visor).")]
        [SerializeField] private UnityEngine.UI.Image    _fadeOverlay;
        [SerializeField] private TMPro.TextMeshProUGUI   _retryText;
        [SerializeField] private TMPro.TextMeshProUGUI   _zonePromptText;

        private HelmetVisorController _visor;

        [Header("Zone Z Coordinates")]
        [SerializeField] private float _zoneAStartZ = 0f;
        [SerializeField] private float _zoneBStartZ = 200f;
        [SerializeField] private float _zoneCStartZ = 400f;

        [Header("Zone Prompts")]
        [SerializeField, TextArea] private string _promptZoneA =
            "¡OBSTÁCULO ADELANTE! USA 'A/D' PARA CAMBIAR DE CARRIL";
        [Tooltip("Vacío = sin aviso adicional en esta zona.")]
        [SerializeField, TextArea] private string _promptZoneB = "";
        [SerializeField, TextArea] private string _promptZoneC =
            "PRESIONA 'ESPACIO' PARA DISPARAR";

        [Header("Timing")]
        [SerializeField] private float _fadeDuration  = 0.15f;
        [SerializeField] private float _retryHoldTime = 1.0f;

        private int        _currentZone = 0;
        private bool       _resetting   = false;
        private Vector3    _levelStartPosition;
        private Quaternion _levelStartRotation;
        private Rigidbody  _motoRigidbody;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _visor = FindFirstObjectByType<HelmetVisorController>();

            if (_playerHealth != null)
                _playerHealth.OnPlayerDeath += HandleDeathSuppressed;

            if (_motoForward != null)
            {
                _levelStartPosition = _motoForward.transform.position;
                _levelStartRotation = _motoForward.transform.rotation;
                _motoRigidbody      = _motoForward.GetComponent<Rigidbody>();
            }
        }

        private void Start()
        {
            SetFadeAlpha(0f);
            ShowRetryText(false);
            UpdateZonePrompt();
        }

        private void OnDestroy()
        {
            if (_playerHealth != null)
                _playerHealth.OnPlayerDeath -= HandleDeathSuppressed;
        }

        private void Update()
        {
            // ── RULE 2: swallow pause inputs ─────────────────────────────────
            if (Keyboard.current != null &&
               (Keyboard.current.escapeKey.wasPressedThisFrame ||
                Keyboard.current.pKey.wasPressedThisFrame))
            { /* consumed */ }

            // ── RULE 1: keep integrity above 1% ──────────────────────────────
            _playerHealth?.ClampMinPercent(1f);

            // ── Zone detection from Z position ────────────────────────────────
            if (_motoForward != null)
                EvaluateZone(_motoForward.transform.position.z);
        }

        /// <summary>Called by MotoArsenal or obstacles on unshielded collision.</summary>
        public void RegisterCollision()
        {
            if (!_resetting) StartCoroutine(FailSafeReset());
        }

        private void HandleDeathSuppressed()
        {
            _playerHealth?.ForceRestorePercent(100f);
            if (!_resetting) StartCoroutine(FailSafeReset());
        }

        private void EvaluateZone(float z)
        {
            int newZone = z >= _zoneCStartZ ? 2 : z >= _zoneBStartZ ? 1 : 0;
            if (newZone == _currentZone) return;
            _currentZone = newZone;
            UpdateZonePrompt();
        }

        private void UpdateZonePrompt()
        {
            string prompt = _currentZone == 2 ? _promptZoneC
                          : _currentZone == 1 ? _promptZoneB
                          : _promptZoneA;

            if (_zonePromptText != null) _zonePromptText.text = prompt;

            if (string.IsNullOrWhiteSpace(prompt)) _visor?.HideTutorialPrompt();
            else _visor?.ShowTutorialPrompt(prompt);
        }

        private IEnumerator FailSafeReset()
        {
            _resetting = true;
            yield return Fade(0f, 1f, _fadeDuration);
            ShowRetryText(true);

            if (_motoForward != null)
            {
                if (_motoRigidbody != null)
                {
                    // Evita el "smear" visual de la interpolación al teletransportar
                    // un Rigidbody dinámico a mitad de una resolución de colisión.
                    var prevInterpolation = _motoRigidbody.interpolation;
                    _motoRigidbody.interpolation   = RigidbodyInterpolation.None;
                    _motoRigidbody.linearVelocity  = Vector3.zero;
                    _motoRigidbody.angularVelocity = Vector3.zero;
                    _motoRigidbody.position = _levelStartPosition;
                    _motoRigidbody.rotation = _levelStartRotation;
                    Physics.SyncTransforms();
                    _motoRigidbody.interpolation = prevInterpolation;
                }
                else
                {
                    _motoForward.transform.SetPositionAndRotation(
                        _levelStartPosition, _levelStartRotation);
                }
            }
            _playerHealth?.ForceRestorePercent(100f);

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

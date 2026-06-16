using UnityEngine;

namespace NeoFastRider.Moto
{
    /// <summary>
    /// Inclina visualmente el Motorcycle en Z cuando el jugador cambia de carril.
    /// Se añade al GameObject Motorcycle (hijo de [Moto_Runner]).
    /// Lee el LaneController del padre — nunca mueve el contenedor raíz.
    /// </summary>
    public sealed class MotoLeanController : MonoBehaviour
    {
        [Header("Ángulo de inclinación")]
        [Tooltip("Grados máximos de inclinación lateral.")]
        [SerializeField] private float _maxLeanAngle  = 12f;

        [Tooltip("Tiempo de suavizado del lean.")]
        [SerializeField, Range(0.05f, 0.5f)] private float _leanSmoothTime = 0.25f;

        // ── Caché ─────────────────────────────────────────────────────────────
        private LaneController _lane;
        private float          _currentLean;
        private float          _leanVelocity;

        private void Awake()
        {
            // LaneController vive en el padre [Moto_Runner]
            _lane = GetComponentInParent<LaneController>();
        }

        private void Update()
        {
            if (_lane == null) return;

            // Calcular lean objetivo según si está cambiando carril y en qué dirección
            float targetLean = 0f;

            if (_lane.IsChangingLane)
            {
                // CurrentLane ahora es -1 (izq) o +1 (der)
                // Lean: izquierda = +Z, derecha = -Z
                targetLean = -_lane.CurrentLane * _maxLeanAngle;
            }

            // Suavizar
            _currentLean = Mathf.SmoothDamp(
                _currentLean, targetLean,
                ref _leanVelocity, _leanSmoothTime);

            // Aplicar solo en Z — X e Y los gestiona el RailController
            Vector3 euler        = transform.localEulerAngles;
            euler.z              = _currentLean;
            transform.localEulerAngles = euler;
        }
    }
}

using UnityEngine;
using NeoFastRider.Moto;
using NeoFastRider.Core.LevelGen;

namespace NeoFastRider.Core
{
    /// <summary>
    /// Manager de niveles con meta (Scene_Level_01 y sucesores): cuenta
    /// regresiva y rampa de velocidad progresiva según el tiempo transcurrido,
    /// más detección de línea de meta.
    ///
    /// A diferencia de TutorialManager, aquí NO hay inmortalidad — el núcleo
    /// sí puede llegar a 0. La condición de fallo exacta (qué pasa al perder)
    /// queda pendiente de diseño; por ahora solo se registra en consola.
    /// </summary>
    public sealed class LevelGoalManager : MonoBehaviour
    {
        public static LevelGoalManager Instance { get; private set; }

        [Header("Referencias")]
        [SerializeField] private MotoForwardDriver   _motoForward;
        [SerializeField] private PlayerHealth        _playerHealth;
        [SerializeField] private LevelChunkGenerator _generator;

        [Header("Tiempo")]
        [SerializeField] private float _timeLimitSeconds = 120f;

        [Header("Rampa de velocidad (según % de tiempo transcurrido)")]
        [SerializeField] private float _startBaseSpeedKmh  = 60f;
        [SerializeField] private float _endBaseSpeedKmh    = 90f;
        [SerializeField] private float _startBoostSpeedKmh = 90f;
        [SerializeField] private float _endBoostSpeedKmh   = 120f;

        public float TimeRemaining => _timeRemaining;
        public bool  IsFinished    => _finished;

        private float _timeRemaining;
        private bool  _finished;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _timeRemaining = _timeLimitSeconds;
        }

        private void Update()
        {
            if (_finished) return;

            _timeRemaining -= Time.deltaTime;
            UpdateSpeedRamp();

            if (_timeRemaining <= 0f)
            {
                _timeRemaining = 0f;
                HandleTimeUp();
            }
        }

        private void UpdateSpeedRamp()
        {
            if (_motoForward == null) return;

            float t = 1f - Mathf.Clamp01(_timeRemaining / _timeLimitSeconds);
            float baseKmh  = Mathf.Lerp(_startBaseSpeedKmh,  _endBaseSpeedKmh,  t);
            float boostKmh = Mathf.Lerp(_startBoostSpeedKmh, _endBoostSpeedKmh, t);
            _motoForward.SetSpeedRange(baseKmh, boostKmh);
        }

        /// <summary>Llamado por FinishLineTrigger al cruzar la línea de meta.</summary>
        public void RegisterFinish()
        {
            if (_finished) return;
            _finished = true;

            bool coreAlive = _playerHealth == null || !_playerHealth.IsDead;
            Debug.Log(coreAlive
                ? "[LevelGoalManager] ¡Meta alcanzada con el núcleo sano!"
                : "[LevelGoalManager] Meta alcanzada, pero el núcleo no sobrevivió.");

            // TODO: pantalla de victoria — pendiente de diseño (siguiente fase).
        }

        private void HandleTimeUp()
        {
            _finished = true;
            Debug.Log("[LevelGoalManager] Tiempo agotado antes de llegar a la meta.");

            // TODO: condición de fallo — pendiente de diseño (definida por el usuario más adelante).
        }
    }
}

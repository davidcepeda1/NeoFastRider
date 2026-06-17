using UnityEngine;
using UnityEngine.InputSystem;

namespace NeoFastRider.Moto
{
    /// <summary>
    /// Movimiento lateral LIBRE dentro del ancho de la carretera.
    /// A mantenido = mueve a la izquierda continuamente.
    /// D mantenido = mueve a la derecha continuamente.
    /// Sin tecla = se queda donde está.
    /// Clampeado al ancho del camino.
    /// </summary>
    public sealed class LaneController : MonoBehaviour
    {
        [Tooltip("Velocidad lateral en unidades por segundo.")]
        [SerializeField] private float _lateralSpeed = 8f;

        [Tooltip("Limite maximo de offset lateral (medio ancho del camino).")]
        [SerializeField] private float _maxOffset = 20f;

        private float _currentOffsetX = 0f;
        private bool  _isMovingLateral = false;
        private int   _moveDirection   = 0; // -1 izq, 0 nada, +1 der

        /// <summary>Evento: empieza a moverse lateral. -1=izq, +1=der.</summary>
        public event System.Action<int> OnLaneChangeStarted;
        /// <summary>Evento: dejó de moverse lateralmente.</summary>
        public event System.Action      OnLaneChangeCompleted;

        public float CurrentOffsetX => _currentOffsetX;
        public bool  IsChangingLane => _isMovingLateral;
        public int   TargetLane     => _moveDirection;
        public int   CurrentLane    => _moveDirection;

        private void Update()
        {
            if (Keyboard.current == null) return;

            bool aHeld = Keyboard.current.aKey.isPressed;
            bool dHeld = Keyboard.current.dKey.isPressed;

            int newDir = 0;
            if (aHeld && !dHeld) newDir = -1;
            else if (dHeld && !aHeld) newDir = 1;

            // Detectar cambio de estado para eventos del animator
            if (newDir != 0 && !_isMovingLateral)
            {
                _isMovingLateral = true;
                _moveDirection   = newDir;
                OnLaneChangeStarted?.Invoke(newDir);
            }
            else if (newDir == 0 && _isMovingLateral)
            {
                _isMovingLateral = false;
                _moveDirection   = 0;
                OnLaneChangeCompleted?.Invoke();
            }
            else if (newDir != 0 && newDir != _moveDirection)
            {
                // Cambió de dirección sin soltar
                _moveDirection = newDir;
                OnLaneChangeStarted?.Invoke(newDir);
            }
        }

        /// <summary>Llamar cada frame desde MotoForwardDriver.</summary>
        public void Tick(float deltaTime)
        {
            if (_moveDirection != 0)
            {
                _currentOffsetX += _moveDirection * _lateralSpeed * deltaTime;
                _currentOffsetX  = Mathf.Clamp(_currentOffsetX, -_maxOffset, _maxOffset);
            }
            // Sin input: offset se mantiene (no vuelve al centro)
        }
    }
}
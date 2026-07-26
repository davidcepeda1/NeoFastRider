using UnityEngine;

namespace NeoFastRider.Core.LevelGen
{
    /// <summary>
    /// Metadata de un prefab de "chunk" de pista para el generador procedural.
    /// El cursor del generador entra en (0,0,0) mirando +Z y avanza según estos valores.
    /// </summary>
    public sealed class LevelChunkInfo : MonoBehaviour
    {
        public enum ChunkKind { Straight, CurveLeft, CurveRight }

        [Header("Tipo")]
        [SerializeField] private ChunkKind _kind = ChunkKind.Straight;

        [Header("Avance del cursor")]
        [Tooltip("Cuánto avanza el cursor en su eje local hacia adelante.")]
        [SerializeField] private float _forwardLength = 30f;
        [Tooltip("Desplazamiento lateral local del punto de salida (curvas).")]
        [SerializeField] private float _lateralOffset = 0f;
        [Tooltip("Giro en Y aplicado al cursor al salir (0 = recto).")]
        [SerializeField] private float _turnAngleY = 0f;

        [Header("Dificultad")]
        [Tooltip("Solo aplica a chunks rectos: nivel de dificultad para la selección ponderada (0=fácil, 2=difícil).")]
        [SerializeField] private int _difficultyTier = 0;

        [Header("Rareza")]
        [Tooltip("Mínimo de chunks rectos que deben pasar antes de poder repetir este mismo prefab (0 = sin restricción). Útil para obstáculos muy vistosos/específicos (p.ej. choque de autos) que se sienten raros si se repiten seguido.")]
        [SerializeField] private int _minSpacingFromSameKind = 0;

        public ChunkKind Kind                   => _kind;
        public float     ForwardLength          => _forwardLength;
        public float     LateralOffset          => _lateralOffset;
        public float     TurnAngleY             => _turnAngleY;
        public int       DifficultyTier         => _difficultyTier;
        public int       MinSpacingFromSameKind => _minSpacingFromSameKind;
    }
}

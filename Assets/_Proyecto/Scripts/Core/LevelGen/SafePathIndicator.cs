using System.Collections.Generic;
using UnityEngine;
using NeoFastRider.Moto;

namespace NeoFastRider.Core.LevelGen
{
    /// <summary>
    /// Dibuja una línea guía (LineRenderer) que sigue, con anticipación, la ruta segura
    /// precalculada por <see cref="LevelChunkGenerator"/> (el centro del hueco más ancho entre
    /// obstáculos en cada tramo). No detecta nada en tiempo real: solo lee los waypoints ya
    /// calculados; el avance de índice y el suavizado de la curva son puramente de presentación,
    /// la ruta en sí (los waypoints) ya viene garantizada libre de obstáculos.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class SafePathIndicator : MonoBehaviour
    {
        [SerializeField] private LevelChunkGenerator _generator;
        [SerializeField] private Transform _moto;
        [SerializeField] private MotoForwardDriver _motoDriver;
        [Tooltip("Si está activo, la línea solo se muestra mientras el jugador NO acelera a fondo (suelta 'W') — para no saturar la vista cuando ya va a máxima velocidad.")]
        [SerializeField] private bool _hideWhileAccelerating = true;
        [Tooltip("Cuántos waypoints por delante de la moto se dibujan (cada uno son ~2 unidades en tramos rectos).")]
        [SerializeField] private int _lookaheadPoints = 16;
        [Tooltip("Cuántos waypoints hacia adelante se revisan cada cuadro para encontrar por dónde va la moto. Debe cubrir de sobra lo que avanza en un cuadro, pero no tanto como para 'confundirse' con un tramo lejano.")]
        [SerializeField] private int _searchWindow = 20;
        [Tooltip("Radio de coincidencia: un waypoint solo cuenta como 'la moto ya pasó por ahí' si está a esta distancia o menos. Evita saltos falsos hacia un punto lejano que por casualidad quedó cerca (p.ej. el regreso de un esquive).")]
        [SerializeField] private float _matchRadius = 6f;
        [Tooltip("Iteraciones del suavizado (algoritmo de Chaikin, recorte de esquinas). Cada iteración duplica los puntos y redondea más — 3-4 ya se ve bien curvo.")]
        [SerializeField] private int _smoothingIterations = 3;
        [SerializeField] private float _heightOffset = 0.15f;
        [Tooltip("Segundos que tarda en aparecer/desaparecer (fade) al soltar/mantener el acelerador, en vez de aparecer y desaparecer de golpe.")]
        [SerializeField] private float _fadeDuration = 0.35f;
        [Tooltip("Ancho normal de la línea (visible al 100% del fade).")]
        [SerializeField] private float _lineWidth = 0.35f;

        private LineRenderer   _line;
        private Material       _lineMaterial;
        private int            _lastIndex;
        private float          _fadeAlpha; // 0 = invisible, 1 = totalmente visible
        private Color          _baseColor;
        private List<Vector3>  _rawPositions      = new List<Vector3>();
        private List<Vector3>  _smoothedPositions = new List<Vector3>();

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _lineMaterial = _line.material; // instancia propia — no toca el material compartido
            _baseColor = _lineMaterial.color;
        }

        private void Update()
        {
            var waypoints = _generator != null ? _generator.SafePathWaypoints : null;
            bool accelerating = _hideWhileAccelerating && _motoDriver != null && _motoDriver.IsAccelerating;
            bool wantVisible  = waypoints != null && waypoints.Count > 0 && _moto != null && !accelerating;

            // Fundido: en vez de aparecer/desaparecer de golpe (positionCount 0↔N), se anima un
            // 0..1 hacia el estado deseado y se aplica tanto al ancho como al alfa del material,
            // así la línea se ve encenderse/apagarse suave en vez de cortar de un cuadro a otro.
            float target = wantVisible ? 1f : 0f;
            _fadeAlpha = Mathf.MoveTowards(_fadeAlpha, target, Time.deltaTime / Mathf.Max(_fadeDuration, 0.01f));

            if (_fadeAlpha <= 0f)
            {
                _line.positionCount = 0;
                return;
            }

            if (!wantVisible && waypoints == null)
            {
                _line.positionCount = 0;
                return;
            }

            Vector3 motoPos = _moto != null ? _moto.position : transform.position;
            if (waypoints != null && waypoints.Count > 0)
            {
                AdvanceIndex(waypoints, motoPos);

                int count = Mathf.Min(_lookaheadPoints, waypoints.Count - _lastIndex);
                if (count > 0) BuildSmoothedLine(waypoints, motoPos, count);
                else { _line.positionCount = 0; return; }
            }

            ApplyFadeVisuals();
        }

        /// <summary>Aplica el 0..1 de fundido al ancho de la línea y al alfa del material.</summary>
        private void ApplyFadeVisuals()
        {
            float w = _lineWidth * _fadeAlpha;
            _line.startWidth = w;
            _line.endWidth   = w;

            var c = _baseColor;
            c.a *= _fadeAlpha;
            _lineMaterial.color = c;
        }

        /// <summary>
        /// Avanza _lastIndex buscando, en una ventana hacia adelante, el ÚLTIMO waypoint del
        /// primer tramo contiguo que quede dentro de _matchRadius de la moto — no el más cercano
        /// en distancia recta de TODA la ventana. La diferencia importa: cerca de un esquive, el
        /// tramo "de vuelta" del rodeo puede terminar más cerca en línea recta que el tramo
        /// correcto de avance, y saltar ahí hacía que la línea "se distrajera" con el obstáculo
        /// y diera un salto/vuelta rara. El índice nunca retrocede.
        /// </summary>
        private void AdvanceIndex(IReadOnlyList<Vector3> waypoints, Vector3 motoPos)
        {
            int searchStart = Mathf.Max(0, _lastIndex - 2);
            int searchEnd   = Mathf.Min(waypoints.Count - 1, _lastIndex + _searchWindow);

            int newIndex = -1;
            for (int i = searchStart; i <= searchEnd; i++)
            {
                if (Vector3.Distance(motoPos, waypoints[i]) <= _matchRadius)
                {
                    newIndex = i;
                }
                else if (newIndex >= 0)
                {
                    break; // ya encontramos el primer tramo contiguo cercano — no seguir de largo
                }
            }

            if (newIndex >= 0)
            {
                _lastIndex = Mathf.Max(_lastIndex, newIndex);
                return;
            }

            // La ventana local no encontró nada cerca — la moto se movió más de lo esperado
            // en un cuadro (teletransporte, respawn, etc). En vez de quedar congelado en un
            // índice viejo (lo que produce una línea absurda que salta desde muy lejos), se
            // resincroniza buscando el waypoint más cercano en TODA la ruta.
            int fallbackIndex = 0;
            float fallbackDist = Vector3.Distance(motoPos, waypoints[0]);
            for (int i = 1; i < waypoints.Count; i++)
            {
                float d = Vector3.Distance(motoPos, waypoints[i]);
                if (d < fallbackDist) { fallbackDist = d; fallbackIndex = i; }
            }
            _lastIndex = fallbackIndex;
        }

        /// <summary>
        /// Arma la línea final aplicando el algoritmo de Chaikin (recorte de esquinas) sobre el
        /// polígono moto→waypoints. Se probó antes con Catmull-Rom: aunque pasa EXACTO por cada
        /// waypoint (que ya viene garantizado libre de obstáculos), la curva puede "abombarse"
        /// hacia afuera ENTRE dos puntos y meterse en un obstáculo — se verificó matemáticamente
        /// y pasaba. Chaikin no tiene ese problema: cada punto nuevo es una combinación convexa
        /// de dos puntos consecutivos del polígono original, así que la curva resultante NUNCA
        /// sale del corredor que ya se sabe seguro (las líneas rectas originales entre waypoints
        /// ya se verificaron libres de obstáculos).
        /// </summary>
        private void BuildSmoothedLine(IReadOnlyList<Vector3> waypoints, Vector3 motoPos, int count)
        {
            _rawPositions.Clear();
            _rawPositions.Add(motoPos);
            for (int i = 0; i < count; i++)
                _rawPositions.Add(waypoints[_lastIndex + i]);

            ChaikinSmooth(_rawPositions, _smoothingIterations, _smoothedPositions);

            for (int i = 0; i < _smoothedPositions.Count; i++)
                _smoothedPositions[i] += Vector3.up * _heightOffset;

            _line.positionCount = _smoothedPositions.Count;
            _line.SetPositions(_smoothedPositions.ToArray());
        }

        /// <summary>
        /// Recorte de esquinas de Chaikin: cada segmento (p0,p1) se reemplaza por dos puntos al
        /// 25% y 75% del camino entre ellos. El primer y último punto del polígono original se
        /// mantienen exactos (no se recortan) para que la curva arranque justo en la moto.
        /// </summary>
        private static void ChaikinSmooth(List<Vector3> input, int iterations, List<Vector3> output)
        {
            output.Clear();
            output.AddRange(input);

            var scratch = new List<Vector3>();
            for (int iter = 0; iter < iterations; iter++)
            {
                scratch.Clear();
                scratch.Add(output[0]);
                for (int i = 0; i < output.Count - 1; i++)
                {
                    Vector3 p0 = output[i], p1 = output[i + 1];
                    scratch.Add(Vector3.Lerp(p0, p1, 0.25f));
                    scratch.Add(Vector3.Lerp(p0, p1, 0.75f));
                }
                scratch.Add(output[output.Count - 1]);

                output.Clear();
                output.AddRange(scratch);
            }
        }
    }
}

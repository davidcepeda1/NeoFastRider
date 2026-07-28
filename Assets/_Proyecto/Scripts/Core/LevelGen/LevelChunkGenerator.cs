using System.Collections.Generic;
using UnityEngine;

namespace NeoFastRider.Core.LevelGen
{
    /// <summary>
    /// Genera toda la pista de una sola vez al iniciar el nivel, encadenando
    /// prefabs de "chunk" (rectos y curvas) mediante un cursor que avanza
    /// según los datos de <see cref="LevelChunkInfo"/> de cada pieza.
    ///
    /// No hay streaming/pooling en tiempo real: como el nivel tiene una meta
    /// fija (no es infinito), toda la secuencia se arma de antemano.
    /// </summary>
    public sealed class LevelChunkGenerator : MonoBehaviour
    {
        [Header("Piezas rectas (mezcla libre de dificultades)")]
        [Tooltip("Cada prefab debe tener LevelChunkInfo con Kind=Straight y su DifficultyTier (0=fácil,1=medio,2=difícil).")]
        [SerializeField] private LevelChunkInfo[] _straightChunks;

        [Header("Piezas de curva")]
        [SerializeField] private LevelChunkInfo _curveLeftPrefab;
        [SerializeField] private LevelChunkInfo _curveRightPrefab;
        [Range(0f, 1f)]
        [SerializeField] private float _curveChance = 0.15f;
        [Tooltip("Mínimo de tramos rectos obligatorios entre dos curvas — evita que la pista se enrosque sobre sí misma.")]
        [SerializeField] private int _minStraightsBetweenCurves = 18;

        [Header("Generación")]
        [SerializeField] private float _totalTrackLength = 3000f;
        [SerializeField] private int   _randomSeed = 0;
        [Tooltip("0 = semilla aleatoria real cada vez.")]
        [SerializeField] private bool _useFixedSeed = false;

        [Tooltip("Distancia inicial (unidades) sin chunks con obstáculos — le da al jugador tiempo de ver el camino y acostumbrarse antes de que aparezca el primero, en vez de encontrarse uno a los pocos metros de arrancar.")]
        [SerializeField] private float _safeStartDistance = 150f;

        [Header("Meta")]
        [SerializeField] private GameObject _finishLinePrefab;

        [Header("Anti-cruce")]
        [Tooltip("Distancia mínima permitida entre tramos no adyacentes (debe cubrir el ancho de la pista + margen).")]
        [SerializeField] private float _minClearance = 24f;
        [Tooltip("Cuántas piezas candidatas se intentan antes de aceptar una que se cruce (último recurso).")]
        [SerializeField] private int _maxPlacementAttempts = 6;

        [Header("Ciudad — fila de calle (tiendas/residencial, cerca)")]
        [SerializeField] private GameObject[] _streetBuildings;
        [SerializeField] private float _streetSpacing = 14f;
        [SerializeField] private Vector2 _streetDistanceRange = new Vector2(18f, 26f);
        [Range(0f, 1f)]
        [SerializeField] private float _streetChance = 0.92f;

        [Header("Ciudad — fondo (rascacielos, lejos)")]
        [SerializeField] private GameObject[] _backgroundBuildings;
        [SerializeField] private float _backgroundSpacing = 30f;
        [SerializeField] private Vector2 _backgroundDistanceRange = new Vector2(45f, 78f);
        [Range(0f, 1f)]
        [SerializeField] private float _backgroundChance = 0.8f;

        [SerializeField] private Vector2 _buildingYRotationJitter = new Vector2(-8f, 8f);

        [Header("Suelo (placa gris de fondo, como en el tutorial)")]
        [SerializeField] private GameObject _groundPlatePrefab;
        [SerializeField] private float _groundPlateSpacing = 35f;
        [Tooltip("Desplazamiento vertical respecto a la superficie del camino (negativo = por debajo, evita z-fighting).")]
        [SerializeField] private float _groundPlateYOffset = -0.3f;

        [Header("Anti-solape de edificios")]
        [Tooltip("Medio ancho real del camino — ningún edificio puede invadir esta franja.")]
        [SerializeField] private float _roadHalfWidth = 7.5f;
        [Tooltip("Margen extra además del radio real del edificio.")]
        [SerializeField] private float _buildingMargin = 1.5f;
        [SerializeField] private int _buildingPlacementAttempts = 9;

        [Tooltip("Multiplica el espaciado (edificios y suelo) al recorrer curvas — deben ir más juntos que en rectos porque, al seguir un arco, las piezas 'se abren en abanico' y dejan huecos visibles en el borde exterior si usan el mismo espaciado que en línea recta.")]
        [Range(0.2f, 1f)]
        [SerializeField] private float _curveSpacingFactor = 0.5f;

        [Header("Obstáculos — variedad de posición")]
        [Tooltip("Rango (± unidades) de desplazamiento lateral aleatorio aplicado en bloque al arreglo de obstáculos de cada chunk, sumado al espejo izquierda↔derecha — le da variedad de izquierda/centro/derecha sin romper el espaciado de cada patrón.")]
        [SerializeField] private float _obstacleLateralJitter = 1.5f;

        [Header("Power-ups (opcional — vacío = ninguno, no afecta niveles que no lo configuren)")]
        [Tooltip("Prefabs de power-up (láser, escudo, etc.) a repartir por la pista.")]
        [SerializeField] private GameObject[] _pickupPrefabs;
        [Range(0f, 1f)]
        [Tooltip("Probabilidad de aparición al pasar el espaciado mínimo, evaluada solo en tramos rectos SIN obstáculos (así siempre quedan alcanzables sin arriesgar un choque).")]
        [SerializeField] private float _pickupChance = 0.5f;
        [Tooltip("Distancia mínima (unidades de pista) entre dos power-ups.")]
        [SerializeField] private float _pickupMinSpacing = 300f;

        [Header("Enjambres de drones (opcional — vacío = ninguno, no afecta niveles que no lo configuren)")]
        [SerializeField] private GameObject _dronePrefab;
        [Range(0f, 1f)]
        [Tooltip("Probabilidad de aparición al pasar el espaciado mínimo, evaluada solo en tramos rectos SIN obstáculos.")]
        [SerializeField] private float _droneSwarmChance = 0.35f;
        [Tooltip("Cuántos drones trae cada enjambre (mín, máx inclusive).")]
        [SerializeField] private Vector2Int _dronesPerSwarmRange = new Vector2Int(2, 3);
        [Tooltip("Distancia mínima (unidades de pista) entre dos enjambres.")]
        [SerializeField] private float _droneSwarmMinSpacing = 450f;
        [Tooltip("Separación lateral entre drones de un mismo enjambre al aparecer.")]
        [SerializeField] private float _droneSwarmSpread = 3.5f;

        public Transform FinishLineInstance { get; private set; }

        /// <summary>
        /// Ruta recomendada precalculada durante la generación: en cada tramo recto pasa por el
        /// centro del hueco más ancho entre los obstáculos ya colocados (en curvas y tramos
        /// vacíos, sigue el centro del camino). Pensada para que un indicador en tiempo real
        /// (p.ej. una línea guía) solo tenga que leer estos puntos con anticipación, sin tener
        /// que detectar obstáculos por su cuenta cuadro a cuadro.
        /// </summary>
        public IReadOnlyList<Vector3> SafePathWaypoints => _safePathWaypoints;
        private readonly List<Vector3> _safePathWaypoints = new List<Vector3>();

        private Transform _root;
        private Transform _cityRoot;
        private readonly List<(Vector3 a, Vector3 b)> _placedSegments  = new List<(Vector3, Vector3)>();
        private readonly List<(Vector3 pos, float radius, GameObject go)> _placedBuildings = new List<(Vector3, float, GameObject)>();
        private readonly Dictionary<GameObject, float> _footprintRadiusCache = new Dictionary<GameObject, float>();
        private readonly Dictionary<LevelChunkInfo, int> _straightsSincePlaced = new Dictionary<LevelChunkInfo, int>();
        private readonly Dictionary<LevelChunkInfo, bool> _hasObstaclesCache = new Dictionary<LevelChunkInfo, bool>();

        /// <summary>Suma de giros aplicados desde el inicio (grados, signo = lado). Evita que la pista se enrosque sobre sí misma.</summary>
        private float _netHeadingDeg;

        /// <summary>Cuántos tramos rectos consecutivos van desde la última curva.</summary>
        private int _straightsSinceCurve;

        private float _distanceSinceLastPickup;
        private float _distanceSinceLastDroneSwarm;

        private void Awake()
        {
            Generate();
        }

        public void Generate()
        {
            if (_useFixedSeed) Random.InitState(_randomSeed);

            _root     = new GameObject("GeneratedTrack").transform;
            _cityRoot = new GameObject("GeneratedCity").transform;
            _placedSegments.Clear();
            _placedBuildings.Clear();
            _straightsSincePlaced.Clear();
            _safePathWaypoints.Clear();
            _netHeadingDeg        = 0f;
            _straightsSinceCurve  = _minStraightsBetweenCurves; // permite curva desde el arranque
            _distanceSinceLastPickup     = 0f;
            _distanceSinceLastDroneSwarm = 0f;

            Vector3    cursorPos = transform.position;
            Quaternion cursorRot = transform.rotation;

            float distanceSoFar = 0f;

            while (distanceSoFar < _totalTrackLength)
            {
                float progress = Mathf.Clamp01(distanceSoFar / _totalTrackLength);

                LevelChunkInfo chosen = SelectChunk(cursorPos, cursorRot, progress, out Vector3 nextPos);
                if (chosen == null) break; // sin piezas configuradas — evita loop infinito

                var chunkInstance = Instantiate(chosen, cursorPos, cursorRot, _root);

                RandomizeObstacleLayout(chunkInstance.transform);

                if (chosen.Kind == LevelChunkInfo.ChunkKind.Straight)
                    AppendSafePathForStraight(cursorPos, cursorRot, chosen.ForwardLength, chunkInstance.transform);
                else
                    AppendSafePathForCurve(cursorPos, cursorRot, chosen.ForwardLength, chosen.TurnAngleY);

                // Power-ups y enjambres de drones: solo en tramos rectos SIN obstáculos (así el
                // jugador siempre puede tomarlos/pelear sin sumarle riesgo de choque encima) y
                // pasada la zona segura inicial, igual que los obstáculos.
                if (chosen.Kind == LevelChunkInfo.ChunkKind.Straight
                    && distanceSoFar >= _safeStartDistance
                    && !HasObstacles(chosen))
                {
                    TrySpawnPickup(cursorPos, cursorRot, chosen.ForwardLength);
                    TrySpawnDroneSwarm(cursorPos, cursorRot, chosen.ForwardLength);
                }

                if (Mathf.Approximately(chosen.TurnAngleY, 0f))
                {
                    _placedSegments.Add((cursorPos, nextPos));
                }
                else
                {
                    // Se guarda como varios sub-segmentos siguiendo el arco real (no la
                    // cuerda directa) para que tanto la detección de cruces entre piezas
                    // como el chequeo de distancia-al-camino de los edificios (DistanceToRoad)
                    // sean precisos en cualquier punto del arco, no solo en entrada/mitad/salida.
                    const int arcSegments = 16;
                    Vector3 prevPoint = cursorPos;
                    for (int s = 1; s <= arcSegments; s++)
                    {
                        float   t         = (float)s / arcSegments;
                        Vector3 arcPoint  = s == arcSegments
                            ? nextPos
                            : ComputeArcPoint(cursorPos, cursorRot, chosen.ForwardLength, chosen.TurnAngleY, t).pos;
                        _placedSegments.Add((prevPoint, arcPoint));
                        prevPoint = arcPoint;
                    }
                }

                Quaternion exitRot = cursorRot * Quaternion.Euler(0f, chosen.TurnAngleY, 0f);

                if (chosen.Kind == LevelChunkInfo.ChunkKind.Straight)
                {
                    ScatterAlongStraight(cursorPos, cursorRot, chosen.ForwardLength, _streetBuildings, _streetSpacing, _streetDistanceRange, _streetChance);
                    ScatterAlongStraight(cursorPos, cursorRot, chosen.ForwardLength, _backgroundBuildings, _backgroundSpacing, _backgroundDistanceRange, _backgroundChance);
                    ScatterGroundAlongStraight(cursorPos, cursorRot, chosen.ForwardLength);
                }
                else
                {
                    ScatterAlongCurve(cursorPos, cursorRot, chosen.ForwardLength, chosen.TurnAngleY, _streetBuildings, _streetSpacing * _curveSpacingFactor, _streetDistanceRange, _streetChance);
                    ScatterAlongCurve(cursorPos, cursorRot, chosen.ForwardLength, chosen.TurnAngleY, _backgroundBuildings, _backgroundSpacing * _curveSpacingFactor, _backgroundDistanceRange, _backgroundChance);
                    ScatterGroundAlongCurve(cursorPos, cursorRot, chosen.ForwardLength, chosen.TurnAngleY, _groundPlateSpacing * _curveSpacingFactor);
                }

                cursorRot       = exitRot;
                cursorPos       = nextPos;
                distanceSoFar  += chosen.ForwardLength;
                _netHeadingDeg += chosen.TurnAngleY;

                if (chosen.Kind == LevelChunkInfo.ChunkKind.Straight)
                {
                    _straightsSinceCurve++;

                    // Cooldown de rareza: cuenta chunks rectos desde la última vez que se usó
                    // CADA prefab con _minSpacingFromSameKind > 0 (p.ej. el choque de autos),
                    // para que PickWeightedStraight pueda excluirlo hasta que pase suficiente
                    // distancia — evita que un obstáculo muy vistoso se repita seguido.
                    foreach (var key in new List<LevelChunkInfo>(_straightsSincePlaced.Keys))
                        _straightsSincePlaced[key]++;
                    _straightsSincePlaced[chosen] = 0;
                }
                else
                {
                    _straightsSinceCurve = 0;
                }
            }

            // Pasada final anti-solape: los edificios se validan contra el camino a medida que
            // se genera, pero un tramo POSTERIOR (p.ej. una curva que da la vuelta) puede pasar
            // cerca de un edificio ya colocado antes, sin forma de saberlo en ese momento. Ahora
            // que toda la pista existe, se purgan los pocos casos que quedaron inválidos.
            for (int i = _placedBuildings.Count - 1; i >= 0; i--)
            {
                var (pos, radius, go) = _placedBuildings[i];
                if (DistanceToRoad(pos) < _roadHalfWidth + radius + _buildingMargin)
                {
                    Destroy(go);
                    _placedBuildings.RemoveAt(i);
                }
            }

            if (_finishLinePrefab != null)
            {
                var finish = Instantiate(_finishLinePrefab, cursorPos, cursorRot, _root);
                FinishLineInstance = finish.transform;
            }
        }

        /// <summary>Reparte un power-up al azar cerca del centro de un tramo recto vacío, respetando el espaciado mínimo configurado.</summary>
        private void TrySpawnPickup(Vector3 cursorPos, Quaternion cursorRot, float forwardLength)
        {
            _distanceSinceLastPickup += forwardLength;
            if (_pickupPrefabs == null || _pickupPrefabs.Length == 0) return;
            if (_distanceSinceLastPickup < _pickupMinSpacing) return;
            if (Random.value > _pickupChance) return;

            var     prefab = _pickupPrefabs[Random.Range(0, _pickupPrefabs.Length)];
            float   t      = Random.Range(0.3f, 0.7f);
            Vector3 pos    = cursorPos + cursorRot * Vector3.forward * (t * forwardLength) + Vector3.up * 1.2f;
            Instantiate(prefab, pos, cursorRot, _root);

            _distanceSinceLastPickup = 0f;
        }

        /// <summary>Genera un enjambre de 2-3 drones cerca del centro de un tramo recto vacío, respetando el espaciado mínimo configurado.</summary>
        private void TrySpawnDroneSwarm(Vector3 cursorPos, Quaternion cursorRot, float forwardLength)
        {
            _distanceSinceLastDroneSwarm += forwardLength;
            if (_dronePrefab == null) return;
            if (_distanceSinceLastDroneSwarm < _droneSwarmMinSpacing) return;
            if (Random.value > _droneSwarmChance) return;

            int     count       = Random.Range(_dronesPerSwarmRange.x, _dronesPerSwarmRange.y + 1);
            float   t           = Random.Range(0.35f, 0.65f);
            Vector3 swarmCenter = cursorPos + cursorRot * Vector3.forward * (t * forwardLength);

            for (int i = 0; i < count; i++)
            {
                float   lateral  = count == 1 ? 0f : Mathf.Lerp(-_droneSwarmSpread, _droneSwarmSpread, i / (float)(count - 1));
                Vector3 spawnPos = swarmCenter + cursorRot * Vector3.right * lateral;

                var drone = Instantiate(_dronePrefab, spawnPos, cursorRot, _root);
                var ai    = drone.GetComponent<NeoFastRider.Enemies.DroneAIController>();
                if (ai != null) ai.ConfigureFrame(swarmCenter, cursorRot);
            }

            _distanceSinceLastDroneSwarm = 0f;
        }

        /// <summary>
        /// Le da variedad de posición a los obstáculos de un chunk recién instanciado: 50% de
        /// las veces refleja el arreglo completo de lado (izquierda↔derecha) y además aplica un
        /// desplazamiento lateral aleatorio EN BLOQUE (a todos los "Obstacle_*" por igual, no
        /// cada uno por separado) — así un patrón con hueco al medio (p.ej. ObstaculoDoble)
        /// termina con ese hueco más a la izquierda, al centro o a la derecha del camino en vez
        /// de solo dos posiciones fijas espejadas, sin romper el espaciado relativo ya diseñado.
        /// El resultado se recorta para no salirse del ancho real del camino.
        /// </summary>
        private void RandomizeObstacleLayout(Transform chunkInstance)
        {
            bool  mirror = Random.value < 0.5f;
            float shift  = Random.Range(-_obstacleLateralJitter, _obstacleLateralJitter);

            foreach (Transform child in chunkInstance)
            {
                if (!child.name.StartsWith("Obstacle_")) continue;
                var pos = child.localPosition;
                if (mirror) pos.x = -pos.x;
                pos.x = Mathf.Clamp(pos.x + shift, -_roadHalfWidth + 0.5f, _roadHalfWidth - 0.5f);
                child.localPosition = pos;
            }
        }

        /// <summary>
        /// Agrega waypoints de la ruta segura para un tramo recto. Antes se calculaba, en cada
        /// rebanada Z, el "hueco más ancho" de forma independiente — con pocos obstáculos
        /// funcionaba, pero con varios (o mal distribuidos) dos rebanadas vecinas podían elegir
        /// huecos en lados OPUESTOS, y la línea recta que las conecta termina cruzando justo por
        /// donde está el obstáculo (o generando saltos/"encierros" raros). Ahora se resuelve como
        /// lo que realmente es: búsqueda del camino más corto en una grilla 2D (Z × X), donde solo
        /// se puede avanzar entre celdas libres ADYACENTES — así la continuidad queda garantizada
        /// sin importar cuántos obstáculos haya ni cómo estén distribuidos.
        /// </summary>
        private void AppendSafePathForStraight(Vector3 cursorPos, Quaternion cursorRot, float forwardLength, Transform chunkInstance)
        {
            Vector3 forward = cursorRot * Vector3.forward;
            Vector3 right   = cursorRot * Vector3.right;

            // Huella de cada obstáculo de este chunk, en espacio local del chunk (z = a lo largo,
            // x = lateral), con un margen de seguridad extra para no rozarlos.
            const float safetyMargin = 1f;
            var obstacles = new List<(float zMin, float zMax, float xMin, float xMax)>();

            foreach (Transform child in chunkInstance)
            {
                if (!child.name.StartsWith("Obstacle_")) continue;
                var colliders = child.GetComponentsInChildren<Collider>();
                if (colliders.Length == 0) continue;

                Bounds worldBounds = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++) worldBounds.Encapsulate(colliders[i].bounds);

                float xMin = float.PositiveInfinity, xMax = float.NegativeInfinity;
                float zMin = float.PositiveInfinity, zMax = float.NegativeInfinity;
                for (int cx = 0; cx <= 1; cx++)
                for (int cy = 0; cy <= 1; cy++)
                for (int cz = 0; cz <= 1; cz++)
                {
                    Vector3 corner = worldBounds.min + Vector3.Scale(worldBounds.size, new Vector3(cx, cy, cz));
                    Vector3 local  = chunkInstance.InverseTransformPoint(corner);
                    xMin = Mathf.Min(xMin, local.x); xMax = Mathf.Max(xMax, local.x);
                    zMin = Mathf.Min(zMin, local.z); zMax = Mathf.Max(zMax, local.z);
                }
                obstacles.Add((zMin - safetyMargin, zMax + safetyMargin, xMin - safetyMargin, xMax + safetyMargin));
            }

            float laneMin = -_roadHalfWidth + 0.5f;
            float laneMax =  _roadHalfWidth - 0.5f;

            foreach (var (z, x) in FindGridPath(obstacles, forwardLength, laneMin, laneMax))
                AddSafePathWaypoint(cursorPos + forward * z + right * x);
        }

        /// <summary>
        /// Busca el camino más corto (Dijkstra, 8 direcciones) entre el centro de entrada
        /// (z=0,x=0) y el centro de salida (z=forwardLength,x=0) de un tramo, a través de una
        /// grilla local donde cada celda es libre u ocupada según los obstáculos reales. Un
        /// pequeño sesgo hacia el centro evita zigzagueos innecesarios cuando hay varios caminos
        /// igual de cortos. Devuelve directamente los puntos ya en camino más corto encontrado —
        /// nunca "salta" entre huecos no conectados, porque solo puede moverse entre celdas
        /// adyacentes que ya se sabe que están libres.
        /// </summary>
        private static List<(float z, float x)> FindGridPath(
            List<(float zMin, float zMax, float xMin, float xMax)> obstacles,
            float forwardLength, float laneMin, float laneMax)
        {
            const float zStep      = 1f;
            const float xStep      = 1f;
            const float centerBias = 0.05f;

            int rows = Mathf.Max(2, Mathf.RoundToInt(forwardLength / zStep) + 1);
            int cols = Mathf.Max(2, Mathf.RoundToInt((laneMax - laneMin) / xStep) + 1);

            var zOf = new float[rows];
            for (int r = 0; r < rows; r++) zOf[r] = Mathf.Min(r * zStep, forwardLength);
            var xOf = new float[cols];
            for (int c = 0; c < cols; c++) xOf[c] = laneMin + c * xStep;

            var blocked = new bool[rows, cols];
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                float z = zOf[r], x = xOf[c];
                for (int i = 0; i < obstacles.Count; i++)
                {
                    var (zMin, zMax, xMin, xMax) = obstacles[i];
                    if (z >= zMin && z <= zMax && x >= xMin && x <= xMax) { blocked[r, c] = true; break; }
                }
            }

            int startCol = NearestFreeCol(blocked, 0,        cols, xOf, 0f);
            int goalCol  = NearestFreeCol(blocked, rows - 1,  cols, xOf, 0f);

            var dist    = new float[rows, cols];
            var visited = new bool[rows, cols];
            var prevR   = new int[rows, cols];
            var prevC   = new int[rows, cols];
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                dist[r, c] = float.PositiveInfinity;
            dist[0, startCol] = 0f;

            int[] dr = { -1, -1, -1, 0, 0, 1, 1, 1 };
            int[] dc = { -1,  0,  1,-1, 1,-1, 0, 1 };

            int lastRowReached = -1;
            for (int iter = 0; iter < rows * cols; iter++)
            {
                int br = -1, bc = -1; float bd = float.PositiveInfinity;
                for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    if (!visited[r, c] && dist[r, c] < bd) { bd = dist[r, c]; br = r; bc = c; }

                if (br < 0) break; // no queda nada más alcanzable
                visited[br, bc] = true;
                if (br == rows - 1) { lastRowReached = bc; break; } // ya llegamos al final del tramo

                for (int k = 0; k < 8; k++)
                {
                    int nr = br + dr[k], nc = bc + dc[k];
                    if (nr < 0 || nr >= rows || nc < 0 || nc >= cols) continue;
                    if (blocked[nr, nc] || visited[nr, nc]) continue;

                    float dz = zOf[nr] - zOf[br], dx = xOf[nc] - xOf[bc];
                    float stepDist = Mathf.Sqrt(dz * dz + dx * dx);
                    float cost     = stepDist + centerBias * Mathf.Abs(xOf[nc]) * stepDist;
                    float nd       = dist[br, bc] + cost;

                    if (nd < dist[nr, nc])
                    {
                        dist[nr, nc] = nd;
                        prevR[nr, nc] = br;
                        prevC[nr, nc] = bc;
                    }
                }
            }

            int endCol = goalCol;
            if (lastRowReached >= 0) endCol = lastRowReached;
            else if (float.IsPositiveInfinity(dist[rows - 1, goalCol]))
            {
                float best = float.PositiveInfinity;
                for (int c = 0; c < cols; c++)
                    if (dist[rows - 1, c] < best) { best = dist[rows - 1, c]; endCol = c; }
            }

            var path = new List<(float z, float x)>();
            if (float.IsPositiveInfinity(dist[rows - 1, endCol]))
            {
                // Caso extremo (no debería ocurrir dado el margen que ya exige la generación de
                // obstáculos): sin celda libre alcanzable en la última fila — última salida segura.
                path.Add((0f, 0f));
                path.Add((forwardLength, 0f));
                return path;
            }

            var reversed = new List<(int r, int c)>();
            int curR = rows - 1, curC = endCol;
            while (!(curR == 0 && curC == startCol))
            {
                reversed.Add((curR, curC));
                int pr = prevR[curR, curC], pc = prevC[curR, curC];
                curR = pr; curC = pc;
            }
            reversed.Add((0, startCol));
            reversed.Reverse();

            foreach (var (r, c) in reversed) path.Add((zOf[r], xOf[c]));
            return path;
        }

        /// <summary>Columna libre más cercana a preferredX en una fila dada (búsqueda en espiral desde la columna preferida).</summary>
        private static int NearestFreeCol(bool[,] blocked, int row, int cols, float[] xOf, float preferredX)
        {
            int preferredCol = 0;
            float bestDist = float.PositiveInfinity;
            for (int c = 0; c < cols; c++)
            {
                float d = Mathf.Abs(xOf[c] - preferredX);
                if (d < bestDist) { bestDist = d; preferredCol = c; }
            }
            if (!blocked[row, preferredCol]) return preferredCol;

            for (int offset = 1; offset < cols; offset++)
            {
                int left = preferredCol - offset, right = preferredCol + offset;
                if (left >= 0 && !blocked[row, left]) return left;
                if (right < cols && !blocked[row, right]) return right;
            }
            return preferredCol; // no debería llegar acá si hay al menos una celda libre en la fila
        }

        /// <summary>
        /// Agrega un waypoint de ruta segura, salvo que sea (casi) el mismo punto que el último
        /// agregado. La unión entre chunks siempre repite el punto exacto (fin de uno = inicio
        /// del siguiente); sin este filtro, esos duplicados generaban segmentos de longitud cero
        /// que le rompían la fluidez a la curva de presentación (SafePathIndicator).
        /// </summary>
        private void AddSafePathWaypoint(Vector3 point)
        {
            if (_safePathWaypoints.Count > 0 &&
                (point - _safePathWaypoints[_safePathWaypoints.Count - 1]).sqrMagnitude < 0.0001f)
                return;
            _safePathWaypoints.Add(point);
        }

        /// <summary>Waypoints de ruta segura para una curva: no hay obstáculos en curvas, así que sigue el centro del arco real.</summary>
        private void AppendSafePathForCurve(Vector3 cursorPos, Quaternion cursorRot, float forwardLength, float turnAngleY)
        {
            const int samples = 8;
            for (int i = 0; i <= samples; i++)
            {
                float t = (float)i / samples;
                var (pos, _) = ComputeArcPoint(cursorPos, cursorRot, forwardLength, turnAngleY, t);
                AddSafePathWaypoint(pos);
            }
        }

        /// <summary>
        /// Elige la siguiente pieza probando varios candidatos (curva preferida primero si toca,
        /// luego rectos ponderados) y descarta los que se crucen con tramos ya colocados.
        /// Si ningún candidato libre de cruce aparece, usa el primero como último recurso.
        /// </summary>
        private LevelChunkInfo SelectChunk(Vector3 cursorPos, Quaternion cursorRot, float progress, out Vector3 chosenEnd)
        {
            Vector3 forward = cursorRot * Vector3.forward;
            Vector3 right   = cursorRot * Vector3.right;

            var candidates = new List<LevelChunkInfo>();

            bool curveEligible = _straightsSinceCurve >= _minStraightsBetweenCurves
                               && _curveLeftPrefab != null && _curveRightPrefab != null;
            if (curveEligible && Random.value < _curveChance)
            {
                // La pista ya se desvió mucho de su rumbo original: forzar el giro
                // contrario para evitar que se enrosque sobre sí misma. Bajado de 110°
                // a 75° — con 110° la pista podía desviarse tanto antes de corregir que
                // llegaba a formar casi un lazo completo de vuelta al origen, cruzándose
                // a sí misma muy lejos en el recorrido (fuera del alcance de cualquier
                // chequeo local anti-cruce).
                const float hardCorrectionDeg = 75f;
                bool forceLeft  = _netHeadingDeg >  hardCorrectionDeg;
                bool forceRight = _netHeadingDeg < -hardCorrectionDeg;

                if (forceLeft)
                {
                    candidates.Add(_curveLeftPrefab);
                }
                else if (forceRight)
                {
                    candidates.Add(_curveRightPrefab);
                }
                else
                {
                    // Sesgo suave: mientras más desviado el rumbo neto, más probable
                    // que se prefiera el giro que lo corrige de vuelta hacia 0.
                    float leftBias = Mathf.Clamp(0.5f + _netHeadingDeg / 360f, 0.15f, 0.85f);
                    bool  leftFirst = Random.value < leftBias;
                    candidates.Add(leftFirst ? _curveLeftPrefab  : _curveRightPrefab);
                    candidates.Add(leftFirst ? _curveRightPrefab : _curveLeftPrefab);
                }
            }

            for (int i = 0; i < _maxPlacementAttempts; i++)
            {
                var straight = PickWeightedStraight(progress);
                if (straight != null) candidates.Add(straight);
            }

            LevelChunkInfo bestFallback     = null;
            Vector3        bestFallbackEnd  = cursorPos;
            float          bestFallbackDist = float.NegativeInfinity;

            EvaluateCandidates(candidates, cursorPos, cursorRot, forward, right,
                                ref bestFallback, ref bestFallbackEnd, ref bestFallbackDist, out var accepted, out var acceptedEnd);
            if (accepted != null) { chosenEnd = acceptedEnd; return accepted; }

            // Ningún candidato normal evitó el cruce. Si encima el "menos malo" se
            // solapa de verdad con la pista (no solo queda algo cerca), la pista está
            // atrapada: probamos una pasada de emergencia con AMBAS curvas (sin el
            // filtro probabilístico _curveChance/_minStraightsBetweenCurves, que en la
            // mayoría de los casos ni siquiera proponía una curva como salida) y más
            // rectos, antes de resignarnos a aceptar un cruce real.
            bool physicallyOverlapping = bestFallbackDist < _roadHalfWidth * 2f;
            if (physicallyOverlapping && _curveLeftPrefab != null && _curveRightPrefab != null)
            {
                var emergencyCandidates = new List<LevelChunkInfo> { _curveLeftPrefab, _curveRightPrefab };
                for (int i = 0; i < _maxPlacementAttempts * 2; i++)
                {
                    var straight = PickWeightedStraight(progress);
                    if (straight != null) emergencyCandidates.Add(straight);
                }

                EvaluateCandidates(emergencyCandidates, cursorPos, cursorRot, forward, right,
                                    ref bestFallback, ref bestFallbackEnd, ref bestFallbackDist, out accepted, out acceptedEnd);
                if (accepted != null) { chosenEnd = acceptedEnd; return accepted; }
            }

            chosenEnd = bestFallbackEnd;
            return bestFallback;
        }

        /// <summary>
        /// Evalúa una lista de candidatos: si alguno libra el cruce (minDist >= _minClearance)
        /// lo devuelve de inmediato en <paramref name="accepted"/>; si no, va actualizando el
        /// mejor "menos malo" visto hasta ahora en los parámetros bestFallback* (por referencia,
        /// para poder encadenar varias pasadas de candidatos sin perder el mejor global).
        /// </summary>
        private void EvaluateCandidates(List<LevelChunkInfo> candidates, Vector3 cursorPos, Quaternion cursorRot,
                                         Vector3 forward, Vector3 right,
                                         ref LevelChunkInfo bestFallback, ref Vector3 bestFallbackEnd, ref float bestFallbackDist,
                                         out LevelChunkInfo accepted, out Vector3 acceptedEnd)
        {
            accepted    = null;
            acceptedEnd = cursorPos;

            foreach (var candidate in candidates)
            {
                Vector3 end = cursorPos + forward * candidate.ForwardLength + right * candidate.LateralOffset;

                float minDist;
                if (Mathf.Approximately(candidate.TurnAngleY, 0f))
                {
                    // Tramo recto: la línea entrada→salida es la geometría real, sin aproximación.
                    minDist = MinDistanceToExisting(cursorPos, end);
                }
                else
                {
                    // Curva: el arco real se abomba hacia afuera respecto a la cuerda
                    // entrada→salida — se revisan las dos mitades pasando por el punto
                    // medio real del arco para no dejar pasar cruces por ese bulto.
                    Vector3 mid = ComputeArcMidpoint(cursorPos, cursorRot, candidate.ForwardLength, candidate.TurnAngleY);
                    minDist = Mathf.Min(MinDistanceToExisting(cursorPos, mid), MinDistanceToExisting(mid, end));
                }

                if (minDist >= _minClearance)
                {
                    accepted    = candidate;
                    acceptedEnd = end;
                    return;
                }

                // Ningún candidato libre de cruce todavía — nos quedamos con el que
                // deje MÁS distancia (el "menos malo"), no el primero evaluado.
                if (minDist > bestFallbackDist)
                {
                    bestFallbackDist = minDist;
                    bestFallback     = candidate;
                    bestFallbackEnd  = end;
                }
            }
        }

        /// <summary>
        /// Punto medio real del arco de una curva (no el punto medio de la cuerda
        /// entrada→salida), asumiendo que forma parte de un arco circular consistente
        /// con su ForwardLength/TurnAngleY. Se usa solo para el chequeo anti-cruce.
        /// </summary>
        private static Vector3 ComputeArcMidpoint(Vector3 entryPos, Quaternion entryRot, float forwardLength, float turnAngleY)
        {
            float thetaRad = Mathf.Abs(turnAngleY) * Mathf.Deg2Rad;
            float radius    = forwardLength / Mathf.Sin(thetaRad);
            float halfTheta = thetaRad * 0.5f;

            float localX = Mathf.Sign(turnAngleY) * radius * (1f - Mathf.Cos(halfTheta));
            float localZ = radius * Mathf.Sin(halfTheta);

            return entryPos + entryRot * new Vector3(localX, 0f, localZ);
        }

        /// <summary>
        /// Posición y rotación en cualquier punto (t=0..1) del arco circular real de una curva,
        /// generalizando <see cref="ComputeArcMidpoint"/>. Se usa para muestrear decoración
        /// (edificios, suelo) siguiendo la geometría real en vez de la cuerda entrada→salida.
        /// </summary>
        private static (Vector3 pos, Quaternion rot) ComputeArcPoint(Vector3 entryPos, Quaternion entryRot,
                                                                        float forwardLength, float turnAngleY, float t)
        {
            float thetaRad = Mathf.Abs(turnAngleY) * Mathf.Deg2Rad;
            float radius    = forwardLength / Mathf.Sin(thetaRad);
            float angle     = thetaRad * t;

            float localX = Mathf.Sign(turnAngleY) * radius * (1f - Mathf.Cos(angle));
            float localZ = radius * Mathf.Sin(angle);

            Vector3    pos = entryPos + entryRot * new Vector3(localX, 0f, localZ);
            Quaternion rot = entryRot * Quaternion.Euler(0f, turnAngleY * t, 0f);
            return (pos, rot);
        }

        /// <summary>Distancia mínima entre el segmento candidato y cualquier tramo ya colocado (no adyacente).</summary>
        private float MinDistanceToExisting(Vector3 start, Vector3 end)
        {
            Vector2 a = new Vector2(start.x, start.z);
            Vector2 b = new Vector2(end.x, end.z);

            // Solo el punto de unión con la pieza anterior es "adyacente/tocante por
            // construcción" — se excluye un buffer pequeño y fijo (no toda la pieza
            // anterior: con la resolución fina del arco de las curvas —ver arcSegments
            // en Generate()— excluir la curva entera dejaría un hueco real de detección
            // en el resto de su recorrido, que sí puede llegar a cruzarse con una pieza
            // nueva en pistas muy curvas).
            int checkCount = Mathf.Max(0, _placedSegments.Count - 3);

            float minDist = float.PositiveInfinity;
            for (int i = 0; i < checkCount; i++)
            {
                var (segA, segB) = _placedSegments[i];
                Vector2 c = new Vector2(segA.x, segA.z);
                Vector2 d = new Vector2(segB.x, segB.z);
                minDist = Mathf.Min(minDist, SegmentDistance2D(a, b, c, d));
            }
            return minDist;
        }

        /// <summary>Distancia mínima entre dos segmentos 2D (proyección XZ).</summary>
        private static float SegmentDistance2D(Vector2 p1, Vector2 q1, Vector2 p2, Vector2 q2)
        {
            Vector2 d1 = q1 - p1;
            Vector2 d2 = q2 - p2;
            Vector2 r  = p1 - p2;

            float a = Vector2.Dot(d1, d1);
            float e = Vector2.Dot(d2, d2);
            float f = Vector2.Dot(d2, r);

            const float eps = 1e-6f;
            float s, t;

            if (a <= eps && e <= eps)
            {
                return Vector2.Distance(p1, p2);
            }
            if (a <= eps)
            {
                s = 0f;
                t = Mathf.Clamp01(f / e);
            }
            else
            {
                float c = Vector2.Dot(d1, r);
                if (e <= eps)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-c / a);
                }
                else
                {
                    float b     = Vector2.Dot(d1, d2);
                    float denom = a * e - b * b;
                    s = denom != 0f ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f)      { t = 0f; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
                }
            }

            Vector2 c1 = p1 + s * d1;
            Vector2 c2 = p2 + t * d2;
            return Vector2.Distance(c1, c2);
        }

        /// <summary>Coloca una capa de edificios a ambos lados de un tramo recto, mirando hacia el camino.</summary>
        private void ScatterAlongStraight(Vector3 origin, Quaternion rot, float length,
                                           GameObject[] pool, float spacing, Vector2 distRange, float chance)
        {
            if (pool == null || pool.Length == 0) return;

            Vector3 forward = rot * Vector3.forward;
            Vector3 right   = rot * Vector3.right;

            int slots = Mathf.Max(1, Mathf.RoundToInt(length / spacing));
            for (int i = 0; i < slots; i++)
            {
                float   t        = (i + 0.5f) / slots;
                Vector3 alongPos = origin + forward * (t * length);
                PlaceBuildingPair(alongPos, forward, right, pool, distRange, chance);
            }
        }

        /// <summary>
        /// Coloca una capa de edificios a lo largo de una curva, muestreando el arco real
        /// con el mismo espaciado por longitud que <see cref="ScatterAlongStraight"/> —
        /// antes solo se muestreaban 3 puntos fijos (entrada/mitad/salida) sin importar
        /// la longitud del arco, dejando las curvas visiblemente menos densas que los rectos.
        /// </summary>
        private void ScatterAlongCurve(Vector3 entryPos, Quaternion entryRot, float forwardLength, float turnAngleY,
                                        GameObject[] pool, float spacing, Vector2 distRange, float chance)
        {
            if (pool == null || pool.Length == 0) return;

            float arcLength = ArcLength(forwardLength, turnAngleY);
            int   slots     = Mathf.Max(3, Mathf.CeilToInt(arcLength / spacing));

            for (int i = 0; i < slots; i++)
            {
                float t = (i + 0.5f) / slots;
                var (pos, rot) = ComputeArcPoint(entryPos, entryRot, forwardLength, turnAngleY, t);
                PlaceBuildingPair(pos, rot * Vector3.forward, rot * Vector3.right, pool, distRange, chance);
            }
        }

        private static float ArcLength(float forwardLength, float turnAngleY)
        {
            float thetaRad = Mathf.Abs(turnAngleY) * Mathf.Deg2Rad;
            float radius    = forwardLength / Mathf.Sin(thetaRad);
            return radius * thetaRad;
        }

        private void PlaceBuildingPair(Vector3 alongPos, Vector3 forward, Vector3 right, GameObject[] pool, Vector2 distRange, float chance)
        {
            foreach (int side in new[] { -1, 1 })
            {
                if (Random.value > chance) continue;

                var   prefab = pool[Random.Range(0, pool.Length)];
                float radius = GetFootprintRadius(prefab);

                // Nunca invade el camino, sin importar lo que haya salido del random.
                float minDistFromRoad = _roadHalfWidth + radius + _buildingMargin;
                float baseDist = Mathf.Max(Random.Range(distRange.x, distRange.y), minDistFromRoad);

                Vector3 pos = default;
                bool    placed = false;

                // Búsqueda local tipo espiral: en vez de solo retroceder en línea recta
                // desde el camino (lo que deja huecos cuando ese carril está bloqueado
                // en toda su profundidad, p.ej. por otro edificio), también se prueban
                // desplazamientos hacia adelante/atrás a lo largo del camino. Esto
                // encuentra espacio libre cercano en vez de rendirse o alejarse demasiado.
                for (int attempt = 0; attempt < _buildingPlacementAttempts; attempt++)
                {
                    float alongJitter = 0f;
                    float distJitter  = 0f;
                    if (attempt > 0)
                    {
                        int   pairIndex = (attempt - 1) / 2;
                        float alongSign = (attempt % 2 == 1) ? 1f : -1f;
                        alongJitter = alongSign * radius * (pairIndex + 1);
                        distJitter  = pairIndex * radius * 0.5f;
                    }

                    Vector3 candidate = alongPos + forward * alongJitter + right * (side * (baseDist + distJitter));

                    // Chequeo contra la geometría REAL del camino (no solo la distancia
                    // asumida desde este punto) — cerca de curvas, el camino puede pasar
                    // más cerca de lo que este único punto de muestreo sugiere.
                    bool tooCloseToRoad = DistanceToRoad(candidate) < _roadHalfWidth + radius + _buildingMargin;

                    if (!tooCloseToRoad && !OverlapsPlacedBuilding(candidate, radius))
                    {
                        pos    = candidate;
                        placed = true;
                        break;
                    }
                }

                if (!placed) continue; // no hubo lugar libre cerca — se deja el hueco antes que solapar

                Vector3    faceDir     = -side * right; // mira hacia el camino
                float      jitter      = Random.Range(_buildingYRotationJitter.x, _buildingYRotationJitter.y);
                Quaternion buildingRot = Quaternion.LookRotation(faceDir, Vector3.up) * Quaternion.Euler(0f, jitter, 0f);

                var instance = Instantiate(prefab, pos, buildingRot, _cityRoot);
                _placedBuildings.Add((pos, radius, instance));
            }
        }

        /// <summary>Coloca placas de suelo centradas en el camino a lo largo de un tramo recto.</summary>
        private void ScatterGroundAlongStraight(Vector3 origin, Quaternion rot, float length)
        {
            if (_groundPlatePrefab == null) return;

            Vector3 forward   = rot * Vector3.forward;
            Vector3 yOffset   = Vector3.up * _groundPlateYOffset;
            int     slots     = Mathf.Max(1, Mathf.RoundToInt(length / _groundPlateSpacing));
            for (int i = 0; i < slots; i++)
            {
                float t = (i + 0.5f) / slots;
                Instantiate(_groundPlatePrefab, origin + forward * (t * length) + yOffset, rot, _cityRoot);
            }
        }

        /// <summary>Coloca placas de suelo a lo largo del arco real de una curva. Usa un espaciado propio (más denso que en rectos, ver <see cref="_curveSpacingFactor"/>) porque las placas rectangulares se abren en abanico al seguir el arco.</summary>
        private void ScatterGroundAlongCurve(Vector3 entryPos, Quaternion entryRot, float forwardLength, float turnAngleY, float spacing)
        {
            if (_groundPlatePrefab == null) return;

            Vector3 yOffset   = Vector3.up * _groundPlateYOffset;
            float   arcLength = ArcLength(forwardLength, turnAngleY);
            int     slots     = Mathf.Max(3, Mathf.CeilToInt(arcLength / spacing));

            for (int i = 0; i < slots; i++)
            {
                float t = (i + 0.5f) / slots;
                var (pos, rot) = ComputeArcPoint(entryPos, entryRot, forwardLength, turnAngleY, t);
                Instantiate(_groundPlatePrefab, pos + yOffset, rot, _cityRoot);
            }
        }

        /// <summary>Radio (XZ, ignora altura) del prefab, medido una vez e instanciando temporalmente. Se cachea por prefab.</summary>
        private float GetFootprintRadius(GameObject prefab)
        {
            if (_footprintRadiusCache.TryGetValue(prefab, out float cached)) return cached;

            float radius = 1f;
            var   temp   = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            var   renderers = temp.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                // Radio de la circunferencia que cubre la huella XZ — conservador ante rotaciones.
                radius = new Vector2(b.size.x, b.size.z).magnitude * 0.5f;
            }
            Destroy(temp);

            _footprintRadiusCache[prefab] = radius;
            return radius;
        }

        /// <summary>Distancia real del punto a la pista ya colocada (todos los sub-segmentos, incluidas curvas).</summary>
        private float DistanceToRoad(Vector3 pos)
        {
            Vector2 p = new Vector2(pos.x, pos.z);
            float   minDist = float.PositiveInfinity;

            for (int i = 0; i < _placedSegments.Count; i++)
            {
                var (a, b) = _placedSegments[i];
                Vector2 segA = new Vector2(a.x, a.z);
                Vector2 segB = new Vector2(b.x, b.z);
                Vector2 ab   = segB - segA;
                float   t    = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - segA, ab) / ab.sqrMagnitude) : 0f;
                float   d    = Vector2.Distance(p, segA + t * ab);
                if (d < minDist) minDist = d;
            }
            return minDist;
        }

        private bool OverlapsPlacedBuilding(Vector3 pos, float radius)
        {
            for (int i = 0; i < _placedBuildings.Count; i++)
            {
                var (otherPos, otherRadius, _) = _placedBuildings[i];
                float minDist = radius + otherRadius + _buildingMargin;
                if ((pos - otherPos).sqrMagnitude < minDist * minDist) return true;
            }
            return false;
        }

        /// <summary>Si el prefab de un chunk tiene algún hijo "Obstacle_*" (se cachea por prefab, no cambia entre llamadas).</summary>
        private bool HasObstacles(LevelChunkInfo candidate)
        {
            if (_hasObstaclesCache.TryGetValue(candidate, out bool cached)) return cached;

            bool has = false;
            foreach (Transform child in candidate.transform)
                if (child.name.StartsWith("Obstacle_")) { has = true; break; }

            _hasObstaclesCache[candidate] = has;
            return has;
        }

        private LevelChunkInfo PickWeightedStraight(float progress)
        {
            if (_straightChunks == null || _straightChunks.Length == 0) return null;

            float totalWeight = 0f;
            var weights = new float[_straightChunks.Length];

            for (int i = 0; i < _straightChunks.Length; i++)
            {
                var candidate = _straightChunks[i];
                weights[i] = TierWeight(candidate.DifficultyTier, progress);

                // Zona segura de arranque: mientras no se recorra _safeStartDistance, se
                // excluyen los chunks con obstáculos — el jugador necesita ver el camino y
                // acostumbrarse antes de que aparezca el primero.
                if (progress * _totalTrackLength < _safeStartDistance && HasObstacles(candidate))
                    weights[i] = 0f;

                // Cooldown de rareza: si este prefab se usó hace menos de MinSpacingFromSameKind
                // chunks rectos, se excluye del sorteo esta vez (peso 0).
                if (candidate.MinSpacingFromSameKind > 0)
                {
                    int sincePlaced = _straightsSincePlaced.TryGetValue(candidate, out int v) ? v : int.MaxValue;
                    if (sincePlaced < candidate.MinSpacingFromSameKind) weights[i] = 0f;
                }

                totalWeight += weights[i];
            }

            if (totalWeight <= 0f) return _straightChunks[Random.Range(0, _straightChunks.Length)];

            float roll = Random.value * totalWeight;
            for (int i = 0; i < _straightChunks.Length; i++)
            {
                roll -= weights[i];
                if (roll <= 0f) return _straightChunks[i];
            }
            return _straightChunks[_straightChunks.Length - 1];
        }

        /// <summary>Curva de peso por dificultad a lo largo del % de pista recorrida.</summary>
        private static float TierWeight(int tier, float progress)
        {
            switch (tier)
            {
                case 0:  return Mathf.Lerp(1.0f, 0.3f, progress);
                case 1:  return Mathf.Clamp01((progress - 0.15f) / 0.35f);
                default: return Mathf.Clamp01((progress - 0.55f) / 0.35f) * 1.2f;
            }
        }
    }
}

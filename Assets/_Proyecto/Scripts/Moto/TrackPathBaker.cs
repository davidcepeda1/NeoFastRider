using System.Collections.Generic;
using UnityEngine;

namespace NeoFastRider.Moto
{
    /// <summary>
    /// Construye un path preciso proyectando puntos sobre los MeshColliders
    /// reales del Road Pack (capa "Road") mediante raycast.
    ///
    /// Se ejecuta una vez en Start(). El resultado es un array de PathPoint
    /// con posición, normal y forward exactos sobre la superficie del asfalto.
    ///
    /// Dibuja Gizmos en Scene View para verificación visual antes del Play.
    /// </summary>
    public sealed class TrackPathBaker : MonoBehaviour
    {
        [Header("Track")]
        [Tooltip("El GameObject [Track_System] con los segmentos.")]
        [SerializeField] private Transform _trackSystem;

        [Header("Bake Settings")]
        [Tooltip("Altura del rider sobre la superficie del asfalto.")]
        [SerializeField] private float _riderHeight = 0.4f;

        [Tooltip("Distancia máxima entre puntos del path. Menor = más denso.")]
        [SerializeField] private float _maxPointSpacing = 3.5f;

        [Tooltip("Altura desde la que se lanza el raycast de proyección.")]
        [SerializeField] private float _rayHeight = 30f;

        [Header("Debug")]
        [SerializeField] private bool _drawGizmos = true;

        // ── Path Data ─────────────────────────────────────────────────────────
        public struct PathPoint
        {
            public Vector3 position;
            public Vector3 normal;
            public Vector3 forward;
            public float   cumDist;
        }

        private PathPoint[] _path;
        private float       _totalLength;
        private bool        _baked;

        public PathPoint[] Path        => _path;
        public float       TotalLength => _totalLength;
        public bool        IsBaked     => _baked;

        // ── API pública ───────────────────────────────────────────────────────

        /// <summary>Construye el path. Llamar desde Start() del controlador.</summary>
        public void Bake()
        {
            if (_trackSystem == null)
            {
                Debug.LogError("[TrackPathBaker] _trackSystem no asignado.");
                return;
            }

            int roadMask = LayerMask.GetMask("Road");
            if (roadMask == 0)
            {
                Debug.LogError("[TrackPathBaker] Capa 'Road' no existe.");
                return;
            }

            // ── PASO 1: puntos de aproximación desde los bounds ──────────────
            var approx = new List<Vector3>();
            Vector3 lastPt = Vector3.zero;
            bool first = true;

            for (int s = 0; s < _trackSystem.childCount; s++)
            {
                var seg = _trackSystem.GetChild(s);
                for (int c = 0; c < seg.childCount; c++)
                {
                    var mr = seg.GetChild(c).GetComponent<MeshRenderer>()
                          ?? seg.GetChild(c).GetComponentInChildren<MeshRenderer>();
                    if (mr == null) continue;

                    var b = mr.bounds;
                    float spanX = b.size.x, spanZ = b.size.z;
                    bool  large = spanX > 15f || spanZ > 15f;

                    if (!large)
                    {
                        approx.Add(b.center);
                    }
                    else
                    {
                        // Entrada = esquina más cercana al último punto
                        Vector2 last2 = new Vector2(lastPt.x, lastPt.z);
                        Vector2[] corners =
                        {
                            new Vector2(b.min.x, b.min.z),
                            new Vector2(b.max.x, b.min.z),
                            new Vector2(b.min.x, b.max.z),
                            new Vector2(b.max.x, b.max.z),
                        };
                        int entryIdx = 0; float minD = float.MaxValue;
                        if (!first)
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                float d = Vector2.Distance(last2, corners[i]);
                                if (d < minD) { minD = d; entryIdx = i; }
                            }
                        }
                        int exitIdx = 3 - entryIdx;

                        // Entrada → centro → salida, con inset para no caer fuera
                        Vector2 en = Vector2.Lerp(corners[entryIdx], new Vector2(b.center.x, b.center.z), 0.15f);
                        Vector2 ex = Vector2.Lerp(corners[exitIdx],  new Vector2(b.center.x, b.center.z), 0.15f);

                        approx.Add(new Vector3(en.x, b.center.y, en.y));
                        approx.Add(b.center);
                        approx.Add(new Vector3(ex.x, b.center.y, ex.y));
                    }

                    if (approx.Count > 0) { lastPt = approx[approx.Count - 1]; first = false; }
                }
            }

            if (approx.Count < 2)
            {
                Debug.LogError("[TrackPathBaker] Puntos de aproximación insuficientes.");
                return;
            }

            // ── PASO 2: densificación previa ─────────────────────────────────
            var dense = new List<Vector3>();
            for (int i = 0; i < approx.Count - 1; i++)
            {
                dense.Add(approx[i]);
                float d = Vector3.Distance(approx[i], approx[i + 1]);
                int   n = Mathf.FloorToInt(d / _maxPointSpacing);
                for (int j = 1; j <= n; j++)
                    dense.Add(Vector3.Lerp(approx[i], approx[i + 1], j / (float)(n + 1)));
            }
            dense.Add(approx[approx.Count - 1]);

            // ── PASO 3: proyección física sobre la capa Road ─────────────────
            var projected = new List<(Vector3 pos, Vector3 nrm)>();
            foreach (var p in dense)
            {
                Ray ray = new Ray(p + Vector3.up * _rayHeight, Vector3.down);
                if (Physics.Raycast(ray, out RaycastHit hit, _rayHeight * 2f, roadMask))
                {
                    Vector3 pos = hit.point + hit.normal * _riderHeight;
                    // Evitar duplicados muy cercanos
                    if (projected.Count == 0 ||
                        Vector3.Distance(projected[projected.Count - 1].pos, pos) > 0.5f)
                        projected.Add((pos, hit.normal));
                }
                // Si el ray no golpea (punto fuera del asfalto) se descarta —
                // la densificación garantiza que haya suficientes puntos válidos
            }

            if (projected.Count < 2)
            {
                Debug.LogError("[TrackPathBaker] La proyección no encontró superficie Road.");
                return;
            }

            // ── PASO 4: construir PathPoints con forward y distancias ────────
            int count = projected.Count;
            _path = new PathPoint[count];

            for (int i = 0; i < count; i++)
            {
                Vector3 fwd;
                if (i == 0)          fwd = (projected[1].pos     - projected[0].pos).normalized;
                else if (i == count - 1) fwd = (projected[i].pos - projected[i - 1].pos).normalized;
                else                 fwd = (projected[i + 1].pos - projected[i - 1].pos).normalized;

                _path[i] = new PathPoint
                {
                    position = projected[i].pos,
                    normal   = projected[i].nrm,
                    forward  = fwd,
                    cumDist  = i == 0 ? 0f :
                        _path[i - 1].cumDist +
                        Vector3.Distance(projected[i - 1].pos, projected[i].pos)
                };
            }

            _totalLength = _path[count - 1].cumDist;
            _baked       = true;

            Debug.Log($"[TrackPathBaker] Path baked: {count} puntos, {_totalLength:F0} unidades.");
        }

        /// <summary>
        /// Devuelve posición/normal/forward interpolados a la distancia dada.
        /// </summary>
        public void Sample(float dist, out Vector3 pos, out Vector3 normal, out Vector3 forward)
        {
            dist = Mathf.Clamp(dist, 0f, _totalLength);

            // Búsqueda binaria
            int lo = 0, hi = _path.Length - 2;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (_path[mid].cumDist <= dist) lo = mid;
                else hi = mid - 1;
            }
            int nxt = Mathf.Min(lo + 1, _path.Length - 1);

            float segLen = _path[nxt].cumDist - _path[lo].cumDist;
            float t      = segLen > 0.0001f ? (dist - _path[lo].cumDist) / segLen : 0f;

            pos     = Vector3.Lerp (_path[lo].position, _path[nxt].position, t);
            normal  = Vector3.Slerp(_path[lo].normal,   _path[nxt].normal,   t).normalized;
            forward = Vector3.Slerp(_path[lo].forward,  _path[nxt].forward,  t).normalized;
        }

        // ── Gizmos ────────────────────────────────────────────────────────────
        private void OnDrawGizmos()
        {
            if (!_drawGizmos || !_baked || _path == null) return;

            Gizmos.color = Color.cyan;
            for (int i = 0; i < _path.Length - 1; i++)
                Gizmos.DrawLine(_path[i].position, _path[i + 1].position);

            Gizmos.color = Color.magenta;
            for (int i = 0; i < _path.Length; i += 4)
                Gizmos.DrawRay(_path[i].position, _path[i].normal * 1.5f);
        }
    }
}

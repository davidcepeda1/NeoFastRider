using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NeoFastRider.Moto
{
    [AddComponentMenu("NeoFastRider/Moto/Player Shield Controller")]
    public sealed class PlayerShieldController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────
        [Header("Duración")]
        [SerializeField] private float shieldDuration   = 3f;
        [SerializeField] private float flickerThreshold = 1.5f;
        [SerializeField] private float flickerInterval  = 0.08f;

        [Header("Geometría del escudo")]
        [SerializeField] private float shieldRadius  = 2f;
        [SerializeField] private float hexSize       = 0.55f;
        [SerializeField] private int   hexCount      = 30;
        [SerializeField] private float centerYOffset = 1.4f;
        [SerializeField] private float scaleInTime   = 0.3f;

        [Header("Color")]
        [SerializeField] private Color hexColor = new Color(0.1f, 0.9f, 1f, 1f);

        [Header("Cooldown")]
        [SerializeField] private float activationCooldown = 1f;

        [Header("Partículas al destruir obstáculo (opcional)")]
        [SerializeField] private GameObject obstacleBurstPrefab;

        // ── Estado público ────────────────────────────────────────────────────────
        public bool IsShieldActive  { get; private set; }
        public bool HasShieldCharge => _shieldCharges > 0;

        // ── Privado ───────────────────────────────────────────────────────────────
        private PlayerHealth                          _playerHealth;
        private NeoFastRider.UI.HelmetVisorController _hud;
        private GameObject                            _shieldRoot;
        private List<MeshRenderer>                    _panels = new List<MeshRenderer>();
        private Material                              _hexMat;
        private Coroutine                             _routine;
        private float                                 _cooldown;
        private int                                   _shieldCharges;

        // Buffer reutilizable para OverlapSphere (cero GC)
        private readonly Collider[] _overlapBuffer = new Collider[32];
        // Escanea en Default + Obstacle layer — los obstáculos están en Default con tag "Obstacle"
        private static readonly int _scanMask = ~0; // todas las capas; filtramos por tag en el loop

        // ─────────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _playerHealth = GetComponent<PlayerHealth>();
            _hud          = Object.FindObjectOfType<NeoFastRider.UI.HelmetVisorController>();
            BuildHoneycombShield();
        }

        private void Update()
        {
            if (_cooldown > 0f) _cooldown -= Time.deltaTime;
            if (UnityEngine.Input.GetKeyDown(KeyCode.E)) TryActivateShield();
            if (IsShieldActive) ScanAndDestroyObstacles();
        }

        // ── Detección de obstáculos por OverlapSphere ─────────────────────────────
        // Escanea todas las capas y filtra por tag "Obstacle" — independiente de layer matrix.

        private void ScanAndDestroyObstacles()
        {
            if (_shieldRoot == null) return;

            Vector3 center = _shieldRoot.transform.position;
            int count = Physics.OverlapSphereNonAlloc(
                center, shieldRadius, _overlapBuffer, _scanMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                var col = _overlapBuffer[i];
                if (col == null) continue;
                // Filtrar solo objetos con tag "Obstacle"
                if (!col.CompareTag("Obstacle") && !col.gameObject.CompareTag("Obstacle")) continue;
                DestroyObstacle(col);
            }
        }

        private void DestroyObstacle(Collider col)
        {
            // Deshabilitar el collider inmediatamente para que la moto pase sin chocar
            col.enabled = false;

            if (obstacleBurstPrefab != null)
                Instantiate(obstacleBurstPrefab, col.bounds.center, Quaternion.identity);

            var dissolve = col.GetComponentInParent<NeoFastRider.Environment.DissolveObstacle>();
            if (dissolve != null)
            {
                dissolve.Dissolve();
            }
            else
            {
                Destroy(col.gameObject, 0.15f);
            }

            Debug.Log($"[Shield] Obstáculo eliminado: {col.gameObject.name}", this);
        }

        // ── Construcción del escudo ───────────────────────────────────────────────

        private void BuildHoneycombShield()
        {
            var old = transform.Find("ShieldRoot");
            if (old != null) Destroy(old.gameObject);
            _panels.Clear();

            _shieldRoot = new GameObject("ShieldRoot");
            _shieldRoot.transform.SetParent(transform, false);
            _shieldRoot.transform.localPosition = new Vector3(0f, centerYOffset, 0f);
            _shieldRoot.transform.localScale    = Vector3.zero;

            // Material aditivo — visible desde cualquier ángulo, jamás oscurece la pantalla
            _hexMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Mat_HexShield" };
            _hexMat.SetFloat("_Surface", 1f);
            _hexMat.SetFloat("_Blend",   2f);
            _hexMat.SetInt("_SrcBlend",  (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _hexMat.SetInt("_DstBlend",  (int)UnityEngine.Rendering.BlendMode.One);
            _hexMat.SetInt("_ZWrite",    0);
            _hexMat.SetInt("_Cull",      0);
            _hexMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _hexMat.renderQueue = 3500;
            _hexMat.SetColor("_BaseColor", hexColor);

            Mesh hexMesh = CreateHexMesh(hexSize);

            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < hexCount; i++)
            {
                float y   = 1f - (i / (hexCount - 1f)) * 2f;
                float r   = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                float th  = golden * i;
                var   dir = new Vector3(Mathf.Cos(th) * r, y, Mathf.Sin(th) * r).normalized;

                var panel = new GameObject($"Hex_{i}");
                panel.transform.SetParent(_shieldRoot.transform, false);
                panel.transform.localPosition = dir * shieldRadius;
                panel.transform.localRotation = Quaternion.LookRotation(dir, Vector3.up);

                var mf = panel.AddComponent<MeshFilter>();
                mf.sharedMesh = hexMesh;

                var mr = panel.AddComponent<MeshRenderer>();
                mr.sharedMaterial    = _hexMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows    = false;
                _panels.Add(mr);
            }

            _shieldRoot.SetActive(false);
            Debug.Log($"[Shield] Honeycomb listo: {hexCount} paneles, radio {shieldRadius}m.", this);
        }

        private static Mesh CreateHexMesh(float size)
        {
            var verts = new Vector3[7];
            verts[0] = Vector3.zero;
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                verts[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * size;
            }

            var tris = new int[36];
            for (int i = 0; i < 6; i++)
            {
                int a = i + 1;
                int b = (i + 1) % 6 + 1;
                tris[i * 3]      = 0; tris[i * 3 + 1]      = a; tris[i * 3 + 2]      = b;
                tris[18 + i * 3] = 0; tris[18 + i * 3 + 1] = b; tris[18 + i * 3 + 2] = a;
            }

            var mesh = new Mesh { name = "HexPanel" };
            mesh.vertices  = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ── API pública ───────────────────────────────────────────────────────────

        public void GrantShield()
        {
            _shieldCharges++;
            Debug.Log($"[Shield] Carga recibida. Disponibles: {_shieldCharges}", this);
        }

        /// <summary>
        /// Llamado por DroneCollisionHandler cuando una embestida alcanza al jugador con escudo activo.
        /// Destruye al dron usando su propia explosión configurada; el escudo SIGUE activo.
        /// </summary>
        public void AbsorbImpact(GameObject enemy)
        {
            if (!IsShieldActive || enemy == null) return;

            var handler = enemy.GetComponent<NeoFastRider.Enemies.DroneCollisionHandler>();
            if (handler != null)
                handler.SpawnExplosionAndDestroy();
            else
                Destroy(enemy);

            Debug.Log("[Shield] ¡Embestida absorbida! El escudo sigue activo.", this);
        }

        // ── Activación ────────────────────────────────────────────────────────────

        private void TryActivateShield()
        {
            if (IsShieldActive)      { Debug.Log("[Shield] Ya activo.", this); return; }
            if (_cooldown > 0f)      { Debug.Log($"[Shield] Cooldown {_cooldown:F1}s", this); return; }
            if (_shieldCharges <= 0) { Debug.Log("[Shield] Sin carga. Recoge el cubo azul.", this); return; }

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(ShieldRoutine());
        }

        private IEnumerator ShieldRoutine()
        {
            _shieldCharges--;
            IsShieldActive = true;
            _shieldRoot.SetActive(true);

            // Escala-in
            float t = 0f;
            while (t < scaleInTime)
            {
                t += Time.deltaTime;
                float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / scaleInTime));
                _shieldRoot.transform.localScale = Vector3.one * s;
                yield return null;
            }
            _shieldRoot.transform.localScale = Vector3.one;

            // Duración + parpadeo final
            float elapsed = 0f;
            float fTimer  = 0f;
            bool  fState  = true;

            while (elapsed < shieldDuration)
            {
                elapsed += Time.deltaTime;

                if (shieldDuration - elapsed <= flickerThreshold)
                {
                    fTimer += Time.deltaTime;
                    if (fTimer >= flickerInterval)
                    {
                        fTimer = 0f;
                        fState = !fState;
                        foreach (var mr in _panels) mr.enabled = fState;
                    }
                }

                yield return null;
            }

            Deactivate();
        }

        private void Deactivate()
        {
            IsShieldActive = false;
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            if (_shieldRoot != null)
            {
                foreach (var mr in _panels) mr.enabled = true;
                _shieldRoot.transform.localScale = Vector3.zero;
                _shieldRoot.SetActive(false);
            }
            _cooldown = activationCooldown;
        }

        private void OnDestroy()
        {
            if (_hexMat != null) Destroy(_hexMat);
        }
    }
}

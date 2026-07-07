using UnityEngine;
using UnityEngine.Rendering;

namespace NeoFastRider.Moto
{
    [AddComponentMenu("NeoFastRider/Moto/Player Pulse Weapon")]
    public sealed class PlayerPulseWeapon : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────
        [Header("HUD")]
        [SerializeField] private NeoFastRider.UI.HelmetVisorController[] _hudControllers;

        [Header("Energía")]
        [SerializeField] private float _drainDuration = 2.5f;

        [Header("Canon — Geometría")]
        [SerializeField]gi private Transform _muzzlePoint;
        [SerializeField] private float _starRadius     = 0.75f; // radio de la estrella
        [SerializeField] private float _rotationSpeed  = 500f;  // grados/s

        // ── Runtime ──────────────────────────────────────────────────────────
        private float          _laserEnergy;
        private Transform      _starRoot;
        private MeshRenderer   _starMR;
        private ParticleSystem _particles;

        public float LaserEnergy => _laserEnergy;

        // ────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            BuildWeapon();
        }

        private void Start()
        {
            if (_hudControllers == null || _hudControllers.Length == 0)
            {
                var found = Object.FindObjectsOfType(
                    typeof(NeoFastRider.UI.HelmetVisorController), true)
                    as NeoFastRider.UI.HelmetVisorController[];
                _hudControllers = found;
            }
            Debug.Log("[PlayerPulseWeapon] HUD controllers: " + _hudControllers.Length, this);
        }

        private void Update()
        {
            bool firing = UnityEngine.Input.GetKey(KeyCode.Space) && _laserEnergy > 0f;

            if (firing)
            {
                _laserEnergy = Mathf.Max(0f, _laserEnergy - Time.deltaTime / _drainDuration);
                SyncToHUD();
                // Estrella plana gira sobre su propio eje (hacia adelante = Z)
                _starRoot.Rotate(0f, 0f, _rotationSpeed * Time.deltaTime, Space.Self);
            }

            _starMR.enabled = firing;

            if (firing)
            {
                if (!_particles.isPlaying) _particles.Play();
            }
            else
            {
                if (_particles.isPlaying)
                    _particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        // ── API pública ──────────────────────────────────────────────────────
        public void SetLaserEnergy(float value)
        {
            _laserEnergy = Mathf.Clamp01(value);
            SyncToHUD();
            Debug.Log("[PlayerPulseWeapon] LaserEnergy = " + _laserEnergy, this);
        }

        // ────────────────────────────────────────────────────────────────────
        private void BuildWeapon()
        {
            Vector3 muzzleLocal = _muzzlePoint != null
                ? transform.InverseTransformPoint(_muzzlePoint.position)
                : new Vector3(0f, 0.1f, 1.8f);

            // ── 1. Estrella plana (starburst) ─────────────────────────────
            var starGO = new GameObject("PulseLaser_StarDisc");
            _starRoot = starGO.transform;
            _starRoot.SetParent(transform, false);
            _starRoot.localPosition = muzzleLocal;
            _starRoot.localRotation = Quaternion.identity;
            _starRoot.localScale    = new Vector3(_starRadius, _starRadius, 1f);

            var mf  = starGO.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildFlatStarMesh(points: 6, innerRatio: 0.42f);

            _starMR = starGO.AddComponent<MeshRenderer>();
            _starMR.sharedMaterial    = BuildStarMaterial();
            _starMR.shadowCastingMode = ShadowCastingMode.Off;
            _starMR.receiveShadows    = false;
            _starMR.enabled           = false;

            // ── 2. Partículas (beam hacia adelante) ───────────────────────
            var vfxGO = new GameObject("VFX_PulseLaser_DataParticles");
            vfxGO.transform.SetParent(transform, false);
            vfxGO.transform.localPosition = muzzleLocal;
            vfxGO.transform.localRotation = Quaternion.identity;

            _particles = vfxGO.AddComponent<ParticleSystem>();
            SetupParticles(_particles);
        }

        // ── Estrella plana de 6 puntas, TODOS los vértices en Z=0 ────────────
        // Desde la cámara (detrás de la moto, mirando +Z) se ve de frente.
        // innerRatio: qué tan marcadas son las entrantes (0.3=muy aguda, 0.6=suave)
        private static Mesh BuildFlatStarMesh(int points = 6, float innerRatio = 0.42f)
        {
            // Sin compartir vértices → normals independientes por triángulo
            int triCount = points * 2;           // 2 tri por sector
            int vCount   = triCount * 3;         // sin degenerar
            var verts    = new Vector3[vCount];
            var colors   = new Color[vCount];
            var normals  = new Vector3[vCount];
            var tris     = new int[vCount];

            // Cámara está en -Z mirando +Z → cara visible = normal apuntando -Z
            var faceNormal = Vector3.back;

            int vi = 0;
            for (int i = 0; i < points; i++)
            {
                float aOuter  = (float)i / points * Mathf.PI * 2f;
                float aInner  = aOuter + Mathf.PI / points; // mitad entre punta y punta
                float aNextOuter = (float)(i + 1) / points * Mathf.PI * 2f;

                Vector3 outer     = new Vector3(Mathf.Cos(aOuter),     Mathf.Sin(aOuter),     0f);
                Vector3 inner     = new Vector3(Mathf.Cos(aInner)  * innerRatio,
                                                Mathf.Sin(aInner)  * innerRatio, 0f);
                Vector3 nextOuter = new Vector3(Mathf.Cos(aNextOuter), Mathf.Sin(aNextOuter), 0f);
                Vector3 center    = Vector3.zero;

                Color tipColor    = new Color(0f, 1f, 1f, 1f);   // cyan vivo en punta
                Color innerColor  = new Color(0f, 0.6f, 1f, 0.5f); // azul suave en entrante
                Color centerColor = new Color(0.2f, 1f, 1f, 0.9f); // cyan casi opaco en centro

                // Tri 1: centro → outer → inner
                verts[vi] = center; colors[vi] = centerColor; normals[vi] = faceNormal; tris[vi] = vi; vi++;
                verts[vi] = outer;  colors[vi] = tipColor;    normals[vi] = faceNormal; tris[vi] = vi; vi++;
                verts[vi] = inner;  colors[vi] = innerColor;  normals[vi] = faceNormal; tris[vi] = vi; vi++;

                // Tri 2: centro → inner → nextOuter
                verts[vi] = center;     colors[vi] = centerColor; normals[vi] = faceNormal; tris[vi] = vi; vi++;
                verts[vi] = inner;      colors[vi] = innerColor;  normals[vi] = faceNormal; tris[vi] = vi; vi++;
                verts[vi] = nextOuter;  colors[vi] = tipColor;    normals[vi] = faceNormal; tris[vi] = vi; vi++;
            }

            var mesh = new Mesh { name = "FlatStar6_PulseLaser" };
            mesh.vertices  = verts;
            mesh.colors    = colors;
            mesh.normals   = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material BuildStarMaterial()
        {
            var shd = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var mat = new Material(shd) { name = "Mat_StarLaser" };

            mat.SetColor("_BaseColor",  Color.white);
            mat.SetFloat("_ColorMode", 0f);   // Multiply → vertex colors mandan
            mat.SetFloat("_Surface",   1f);   // Transparent
            mat.SetFloat("_Blend",     2f);   // Additive
            mat.SetInt("_SrcBlend",    (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend",    (int)BlendMode.One);
            mat.SetInt("_ZWrite",      0);
            mat.SetInt("_Cull",        0);    // Cull Off — doble cara
            mat.renderQueue = (int)RenderQueue.Transparent + 1;

            return mat;
        }

        private static void SetupParticles(ParticleSystem ps)
        {
            var main = ps.main;
            main.loop            = true;
            main.playOnAwake     = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles    = 200;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(55f, 70f);
            main.startSize       = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
            main.startRotation   = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor      = new Color(0f, 1f, 1f, 1f);

            var emission = ps.emission;
            emission.enabled      = true;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(120f);

            var shape = ps.shape;
            shape.enabled         = true;
            shape.shapeType       = ParticleSystemShapeType.Cone;
            shape.angle           = 4f;
            shape.radius          = 0.05f;
            shape.radiusThickness = 1f;
            shape.rotation        = new Vector3(-90f, 0f, 0f);

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size    = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            var psr = ps.GetComponent<ParticleSystemRenderer>();
            psr.renderMode          = ParticleSystemRenderMode.Stretch;
            psr.lengthScale         = 7f;
            psr.velocityScale       = 0.25f;
            psr.cameraVelocityScale = 0f;
            psr.material            = BuildParticleMaterial();
            psr.shadowCastingMode   = ShadowCastingMode.Off;
            psr.receiveShadows      = false;
        }

        private static Material BuildParticleMaterial()
        {
            var shd = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var mat = new Material(shd) { name = "Mat_ParticleLaser" };

            mat.SetColor("_BaseColor",     new Color(0f, 1f, 1f, 1f));
            mat.SetColor("_EmissionColor", new Color(0f, 8f, 8f, 1f));
            mat.EnableKeyword("_EMISSION");
            mat.SetFloat("_ColorMode",     0f);
            mat.SetFloat("_Surface",       1f);
            mat.SetFloat("_Blend",         2f);
            mat.SetInt("_SrcBlend",        (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend",        (int)BlendMode.One);
            mat.SetInt("_ZWrite",          0);
            mat.SetInt("_Cull",            0);
            mat.renderQueue = (int)RenderQueue.Transparent + 1;

            return mat;
        }

        private void SyncToHUD()
        {
            if (_hudControllers == null) return;
            foreach (var ctrl in _hudControllers)
                ctrl?.SetLaserEnergy(_laserEnergy);
        }
    }
}

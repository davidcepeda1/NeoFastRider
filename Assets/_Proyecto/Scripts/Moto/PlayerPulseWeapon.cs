using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] private Transform _muzzlePoint;
        [SerializeField] private float _starRadius    = 0.75f;
        [SerializeField] private float _rotationSpeed = 500f;

        [Header("Raycast / Impacto")]
        [SerializeField] private float    _laserLength    = 30f;
        [SerializeField] private Material _impactMaterial;   // Effect_01.mat de Creepy Cat

        // ── Runtime ──────────────────────────────────────────────────────────
        private float          _laserEnergy;
        private Transform      _starRoot;
        private MeshRenderer   _starMR;
        private ParticleSystem _particles;
        private ParticleSystem _impactPS;      // burst principal (escala con el objeto)
        private ParticleSystem _shockwavePS;   // anillo expansivo (siempre grande)

        private readonly HashSet<int> _destroyingObstacles = new HashSet<int>();

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
                _starRoot.Rotate(0f, 0f, _rotationSpeed * Time.deltaTime, Space.Self);

                if (Physics.Raycast(_starRoot.position, transform.forward, out var hit, _laserLength))
                {
                    if (IsObstacle(hit.collider.transform))
                        TriggerImpact(hit.point, hit.collider.gameObject, hit.collider.bounds);
                }
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

        // ── Impacto ──────────────────────────────────────────────────────────
        private static bool IsObstacle(Transform t)
        {
            return t.parent != null && t.parent.name == "Obstacles";
        }

        private void TriggerImpact(Vector3 hitPoint, GameObject obstacle, Bounds bounds)
        {
            int id = obstacle.GetInstanceID();
            if (!obstacle.activeSelf || _destroyingObstacles.Contains(id)) return;

            _destroyingObstacles.Add(id);

            // ── Escala proporcional al tamaño del obstáculo ───────────────
            // extents.magnitude: barril≈0.6, caja≈0.8, pared≈1.5–3
            float rawRadius = bounds.extents.magnitude;
            float scale     = Mathf.Clamp(rawRadius, 0.4f, 3.5f);

            // ── Burst principal: ajustar parámetros dinámicamente ─────────
            _impactPS.transform.position = hitPoint;
            ApplyImpactScale(_impactPS, scale);
            _impactPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _impactPS.Play();

            // ── Shockwave: siempre en hitPoint, tamaño fijo grande ────────
            _shockwavePS.transform.position = hitPoint;
            _shockwavePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _shockwavePS.Play();

            StartCoroutine(DisableAfterDelay(obstacle, 0.3f));
        }

        // Modifica en runtime los params del burst principal según escala
        private static void ApplyImpactScale(ParticleSystem ps, float scale)
        {
            var main = ps.main;
            main.startSize  = new ParticleSystem.MinMaxCurve(0.18f * scale, 0.5f * scale);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f * scale, 12f * scale);
            main.maxParticles = Mathf.RoundToInt(30 * Mathf.Clamp(scale, 0.5f, 3f));

            var shape = ps.shape;
            shape.radius = Mathf.Max(0.1f, 0.2f * scale);

            var emission = ps.emission;
            int count = Mathf.RoundToInt(Mathf.Lerp(15f, 50f, Mathf.InverseLerp(0.4f, 3.5f, scale)));
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
        }

        private static IEnumerator DisableAfterDelay(GameObject go, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (go != null) go.SetActive(false);
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

            // ── 2. Partículas beam (hacia adelante) ────────────────────────
            var vfxGO = new GameObject("VFX_PulseLaser_DataParticles");
            vfxGO.transform.SetParent(transform, false);
            vfxGO.transform.localPosition = muzzleLocal;
            vfxGO.transform.localRotation = Quaternion.identity;

            _particles = vfxGO.AddComponent<ParticleSystem>();
            SetupBeamParticles(_particles);

            // ── 3. Burst principal de impacto (escala con el obstáculo) ───
            var impactGO = new GameObject("VFX_LaserImpact");
            impactGO.transform.SetParent(null);
            _impactPS = impactGO.AddComponent<ParticleSystem>();
            SetupImpactParticles(_impactPS, _impactMaterial);

            // ── 4. Shockwave (anillo expansivo siempre grande) ─────────────
            var shockGO = new GameObject("VFX_LaserShockwave");
            shockGO.transform.SetParent(null);
            _shockwavePS = shockGO.AddComponent<ParticleSystem>();
            SetupShockwaveParticles(_shockwavePS);
        }

        private void OnDestroy()
        {
            if (_impactPS    != null) Destroy(_impactPS.gameObject);
            if (_shockwavePS != null) Destroy(_shockwavePS.gameObject);
        }

        // ── Estrella plana de 6 puntas ───────────────────────────────────────
        private static Mesh BuildFlatStarMesh(int points = 6, float innerRatio = 0.42f)
        {
            int vCount   = points * 2 * 3;
            var verts    = new Vector3[vCount];
            var colors   = new Color[vCount];
            var normals  = new Vector3[vCount];
            var tris     = new int[vCount];
            var faceNormal = Vector3.back;

            int vi = 0;
            for (int i = 0; i < points; i++)
            {
                float aOuter     = (float)i / points * Mathf.PI * 2f;
                float aInner     = aOuter + Mathf.PI / points;
                float aNextOuter = (float)(i + 1) / points * Mathf.PI * 2f;

                Vector3 outer     = new Vector3(Mathf.Cos(aOuter),              Mathf.Sin(aOuter),              0f);
                Vector3 inner     = new Vector3(Mathf.Cos(aInner) * innerRatio, Mathf.Sin(aInner) * innerRatio, 0f);
                Vector3 nextOuter = new Vector3(Mathf.Cos(aNextOuter),           Mathf.Sin(aNextOuter),           0f);

                Color tipColor    = new Color(0f,  1f,   1f,   1f);
                Color innerColor  = new Color(0f,  0.6f, 1f,   0.5f);
                Color centerColor = new Color(0.2f,1f,   1f,   0.9f);

                verts[vi]=Vector3.zero; colors[vi]=centerColor; normals[vi]=faceNormal; tris[vi]=vi; vi++;
                verts[vi]=outer;        colors[vi]=tipColor;    normals[vi]=faceNormal; tris[vi]=vi; vi++;
                verts[vi]=inner;        colors[vi]=innerColor;  normals[vi]=faceNormal; tris[vi]=vi; vi++;

                verts[vi]=Vector3.zero; colors[vi]=centerColor; normals[vi]=faceNormal; tris[vi]=vi; vi++;
                verts[vi]=inner;        colors[vi]=innerColor;  normals[vi]=faceNormal; tris[vi]=vi; vi++;
                verts[vi]=nextOuter;    colors[vi]=tipColor;    normals[vi]=faceNormal; tris[vi]=vi; vi++;
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
            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"))
                { name = "Mat_StarLaser" };
            mat.SetColor("_BaseColor",  Color.white);
            mat.SetFloat("_ColorMode", 0f);
            mat.SetFloat("_Surface",   1f);
            mat.SetFloat("_Blend",     2f);
            mat.SetInt("_SrcBlend",    (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend",    (int)BlendMode.One);
            mat.SetInt("_ZWrite",      0);
            mat.SetInt("_Cull",        0);
            mat.renderQueue = (int)RenderQueue.Transparent + 1;
            return mat;
        }

        private static void SetupBeamParticles(ParticleSystem ps)
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
            psr.material            = BuildBeamMaterial();
            psr.shadowCastingMode   = ShadowCastingMode.Off;
            psr.receiveShadows      = false;
        }

        private static Material BuildBeamMaterial()
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"))
                { name = "Mat_ParticleLaser" };
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

        // ── Burst principal: spritesheet animado, escala dinámica ────────────
        private static void SetupImpactParticles(ParticleSystem ps, Material overrideMat)
        {
            var main = ps.main;
            main.loop            = false;
            main.playOnAwake     = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles    = 90;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(4f, 12f);
            main.startSize       = new ParticleSystem.MinMaxCurve(0.2f, 0.55f);
            main.startRotation   = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor      = new Color(0f, 2f, 2f, 1f);

            var emission = ps.emission;
            emission.enabled      = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });

            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius    = 0.15f;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size    = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            var psr = ps.GetComponent<ParticleSystemRenderer>();
            psr.renderMode        = ParticleSystemRenderMode.Billboard;
            psr.shadowCastingMode = ShadowCastingMode.Off;
            psr.receiveShadows    = false;

            bool hasSpritesheet = overrideMat != null
                && overrideMat.GetTexture("_BaseMap") != null;

            if (hasSpritesheet)
            {
                var mat = new Material(overrideMat) { name = "Mat_LaserImpact_Cyan" };
                mat.SetFloat("_Blend",   2f);
                mat.SetFloat("_Surface", 1f);
                mat.SetInt("_SrcBlend",  (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend",  (int)BlendMode.One);
                mat.SetInt("_ZWrite",    0);
                mat.SetInt("_Cull",      0);
                mat.SetColor("_BaseColor",     new Color(0f, 2f, 2f, 1f));
                mat.SetColor("_EmissionColor", new Color(0f, 6f, 6f, 1f));
                mat.EnableKeyword("_EMISSION");
                mat.DisableKeyword("_ALPHABLEND_ON");
                mat.renderQueue = (int)RenderQueue.Transparent + 1;
                psr.material = mat;

                var tsa = ps.textureSheetAnimation;
                tsa.enabled       = true;
                tsa.numTilesX     = 8;
                tsa.numTilesY     = 4;
                tsa.animation     = ParticleSystemAnimationType.WholeSheet;
                tsa.frameOverTime = new ParticleSystem.MinMaxCurve(
                    1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            }
            else
            {
                psr.material = BuildAdditiveGlowMaterial(new Color(0f, 2f, 2f, 1f), "Mat_LaserImpact_Fallback");
            }
        }

        // ── Shockwave: pocas partículas grandes y lentas, siempre visibles ──
        private static void SetupShockwaveParticles(ParticleSystem ps)
        {
            var main = ps.main;
            main.loop            = false;
            main.playOnAwake     = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles    = 12;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            main.startSize       = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);   // partículas grandes
            main.startRotation   = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor      = new Color(0f, 1.5f, 1.5f, 0.7f);             // cyan semitransparente

            var emission = ps.emission;
            emission.enabled      = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius    = 0.05f;   // nacen casi desde el mismo punto, se expanden

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            // Crecen primero y luego se desvanecen → onda expansiva
            sol.size = new ParticleSystem.MinMaxCurve(
                1f, new AnimationCurve(
                    new Keyframe(0f, 0.1f),
                    new Keyframe(0.3f, 1f),
                    new Keyframe(1f, 0f)));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(0f, 1f, 1f), 0f),
                         new GradientColorKey(new Color(0f, 0.5f, 1f), 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f),
                         new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var psr = ps.GetComponent<ParticleSystemRenderer>();
            psr.renderMode        = ParticleSystemRenderMode.Billboard;
            psr.shadowCastingMode = ShadowCastingMode.Off;
            psr.receiveShadows    = false;
            psr.material          = BuildAdditiveGlowMaterial(new Color(0f, 3f, 3f, 1f), "Mat_Shockwave");
        }

        private static Material BuildAdditiveGlowMaterial(Color baseColor, string name)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"))
                { name = name };
            mat.SetColor("_BaseColor",     baseColor);
            mat.SetColor("_EmissionColor", baseColor * 3f);
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

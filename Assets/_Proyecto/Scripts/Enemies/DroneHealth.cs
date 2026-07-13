using System.Collections;
using UnityEngine;

namespace NeoFastRider.Enemies
{
    [AddComponentMenu("NeoFastRider/Enemies/Drone Health")]
    public sealed class DroneHealth : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 3;

        // Estas constantes NO son campos serializados para que valores viejos
        // guardados en el Inspector no sobrescriban la geometría.
        private const float BarHeightOffset = 1.0f;   // sobre el pivote del dron
        private const float BarWidth        = 2.5f;
        private const float BarHeight       = 0.32f;

        private static readonly Color ColBg   = new Color(0.08f, 0.08f, 0.08f, 1f);
        private static readonly Color ColFull = new Color(0.10f, 1.00f, 0.28f, 1f);
        private static readonly Color ColMid  = new Color(1.00f, 0.80f, 0.00f, 1f);
        private static readonly Color ColLow  = new Color(1.00f, 0.08f, 0.08f, 1f);

        private int       _hp;
        private bool      _dead;
        private Transform _barRoot;
        private Transform _fillTR;
        private Material  _fillMat;
        private Camera    _cam;

        // ─────────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _hp  = maxHealth;
            _cam = Camera.main;
            BuildBar();
        }

        public void TakeDamage(int amount)
        {
            if (_dead) return;
            _hp = Mathf.Max(0, _hp - amount);
            RefreshFill();
            StartCoroutine(DamageFlash());
            if (_hp <= 0) Die();
        }

        // ── Construcción ──────────────────────────────────────────────────────────
        private void BuildBar()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");

            _barRoot = new GameObject("HP_Root").transform;
            _barRoot.SetParent(transform, false);
            _barRoot.localPosition = new Vector3(0f, BarHeightOffset, 0f);

            // Fondo oscuro
            var bgMat = MakeMat(shader, ColBg, 2490);
            Quad("HP_BG", _barRoot, Vector3.zero, BarWidth + 0.12f, BarHeight + 0.12f)
                .sharedMaterial = bgMat;

            // Fill — queue 2491, se dibuja sobre el BG
            _fillMat = MakeMat(shader, ColFull, 2491);
            var fillGO = Quad("HP_Fill", _barRoot, Vector3.zero, BarWidth, BarHeight * 0.72f);
            fillGO.sharedMaterial = _fillMat;
            _fillTR = fillGO.transform;

            RefreshFill();
        }

        // Material opaco con ZTest=Always — renderqueue en rango opaco (< 2500)
        // para que URP lo trate consistentemente
        private static Material MakeMat(Shader shader, Color color, int queue)
        {
            var m = new Material(shader);
            m.SetColor("_BaseColor", color);
            m.SetInt("_ZTest",  8); // CompareFunction.Always
            m.SetInt("_ZWrite", 0);
            m.SetInt("_Cull",   0); // CullMode.Off — visible desde ambas caras
            m.renderQueue = queue;
            return m;
        }

        private static MeshRenderer Quad(string name, Transform parent,
                                         Vector3 lpos, float w, float h)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = lpos;
            t.localScale    = new Vector3(w, h, 1f);
            var mr = go.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows    = false;
            return mr;
        }

        // ── Fill: solo se actualiza al recibir daño ───────────────────────────────
        private void RefreshFill()
        {
            if (_fillTR == null || _fillMat == null) return;
            float t  = (float)_hp / maxHealth;
            float fw = BarWidth * t;
            _fillTR.localPosition = new Vector3(-BarWidth * 0.5f + fw * 0.5f, 0f, 0f);
            _fillTR.localScale    = new Vector3(Mathf.Max(fw, 0.001f), BarHeight * 0.72f, 1f);
            _fillMat.SetColor("_BaseColor", t > 0.5f
                ? Color.Lerp(ColMid, ColFull, (t - 0.5f) * 2f)
                : Color.Lerp(ColLow, ColMid,   t          * 2f));
        }

        private IEnumerator DamageFlash()
        {
            if (_fillMat == null) yield break;
            _fillMat.SetColor("_BaseColor", Color.white);
            yield return new WaitForSeconds(0.07f);
            if (_fillMat != null) RefreshFill();
        }

        // ── Billboard: siempre mira hacia la cámara ───────────────────────────────
        private void LateUpdate()
        {
            if (_barRoot == null) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;
            // LookAt hace que el +Z local (frente del quad) apunte hacia la cámara
            _barRoot.LookAt(_cam.transform.position);
        }

        private void Die()
        {
            _dead = true;
            GetComponent<DroneCollisionHandler>()?.OnKilledByLaser();
        }
    }
}

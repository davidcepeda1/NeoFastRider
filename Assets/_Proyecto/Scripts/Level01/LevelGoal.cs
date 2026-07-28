using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace NeoFastRider.Level01
{
    /// <summary>
    /// Meta del nivel. Se coloca como trigger cruzando la calzada al final.
    /// Al pasar el jugador muestra un panel de nivel superado con opciones para
    /// repetir o ir a otros niveles. Construye su interfaz en tiempo de ejecucion.
    /// Script AISLADO: no modifica ninguna logica existente.
    /// </summary>
    [AddComponentMenu("NeoFastRider/Level01/Level Goal")]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class LevelGoal : MonoBehaviour
    {
        [Header("Mensaje")]
        [SerializeField] private string _titulo   = "NIVEL COMPLETADO";
        [SerializeField] private string _subtitulo = "Has llegado a la meta";

        [Header("Escenas")]
        [SerializeField] private string _escenaTutorial = "Scene_TutorialLevel";
        [SerializeField] private string _escenaMenu     = "Scene_MainMenu";

        [Header("Comportamiento")]
        [SerializeField] private bool _congelarTiempo = true;

        public bool Completado => _completado;

        private bool _completado;

        private void Reset()
        {
            var bc = GetComponent<BoxCollider>();
            bc.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_completado) return;
            if (!other.CompareTag("Player") && other.GetComponentInParent<Rigidbody>() == null) return;
            if (!other.transform.root.CompareTag("Player")) return;

            _completado = true;
            Debug.Log("[Meta] Nivel completado.");

            AsegurarEventSystem();
            ConstruirUI();

            if (_congelarTiempo) Time.timeScale = 0f;
        }

        private static void AsegurarEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null) return;
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;

            var go = new GameObject("EventSystem_Meta");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            var tMod = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (tMod != null) go.AddComponent(tMod);
            else go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        private static bool EstaEnBuild(string nombre)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string ruta = SceneUtility.GetScenePathByBuildIndex(i);
                if (System.IO.Path.GetFileNameWithoutExtension(ruta) == nombre) return true;
            }
            return false;
        }

        private void ConstruirUI()
        {
            var go = new GameObject("Goal_Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            go.AddComponent<GraphicRaycaster>();

            var fondo = Hijo(go.transform, "Fondo");
            fondo.gameObject.AddComponent<Image>().color = new Color(0f, 0.05f, 0.08f, 0.78f);
            Estirar(fondo);

            Texto(go.transform, _titulo,    96, new Color(0f, 1f, 0.75f), new Vector2(0f, 210f), 900f);
            Texto(go.transform, _subtitulo, 34, new Color(0.8f, 1f, 1f, 0.9f), new Vector2(0f, 120f), 900f);

            Boton(go.transform, "REPETIR NIVEL",    new Vector2(0f,  20f), () => Cargar(SceneManager.GetActiveScene().name));
            Boton(go.transform, "TUTORIAL",         new Vector2(0f, -70f), () => Cargar(_escenaTutorial));
            Boton(go.transform, "MENU PRINCIPAL",   new Vector2(0f,-160f), () => Cargar(_escenaMenu));
        }

        private void Cargar(string escena)
        {
            Time.timeScale = 1f;
            if (EstaEnBuild(escena)) { SceneManager.LoadScene(escena); return; }
            Debug.LogWarning($"[Meta] '{escena}' no esta en Build Settings; se recarga el nivel.");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private static RectTransform Hijo(Transform padre, string nombre)
        {
            var g = new GameObject(nombre);
            g.transform.SetParent(padre, false);
            return g.AddComponent<RectTransform>();
        }

        private static void Estirar(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static void Texto(Transform padre, string txt, int tam, Color col, Vector2 pos, float ancho)
        {
            var rt = Hijo(padre, "Txt_" + txt);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(ancho, tam + 40f);
            var t = rt.gameObject.AddComponent<Text>();
            t.text = txt;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = tam;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = col;
        }

        private void Boton(Transform padre, string texto, Vector2 pos, UnityEngine.Events.UnityAction accion)
        {
            var rt = Hijo(padre, "Btn_" + texto);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(420f, 72f);

            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0.55f, 0.6f, 0.95f);

            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(accion);

            var trt = Hijo(rt, "Texto");
            Estirar(trt);
            var t = trt.gameObject.AddComponent<Text>();
            t.text = texto;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 30;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
        }

        private void OnDestroy()
        {
            if (_congelarTiempo) Time.timeScale = 1f;
        }
    }
}

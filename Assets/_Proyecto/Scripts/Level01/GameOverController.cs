using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace NeoFastRider.Level01
{
    /// <summary>
    /// Fin de partida del Nivel 1. Construye su propia interfaz en tiempo de
    /// ejecucion, asi que no depende de ningun prefab ni escena de menu.
    /// Script AISLADO: no modifica la logica existente del juego.
    /// </summary>
    [AddComponentMenu("NeoFastRider/Level01/Game Over Controller")]
    public sealed class GameOverController : MonoBehaviour
    {
        [Header("Escenas")]
        [Tooltip("Escena del menu principal.")]
        [SerializeField] private string _escenaMenu  = "Scene_MainMenu";

        [Header("Comportamiento")]
        [Tooltip("Congela el tiempo al mostrar el Game Over.")]
        [SerializeField] private bool _congelarTiempo = true;

        public bool IsGameOver => _gameOver;

        private bool   _gameOver;
        private Canvas _canvas;

        /// <summary>Llamado por el detector de choques. Idempotente.</summary>
        public void TriggerGameOver(string motivo)
        {
            if (_gameOver) return;
            _gameOver = true;

            Debug.Log("[GameOver] Fin de partida.");

            AsegurarEventSystem();
            ConstruirUI(motivo);

            if (_congelarTiempo) Time.timeScale = 0f;
        }

        /// <summary>
        /// Sin EventSystem los botones de Unity UI no reciben clics. Ademas, con el
        /// nuevo Input System hace falta InputSystemUIInputModule; el modulo antiguo
        /// no entrega eventos. Se crea aqui si falta, para no depender de la escena.
        /// </summary>
        private static void AsegurarEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null) return;
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;

            var go = new GameObject("EventSystem_GameOver");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();

            var tMod = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (tMod != null) go.AddComponent(tMod);
            else go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            Debug.Log("[GameOver] EventSystem creado en tiempo de ejecucion.");
        }

        private void ConstruirUI(string motivo)
        {
            var go = new GameObject("GameOver_Canvas");
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32000;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            go.AddComponent<GraphicRaycaster>();

            // Fondo oscuro
            var fondo = NuevoHijo(go.transform, "Fondo");
            var img = fondo.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.72f);
            Estirar(fondo);

            // Titulo
            var titulo = NuevoHijo(go.transform, "Titulo");
            var txt = titulo.gameObject.AddComponent<Text>();
            txt.text      = "GAME OVER";
            txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize  = 96;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color     = new Color(1f, 0.16f, 0.33f);
            titulo.anchorMin = new Vector2(0.5f, 0.5f);
            titulo.anchorMax = new Vector2(0.5f, 0.5f);
            titulo.pivot     = new Vector2(0.5f, 0.5f);
            titulo.anchoredPosition = new Vector2(0f, 150f);
            titulo.sizeDelta = new Vector2(900f, 160f);

            // Motivo
            var sub = NuevoHijo(go.transform, "Motivo");
            var stx = sub.gameObject.AddComponent<Text>();
            stx.text      = motivo;
            stx.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            stx.fontSize  = 34;
            stx.alignment = TextAnchor.MiddleCenter;
            stx.color     = new Color(0f, 1f, 1f, 0.9f);
            sub.anchorMin = new Vector2(0.5f, 0.5f);
            sub.anchorMax = new Vector2(0.5f, 0.5f);
            sub.pivot     = new Vector2(0.5f, 0.5f);
            sub.anchoredPosition = new Vector2(0f, 60f);
            sub.sizeDelta = new Vector2(900f, 60f);

            CrearBoton(go.transform, "REINTENTAR", new Vector2(0f, -40f), Reintentar);
            CrearBoton(go.transform, "MENU",       new Vector2(0f, -140f), IrAlMenu);
        }

        private static RectTransform NuevoHijo(Transform padre, string nombre)
        {
            var g = new GameObject(nombre);
            g.transform.SetParent(padre, false);
            return g.AddComponent<RectTransform>();
        }

        private static void Estirar(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void CrearBoton(Transform padre, string texto, Vector2 pos, UnityEngine.Events.UnityAction accion)
        {
            var rt = NuevoHijo(padre, "Btn_" + texto);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(360f, 76f);

            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0.55f, 0.65f, 0.95f);

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(accion);

            var trt = NuevoHijo(rt, "Texto");
            Estirar(trt);
            var t = trt.gameObject.AddComponent<Text>();
            t.text      = texto;
            t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize  = 32;
            t.alignment = TextAnchor.MiddleCenter;
            t.color     = Color.white;
        }

        /// <summary>
        /// Comprueba si una escena esta en Build Settings recorriendo los indices.
        /// No se usa Application.CanStreamedLevelBeLoaded porque en el Editor
        /// devuelve false aunque la escena si este registrada.
        /// </summary>
        private static bool EstaEnBuild(string nombre)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string ruta = SceneUtility.GetScenePathByBuildIndex(i);
                if (System.IO.Path.GetFileNameWithoutExtension(ruta) == nombre) return true;
            }
            return false;
        }

        /// <summary>Reinicia el nivel desde el principio (siempre la escena activa, sin importar cuál sea).</summary>
        public void Reintentar()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>Vuelve al menu principal.</summary>
        public void IrAlMenu()
        {
            Time.timeScale = 1f;

            if (EstaEnBuild(_escenaMenu))
            {
                SceneManager.LoadScene(_escenaMenu);
                return;
            }

            Debug.LogWarning($"[GameOver] '{_escenaMenu}' no esta en Build Settings; se reinicia el nivel.");
            Reintentar();
        }

        private void OnDestroy()
        {
            if (_congelarTiempo) Time.timeScale = 1f;   // nunca dejar el juego congelado
        }
    }
}

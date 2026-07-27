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
        [Tooltip("Escena de este nivel, para el boton Reintentar.")]
        [SerializeField] private string _escenaNivel = "Scene_Level01";
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

            Debug.Log($"[GameOver] Fin de partida: {motivo}");

            ConstruirUI(motivo);

            if (_congelarTiempo) Time.timeScale = 0f;
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

        private void Reintentar()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(_escenaNivel);
        }

        private void IrAlMenu()
        {
            Time.timeScale = 1f;
            if (Application.CanStreamedLevelBeLoaded(_escenaMenu)) SceneManager.LoadScene(_escenaMenu);
            else { Debug.LogWarning($"[GameOver] '{_escenaMenu}' no esta en Build Settings; se reinicia el nivel."); SceneManager.LoadScene(_escenaNivel); }
        }

        private void OnDestroy()
        {
            if (_congelarTiempo) Time.timeScale = 1f;   // nunca dejar el juego congelado
        }
    }
}

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using NeoFastRider.Core.LevelGen;

namespace NeoFastRider.EditorTools
{
    /// <summary>
    /// Herramienta SOLO DE EDITOR para inspeccionar el mapa procedural sin entrar en Play.
    ///
    /// Vive en una carpeta "Editor", asi que NO se compila en la build y no puede afectar
    /// al juego. No modifica el generador, ni sus parametros, ni ningun script de runtime.
    ///
    /// La previsualizacion es TEMPORAL: los objetos se crean con HideFlags.DontSave, de modo
    /// que Unity nunca los escribe en el archivo de escena aunque guardes por accidente.
    ///
    /// Como el generador usa semilla fija (_useFixedSeed), lo que se ve aqui es exactamente
    /// el mismo mapa que se generara al jugar.
    ///
    /// Menu:  NeoFastRider > Mapa
    /// </summary>
    public static class LevelMapPreview
    {
        private const string RAIZ_PISTA  = "GeneratedTrack";
        private const string RAIZ_CIUDAD = "GeneratedCity";
        private const string SUFIJO      = "  [PREVIEW - no se guarda]";

        // ── Generar ──────────────────────────────────────────────────────────
        [MenuItem("NeoFastRider/Mapa/Previsualizar mapa %#m", false, 0)]
        private static void Previsualizar()
        {
            var gen = Object.FindFirstObjectByType<LevelChunkGenerator>(FindObjectsInactive.Include);
            if (gen == null)
            {
                EditorUtility.DisplayDialog("Mapa",
                    "No hay ningun LevelChunkGenerator en la escena abierta.\n\n" +
                    "Abre una escena que use generacion procedural (por ejemplo Scene_Level_01).",
                    "Entendido");
                return;
            }

            Limpiar();   // nunca acumular dos previsualizaciones

            gen.Generate();

            // Se cuenta ANTES de renombrar: MarcarComoTemporal anade un sufijo a las raices
            // y despues GameObject.Find(RAIZ_PISTA) ya no las encuentra.
            var pista  = GameObject.Find(RAIZ_PISTA);
            var ciudad = GameObject.Find(RAIZ_CIUDAD);
            int nPista  = pista  ? pista.transform.childCount  : 0;
            int nCiudad = ciudad ? ciudad.transform.childCount : 0;

            MarcarComoTemporal();
            Encuadrar();

            Debug.Log($"[Mapa] Previsualizacion generada: " +
                      $"{nPista} piezas de pista, " +
                      $"{nCiudad} objetos de ciudad. " +
                      $"Es TEMPORAL: no se guarda en la escena. Usa 'Limpiar previsualizacion' al terminar.");
        }

        // ── Limpiar ──────────────────────────────────────────────────────────
        [MenuItem("NeoFastRider/Mapa/Limpiar previsualizacion %#l", false, 1)]
        private static void LimpiarConAviso()
        {
            int n = Limpiar();
            Debug.Log(n > 0
                ? $"[Mapa] Previsualizacion eliminada ({n} raices)."
                : "[Mapa] No habia ninguna previsualizacion que limpiar.");
        }

        /// <summary>
        /// Borra cualquier resto de previsualizacion. Se usa StartsWith en vez de igualdad
        /// exacta para barrer tambien raices ya renombradas con el sufijo, o duplicadas por
        /// Unity con " (1)". Dejar un resto vivo altera el resultado del generador, asi que
        /// esta limpieza tiene que ser exhaustiva.
        /// </summary>
        private static int Limpiar()
        {
            int n = 0;
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go == null || go.transform.parent != null) continue;
                if (!go.name.StartsWith(RAIZ_PISTA) && !go.name.StartsWith(RAIZ_CIUDAD)) continue;
                Object.DestroyImmediate(go);
                n++;
            }

            // Los colliders destruidos siguen en la escena de fisica hasta que se sincroniza.
            // El generador consulta el entorno al colocar piezas, asi que sin esto la siguiente
            // generacion puede salir distinta.
            if (n > 0) Physics.SyncTransforms();
            return n;
        }

        /// <summary>
        /// Marca lo generado como no persistente. HideFlags.DontSave impide que Unity
        /// lo escriba en el .unity aunque el usuario pulse Ctrl+S.
        /// </summary>
        private static void MarcarComoTemporal()
        {
            foreach (var nombre in new[] { RAIZ_PISTA, RAIZ_CIUDAD })
            {
                var raiz = GameObject.Find(nombre);
                if (raiz == null) continue;
                foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                    t.gameObject.hideFlags = HideFlags.DontSave;
                raiz.name = nombre + SUFIJO;
            }
        }

        // ── Encuadre ─────────────────────────────────────────────────────────
        [MenuItem("NeoFastRider/Mapa/Encuadrar vista cenital", false, 20)]
        private static void Encuadrar()
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null) return;

            var raiz = GameObject.Find(RAIZ_PISTA) ?? GameObject.Find(RAIZ_PISTA + SUFIJO);
            if (raiz == null) return;

            var rends = raiz.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return;

            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);

            sv.orthographic = true;
            sv.pivot    = b.center;
            sv.rotation = Quaternion.Euler(90f, 0f, 0f);
            sv.size     = Mathf.Max(b.size.x, b.size.z) * 0.58f;
            sv.Repaint();

            Debug.Log($"[Mapa] Extension: centro {b.center:F0}, tamano {b.size:F0}  " +
                      $"X[{b.min.x:F0}..{b.max.x:F0}]  Z[{b.min.z:F0}..{b.max.z:F0}]");
        }

        [MenuItem("NeoFastRider/Mapa/Encuadrar vista en perspectiva", false, 21)]
        private static void EncuadrarPerspectiva()
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null) return;

            var raiz = GameObject.Find(RAIZ_PISTA) ?? GameObject.Find(RAIZ_PISTA + SUFIJO);
            if (raiz == null) return;

            var rends = raiz.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return;

            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);

            sv.orthographic = false;
            sv.pivot    = b.center;
            sv.rotation = Quaternion.Euler(38f, 35f, 0f);
            sv.size     = Mathf.Max(b.size.x, b.size.z) * 0.6f;
            sv.Repaint();
        }

        // ── Validacion de menu ───────────────────────────────────────────────
        [MenuItem("NeoFastRider/Mapa/Previsualizar mapa %#m", true)]
        [MenuItem("NeoFastRider/Mapa/Limpiar previsualizacion %#l", true)]
        private static bool SoloFueraDePlay() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeoFastRider.UI
{
    /// <summary>
    /// Navegacion del menu de seleccion de niveles.
    ///
    /// Script AISLADO: no toca MainMenuController, OptionsController ni ningun
    /// sistema existente. Solo carga escenas por nombre.
    ///
    /// Mapeo pedido:
    ///   Nivel 1 -> Scene_TutorialLevel
    ///   Nivel 2 -> Scene_Level01
    ///   Nivel 3 -> Scene_Level_01
    ///
    /// Los metodos son SIN PARAMETROS a proposito: as� el cableado de los
    /// botones en el inspector es una referencia directa al metodo, sin
    /// argumentos serializados que se puedan corromper al renombrar escenas.
    /// </summary>
    [AddComponentMenu("NeoFastRider/UI/Level Select Controller")]
    public sealed class LevelSelectController : MonoBehaviour
    {
        // ── Nombres de escena (editables en el inspector) ────────────────────
        [Header("Escenas de nivel")]
        [Tooltip("Nivel 1 del menu.")]
        [SerializeField] private string _nivel1 = "Scene_TutorialLevel";

        [Tooltip("Nivel 2 del menu.")]
        [SerializeField] private string _nivel2 = "Scene_Level01";

        [Tooltip("Nivel 3 del menu.")]
        [SerializeField] private string _nivel3 = "Scene_Level_01";

        [Header("Navegacion de menus")]
        [Tooltip("Escena del menu principal.")]
        [SerializeField] private string _escenaMenuPrincipal = "Scene_MainMenu";

        [Tooltip("Escena del menu de seleccion de niveles.")]
        [SerializeField] private string _escenaSeleccionNivel = "Scene_LevelSelect";

        // ── Botones de nivel ─────────────────────────────────────────────────
        /// <summary>Nivel 1.</summary>
        public void CargarNivel1() => Cargar(_nivel1);

        /// <summary>Nivel 2.</summary>
        public void CargarNivel2() => Cargar(_nivel2);

        /// <summary>Nivel 3.</summary>
        public void CargarNivel3() => Cargar(_nivel3);

        // ── Navegacion entre menus ───────────────────────────────────────────
        /// <summary>Abre el menu de seleccion de niveles. Se usa desde el menu principal.</summary>
        public void AbrirSeleccionNivel() => Cargar(_escenaSeleccionNivel);

        /// <summary>Vuelve al menu principal.</summary>
        public void VolverAlMenuPrincipal() => Cargar(_escenaMenuPrincipal);

        // ── Carga ────────────────────────────────────────────────────────────
        /// <summary>
        /// Carga una escena por nombre. Se restaura timeScale por si se llega
        /// aqui desde una pausa. Si la escena no esta en Build Settings se avisa
        /// por consola en vez de lanzar una excepcion.
        /// </summary>
        public void Cargar(string nombreEscena)
        {
            if (string.IsNullOrEmpty(nombreEscena))
            {
                Debug.LogWarning("[LevelSelect] Nombre de escena vacio; no se carga nada.");
                return;
            }

            if (!EstaEnBuild(nombreEscena))
            {
                Debug.LogWarning($"[LevelSelect] '{nombreEscena}' no esta en Build Settings. " +
                                 "Anadela en File > Build Settings para poder cargarla.");
                return;
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(nombreEscena);
        }

        /// <summary>
        /// Comprueba si una escena esta registrada recorriendo los indices de
        /// Build Settings. No se usa Application.CanStreamedLevelBeLoaded porque
        /// en el Editor devuelve false aunque la escena si este registrada.
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
    }
}

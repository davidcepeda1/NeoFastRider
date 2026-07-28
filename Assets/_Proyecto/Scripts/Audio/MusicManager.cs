using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeoFastRider.Audio
{
    /// <summary>
    /// Gestor de musica de ambientacion. Persiste entre escenas y decide que
    /// pista suena en cada una.
    ///
    /// Script AISLADO: no modifica ningun sistema existente. Se arranca solo
    /// mediante RuntimeInitializeOnLoadMethod cargando su prefab desde Resources,
    /// asi que NO hace falta anadirlo a ninguna escena.
    ///
    /// Comportamiento:
    ///  - Si dos escenas comparten pista, la musica NO se reinicia al cambiar de
    ///    escena: sigue sonando de forma continua (menu -> seleccion de nivel).
    ///  - Si la pista cambia, hace un cross-fade entre las dos.
    ///  - El volumen sale de PlayerPrefs["opt_bgm_volume"], la misma clave que ya
    ///    escribe OptionsController, de modo que su slider por fin tiene efecto.
    ///    Se relee periodicamente para responder en cuanto pulsas Aplicar.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MusicManager : MonoBehaviour
    {
        [System.Serializable]
        public struct PistaDeEscena
        {
            [Tooltip("Nombre EXACTO de la escena, tal cual aparece en Build Settings.")]
            public string escena;

            [Tooltip("Pista para esa escena. Si se deja vacia se usa la pista por defecto.")]
            public AudioClip pista;
        }

        // ── Configuracion ────────────────────────────────────────────────────
        [Header("Pistas")]
        [Tooltip("Pista que suena en cualquier escena que no tenga una asignada abajo.")]
        [SerializeField] private AudioClip _pistaPorDefecto;

        [Tooltip("Pista concreta por escena. Deja el clip vacio para que use la de por defecto.")]
        [SerializeField] private List<PistaDeEscena> _pistasPorEscena = new List<PistaDeEscena>();

        [Header("Mezcla")]
        [Tooltip("Segundos que dura el fundido cruzado al cambiar de pista.")]
        [SerializeField, Range(0f, 5f)] private float _duracionCrossFade = 1.5f;

        [Tooltip("Volumen maximo de la musica, antes de aplicar la preferencia del jugador.")]
        [SerializeField, Range(0f, 1f)] private float _volumenBase = 0.55f;

        [Header("Preferencias")]
        [Tooltip("Clave de PlayerPrefs con el volumen de musica (la que usa OptionsController).")]
        [SerializeField] private string _claveVolumen = "opt_bgm_volume";

        [Tooltip("Cada cuantos segundos se relee la preferencia de volumen.")]
        [SerializeField, Range(0.05f, 1f)] private float _intervaloReleerVolumen = 0.25f;

        // ── Estado ───────────────────────────────────────────────────────────
        public static MusicManager Instancia { get; private set; }

        private AudioSource _fuenteA, _fuenteB;
        private AudioSource _activa;          // la que esta sonando ahora
        private float       _volumenJugador = 1f;
        private Coroutine   _fundido;

        /// <summary>Pista sonando actualmente, o null.</summary>
        public AudioClip PistaActual => _activa != null ? _activa.clip : null;

        // ── Arranque automatico, sin tocar ninguna escena ─────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Arrancar()
        {
            if (Instancia != null) return;

            var prefab = Resources.Load<GameObject>("Audio_MusicManager");
            if (prefab == null)
            {
                Debug.LogWarning("[Musica] No encuentro 'Audio_MusicManager' en Resources; no habra musica.");
                return;
            }
            var go = Instantiate(prefab);
            go.name = "Audio_MusicManager";
        }

        private void Awake()
        {
            if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
            Instancia = this;
            DontDestroyOnLoad(gameObject);

            _fuenteA = CrearFuente("Fuente_A");
            _fuenteB = CrearFuente("Fuente_B");
            _activa  = _fuenteA;

            LeerVolumen();
            SceneManager.sceneLoaded += AlCargarEscena;
        }

        private void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
            SceneManager.sceneLoaded -= AlCargarEscena;
        }

        private void Start()
        {
            // La escena inicial no dispara sceneLoaded para este objeto, se resuelve a mano
            Reproducir(PistaPara(SceneManager.GetActiveScene().name), inmediato: true);
            StartCoroutine(VigilarVolumen());
        }

        private AudioSource CrearFuente(string nombre)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(transform, false);
            var a = go.AddComponent<AudioSource>();
            a.playOnAwake   = false;
            a.loop          = true;
            a.spatialBlend  = 0f;    // 2D: no depende de donde este el AudioListener
            a.volume        = 0f;
            a.ignoreListenerPause = true;   // la musica sigue durante la pausa
            return a;
        }

        // ── Seleccion de pista ───────────────────────────────────────────────
        private AudioClip PistaPara(string nombreEscena)
        {
            for (int i = 0; i < _pistasPorEscena.Count; i++)
                if (_pistasPorEscena[i].escena == nombreEscena)
                    return _pistasPorEscena[i].pista != null ? _pistasPorEscena[i].pista : _pistaPorDefecto;
            return _pistaPorDefecto;
        }

        private void AlCargarEscena(Scene escena, LoadSceneMode modo)
        {
            LeerVolumen();
            Reproducir(PistaPara(escena.name), inmediato: false);
        }

        /// <summary>
        /// Cambia de pista. Si ya suena esa misma pista no hace nada, de modo que
        /// la musica continua sin cortes al navegar entre escenas que la comparten.
        /// </summary>
        public void Reproducir(AudioClip pista, bool inmediato = false)
        {
            if (pista == null)
            {
                if (_fundido != null) StopCoroutine(_fundido);
                _fundido = StartCoroutine(FundirA(null, inmediato ? 0f : _duracionCrossFade));
                return;
            }

            if (_activa != null && _activa.clip == pista && _activa.isPlaying) return;  // continuidad

            if (_fundido != null) StopCoroutine(_fundido);
            _fundido = StartCoroutine(FundirA(pista, inmediato ? 0f : _duracionCrossFade));
        }

        private IEnumerator FundirA(AudioClip pista, float duracion)
        {
            var saliente = _activa;
            var entrante = (_activa == _fuenteA) ? _fuenteB : _fuenteA;

            if (pista != null)
            {
                entrante.clip   = pista;
                entrante.volume = 0f;
                entrante.Play();
            }

            float objetivo = VolumenFinal();
            float vSaliente = saliente != null ? saliente.volume : 0f;

            if (duracion <= 0f)
            {
                if (saliente != null) { saliente.Stop(); saliente.volume = 0f; }
                if (pista != null) entrante.volume = objetivo;
            }
            else
            {
                float t = 0f;
                while (t < duracion)
                {
                    t += Time.unscaledDeltaTime;          // funciona con el juego en pausa
                    float k = Mathf.Clamp01(t / duracion);
                    if (pista != null)  entrante.volume = Mathf.Lerp(0f, objetivo, k);
                    if (saliente != null) saliente.volume = Mathf.Lerp(vSaliente, 0f, k);
                    yield return null;
                }
                if (saliente != null) { saliente.Stop(); saliente.volume = 0f; }
                if (pista != null) entrante.volume = objetivo;
            }

            if (pista != null) _activa = entrante;
            _fundido = null;
        }

        // ── Volumen ──────────────────────────────────────────────────────────
        private float VolumenFinal() => Mathf.Clamp01(_volumenBase * _volumenJugador);

        private void LeerVolumen()
        {
            _volumenJugador = PlayerPrefs.GetFloat(_claveVolumen, 1f);
        }

        /// <summary>Relee la preferencia y la aplica. Puede llamarse desde fuera.</summary>
        public void RefrescarVolumen()
        {
            LeerVolumen();
            if (_activa != null && _fundido == null) _activa.volume = VolumenFinal();
        }

        private IEnumerator VigilarVolumen()
        {
            var espera = new WaitForSecondsRealtime(_intervaloReleerVolumen);
            float ultimo = _volumenJugador;
            while (true)
            {
                yield return espera;
                float actual = PlayerPrefs.GetFloat(_claveVolumen, 1f);
                if (!Mathf.Approximately(actual, ultimo))
                {
                    ultimo = actual;
                    RefrescarVolumen();
                }
            }
        }
    }
}

using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace NeoFastRider.UI
{
    /// <summary>
    /// Runner Visor HUD — Speedometer + Tilt Indicator + Glitch Line feed.
    /// Pre-built km/h string table: zero GC in Update.
    /// </summary>
    public sealed class RunnerHUD : MonoBehaviour
    {
        [Header("Moto Reference")]
        [SerializeField] private NeoFastRider.Moto.MotoPhysics _motoPhysics;

        [Header("Speedometer")]
        [SerializeField] private TextMeshProUGUI _speedText;

        [Header("Tilt Indicator")]
        [SerializeField] private Slider _tiltSlider;

        [Header("Glitch Line")]
        [SerializeField] private GlitchLineController _glitchLine;

        private string[] _kmhTable;
        private int      _lastKmh = -1;

        private void Awake()
        {
            // Build 0-300 km/h lookup table once at startup — zero Update alloc
            _kmhTable = new string[301];
            for (int i = 0; i <= 300; i++)
                _kmhTable[i] = i.ToString() + " km/h";
        }

        private void Update()
        {
            if (_motoPhysics == null) return;

            // ── Speedometer ────────────────────────────────────────────────
            int kmh = Mathf.Clamp(Mathf.RoundToInt(_motoPhysics.CurrentKmh), 0, 300);
            if (_speedText != null && kmh != _lastKmh)
            {
                _speedText.text = _kmhTable[kmh];
                _lastKmh = kmh;
            }

            // ── Tilt Indicator: moto Z-rotation → slider 0..1 ──────────────
            if (_tiltSlider != null)
            {
                float z    = _motoPhysics.transform.eulerAngles.z;
                float norm = z <= 180f
                    ? 0.5f + (z / 180f) * 0.5f
                    : (z - 180f) / 180f * 0.5f;
                _tiltSlider.value = Mathf.Clamp01(norm);
            }

            // ── Feed km/h to glitch line ────────────────────────────────────
            _glitchLine?.SetCurrentKmh(kmh);
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NeoFastRider.UI
{
    /// <summary>
    /// Runner Visor HUD — Digital Speedometer, Glitch Line feed, Tilt Indicator.
    /// This is a legacy alias maintained for scene backward compatibility.
    /// New code should prefer RunnerHUD.cs.
    /// </summary>
    public sealed class RunnerVisorHUD : MonoBehaviour
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
            _kmhTable = new string[301];
            for (int i = 0; i <= 300; i++)
                _kmhTable[i] = i.ToString() + " km/h";
        }

        private void Update()
        {
            if (_motoPhysics == null) return;

            int kmh = Mathf.Clamp(Mathf.RoundToInt(_motoPhysics.CurrentKmh), 0, 300);
            if (_speedText != null && kmh != _lastKmh)
            {
                _speedText.text = _kmhTable[kmh];
                _lastKmh = kmh;
            }

            if (_tiltSlider != null)
            {
                float z = _motoPhysics.transform.eulerAngles.z;
                float n = z <= 180f ? 0.5f + (z / 180f) * 0.5f : (z - 180f) / 180f * 0.5f;
                _tiltSlider.value = Mathf.Clamp01(n);
            }

            _glitchLine?.SetCurrentKmh(kmh);
        }
    }
}
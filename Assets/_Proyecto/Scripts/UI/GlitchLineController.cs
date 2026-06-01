using UnityEngine;
using TMPro;

namespace NeoFastRider.UI
{
    /// <summary>
    /// Bottom-center predictive trajectory line.
    /// Below 120 km/h: clean neon dash separator.
    /// At/above 120 km/h: cycles digital noise frames. Zero heap alloc in Update.
    /// </summary>
    public sealed class GlitchLineController : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _lineText;
        [SerializeField] private float           _glitchThreshold = 120f;
        [SerializeField, Range(0.02f, 0.2f)] private float _glitchInterval = 0.07f;

        private const string NormalLine = "─────────────────────────────────────────";
        private static readonly string[] GlitchFrames =
        {
            "─ █ ─ ⚡ ─ 🏁 ─ █ ─ ⚡ ─ █ ─ 🏁 ─ █ ─",
            "█ ─ 🏁 ─ ⚡ ─ █ ─ ─ ─ 🏁 ─ █ ─ ⚡ ─ █",
            "⚡ █ ─ 🏁 ─ ─ █ ─ ⚡ ─ 🏁 ─ █ ─ ─ ⚡ ─",
            "─ 🏁 █ ─ ⚡ █ ─ ─ 🏁 ─ ⚡ ─ █ 🏁 ─ ─ █",
        };

        private int   _frame; private float _timer; private bool _glitching; private int _currentKmh;

        private void Awake() { if (_lineText != null) _lineText.text = NormalLine; }

        private void Update()
        {
            bool should = _currentKmh >= _glitchThreshold;
            if (should != _glitching)
            {
                _glitching = should;
                if (!_glitching && _lineText != null) _lineText.text = NormalLine;
            }
            if (_glitching)
            {
                _timer += Time.deltaTime;
                if (_timer >= _glitchInterval)
                {
                    _timer = 0f; _frame = (_frame + 1) % GlitchFrames.Length;
                    if (_lineText != null) _lineText.text = GlitchFrames[_frame];
                }
            }
        }

        public void SetCurrentKmh(int kmh) => _currentKmh = kmh;
    }
}
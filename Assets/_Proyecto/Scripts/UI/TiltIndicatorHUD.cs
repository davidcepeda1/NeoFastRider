using UnityEngine;
using UnityEngine.UI;

namespace NeoFastRider.UI
{
    /// <summary>
    /// Vertigo & Tilt Indicator HUD.
    /// A vertical Slider on the left edge of the screen whose value is
    /// driven by the motorcycle's local Z-rotation (camera roll/tilt).
    /// Value range: 0 = full left tilt, 0.5 = neutral, 1 = full right tilt.
    /// </summary>
    public sealed class TiltIndicatorHUD : MonoBehaviour
    {
        [SerializeField] private Slider _tiltSlider;
        [SerializeField] private float  _smoothSpeed = 8f;

        private float _targetValue = 0.5f;

        private void Awake()
        {
            if (_tiltSlider != null)
            {
                _tiltSlider.minValue = 0f;
                _tiltSlider.maxValue = 1f;
                _tiltSlider.value    = 0.5f;
            }
        }

        private void Update()
        {
            if (_tiltSlider == null) return;
            _tiltSlider.value = Mathf.Lerp(
                _tiltSlider.value, _targetValue, _smoothSpeed * Time.deltaTime);
        }

        /// <summary>Sets the target tilt value [0..1]. Called by RunnerVisorHUD every frame.</summary>
        public void SetValue(float normalized01)
        {
            _targetValue = Mathf.Clamp01(normalized01);
        }
    }
}

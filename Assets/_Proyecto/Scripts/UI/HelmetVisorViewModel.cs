using System;
using System.ComponentModel;
using System.Globalization;
using UnityEngine;

#if NOESIS
using Noesis;
#else
// Stubs para compilar sin NoesisGUI instalado — se eliminan una vez importado el paquete
using Point      = UnityEngine.Vector2;
using Visibility = System.Object;
#endif

namespace NeoFastRider.UI
{
    // ═══════════════════════════════════════════════════════════════════════
    //  HelmetVisorViewModel
    //  Fuente de datos WPF del Visor del Casco.
    //  Todos los campos son hilo-seguro desde Unity (main thread).
    // ═══════════════════════════════════════════════════════════════════════
    public sealed class HelmetVisorViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        // ── Constantes del arco tacómetro ───────────────────────────────
        // Centro (55,55) radio 48 · inicio 150° · barrido CW 240°
        private const double ArcCx         = 55.0;
        private const double ArcCy         = 55.0;
        private const double ArcRadius     = 48.0;
        private const double ArcStartDeg   = 150.0;
        private const double ArcSweepDeg   = 240.0;
        private const double ArcLargeThreshold = 0.75; // t > 0.75 → IsLargeArc

        // ────────────────────────────────────────────────────────────────
        //  VELOCIDAD
        // ────────────────────────────────────────────────────────────────
        private float _speedKmh;
        public float SpeedKmh
        {
            get => _speedKmh;
            set
            {
                if (Mathf.Approximately(_speedKmh, value)) return;
                _speedKmh = value;
                Notify(nameof(SpeedKmh));
                Notify(nameof(SpeedFormatted));
            }
        }

        // Tres dígitos sin decimales para el TextBlock del velocímetro
        public string SpeedFormatted =>
            Mathf.Clamp(Mathf.RoundToInt(_speedKmh), 0, 999).ToString("000");

        // ────────────────────────────────────────────────────────────────
        //  RPM NORMALIZADO [0..1] + geometría del arco
        // ────────────────────────────────────────────────────────────────
        private double _rpmNormalized;
        public double RPMNormalized
        {
            get => _rpmNormalized;
            set
            {
                value = Math.Max(0.001, Math.Min(1.0, value));
                if (Math.Abs(_rpmNormalized - value) < 0.002) return;
                _rpmNormalized = value;
                RecalcArc();
                Notify(nameof(RPMNormalized));
            }
        }

        // ────────────────────────────────────────────────────────────────
        //  TIEMPO RESTANTE
        // ────────────────────────────────────────────────────────────────
        private float _timeRemaining;
        public float TimeRemaining
        {
            get => _timeRemaining;
            set
            {
                if (Mathf.Approximately(_timeRemaining, value)) return;
                _timeRemaining = Mathf.Max(0f, value);
                Notify(nameof(TimeRemaining));
                Notify(nameof(TimeFormatted));
            }
        }

        // Formato MM:SS
        public string TimeFormatted
        {
            get
            {
                int total = Mathf.RoundToInt(_timeRemaining);
                return $"{total / 60:00}:{total % 60:00}";
            }
        }

        // ────────────────────────────────────────────────────────────────
        //  CORE ENERGY [0..1]
        // ────────────────────────────────────────────────────────────────
        private float _coreEnergy = 1f;
        public float CoreEnergy
        {
            get => _coreEnergy;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(_coreEnergy, value)) return;
                _coreEnergy = value;
                IsCritical  = value < 0.25f;
                Notify(nameof(CoreEnergy));
                Notify(nameof(CoreEnergyPercent));
            }
        }

        public float CoreEnergyPercent => _coreEnergy * 100f;

        // ────────────────────────────────────────────────────────────────
        //  ESTADO CRÍTICO (energía < 25 %)
        // ────────────────────────────────────────────────────────────────
        private bool _isCritical;
        public bool IsCritical
        {
            get => _isCritical;
            private set
            {
                if (_isCritical == value) return;
                _isCritical = value;
                Notify(nameof(IsCritical));
            }
        }

        // ────────────────────────────────────────────────────────────────
        //  SHAKE OFFSET — animado por corrutina en HelmetVisorController
        // ────────────────────────────────────────────────────────────────
        private double _shakeOffsetX;
        public double ShakeOffsetX
        {
            get => _shakeOffsetX;
            set { _shakeOffsetX = value; Notify(nameof(ShakeOffsetX)); }
        }

        private double _shakeOffsetY;
        public double ShakeOffsetY
        {
            get => _shakeOffsetY;
            set { _shakeOffsetY = value; Notify(nameof(ShakeOffsetY)); }
        }

        // ────────────────────────────────────────────────────────────────
        //  Helpers internos
        // ────────────────────────────────────────────────────────────────
        private void Notify(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        // Recalcula el endpoint del arco del tacómetro; invocado al cambiar RPM
        private void RecalcArc()
        {
            // Ángulo final: avanzamos CW desde startDeg en función del RPM
            double endDeg = ArcStartDeg + ArcSweepDeg * _rpmNormalized;
            double endRad = endDeg * Math.PI / 180.0;
            double ex = ArcCx + ArcRadius * Math.Cos(endRad);
            double ey = ArcCy + ArcRadius * Math.Sin(endRad);

            // Los converters en XAML usan RPMNormalized directamente.
            // Este método se mantiene para posible uso desde code-behind.
            _ = ex; _ = ey; // evita warning "unused" sin pragma
        }
    }

// ═══════════════════════════════════════════════════════════════════════
//  CONVERTERS — solo se compilan cuando NoesisGUI está instalado.
//  IValueConverter pertenece al ensamblado Noesis; sin él el tipo no existe.
//  Una vez importado el paquete NoesisGUI, #if NOESIS se activa y estos
//  tipos quedan disponibles para el XAML vía:
//  xmlns:hud="clr-namespace:NeoFastRider.UI;assembly=Assembly-CSharp"
// ═══════════════════════════════════════════════════════════════════════
#if NOESIS

    /// <summary>
    /// double RPMNormalized [0..1] → Noesis.Point (endpoint del arco)
    /// Centro (55,55) radio 48, barrido 240° CW desde 150°
    /// </summary>
    public sealed class RPMToArcPointConverter : Noesis.IValueConverter
    {
        private const double Cx       = 55.0;
        private const double Cy       = 55.0;
        private const double R        = 48.0;
        private const double StartDeg = 150.0;
        private const double SweepDeg = 240.0;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double t = System.Convert.ToDouble(value);
            t = Math.Max(0.001, Math.Min(1.0, t));

            double endDeg = StartDeg + SweepDeg * t;
            double endRad = endDeg * Math.PI / 180.0;
            double x = Cx + R * Math.Cos(endRad);
            double y = Cy + R * Math.Sin(endRad);
            return new Noesis.Point((float)x, (float)y);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// double RPMNormalized [0..1] → bool IsLargeArc
    /// True cuando el arco supera los 180° (RPM > 75 %)
    /// </summary>
    public sealed class RPMToIsLargeArcConverter : Noesis.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double t = System.Convert.ToDouble(value);
            return t > 0.75;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// bool → Noesis.Visibility (True = Visible, False = Collapsed)
    /// </summary>
    public sealed class BoolToVisibilityConverter : Noesis.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (bool)value ? Noesis.Visibility.Visible : Noesis.Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// double RPMNormalized [0..1] → Noesis.StreamGeometry
    /// Construye el arco del tacómetro usando StreamGeometryContext.ArcTo
    /// porque PathGeometry/PathFigure/ArcSegment no existen en esta versión de Noesis.
    /// Centro (55,55) radio 48 · inicio 150° CW · barrido 240°
    /// </summary>
    public sealed class RPMToArcGeometryConverter : Noesis.IValueConverter
    {
        private const float   Cx       = 55f;
        private const float   Cy       = 55f;
        private const float   R        = 48f;
        private const double  StartDeg = 150.0;
        private const double  SweepDeg = 240.0;

        // Punto de inicio fijo (calculado una sola vez)
        private static readonly Noesis.Point StartPt = new Noesis.Point(
            Cx + R * (float)Math.Cos(StartDeg * Math.PI / 180.0),
            Cy + R * (float)Math.Sin(StartDeg * Math.PI / 180.0)
        );

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double t = System.Convert.ToDouble(value);
            t = Math.Max(0.002, Math.Min(1.0, t));

            double endDeg = StartDeg + SweepDeg * t;
            double endRad = endDeg * Math.PI / 180.0;
            float  ex     = Cx + R * (float)Math.Cos(endRad);
            float  ey     = Cy + R * (float)Math.Sin(endRad);

            var sg = new Noesis.StreamGeometry();
            using (var ctx = sg.Open())
            {
                ctx.BeginFigure(StartPt, false, false);
                ctx.ArcTo(
                    new Noesis.Point(ex, ey),
                    new Noesis.Size(R, R),
                    0.0,
                    t > 0.75,
                    Noesis.SweepDirection.Clockwise,
                    true, true);
            }
            return sg;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

#endif // NOESIS
}

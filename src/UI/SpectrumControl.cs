using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using DeskOrder.Core;

namespace DeskOrder.UI
{
    public class SpectrumControl : FrameworkElement
    {
        private readonly DispatcherTimer _timer;
        private readonly float[] _smoothed = new float[AudioEngine.Bands];
        private readonly Brush _barBrush;
        private readonly Brush _barBrush2;
        private readonly Brush _waveBrush;
        private float _phase;

        public AudioEngine Audio { get; set; }
        public PlayerService Player { get; set; }
        public bool UseMock { get; set; }
        public float[] MockSpectrum { get; set; }
        public string VisualStyle { get; set; }
        public double Intensity { get; set; }
        public bool PlayerOnly { get; set; }

        public SpectrumControl()
        {
            VisualStyle = "bars";
            Intensity = 1.0;
            SnapsToDevicePixels = true;
            IsHitTestVisible = false;
            _barBrush = new LinearGradientBrush(Color.FromRgb(124, 155, 255), Color.FromRgb(87, 224, 200), new Point(0, 1), new Point(0, 0));
            _barBrush.Freeze();
            _barBrush2 = new SolidColorBrush(Color.FromArgb(110, 124, 155, 255));
            _barBrush2.Freeze();
            var wg = new LinearGradientBrush(Color.FromRgb(124, 155, 255), Color.FromRgb(87, 224, 200), new Point(0, 0), new Point(1, 0));
            wg.Freeze();
            _waveBrush = wg;
            _timer = new DispatcherTimer(DispatcherPriority.Render);
            _timer.Interval = TimeSpan.FromMilliseconds(33);
            _timer.Tick += delegate { _phase += 0.055f; InvalidateVisual(); };
            Loaded += delegate { if (IsVisible) _timer.Start(); };
            Unloaded += delegate { _timer.Stop(); };
            IsVisibleChanged += delegate { if (IsVisible) _timer.Start(); else _timer.Stop(); };
        }

        public void SetMock(float[] values)
        {
            MockSpectrum = values;
            UseMock = true;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 1 || h <= 1) return;
            float[] source = null;
            bool active = false;
            if (UseMock && MockSpectrum != null)
            {
                source = MockSpectrum;
                active = true;
            }
            else if (Audio != null)
            {
                if (PlayerOnly && (Player == null || !Player.IsPlaying))
                {
                    source = new float[AudioEngine.Bands];
                    active = false;
                }
                else
                {
                    active = Audio.IsActive;
                    source = Audio.GetSpectrum();
                }
            }
            if (source == null) source = new float[AudioEngine.Bands];
            bool hasEnergy = false;
            for (int i = 0; i < source.Length; i++) { if (source[i] > 0.02f) { hasEnergy = true; break; } }
            for (int i = 0; i < _smoothed.Length; i++)
            {
                float v = i < source.Length ? source[i] : 0;
                _smoothed[i] = Math.Max(v, _smoothed[i] * 0.80f + v * 0.20f);
            }

            bool wave = string.Equals(VisualStyle, "wave", StringComparison.OrdinalIgnoreCase);
            if (wave) DrawWave(dc, w, h, hasEnergy, active);
            else DrawBars(dc, w, h, hasEnergy, active);
        }

        private void DrawBars(DrawingContext dc, double w, double h, bool hasEnergy, bool active)
        {
            int n = _smoothed.Length;
            double gap = 2.0;
            double bw = Math.Max(1.5, (w - gap * (n - 1)) / n);
            double maxH = h - 2;
            for (int i = 0; i < n; i++)
            {
                double v = _smoothed[i] * Intensity;
                if (!active || !hasEnergy) v = IdleLevel(i) * 0.18;
                if (v < 0.025) v = 0.025;
                double bh = Math.Max(2, Math.Min(1, v) * maxH);
                double x = i * (bw + gap);
                var rect = new Rect(x, h - bh, bw, bh);
                Brush brush = i % 5 == 0 ? _barBrush2 : _barBrush;
                dc.DrawRoundedRectangle(brush, null, rect, Math.Min(2.5, bw / 2), Math.Min(2.5, bw / 2));
            }
        }

        private void DrawWave(DrawingContext dc, double w, double h, bool hasEnergy, bool active)
        {
            int n = _smoothed.Length;
            double step = w / (n - 1);
            double cy = h / 2;
            double amplitude = (h * 0.42) * Intensity;
            var geo = new StreamGeometry();
            using (StreamGeometryContext ctx = geo.Open())
            {
                bool started = false;
                for (int i = 0; i < n; i++)
                {
                    double v = _smoothed[i];
                    if (!active || !hasEnergy) v = IdleLevel(i) * 0.12;
                    double y = cy - Math.Min(1, v) * amplitude * (0.5 + 0.5 * Math.Sin(i * 0.35 + _phase));
                    if (!started) { ctx.BeginFigure(new Point(i * step, y), false, false); started = true; }
                    else ctx.LineTo(new Point(i * step, y), true, false);
                }
            }
            geo.Freeze();
            var pen = new Pen(_waveBrush, 2.2);
            pen.StartLineCap = PenLineCap.Round;
            pen.EndLineCap = PenLineCap.Round;
            dc.DrawGeometry(null, pen, geo);

            var geo2 = new StreamGeometry();
            using (StreamGeometryContext ctx = geo2.Open())
            {
                bool started = false;
                for (int i = 0; i < n; i++)
                {
                    double v = _smoothed[i];
                    if (!active || !hasEnergy) v = IdleLevel(i) * 0.12;
                    double y = cy + Math.Min(1, v) * amplitude * 0.6;
                    if (!started) { ctx.BeginFigure(new Point(i * step, y), false, false); started = true; }
                    else ctx.LineTo(new Point(i * step, y), true, false);
                }
            }
            geo2.Freeze();
            var pen2 = new Pen(_barBrush2, 1.2);
            dc.DrawGeometry(null, pen2, geo2);
        }

        private double IdleLevel(int i)
        {
            return 0.25 + 0.75 * Math.Abs(Math.Sin(i * 0.14 + _phase * 0.6));
        }
    }
}

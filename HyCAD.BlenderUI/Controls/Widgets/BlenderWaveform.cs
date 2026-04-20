using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HyCAD.BlenderUI.Controls.Widgets
{
    /// <summary>UI_BTYPE_WAVEFORM。</summary>
    public class BlenderWaveform : Control
    {
        static BlenderWaveform()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BlenderWaveform),
                new FrameworkPropertyMetadata(typeof(BlenderWaveform)));
        }

        public static readonly DependencyProperty SamplesProperty = DependencyProperty.Register(
            nameof(Samples), typeof(double[]), typeof(BlenderWaveform),
            new PropertyMetadata(null, (d, _) => { if (d is BlenderWaveform w) w.InvalidateVisual(); }));

        public double[] Samples
        {
            get => (double[])GetValue(SamplesProperty);
            set => SetValue(SamplesProperty, value);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            var s = Samples;
            if (s == null || s.Length < 2)
            {
                var rnd = new Random(1);
                s = Enumerable.Range(0, 256).Select(i => Math.Sin(i * 0.1) * 0.5 + rnd.NextDouble() * 0.05).ToArray();
            }

            double cy = h / 2;
            double amp = h * 0.45;
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(0, cy - s[0] * amp), false, false);
                for (int i = 1; i < s.Length; i++)
                {
                    double x = i / (double)(s.Length - 1) * w;
                    ctx.LineTo(new Point(x, cy - s[i] * amp), true, false);
                }
            }
            geo.Freeze();
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(Colors.LightGreen), 1), geo);
        }
    }
}

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HyCAD.BlenderUI.Controls.Widgets
{
    /// <summary>UI_BTYPE_HISTOGRAM。</summary>
    public class BlenderHistogram : Control
    {
        static BlenderHistogram()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BlenderHistogram),
                new FrameworkPropertyMetadata(typeof(BlenderHistogram)));
        }

        public static readonly DependencyProperty BinsProperty = DependencyProperty.Register(
            nameof(Bins), typeof(double[]), typeof(BlenderHistogram),
            new PropertyMetadata(null, (d, _) => { if (d is BlenderHistogram h) h.InvalidateVisual(); }));

        public double[] Bins
        {
            get => (double[])GetValue(BinsProperty);
            set => SetValue(BinsProperty, value);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            var w = ActualWidth;
            var h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            var bins = Bins;
            if (bins == null || bins.Length == 0)
            {
                var rnd = new Random(0);
                bins = Enumerable.Range(0, 64).Select(_ => rnd.NextDouble()).ToArray();
            }

            double max = bins.Max();
            if (max < 1e-9) max = 1;
            double bw = w / bins.Length;
            for (int i = 0; i < bins.Length; i++)
            {
                double bh = (bins[i] / max) * h * 0.9;
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x47, 0x72, 0xB3)), null,
                    new Rect(i * bw, h - bh, Math.Max(1, bw - 1), bh));
            }
        }
    }
}

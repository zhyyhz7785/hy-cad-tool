using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HyCAD.BlenderUI.Controls.Widgets
{
    /// <summary>UI_BTYPE_VECTORSCOPE。</summary>
    public class BlenderVectorscope : Control
    {
        static BlenderVectorscope()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BlenderVectorscope),
                new FrameworkPropertyMetadata(typeof(BlenderVectorscope)));
        }

        public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
            nameof(Points), typeof(Point[]), typeof(BlenderVectorscope),
            new PropertyMetadata(null, (d, _) => { if (d is BlenderVectorscope v) v.InvalidateVisual(); }));

        public Point[] Points
        {
            get => (Point[])GetValue(PointsProperty);
            set => SetValue(PointsProperty, value);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            double cx = w / 2;
            double cy = h / 2;
            double r = Math.Min(cx, cy) - 2;
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Colors.Gray), 1), new Point(cx, cy), r, r);

            var pts = Points;
            if (pts == null || pts.Length == 0)
            {
                var rnd = new Random(2);
                pts = new Point[200];
                for (int i = 0; i < pts.Length; i++)
                    pts[i] = new Point(rnd.NextDouble() * 2 - 1, rnd.NextDouble() * 2 - 1);
            }

            foreach (var p in pts)
            {
                double x = cx + p.X * r;
                double y = cy + p.Y * r;
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(120, 0, 255, 100)), null, new Point(x, y), 1.5, 1.5);
            }
        }
    }
}

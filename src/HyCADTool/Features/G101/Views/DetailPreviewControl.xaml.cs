using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using HyCADTool.Features.G101.Domain.Geometry;

namespace HyCADTool.Features.G101.Views
{
    public partial class DetailPreviewControl : UserControl
    {
        public static readonly DependencyProperty SketchProperty =
            DependencyProperty.Register(nameof(Sketch), typeof(DetailSketch), typeof(DetailPreviewControl),
                new PropertyMetadata(null, OnSketchChanged));

        private readonly Canvas _canvas = new Canvas
        {
            Background = new SolidColorBrush(Color.FromRgb(0x2B, 0x2B, 0x2B)),
            ClipToBounds = true
        };

        public DetailSketch Sketch
        {
            get => (DetailSketch)GetValue(SketchProperty);
            set => SetValue(SketchProperty, value);
        }

        public DetailPreviewControl()
        {
            Content = _canvas;
        }

        private static void OnSketchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DetailPreviewControl)d).Redraw();
        }

        private void Redraw()
        {
            _canvas.Children.Clear();
            var sketch = Sketch;
            if (sketch == null) return;

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            CollectBounds(sketch, ref minX, ref minY, ref maxX, ref maxY);
            if (minX >= maxX) return;

            double pad = 12;
            double cw = ActualWidth > 0 ? ActualWidth : 280;
            double ch = ActualHeight > 0 ? ActualHeight : 160;
            double sw = maxX - minX;
            double sh = maxY - minY;
            double scale = System.Math.Min((cw - 2 * pad) / sw, (ch - 2 * pad) / sh);

            foreach (var pl in sketch.Polylines)
            {
                if (pl.Points.Count < 2) continue;
                var poly = new Polyline { StrokeThickness = pl.LineKind == SketchLineKind.Rebar ? 2 : 1 };
                poly.Stroke = pl.LineKind == SketchLineKind.Rebar
                    ? Brushes.OrangeRed
                    : Brushes.LightGray;
                for (int i = 0; i < pl.Points.Count; i++)
                {
                    var p = pl.Points[i];
                    poly.Points.Add(ToScreen(p.X, p.Y, minX, minY, scale, ch, pad));
                }
                if (pl.Closed && pl.Points.Count > 0)
                    poly.Points.Add(ToScreen(pl.Points[0].X, pl.Points[0].Y, minX, minY, scale, ch, pad));
                _canvas.Children.Add(poly);
            }

            foreach (var t in sketch.Texts)
            {
                var tb = new TextBlock
                {
                    Text = t.Content,
                    Foreground = Brushes.White,
                    FontSize = System.Math.Max(8, t.Height * scale * 0.8)
                };
                var pt = ToScreen(t.Position.X, t.Position.Y, minX, minY, scale, ch, pad);
                Canvas.SetLeft(tb, pt.X);
                Canvas.SetTop(tb, pt.Y);
                _canvas.Children.Add(tb);
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            Redraw();
        }

        private static void CollectBounds(DetailSketch sketch, ref double minX, ref double minY, ref double maxX, ref double maxY)
        {
            foreach (var pl in sketch.Polylines)
                foreach (var p in pl.Points)
                {
                    minX = System.Math.Min(minX, p.X);
                    minY = System.Math.Min(minY, p.Y);
                    maxX = System.Math.Max(maxX, p.X);
                    maxY = System.Math.Max(maxY, p.Y);
                }
        }

        private static Point ToScreen(double x, double y, double minX, double minY, double scale, double ch, double pad)
            => new Point(pad + (x - minX) * scale, ch - pad - (y - minY) * scale);
    }
}
